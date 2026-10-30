using LogTracker.Core.Notifications;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LogTracker.Core.Tests;

public sealed class LogWriterServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeLogWriter _writer = new();
    private readonly FakeErrorNotifier _notifier = new();

    [Fact]
    public async Task Writes_queued_entries_in_batches()
    {
        var options = TestOptions.Create(o => o.BatchSize = 2);
        var queue = new LogQueue(options);
        for (var i = 0; i < 5; i++)
        {
            queue.TryEnqueue(CreateRequest(i));
        }

        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Wait.UntilAsync(() => _writer.Requests.Count() == 5);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.All(_writer.RequestBatches, batch => Assert.True(batch.Count <= 2));
        Assert.Equal(5, service.WrittenCount);
    }

    [Fact]
    public async Task Keeps_running_and_counts_lost_entries_when_the_database_fails()
    {
        var options = TestOptions.Create();
        var queue = new LogQueue(options);
        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);

        _writer.Fail = true;
        queue.TryEnqueue(CreateRequest(1));
        await Wait.UntilAsync(() => service.FailedCount == 1);

        _writer.Fail = false;
        queue.TryEnqueue(CreateRequest(2));
        // Skip the back-off pause that follows a failed write.
        await Wait.UntilAsync(() => _writer.Requests.Any(), onEachPoll: () => _time.Advance(LogWriterService.FailureBackoff));
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(_writer.Requests).DurationMs);
    }

    [Fact]
    public async Task Sends_alerts_even_when_the_database_is_down()
    {
        var options = TestOptions.Create(EnableTelegram);
        var queue = new LogQueue(options);
        _writer.Fail = true;

        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);
        queue.TryEnqueue(CreateError("System.TimeoutException"));
        await Wait.UntilAsync(() => !_notifier.Sent.IsEmpty);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal("System.TimeoutException", Assert.Single(_notifier.Sent).Error.ErrorType);
    }

    [Fact]
    public async Task Sends_one_alert_per_error_type_within_the_throttle_window()
    {
        var options = TestOptions.Create(EnableTelegram);
        var queue = new LogQueue(options);
        queue.TryEnqueue(CreateError("System.TimeoutException"));
        queue.TryEnqueue(CreateError("System.TimeoutException"));
        queue.TryEnqueue(CreateError("System.ArgumentException"));

        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Wait.UntilAsync(() => _writer.Errors.Count() == 3);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "System.TimeoutException", "System.ArgumentException" }, _notifier.Sent.Select(s => s.Error.ErrorType));
    }

    [Fact]
    public async Task Sends_no_alerts_when_telegram_is_disabled()
    {
        var options = TestOptions.Create();
        var queue = new LogQueue(options);
        queue.TryEnqueue(CreateError("System.TimeoutException"));

        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);
        await Wait.UntilAsync(() => _writer.Errors.Any());
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Flushes_entries_that_are_still_queued_at_shutdown()
    {
        var options = TestOptions.Create(o => o.BatchSize = 1);
        var queue = new LogQueue(options);
        var gate = new TaskCompletionSource();
        _writer.Gate = gate.Task;

        var service = CreateService(queue, options);
        await service.StartAsync(TestContext.Current.CancellationToken);
        for (var i = 0; i < 3; i++)
        {
            queue.TryEnqueue(CreateRequest(i));
        }

        // The first write is stuck while shutdown begins; the other two entries are still queued.
        var stopping = service.StopAsync(TestContext.Current.CancellationToken);
        gate.SetResult();
        await stopping;

        Assert.Equal(3, _writer.Requests.Count());
        Assert.False(queue.TryEnqueue(CreateRequest(99)), "The queue should be closed after shutdown.");
    }

    [Fact]
    public async Task Flushes_queued_entries_even_when_stopped_before_the_loop_started()
    {
        var options = TestOptions.Create();
        var queue = new LogQueue(options);
        for (var i = 0; i < 3; i++)
        {
            queue.TryEnqueue(CreateRequest(i));
        }

        // A cancelled start token is the deterministic version of a host that stops right after starting:
        // BackgroundService then never runs ExecuteAsync at all.
        var service = CreateService(queue, options);
        await service.StartAsync(new CancellationToken(canceled: true));
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, _writer.Requests.Count());
    }

    [Fact]
    public async Task Flushes_queued_entries_when_stopped_immediately_after_starting()
    {
        // Repeated because the race between StartAsync and StopAsync only shows up some of the time.
        for (var run = 0; run < 50; run++)
        {
            var writer = new FakeLogWriter();
            var options = TestOptions.Create();
            var queue = new LogQueue(options);
            queue.TryEnqueue(CreateRequest(run));

            var service = new LogWriterService(queue, writer, _notifier, new AlertThrottle(_time), options, _time,
                NullLogger<LogWriterService>.Instance);
            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.StopAsync(TestContext.Current.CancellationToken);

            Assert.Single(writer.Requests);
        }
    }

    [Fact]
    public void Queue_drops_and_counts_entries_when_full()
    {
        var queue = new LogQueue(TestOptions.Create(o => o.QueueCapacity = 2));

        Assert.True(queue.TryEnqueue(CreateRequest(1)));
        Assert.True(queue.TryEnqueue(CreateRequest(2)));
        Assert.False(queue.TryEnqueue(CreateRequest(3)));
        Assert.Equal(1, queue.DroppedCount);
    }

    private LogWriterService CreateService(LogQueue queue, IOptions<LogTrackerOptions> options) =>
        new(queue, _writer, _notifier, new AlertThrottle(_time), options, _time, NullLogger<LogWriterService>.Instance);

    private static void EnableTelegram(LogTrackerOptions options)
    {
        options.Telegram.Enabled = true;
        options.Telegram.BotToken = "token";
        options.Telegram.ChatId = "chat";
    }

    private static RequestLogEntry CreateRequest(int durationMs) =>
        new(DateTime.UtcNow, "GET", "/api/products", "/api/products", 200, durationMs, null);

    private static ErrorEventEntry CreateError(string type) =>
        new(DateTime.UtcNow, type, "message", null, null, "GET", "/api/test", "/api/test", 500, null);
}
