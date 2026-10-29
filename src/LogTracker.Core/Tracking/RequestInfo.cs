using System.Diagnostics;
using LogTracker.Core.Pipeline;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogTracker.Core.Tracking;

/// <summary>Reads the values LogTracker stores about a request, already cut to the column sizes.</summary>
internal static class RequestInfo
{
    /// <summary>Endpoint name used when routing found no endpoint (typically a 404).</summary>
    public const string UnmatchedEndpoint = "(unmatched)";

    /// <summary>
    /// The same id ASP.NET Core writes into ProblemDetails, so an error response a client
    /// received can be matched to the stored rows.
    /// </summary>
    public static string GetTraceId(HttpContext context) =>
        ColumnLimits.Truncate(Activity.Current?.Id ?? context.TraceIdentifier, ColumnLimits.TraceId);

    public static string GetMethod(HttpContext context) =>
        ColumnLimits.Truncate(context.Request.Method, ColumnLimits.Method);

    /// <summary>Path without the query string, which may contain personal data or secrets.</summary>
    public static string GetPath(HttpContext context)
    {
        var path = context.Request.PathBase.Add(context.Request.Path).Value;
        return ColumnLimits.Truncate(string.IsNullOrEmpty(path) ? "/" : path, ColumnLimits.Path);
    }

    /// <summary>
    /// The route template, e.g. <c>/api/products/{id:int}</c>. Grouping by template instead of the raw path
    /// keeps reports small: /api/products/1 and /api/products/2 are the same endpoint.
    /// </summary>
    public static string GetEndpoint(HttpContext context)
    {
        // The exception handler clears the endpoint before it runs, but keeps the original in this feature.
        var endpoint = context.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? context.GetEndpoint();

        var name = endpoint switch
        {
            null => UnmatchedEndpoint,
            // Controllers give "api/x", minimal APIs "/api/x", and groups mapped with "" end in "/";
            // normalizing keeps one spelling per endpoint.
            RouteEndpoint { RoutePattern.RawText: { } template } => "/" + template.Trim('/'),
            _ => endpoint.DisplayName ?? UnmatchedEndpoint,
        };

        return ColumnLimits.Truncate(name, ColumnLimits.Endpoint);
    }
}
