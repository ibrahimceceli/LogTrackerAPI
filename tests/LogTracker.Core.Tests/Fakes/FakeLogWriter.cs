using System.Collections.Concurrent;
using LogTracker.Core.Data;
using LogTracker.Core.Pipeline;

namespace LogTracker.Core.Tests.Fakes;

/// <summary>Records written batches; can be told to fail or to block, like a database that is down or slow.</summary>
internal sealed class FakeLogWriter : ILogWriter
{
    public ConcurrentQueue<IReadOnlyList<RequestLogEntry>> RequestBatches { get; } = new();

    public ConcurrentQueue<IReadOnlyList<ErrorEventEntry>> ErrorBatches { get; } = new();

    public bool Fail { get; set; }

    /// <summary>When set, every write waits for this task first.</summary>
    public Task? Gate { get; set; }

    public IEnumerable<RequestLogEntry> Requests => RequestBatches.SelectMany(batch => batch);

    public IEnumerable<ErrorEventEntry> Errors => ErrorBatches.SelectMany(batch => batch);

    public async Task WriteRequestsAsync(IReadOnlyList<RequestLogEntry> entries, CancellationToken cancellationToken)
    {
        await WaitAndMaybeFailAsync();
        RequestBatches.Enqueue(entries.ToList());
    }

    public async Task WriteErrorsAsync(IReadOnlyList<ErrorEventEntry> entries, CancellationToken cancellationToken)
    {
        await WaitAndMaybeFailAsync();
        ErrorBatches.Enqueue(entries.ToList());
    }

    public Task<PurgeResult> PurgeAsync(int retentionDays, CancellationToken cancellationToken) =>
        Task.FromResult(new PurgeResult());

    private async Task WaitAndMaybeFailAsync()
    {
        if (Gate is not null)
        {
            await Gate;
        }

        if (Fail)
        {
            throw new InvalidOperationException("Simulated database outage.");
        }
    }
}
