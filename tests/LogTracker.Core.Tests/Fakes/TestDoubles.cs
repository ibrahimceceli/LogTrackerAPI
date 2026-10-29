using System.Collections.Concurrent;
using System.Net;
using LogTracker.Core.Notifications;
using LogTracker.Core.Pipeline;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Tests.Fakes;

internal sealed class FakeErrorNotifier : IErrorNotifier
{
    public ConcurrentQueue<(ErrorEventEntry Error, int SuppressedCount)> Sent { get; } = new();

    public Task NotifyAsync(ErrorEventEntry error, int suppressedCount, CancellationToken cancellationToken)
    {
        Sent.Enqueue((error, suppressedCount));
        return Task.CompletedTask;
    }
}

/// <summary>Answers every HTTP request with a fixed response and remembers what was sent.</summary>
internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody = "{\"ok\":true}") : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) };
    }
}

internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "LogTracker.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal static class TestOptions
{
    public static IOptions<LogTrackerOptions> Create(Action<LogTrackerOptions>? configure = null)
    {
        var options = new LogTrackerOptions { ConnectionString = "Server=unused;Database=unused" };
        configure?.Invoke(options);
        return Options.Create(options);
    }
}

internal static class Wait
{
    /// <summary>Polls until the condition holds; background services finish their work asynchronously.</summary>
    public static async Task UntilAsync(Func<bool> condition, Action? onEachPoll = null, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            onEachPoll?.Invoke();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
