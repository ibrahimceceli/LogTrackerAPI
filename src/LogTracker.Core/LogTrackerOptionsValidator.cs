using Microsoft.Extensions.Options;

namespace LogTracker.Core;

/// <summary>
/// Checks the settings when the application starts, so a missing connection string or token
/// stops the app immediately instead of failing on the first request.
/// </summary>
internal sealed class LogTrackerOptionsValidator : IValidateOptions<LogTrackerOptions>
{
    public const int MinApiKeyLength = 16;

    public ValidateOptionsResult Validate(string? name, LogTrackerOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            errors.Add(
                $"Connection string 'ConnectionStrings:{LogTrackerOptions.ConnectionStringName}' is missing. " +
                $"Set it in appsettings, user secrets or the ConnectionStrings__{LogTrackerOptions.ConnectionStringName} environment variable.");
        }

        // A short key can be guessed by simply trying values against the reports API.
        if (!string.IsNullOrEmpty(options.ApiKey) && options.ApiKey.Length < MinApiKeyLength)
        {
            errors.Add($"LogTracker:ApiKey must be at least {MinApiKeyLength} characters long, or empty to leave the reports open.");
        }

        if (options.RetentionDays < 1)
        {
            errors.Add("LogTracker:RetentionDays must be at least 1.");
        }

        if (options.QueueCapacity < 1)
        {
            errors.Add("LogTracker:QueueCapacity must be at least 1.");
        }

        if (options.BatchSize < 1)
        {
            errors.Add("LogTracker:BatchSize must be at least 1.");
        }

        foreach (var prefix in options.ExcludedPathPrefixes)
        {
            if (string.IsNullOrEmpty(prefix) || prefix[0] != '/')
            {
                errors.Add($"LogTracker:ExcludedPathPrefixes entry '{prefix}' must start with '/'.");
            }
        }

        var telegram = options.Telegram;
        if (telegram.ThrottleSeconds < 0)
        {
            errors.Add("LogTracker:Telegram:ThrottleSeconds cannot be negative.");
        }

        if (telegram.Enabled && (string.IsNullOrWhiteSpace(telegram.BotToken) || string.IsNullOrWhiteSpace(telegram.ChatId)))
        {
            errors.Add("LogTracker:Telegram:BotToken and LogTracker:Telegram:ChatId are required when Telegram alerts are enabled.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
