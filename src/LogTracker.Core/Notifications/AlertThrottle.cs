namespace LogTracker.Core.Notifications;

/// <summary>
/// Allows one alert per key (exception type) per time window, so a failing endpoint
/// called in a loop does not flood the chat.
/// </summary>
internal sealed class AlertThrottle(TimeProvider timeProvider)
{
    private readonly Dictionary<string, KeyState> _states = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>
    /// Returns true when an alert for <paramref name="key"/> may be sent now. In that case
    /// <paramref name="suppressedCount"/> tells how many alerts were skipped since the last one.
    /// </summary>
    public bool TryAcquire(string key, TimeSpan window, out int suppressedCount)
    {
        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (_states.TryGetValue(key, out var state) && now - state.LastSentAt < window)
            {
                state.SuppressedCount++;
                suppressedCount = 0;
                return false;
            }

            suppressedCount = state?.SuppressedCount ?? 0;
            _states[key] = new KeyState { LastSentAt = now };
            return true;
        }
    }

    private sealed class KeyState
    {
        public DateTimeOffset LastSentAt { get; init; }

        public int SuppressedCount { get; set; }
    }
}
