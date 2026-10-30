using System.Data;
using Dapper;
using LogTracker.Core.Reports;

namespace LogTracker.Core.Data;

/// <summary>
/// Report queries. Aggregates live in stored procedures (see database/04_report_procedures.sql);
/// the simple paged lists are plain parameterized SELECTs.
/// </summary>
internal sealed class SqlLogReportRepository(SqlConnectionFactory connectionFactory) : ILogReportRepository
{
    private const string RequestsPageSql = """
        SELECT COUNT_BIG(*) FROM dbo.RequestLogs;

        SELECT Id, Method, Endpoint, Path, StatusCode, DurationMs, TraceId, OccurredAtUtc
        FROM dbo.RequestLogs
        ORDER BY OccurredAtUtc DESC, Id DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

    private const string ErrorsPageSql = """
        SELECT COUNT_BIG(*) FROM dbo.ErrorEvents;

        SELECT Id, ErrorType AS Type, Message, Source, Method, Path, Endpoint, StatusCode, TraceId, OccurredAtUtc
        FROM dbo.ErrorEvents
        ORDER BY OccurredAtUtc DESC, Id DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

    private const string ErrorByIdSql = """
        SELECT Id, ErrorType AS Type, Message, Source, Method, Path, Endpoint, StatusCode, TraceId, OccurredAtUtc, StackTrace
        FROM dbo.ErrorEvents
        WHERE Id = @Id;
        """;

    public async Task<SummaryReport> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleAsync<SummaryRow>(
            Procedure("dbo.GetRequestSummary", new { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken));

        return new SummaryReport
        {
            From = fromUtc,
            To = toUtc,
            TotalRequests = row.TotalRequests,
            ServerErrorCount = row.ServerErrorCount,
            ClientErrorCount = row.ClientErrorCount,
            ErrorRatePercent = row.TotalRequests == 0 ? 0 : Math.Round(100.0 * row.ServerErrorCount / row.TotalRequests, 2),
            AverageDurationMs = row.AverageDurationMs ?? 0,
            P95DurationMs = row.P95DurationMs ?? 0,
        };
    }

    public async Task<PagedResult<RequestLogItem>> GetRequestsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(RequestsPageSql, PageParameters(page, pageSize), cancellationToken: cancellationToken));

        var totalCount = await results.ReadSingleAsync<long>();
        var items = (await results.ReadAsync<RequestLogItem>())
            .Select(item => item with { OccurredAtUtc = AsUtc(item.OccurredAtUtc) })
            .ToList();

        return new PagedResult<RequestLogItem>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<ErrorEventItem>> GetErrorsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(ErrorsPageSql, PageParameters(page, pageSize), cancellationToken: cancellationToken));

        var totalCount = await results.ReadSingleAsync<long>();
        var items = (await results.ReadAsync<ErrorEventItem>())
            .Select(item => item with { OccurredAtUtc = AsUtc(item.OccurredAtUtc) })
            .ToList();

        return new PagedResult<ErrorEventItem>(items, page, pageSize, totalCount);
    }

    public async Task<ErrorEventDetail?> GetErrorAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        var error = await connection.QuerySingleOrDefaultAsync<ErrorEventDetail>(
            new CommandDefinition(ErrorByIdSql, new { Id = id }, cancellationToken: cancellationToken));

        return error is null ? null : error with { OccurredAtUtc = AsUtc(error.OccurredAtUtc) };
    }

    public async Task<IReadOnlyList<EndpointStats>> GetEndpointStatsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<EndpointStats>(
            Procedure("dbo.GetEndpointStats", new { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<HourlyErrorCount>> GetHourlyErrorCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<HourlyErrorCount>(
            Procedure("dbo.GetHourlyErrorCounts", new { FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken));

        return rows.Select(row => row with { HourStartUtc = AsUtc(row.HourStartUtc) }).ToList();
    }

    public async Task<IReadOnlyList<WeeklyTrendRow>> GetWeeklyTrendAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<WeeklyTrendRow>(
            Procedure("dbo.GetWeeklyTrend", new { NowUtc = nowUtc }, cancellationToken));

        return rows.AsList();
    }

    private static CommandDefinition Procedure(string name, object parameters, CancellationToken cancellationToken) =>
        new(name, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

    private static object PageParameters(int page, int pageSize) =>
        new { Offset = (long)(page - 1) * pageSize, PageSize = pageSize };

    // datetime2 carries no time zone, so values come back as "Unspecified". The columns hold UTC;
    // marking them as such makes the JSON end with "Z".
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private sealed class SummaryRow
    {
        public int TotalRequests { get; init; }

        public int ServerErrorCount { get; init; }

        public int ClientErrorCount { get; init; }

        public double? AverageDurationMs { get; init; }

        public double? P95DurationMs { get; init; }
    }
}
