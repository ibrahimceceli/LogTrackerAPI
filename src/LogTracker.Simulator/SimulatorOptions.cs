using System.Globalization;
using System.Text;

namespace LogTracker.Simulator;

/// <summary>Settings for one simulator run.</summary>
internal sealed record SimulatorOptions(
    Uri BaseUrl,
    int Concurrency,
    int MinDelayMs,
    int MaxDelayMs,
    int ErrorPercent,
    TimeSpan? Duration,
    TimeSpan SummaryInterval);

/// <summary>Thrown when a command-line argument or environment variable has an invalid value.</summary>
internal sealed class OptionException(string message) : Exception(message);

/// <summary>
/// Reads <see cref="SimulatorOptions"/> from command-line flags and environment variables.
/// A flag wins over its environment variable, which wins over the default. Environment
/// variables make the simulator easy to configure in Docker Compose.
/// </summary>
internal static class SimulatorOptionsParser
{
    private sealed record Option(string Flag, string ValueName, string EnvironmentVariable, string DefaultValue, string Description);

    private static readonly Option BaseUrl = new(
        "--base-url", "<url>", "SIMULATOR_BASE_URL", "https://localhost:7292",
        "Address of the LogTracker API.");

    private static readonly Option Concurrency = new(
        "--concurrency", "<n>", "SIMULATOR_CONCURRENCY", "4",
        "Number of virtual users sending requests in parallel (1-50).");

    private static readonly Option MinDelay = new(
        "--min-delay", "<ms>", "SIMULATOR_MIN_DELAY_MS", "200",
        "Shortest pause between two requests of one virtual user.");

    private static readonly Option MaxDelay = new(
        "--max-delay", "<ms>", "SIMULATOR_MAX_DELAY_MS", "1500",
        "Longest pause between two requests of one virtual user.");

    private static readonly Option ErrorPercent = new(
        "--error-percent", "<0-100>", "SIMULATOR_ERROR_PERCENT", "10",
        "Share of requests sent to /api/test/errors/{type} (they return 500).");

    private static readonly Option Duration = new(
        "--duration", "<seconds>", "SIMULATOR_DURATION_SECONDS", "",
        "Stop automatically after this many seconds. Empty or 0 = run until Ctrl+C.");

    private static readonly Option SummaryInterval = new(
        "--summary-interval", "<seconds>", "SIMULATOR_SUMMARY_INTERVAL_SECONDS", "30",
        "Print a summary every N seconds. 0 = only the final summary.");

    private static readonly Option[] AllOptions =
        [BaseUrl, Concurrency, MinDelay, MaxDelay, ErrorPercent, Duration, SummaryInterval];

    public static bool IsHelpRequested(IEnumerable<string> args) =>
        args.Any(arg => arg is "-h" or "--help" or "-?" or "/?");

    public static SimulatorOptions Parse(IReadOnlyList<string> args, Func<string, string?> getEnvironmentVariable)
    {
        Dictionary<Option, string> values = AllOptions.ToDictionary(
            option => option,
            option => NullIfEmpty(getEnvironmentVariable(option.EnvironmentVariable)) ?? option.DefaultValue);

        for (int i = 0; i < args.Count; i++)
        {
            // Accept both "--name value" and "--name=value".
            string flag = args[i];
            string? value = null;
            int equalsIndex = flag.IndexOf('=');
            if (flag.StartsWith("--", StringComparison.Ordinal) && equalsIndex > 0)
            {
                value = flag[(equalsIndex + 1)..];
                flag = flag[..equalsIndex];
            }

            Option option = AllOptions.FirstOrDefault(o => o.Flag == flag)
                ?? throw new OptionException($"Unknown argument '{args[i]}'.");

            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    throw new OptionException($"{option.Flag} needs a value {option.ValueName}.");
                }

                value = args[++i];
            }

            values[option] = value;
        }

        int minDelay = ParseInt(MinDelay, values[MinDelay], 0, 60_000);
        int maxDelay = ParseInt(MaxDelay, values[MaxDelay], 0, 60_000);
        if (maxDelay < minDelay)
        {
            throw new OptionException($"{MaxDelay.Flag} ({maxDelay}) must not be smaller than {MinDelay.Flag} ({minDelay}).");
        }

        // One week is far more than a demo needs and stays below the CancelAfter limit (~49 days).
        int durationSeconds = ParseInt(Duration, values[Duration], 0, 604_800, emptyValue: 0);
        int summarySeconds = ParseInt(SummaryInterval, values[SummaryInterval], 0, 86_400);

        return new SimulatorOptions(
            BaseUrl: ParseBaseUrl(values[BaseUrl]),
            Concurrency: ParseInt(Concurrency, values[Concurrency], 1, 50),
            MinDelayMs: minDelay,
            MaxDelayMs: maxDelay,
            ErrorPercent: ParseInt(ErrorPercent, values[ErrorPercent], 0, 100),
            Duration: durationSeconds == 0 ? null : TimeSpan.FromSeconds(durationSeconds),
            SummaryInterval: TimeSpan.FromSeconds(summarySeconds));
    }

    public static string HelpText()
    {
        var help = new StringBuilder();
        help.AppendLine("LogTracker traffic simulator - sends realistic demo traffic to a LogTracker API.");
        help.AppendLine();
        help.AppendLine("Usage: dotnet run --project src/LogTracker.Simulator -- [options]");
        help.AppendLine();
        help.AppendLine("Options (a flag wins over its environment variable):");

        foreach (Option option in AllOptions)
        {
            string defaultText = option.DefaultValue.Length == 0 ? "none" : option.DefaultValue;
            help.AppendLine($"  {option.Flag} {option.ValueName}");
            help.AppendLine($"      {option.Description}");
            help.AppendLine($"      env: {option.EnvironmentVariable}, default: {defaultText}");
        }

        help.AppendLine("  -h, --help");
        help.AppendLine("      Show this help.");
        help.AppendLine();
        help.AppendLine("Stop with Ctrl+C (or SIGTERM, e.g. 'docker stop'); a final summary is printed.");
        help.AppendLine("HTTPS on localhost accepts the ASP.NET Core development certificate. If the TLS");
        help.AppendLine("handshake still fails, run 'dotnet dev-certs https --trust' or use http://localhost:5269.");
        help.AppendLine("Set NO_COLOR=1 to disable colored output.");
        return help.ToString();
    }

    private static Uri ParseBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new OptionException($"{BaseUrl.Flag} / {BaseUrl.EnvironmentVariable} must be an absolute http or https URL (got '{value}').");
        }

        // Request paths are resolved relative to the base URL, which only keeps a path
        // prefix such as "/logtracker" when the base URL ends with a slash.
        return uri.AbsolutePath.EndsWith('/') ? uri : new Uri(uri + "/");
    }

    private static int ParseInt(Option option, string value, int min, int max, int? emptyValue = null)
    {
        if (value.Length == 0 && emptyValue is int fallback)
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < min || result > max)
        {
            throw new OptionException($"{option.Flag} / {option.EnvironmentVariable} must be a whole number between {min} and {max} (got '{value}').");
        }

        return result;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
