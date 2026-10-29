using Microsoft.AspNetCore.Http.HttpResults;

namespace LogTracker.Api.Endpoints;

/// <summary>Response of GET /api/test/slow.</summary>
/// <param name="DelayedMs">How long the request waited, in milliseconds.</param>
public sealed record SlowResponse(int DelayedMs);

/// <summary>
/// Endpoints that fail or answer slowly on purpose, to exercise error tracking and alerts.
/// Only mapped when LogTracker:EnableTestEndpoints is true.
/// </summary>
public static class TestEndpoints
{
    private const int MaxDelayMs = 10_000;

    private static readonly string[] ErrorTypes =
        ["invalid-operation", "null-reference", "divide-by-zero", "timeout", "argument", "key-not-found"];

    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        var test = app.MapGroup("/api/test").WithTags("Test");

        test.MapGet("/errors/{type}", ThrowError);
        test.MapGet("/slow", Slow);

        return app;
    }

    /// <summary>Throws an exception of the requested kind.</summary>
    /// <param name="type">invalid-operation, null-reference, divide-by-zero, timeout, argument or key-not-found.</param>
    /// <response code="400">Unknown exception kind.</response>
    /// <response code="500">The simulated exception, answered as ProblemDetails.</response>
    public static ValidationProblem ThrowError(string type)
    {
        Exception? exception = type.ToLowerInvariant() switch
        {
            "invalid-operation" => new InvalidOperationException("Simulated invalid operation."),
            "null-reference" => new NullReferenceException("Simulated null reference."),
            "divide-by-zero" => new DivideByZeroException("Simulated division by zero."),
            "timeout" => new TimeoutException("Simulated timeout."),
            "argument" => new ArgumentException("Simulated invalid argument."),
            "key-not-found" => new KeyNotFoundException("Simulated missing key."),
            _ => null,
        };

        if (exception is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["type"] = [$"Must be one of: {string.Join(", ", ErrorTypes)}."],
            });
        }

        throw exception;
    }

    /// <summary>Waits before answering, to produce slow requests.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <param name="ms">Delay in milliseconds, limited to 0-10000.</param>
    /// <response code="200">How long the request waited.</response>
    public static async Task<Ok<SlowResponse>> Slow(CancellationToken cancellationToken, int ms = 1500)
    {
        var delayMs = Math.Clamp(ms, 0, MaxDelayMs);
        await Task.Delay(delayMs, cancellationToken);
        return TypedResults.Ok(new SlowResponse(delayMs));
    }
}
