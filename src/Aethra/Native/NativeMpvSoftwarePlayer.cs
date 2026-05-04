using Aethra.Configuration;
using Aethra.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace Aethra.Native;

internal sealed partial class NativeMpvSoftwarePlayer : INativeMpvPlayerBackend
{
    private const int FrameWidth = 640;
    private const int FrameHeight = 360;
    private const int BytesPerPixel = 4;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action<WriteableBitmap> _frameReady;
    private readonly ConcurrentQueue<string[]> _commands = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _playerTask;
    private int _frameRequested = 1;
    private int _presentationQueued;
    private bool _disposed;
    private WriteableBitmap? _bitmap;
    private byte[]? _frameScratch;
    private double _position;
    private double _duration;

    internal NativeMpvSoftwarePlayer(DispatcherQueue dispatcherQueue, Action<WriteableBitmap> frameReady)
    {
        _dispatcherQueue = dispatcherQueue;
        _frameReady = frameReady;
        _playerTask = Task.Run(() => RunAsync(_cancellationTokenSource.Token));
    }

    public event EventHandler<NativeMpvPlaybackProgress>? ProgressChanged;
    public event EventHandler<bool>? PlaybackPausedChanged;
    public event EventHandler<IReadOnlyList<MpvChapter>>? ChaptersChanged;
    public event EventHandler<int>? PlaylistCountChanged;
    public event EventHandler? PlaybackEnded;

    public void LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnqueueCommand("loadfile", path, "replace");
    }

    public void TogglePause()
    {
        EnqueueCommand("cycle", "pause");
    }

    public void Pause()
    {
        EnqueueCommand("set", "pause", "yes");
    }

    public void SetProperty(string name, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnqueueCommand("set", name, value.ToString(CultureInfo.InvariantCulture));
    }

    public void SetProperty(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        EnqueueCommand("set", name, value);
    }

    public void ExecuteCommand(params string[] args)
    {
        if (args is null || args.Length == 0)
            return;

        EnqueueCommand(args);
    }

    public void Seek(double seconds)
    {
        EnqueueCommand("seek", seconds.ToString(CultureInfo.InvariantCulture));
    }

    public void SeekToTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            return;

        var clamped = Math.Max(0.0, seconds);
        EnqueueCommand("seek", clamped.ToString(CultureInfo.InvariantCulture), "absolute");
    }

    public void SeekToPercent(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
            return;

        var clamped = Math.Clamp(percent, 0.0, 100.0);
        EnqueueCommand("seek", clamped.ToString(CultureInfo.InvariantCulture), "absolute-percent");
    }

    public void SetVolume(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return;

        var clamped = Math.Clamp(value, 0.0, 100.0);
        EnqueueCommand("set", "volume", clamped.ToString(CultureInfo.InvariantCulture));
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

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var context = new NativeMpvContext();
            using var renderer = new NativeMpvSoftwareRenderer(context);
            using var frame = new NativeMpvSoftwareFrame(FrameWidth, FrameHeight);

            context.SetOptionString("config", "no");
            context.SetOptionString("idle", "yes");
            context.SetOptionString("terminal", "no");
            context.SetOptionString("vo", "libmpv");
            context.TrySetOptionString("osc", "no");
            context.SetOptionString("osd-level", "0");
            context.TrySetOptionString("input-default-bindings", "no");
            context.TrySetOptionString("input-vo-keyboard", "no");
            ApplyRuntimeBootstrapOptions(context);
            context.SetWakeupCallback(() => Interlocked.Exchange(ref _frameRequested, 1));
            context.Initialize();
            context.ObserveProperty(1, "time-pos", MpvNative.MpvFormat.Double);
            context.ObserveProperty(2, "duration", MpvNative.MpvFormat.Double);
            context.ObserveProperty(3, "pause", MpvNative.MpvFormat.Flag);
            context.ObserveProperty(4, "chapter-list/count", MpvNative.MpvFormat.Int64);
            context.ObserveProperty(5, "playlist/count", MpvNative.MpvFormat.Int64);
            context.ObserveProperty(6, "eof-reached", MpvNative.MpvFormat.Flag);

            renderer.FrameRequested += (_, _) => Interlocked.Exchange(ref _frameRequested, 1);
            renderer.Create();

            while (!cancellationToken.IsCancellationRequested)
            {
                DrainCommands(context);
                context.DrainEvents(HandleMpvEvent);

                if (Interlocked.Exchange(ref _frameRequested, 0) == 1 && renderer.Render(frame))
                {
                    QueueFramePresentation(frame);
                    renderer.ReportSwap();
                }

                await Task.Delay(TimeSpan.FromMilliseconds(15), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void HandleMpvEvent(NativeMpvContext context, MpvNative.MpvEvent mpvEvent)
    {
        if (mpvEvent.EventId == MpvNative.MpvEventId.FileLoaded)
        {
            RefreshChapters(context);
            RefreshPlaylistCount(context);
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

        if (mpvEvent.ReplyUserData == 5 && property.Format == MpvNative.MpvFormat.Int64)
        {
            var playlistCount = (int)Math.Max(0, Marshal.PtrToStructure<long>(property.Data));
            QueuePlaylistCountChanged(playlistCount);
            return;
        }

        if (mpvEvent.ReplyUserData == 6 && property.Format == MpvNative.MpvFormat.Flag)
        {
            var reachedEndOfFile = Marshal.PtrToStructure<int>(property.Data) != 0;
            if (reachedEndOfFile)
                QueuePlaybackEnded();

            return;
        }

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

    private void RefreshPlaylistCount(NativeMpvContext context)
    {
        var countText = context.GetPropertyString("playlist/count");
        if (!int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0)
            count = 0;

        QueuePlaylistCountChanged(count);
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
            var result = context.TryCommand(command);
            if (result < 0)
            {
                var commandText = command.Length == 0 ? "<empty>" : string.Join(" ", command);
                System.Diagnostics.Debug.WriteLine(
                    $"Ignored mpv command failure in software backend: {commandText} (error {result}).");
            }
        }
    }

    private void QueueFramePresentation(NativeMpvSoftwareFrame frame)
    {
        if (Interlocked.Exchange(ref _presentationQueued, 1) == 1)
            return;

        var pixels = CopyFrame(frame);
        if (!_dispatcherQueue.TryEnqueue(() => PresentFrame(frame.Width, frame.Height, pixels)))
            Interlocked.Exchange(ref _presentationQueued, 0);
    }

    private void PresentFrame(int width, int height, byte[] pixels)
    {
        try
        {
            _bitmap ??= new WriteableBitmap(width, height);

            using var stream = _bitmap.PixelBuffer.AsStream();
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(pixels, 0, pixels.Length);
            _bitmap.Invalidate();
            _frameReady(_bitmap);
        }
        finally
        {
            Interlocked.Exchange(ref _presentationQueued, 0);
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

    private void QueuePlaylistCountChanged(int playlistCount)
    {
        _dispatcherQueue.TryEnqueue(() => PlaylistCountChanged?.Invoke(this, playlistCount));
    }

    private void QueuePlaybackEnded()
    {
        _dispatcherQueue.TryEnqueue(() => PlaybackEnded?.Invoke(this, EventArgs.Empty));
    }

    // Reuse is safe because QueueFramePresentation's _presentationQueued gate keeps
    // only one frame in flight until PresentFrame finishes reading the buffer.
    private byte[] CopyFrame(NativeMpvSoftwareFrame frame)
    {
        var rowBytes = frame.Width * BytesPerPixel;
        var required = rowBytes * frame.Height;
        var pixels = _frameScratch ??= new byte[required];
        if (pixels.Length < required)
            pixels = _frameScratch = new byte[required];

        for (var row = 0; row < frame.Height; row++)
        {
            Marshal.Copy(
                frame.Buffer + row * frame.Stride,
                pixels,
                row * rowBytes,
                rowBytes);
        }

        return pixels;
    }

    private static void ApplyRuntimeBootstrapOptions(NativeMpvContext context)
    {
        var scriptsEnabled = ScriptExtensionSettingsStore.ScriptsEnabled;
        context.TrySetOptionString("load-scripts", scriptsEnabled ? "yes" : "no");

        var scriptsFolder = ScriptExtensionSettingsStore.ResolveEffectiveScriptsFolder(
            MpvRuntimeBootstrapSettings.Instance.PortableConfigDirectory);
        if (!string.IsNullOrWhiteSpace(scriptsFolder))
            context.TrySetOptionString("scripts", scriptsFolder);

        var imported = MpvRuntimeBootstrapSettings.Instance.ImportedMpvOptions;
        TryApplyImportedOption(context, imported, "video-sync");
        TryApplyImportedOption(context, imported, "tscale");
        TryApplyImportedOption(context, imported, "target-peak");
    }

    private static void TryApplyImportedOption(NativeMpvContext context, IReadOnlyDictionary<string, string> options, string key)
    {
        if (options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            context.TrySetOptionString(key, value);
    }
}
