using System.Text.Json;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Tests.Fakes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogTracker.Core.Tests;

public sealed class LogTrackerExceptionHandlerTests
{
    [Fact]
    public async Task Answers_500_problem_details_with_trace_id_and_hides_details_outside_development()
    {
        using var services = BuildServices(Environments.Production);
        var context = CreateContext(services);

        var handled = await HandleAsync(services, context, new InvalidOperationException("secret internal detail"));

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        using var problem = await ReadBodyAsync(context);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("trace-1", problem.RootElement.GetProperty("traceId").GetString());
        Assert.False(problem.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain("secret internal detail", problem.RootElement.GetRawText());
    }

    [Fact]
    public async Task Shows_the_exception_message_in_development()
    {
        using var services = BuildServices(Environments.Development);
        var context = CreateContext(services);

        await HandleAsync(services, context, new TimeoutException("Simulated timeout."));

        using var problem = await ReadBodyAsync(context);
        Assert.Equal("Simulated timeout.", problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Records_the_error_with_request_details()
    {
        using var services = BuildServices(Environments.Production);
        var context = CreateContext(services);

        await HandleAsync(services, context, new KeyNotFoundException("missing"));

        var queue = services.GetRequiredService<LogQueue>();
        Assert.True(queue.Reader.TryRead(out var entry));
        var error = Assert.IsType<ErrorEventEntry>(entry);
        Assert.Equal("System.Collections.Generic.KeyNotFoundException", error.ErrorType);
        Assert.Equal("missing", error.Message);
        Assert.Equal("DELETE", error.Method);
        Assert.Equal("/api/orders/5", error.Path);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal("trace-1", error.TraceId);
        Assert.Contains("KeyNotFoundException", error.StackTrace);
    }

    [Fact]
    public async Task Answers_bad_requests_with_400_without_recording_an_error()
    {
        using var services = BuildServices(Environments.Development);
        var context = CreateContext(services);

        var handled = await HandleAsync(services, context, new BadHttpRequestException("Malformed JSON.", 400));

        Assert.True(handled);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.False(services.GetRequiredService<LogQueue>().Reader.TryRead(out _));
    }

    [Fact]
    public async Task Records_but_does_not_answer_when_the_response_has_already_started()
    {
        using var services = BuildServices(Environments.Production);
        var context = CreateContext(services);
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var handled = await HandleAsync(services, context, new InvalidOperationException("late failure"));

        Assert.False(handled);
        Assert.True(services.GetRequiredService<LogQueue>().Reader.TryRead(out _));
    }

    private static ServiceProvider BuildServices(string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LogTrackerDb"] = "Server=unused;Database=unused",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        services.AddLogTracker(configuration);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-1" };
        context.Request.Method = "DELETE";
        context.Request.Path = "/api/orders/5";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<bool> HandleAsync(IServiceProvider services, HttpContext context, Exception exception)
    {
        var handler = Assert.Single(services.GetServices<IExceptionHandler>());
        return await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
