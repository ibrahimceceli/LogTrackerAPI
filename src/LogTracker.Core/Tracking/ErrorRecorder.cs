using LogTracker.Core.Pipeline;
using Microsoft.AspNetCore.Http;

namespace LogTracker.Core.Tracking;

/// <summary>Turns an exception into an <see cref="ErrorEventEntry"/> and queues it.</summary>
internal sealed class ErrorRecorder(LogQueue queue, TimeProvider timeProvider)
{
    // Both the exception handler and the tracking middleware may see the same exception
    // (for example when the handler itself fails); this marker stores it only once.
    private static readonly object RecordedMarker = new();

    public void Record(HttpContext context, Exception exception, int statusCode)
    {
        if (!context.Items.TryAdd(RecordedMarker, true))
        {
            return;
        }

        var exceptionType = exception.GetType();

        queue.TryEnqueue(new ErrorEventEntry(
            OccurredAtUtc: timeProvider.GetUtcNow().UtcDateTime,
            ErrorType: ColumnLimits.Truncate(exceptionType.FullName ?? exceptionType.Name, ColumnLimits.ErrorType),
            Message: ColumnLimits.Truncate(exception.Message, ColumnLimits.Message),
            Source: DescribeSource(exception),
            StackTrace: exception.ToString(),
            Method: RequestInfo.GetMethod(context),
            Path: RequestInfo.GetPath(context),
            Endpoint: RequestInfo.GetEndpoint(context),
            StatusCode: statusCode,
            TraceId: RequestInfo.GetTraceId(context)));
    }

    private static string? DescribeSource(Exception exception)
    {
        var type = exception.TargetSite?.DeclaringType;

        // Async methods and lambdas compile into nested classes such as <GetAsync>d__3;
        // report the class the developer actually wrote.
        while (type?.DeclaringType is not null && type.Name.StartsWith('<'))
        {
            type = type.DeclaringType;
        }

        var source = type?.FullName ?? exception.Source;
        return source is null ? null : ColumnLimits.Truncate(source, ColumnLimits.Source);
    }
}
