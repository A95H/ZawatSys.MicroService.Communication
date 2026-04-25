using System.Globalization;
using System.Text.Json;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class TelegramProviderWebhookPayloadNormalizer : IProviderWebhookPayloadNormalizer
{
    public bool CanNormalize(string provider, string eventType)
    {
        return string.Equals(provider, ProviderWebhookProviders.Telegram, StringComparison.OrdinalIgnoreCase)
            && string.Equals(eventType, ProviderWebhookEventTypes.Inbound, StringComparison.Ordinal);
    }

    public NormalizedProviderWebhook Normalize(ProviderWebhookEnvelope envelope)
    {
        if (!CanNormalize(envelope.Provider, envelope.EventType))
        {
            throw new ArgumentException($"Telegram normalizer does not support provider '{envelope.Provider}' and event type '{envelope.EventType}'.");
        }

        using var document = ParseJson(envelope.RawBody);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Telegram inbound webhook payload must be a JSON object.");
        }

        var entries = new List<NormalizedProviderWebhookEntry>();

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            entries.Add(BuildInboundMessageEntry(message, root));
        }

        return new NormalizedProviderWebhook(
            envelope.Provider,
            envelope.EndpointKey,
            envelope.EventType,
            ProviderObject: "update",
            VerificationChallenge: null,
            ProviderWebhookRawPayloadReference.FromRawBody(envelope.RawBody),
            entries);
    }

    private static NormalizedProviderWebhookEntry BuildInboundMessageEntry(JsonElement message, JsonElement root)
    {
        var updateId = GetOptionalString(root, "update_id");
        var messageId = GetOptionalString(message, "message_id");
        var providerMessageId = BuildProviderMessageId(updateId, messageId);

        var chat = TryGetObject(message, "chat");
        var from = TryGetObject(message, "from");

        var externalUserId = chat is { } chatObject
            ? GetOptionalString(chatObject, "id")
            : null;

        var displayName = ResolveDisplayName(from, chat);
        var messageText = GetOptionalString(message, "text") ?? GetOptionalString(message, "caption");

        return new NormalizedProviderWebhookEntry(
            CanonicalType: "message",
            ProviderMessageId: providerMessageId,
            ExternalUserId: externalUserId,
            ExternalConversationId: null,
            DisplayName: displayName,
            MessageType: ResolveMessageType(message),
            MessageText: messageText,
            DeliveryStatus: null,
            OccurredAtUtc: ParseUnixTimestamp(GetOptionalString(message, "date")),
            RawPayloadPath: "$.message");
    }

    private static string? BuildProviderMessageId(string? updateId, string? messageId)
    {
        if (!string.IsNullOrWhiteSpace(updateId) && !string.IsNullOrWhiteSpace(messageId))
        {
            return $"tg:{updateId}:{messageId}";
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            return $"tg:message:{messageId}";
        }

        if (!string.IsNullOrWhiteSpace(updateId))
        {
            return $"tg:update:{updateId}";
        }

        return null;
    }

    private static string ResolveMessageType(JsonElement message)
    {
        var text = GetOptionalString(message, "text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (text.StartsWith("/", StringComparison.Ordinal) || HasBotCommandEntity(message))
            {
                return "command";
            }

            return "text";
        }

        if (message.TryGetProperty("photo", out var photo) && photo.ValueKind == JsonValueKind.Array)
        {
            return "image";
        }

        if (message.TryGetProperty("video", out var video) && video.ValueKind == JsonValueKind.Object)
        {
            return "video";
        }

        if (message.TryGetProperty("audio", out var audio) && audio.ValueKind == JsonValueKind.Object)
        {
            return "audio";
        }

        if (message.TryGetProperty("document", out var document) && document.ValueKind == JsonValueKind.Object)
        {
            return "document";
        }

        if (message.TryGetProperty("sticker", out var sticker) && sticker.ValueKind == JsonValueKind.Object)
        {
            return "sticker";
        }

        return "unknown";
    }

    private static bool HasBotCommandEntity(JsonElement message)
    {
        if (!message.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var entity in entities.EnumerateArray())
        {
            if (string.Equals(GetOptionalString(entity, "type"), "bot_command", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveDisplayName(JsonElement? from, JsonElement? chat)
    {
        var fromFirst = from.HasValue ? GetOptionalString(from.Value, "first_name") : null;
        var fromLast = from.HasValue ? GetOptionalString(from.Value, "last_name") : null;
        var fromUsername = from.HasValue ? GetOptionalString(from.Value, "username") : null;

        var composedFromName = ComposeName(fromFirst, fromLast);
        if (!string.IsNullOrWhiteSpace(composedFromName))
        {
            return composedFromName;
        }

        if (!string.IsNullOrWhiteSpace(fromUsername))
        {
            return fromUsername;
        }

        var chatFirst = chat.HasValue ? GetOptionalString(chat.Value, "first_name") : null;
        var chatLast = chat.HasValue ? GetOptionalString(chat.Value, "last_name") : null;
        var chatTitle = chat.HasValue ? GetOptionalString(chat.Value, "title") : null;
        var chatUsername = chat.HasValue ? GetOptionalString(chat.Value, "username") : null;

        var composedChatName = ComposeName(chatFirst, chatLast);
        if (!string.IsNullOrWhiteSpace(composedChatName))
        {
            return composedChatName;
        }

        if (!string.IsNullOrWhiteSpace(chatTitle))
        {
            return chatTitle;
        }

        return string.IsNullOrWhiteSpace(chatUsername) ? null : chatUsername;
    }

    private static string? ComposeName(string? firstName, string? lastName)
    {
        var first = firstName?.Trim();
        var last = lastName?.Trim();

        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
        {
            return null;
        }

        return string.Join(" ", new[] { first, last }.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static JsonDocument ParseJson(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new ArgumentException("Webhook payload is required for normalization.");
        }

        try
        {
            return JsonDocument.Parse(rawBody);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Webhook payload is not valid JSON.", exception);
        }
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static DateTimeOffset? ParseUnixTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }
}
