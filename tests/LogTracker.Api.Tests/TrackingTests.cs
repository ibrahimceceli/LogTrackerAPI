using System.Diagnostics;
using System.Net;
using System.Text.Json;
using static LogTracker.Api.Tests.ReportEndpointTests;

namespace LogTracker.Api.Tests;

/// <summary>End-to-end: request, tracking middleware, queue, background writer, store and reports.</summary>
public sealed class TrackingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Records_route_template_and_path_without_query_string()
    {
        await using var factory = new LogTrackerApiFactory();

        await factory.CreateClient().GetAsync("/api/products/5?token=secret", Ct);

        await WaitForAsync(() => factory.Store.Requests.Count == 1);
        var entry = factory.Store.Requests[0];
        Assert.Equal("GET", entry.Method);
        Assert.Equal("/api/products/{id:int}", entry.Endpoint);
        Assert.Equal("/api/products/5", entry.Path);
        Assert.Equal(200, entry.StatusCode);
        Assert.InRange(entry.DurationMs, 10, 5000);
    }

    [Fact]
    public async Task Does_not_track_reports_health_or_static_files()
    {
        await using var factory = new LogTrackerApiFactory();
        var client = factory.CreateClient();

        await client.GetAsync("/api/reports/summary", Ct);
        await client.GetAsync("/", Ct);
        await client.GetAsync("/index.html", Ct);
        await client.GetAsync("/api/products/1", Ct);

        await WaitForAsync(() => factory.Store.Requests.Count >= 1);
        await Task.Delay(200, Ct);
        Assert.Equal(["/api/products/1"], factory.Store.Requests.Select(entry => entry.Path));
    }

    [Fact]
    public async Task Stores_script_like_paths_as_plain_data_and_returns_them_json_encoded()
    {
        await using var factory = new LogTrackerApiFactory();
        var client = factory.CreateClient();

        var notFound = await client.GetAsync("/api/%3Cscript%3Ealert(1)%3C/script%3E", Ct);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        await WaitForAsync(() => factory.Store.Requests.Count == 1);

        var entry = factory.Store.Requests[0];
        Assert.Equal("(unmatched)", entry.Endpoint);
        Assert.Equal("/api/<script>alert(1)</script>", entry.Path);

        // The path comes back as an ordinary JSON string value; it is never turned into markup by the API.
        // Rendering it safely (textContent, not innerHTML) is the dashboard's job.
        var response = await client.GetAsync("/api/reports/requests", Ct);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var page = await response.ReadJsonAsync(HttpStatusCode.OK);
        var path = page.GetProperty("items")[0].GetProperty("path");
        Assert.Equal(JsonValueKind.String, path.ValueKind);
        Assert.Equal("/api/<script>alert(1)</script>", path.GetString());
    }

    [Fact]
    public async Task Error_response_trace_id_matches_the_stored_request_and_error()
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/test/errors/key-not-found", Ct);
        var problem = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        var traceId = problem.GetProperty("traceId").GetString();

        await WaitForAsync(() => factory.Store.Requests.Count == 1 && factory.Store.Errors.Count == 1);
        var request = factory.Store.Requests[0];
        var error = factory.Store.Errors[0];

        Assert.Equal("System.Collections.Generic.KeyNotFoundException", error.ErrorType);
        Assert.Equal("Simulated missing key.", error.Message);
        Assert.Equal("LogTracker.Api.Endpoints.TestEndpoints", error.Source);
        Assert.Equal("/api/test/errors/{type}", error.Endpoint);
        Assert.Equal("/api/test/errors/key-not-found", error.Path);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal(traceId, error.TraceId);

        Assert.Equal("/api/test/errors/{type}", request.Endpoint);
        Assert.Equal(500, request.StatusCode);
        Assert.Equal(traceId, request.TraceId);
    }

    [Fact]
    public async Task Requests_stay_fast_and_successful_while_the_log_database_is_down()
    {
        await using var factory = new LogTrackerApiFactory();
        factory.Store.FailWrites = true;
        var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/api/orders/1", Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var errorResponse = await client.GetAsync("/api/test/errors/timeout", Ct);

        await errorResponse.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Requests took {stopwatch.Elapsed}.");
        Assert.Empty(factory.Store.Requests);
    }
}
