using System.Net;

namespace LogTracker.Simulator;

/// <summary>
/// Writes request lines and summaries to the console. Several virtual users print at the
/// same time, so a lock keeps each line (and its color) from mixing with another line.
/// </summary>
internal sealed class ConsoleReporter(bool useColors)
{
    private readonly Lock _lock = new();

    public void PrintStart(SimulatorOptions options)
    {
        string duration = options.Duration is TimeSpan d ? $"{d.TotalSeconds:0} s" : "until Ctrl+C";
        Write(ConsoleColor.Cyan,
            "LogTracker traffic simulator",
            $"  API:          {options.BaseUrl}",
            $"  Users:        {options.Concurrency} (pause {options.MinDelayMs}-{options.MaxDelayMs} ms between requests)",
            $"  Error share:  {options.ErrorPercent}%",
            $"  Duration:     {duration}",
            "Press Ctrl+C to stop.",
            "");
    }

    public void PrintRequest(RequestResult result)
    {
        PlannedRequest request = result.Request;
        string status = result.StatusCode?.ToString() ?? "---";
        string line = $"{DateTime.Now:HH:mm:ss}  {request.Method.Method,-4} {request.Path,-36} {status}  {result.Elapsed.TotalMilliseconds,6:0} ms";
        if (result.Error is not null)
        {
            line += $"  {result.Error}";
        }

        Write(ColorFor(result.StatusCode), line);
    }

    public void PrintSummary(StatsSnapshot stats, bool isFinal)
    {
        string title = isFinal ? "Final summary" : "Summary so far";
        double perSecond = stats.Elapsed.TotalSeconds > 0 ? stats.Total / stats.Elapsed.TotalSeconds : 0;

        var lines = new List<string>
        {
            "",
            $"--- {title}: {stats.Total} requests in {stats.Elapsed:hh\\:mm\\:ss} ({perSecond:0.0} per second) ---",
        };

        foreach (StatusCount status in stats.ByStatusCode)
        {
            string name = $"{status.StatusCode} {(HttpStatusCode)status.StatusCode}";
            lines.Add($"  {name,-28} {status.Count,7}   avg {status.AverageMs,6:0} ms");
        }

        if (stats.Failures > 0)
        {
            lines.Add($"  {"no response",-28} {stats.Failures,7}");
        }

        lines.Add(stats.Responses > 0
            ? $"  Average latency: {stats.AverageMs:0} ms"
            : "  No response received - is the API running at the given --base-url?");
        lines.Add("");

        Write(ConsoleColor.Cyan, [.. lines]);
    }

    private static ConsoleColor ColorFor(int? statusCode) => statusCode switch
    {
        null => ConsoleColor.Magenta,
        < 300 => ConsoleColor.Green,
        < 400 => ConsoleColor.Cyan,
        < 500 => ConsoleColor.Yellow,
        _ => ConsoleColor.Red,
    };

    private void Write(ConsoleColor color, params string[] lines)
    {
        lock (_lock)
        {
            if (useColors)
            {
                Console.ForegroundColor = color;
            }

            foreach (string line in lines)
            {
                Console.WriteLine(line);
            }

            if (useColors)
            {
                Console.ResetColor();
            }
        }
    }
}
