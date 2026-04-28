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
        Action toggleMute,
        Action escapeAction,
        Action markLoopA,
        Action markLoopB,
        Action resetLoop,
        Action openFile,
        Action openFolder,
        Action openRecent,
        Action showPlaylist,
        Action showTools,
        Action showHelp,
        Action showFavorites,
        Action toggleAdjustments,
        Action toggleCommandRail)
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
        ToggleMute = toggleMute;
        EscapeAction = escapeAction;
        MarkLoopA = markLoopA;
        MarkLoopB = markLoopB;
        ResetLoop = resetLoop;
        OpenFile = openFile;
        OpenFolder = openFolder;
        OpenRecent = openRecent;
        ShowPlaylist = showPlaylist;
        ShowTools = showTools;
        ShowHelp = showHelp;
        ShowFavorites = showFavorites;
        ToggleAdjustments = toggleAdjustments;
        ToggleCommandRail = toggleCommandRail;
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

    internal Action ToggleMute { get; }

    internal Action EscapeAction { get; }

    internal Action MarkLoopA { get; }

    internal Action MarkLoopB { get; }

    internal Action ResetLoop { get; }

    internal Action OpenFile { get; }

    internal Action OpenFolder { get; }

    internal Action OpenRecent { get; }

    internal Action ShowPlaylist { get; }

    internal Action ShowTools { get; }

    internal Action ShowHelp { get; }

    internal Action ShowFavorites { get; }

    internal Action ToggleAdjustments { get; }

    internal Action ToggleCommandRail { get; }
}
