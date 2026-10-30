using LogTracker.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LogTracker.Core.Reports;

/// <summary>Request handlers for <c>/api/reports</c>. Inputs are validated before the database is queried.</summary>
internal static class ReportHandlers
{
    public const int MaxHours = 24 * 366;
    public const int MaxPageSize = 100;

    public static async Task<Results<Ok<SummaryReport>, ValidationProblem>> GetSummaryAsync(
        ILogReportRepository repository, TimeProvider timeProvider, CancellationToken cancellationToken, int hours = 24)
    {
        if (ValidateHours(hours) is { } problem)
        {
            return problem;
        }

        var to = timeProvider.GetUtcNow().UtcDateTime;
        return TypedResults.Ok(await repository.GetSummaryAsync(to.AddHours(-hours), to, cancellationToken));
    }

    public static async Task<Results<Ok<PagedResult<RequestLogItem>>, ValidationProblem>> GetRequestsAsync(
        ILogReportRepository repository, CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        if (ValidatePaging(page, pageSize) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await repository.GetRequestsAsync(page, pageSize, cancellationToken));
    }

    public static async Task<Results<Ok<PagedResult<ErrorEventItem>>, ValidationProblem>> GetErrorsAsync(
        ILogReportRepository repository, CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        if (ValidatePaging(page, pageSize) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(await repository.GetErrorsAsync(page, pageSize, cancellationToken));
    }

    public static async Task<Results<Ok<ErrorEventDetail>, ProblemHttpResult>> GetErrorAsync(
        long id, ILogReportRepository repository, CancellationToken cancellationToken)
    {
        var error = await repository.GetErrorAsync(id, cancellationToken);
        if (error is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Error event not found.",
                detail: $"There is no error event with id {id}.");
        }

        return TypedResults.Ok(error);
    }

    public static async Task<Results<Ok<IReadOnlyList<EndpointStats>>, ValidationProblem>> GetEndpointStatsAsync(
        ILogReportRepository repository, TimeProvider timeProvider, CancellationToken cancellationToken, int hours = 168)
    {
        if (ValidateHours(hours) is { } problem)
        {
            return problem;
        }

        var to = timeProvider.GetUtcNow().UtcDateTime;
        return TypedResults.Ok(await repository.GetEndpointStatsAsync(to.AddHours(-hours), to, cancellationToken));
    }

    public static async Task<Results<Ok<IReadOnlyList<HourlyErrorCount>>, ValidationProblem>> GetHourlyErrorsAsync(
        ILogReportRepository repository, TimeProvider timeProvider, CancellationToken cancellationToken, int hours = 24)
    {
        if (ValidateHours(hours) is { } problem)
        {
            return problem;
        }

        // Whole hours: the oldest bucket starts on the hour and the newest one is the current hour.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        return TypedResults.Ok(await repository.GetHourlyErrorCountsAsync(currentHour.AddHours(1 - hours), now, cancellationToken));
    }

    public static async Task<Ok<IReadOnlyList<WeeklyTrendRow>>> GetWeeklyTrendAsync(
        ILogReportRepository repository, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await repository.GetWeeklyTrendAsync(timeProvider.GetUtcNow().UtcDateTime, cancellationToken));
    }

    private static ValidationProblem? ValidateHours(int hours) =>
        hours is >= 1 and <= MaxHours
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["hours"] = [$"Must be between 1 and {MaxHours}."],
            });

    private static ValidationProblem? ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Must be 1 or greater."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Must be between 1 and {MaxPageSize}."];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }
}
