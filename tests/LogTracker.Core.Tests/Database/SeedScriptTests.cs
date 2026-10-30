using Dapper;
using LogTracker.Core.Data;
using LogTracker.Core.Tests.Fakes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogTracker.Core.Tests.Database;

/// <summary>Uses its own database, because the seed script only runs on empty tables.</summary>
[Collection(DatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SeedScriptTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task Health_check_reports_missing_tables_until_the_scripts_have_run()
    {
        await using var database = await TestDatabase.CreateAsync();
        var healthCheck = new LogDatabaseHealthCheck(new SqlConnectionFactory(
            TestOptions.Create(options => options.ConnectionString = database.ConnectionString)));

        var before = await healthCheck.CheckHealthAsync(new HealthCheckContext(), Ct);
        await TestDatabase.RunSchemaScriptsAsync(database.ConnectionString);
        var after = await healthCheck.CheckHealthAsync(new HealthCheckContext(), Ct);

        Assert.Equal(HealthStatus.Unhealthy, before.Status);
        Assert.Contains("tables are missing", before.Description);
        Assert.Equal(HealthStatus.Healthy, after.Status);
    }

    [DatabaseFact]
    public async Task Seed_fills_empty_tables_once()
    {
        await using var database = await TestDatabase.CreateAsync();
        await TestDatabase.RunSchemaScriptsAsync(database.ConnectionString);

        await TestDatabase.RunScriptAsync(database.ConnectionString, "90_seed_demo_data.sql");
        var first = await CountRowsAsync(database.ConnectionString);
        await TestDatabase.RunScriptAsync(database.ConnectionString, "90_seed_demo_data.sql");
        var second = await CountRowsAsync(database.ConnectionString);

        Assert.Equal(4000, first.Requests);
        Assert.True(first.Errors > 0);
        Assert.Equal(first, second);
    }

    private static async Task<(int Requests, int Errors)> CountRowsAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleAsync<(int, int)>(new CommandDefinition(
            "SELECT (SELECT COUNT(*) FROM dbo.RequestLogs), (SELECT COUNT(*) FROM dbo.ErrorEvents);",
            cancellationToken: Ct));
    }
}
