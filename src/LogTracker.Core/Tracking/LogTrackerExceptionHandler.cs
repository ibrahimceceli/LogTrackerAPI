using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogTracker.Core.Tracking;

/// <summary>
/// Records unhandled exceptions and answers with a ProblemDetails body.
/// Exception details are only shown in Development.
/// </summary>
internal sealed class LogTrackerExceptionHandler(
    ErrorRecorder errorRecorder,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<LogTrackerExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // In Development, minimal APIs throw this for malformed input (bad JSON, wrong parameter type).
        // It is the client's mistake, not a server error, so it is answered but not recorded.
        if (exception is BadHttpRequestException badRequest && !httpContext.Response.HasStarted)
        {
            return await WriteProblemAsync(httpContext, exception, badRequest.StatusCode, "The request is invalid.", badRequest.Message);
        }

        // Since .NET 10 the framework no longer logs exceptions that a handler takes care of.
        logger.LogError(exception, "Unhandled exception for {Method} {Path}.", httpContext.Request.Method, RequestInfo.GetPath(httpContext));
        errorRecorder.Record(httpContext, exception, StatusCodes.Status500InternalServerError);

        if (httpContext.Response.HasStarted)
        {
            // Too late to change the status code or write a body.
            return false;
        }

        var detail = environment.IsDevelopment() ? exception.Message : null;
        return await WriteProblemAsync(httpContext, exception, StatusCodes.Status500InternalServerError, "An unexpected error occurred.", detail);
    }

    private async ValueTask<bool> WriteProblemAsync(HttpContext httpContext, Exception exception, int statusCode, string title, string? detail)
    {
        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
            },
        });
    }
}
