using System;

namespace Aethra.Input;

internal enum PrimaryClickDecision
{
    None,
    QueueSingle,
    ExecuteDouble
}

internal sealed class PrimaryClickTracker
{
    private readonly TimeSpan _doubleClickWindow;
    private DateTimeOffset? _pendingSince;

    internal PrimaryClickTracker(TimeSpan doubleClickWindow)
    {
        _doubleClickWindow = doubleClickWindow;
    }

    internal bool HasPendingSingleClick => _pendingSince.HasValue;

    internal PrimaryClickDecision RegisterPrimaryClick(DateTimeOffset now)
    {
        if (_pendingSince is { } pendingSince && now - pendingSince <= _doubleClickWindow)
        {
            _pendingSince = null;
            return PrimaryClickDecision.ExecuteDouble;
        }

        _pendingSince = now;
        return PrimaryClickDecision.QueueSingle;
    }

    internal bool TryFlushPendingSingleClick(DateTimeOffset now)
    {
        if (_pendingSince is not { } pendingSince)
            return false;

        if (now - pendingSince < _doubleClickWindow)
            return false;

        _pendingSince = null;
        return true;
    }

    internal void CancelPendingSingleClick()
    {
        _pendingSince = null;
    }
}
