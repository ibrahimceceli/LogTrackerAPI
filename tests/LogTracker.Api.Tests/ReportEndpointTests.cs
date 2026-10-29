using System.Net;
using System.Text.Json;

namespace LogTracker.Api.Tests;

/// <summary>Checks the JSON shapes promised by the shared API contract.</summary>
public sealed class ReportEndpointTests : IAsyncLifetime
{
    private readonly LogTrackerApiFactory _factory = new();
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();

        // One tracked request and one error so the lists have content.
        await _client.GetAsync("/api/products/1", Ct);
        await _client.GetAsync("/api/test/errors/timeout", Ct);
        await WaitForAsync(() => _factory.Store.Requests.Count >= 2 && _factory.Store.Errors.Count >= 1);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Summary_has_the_contract_shape()
    {
        var summary = await GetJsonAsync("/api/reports/summary?hours=24");

        JsonAssert.HasProperties(summary, "from", "to", "totalRequests", "serverErrorCount", "clientErrorCount",
            "errorRatePercent", "averageDurationMs", "p95DurationMs");
        JsonAssert.IsUtcTimestamp(summary.GetProperty("from"));
        JsonAssert.IsUtcTimestamp(summary.GetProperty("to"));
        var window = summary.GetProperty("to").GetDateTime() - summary.GetProperty("from").GetDateTime();
        Assert.Equal(TimeSpan.FromHours(24), window);
    }

    [Fact]
    public async Task Requests_page_has_the_contract_shape()
    {
        var page = await GetJsonAsync("/api/reports/requests?page=1&pageSize=20");

        JsonAssert.HasProperties(page, "items", "page", "pageSize", "totalCount");
        var item = page.GetProperty("items")[0];
        JsonAssert.HasProperties(item, "id", "method", "endpoint", "path", "statusCode", "durationMs", "traceId", "occurredAtUtc");
        JsonAssert.IsUtcTimestamp(item.GetProperty("occurredAtUtc"));
    }

    [Fact]
    public async Task Errors_page_and_detail_have_the_contract_shape()
    {
        var page = await GetJsonAsync("/api/reports/errors?page=1&pageSize=20");

        JsonAssert.HasProperties(page, "items", "page", "pageSize", "totalCount");
        var item = page.GetProperty("items")[0];
        string[] itemProperties = ["id", "type", "message", "source", "method", "path", "endpoint", "statusCode", "traceId", "occurredAtUtc"];
        JsonAssert.HasProperties(item, itemProperties);

        var detail = await GetJsonAsync($"/api/reports/errors/{item.GetProperty("id").GetInt64()}");
        JsonAssert.HasProperties(detail, [.. itemProperties, "stackTrace"]);
        Assert.Contains("TimeoutException", detail.GetProperty("stackTrace").GetString());
    }

    [Fact]
    public async Task Unknown_error_id_is_a_404_problem()
    {
        await (await _client.GetAsync("/api/reports/errors/987654", Ct)).ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoint_stats_have_the_contract_shape()
    {
        var stats = await GetJsonAsync("/api/reports/endpoints?hours=168");

        JsonAssert.HasProperties(stats[0], "method", "endpoint", "totalRequests", "serverErrorCount",
            "averageDurationMs", "p95DurationMs", "maxDurationMs");
    }

    [Fact]
    public async Task Hourly_errors_have_the_contract_shape()
    {
        var hourly = await GetJsonAsync("/api/reports/hourly-errors?hours=24");

        JsonAssert.HasProperties(hourly[0], "hourStartUtc", "errorType", "count");
        JsonAssert.IsUtcTimestamp(hourly[0].GetProperty("hourStartUtc"));
    }

    [Fact]
    public async Task Weekly_trend_has_the_contract_shape_with_null_for_missing_days()
    {
        var trend = await GetJsonAsync("/api/reports/weekly-trend");

        var row = trend[0];
        JsonAssert.HasProperties(row, "method", "endpoint", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday");
        Assert.Equal(JsonValueKind.Null, row.GetProperty("tuesday").ValueKind);
        Assert.Equal(41.5, row.GetProperty("monday").GetDouble());
    }

    [Theory]
    [InlineData("/api/reports/summary?hours=0")]
    [InlineData("/api/reports/summary?hours=100000")]
    [InlineData("/api/reports/endpoints?hours=-1")]
    [InlineData("/api/reports/hourly-errors?hours=0")]
    [InlineData("/api/reports/requests?pageSize=0")]
    [InlineData("/api/reports/requests?pageSize=101")]
    [InlineData("/api/reports/errors?page=0")]
    public async Task Out_of_range_parameters_are_400_validation_problems(string url)
    {
        var problem = await (await _client.GetAsync(url, Ct)).ReadProblemAsync(HttpStatusCode.BadRequest);

        Assert.True(problem.TryGetProperty("errors", out _));
    }

    private async Task<JsonElement> GetJsonAsync(string url) =>
        await (await _client.GetAsync(url, Ct)).ReadJsonAsync(HttpStatusCode.OK);

    internal static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The background writer did not store the entries in time.");
            await Task.Delay(10, Ct);
        }
    }
}
