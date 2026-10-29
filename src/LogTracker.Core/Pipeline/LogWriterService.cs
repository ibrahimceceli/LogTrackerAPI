using LogTracker.Core.Data;
using LogTracker.Core.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Pipeline;

/// <summary>
/// Reads the queue in the background, writes entries to the database in batches and sends alerts.
/// Failures are logged and counted; they never reach a request.
/// </summary>
internal sealed class LogWriterService(
    LogQueue queue,
    ILogWriter writer,
    IErrorNotifier notifier,
    AlertThrottle alertThrottle,
    IOptions<LogTrackerOptions> options,
    TimeProvider timeProvider,
    ILogger<LogWriterService> logger) : BackgroundService
{
    // After a failed write the database is probably down; waiting a little avoids
    // a failed round trip (and a warning) for every single request.
    internal static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    private long _writtenCount;
    private long _failedCount;
    private long _reportedDropCount;

    /// <summary>Entries stored in the database.</summary>
    public long WrittenCount => Interlocked.Read(ref _writtenCount);

    /// <summary>Entries lost because the database write failed.</summary>
    public long FailedCount => Interlocked.Read(ref _failedCount);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                // The batch itself is not cancelled on shutdown; the command timeout bounds it.
                var succeeded = await ProcessBatchAsync(ReadBatch(), CancellationToken.None);
                ReportDroppedEntries();

                if (!succeeded)
                {
                    await Task.Delay(FailureBackoff, timeProvider, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown; whatever is still queued is flushed below.
        }

        await FlushAsync();
    }

    private async Task FlushAsync()
    {
        queue.Complete();
        using var timeout = new CancellationTokenSource(ShutdownFlushTimeout, timeProvider);

        while (!timeout.IsCancellationRequested)
        {
            var batch = ReadBatch();
            if (batch.Count == 0)
            {
                break;
            }

            await ProcessBatchAsync(batch, timeout.Token);
        }

        ReportDroppedEntries();
    }

    private List<LogEntry> ReadBatch()
    {
        var batch = new List<LogEntry>();
        while (batch.Count < options.Value.BatchSize && queue.Reader.TryRead(out var entry))
        {
            batch.Add(entry);
        }

        return batch;
    }

    private async Task<bool> ProcessBatchAsync(List<LogEntry> batch, CancellationToken cancellationToken)
    {
        var requests = batch.OfType<RequestLogEntry>().ToList();
        var errors = batch.OfType<ErrorEventEntry>().ToList();

        // Alerts run alongside the database write and do not depend on it,
        // so an unreachable log database still produces alerts.
        var alerts = SendAlertsAsync(errors, cancellationToken);

        var requestsWritten = await TryWriteAsync(requests, writer.WriteRequestsAsync, cancellationToken);
        var errorsWritten = await TryWriteAsync(errors, writer.WriteErrorsAsync, cancellationToken);

        await alerts;
        return requestsWritten && errorsWritten;
    }

    private async Task<bool> TryWriteAsync<TEntry>(
        List<TEntry> entries,
        Func<IReadOnlyList<TEntry>, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return true;
        }

        try
        {
            await write(entries, cancellationToken);
            Interlocked.Add(ref _writtenCount, entries.Count);
            return true;
        }
        catch (Exception exception)
        {
            // Not retried: new entries keep arriving while the database is down, and they matter more.
            Interlocked.Add(ref _failedCount, entries.Count);
            logger.LogWarning(exception, "Could not write {Count} {EntryType} entries to the log database; they were dropped.",
                entries.Count, typeof(TEntry).Name);
            return false;
        }
    }

    private async Task SendAlertsAsync(List<ErrorEventEntry> errors, CancellationToken cancellationToken)
    {
        var telegram = options.Value.Telegram;
        if (!telegram.Enabled)
        {
            return;
        }

        var window = TimeSpan.FromSeconds(telegram.ThrottleSeconds);
        foreach (var error in errors)
        {
            if (!alertThrottle.TryAcquire(error.ErrorType, window, out var suppressedCount))
            {
                continue;
            }

            try
            {
                await notifier.NotifyAsync(error, suppressedCount, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not send the alert for {ErrorType}.", error.ErrorType);
            }
        }
    }

    private void ReportDroppedEntries()
    {
        var dropped = queue.DroppedCount;
        if (dropped == _reportedDropCount)
        {
            return;
        }

        logger.LogWarning("{Count} log entries were dropped because the queue was full.", dropped - _reportedDropCount);
        _reportedDropCount = dropped;
    }
}
