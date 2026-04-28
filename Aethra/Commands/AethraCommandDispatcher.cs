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
            default:
                return false;
        }
    }
}
