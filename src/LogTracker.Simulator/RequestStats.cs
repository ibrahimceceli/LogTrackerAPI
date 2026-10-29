using System.Diagnostics;

namespace LogTracker.Simulator;

/// <summary>Outcome of one HTTP call: either a status code or an error message.</summary>
internal sealed record RequestResult(PlannedRequest Request, int? StatusCode, TimeSpan Elapsed, string? Error = null);

/// <summary>Number of responses with one status code and their average latency.</summary>
internal sealed record StatusCount(int StatusCode, int Count, double AverageMs);

/// <summary>A consistent copy of the statistics at one point in time.</summary>
internal sealed record StatsSnapshot(
    TimeSpan Elapsed,
    int Responses,
    int Failures,
    double AverageMs,
    IReadOnlyList<StatusCount> ByStatusCode)
{
    public int Total => Responses + Failures;
}

/// <summary>
/// Collects counts per status code and latencies. All virtual users write to the same
/// instance at the same time, so every access goes through one lock.
/// </summary>
internal sealed class RequestStats
{
    private readonly Lock _lock = new();
    private readonly Stopwatch _runTime = Stopwatch.StartNew();
    private readonly SortedDictionary<int, (int Count, double TotalMs)> _byStatusCode = [];
    private int _failures;

    public void Record(RequestResult result)
    {
        lock (_lock)
        {
            if (result.StatusCode is not int statusCode)
            {
                // No response at all (connection refused, timeout, ...) - kept out of the latency average.
                _failures++;
                return;
            }

            (int count, double totalMs) = _byStatusCode.GetValueOrDefault(statusCode);
            _byStatusCode[statusCode] = (count + 1, totalMs + result.Elapsed.TotalMilliseconds);
        }
    }

    public StatsSnapshot Snapshot()
    {
        lock (_lock)
        {
            List<StatusCount> byStatus =
            [
                .. _byStatusCode.Select(entry => new StatusCount(entry.Key, entry.Value.Count, entry.Value.TotalMs / entry.Value.Count)),
            ];

            int responses = byStatus.Sum(s => s.Count);
            double totalMs = _byStatusCode.Values.Sum(v => v.TotalMs);
            double averageMs = responses == 0 ? 0 : totalMs / responses;

            return new StatsSnapshot(_runTime.Elapsed, responses, _failures, averageMs, byStatus);
        }
    }
}
