using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;

namespace Aethra.Services;

internal sealed class VideoAdjustmentBatcher : IDisposable
{
    private readonly Dictionary<string, double> _pending = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _timer;
    private readonly Action<string, double> _apply;

    internal VideoAdjustmentBatcher(TimeSpan flushInterval, Action<string, double> apply)
    {
        _apply = apply;
        _timer = new DispatcherTimer
        {
            Interval = flushInterval
        };
        _timer.Tick += Timer_Tick;
    }

    internal void Queue(string mpvProperty, double value)
    {
        _pending[mpvProperty] = value;

        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _pending.Clear();
    }

    private void Timer_Tick(object? sender, object e)
    {
        if (_pending.Count == 0)
        {
            _timer.Stop();
            return;
        }

        var snapshot = _pending.ToArray();
        _pending.Clear();

        foreach (var adjustment in snapshot)
            _apply(adjustment.Key, adjustment.Value);
    }
}
