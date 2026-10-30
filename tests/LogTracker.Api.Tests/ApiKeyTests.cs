using System.Net;

namespace LogTracker.Api.Tests;

public sealed class ApiKeyTests
{
    private const string Key = "test-key-0123456789";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    [InlineData("test-key-12")]
    public async Task Reports_need_the_right_key_when_one_is_configured(string? providedKey)
    {
        await using var factory = new LogTrackerApiFactory { ApiKey = Key };
        var client = factory.CreateClient();
        if (providedKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", providedKey);
        }

        var response = await client.GetAsync("/api/reports/summary", Ct);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/reports/summary")]
    [InlineData("/api/reports/weekly-trend")]
    public async Task Reports_answer_with_the_right_key(string path)
    {
        await using var factory = new LogTrackerApiFactory { ApiKey = Key };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", Key);

        var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reports_are_open_when_no_key_is_configured()
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/reports/summary", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Demo_endpoints_never_need_the_key()
    {
        await using var factory = new LogTrackerApiFactory { ApiKey = Key };

        var response = await factory.CreateClient().GetAsync("/api/products", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
