namespace LogTracker.Core.Pipeline;

/// <summary>Something recorded during a request that waits in the queue until it is written.</summary>
/// <param name="OccurredAtUtc">When it happened, in UTC.</param>
public abstract record LogEntry(DateTime OccurredAtUtc);

/// <summary>One finished HTTP request.</summary>
/// <param name="OccurredAtUtc">When the request started, in UTC.</param>
/// <param name="Method">HTTP method.</param>
/// <param name="Endpoint">Route template such as <c>/api/products/{id:int}</c>, or <c>(unmatched)</c>.</param>
/// <param name="Path">Request path without the query string.</param>
/// <param name="StatusCode">Response status code; 500 when an exception escaped.</param>
/// <param name="DurationMs">Time spent in the pipeline, in milliseconds.</param>
/// <param name="TraceId">Id that also appears in error responses.</param>
public sealed record RequestLogEntry(
    DateTime OccurredAtUtc,
    string Method,
    string Endpoint,
    string Path,
    int StatusCode,
    int DurationMs,
    string? TraceId) : LogEntry(OccurredAtUtc);

/// <summary>One unhandled exception.</summary>
/// <param name="OccurredAtUtc">When it was caught, in UTC.</param>
/// <param name="ErrorType">Full exception type name.</param>
/// <param name="Message">Exception message.</param>
/// <param name="Source">Class where the exception was thrown, when known.</param>
/// <param name="StackTrace">Full exception text, including inner exceptions.</param>
/// <param name="Method">HTTP method of the failed request.</param>
/// <param name="Path">Request path without the query string.</param>
/// <param name="Endpoint">Route template, or <c>(unmatched)</c>.</param>
/// <param name="StatusCode">Status code returned to the client.</param>
/// <param name="TraceId">Id that also appears in the error response.</param>
public sealed record ErrorEventEntry(
    DateTime OccurredAtUtc,
    string ErrorType,
    string Message,
    string? Source,
    string? StackTrace,
    string Method,
    string Path,
    string Endpoint,
    int StatusCode,
    string? TraceId) : LogEntry(OccurredAtUtc);

/// <summary>
/// Lengths of the database columns. Values are cut to fit before they are queued,
/// so one oversized request can never make a whole batch insert fail.
/// </summary>
internal static class ColumnLimits
{
    public const int Method = 16;
    public const int Endpoint = 256;
    public const int Path = 512;
    public const int TraceId = 128;
    public const int ErrorType = 256;
    public const int Message = 4000;
    public const int Source = 512;

    public static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
