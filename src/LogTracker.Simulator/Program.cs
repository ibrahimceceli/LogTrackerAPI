using System.Runtime.InteropServices;
using LogTracker.Simulator;

if (SimulatorOptionsParser.IsHelpRequested(args))
{
    Console.Write(SimulatorOptionsParser.HelpText());
    return 0;
}

SimulatorOptions options;
try
{
    options = SimulatorOptionsParser.Parse(args, Environment.GetEnvironmentVariable);
}
catch (OptionException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Run with --help to see all options.");
    return 2;
}

// Ctrl+C sends SIGINT and "docker stop" sends SIGTERM. Instead of letting the process die,
// both cancel this token: in-flight requests and pauses end at once, then the final summary prints.
using var stopping = new CancellationTokenSource();
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, StopGracefully);
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, StopGracefully);
if (options.Duration is TimeSpan duration)
{
    stopping.CancelAfter(duration);
}

// https://no-color.org: any non-empty NO_COLOR value turns colors off.
var reporter = new ConsoleReporter(useColors: string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
var stats = new RequestStats();
using HttpClient client = ApiClientFactory.Create(options.BaseUrl);
var runner = new TrafficRunner(client, new TrafficMix(options.ErrorPercent), stats, reporter, options);

reporter.PrintStart(options);
await runner.RunAsync(stopping.Token);

StatsSnapshot finalStats = stats.Snapshot();
reporter.PrintSummary(finalStats, isFinal: true);

// Exit code 1 tells scripts (and the CI smoke test) that the API never answered.
return finalStats.Responses == 0 ? 1 : 0;

void StopGracefully(PosixSignalContext context)
{
    context.Cancel = true;
    stopping.Cancel();
}
