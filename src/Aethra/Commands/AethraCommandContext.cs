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
        Action quit,
        Action quitWatchLater,
        Action seekBack5,
        Action seekForward5,
        Action seekBack10,
        Action seekForward30,
        Action seekBack60,
        Action seekForward60,
        Action seekBack300,
        Action seekForward300,
        Action volumeUp2,
        Action volumeDown2,
        Action volumeUp5,
        Action volumeDown5,
        Action volumeUp10,
        Action volumeDown10,
        Action toggleMute,
        Action escapeAction,
        Action markLoopA,
        Action markLoopB,
        Action resetLoop,
        Action cycleRepeat,
        Action toggleSubtitles,
        Action openFile,
        Action openFolder,
        Action openRecent,
        Action previousFile,
        Action nextFile,
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
        Quit = quit;
        QuitWatchLater = quitWatchLater;
        SeekBack5 = seekBack5;
        SeekForward5 = seekForward5;
        SeekBack10 = seekBack10;
        SeekForward30 = seekForward30;
        SeekBack60 = seekBack60;
        SeekForward60 = seekForward60;
        SeekBack300 = seekBack300;
        SeekForward300 = seekForward300;
        VolumeUp2 = volumeUp2;
        VolumeDown2 = volumeDown2;
        VolumeUp5 = volumeUp5;
        VolumeDown5 = volumeDown5;
        VolumeUp10 = volumeUp10;
        VolumeDown10 = volumeDown10;
        ToggleMute = toggleMute;
        EscapeAction = escapeAction;
        MarkLoopA = markLoopA;
        MarkLoopB = markLoopB;
        ResetLoop = resetLoop;
        CycleRepeat = cycleRepeat;
        ToggleSubtitles = toggleSubtitles;
        OpenFile = openFile;
        OpenFolder = openFolder;
        OpenRecent = openRecent;
        PreviousFile = previousFile;
        NextFile = nextFile;
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

    internal Action Quit { get; }

    internal Action QuitWatchLater { get; }

    internal Action SeekBack5 { get; }

    internal Action SeekForward5 { get; }

    internal Action SeekBack10 { get; }

    internal Action SeekForward30 { get; }

    internal Action SeekBack60 { get; }

    internal Action SeekForward60 { get; }

    internal Action SeekBack300 { get; }

    internal Action SeekForward300 { get; }

    internal Action VolumeUp2 { get; }

    internal Action VolumeDown2 { get; }

    internal Action VolumeUp5 { get; }

    internal Action VolumeDown5 { get; }

    internal Action VolumeUp10 { get; }

    internal Action VolumeDown10 { get; }

    internal Action ToggleMute { get; }

    internal Action EscapeAction { get; }

    internal Action MarkLoopA { get; }

    internal Action MarkLoopB { get; }

    internal Action ResetLoop { get; }

    internal Action CycleRepeat { get; }

    internal Action ToggleSubtitles { get; }

    internal Action OpenFile { get; }

    internal Action OpenFolder { get; }

    internal Action OpenRecent { get; }

    internal Action PreviousFile { get; }

    internal Action NextFile { get; }

    internal Action ShowPlaylist { get; }

    internal Action ShowTools { get; }

    internal Action ShowHelp { get; }

    internal Action ShowFavorites { get; }

    internal Action ToggleAdjustments { get; }

    internal Action ToggleCommandRail { get; }
}
