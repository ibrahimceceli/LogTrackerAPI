using LogTracker.Core.Reports;

namespace LogTracker.Core.Data;

/// <summary>Read-only queries behind the <c>/api/reports</c> endpoints. All times are UTC.</summary>
public interface ILogReportRepository
{
    /// <summary>Totals, error counts and latency for requests in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    Task<SummaryReport> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>One page of request logs, newest first.</summary>
    Task<PagedResult<RequestLogItem>> GetRequestsAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>One page of error events, newest first.</summary>
    Task<PagedResult<ErrorEventItem>> GetErrorsAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>One error event with its stack trace, or null when the id is unknown.</summary>
    Task<ErrorEventDetail?> GetErrorAsync(long id, CancellationToken cancellationToken);

    /// <summary>Statistics per endpoint, busiest first.</summary>
    Task<IReadOnlyList<EndpointStats>> GetEndpointStatsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>Error counts per hour and exception type; empty hours are left out.</summary>
    Task<IReadOnlyList<HourlyErrorCount>> GetHourlyErrorCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);

    /// <summary>Average duration per endpoint and weekday over the 7 UTC calendar days ending on <paramref name="nowUtc"/>.</summary>
    Task<IReadOnlyList<WeeklyTrendRow>> GetWeeklyTrendAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
