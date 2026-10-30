using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Pipeline;

/// <summary>
/// Hands log entries from requests to the background writer. Requests only add to an in-memory
/// queue, so a slow or unavailable database never slows them down.
/// </summary>
internal sealed class LogQueue
{
    private readonly Channel<LogEntry> _channel;
    private long _droppedCount;

    public LogQueue(IOptions<LogTrackerOptions> options)
    {
        // In Wait mode TryWrite returns false when the queue is full, which lets us count drops.
        // Nothing ever waits, because requests only call TryWrite.
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public ChannelReader<LogEntry> Reader => _channel.Reader;

    /// <summary>Entries rejected because the queue was full or already closed.</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>Adds an entry without blocking. Returns false when the entry had to be dropped.</summary>
    public bool TryEnqueue(LogEntry entry)
    {
        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }

    /// <summary>Stops accepting entries; used at shutdown before the last flush.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
