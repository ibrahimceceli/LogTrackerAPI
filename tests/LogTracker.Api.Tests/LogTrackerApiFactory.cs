using LogTracker.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogTracker.Api.Tests;

/// <summary>Runs the real API in memory with <see cref="InMemoryLogStore"/> instead of SQL Server.</summary>
internal sealed class LogTrackerApiFactory : WebApplicationFactory<Program>
{
    public InMemoryLogStore Store { get; } = new();

    public string ApiKey { get; init; } = "";

    public bool EnableTestEndpoints { get; init; } = true;

    /// <summary>"Testing" by default, so appsettings.Development.json (and its LocalDB connection) is not loaded.</summary>
    public string EnvironmentName { get; init; } = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:LogTrackerDb"] = "Server=unused;Database=unused",
            ["LogTracker:ApiKey"] = ApiKey,
            ["LogTracker:EnableTestEndpoints"] = EnableTestEndpoints.ToString(),
        }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogWriter>(Store);
            services.AddSingleton<ILogReportRepository>(Store);
        });
    }

    /// <summary>
    /// Stops the app. The background writer flushes its queue on shutdown, so afterwards <see cref="Store"/>
    /// holds everything that will ever be written and "nothing was recorded" can be checked reliably.
    /// </summary>
    public ValueTask StopAndFlushAsync() => DisposeAsync();
}
