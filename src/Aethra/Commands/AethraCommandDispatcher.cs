namespace Aethra.Commands;

internal sealed class AethraCommandDispatcher
{
    private readonly AethraCommandContext _context;

    internal AethraCommandDispatcher(AethraCommandContext context)
    {
        _context = context;
    }

    internal bool Execute(string command)
    {
        switch (command)
        {
            case AethraCommandIds.BossKey:
                _context.PausePlayback();
                _context.MinimizeWindow();
                return true;
            case AethraCommandIds.ToggleSettings:
                _context.ToggleSettings();
                return true;
            case AethraCommandIds.ToggleFullscreen:
                _context.ToggleFullscreen();
                return true;
            case AethraCommandIds.TogglePlayPause:
                _context.TogglePlayPause();
                return true;
            case AethraCommandIds.Quit:
                _context.Quit();
                return true;
            case AethraCommandIds.QuitWatchLater:
                _context.QuitWatchLater();
                return true;
            case AethraCommandIds.SeekBack5:
                _context.SeekBack5();
                return true;
            case AethraCommandIds.SeekForward5:
                _context.SeekForward5();
                return true;
            case AethraCommandIds.SeekBack10:
                _context.SeekBack10();
                return true;
            case AethraCommandIds.SeekForward30:
                _context.SeekForward30();
                return true;
            case AethraCommandIds.SeekBack60:
                _context.SeekBack60();
                return true;
            case AethraCommandIds.SeekForward60:
                _context.SeekForward60();
                return true;
            case AethraCommandIds.SeekBack300:
                _context.SeekBack300();
                return true;
            case AethraCommandIds.SeekForward300:
                _context.SeekForward300();
                return true;
            case AethraCommandIds.VolumeUp2:
                _context.VolumeUp2();
                return true;
            case AethraCommandIds.VolumeDown2:
                _context.VolumeDown2();
                return true;
            case AethraCommandIds.VolumeUp5:
                _context.VolumeUp5();
                return true;
            case AethraCommandIds.VolumeDown5:
                _context.VolumeDown5();
                return true;
            case AethraCommandIds.VolumeUp10:
                _context.VolumeUp10();
                return true;
            case AethraCommandIds.VolumeDown10:
                _context.VolumeDown10();
                return true;
            case AethraCommandIds.ToggleMute:
                _context.ToggleMute();
                return true;
            case AethraCommandIds.ExitOverlayOrFullscreen:
                _context.EscapeAction();
                return true;
            case AethraCommandIds.MarkLoopA:
                _context.MarkLoopA();
                return true;
            case AethraCommandIds.MarkLoopB:
                _context.MarkLoopB();
                return true;
            case AethraCommandIds.ResetLoop:
                _context.ResetLoop();
                return true;
            case AethraCommandIds.ToggleLoopFile:
                _context.ToggleLoopFile();
                return true;
            case AethraCommandIds.OpenFile:
                _context.OpenFile();
                return true;
            case AethraCommandIds.OpenFolder:
                _context.OpenFolder();
                return true;
            case AethraCommandIds.OpenRecent:
                _context.OpenRecent();
                return true;
            case AethraCommandIds.ShowPlaylist:
                _context.ShowPlaylist();
                return true;
            case AethraCommandIds.ShowTools:
                _context.ShowTools();
                return true;
            case AethraCommandIds.ShowHelp:
                _context.ShowHelp();
                return true;
            case AethraCommandIds.ShowFavorites:
                _context.ShowFavorites();
                return true;
            case AethraCommandIds.ToggleAdjustments:
                _context.ToggleAdjustments();
                return true;
            case AethraCommandIds.ToggleCommandRail:
                _context.ToggleCommandRail();
                return true;
            default:
                return false;
        }
    }
}
