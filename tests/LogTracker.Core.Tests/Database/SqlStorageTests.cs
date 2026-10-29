using Dapper;
using LogTracker.Core.Data;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogTracker.Core.Tests.Database;

/// <summary>
/// Runs the real scripts, writer and report queries against SQL Server.
/// Each test uses its own endpoint names or time window, so tests do not see each other's rows.
/// </summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SqlStorageTests(DatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SqlLogWriter Writer => new(fixture.ConnectionFactory);

    private SqlLogReportRepository Reports => new(fixture.ConnectionFactory);

    [DatabaseFact]
    public async Task Schema_scripts_can_run_again_without_errors()
    {
        await TestDatabase.RunSchemaScriptsAsync(fixture.ConnectionString);
    }

    [DatabaseFact]
    public async Task Request_logs_round_trip_exactly_and_newest_first()
    {
        // Far in the future, so these rows are the newest in the table.
        var start = new DateTime(2099, 1, 1, 10, 0, 0, 125, DateTimeKind.Utc);
        const string xssPath = "/api/<script>alert('x')</script>";
        await Writer.WriteRequestsAsync(
        [
            new RequestLogEntry(start, "GET", "/api/products/{id:int}", "/api/products/7", 200, 15, "00-abc-01"),
            new RequestLogEntry(start.AddSeconds(1), "GET", "(unmatched)", xssPath, 404, 2, null),
            new RequestLogEntry(start.AddSeconds(2), "POST", "/api/orders", "/api/orders", 201, 120, "00-def-01"),
        ], Ct);

        var page = await Reports.GetRequestsAsync(page: 1, pageSize: 3, Ct);

        Assert.True(page.TotalCount >= 3);
        Assert.Equal(["/api/orders", xssPath, "/api/products/7"], page.Items.Select(item => item.Path));

        var unmatched = page.Items[1];
        Assert.Equal("(unmatched)", unmatched.Endpoint);
        Assert.Equal(404, unmatched.StatusCode);
        Assert.Null(unmatched.TraceId);

        var product = page.Items[2];
        Assert.Equal(start, product.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, product.OccurredAtUtc.Kind);
        Assert.Equal("00-abc-01", product.TraceId);
        Assert.Equal(15, product.DurationMs);
    }

    [DatabaseFact]
    public async Task Error_events_round_trip_with_full_stack_trace()
    {
        var occurredAt = new DateTime(2099, 2, 1, 8, 30, 0, DateTimeKind.Utc);
        var longStackTrace = string.Concat(Enumerable.Repeat("   at Some.Method()\n", 500));
        await Writer.WriteErrorsAsync(
        [
            new ErrorEventEntry(occurredAt, "System.TimeoutException", "Simulated timeout.", "LogTracker.Api.Endpoints.TestEndpoints",
                longStackTrace, "GET", "/api/test/errors/timeout", "/api/test/errors/{type}", 500, "00-trace-01"),
        ], Ct);

        var newest = Assert.Single((await Reports.GetErrorsAsync(page: 1, pageSize: 1, Ct)).Items);
        Assert.Equal("System.TimeoutException", newest.Type);
        Assert.Equal("/api/test/errors/{type}", newest.Endpoint);
        Assert.Equal(occurredAt, newest.OccurredAtUtc);

        var detail = await Reports.GetErrorAsync(newest.Id, Ct);
        Assert.NotNull(detail);
        Assert.Equal(longStackTrace, detail.StackTrace);
        Assert.Equal(DateTimeKind.Utc, detail.OccurredAtUtc.Kind);

        Assert.Null(await Reports.GetErrorAsync(-1, Ct));
    }

    [DatabaseFact]
    public async Task Summary_counts_status_classes_and_computes_average_and_p95()
    {
        var from = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Durations 1..20 ms: two server errors, three client errors, fifteen successes.
        var entries = Enumerable.Range(1, 20)
            .Select(i => new RequestLogEntry(from.AddMinutes(i), "GET", "/summary-test", "/summary-test", i switch
            {
                <= 2 => 500,
                <= 5 => 404,
                _ => 200,
            }, i, null))
            .ToList();
        await Writer.WriteRequestsAsync(entries, Ct);

        var summary = await Reports.GetSummaryAsync(from, from.AddDays(1), Ct);

        Assert.Equal(20, summary.TotalRequests);
        Assert.Equal(2, summary.ServerErrorCount);
        Assert.Equal(3, summary.ClientErrorCount);
        Assert.Equal(10, summary.ErrorRatePercent);
        Assert.Equal(10.5, summary.AverageDurationMs);
        // PERCENTILE_CONT interpolates: position 0.95 * 19 = 18.05 lies between 19 and 20.
        Assert.Equal(19.05, summary.P95DurationMs, precision: 2);
    }

    [DatabaseFact]
    public async Task Summary_of_an_empty_window_is_all_zeros()
    {
        var from = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = await Reports.GetSummaryAsync(from, from.AddHours(1), Ct);

        Assert.Equal(new SummaryReport { From = from, To = from.AddHours(1) }, summary);
    }

    [DatabaseFact]
    public async Task Endpoint_stats_group_by_method_and_template_busiest_first()
    {
        var from = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await Writer.WriteRequestsAsync(
        [
            new RequestLogEntry(from.AddMinutes(1), "GET", "/stats/a", "/stats/a", 200, 10, null),
            new RequestLogEntry(from.AddMinutes(2), "GET", "/stats/a", "/stats/a", 200, 20, null),
            new RequestLogEntry(from.AddMinutes(3), "GET", "/stats/a", "/stats/a", 200, 30, null),
            new RequestLogEntry(from.AddMinutes(4), "POST", "/stats/a", "/stats/a", 500, 99, null),
        ], Ct);

        var stats = await Reports.GetEndpointStatsAsync(from, from.AddDays(1), Ct);

        Assert.Equal(2, stats.Count);
        Assert.Equal(new EndpointStats
        {
            Method = "GET",
            Endpoint = "/stats/a",
            TotalRequests = 3,
            ServerErrorCount = 0,
            AverageDurationMs = 20,
            P95DurationMs = 29,
            MaxDurationMs = 30,
        }, stats[0]);
        Assert.Equal(("POST", 1, 1), (stats[1].Method, stats[1].TotalRequests, stats[1].ServerErrorCount));
    }

    [DatabaseFact]
    public async Task Hourly_errors_are_counted_per_hour_and_type_without_empty_hours()
    {
        var from = new DateTime(2003, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        await Writer.WriteErrorsAsync(
        [
            CreateError(from.AddMinutes(5), "Test.TypeA"),
            CreateError(from.AddMinutes(55), "Test.TypeA"),
            CreateError(from.AddMinutes(30), "Test.TypeB"),
            CreateError(from.AddHours(2).AddMinutes(10), "Test.TypeA"),
        ], Ct);

        var hourly = await Reports.GetHourlyErrorCountsAsync(from, from.AddHours(3), Ct);

        Assert.Equal(
        [
            new HourlyErrorCount { HourStartUtc = from, ErrorType = "Test.TypeA", Count = 2 },
            new HourlyErrorCount { HourStartUtc = from, ErrorType = "Test.TypeB", Count = 1 },
            new HourlyErrorCount { HourStartUtc = from.AddHours(2), ErrorType = "Test.TypeA", Count = 1 },
        ], hourly);
        Assert.All(hourly, row => Assert.Equal(DateTimeKind.Utc, row.HourStartUtc.Kind));
    }

    [DatabaseFact]
    public async Task Weekly_trend_covers_the_last_seven_utc_days_whatever_the_session_language()
    {
        // Thursday 16 January 2025; the window is Friday 10 January 00:00 to Friday 17 January 00:00.
        var now = new DateTime(2025, 1, 16, 15, 0, 0, DateTimeKind.Utc);
        const string endpoint = "/weekly-test";
        await Writer.WriteRequestsAsync(
        [
            CreateRequest(new DateTime(2025, 1, 9, 12, 0, 0, DateTimeKind.Utc), endpoint, 999), // eighth day back: excluded
            CreateRequest(new DateTime(2025, 1, 10, 1, 0, 0, DateTimeKind.Utc), endpoint, 100),  // Friday
            CreateRequest(new DateTime(2025, 1, 10, 23, 0, 0, DateTimeKind.Utc), endpoint, 201), // Friday
            CreateRequest(new DateTime(2025, 1, 13, 9, 0, 0, DateTimeKind.Utc), endpoint, 40),   // Monday
            CreateRequest(new DateTime(2025, 1, 16, 23, 0, 0, DateTimeKind.Utc), endpoint, 30),  // Thursday, later today
        ], Ct);

        var expected = new WeeklyTrendRow
        {
            Method = "GET",
            Endpoint = endpoint,
            Monday = 40,
            Thursday = 30,
            Friday = 150.5,
        };

        var trend = await Reports.GetWeeklyTrendAsync(now, Ct);
        Assert.Equal(expected, Assert.Single(trend, row => row.Endpoint == endpoint));

        // DATENAME and DATEPART(weekday) would change with these settings; the procedure must not.
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await connection.ExecuteAsync(new CommandDefinition("SET LANGUAGE Turkish; SET DATEFIRST 3;", cancellationToken: Ct));
        var turkishTrend = await connection.QueryAsync<WeeklyTrendRow>(new CommandDefinition(
            "dbo.GetWeeklyTrend", new { NowUtc = now }, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: Ct));
        Assert.Equal(expected, Assert.Single(turkishTrend, row => row.Endpoint == endpoint));
    }

    [DatabaseFact]
    public async Task Purge_deletes_only_rows_older_than_the_retention_period()
    {
        const string endpoint = "/purge-test";
        var now = DateTime.UtcNow;
        await Writer.WriteRequestsAsync(
        [
            CreateRequest(now.AddDays(-40), endpoint, 1),
            CreateRequest(now.AddDays(-1), endpoint, 2),
        ], Ct);
        await Writer.WriteErrorsAsync([CreateError(now.AddDays(-40), endpoint), CreateError(now.AddDays(-1), endpoint)], Ct);

        var result = await Writer.PurgeAsync(retentionDays: 30, Ct);

        Assert.True(result.DeletedRequestLogs >= 1);
        Assert.True(result.DeletedErrorEvents >= 1);

        await using var connection = new SqlConnection(fixture.ConnectionString);
        var remainingDurations = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DurationMs FROM dbo.RequestLogs WHERE Endpoint = @endpoint", new { endpoint }, cancellationToken: Ct));
        var remainingErrors = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.ErrorEvents WHERE ErrorType = @endpoint", new { endpoint }, cancellationToken: Ct));
        Assert.Equal([2], remainingDurations);
        Assert.Equal(1, remainingErrors);
    }

    [DatabaseFact]
    public async Task Purge_rejects_a_retention_below_one_day()
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => Writer.PurgeAsync(retentionDays: 0, Ct));

        Assert.Contains("RetentionDays", exception.Message);
    }

    [DatabaseFact]
    public async Task Health_check_is_healthy_when_the_tables_exist()
    {
        var result = await new LogDatabaseHealthCheck(fixture.ConnectionFactory).CheckHealthAsync(new HealthCheckContext(), Ct);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static RequestLogEntry CreateRequest(DateTime occurredAtUtc, string endpoint, int durationMs) =>
        new(occurredAtUtc, "GET", endpoint, endpoint, 200, durationMs, null);

    private static ErrorEventEntry CreateError(DateTime occurredAtUtc, string type) =>
        new(occurredAtUtc, type, "message", null, null, "GET", "/errors-test", "/errors-test", 500, null);
}
