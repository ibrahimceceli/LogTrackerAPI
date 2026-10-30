using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Reports;

/// <summary>
/// Requires the <c>X-Api-Key</c> header on report endpoints when <see cref="LogTrackerOptions.ApiKey"/> is set.
/// </summary>
internal sealed class ApiKeyEndpointFilter(IOptions<LogTrackerOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expectedKey = options.Value.ApiKey;
        if (string.IsNullOrEmpty(expectedKey))
        {
            return await next(context);
        }

        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!KeysMatch(providedKey, expectedKey))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"A valid {HeaderName} header is required.");
        }

        return await next(context);
    }

    // A constant-time comparison does not reveal through response timing how many leading characters were right.
    private static bool KeysMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}
