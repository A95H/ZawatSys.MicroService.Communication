using System.Text.Json;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed class MetaOutboundProviderAdapter : IOutboundProviderAdapter
{
    private const string ProviderName = "meta";

    public bool CanHandle(string provider)
        => string.Equals(provider, ProviderName, StringComparison.OrdinalIgnoreCase);

    public Task<OutboundProviderSendResult> SendAsync(
        ConversationChannelEndpoint endpoint,
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        OutboundProviderSendRequest request,
        CancellationToken cancellationToken)
    {
        _ = outboundMessage;
        _ = attempt;
        _ = cancellationToken;

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = ResolveRecipient(endpoint),
            type = "text",
            text = new
            {
                preview_url = false,
                body = request.Content
            },
            context = request.ReplyToMessageId is null
                ? null
                : new
                {
                    message_id = request.ReplyToMessageId.Value
                }
        };

        var simulation = ReadSimulation(endpoint.MetadataJson);
        var providerMessageId = string.IsNullOrWhiteSpace(simulation.ProviderMessageId)
            ? $"wamid.{request.OutboundConversationMessageId:N}"
            : simulation.ProviderMessageId.Trim();

        if (string.Equals(simulation.Mode, "FAIL_TERMINAL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: simulation.HttpStatusCode ?? 400,
                ProviderMessageId: simulation.EmitProviderMessageIdOnFailure ? providerMessageId : null,
                ResponseCode: simulation.ResponseCode ?? "META_INVALID_REQUEST",
                ErrorCode: simulation.ErrorCode ?? "META_INVALID_REQUEST",
                ErrorMessageRedacted: simulation.ErrorMessageRedacted ?? "Meta provider rejected outbound request.",
                ResponseMetadata: new Dictionary<string, object?>
                {
                    ["provider"] = ProviderName,
                    ["mode"] = "FAIL_TERMINAL",
                    ["payload"] = payload
                }));
        }

        if (string.Equals(simulation.Mode, "FAIL_TRANSIENT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: simulation.HttpStatusCode ?? 503,
                ProviderMessageId: simulation.EmitProviderMessageIdOnFailure ? providerMessageId : null,
                ResponseCode: simulation.ResponseCode ?? "META_TEMPORARY_UNAVAILABLE",
                ErrorCode: simulation.ErrorCode ?? "META_TEMPORARY_UNAVAILABLE",
                ErrorMessageRedacted: simulation.ErrorMessageRedacted ?? "Meta provider temporarily unavailable.",
                ResponseMetadata: new Dictionary<string, object?>
                {
                    ["provider"] = ProviderName,
                    ["mode"] = "FAIL_TRANSIENT",
                    ["payload"] = payload
                },
                RetryAfter: simulation.RetryAfterSeconds is null ? null : TimeSpan.FromSeconds(simulation.RetryAfterSeconds.Value)));
        }

        return Task.FromResult(new OutboundProviderSendResult(
            Accepted: true,
            HttpStatusCode: simulation.HttpStatusCode ?? 202,
            ProviderMessageId: providerMessageId,
            ResponseCode: simulation.ResponseCode ?? "META_ACCEPTED",
            ErrorCode: null,
            ErrorMessageRedacted: null,
            ResponseMetadata: new Dictionary<string, object?>
            {
                ["provider"] = ProviderName,
                ["mode"] = "SUCCESS",
                ["payload"] = payload
            }));
    }

    private static string ResolveRecipient(ConversationChannelEndpoint endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.DisplayName))
        {
            return "unknown";
        }

        return endpoint.DisplayName;
    }

    private static MetaOutboundSimulation ReadSimulation(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return MetaOutboundSimulation.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("outboundProviderSimulation", out var simulationElement))
            {
                return MetaOutboundSimulation.Success();
            }

            return new MetaOutboundSimulation(
                Mode: TryGetString(simulationElement, "mode") ?? "SUCCESS",
                HttpStatusCode: TryGetInt(simulationElement, "httpStatusCode"),
                ProviderMessageId: TryGetString(simulationElement, "providerMessageId"),
                ResponseCode: TryGetString(simulationElement, "responseCode"),
                ErrorCode: TryGetString(simulationElement, "errorCode"),
                ErrorMessageRedacted: TryGetString(simulationElement, "errorMessageRedacted"),
                RetryAfterSeconds: TryGetInt(simulationElement, "retryAfterSeconds"),
                EmitProviderMessageIdOnFailure: TryGetBool(simulationElement, "emitProviderMessageIdOnFailure") ?? false);
        }
        catch (JsonException)
        {
            return MetaOutboundSimulation.Success();
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? TryGetInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static bool? TryGetBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private sealed record MetaOutboundSimulation(
        string Mode,
        int? HttpStatusCode,
        string? ProviderMessageId,
        string? ResponseCode,
        string? ErrorCode,
        string? ErrorMessageRedacted,
        int? RetryAfterSeconds,
        bool EmitProviderMessageIdOnFailure)
    {
        public static MetaOutboundSimulation Success()
            => new(
                Mode: "SUCCESS",
                HttpStatusCode: null,
                ProviderMessageId: null,
                ResponseCode: null,
                ErrorCode: null,
                ErrorMessageRedacted: null,
                RetryAfterSeconds: null,
                EmitProviderMessageIdOnFailure: false);
    }
}
