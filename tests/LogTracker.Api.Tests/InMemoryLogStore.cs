using System.Collections.Concurrent;
using LogTracker.Core.Data;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Reports;

namespace LogTracker.Api.Tests;

/// <summary>
/// Replaces SQL Server in API tests. Written entries can be read back through the request and error lists;
/// the aggregate reports return fixed sample data, because their SQL is covered by the database tests.
/// </summary>
internal sealed class InMemoryLogStore : ILogWriter, ILogReportRepository
{
    private static readonly DateTime SampleTime = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly ConcurrentQueue<RequestLogEntry> _requests = new();
    private readonly ConcurrentQueue<ErrorEventEntry> _errors = new();

    /// <summary>Makes every write fail, like an unreachable database.</summary>
    public bool FailWrites { get; set; }

    public IReadOnlyList<RequestLogEntry> Requests => _requests.ToList();

    public IReadOnlyList<ErrorEventEntry> Errors => _errors.ToList();

    public Task WriteRequestsAsync(IReadOnlyList<RequestLogEntry> entries, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        foreach (var entry in entries)
        {
            _requests.Enqueue(entry);
        }

        return Task.CompletedTask;
    }

    public Task WriteErrorsAsync(IReadOnlyList<ErrorEventEntry> entries, CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        foreach (var entry in entries)
        {
            _errors.Enqueue(entry);
        }

        return Task.CompletedTask;
    }

    public Task<PurgeResult> PurgeAsync(int retentionDays, CancellationToken cancellationToken) =>
        Task.FromResult(new PurgeResult());

    public Task<SummaryReport> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        Task.FromResult(new SummaryReport
        {
            From = fromUtc,
            To = toUtc,
            TotalRequests = 10,
            ServerErrorCount = 1,
            ClientErrorCount = 2,
            ErrorRatePercent = 10,
            AverageDurationMs = 12.5,
            P95DurationMs = 40,
        });

    public Task<PagedResult<RequestLogItem>> GetRequestsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = _requests.Reverse().Select((entry, index) => new RequestLogItem
        {
            Id = index + 1,
            Method = entry.Method,
            Endpoint = entry.Endpoint,
            Path = entry.Path,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            TraceId = entry.TraceId,
            OccurredAtUtc = entry.OccurredAtUtc,
        }).ToList();

        return Task.FromResult(Page(all, page, pageSize));
    }

    public Task<PagedResult<ErrorEventItem>> GetErrorsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = _errors.Reverse().Select((entry, index) => (ErrorEventItem)ToDetail(entry, index + 1)).ToList();
        return Task.FromResult(Page(all, page, pageSize));
    }

    public Task<ErrorEventDetail?> GetErrorAsync(long id, CancellationToken cancellationToken)
    {
        var entry = _errors.Reverse().ElementAtOrDefault((int)id - 1);
        return Task.FromResult(entry is null ? null : ToDetail(entry, id));
    }

    public Task<IReadOnlyList<EndpointStats>> GetEndpointStatsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EndpointStats>>(
        [
            new EndpointStats
            {
                Method = "GET",
                Endpoint = "/api/products",
                TotalRequests = 8,
                ServerErrorCount = 0,
                AverageDurationMs = 44.5,
                P95DurationMs = 77.4,
                MaxDurationMs = 80,
            },
        ]);

    public Task<IReadOnlyList<HourlyErrorCount>> GetHourlyErrorCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HourlyErrorCount>>(
        [
            new HourlyErrorCount { HourStartUtc = SampleTime, ErrorType = "System.TimeoutException", Count = 3 },
        ]);

    public Task<IReadOnlyList<WeeklyTrendRow>> GetWeeklyTrendAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WeeklyTrendRow>>(
        [
            new WeeklyTrendRow { Method = "GET", Endpoint = "/api/products", Monday = 41.5, Friday = 39 },
        ]);

    private static PagedResult<T> Page<T>(List<T> all, int page, int pageSize) =>
        new(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);

    private static ErrorEventDetail ToDetail(ErrorEventEntry entry, long id) => new()
    {
        Id = id,
        Type = entry.ErrorType,
        Message = entry.Message,
        Source = entry.Source,
        Method = entry.Method,
        Path = entry.Path,
        Endpoint = entry.Endpoint,
        StatusCode = entry.StatusCode,
        TraceId = entry.TraceId,
        OccurredAtUtc = entry.OccurredAtUtc,
        StackTrace = entry.StackTrace,
    };

    private void ThrowIfFailing()
    {
        if (FailWrites)
        {
            throw new InvalidOperationException("Simulated database outage.");
        }
    }
}
