using System;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Aethra.Services;

internal enum PlaybackActivityMode
{
    Active,
    Idle
}

internal sealed class PlaybackActivityController
{
    private const double PointerMovementThreshold = 2;
    private static readonly TimeSpan IgnoreMoveAfterIdleDelay = TimeSpan.FromMilliseconds(250);

    private readonly DispatcherTimer _idleTimer;
    private readonly Func<bool> _canEnterIdle;
    private Point _lastPointerPosition;
    private bool _hasLastPointerPosition;
    private bool _isEnabled;
    private DateTime _enteredIdleAtUtc = DateTime.MinValue;

    internal PlaybackActivityController(TimeSpan idleDelay, Func<bool> canEnterIdle)
    {
        _canEnterIdle = canEnterIdle;
        _idleTimer = new DispatcherTimer
        {
            Interval = idleDelay
        };
        _idleTimer.Tick += IdleTimer_Tick;
    }

    internal event EventHandler? ModeChanged;

    internal PlaybackActivityMode Mode { get; private set; } = PlaybackActivityMode.Active;

    internal bool IsEnabled => _isEnabled;

    internal void Start()
    {
        _isEnabled = true;
        _hasLastPointerPosition = false;
        MarkActive();
    }

    internal void Stop()
    {
        _isEnabled = false;
        _idleTimer.Stop();
        _hasLastPointerPosition = false;
        SetMode(PlaybackActivityMode.Active);
    }

    internal void MarkActive()
    {
        if (!_isEnabled)
            return;

        SetMode(PlaybackActivityMode.Active);
        RestartIdleTimer();
    }

    internal void NotifyPointerMoved(Point position)
    {
        if (!_isEnabled)
            return;

        if (!_hasLastPointerPosition)
        {
            _lastPointerPosition = position;
            _hasLastPointerPosition = true;

            if (DateTime.UtcNow - _enteredIdleAtUtc < IgnoreMoveAfterIdleDelay)
                return;

            MarkActive();
            return;
        }

        var xDelta = Math.Abs(position.X - _lastPointerPosition.X);
        var yDelta = Math.Abs(position.Y - _lastPointerPosition.Y);

        if (xDelta < PointerMovementThreshold && yDelta < PointerMovementThreshold)
            return;

        _lastPointerPosition = position;
        MarkActive();
    }

    private void RestartIdleTimer()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private void IdleTimer_Tick(object? sender, object e)
    {
        _idleTimer.Stop();

        if (!_isEnabled)
            return;

        if (!_canEnterIdle())
        {
            RestartIdleTimer();
            return;
        }

        _enteredIdleAtUtc = DateTime.UtcNow;
        SetMode(PlaybackActivityMode.Idle);
    }

    private void SetMode(PlaybackActivityMode mode)
    {
        if (Mode == mode)
            return;

        Mode = mode;
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
