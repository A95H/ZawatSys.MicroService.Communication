using System.Globalization;
using System.Text.Json;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class MetaProviderWebhookPayloadNormalizer : IProviderWebhookPayloadNormalizer
{
    public bool CanNormalize(string provider, string eventType)
    {
        return string.Equals(provider, ProviderWebhookProviders.Meta, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(eventType, ProviderWebhookEventTypes.Verification, StringComparison.Ordinal)
                || string.Equals(eventType, ProviderWebhookEventTypes.Inbound, StringComparison.Ordinal)
                || string.Equals(eventType, ProviderWebhookEventTypes.DeliveryStatus, StringComparison.Ordinal));
    }

    public NormalizedProviderWebhook Normalize(ProviderWebhookEnvelope envelope)
    {
        if (!CanNormalize(envelope.Provider, envelope.EventType))
        {
            throw new ArgumentException($"Meta normalizer does not support provider '{envelope.Provider}' and event type '{envelope.EventType}'.");
        }

        return envelope.EventType switch
        {
            ProviderWebhookEventTypes.Verification => NormalizeVerification(envelope),
            ProviderWebhookEventTypes.Inbound => NormalizeInbound(envelope),
            ProviderWebhookEventTypes.DeliveryStatus => NormalizeDeliveryStatus(envelope),
            _ => throw new ArgumentException($"Unsupported webhook event type '{envelope.EventType}'.")
        };
    }

    private static NormalizedProviderWebhook NormalizeVerification(ProviderWebhookEnvelope envelope)
    {
        envelope.Query.TryGetValue("hub.challenge", out var challenges);

        return new NormalizedProviderWebhook(
            envelope.Provider,
            envelope.EndpointKey,
            envelope.EventType,
            ProviderObject: null,
            VerificationChallenge: challenges?.FirstOrDefault(),
            ProviderWebhookRawPayloadReference.FromRawBody(envelope.RawBody),
            []);
    }

    private static NormalizedProviderWebhook NormalizeInbound(ProviderWebhookEnvelope envelope)
    {
        using var document = ParseJson(envelope.RawBody);
        var root = document.RootElement;
        var providerObject = GetOptionalString(root, "object");
        var entries = new List<NormalizedProviderWebhookEntry>();

        if (!root.TryGetProperty("entry", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Meta inbound webhook payload must contain an entry array.");
        }

        for (var entryIndex = 0; entryIndex < entryArray.GetArrayLength(); entryIndex++)
        {
            var entry = entryArray[entryIndex];
            if (!entry.TryGetProperty("changes", out var changesArray) || changesArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            for (var changeIndex = 0; changeIndex < changesArray.GetArrayLength(); changeIndex++)
            {
                var change = changesArray[changeIndex];
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var contactNames = BuildContactNames(value);
                if (!value.TryGetProperty("messages", out var messagesArray) || messagesArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                for (var messageIndex = 0; messageIndex < messagesArray.GetArrayLength(); messageIndex++)
                {
                    var message = messagesArray[messageIndex];
                    var externalUserId = GetOptionalString(message, "from");

                    entries.Add(new NormalizedProviderWebhookEntry(
                        CanonicalType: "message",
                        ProviderMessageId: GetOptionalString(message, "id"),
                        ExternalUserId: externalUserId,
                        ExternalConversationId: null,
                        DisplayName: externalUserId is null || !contactNames.TryGetValue(externalUserId, out var displayName) ? null : displayName,
                        MessageType: GetOptionalString(message, "type"),
                        MessageText: TryGetNestedString(message, "text", "body"),
                        DeliveryStatus: null,
                        OccurredAtUtc: ParseUnixTimestamp(GetOptionalString(message, "timestamp")),
                        RawPayloadPath: $"$.entry[{entryIndex}].changes[{changeIndex}].value.messages[{messageIndex}]"));
                }
            }
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("Meta inbound webhook payload did not contain any messages to normalize.");
        }

        return new NormalizedProviderWebhook(
            envelope.Provider,
            envelope.EndpointKey,
            envelope.EventType,
            providerObject,
            VerificationChallenge: null,
            ProviderWebhookRawPayloadReference.FromRawBody(envelope.RawBody),
            entries);
    }

    private static NormalizedProviderWebhook NormalizeDeliveryStatus(ProviderWebhookEnvelope envelope)
    {
        using var document = ParseJson(envelope.RawBody);
        var root = document.RootElement;
        var providerObject = GetOptionalString(root, "object");
        var entries = new List<NormalizedProviderWebhookEntry>();

        if (!root.TryGetProperty("entry", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Meta delivery status webhook payload must contain an entry array.");
        }

        for (var entryIndex = 0; entryIndex < entryArray.GetArrayLength(); entryIndex++)
        {
            var entry = entryArray[entryIndex];
            if (!entry.TryGetProperty("changes", out var changesArray) || changesArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            for (var changeIndex = 0; changeIndex < changesArray.GetArrayLength(); changeIndex++)
            {
                var change = changesArray[changeIndex];
                if (!change.TryGetProperty("value", out var value)
                    || !value.TryGetProperty("statuses", out var statusesArray)
                    || statusesArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                for (var statusIndex = 0; statusIndex < statusesArray.GetArrayLength(); statusIndex++)
                {
                    var status = statusesArray[statusIndex];

                    entries.Add(new NormalizedProviderWebhookEntry(
                        CanonicalType: "delivery-status",
                        ProviderMessageId: GetOptionalString(status, "id"),
                        ExternalUserId: GetOptionalString(status, "recipient_id"),
                        ExternalConversationId: TryGetNestedString(status, "conversation", "id"),
                        DisplayName: null,
                        MessageType: null,
                        MessageText: null,
                        DeliveryStatus: NormalizeDeliveryStatusValue(GetOptionalString(status, "status")),
                        OccurredAtUtc: ParseUnixTimestamp(GetOptionalString(status, "timestamp")),
                        RawPayloadPath: $"$.entry[{entryIndex}].changes[{changeIndex}].value.statuses[{statusIndex}]"));
                }
            }
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("Meta delivery status webhook payload did not contain any statuses to normalize.");
        }

        return new NormalizedProviderWebhook(
            envelope.Provider,
            envelope.EndpointKey,
            envelope.EventType,
            providerObject,
            VerificationChallenge: null,
            ProviderWebhookRawPayloadReference.FromRawBody(envelope.RawBody),
            entries);
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

    private static Dictionary<string, string> BuildContactNames(JsonElement value)
    {
        var contactNames = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!value.TryGetProperty("contacts", out var contactsArray) || contactsArray.ValueKind != JsonValueKind.Array)
        {
            return contactNames;
        }

        foreach (var contact in contactsArray.EnumerateArray())
        {
            var waId = GetOptionalString(contact, "wa_id");
            var displayName = TryGetNestedString(contact, "profile", "name");
            if (!string.IsNullOrWhiteSpace(waId) && !string.IsNullOrWhiteSpace(displayName))
            {
                contactNames[waId] = displayName;
            }
        }

        return contactNames;
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

    private static string? NormalizeDeliveryStatusValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string? TryGetNestedString(JsonElement element, string parentPropertyName, string childPropertyName)
    {
        if (!element.TryGetProperty(parentPropertyName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetOptionalString(parent, childPropertyName);
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
