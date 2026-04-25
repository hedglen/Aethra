using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Aethra.Native;

internal static class NativeMpvOpenGlSmokeRunner
{
    internal static async Task<NativeMpvOpenGlSmokeResult> RunAsync(
        string mediaPath,
        int width = 320,
        int height = 180,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (!File.Exists(mediaPath))
            throw new FileNotFoundException("The smoke-test media file was not found.", mediaPath);

        using var angle = AngleEglContext.CreatePbuffer(width, height);
        using var context = new NativeMpvContext();
        using var renderer = new NativeMpvOpenGlRenderer(context);

        var frameRequested = 0;
        var fileLoaded = false;
        var frameRendered = false;
        var shutdownReceived = false;

        context.SetOptionString("config", "no");
        context.SetOptionString("idle", "yes");
        context.SetOptionString("terminal", "no");
        context.SetOptionString("audio", "no");
        context.SetOptionString("vo", "libmpv");
        context.TrySetOptionString("osc", "no");
        context.SetOptionString("osd-level", "0");
        context.SetWakeupCallback(() => Interlocked.Exchange(ref frameRequested, 1));
        context.Initialize();

        angle.MakeCurrent();
        renderer.FrameRequested += (_, _) => Interlocked.Exchange(ref frameRequested, 1);
        renderer.Create();
        context.Command("loadfile", mediaPath, "replace");

        var maxWait = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < maxWait)
        {
            cancellationToken.ThrowIfCancellationRequested();

            context.DrainEvents(mpvEvent =>
            {
                if (mpvEvent.EventId == MpvNative.MpvEventId.FileLoaded)
                    fileLoaded = true;

                if (mpvEvent.EventId == MpvNative.MpvEventId.Shutdown)
                    shutdownReceived = true;
            });

            if (shutdownReceived)
                break;

            if (fileLoaded || Interlocked.Exchange(ref frameRequested, 0) == 1)
            {
                angle.MakeCurrent();

                if (renderer.Render(width, height))
                {
                    angle.SwapBuffers();
                    renderer.ReportSwap();
                    frameRendered = true;
                    break;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(15), cancellationToken);
        }

        stopwatch.Stop();

        return new NativeMpvOpenGlSmokeResult(
            fileLoaded,
            frameRendered,
            shutdownReceived,
            stopwatch.Elapsed,
            width,
            height,
            AngleNative.GetGlError());
    }
}
