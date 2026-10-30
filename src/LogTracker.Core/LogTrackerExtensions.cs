using LogTracker.Core.Data;
using LogTracker.Core.Notifications;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Reports;
using LogTracker.Core.Tracking;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LogTracker.Core;

/// <summary>Entry points for adding LogTracker to an ASP.NET Core application.</summary>
public static class LogTrackerExtensions
{
    /// <summary>
    /// Registers request tracking, exception handling, the background writer, retention,
    /// Telegram alerts and the database health check.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configuration">Configuration containing the <c>LogTracker</c> section and <c>ConnectionStrings:LogTrackerDb</c>.</param>
    public static IServiceCollection AddLogTracker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LogTrackerOptions>()
            .Bind(configuration.GetSection(LogTrackerOptions.SectionName))
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(LogTrackerOptions.ConnectionStringName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<LogTrackerOptions>, LogTrackerOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SqlConnectionFactory>();
        services.TryAddSingleton<ILogWriter, SqlLogWriter>();
        services.TryAddSingleton<ILogReportRepository, SqlLogReportRepository>();

        services.TryAddSingleton<LogQueue>();
        services.TryAddSingleton<ErrorRecorder>();
        services.AddHostedService<LogWriterService>();
        services.AddHostedService<LogRetentionService>();

        services.TryAddSingleton<AlertThrottle>();
        services.TryAddSingleton<IErrorNotifier, TelegramErrorNotifier>();
        services.AddHttpClient(TelegramErrorNotifier.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            // The bot token is part of the request URL, and the default HttpClient logging writes URLs to the log.
            .RemoveAllLoggers();

        services.AddProblemDetails(options =>
        {
            // Keep any customization the application registered and add the trace id to every ProblemDetails.
            var previous = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                previous?.Invoke(context);
                context.ProblemDetails.Extensions["traceId"] = RequestInfo.GetTraceId(context.HttpContext);
            };
        });
        services.AddExceptionHandler<LogTrackerExceptionHandler>();

        services.AddHealthChecks()
            .AddCheck<LogDatabaseHealthCheck>("logtracker-database", timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    /// <summary>
    /// Adds request tracking and the exception handler to the pipeline. Call it after
    /// <c>UseStaticFiles</c> (static files are not API calls) and before mapping endpoints.
    /// </summary>
    public static IApplicationBuilder UseLogTracker(this IApplicationBuilder app)
    {
        // Tracking wraps the exception handler, so it sees the final status code of failed requests.
        app.UseMiddleware<RequestTrackingMiddleware>();
        app.UseExceptionHandler();
        return app;
    }

    /// <summary>
    /// Maps the report endpoints under <c>/api/reports</c>, protected by the optional API key.
    /// </summary>
    public static RouteGroupBuilder MapLogTrackerReports(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/api/reports")
            .WithTags("Reports")
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        reports.MapGet("/summary", ReportHandlers.GetSummaryAsync)
            .WithSummary("Request totals, error rates and latency for the last N hours.");

        reports.MapGet("/requests", ReportHandlers.GetRequestsAsync)
            .WithSummary("Tracked requests, newest first.");

        reports.MapGet("/errors", ReportHandlers.GetErrorsAsync)
            .WithSummary("Unhandled exceptions, newest first.");

        reports.MapGet("/errors/{id:long}", ReportHandlers.GetErrorAsync)
            .WithSummary("One unhandled exception, including its stack trace.");

        reports.MapGet("/endpoints", ReportHandlers.GetEndpointStatsAsync)
            .WithSummary("Request count, errors and latency per endpoint, busiest first.");

        reports.MapGet("/hourly-errors", ReportHandlers.GetHourlyErrorsAsync)
            .WithSummary("Error count per hour and exception type; hours without errors are left out.");

        reports.MapGet("/weekly-trend", ReportHandlers.GetWeeklyTrendAsync)
            .WithSummary("Average duration per endpoint and weekday over the last 7 UTC days; null means no requests.");

        return reports;
    }
}
