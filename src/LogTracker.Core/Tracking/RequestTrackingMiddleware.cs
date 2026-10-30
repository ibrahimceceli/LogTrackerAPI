using LogTracker.Core.Pipeline;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Tracking;

/// <summary>Measures every request and queues one <see cref="RequestLogEntry"/> for it.</summary>
internal sealed class RequestTrackingMiddleware(
    RequestDelegate next,
    LogQueue queue,
    ErrorRecorder errorRecorder,
    IOptions<LogTrackerOptions> options,
    TimeProvider timeProvider)
{
    private readonly PathString[] _excludedPrefixes =
        options.Value.ExcludedPathPrefixes.Select(prefix => new PathString(prefix)).ToArray();

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        // Works like Stopwatch, but tests can control the clock.
        var startTimestamp = timeProvider.GetTimestamp();
        var exceptionEscaped = false;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // Only happens when the exception handler could not answer, e.g. the response had already started.
            exceptionEscaped = true;
            errorRecorder.Record(context, exception, StatusCodes.Status500InternalServerError);
            throw;
        }
        finally
        {
            if (exceptionEscaped || !WasAnsweredWithoutEndpoint(context))
            {
                var duration = timeProvider.GetElapsedTime(startTimestamp);

                queue.TryEnqueue(new RequestLogEntry(
                    OccurredAtUtc: occurredAtUtc,
                    Method: RequestInfo.GetMethod(context),
                    Endpoint: RequestInfo.GetEndpoint(context),
                    Path: RequestInfo.GetPath(context),
                    StatusCode: exceptionEscaped ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                    DurationMs: (int)Math.Round(duration.TotalMilliseconds),
                    TraceId: RequestInfo.GetTraceId(context)));
            }
        }
    }

    private bool IsExcluded(PathString path)
    {
        foreach (var prefix in _excludedPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for successful responses that no endpoint produced, such as static files.
    /// Those are not API calls. Unmatched requests that end in 404 are still tracked.
    /// </summary>
    private static bool WasAnsweredWithoutEndpoint(HttpContext context) =>
        context.GetEndpoint() is null
        && context.Features.Get<IExceptionHandlerFeature>() is null
        && context.Response.StatusCode < StatusCodes.Status400BadRequest;
}
