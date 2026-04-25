using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class TelegramPollingHostedService : BackgroundService
{
    private const string Provider = "telegram";
    private const string EventType = ProviderWebhookEventTypes.Inbound;
    private static readonly HttpClient SharedHttpClient = new();

    private readonly TelegramPollingOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ITelegramPollingWebhookDispatcher _dispatcher;
    private readonly ILogger<TelegramPollingHostedService> _logger;
    private readonly HttpClient _httpClient;
    private long _nextOffset;

    public TelegramPollingHostedService(
        TelegramPollingOptions options,
        IConfiguration configuration,
        ITelegramPollingWebhookDispatcher dispatcher,
        ILogger<TelegramPollingHostedService> logger,
        HttpClient? httpClient = null)
    {
        _options = options;
        _configuration = configuration;
        _dispatcher = dispatcher;
        _logger = logger;
        _httpClient = httpClient ?? SharedHttpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.LongPollTimeoutSeconds + 15));
        _nextOffset = 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Telegram polling worker is disabled by configuration.");
            return;
        }

        var botToken = _configuration[_options.ResolveBotTokenKey()];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogError(
                "Telegram polling worker enabled but BotToken is missing. EndpointKey: {EndpointKey}, BotTokenConfigKey: {BotTokenConfigKey}",
                _options.EndpointKey,
                _options.ResolveBotTokenKey());
            return;
        }

        _logger.LogInformation(
            "Telegram polling worker started. EndpointKey: {EndpointKey}, AllowedChatTypes: {AllowedChatTypes}, IntervalSeconds: {IntervalSeconds}, LongPollTimeoutSeconds: {LongPollTimeoutSeconds}",
            _options.EndpointKey,
            string.Join(',', _options.AllowedChatTypes.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)),
            _options.IntervalSeconds,
            _options.LongPollTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteSinglePollAsync(botToken.Trim(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Telegram polling cycle failed. EndpointKey: {EndpointKey}", _options.EndpointKey);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Telegram polling worker stopped. EndpointKey: {EndpointKey}", _options.EndpointKey);
    }

    public async Task ExecuteSinglePollAsync(string botToken, CancellationToken cancellationToken)
    {
        var requestUri = $"https://api.telegram.org/bot{botToken}/getUpdates";
        var payload = new
        {
            offset = _nextOffset,
            timeout = _options.LongPollTimeoutSeconds,
            allowed_updates = new[] { "message" }
        };

        using var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Telegram polling request failed. EndpointKey: {EndpointKey}, StatusCode: {StatusCode}",
                _options.EndpointKey,
                (int)response.StatusCode);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(responseBody);

        var root = document.RootElement;
        if (!root.TryGetProperty("ok", out var okElement)
            || okElement.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("result", out var updatesElement)
            || updatesElement.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning(
                "Telegram polling response was not in expected format. EndpointKey: {EndpointKey}",
                _options.EndpointKey);
            return;
        }

        foreach (var update in updatesElement.EnumerateArray())
        {
            var updateId = TryGetInt64(update, "update_id");

            var chatInfo = ResolveChatInfo(update);
            var isAcceptedChatType = chatInfo?.ChatType is { Length: > 0 } chatType
                && _options.AllowedChatTypes.Contains(chatType);

            if (chatInfo is not null)
            {
                _logger.LogInformation(
                    "Telegram chat discovered via polling. EndpointKey: {EndpointKey}, UpdateId: {UpdateId}, ChatId: {ChatId}, ChatType: {ChatType}, Username: {Username}, DisplayName: {DisplayName}, Accepted: {Accepted}",
                    _options.EndpointKey,
                    updateId,
                    chatInfo.ChatId,
                    chatInfo.ChatType,
                    chatInfo.Username,
                    chatInfo.DisplayName,
                    isAcceptedChatType);
            }

            if (!isAcceptedChatType)
            {
                if (updateId.HasValue)
                {
                    _nextOffset = Math.Max(_nextOffset, updateId.Value + 1);
                }

                continue;
            }

            var envelope = new ProviderWebhookEnvelope(
                Provider,
                _options.EndpointKey,
                EventType,
                Method: "POST",
                Path: $"/api/providers/{Provider}/{_options.EndpointKey}/webhooks/inbound",
                ContentType: "application/json",
                RawBody: update.GetRawText(),
                Headers: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                Query: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

            await _dispatcher.DispatchAsync(envelope, cancellationToken);

            if (updateId.HasValue)
            {
                _nextOffset = Math.Max(_nextOffset, updateId.Value + 1);
            }
        }
    }

    private static long? TryGetInt64(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var numeric) => numeric,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static TelegramChatInfo? ResolveChatInfo(JsonElement update)
    {
        if (!update.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!message.TryGetProperty("chat", out var chat) || chat.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var chatId = chat.TryGetProperty("id", out var chatIdElement)
            ? chatIdElement.GetRawText()
            : null;

        var chatType = chat.TryGetProperty("type", out var chatTypeElement) && chatTypeElement.ValueKind == JsonValueKind.String
            ? chatTypeElement.GetString()?.Trim()
            : null;

        var username = chat.TryGetProperty("username", out var chatUsernameElement) && chatUsernameElement.ValueKind == JsonValueKind.String
            ? chatUsernameElement.GetString()?.Trim()
            : null;

        string? displayName = null;
        if (message.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.Object)
        {
            var first = from.TryGetProperty("first_name", out var firstNameElement) && firstNameElement.ValueKind == JsonValueKind.String
                ? firstNameElement.GetString()?.Trim()
                : null;
            var last = from.TryGetProperty("last_name", out var lastNameElement) && lastNameElement.ValueKind == JsonValueKind.String
                ? lastNameElement.GetString()?.Trim()
                : null;
            var fromUsername = from.TryGetProperty("username", out var fromUsernameElement) && fromUsernameElement.ValueKind == JsonValueKind.String
                ? fromUsernameElement.GetString()?.Trim()
                : null;

            displayName = string.Join(" ", new[] { first, last }.Where(static x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = fromUsername;
            }
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            var chatFirst = chat.TryGetProperty("first_name", out var chatFirstElement) && chatFirstElement.ValueKind == JsonValueKind.String
                ? chatFirstElement.GetString()?.Trim()
                : null;
            var chatLast = chat.TryGetProperty("last_name", out var chatLastElement) && chatLastElement.ValueKind == JsonValueKind.String
                ? chatLastElement.GetString()?.Trim()
                : null;
            var chatTitle = chat.TryGetProperty("title", out var chatTitleElement) && chatTitleElement.ValueKind == JsonValueKind.String
                ? chatTitleElement.GetString()?.Trim()
                : null;

            displayName = string.Join(" ", new[] { chatFirst, chatLast }.Where(static x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = chatTitle;
            }
        }

        return new TelegramChatInfo(chatId, chatType, username, displayName);
    }

    private sealed record TelegramChatInfo(string? ChatId, string? ChatType, string? Username, string? DisplayName);
}
