namespace Aethra.Commands;

internal static class AethraCommandIds
{
    internal const string BossKey = "aethra:boss-key";
    internal const string ToggleSettings = "aethra:settings";
    internal const string ToggleFullscreen = "aethra:fullscreen";
    internal const string TogglePlayPause = "aethra:play-pause";
    internal const string SeekBack10 = "aethra:seek-back-10";
    internal const string SeekForward30 = "aethra:seek-forward-30";
    internal const string VolumeUp5 = "aethra:volume-up-5";
    internal const string VolumeDown5 = "aethra:volume-down-5";
    internal const string ExitOverlayOrFullscreen = "aethra:escape";
    internal const string MarkLoopA = "aethra:ab-loop-mark-a";
    internal const string MarkLoopB = "aethra:ab-loop-mark-b";
    internal const string ResetLoop = "aethra:ab-loop-reset";

    internal static bool IsAethraCommand(string command)
    {
        return command.StartsWith("aethra:", System.StringComparison.Ordinal);
    }
}
