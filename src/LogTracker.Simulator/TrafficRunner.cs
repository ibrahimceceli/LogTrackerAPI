using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Authentication;

namespace LogTracker.Simulator;

/// <summary>
/// Runs the configured number of virtual users. Each user repeatedly waits a random
/// moment, asks the <see cref="TrafficMix"/> what to call and sends that request.
/// </summary>
internal sealed class TrafficRunner(
    HttpClient client,
    TrafficMix mix,
    RequestStats stats,
    ConsoleReporter reporter,
    SimulatorOptions options)
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        List<Task> tasks =
        [
            .. Enumerable.Range(0, options.Concurrency).Select(_ => RunVirtualUserAsync(stoppingToken)),
            PrintPeriodicSummariesAsync(stoppingToken),
        ];

        await Task.WhenAll(tasks);
    }

    private async Task RunVirtualUserAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                // The pause comes first so that the users do not all start at the same instant.
                await Task.Delay(Random.Shared.Next(options.MinDelayMs, options.MaxDelayMs + 1), stoppingToken);

                RequestResult result = await SendAsync(mix.Next(), stoppingToken);
                stats.Record(result);
                reporter.PrintRequest(result);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown (Ctrl+C, SIGTERM or --duration elapsed).
        }
    }

    private async Task<RequestResult> SendAsync(PlannedRequest planned, CancellationToken stoppingToken)
    {
        // A relative URI (no leading slash) keeps a path prefix of the base URL.
        using var request = new HttpRequestMessage(planned.Method, planned.Path.TrimStart('/'));
        if (planned.Body is not null)
        {
            request.Content = JsonContent.Create(planned.Body, planned.Body.GetType());
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, stoppingToken);
            mix.Observe(planned, response);
            return new RequestResult(planned, (int)response.StatusCode, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw; // shutting down - RunVirtualUserAsync ends quietly
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A failed request is part of the result, not a reason to stop the simulator:
            // the API may still be starting or restarting.
            return new RequestResult(planned, null, stopwatch.Elapsed, Describe(ex));
        }
    }

    private string Describe(Exception exception) => exception switch
    {
        // When our own token is not cancelled, a TaskCanceledException means HttpClient.Timeout elapsed.
        TaskCanceledException => $"no answer within {client.Timeout.TotalSeconds:0} s",
        HttpRequestException { InnerException: AuthenticationException } =>
            "TLS handshake failed - run 'dotnet dev-certs https --trust' or use http://localhost:5269",
        _ => exception.Message,
    };

    private async Task PrintPeriodicSummariesAsync(CancellationToken stoppingToken)
    {
        if (options.SummaryInterval <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.SummaryInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                reporter.PrintSummary(stats.Snapshot(), isFinal: false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown; Program prints the final summary.
        }
    }
}
