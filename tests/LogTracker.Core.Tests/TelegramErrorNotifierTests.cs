using System.Net;
using System.Text.Json;
using LogTracker.Core.Notifications;
using LogTracker.Core.Pipeline;
using LogTracker.Core.Tests.Fakes;

namespace LogTracker.Core.Tests;

public sealed class TelegramErrorNotifierTests
{
    [Fact]
    public async Task Posts_html_message_with_escaped_user_input()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var notifier = CreateNotifier(handler);
        var error = CreateError(message: "Bad <script>alert('x')</script> & more", path: "/api/<b>");

        await notifier.NotifyAsync(error, suppressedCount: 0, TestContext.Current.CancellationToken);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.telegram.org/bot123:abc/sendMessage", request.RequestUri!.ToString());

        using var json = JsonDocument.Parse(body);
        Assert.Equal("42", json.RootElement.GetProperty("chat_id").GetString());
        Assert.Equal("HTML", json.RootElement.GetProperty("parse_mode").GetString());

        var text = json.RootElement.GetProperty("text").GetString()!;
        Assert.Contains("Bad &lt;script&gt;alert('x')&lt;/script&gt; &amp; more", text);
        Assert.Contains("/api/&lt;b&gt;", text);
        Assert.DoesNotContain("<script>", text);
    }

    [Fact]
    public async Task Throws_when_telegram_rejects_the_message()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, "{\"ok\":false,\"description\":\"Bad Request: chat not found\"}");
        var notifier = CreateNotifier(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => notifier.NotifyAsync(CreateError(), 0, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("chat not found", exception.Message);
    }

    [Fact]
    public void Mentions_skipped_alerts()
    {
        var text = TelegramErrorNotifier.BuildMessage(CreateError(), suppressedCount: 3);

        Assert.Contains("3 more alert(s) of this type were skipped", text);
    }

    [Fact]
    public void Stays_within_telegram_length_limit_even_when_every_character_needs_escaping()
    {
        var worstCase = new string('&', 10_000);
        var error = CreateError(message: worstCase, path: worstCase) with
        {
            ErrorType = worstCase,
            Endpoint = worstCase,
            Method = worstCase,
            TraceId = worstCase,
        };

        var text = TelegramErrorNotifier.BuildMessage(error, suppressedCount: 1000);

        Assert.True(text.Length <= TelegramErrorNotifier.MaxMessageLength, $"Length was {text.Length}.");
    }

    private static TelegramErrorNotifier CreateNotifier(HttpMessageHandler handler) =>
        new(new StubHttpClientFactory(handler), TestOptions.Create(options =>
        {
            options.Telegram.Enabled = true;
            options.Telegram.BotToken = "123:abc";
            options.Telegram.ChatId = "42";
        }));

    private static ErrorEventEntry CreateError(string message = "Simulated timeout.", string path = "/api/test/errors/timeout") =>
        new(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc), "System.TimeoutException", message,
            "LogTracker.Api.Endpoints.TestEndpoints", "stack", "GET", path, "/api/test/errors/{type}", 500, "trace-1");
}
