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
            case AethraCommandIds.SeekBack10:
                _context.SeekBack10();
                return true;
            case AethraCommandIds.SeekForward30:
                _context.SeekForward30();
                return true;
            case AethraCommandIds.VolumeUp5:
                _context.VolumeUp5();
                return true;
            case AethraCommandIds.VolumeDown5:
                _context.VolumeDown5();
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
