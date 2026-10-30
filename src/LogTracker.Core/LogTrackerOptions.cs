namespace LogTracker.Core;

/// <summary>
/// Settings bound from the <c>LogTracker</c> configuration section.
/// </summary>
public sealed class LogTrackerOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "LogTracker";

    /// <summary>Name of the connection string under <c>ConnectionStrings</c>.</summary>
    public const string ConnectionStringName = "LogTrackerDb";

    /// <summary>
    /// SQL Server connection string. Filled from <c>ConnectionStrings:LogTrackerDb</c>, not from this section.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// When set, every <c>/api/reports</c> request must send this value in the <c>X-Api-Key</c> header.
    /// When empty, the reports are open.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Days to keep log rows before the daily purge deletes them.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Requests whose path starts with one of these prefixes are not tracked.
    /// Values from configuration are added to these defaults.
    /// </summary>
    public List<string> ExcludedPathPrefixes { get; set; } =
        ["/api/reports", "/health", "/swagger", "/openapi", "/scalar", "/favicon.ico", "/.well-known"];

    /// <summary>
    /// Maximum number of entries waiting to be written. When the queue is full, new entries are dropped
    /// instead of slowing requests down.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Maximum number of entries sent to the database in one round trip.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Telegram alerts for unhandled exceptions.</summary>
    public TelegramOptions Telegram { get; set; } = new();
}

/// <summary>
/// Settings for Telegram alerts. Keep the bot token in user secrets or environment variables,
/// never in appsettings files that are committed.
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>Turns alerts on. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>Bot token from @BotFather.</summary>
    public string? BotToken { get; set; }

    /// <summary>Chat that receives the alerts.</summary>
    public string? ChatId { get; set; }

    /// <summary>
    /// Minimum number of seconds between two alerts for the same exception type.
    /// Alerts skipped in between are counted and mentioned in the next one. 0 sends every alert.
    /// </summary>
    public int ThrottleSeconds { get; set; } = 60;
}
