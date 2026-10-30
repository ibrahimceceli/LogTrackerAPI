using System.Net;

namespace LogTracker.Api.Tests;

public sealed class TestEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("null-reference")]
    [InlineData("divide-by-zero")]
    [InlineData("timeout")]
    [InlineData("argument")]
    [InlineData("key-not-found")]
    public async Task Each_error_type_becomes_a_500_problem_without_internal_details(string type)
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync($"/api/test/errors/{type}", Ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.False(problem.TryGetProperty("detail", out _), "Exception messages must not leak outside Development.");
    }

    [Fact]
    public async Task Development_shows_the_exception_message()
    {
        await using var factory = new LogTrackerApiFactory { EnvironmentName = "Development" };

        var response = await factory.CreateClient().GetAsync("/api/test/errors/timeout", Ct);

        var problem = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal("Simulated timeout.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unknown_error_type_is_a_400()
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/test/errors/out-of-memory", Ct);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        await factory.StopAndFlushAsync();
        Assert.Equal(400, Assert.Single(factory.Store.Requests).StatusCode);
        Assert.Empty(factory.Store.Errors);
    }

    [Theory]
    [InlineData("?ms=20", 20)]
    [InlineData("?ms=-5", 0)]
    public async Task Slow_endpoint_waits_the_clamped_time(string query, int expectedDelay)
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync($"/api/test/slow{query}", Ct);

        var body = await response.ReadJsonAsync(HttpStatusCode.OK);
        Assert.Equal(expectedDelay, body.GetProperty("delayedMs").GetInt32());
    }

    [Theory]
    [InlineData("/api/test/errors/timeout")]
    [InlineData("/api/test/slow")]
    public async Task Test_endpoints_do_not_exist_when_disabled(string path)
    {
        await using var factory = new LogTrackerApiFactory { EnableTestEndpoints = false };

        var response = await factory.CreateClient().GetAsync(path, Ct);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await factory.StopAndFlushAsync();
        Assert.Equal("(unmatched)", Assert.Single(factory.Store.Requests).Endpoint);
        Assert.Empty(factory.Store.Errors);
    }
}
