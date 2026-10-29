namespace LogTracker.Api.Endpoints;

/// <summary>Random delays that make the demo endpoints look like they talk to a database.</summary>
internal static class SimulatedLatency
{
    public static Task WaitAsync(int minMs, int maxMs, CancellationToken cancellationToken) =>
        Task.Delay(Random.Shared.Next(minMs, maxMs + 1), cancellationToken);
}
