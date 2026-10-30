namespace LogTracker.Api.Tests;

public sealed class SecurityHeadersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/")]
    [InlineData("/api/products")]
    [InlineData("/api/reports/summary")]
    [InlineData("/does-not-exist")]
    public async Task Every_response_carries_the_hardening_headers(string path)
    {
        await using var factory = new LogTrackerApiFactory();

        var response = await factory.CreateClient().GetAsync(path, Ct);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : null;
}
