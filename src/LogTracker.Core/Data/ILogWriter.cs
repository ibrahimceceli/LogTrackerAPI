using LogTracker.Core.Pipeline;

namespace LogTracker.Core.Data;

/// <summary>Stores log entries and removes old ones.</summary>
public interface ILogWriter
{
    /// <summary>Inserts a batch of request logs in one round trip.</summary>
    Task WriteRequestsAsync(IReadOnlyList<RequestLogEntry> entries, CancellationToken cancellationToken);

    /// <summary>Inserts a batch of error events in one round trip.</summary>
    Task WriteErrorsAsync(IReadOnlyList<ErrorEventEntry> entries, CancellationToken cancellationToken);

    /// <summary>Deletes rows older than <paramref name="retentionDays"/> days.</summary>
    Task<PurgeResult> PurgeAsync(int retentionDays, CancellationToken cancellationToken);
}

/// <summary>Number of rows removed by a purge.</summary>
public sealed record PurgeResult
{
    /// <summary>Deleted request log rows.</summary>
    public int DeletedRequestLogs { get; init; }

    /// <summary>Deleted error event rows.</summary>
    public int DeletedErrorEvents { get; init; }
}
