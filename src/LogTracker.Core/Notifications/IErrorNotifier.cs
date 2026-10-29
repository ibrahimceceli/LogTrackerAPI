using LogTracker.Core.Pipeline;

namespace LogTracker.Core.Notifications;

/// <summary>Sends an alert about an unhandled exception to a person.</summary>
public interface IErrorNotifier
{
    /// <summary>Sends one alert. Throws when the alert could not be delivered.</summary>
    /// <param name="error">The exception that was recorded.</param>
    /// <param name="suppressedCount">Alerts of the same type skipped by throttling since the last one sent.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task NotifyAsync(ErrorEventEntry error, int suppressedCount, CancellationToken cancellationToken);
}
