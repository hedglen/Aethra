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
            default:
                return false;
        }
    }
}
