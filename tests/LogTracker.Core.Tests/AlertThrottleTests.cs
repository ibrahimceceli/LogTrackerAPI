using LogTracker.Core.Notifications;
using Microsoft.Extensions.Time.Testing;

namespace LogTracker.Core.Tests;

public sealed class AlertThrottleTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly FakeTimeProvider _time = new();
    private readonly AlertThrottle _throttle;

    public AlertThrottleTests() => _throttle = new AlertThrottle(_time);

    [Fact]
    public void Allows_one_alert_per_type_per_window_and_counts_the_skipped_ones()
    {
        Assert.True(_throttle.TryAcquire("System.TimeoutException", Window, out var first));
        Assert.Equal(0, first);

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.False(_throttle.TryAcquire("System.TimeoutException", Window, out _));
        Assert.False(_throttle.TryAcquire("System.TimeoutException", Window, out _));

        _time.Advance(TimeSpan.FromSeconds(50));
        Assert.True(_throttle.TryAcquire("System.TimeoutException", Window, out var skipped));
        Assert.Equal(2, skipped);
    }

    [Fact]
    public void Throttles_each_type_separately()
    {
        Assert.True(_throttle.TryAcquire("System.TimeoutException", Window, out _));
        Assert.True(_throttle.TryAcquire("System.ArgumentException", Window, out _));
    }

    [Fact]
    public void Zero_window_sends_every_alert()
    {
        Assert.True(_throttle.TryAcquire("System.TimeoutException", TimeSpan.Zero, out _));
        Assert.True(_throttle.TryAcquire("System.TimeoutException", TimeSpan.Zero, out _));
    }
}
