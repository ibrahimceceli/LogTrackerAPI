using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LogTracker.Api.Tests;

internal static class JsonAssert
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>Checks an RFC 7807 body and returns it.</summary>
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    /// <summary>The object must have exactly these properties: nothing missing, nothing extra.</summary>
    public static void HasProperties(JsonElement element, params string[] expected)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).Order();
        Assert.Equal(expected.Order(), actual);
    }

    public static void IsUtcTimestamp(JsonElement element)
    {
        var text = element.GetString();
        Assert.NotNull(text);
        Assert.EndsWith("Z", text);
        Assert.True(DateTime.TryParse(text, out _), $"'{text}' is not a date.");
    }
}
