namespace Aethra.Commands;

internal static class AethraCommandIds
{
    internal const string BossKey = "aethra:boss-key";
    internal const string ToggleSettings = "aethra:settings";
    internal const string ToggleFullscreen = "aethra:fullscreen";
    internal const string TogglePlayPause = "aethra:play-pause";
    internal const string Quit = "aethra:quit";
    internal const string QuitWatchLater = "aethra:quit-watch-later";
    internal const string SeekBack5 = "aethra:seek-back-5";
    internal const string SeekForward5 = "aethra:seek-forward-5";
    internal const string SeekBack10 = "aethra:seek-back-10";
    internal const string SeekForward30 = "aethra:seek-forward-30";
    internal const string SeekBack60 = "aethra:seek-back-60";
    internal const string SeekForward60 = "aethra:seek-forward-60";
    internal const string SeekBack300 = "aethra:seek-back-300";
    internal const string SeekForward300 = "aethra:seek-forward-300";
    internal const string VolumeUp2 = "aethra:volume-up-2";
    internal const string VolumeDown2 = "aethra:volume-down-2";
    internal const string VolumeUp5 = "aethra:volume-up-5";
    internal const string VolumeDown5 = "aethra:volume-down-5";
    internal const string VolumeUp10 = "aethra:volume-up-10";
    internal const string VolumeDown10 = "aethra:volume-down-10";
    internal const string ToggleMute = "aethra:mute";
    internal const string ExitOverlayOrFullscreen = "aethra:escape";
    internal const string MarkLoopA = "aethra:ab-loop-mark-a";
    internal const string MarkLoopB = "aethra:ab-loop-mark-b";
    internal const string ResetLoop = "aethra:ab-loop-reset";
    internal const string CycleRepeat = "aethra:cycle-repeat";
    internal const string ToggleSubtitles = "aethra:toggle-subtitles";
    internal const string OpenFile = "aethra:open-file";
    internal const string OpenFolder = "aethra:open-folder";
    internal const string OpenRecent = "aethra:recent";
    internal const string PreviousFile = "aethra:previous-file";
    internal const string NextFile = "aethra:next-file";
    internal const string ShowPlaylist = "aethra:playlist";
    internal const string ShowTools = "aethra:tools";
    internal const string ShowHelp = "aethra:help";
    internal const string ShowFavorites = "aethra:favorites";
    internal const string ToggleAdjustments = "aethra:adjustments";
    internal const string ToggleCommandRail = "aethra:command-rail";

    internal static bool IsAethraCommand(string command)
    {
        return command.StartsWith("aethra:", System.StringComparison.Ordinal);
    }
}
