using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Application.Webhooks;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Observability;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class DeliveryStatusWebhookReconciliationService : IDeliveryStatusWebhookReconciliationService
{
    private const string MeterName = "ZawatSys.MicroService.Communication.Webhooks";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.Webhooks";
    private const string DeliveryCallbackCounterName = "communication.delivery_callbacks.reconciled";
    private const string DeliveryLifecycleHistogramName = "communication.outbound.delivery.lifecycle";
    private const string ReconciliationSeam = "COM-110.1-delivery-callback-reconciliation";
    private const string ReconciliationActivityName = "communication.webhook.delivery_status.reconcile";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Counter<long> DeliveryCallbackCounter = Meter.CreateCounter<long>(DeliveryCallbackCounterName);
    private static readonly Histogram<double> DeliveryLifecycleHistogram = Meter.CreateHistogram<double>(DeliveryLifecycleHistogramName, unit: "ms");

    private readonly CommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeliveryStatusWebhookReconciliationService> _logger;

    public DeliveryStatusWebhookReconciliationService(
        CommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<DeliveryStatusWebhookReconciliationService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task ReconcileAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken)
    {
        var processedAt = DateTimeOffset.UtcNow;
        var actorUserId = _currentUserService.UserId ?? Guid.Empty;
        var correlationId = _currentUserService.CorrelationId == Guid.Empty
            ? Guid.NewGuid()
            : _currentUserService.CorrelationId;
        var endpoint = await ResolveEndpointAsync(webhook, cancellationToken);
        var tenantId = endpoint.TenantId;
        var changed = false;

        foreach (var entry in webhook.Entries.Where(static x => string.Equals(x.CanonicalType, "delivery-status", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(entry.ProviderMessageId))
            {
                _logger.LogWarning(
                    "Skipping delivery callback without provider reference. Provider: {Provider}, EndpointKey: {EndpointKey}, RawPayloadPath: {RawPayloadPath}, RawPayloadSha256: {RawPayloadSha256}",
                    webhook.Provider,
                    webhook.EndpointKey,
                    entry.RawPayloadPath,
                    webhook.RawPayloadReference.Sha256);
                continue;
            }

            if (!TryMapCallbackStatus(entry.DeliveryStatus, out var callbackStatus, out var mappedAttemptStatus))
            {
                _logger.LogInformation(
                    "Ignoring unsupported delivery callback status. Provider: {Provider}, EndpointKey: {EndpointKey}, ProviderMessageId: {ProviderMessageId}, DeliveryStatus: {DeliveryStatus}, RawPayloadSha256: {RawPayloadSha256}",
                    webhook.Provider,
                    webhook.EndpointKey,
                    entry.ProviderMessageId,
                    entry.DeliveryStatus,
                    webhook.RawPayloadReference.Sha256);
                continue;
            }

            var providerMessageId = entry.ProviderMessageId.Trim();
            var attempt = await _dbContext.MessageDeliveryAttempts
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId
                        && x.ConversationChannelEndpointId == endpoint.Id
                        && x.ProviderMessageId == providerMessageId
                        && !x.IsDeleted,
                    cancellationToken);

            if (attempt is null)
            {
                _logger.LogWarning(
                    "Delivery callback provider reference was not found. Provider: {Provider}, EndpointKey: {EndpointKey}, ProviderMessageId: {ProviderMessageId}, DeliveryStatus: {DeliveryStatus}, RawPayloadSha256: {RawPayloadSha256}",
                    webhook.Provider,
                    webhook.EndpointKey,
                    providerMessageId,
                    callbackStatus,
                    webhook.RawPayloadReference.Sha256);
                continue;
            }

            using var activity = ActivitySource.StartActivity(ReconciliationActivityName, ActivityKind.Consumer);
            activity?.SetTag("tenant.id", tenantId);
            activity?.SetTag("endpoint.id", endpoint.Id);
            activity?.SetTag("delivery.attempt_id", attempt.Id);
            activity?.SetTag("provider", webhook.Provider);
            activity?.SetTag("endpoint.key", webhook.EndpointKey);
            activity?.SetTag("channel", endpoint.Channel);
            activity?.SetTag("provider_message_id", providerMessageId);
            activity?.SetTag("delivery.callback_status", callbackStatus);

            var metadata = ParseMetadataObject(attempt.MetadataJson);
            var timeline = GetOrCreateTimeline(metadata);
            var eventKey = BuildEventKey(providerMessageId, callbackStatus, entry.OccurredAtUtc, webhook.RawPayloadReference.Sha256, entry.RawPayloadPath);
            if (ContainsEvent(timeline, eventKey))
            {
                activity?.SetTag("delivery.reconciliation_result", "duplicate-noop");
                _logger.LogInformation(
                    "Delivery callback duplicate-noop. Provider: {Provider}, EndpointKey: {EndpointKey}, ProviderMessageId: {ProviderMessageId}, DeliveryStatus: {DeliveryStatus}, EventKey: {EventKey}",
                    webhook.Provider,
                    webhook.EndpointKey,
                    providerMessageId,
                    callbackStatus,
                    eventKey);
                continue;
            }

            var reconciledAt = processedAt.ToUniversalTime();
            timeline.Add(new JsonObject
            {
                ["eventKey"] = eventKey,
                ["providerMessageId"] = providerMessageId,
                ["status"] = callbackStatus,
                ["mappedAttemptStatus"] = mappedAttemptStatus,
                ["occurredAtUtc"] = FormatDate(entry.OccurredAtUtc),
                ["reconciledAtUtc"] = reconciledAt.ToString("O"),
                ["externalUserId"] = entry.ExternalUserId,
                ["externalConversationId"] = entry.ExternalConversationId,
                ["rawPayloadPath"] = entry.RawPayloadPath,
                ["rawPayloadSha256"] = webhook.RawPayloadReference.Sha256,
                ["eventType"] = webhook.EventType,
                ["source"] = "provider-callback"
            });

            SortTimeline(timeline);
            ApplyReconciliationMetadata(metadata, webhook, callbackStatus, mappedAttemptStatus, reconciledAt, timeline.Count);
            ApplyAttemptState(attempt, metadata, actorUserId, correlationId, reconciledAt);
            changed = true;
            activity?.SetTag("delivery.reconciliation_result", "applied");
            activity?.SetTag("delivery.attempt_status", mappedAttemptStatus);
            activity?.SetTag("delivery.is_terminal", MessageDeliveryAttempt.IsTerminalDeliveryStatus(mappedAttemptStatus));

            if (ShouldEmitMetric(callbackStatus))
            {
                DeliveryCallbackCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("provider", webhook.Provider),
                    new KeyValuePair<string, object?>("endpoint_key", webhook.EndpointKey),
                    new KeyValuePair<string, object?>("channel", endpoint.Channel),
                    new KeyValuePair<string, object?>("delivery_status", callbackStatus.ToLowerInvariant()),
                    new KeyValuePair<string, object?>("attempt_status", mappedAttemptStatus.ToLowerInvariant()));

                var effectiveOccurredAt = entry.OccurredAtUtc?.ToUniversalTime() ?? reconciledAt;
                DeliveryLifecycleHistogram.Record(
                    CommunicationObservability.ToMilliseconds(attempt.AttemptedAt, effectiveOccurredAt),
                    new KeyValuePair<string, object?>("provider", webhook.Provider),
                    new KeyValuePair<string, object?>("channel", endpoint.Channel),
                    new KeyValuePair<string, object?>("lifecycle", callbackStatus.ToLowerInvariant()),
                    new KeyValuePair<string, object?>("attempt_kind", CommunicationObservability.GetAttemptKind(attempt.AttemptNumber)));
            }

            _logger.LogInformation(
                "Delivery callback reconciled. TenantId: {TenantId}, EndpointId: {EndpointId}, DeliveryAttemptId: {DeliveryAttemptId}, ProviderMessageId: {ProviderMessageId}, DeliveryStatus: {DeliveryStatus}, RawPayloadSha256: {RawPayloadSha256}",
                tenantId,
                endpoint.Id,
                attempt.Id,
                providerMessageId,
                callbackStatus,
                webhook.RawPayloadReference.Sha256);
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ConversationChannelEndpoint> ResolveEndpointAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken)
    {
        var matchingEndpoints = await _dbContext.ConversationChannelEndpoints
            .Where(x => x.EndpointKey == webhook.EndpointKey
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var providerMatches = matchingEndpoints
            .Where(x => string.Equals(x.Provider, webhook.Provider, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (providerMatches.Count == 0)
        {
            throw new KeyNotFoundException($"Delivery status endpoint '{webhook.Provider}/{webhook.EndpointKey}' was not found.");
        }

        var currentTenantId = _currentUserService.TenantId;
        if (currentTenantId.HasValue)
        {
            return providerMatches.SingleOrDefault(x => x.TenantId == currentTenantId.Value)
                ?? throw new KeyNotFoundException(
                    $"Delivery status endpoint '{webhook.Provider}/{webhook.EndpointKey}' was not found for tenant '{currentTenantId.Value:D}'.");
        }

        if (providerMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Delivery status endpoint '{webhook.Provider}/{webhook.EndpointKey}' is ambiguous across tenants without tenant context.");
        }

        return providerMatches[0];
    }

    private static JsonObject ParseMetadataObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new JsonObject();
        }

        try
        {
            var parsed = JsonNode.Parse(metadataJson);
            if (parsed is JsonObject jsonObject)
            {
                return jsonObject;
            }

            var wrapped = new JsonObject();
            if (parsed is not null)
            {
                wrapped["legacyMetadata"] = parsed;
            }

            return wrapped;
        }
        catch (JsonException)
        {
            return new JsonObject
            {
                ["legacyMetadataText"] = metadataJson
            };
        }
    }

    private static JsonArray GetOrCreateTimeline(JsonObject metadata)
    {
        if (metadata["deliveryStatusTimeline"] is JsonArray existing)
        {
            return existing;
        }

        var timeline = new JsonArray();
        metadata["deliveryStatusTimeline"] = timeline;
        return timeline;
    }

    private static bool ContainsEvent(JsonArray timeline, string eventKey)
    {
        return timeline
            .OfType<JsonObject>()
            .Any(x => string.Equals(x["eventKey"]?.GetValue<string>(), eventKey, StringComparison.Ordinal));
    }

    private static void SortTimeline(JsonArray timeline)
    {
        var sortedEntries = timeline
            .Select(node => node?.DeepClone())
            .OrderBy(GetSortTimestamp)
            .ThenBy(GetStatusOrder)
            .ToList();

        timeline.Clear();
        foreach (var entry in sortedEntries)
        {
            timeline.Add(entry);
        }
    }

    private static DateTimeOffset GetSortTimestamp(JsonNode? node)
    {
        return ParseDateTimeOffset(node?["occurredAtUtc"])
            ?? ParseDateTimeOffset(node?["reconciledAtUtc"])
            ?? DateTimeOffset.MinValue;
    }

    private static int GetStatusOrder(JsonNode? node)
    {
        var status = node?["status"]?.GetValue<string>() ?? string.Empty;

        return status switch
        {
            "ACCEPTED" => 0,
            "SENT" => 1,
            "DELIVERED" => 2,
            "READ" => 3,
            "FAILED" => 4,
            "EXPIRED" => 5,
            _ => 99
        };
    }

    private static void ApplyReconciliationMetadata(
        JsonObject metadata,
        NormalizedProviderWebhook webhook,
        string callbackStatus,
        string mappedAttemptStatus,
        DateTimeOffset reconciledAt,
        int timelineCount)
    {
        metadata["reconciliation"] = new JsonObject
        {
            ["seam"] = ReconciliationSeam,
            ["provider"] = webhook.Provider,
            ["endpointKey"] = webhook.EndpointKey,
            ["eventType"] = webhook.EventType,
            ["lastProviderStatus"] = callbackStatus,
            ["lastMappedAttemptStatus"] = mappedAttemptStatus,
            ["lastReconciledAtUtc"] = reconciledAt.ToString("O"),
            ["lastRawPayloadSha256"] = webhook.RawPayloadReference.Sha256,
            ["timelineCount"] = timelineCount
        };
    }

    private static void ApplyAttemptState(
        MessageDeliveryAttempt attempt,
        JsonObject metadata,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset processedAt)
    {
        var timeline = GetOrCreateTimeline(metadata);
        var latestEntry = timeline.OfType<JsonObject>().LastOrDefault();
        if (latestEntry is null)
        {
            return;
        }

        var callbackStatus = latestEntry["status"]?.GetValue<string>() ?? string.Empty;
        var mappedAttemptStatus = latestEntry["mappedAttemptStatus"]?.GetValue<string>() ?? attempt.DeliveryStatus;
        var providerMessageId = latestEntry["providerMessageId"]?.GetValue<string>() ?? attempt.ProviderMessageId;
        var effectiveOccurredAt = ParseDateTimeOffset(latestEntry["occurredAtUtc"])
            ?? ParseDateTimeOffset(latestEntry["reconciledAtUtc"])
            ?? processedAt;
        var attemptedAt = effectiveOccurredAt > attempt.AttemptedAt
            ? effectiveOccurredAt
            : attempt.AttemptedAt;
        var isFinal = MessageDeliveryAttempt.IsTerminalDeliveryStatus(mappedAttemptStatus);

        ApplyAudit(attempt, actorUserId, correlationId, processedAt);
        attempt.RecordProviderSendResult(
            deliveryStatus: mappedAttemptStatus,
            attemptedAt: attemptedAt,
            providerMessageId: providerMessageId,
            httpStatusCode: attempt.HttpStatusCode,
            errorCode: ResolveErrorCode(callbackStatus, mappedAttemptStatus, attempt.ErrorCode),
            errorMessageRedacted: ResolveErrorMessage(callbackStatus, mappedAttemptStatus, attempt.ErrorMessageRedacted),
            nextRetryAt: null,
            finalizedAt: isFinal ? effectiveOccurredAt : null,
            isFinal: isFinal,
            metadataJson: metadata.ToJsonString());
    }

    private static void ApplyAudit(MessageDeliveryAttempt attempt, Guid actorUserId, Guid correlationId, DateTimeOffset occurredAt)
    {
        attempt.ModifiedAt = occurredAt;
        attempt.ModifiedByUid = actorUserId;
        attempt.CorrelationId = correlationId;
    }

    private static string BuildEventKey(
        string providerMessageId,
        string callbackStatus,
        DateTimeOffset? occurredAtUtc,
        string? rawPayloadSha256,
        string rawPayloadPath)
    {
        var suffix = occurredAtUtc.HasValue
            ? occurredAtUtc.Value.ToUniversalTime().ToString("O")
            : $"{rawPayloadSha256}:{rawPayloadPath}";

        return $"{providerMessageId}:{callbackStatus}:{suffix}";
    }

    private static string? FormatDate(DateTimeOffset? timestamp)
        => timestamp?.ToUniversalTime().ToString("O");

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var value = node?.GetValue<string>();
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? ResolveErrorCode(string callbackStatus, string mappedAttemptStatus, string? existingErrorCode)
    {
        if (!string.Equals(mappedAttemptStatus, MessageDeliveryAttempt.DeliveryStatusFailed, StringComparison.Ordinal))
        {
            return null;
        }

        return callbackStatus switch
        {
            "EXPIRED" => "PROVIDER_STATUS_EXPIRED",
            "FAILED" => string.IsNullOrWhiteSpace(existingErrorCode) ? "PROVIDER_STATUS_FAILED" : existingErrorCode,
            _ => existingErrorCode
        };
    }

    private static string? ResolveErrorMessage(string callbackStatus, string mappedAttemptStatus, string? existingErrorMessage)
    {
        if (!string.Equals(mappedAttemptStatus, MessageDeliveryAttempt.DeliveryStatusFailed, StringComparison.Ordinal))
        {
            return null;
        }

        return callbackStatus switch
        {
            "EXPIRED" => "Provider reported the message as expired.",
            "FAILED" => string.IsNullOrWhiteSpace(existingErrorMessage) ? "Provider reported the message as failed." : existingErrorMessage,
            _ => existingErrorMessage
        };
    }

    private static bool ShouldEmitMetric(string callbackStatus)
    {
        return string.Equals(callbackStatus, "DELIVERED", StringComparison.Ordinal)
            || string.Equals(callbackStatus, "FAILED", StringComparison.Ordinal)
            || string.Equals(callbackStatus, "EXPIRED", StringComparison.Ordinal);
    }

    private static bool TryMapCallbackStatus(string? deliveryStatus, out string callbackStatus, out string mappedAttemptStatus)
    {
        callbackStatus = string.Empty;
        mappedAttemptStatus = string.Empty;

        var normalized = deliveryStatus?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        switch (normalized)
        {
            case "ACCEPTED":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusAccepted;
                return true;
            case "SENT":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusSent;
                return true;
            case "DELIVERED":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusDelivered;
                return true;
            case "READ":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusRead;
                return true;
            case "FAILED":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusFailed;
                return true;
            case "EXPIRED":
                callbackStatus = normalized;
                mappedAttemptStatus = MessageDeliveryAttempt.DeliveryStatusFailed;
                return true;
            default:
                return false;
        }
    }
}
