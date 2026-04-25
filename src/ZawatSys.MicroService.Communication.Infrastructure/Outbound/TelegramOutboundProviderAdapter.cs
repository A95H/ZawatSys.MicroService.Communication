using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed class TelegramOutboundProviderAdapter : IOutboundProviderAdapter
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly CommunicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramOutboundProviderAdapter> _logger;
    private readonly HttpClient _httpClient;

    public TelegramOutboundProviderAdapter(
        CommunicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<TelegramOutboundProviderAdapter> logger,
        HttpClient? httpClient = null)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public bool CanHandle(string provider)
        => string.Equals(provider, "telegram", StringComparison.OrdinalIgnoreCase);

    public async Task<OutboundProviderSendResult> SendAsync(
        ConversationChannelEndpoint endpoint,
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        OutboundProviderSendRequest request,
        CancellationToken cancellationToken)
    {
        _ = outboundMessage;
        _ = attempt;

        var botToken = _configuration[$"Webhooks:Providers:Telegram:Endpoints:{endpoint.EndpointKey}:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogWarning(
                "Telegram outbound send rejected because BotToken is missing. EndpointKey: {EndpointKey}",
                endpoint.EndpointKey);

            return new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: 400,
                ProviderMessageId: null,
                ResponseCode: "TELEGRAM_BOT_TOKEN_MISSING",
                ErrorCode: "TELEGRAM_BOT_TOKEN_MISSING",
                ErrorMessageRedacted: "Telegram bot token is not configured.");
        }

        var chatId = await ResolveChatIdAsync(request, endpoint, cancellationToken);
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: 400,
                ProviderMessageId: null,
                ResponseCode: "TELEGRAM_CHAT_ID_MISSING",
                ErrorCode: "TELEGRAM_CHAT_ID_MISSING",
                ErrorMessageRedacted: "Telegram chat id could not be resolved for outbound send.");
        }

        var payload = new
        {
            chat_id = chatId,
            text = request.Content
        };

        var endpointUrl = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
        var payloadJson = JsonSerializer.Serialize(payload);

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var providerMessageId = ResolveProviderMessageId(chatId, responseBody);
                return new OutboundProviderSendResult(
                    Accepted: true,
                    HttpStatusCode: (int)response.StatusCode,
                    ProviderMessageId: providerMessageId,
                    ResponseCode: "TELEGRAM_ACCEPTED",
                    ErrorCode: null,
                    ErrorMessageRedacted: null,
                    ResponseMetadata: new Dictionary<string, object?>
                    {
                        ["provider"] = "telegram",
                        ["chatId"] = chatId
                    });
            }

            var retryAfter = ResolveRetryAfter(response, responseBody);
            var isTransient = IsTransientStatusCode(response.StatusCode);

            return new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: (int)response.StatusCode,
                ProviderMessageId: null,
                ResponseCode: isTransient ? "TELEGRAM_TRANSIENT_FAILURE" : "TELEGRAM_TERMINAL_FAILURE",
                ErrorCode: ResolveProviderErrorCode(responseBody),
                ErrorMessageRedacted: ResolveProviderErrorMessage(responseBody),
                ResponseMetadata: new Dictionary<string, object?>
                {
                    ["provider"] = "telegram",
                    ["chatId"] = chatId
                },
                RetryAfter: retryAfter);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: null,
                ProviderMessageId: null,
                ResponseCode: "TELEGRAM_TIMEOUT",
                ErrorCode: "TELEGRAM_TIMEOUT",
                ErrorMessageRedacted: "Telegram provider request timed out.");
        }
        catch (HttpRequestException)
        {
            return new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: null,
                ProviderMessageId: null,
                ResponseCode: "TELEGRAM_NETWORK_FAILURE",
                ErrorCode: "TELEGRAM_NETWORK_FAILURE",
                ErrorMessageRedacted: "Telegram provider network request failed.");
        }
    }

    private async Task<string?> ResolveChatIdAsync(
        OutboundProviderSendRequest request,
        ConversationChannelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var sessionBinding = await _dbContext.ConversationSessions
            .AsNoTracking()
            .Where(x => x.TenantId == request.TenantId
                && x.Id == request.SessionId
                && x.ConversationChannelEndpointId == endpoint.Id)
            .Select(x => new
            {
                x.ExternalIdentityBindingId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sessionBinding is null)
        {
            _logger.LogWarning(
                "Telegram outbound send rejected because session was not found. TenantId: {TenantId}, SessionId: {SessionId}, EndpointId: {EndpointId}",
                request.TenantId,
                request.SessionId,
                endpoint.Id);
            return null;
        }

        var externalUserId = await _dbContext.ExternalIdentityBindings
            .AsNoTracking()
            .Where(x => x.TenantId == request.TenantId
                && x.Id == sessionBinding.ExternalIdentityBindingId
                && x.ConversationChannelEndpointId == endpoint.Id)
            .Select(x => x.ExternalUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            _logger.LogWarning(
                "Telegram outbound send rejected because external user id was not found. TenantId: {TenantId}, SessionId: {SessionId}, EndpointId: {EndpointId}",
                request.TenantId,
                request.SessionId,
                endpoint.Id);
            return null;
        }

        return externalUserId.Trim();
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return numeric == 429 || numeric == 408 || numeric >= 500;
    }

    private static string ResolveProviderMessageId(string chatId, string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            {
                return $"tg:{chatId}:unknown";
            }

            if (!result.TryGetProperty("message_id", out var messageIdElement))
            {
                return $"tg:{chatId}:unknown";
            }

            var messageId = messageIdElement.ValueKind switch
            {
                JsonValueKind.Number => messageIdElement.GetRawText(),
                JsonValueKind.String => messageIdElement.GetString(),
                _ => null
            };

            return string.IsNullOrWhiteSpace(messageId)
                ? $"tg:{chatId}:unknown"
                : $"tg:{chatId}:{messageId.Trim()}";
        }
        catch (JsonException)
        {
            return $"tg:{chatId}:unknown";
        }
    }

    private static TimeSpan? ResolveRetryAfter(HttpResponseMessage response, string responseBody)
    {
        if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        {
            var retryAfterValue = retryAfterValues.FirstOrDefault();
            if (int.TryParse(retryAfterValue, out var retryAfterSeconds) && retryAfterSeconds > 0)
            {
                return TimeSpan.FromSeconds(retryAfterSeconds);
            }
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!parameters.TryGetProperty("retry_after", out var retryAfterElement))
            {
                return null;
            }

            var retryAfterSeconds = retryAfterElement.ValueKind == JsonValueKind.Number
                ? retryAfterElement.GetInt32()
                : 0;

            return retryAfterSeconds > 0 ? TimeSpan.FromSeconds(retryAfterSeconds) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveProviderErrorCode(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("error_code", out var errorCodeElement))
            {
                return null;
            }

            return errorCodeElement.ValueKind switch
            {
                JsonValueKind.Number => $"TELEGRAM_{errorCodeElement.GetRawText()}",
                JsonValueKind.String => $"TELEGRAM_{errorCodeElement.GetString()?.Trim()}",
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveProviderErrorMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            return root.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
                ? description.GetString()?.Trim()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
