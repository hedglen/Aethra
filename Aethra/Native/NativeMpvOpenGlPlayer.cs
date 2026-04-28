using Aethra.Native;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Aethra;

internal sealed class NativeMpvOpenGlPlayer : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action<Exception> _failed;
    private readonly AngleD3D11SwapChainContext _angleContext;
    private readonly ConcurrentQueue<string[]> _commands = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _playerTask;
    private int _frameRequested = 1;
    private bool _disposed;
    private int _width;
    private int _height;
    private readonly object _resizeLock = new();
    private int _pendingWidth;
    private int _pendingHeight;
    private double _position;
    private double _duration;

    internal NativeMpvOpenGlPlayer(DispatcherQueue dispatcherQueue, SwapChainPanel panel, Action<Exception> failed)
    {
        _dispatcherQueue = dispatcherQueue;
        _failed = failed;
        _width = Math.Max(1, (int)Math.Ceiling(panel.ActualWidth));
        _height = Math.Max(1, (int)Math.Ceiling(panel.ActualHeight));
        _angleContext = AngleD3D11SwapChainContext.Create(panel, _width, _height);
        _playerTask = Task.Factory.StartNew(
            () => Run(_cancellationTokenSource.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    internal event EventHandler<NativeMpvPlaybackProgress>? ProgressChanged;
    internal event EventHandler<bool>? PlaybackPausedChanged;
    internal event EventHandler<IReadOnlyList<MpvChapter>>? ChaptersChanged;

    internal void LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnqueueCommand("loadfile", path, "replace");
    }

    internal void TogglePause()
    {
        EnqueueCommand("cycle", "pause");
    }

    internal void Pause()
    {
        EnqueueCommand("set", "pause", "yes");
    }

    internal void SetProperty(string name, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnqueueCommand("set", name, value.ToString(CultureInfo.InvariantCulture));
    }

    internal void SetProperty(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        EnqueueCommand("set", name, value);
    }

    internal void Seek(double seconds)
    {
        EnqueueCommand("seek", seconds.ToString(CultureInfo.InvariantCulture));
    }

    internal void SeekToTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            return;

        var clamped = Math.Max(0.0, seconds);
        EnqueueCommand("seek", clamped.ToString(CultureInfo.InvariantCulture), "absolute");
    }

    internal void SeekToPercent(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
            return;

        var clamped = Math.Clamp(percent, 0.0, 100.0);
        EnqueueCommand("seek", clamped.ToString(CultureInfo.InvariantCulture), "absolute-percent");
    }

    internal void AddVolume(int amount)
    {
        EnqueueCommand("add", "volume", amount.ToString(CultureInfo.InvariantCulture));
    }

    internal void SetVolume(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return;

        var clamped = Math.Clamp(value, 0.0, 100.0);
        EnqueueCommand("set", "volume", clamped.ToString(CultureInfo.InvariantCulture));
    }

    internal void RequestRender()
    {
        Interlocked.Exchange(ref _frameRequested, 1);
    }

    internal void RequestResize(int width, int height)
    {
        if (_disposed || width <= 0 || height <= 0)
            return;

        lock (_resizeLock)
        {
            _pendingWidth = width;
            _pendingHeight = height;
        }

        Interlocked.Exchange(ref _frameRequested, 1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cancellationTokenSource.Cancel();
        _ = _playerTask.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Dispose(),
            _cancellationTokenSource,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        _disposed = true;
    }

    private void Run(CancellationToken cancellationToken)
    {
        try
        {
            _angleContext.MakeCurrent();

            using var context = new NativeMpvContext();
            using var renderer = new NativeMpvOpenGlRenderer(context);

            context.SetOptionString("config", "no");
            context.SetOptionString("idle", "yes");
            context.SetOptionString("terminal", "no");
            context.SetOptionString("vo", "libmpv");
            context.TrySetOptionString("hwdec", "auto-safe");
            context.TrySetOptionString("osc", "no");
            context.SetOptionString("osd-level", "0");
            context.TrySetOptionString("input-default-bindings", "no");
            context.TrySetOptionString("input-vo-keyboard", "no");
            context.SetWakeupCallback(() => Interlocked.Exchange(ref _frameRequested, 1));
            context.Initialize();
            context.ObserveProperty(1, "time-pos", MpvNative.MpvFormat.Double);
            context.ObserveProperty(2, "duration", MpvNative.MpvFormat.Double);
            context.ObserveProperty(3, "pause", MpvNative.MpvFormat.Flag);
            context.ObserveProperty(4, "chapter-list/count", MpvNative.MpvFormat.Int64);

            renderer.FrameRequested += (_, _) => Interlocked.Exchange(ref _frameRequested, 1);
            renderer.Create();
            EnqueueCommand("loadfile", @"C:\Users\rjh\Videos\test.mp4", "replace");

            Action<MpvNative.MpvEvent> eventHandler = mpvEvent => HandleMpvEvent(context, mpvEvent);
            while (!cancellationToken.IsCancellationRequested)
            {
                ApplyPendingResize();

                DrainCommands(context);
                context.DrainEvents(eventHandler);

                if (Interlocked.Exchange(ref _frameRequested, 0) == 1)
                    RenderFrame(renderer, requireFrameUpdate: false);

                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(8)))
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _dispatcherQueue.TryEnqueue(() => _failed(ex));
        }
        finally
        {
            _angleContext.Dispose();
        }
    }

    private void ApplyPendingResize()
    {
        int width;
        int height;

        lock (_resizeLock)
        {
            width = _pendingWidth;
            height = _pendingHeight;
            _pendingWidth = 0;
            _pendingHeight = 0;
        }

        if (width == 0 || height == 0)
            return;

        if (width == _width && height == _height)
            return;

        _angleContext.Resize(width, height);
        _width = width;
        _height = height;
        Interlocked.Exchange(ref _frameRequested, 1);
    }

    private void RenderFrame(NativeMpvOpenGlRenderer renderer, bool requireFrameUpdate)
    {
        _angleContext.MakeCurrent();

        _angleContext.SetViewport(_width, _height);

        if (renderer.Render(_width, _height, flipY: false, requireFrameUpdate))
        {
            _angleContext.SwapBuffers();
            renderer.ReportSwap();
        }
    }

    private void HandleMpvEvent(NativeMpvContext context, MpvNative.MpvEvent mpvEvent)
    {
        if (mpvEvent.EventId == MpvNative.MpvEventId.FileLoaded)
        {
            RefreshChapters(context);
            return;
        }

        if (mpvEvent.EventId != MpvNative.MpvEventId.PropertyChange || mpvEvent.Data == IntPtr.Zero)
            return;

        var property = Marshal.PtrToStructure<MpvNative.MpvEventProperty>(mpvEvent.Data);

        if (mpvEvent.ReplyUserData == 4)
        {
            // chapter-list/count: refresh the chapter list whenever it changes,
            // including the count going from zero to non-zero on file load.
            RefreshChapters(context);
            return;
        }

        if (property.Data == IntPtr.Zero)
            return;

        if (mpvEvent.ReplyUserData == 3 && property.Format == MpvNative.MpvFormat.Flag)
        {
            var isPaused = Marshal.PtrToStructure<int>(property.Data) != 0;
            QueuePlaybackPausedChanged(isPaused);
            return;
        }

        if (property.Format != MpvNative.MpvFormat.Double)
            return;

        var value = Marshal.PtrToStructure<double>(property.Data);
        if (double.IsNaN(value) || double.IsInfinity(value))
            return;

        if (mpvEvent.ReplyUserData == 1)
            _position = Math.Max(0, value);
        else if (mpvEvent.ReplyUserData == 2)
            _duration = Math.Max(0, value);
        else
            return;

        QueueProgressChanged();
    }

    private void RefreshChapters(NativeMpvContext context)
    {
        var countText = context.GetPropertyString("chapter-list/count");
        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            QueueChaptersChanged(Array.Empty<MpvChapter>());
            return;
        }

        var chapters = new List<MpvChapter>(count);
        for (var i = 0; i < count; i++)
        {
            var timeText = context.GetPropertyString($"chapter-list/{i}/time");
            if (!double.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                continue;

            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0)
                continue;

            var title = context.GetPropertyString($"chapter-list/{i}/title");
            chapters.Add(new MpvChapter(time, string.IsNullOrEmpty(title) ? null : title));
        }

        QueueChaptersChanged(chapters);
    }

    private void EnqueueCommand(params string[] args)
    {
        if (_disposed)
            return;

        _commands.Enqueue(args);
        Interlocked.Exchange(ref _frameRequested, 1);
    }

    private void DrainCommands(NativeMpvContext context)
    {
        while (_commands.TryDequeue(out var command))
        {
            context.Command(command);
        }
    }

    private void QueueProgressChanged()
    {
        if (_duration <= 0)
            return;

        var progress = new NativeMpvPlaybackProgress(_position, _duration);
        _dispatcherQueue.TryEnqueue(() => ProgressChanged?.Invoke(this, progress));
    }

    private void QueuePlaybackPausedChanged(bool isPaused)
    {
        _dispatcherQueue.TryEnqueue(() => PlaybackPausedChanged?.Invoke(this, isPaused));
    }

    private void QueueChaptersChanged(IReadOnlyList<MpvChapter> chapters)
    {
        _dispatcherQueue.TryEnqueue(() => ChaptersChanged?.Invoke(this, chapters));
    }
}
