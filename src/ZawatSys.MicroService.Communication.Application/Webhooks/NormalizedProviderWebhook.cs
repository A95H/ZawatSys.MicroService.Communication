using System.Text;
using System.Security.Cryptography;

namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public sealed record NormalizedProviderWebhook(
    string Provider,
    string EndpointKey,
    string EventType,
    string? ProviderObject,
    string? VerificationChallenge,
    ProviderWebhookRawPayloadReference RawPayloadReference,
    IReadOnlyList<NormalizedProviderWebhookEntry> Entries);

public sealed record NormalizedProviderWebhookEntry(
    string CanonicalType,
    string? ProviderMessageId,
    string? ExternalUserId,
    string? ExternalConversationId,
    string? DisplayName,
    string? MessageType,
    string? MessageText,
    string? DeliveryStatus,
    DateTimeOffset? OccurredAtUtc,
    string RawPayloadPath);

public sealed record ProviderWebhookRawPayloadReference(
    string? InlineBody,
    string? Sha256,
    int CharacterCount)
{
    public static ProviderWebhookRawPayloadReference FromRawBody(string? rawBody)
    {
        if (rawBody is null)
        {
            return new ProviderWebhookRawPayloadReference(null, null, 0);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawBody));
        return new ProviderWebhookRawPayloadReference(rawBody, Convert.ToHexStringLower(hash), rawBody.Length);
    }
}
