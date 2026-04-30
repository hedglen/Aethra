using System;
using Aethra.Input;
using Xunit;

namespace Aethra.Tests.Input;

public sealed class PrimaryClickTrackerTests
{
    [Fact]
    public void RegisterPrimaryClick_QueuesSingleThenFlushesAfterWindow()
    {
        var tracker = new PrimaryClickTracker(TimeSpan.FromMilliseconds(250));
        var start = DateTimeOffset.UtcNow;

        var decision = tracker.RegisterPrimaryClick(start);

        Assert.Equal(PrimaryClickDecision.QueueSingle, decision);
        Assert.False(tracker.TryFlushPendingSingleClick(start.AddMilliseconds(200)));
        Assert.True(tracker.TryFlushPendingSingleClick(start.AddMilliseconds(251)));
        Assert.False(tracker.TryFlushPendingSingleClick(start.AddMilliseconds(500)));
    }

    [Fact]
    public void RegisterPrimaryClick_SecondClickWithinWindowExecutesDouble()
    {
        var tracker = new PrimaryClickTracker(TimeSpan.FromMilliseconds(250));
        var start = DateTimeOffset.UtcNow;

        var first = tracker.RegisterPrimaryClick(start);
        var second = tracker.RegisterPrimaryClick(start.AddMilliseconds(120));

        Assert.Equal(PrimaryClickDecision.QueueSingle, first);
        Assert.Equal(PrimaryClickDecision.ExecuteDouble, second);
        Assert.False(tracker.HasPendingSingleClick);
    }

    [Fact]
    public void CancelPendingSingleClick_ClearsPendingState()
    {
        var tracker = new PrimaryClickTracker(TimeSpan.FromMilliseconds(250));
        var start = DateTimeOffset.UtcNow;

        _ = tracker.RegisterPrimaryClick(start);
        tracker.CancelPendingSingleClick();

        Assert.False(tracker.HasPendingSingleClick);
        Assert.False(tracker.TryFlushPendingSingleClick(start.AddMilliseconds(400)));
    }
}
