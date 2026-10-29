using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using LogTracker.Core.Pipeline;
using Microsoft.Extensions.Options;

namespace LogTracker.Core.Notifications;

/// <summary>Sends alerts through the Telegram Bot API.</summary>
/// <remarks>
/// Uses a named client from <see cref="IHttpClientFactory"/> on every send. A typed client would be
/// captured for the whole lifetime of the singleton background service and never get fresh connections.
/// </remarks>
internal sealed class TelegramErrorNotifier(IHttpClientFactory httpClientFactory, IOptions<LogTrackerOptions> options)
    : IErrorNotifier
{
    public const string HttpClientName = "LogTracker.Telegram";
    private const string ApiBaseUrl = "https://api.telegram.org";

    // Telegram rejects messages longer than 4096 characters. Escaping can make a field up to five times
    // longer ("&" becomes "&amp;"), so the raw fields are capped to stay below the limit in the worst case.
    internal const int MaxMessageLength = 4096;
    private const int MaxShortFieldLength = 80;
    private const int MaxExceptionMessageLength = 300;

    public async Task NotifyAsync(ErrorEventEntry error, int suppressedCount, CancellationToken cancellationToken)
    {
        var settings = options.Value.Telegram;
        var request = new SendMessageRequest(settings.ChatId ?? string.Empty, BuildMessage(error, suppressedCount), "HTML");

        // Always an absolute URL: tokens look like "123456:ABC...", and a relative "bot123456:ABC..."
        // would be parsed as a URI with the scheme "bot123456".
        var url = $"{ApiBaseUrl}/bot{settings.BotToken}/sendMessage";

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Telegram explains rejections (unknown chat, malformed HTML) in the body.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Telegram rejected the alert with status {(int)response.StatusCode}: {Shorten(body, 300)}",
                inner: null,
                response.StatusCode);
        }
    }

    internal static string BuildMessage(ErrorEventEntry error, int suppressedCount)
    {
        var message = new StringBuilder()
            .AppendLine("🚨 <b>Unhandled exception</b>")
            .AppendLine($"<b>Type:</b> <code>{Escape(error.ErrorType, MaxShortFieldLength)}</code>")
            .AppendLine($"<b>Endpoint:</b> {Escape(error.Method, MaxShortFieldLength)} {Escape(error.Endpoint, MaxShortFieldLength)}")
            .AppendLine($"<b>Path:</b> <code>{Escape(error.Path, MaxShortFieldLength)}</code>")
            .AppendLine($"<b>Message:</b> {Escape(error.Message, MaxExceptionMessageLength)}")
            .AppendLine($"<b>Trace id:</b> <code>{Escape(error.TraceId ?? "-", MaxShortFieldLength)}</code>")
            .Append(CultureInfo.InvariantCulture, $"<b>Time:</b> {error.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC");

        if (suppressedCount > 0)
        {
            message.AppendLine().Append(CultureInfo.InvariantCulture,
                $"<i>{suppressedCount} more alert(s) of this type were skipped since the previous one.</i>");
        }

        return message.ToString();
    }

    /// <summary>
    /// Escapes the three characters Telegram's HTML mode requires. Without it, a message containing
    /// "&lt;" or "&amp;" is rejected and the alert is silently lost.
    /// </summary>
    internal static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // Shorten before escaping, so an entity such as "&amp;" is never cut in half.
    private static string Escape(string text, int maxLength) => EscapeHtml(Shorten(text, maxLength));

    private static string Shorten(string text, int maxLength) =>
        text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 1), "…");

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string ParseMode);
}
