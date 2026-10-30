using LogTracker.Core.Data;
using LogTracker.Core.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Tests;

public sealed class ConfigurationTests
{
    private readonly LogTrackerOptionsValidator _validator = new();

    [Fact]
    public void Defaults_with_a_connection_string_are_valid()
    {
        var result = _validator.Validate(null, TestOptions.Create().Value);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Missing_connection_string_is_rejected_with_a_helpful_message()
    {
        var result = _validator.Validate(null, new LogTrackerOptions());

        Assert.True(result.Failed);
        Assert.Contains("ConnectionStrings:LogTrackerDb", result.FailureMessage);
    }

    [Fact]
    public void Telegram_needs_a_token_and_chat_when_enabled()
    {
        var options = TestOptions.Create(o => o.Telegram.Enabled = true).Value;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BotToken", result.FailureMessage);
    }

    [Theory]
    [InlineData("short-key", false)]
    [InlineData("exactly-16-chars", true)]
    [InlineData("", true)]
    public void Api_key_is_empty_or_long_enough(string apiKey, bool valid)
    {
        var options = TestOptions.Create(o => o.ApiKey = apiKey).Value;

        var result = _validator.Validate(null, options);

        Assert.Equal(valid, result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Retention_must_be_at_least_one_day(int days)
    {
        var options = TestOptions.Create(o => o.RetentionDays = days).Value;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Excluded_prefixes_must_start_with_a_slash()
    {
        var options = TestOptions.Create(o => o.ExcludedPathPrefixes.Add("metrics")).Value;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Configured_excluded_prefixes_are_added_to_the_defaults()
    {
        using var services = BuildServices(new()
        {
            ["ConnectionStrings:LogTrackerDb"] = "Server=unused",
            ["LogTracker:ExcludedPathPrefixes:0"] = "/metrics",
        });

        var prefixes = services.GetRequiredService<IOptions<LogTrackerOptions>>().Value.ExcludedPathPrefixes;

        Assert.Contains("/metrics", prefixes);
        Assert.Contains("/api/reports", prefixes);
    }

    [Fact]
    public async Task Host_refuses_to_start_without_a_connection_string()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Services.AddLogTracker(builder.Configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("ConnectionStrings:LogTrackerDb", exception.Message);
    }

    [Fact]
    public async Task Health_check_reports_an_unreachable_database_as_unhealthy()
    {
        // Port 1 on localhost refuses connections immediately.
        var options = TestOptions.Create(o => o.ConnectionString = "Server=tcp:127.0.0.1,1;Connect Timeout=2;Encrypt=False");
        var healthCheck = new LogDatabaseHealthCheck(new SqlConnectionFactory(options));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("unreachable", result.Description);
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogTracker(configuration);
        return services.BuildServiceProvider();
    }
}
