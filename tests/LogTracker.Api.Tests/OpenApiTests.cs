using System.Net;

namespace LogTracker.Api.Tests;

public sealed class OpenApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Development_serves_openapi_with_xml_comments_and_scalar()
    {
        await using var factory = new LogTrackerApiFactory { EnvironmentName = "Development" };
        var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json", Ct);
        var scalar = await client.GetAsync("/scalar", Ct);

        Assert.Contains("Lists all products.", document);
        Assert.Contains("/api/reports/weekly-trend", document);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Api_documentation_is_hidden_outside_development(string path)
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
