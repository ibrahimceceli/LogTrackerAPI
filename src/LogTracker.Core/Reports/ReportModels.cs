namespace LogTracker.Core.Reports;

/// <summary>Request totals for a time window.</summary>
public sealed record SummaryReport
{
    /// <summary>Start of the window (inclusive), UTC.</summary>
    public DateTime From { get; init; }

    /// <summary>End of the window (exclusive), UTC.</summary>
    public DateTime To { get; init; }

    /// <summary>Number of tracked requests.</summary>
    public int TotalRequests { get; init; }

    /// <summary>Requests answered with status 500 or higher.</summary>
    public int ServerErrorCount { get; init; }

    /// <summary>Requests answered with a status from 400 to 499.</summary>
    public int ClientErrorCount { get; init; }

    /// <summary>Share of server errors in percent; 0 when there were no requests.</summary>
    public double ErrorRatePercent { get; init; }

    /// <summary>Average duration in milliseconds; 0 when there were no requests.</summary>
    public double AverageDurationMs { get; init; }

    /// <summary>95th percentile duration in milliseconds; 0 when there were no requests.</summary>
    public double P95DurationMs { get; init; }
}

/// <summary>One page of a newest-first list.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items on this page.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Maximum items per page.</param>
/// <param name="TotalCount">Items on all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

/// <summary>One tracked request.</summary>
public sealed record RequestLogItem
{
    /// <summary>Row id.</summary>
    public long Id { get; init; }

    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = "";

    /// <summary>Route template, or <c>(unmatched)</c>.</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>Request path without the query string. Raw client input: encode it before showing it in HTML.</summary>
    public string Path { get; init; } = "";

    /// <summary>Response status code.</summary>
    public int StatusCode { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public int DurationMs { get; init; }

    /// <summary>Trace id that also appears in error responses.</summary>
    public string? TraceId { get; init; }

    /// <summary>When the request started, UTC.</summary>
    public DateTime OccurredAtUtc { get; init; }
}

/// <summary>One unhandled exception, without the stack trace.</summary>
public record ErrorEventItem
{
    /// <summary>Row id.</summary>
    public long Id { get; init; }

    /// <summary>Full exception type name.</summary>
    public string Type { get; init; } = "";

    /// <summary>Exception message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Class where the exception was thrown, when known.</summary>
    public string? Source { get; init; }

    /// <summary>HTTP method of the failed request.</summary>
    public string Method { get; init; } = "";

    /// <summary>Request path without the query string.</summary>
    public string Path { get; init; } = "";

    /// <summary>Route template, or <c>(unmatched)</c>.</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>Status code returned to the client.</summary>
    public int StatusCode { get; init; }

    /// <summary>Trace id that also appears in the error response.</summary>
    public string? TraceId { get; init; }

    /// <summary>When the exception was caught, UTC.</summary>
    public DateTime OccurredAtUtc { get; init; }
}

/// <summary>One unhandled exception including the stack trace.</summary>
public sealed record ErrorEventDetail : ErrorEventItem
{
    /// <summary>Full exception text, including inner exceptions.</summary>
    public string? StackTrace { get; init; }
}

/// <summary>Statistics for one method and route template.</summary>
public sealed record EndpointStats
{
    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = "";

    /// <summary>Route template, or <c>(unmatched)</c>.</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>Number of requests.</summary>
    public int TotalRequests { get; init; }

    /// <summary>Requests answered with status 500 or higher.</summary>
    public int ServerErrorCount { get; init; }

    /// <summary>Average duration in milliseconds.</summary>
    public double AverageDurationMs { get; init; }

    /// <summary>95th percentile duration in milliseconds.</summary>
    public double P95DurationMs { get; init; }

    /// <summary>Slowest request in milliseconds.</summary>
    public int MaxDurationMs { get; init; }
}

/// <summary>Number of errors of one type in one hour.</summary>
public sealed record HourlyErrorCount
{
    /// <summary>Start of the hour, UTC.</summary>
    public DateTime HourStartUtc { get; init; }

    /// <summary>Full exception type name.</summary>
    public string ErrorType { get; init; } = "";

    /// <summary>Number of errors.</summary>
    public int Count { get; init; }
}

/// <summary>Average duration of one endpoint per weekday; null means no requests that day.</summary>
public sealed record WeeklyTrendRow
{
    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = "";

    /// <summary>Route template, or <c>(unmatched)</c>.</summary>
    public string Endpoint { get; init; } = "";

    /// <summary>Average milliseconds on Monday.</summary>
    public double? Monday { get; init; }

    /// <summary>Average milliseconds on Tuesday.</summary>
    public double? Tuesday { get; init; }

    /// <summary>Average milliseconds on Wednesday.</summary>
    public double? Wednesday { get; init; }

    /// <summary>Average milliseconds on Thursday.</summary>
    public double? Thursday { get; init; }

    /// <summary>Average milliseconds on Friday.</summary>
    public double? Friday { get; init; }

    /// <summary>Average milliseconds on Saturday.</summary>
    public double? Saturday { get; init; }

    /// <summary>Average milliseconds on Sunday.</summary>
    public double? Sunday { get; init; }
}
