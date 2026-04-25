using System;

namespace Aethra.Commands;

internal sealed class AethraCommandContext
{
    internal AethraCommandContext(
        Action pausePlayback,
        Action minimizeWindow)
    {
        PausePlayback = pausePlayback;
        MinimizeWindow = minimizeWindow;
    }

    internal Action PausePlayback { get; }

    internal Action MinimizeWindow { get; }
}
