using Microsoft.Extensions.Configuration;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class TelegramPollingOptions
{
    public const string SectionPath = "Webhooks:Providers:Telegram:Polling";

    public bool Enabled { get; init; }

    public string EndpointKey { get; init; } = "main";

    public IReadOnlySet<string> AllowedChatTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "private"
    };

    public int IntervalSeconds { get; init; } = 3;

    public int LongPollTimeoutSeconds { get; init; } = 20;

    public string ResolveBotTokenKey()
        => $"Webhooks:Providers:Telegram:Endpoints:{EndpointKey}:BotToken";

    public static TelegramPollingOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionPath);
        var endpointKey = section["EndpointKey"];

        var intervalSeconds = int.TryParse(section["IntervalSeconds"], out var parsedInterval)
            ? Math.Max(parsedInterval, 1)
            : 3;

        var longPollTimeoutSeconds = int.TryParse(section["LongPollTimeoutSeconds"], out var parsedTimeout)
            ? Math.Max(parsedTimeout, 0)
            : 20;

        var allowedChatTypes = ResolveAllowedChatTypes(section);

        return new TelegramPollingOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            EndpointKey = string.IsNullOrWhiteSpace(endpointKey) ? "main" : endpointKey.Trim(),
            AllowedChatTypes = allowedChatTypes,
            IntervalSeconds = intervalSeconds,
            LongPollTimeoutSeconds = longPollTimeoutSeconds
        };
    }

    private static IReadOnlySet<string> ResolveAllowedChatTypes(IConfigurationSection section)
    {
        var configured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rawCsv = section["AllowedChatTypes"];
        if (!string.IsNullOrWhiteSpace(rawCsv))
        {
            foreach (var value in rawCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                configured.Add(value);
            }
        }

        foreach (var child in section.GetSection("AllowedChatTypes").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
            {
                configured.Add(child.Value);
            }
        }

        if (configured.Count == 0)
        {
            configured.Add("private");
        }

        return configured;
    }
}
