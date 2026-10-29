using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Data;

/// <summary>Deletes rows older than <see cref="LogTrackerOptions.RetentionDays"/> at startup and then once a day.</summary>
internal sealed class LogRetentionService(
    ILogWriter writer,
    IOptions<LogTrackerOptions> options,
    TimeProvider timeProvider,
    ILogger<LogRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        do
        {
            await PurgeAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken stoppingToken)
    {
        var retentionDays = options.Value.RetentionDays;

        try
        {
            var result = await writer.PurgeAsync(retentionDays, stoppingToken);
            logger.LogInformation(
                "Log retention removed {RequestLogs} request logs and {ErrorEvents} error events older than {RetentionDays} days.",
                result.DeletedRequestLogs, result.DeletedErrorEvents, retentionDays);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Log retention failed; it will run again in {Interval}.", Interval);
        }
    }
}
