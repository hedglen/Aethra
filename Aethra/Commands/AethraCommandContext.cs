using System;

namespace Aethra.Commands;

internal sealed class AethraCommandContext
{
    internal AethraCommandContext(
        Action pausePlayback,
        Action minimizeWindow,
        Action toggleSettings,
        Action toggleFullscreen,
        Action togglePlayPause,
        Action seekBack10,
        Action seekForward30,
        Action volumeUp5,
        Action volumeDown5,
        Action escapeAction,
        Action markLoopA,
        Action markLoopB,
        Action resetLoop)
    {
        PausePlayback = pausePlayback;
        MinimizeWindow = minimizeWindow;
        ToggleSettings = toggleSettings;
        ToggleFullscreen = toggleFullscreen;
        TogglePlayPause = togglePlayPause;
        SeekBack10 = seekBack10;
        SeekForward30 = seekForward30;
        VolumeUp5 = volumeUp5;
        VolumeDown5 = volumeDown5;
        EscapeAction = escapeAction;
        MarkLoopA = markLoopA;
        MarkLoopB = markLoopB;
        ResetLoop = resetLoop;
    }

    internal Action PausePlayback { get; }

    internal Action MinimizeWindow { get; }

    internal Action ToggleSettings { get; }

    internal Action ToggleFullscreen { get; }

    internal Action TogglePlayPause { get; }

    internal Action SeekBack10 { get; }

    internal Action SeekForward30 { get; }

    internal Action VolumeUp5 { get; }

    internal Action VolumeDown5 { get; }

    internal Action EscapeAction { get; }

    internal Action MarkLoopA { get; }

    internal Action MarkLoopB { get; }

    internal Action ResetLoop { get; }
}
