using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogTracker.Core.Data;

/// <summary>Healthy when the log database is reachable and its tables exist.</summary>
internal sealed class LogDatabaseHealthCheck(SqlConnectionFactory connectionFactory) : IHealthCheck
{
    private const string TablesExistSql = """
        SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.RequestLogs', N'U') IS NOT NULL
                          AND OBJECT_ID(N'dbo.ErrorEvents', N'U') IS NOT NULL
                         THEN 1 ELSE 0 END AS bit);
        """;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = connectionFactory.Create();
            var tablesExist = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(TablesExistSql, cancellationToken: cancellationToken));

            return tablesExist
                ? HealthCheckResult.Healthy("The log database is reachable.")
                : HealthCheckResult.Unhealthy("The log database is reachable, but the LogTracker tables are missing. Run the scripts in database/.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The log database is unreachable.", exception);
        }
    }
}
