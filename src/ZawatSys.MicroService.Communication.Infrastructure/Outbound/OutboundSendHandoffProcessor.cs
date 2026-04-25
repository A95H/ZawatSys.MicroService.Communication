using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Common.Models;
using ZawatSys.MicroService.Communication.Infrastructure.AI;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Observability;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed class OutboundSendHandoffProcessor : IOutboundSendHandoffProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string MeterName = "ZawatSys.MicroService.Communication.Outbound";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.Outbound";
    private const string SendLatencyHistogramName = "communication.outbound.send.latency";
    private const string SendOutcomeCounterName = "communication.outbound.send.outcomes";
    private const string DeliveryLifecycleHistogramName = "communication.outbound.delivery.lifecycle";
    private const string RetryCounterName = "communication.outbound.retry.events";
    private const string HandoffActivityName = "communication.outbound.handoff.process";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Histogram<double> SendLatencyHistogram = Meter.CreateHistogram<double>(SendLatencyHistogramName, unit: "ms");
    private static readonly Histogram<double> DeliveryLifecycleHistogram = Meter.CreateHistogram<double>(DeliveryLifecycleHistogramName, unit: "ms");
    private static readonly Counter<long> SendOutcomeCounter = Meter.CreateCounter<long>(SendOutcomeCounterName);
    private static readonly Counter<long> RetryCounter = Meter.CreateCounter<long>(RetryCounterName);

    private readonly CommunicationDbContext _dbContext;
    private readonly IEnumerable<IOutboundProviderAdapter> _providerAdapters;
    private readonly ILogger<OutboundSendHandoffProcessor> _logger;
    private readonly OutboundRetryOptions _retryOptions;

    public OutboundSendHandoffProcessor(
        CommunicationDbContext dbContext,
        IEnumerable<IOutboundProviderAdapter> providerAdapters,
        ILogger<OutboundSendHandoffProcessor> logger,
        IOptions<OutboundRetryOptions> retryOptions)
    {
        _dbContext = dbContext;
        _providerAdapters = providerAdapters;
        _logger = logger;
        _retryOptions = retryOptions.Value;
    }

    public async Task ProcessAsync(OutboxMessage handoffMessage, CancellationToken cancellationToken)
    {
        var handoff = JsonSerializer.Deserialize<OutboundSendRequestedHandoff>(
                         handoffMessage.Content.RootElement.GetRawText(),
                         JsonOptions)
                     ?? throw new InvalidOperationException("Outbound send handoff payload is invalid.");

        var outboundMessage = await _dbContext.ConversationMessages
            .SingleAsync(
                x => x.TenantId == handoff.TenantId
                    && x.Id == handoff.OutboundConversationMessageId,
                cancellationToken);

        var attempt = await _dbContext.MessageDeliveryAttempts
            .SingleAsync(
                x => x.TenantId == handoff.TenantId
                    && x.Id == handoff.DeliveryAttemptId,
                cancellationToken);

        var endpoint = await _dbContext.ConversationChannelEndpoints
            .SingleAsync(
                x => x.TenantId == handoff.TenantId
                    && x.Id == handoff.ConversationChannelEndpointId,
                cancellationToken);

        using var activity = ActivitySource.StartActivity(HandoffActivityName, ActivityKind.Internal);
        CommunicationObservability.SetCommonConversationTags(activity, handoff.TenantId, handoff.ConversationId, handoff.SessionId, handoff.CorrelationId);
        activity?.SetTag("delivery.attempt_id", handoff.DeliveryAttemptId);
        activity?.SetTag("delivery.attempt_number", attempt.AttemptNumber);
        activity?.SetTag("delivery.attempt_kind", CommunicationObservability.GetAttemptKind(attempt.AttemptNumber));
        activity?.SetTag("channel", endpoint.Channel);
        activity?.SetTag("provider", endpoint.Provider);
        activity?.SetTag("endpoint.key", endpoint.EndpointKey);
        activity?.SetTag("message.id", handoff.OutboundConversationMessageId);
        activity?.SetTag("message.kind", handoff.MessageKind);

        var startedAt = Stopwatch.GetTimestamp();

        if (!endpoint.OutboundEnabled)
        {
            ApplyFailedAttempt(
                outboundMessage,
                attempt,
                endpoint,
                handoff,
                actorUserId: handoffMessage.CreatedByUid,
                correlationId: handoffMessage.CorrelationId,
                attemptedAt: DateTimeOffset.UtcNow,
                result: new OutboundProviderSendResult(
                    Accepted: false,
                    HttpStatusCode: 409,
                    ProviderMessageId: null,
                    ResponseCode: "OUTBOUND_DISABLED",
                    ErrorCode: "OUTBOUND_DISABLED",
                    ErrorMessageRedacted: "Outbound messaging is disabled for the selected endpoint."),
                isRetryEligible: false,
                nextRetryAt: null);

            RecordSendOutcome(handoff, endpoint, attempt, attemptedAt: DateTimeOffset.UtcNow, result: "failure", accepted: false, httpStatusCode: 409, responseCode: "OUTBOUND_DISABLED", retryEligible: false, nextRetryAt: null, duration: Stopwatch.GetElapsedTime(startedAt));
            activity?.SetTag("outbound.result", "failure");
            activity?.SetTag("outbound.response_code", "OUTBOUND_DISABLED");

            return;
        }

        var providerAdapter = _providerAdapters.SingleOrDefault(x => x.CanHandle(endpoint.Provider))
            ?? throw new InvalidOperationException($"No outbound provider adapter is registered for provider '{endpoint.Provider}'.");

        var request = new OutboundProviderSendRequest(
            handoff.TenantId,
            handoff.ConversationId,
            handoff.SessionId,
            handoff.OutboundConversationMessageId,
            handoff.DeliveryAttemptId,
            handoff.ConversationChannelEndpointId,
            outboundMessage.Channel,
            endpoint.Provider,
            handoff.MessageKind,
            handoff.Content,
            outboundMessage.ProviderCorrelationKey ?? string.Empty,
            outboundMessage.ReplyToMessageId,
            handoff.CorrelationId,
            handoff.OccurredAt);

        OutboundProviderSendResult result;
        var attemptedAt = DateTimeOffset.UtcNow;

        try
        {
            result = await providerAdapter.SendAsync(endpoint, outboundMessage, attempt, request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Outbound provider send threw an unexpected exception. TenantId: {TenantId}, OutboundConversationMessageId: {OutboundConversationMessageId}, DeliveryAttemptId: {DeliveryAttemptId}",
                handoff.TenantId,
                handoff.OutboundConversationMessageId,
                handoff.DeliveryAttemptId);

            result = new OutboundProviderSendResult(
                Accepted: false,
                HttpStatusCode: null,
                ProviderMessageId: null,
                ResponseCode: "PROVIDER_SEND_EXCEPTION",
                ErrorCode: "PROVIDER_SEND_EXCEPTION",
                ErrorMessageRedacted: "Provider send threw an unexpected exception.");

            activity?.SetTag("outbound.error_type", ex.GetType().Name);
        }

        if (result.Accepted)
        {
            ApplyAcceptedAttempt(outboundMessage, attempt, endpoint, handoff, handoffMessage.CreatedByUid, handoffMessage.CorrelationId, attemptedAt, result);
            RecordSendOutcome(handoff, endpoint, attempt, attemptedAt, "success", true, result.HttpStatusCode, result.ResponseCode, false, null, Stopwatch.GetElapsedTime(startedAt));
            activity?.SetTag("outbound.result", "success");
            activity?.SetTag("outbound.response_code", result.ResponseCode);
            return;
        }

        var isRetryEligible = IsRetryEligible(result.HttpStatusCode);
        var shouldRetry = isRetryEligible && OutboundRetryPolicy.CanRetry(attempt.AttemptNumber, _retryOptions);
        DateTimeOffset? nextRetryAt = shouldRetry
            ? attemptedAt.Add(OutboundRetryPolicy.CalculateDelay(attempt.AttemptNumber, result.RetryAfter, _retryOptions))
            : null;

        ApplyFailedAttempt(outboundMessage, attempt, endpoint, handoff, handoffMessage.CreatedByUid, handoffMessage.CorrelationId, attemptedAt, result, shouldRetry, nextRetryAt);
        RecordSendOutcome(handoff, endpoint, attempt, attemptedAt, shouldRetry ? "retry_scheduled" : "failure", false, result.HttpStatusCode, result.ResponseCode, shouldRetry, nextRetryAt, Stopwatch.GetElapsedTime(startedAt));
        activity?.SetTag("outbound.result", shouldRetry ? "retry_scheduled" : "failure");
        activity?.SetTag("outbound.response_code", result.ResponseCode);
    }

    public async Task<int> RetryPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var dueAttempts = await _dbContext.MessageDeliveryAttempts
            .Where(x => !x.IsFinal && x.NextRetryAt != null && x.NextRetryAt <= now)
            .OrderBy(x => x.NextRetryAt)
            .ThenBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);

        if (dueAttempts.Count == 0)
        {
            return 0;
        }

        var retriedCount = 0;

        foreach (var priorAttempt in dueAttempts)
        {
            if (priorAttempt.IsFinal || priorAttempt.NextRetryAt is null)
            {
                continue;
            }

            if (!OutboundRetryPolicy.CanRetry(priorAttempt.AttemptNumber, _retryOptions))
            {
                priorAttempt.FinalizeRetryScheduling(now, BuildRetryExhaustedMetadata(priorAttempt));
                RetryCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("event", "exhausted"),
                    new KeyValuePair<string, object?>("attempt_number", priorAttempt.AttemptNumber),
                    new KeyValuePair<string, object?>("attempt_kind", CommunicationObservability.GetAttemptKind(priorAttempt.AttemptNumber)));
                retriedCount++;
                continue;
            }

            var outboundMessage = await _dbContext.ConversationMessages
                .SingleAsync(
                    x => x.TenantId == priorAttempt.TenantId
                        && x.Id == priorAttempt.ConversationMessageId,
                    cancellationToken);

            var nextAttemptNumber = priorAttempt.AttemptNumber + 1;
            var retryAttempt = new MessageDeliveryAttempt(
                priorAttempt.TenantId,
                priorAttempt.ConversationMessageId,
                priorAttempt.ConversationChannelEndpointId,
                nextAttemptNumber,
                MessageDeliveryAttempt.DeliveryStatusQueued,
                now,
                metadataJson: BuildRetryQueuedMetadata(priorAttempt, nextAttemptNumber));

            retryAttempt.CreatedAt = now;
            retryAttempt.ModifiedAt = retryAttempt.CreatedAt;
            retryAttempt.CreatedByUid = priorAttempt.ModifiedByUid;
            retryAttempt.ModifiedByUid = priorAttempt.ModifiedByUid;
            retryAttempt.CorrelationId = priorAttempt.CorrelationId;

            priorAttempt.FinalizeRetryScheduling(retryAttempt.CreatedAt, BuildRetryPromotedMetadata(priorAttempt, retryAttempt));

            RetryCounter.Add(
                1,
                new KeyValuePair<string, object?>("event", "execution"),
                new KeyValuePair<string, object?>("attempt_number", retryAttempt.AttemptNumber),
                new KeyValuePair<string, object?>("attempt_kind", CommunicationObservability.GetAttemptKind(retryAttempt.AttemptNumber)));

            _dbContext.MessageDeliveryAttempts.Add(retryAttempt);
            _dbContext.OutboxMessages.Add(BuildRetryHandoff(outboundMessage, retryAttempt, priorAttempt));
            retriedCount++;
        }

        if (retriedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return retriedCount;
    }

    private static bool IsRetryEligible(int? httpStatusCode)
    {
        if (!httpStatusCode.HasValue)
        {
            return true;
        }

        return httpStatusCode.Value == 408
            || httpStatusCode.Value == 429
            || httpStatusCode.Value >= 500;
    }

    private static void ApplyAcceptedAttempt(
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        ConversationChannelEndpoint endpoint,
        OutboundSendRequestedHandoff handoff,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset attemptedAt,
        OutboundProviderSendResult result)
    {
        ApplyAudit(outboundMessage, actorUserId, correlationId, attemptedAt);
        ApplyAudit(attempt, actorUserId, correlationId, attemptedAt);

        if (!string.IsNullOrWhiteSpace(result.ProviderMessageId))
        {
            outboundMessage.RecordProviderMessageId(result.ProviderMessageId);
        }

        attempt.RecordProviderSendResult(
            deliveryStatus: MessageDeliveryAttempt.DeliveryStatusAccepted,
            attemptedAt: attemptedAt,
            providerMessageId: result.ProviderMessageId,
            httpStatusCode: result.HttpStatusCode,
            errorCode: null,
            errorMessageRedacted: null,
            nextRetryAt: null,
            finalizedAt: null,
            isFinal: false,
            metadataJson: BuildAttemptAuditMetadata(outboundMessage, endpoint, handoff, result, attemptedAt, isRetryEligible: false, nextRetryAt: null));
    }

    private static void ApplyFailedAttempt(
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        ConversationChannelEndpoint endpoint,
        OutboundSendRequestedHandoff handoff,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset attemptedAt,
        OutboundProviderSendResult result,
        bool isRetryEligible,
        DateTimeOffset? nextRetryAt)
    {
        ApplyAudit(outboundMessage, actorUserId, correlationId, attemptedAt);
        ApplyAudit(attempt, actorUserId, correlationId, attemptedAt);

        if (!string.IsNullOrWhiteSpace(result.ProviderMessageId))
        {
            outboundMessage.RecordProviderMessageId(result.ProviderMessageId);
        }

        attempt.RecordProviderSendResult(
            deliveryStatus: MessageDeliveryAttempt.DeliveryStatusFailed,
            attemptedAt: attemptedAt,
            providerMessageId: result.ProviderMessageId,
            httpStatusCode: result.HttpStatusCode,
            errorCode: result.ErrorCode ?? result.ResponseCode,
            errorMessageRedacted: result.ErrorMessageRedacted,
            nextRetryAt: nextRetryAt,
            finalizedAt: isRetryEligible ? null : attemptedAt,
            isFinal: !isRetryEligible,
            metadataJson: BuildAttemptAuditMetadata(outboundMessage, endpoint, handoff, result, attemptedAt, isRetryEligible, nextRetryAt));

        if (isRetryEligible && nextRetryAt.HasValue)
        {
            RetryCounter.Add(
                1,
                new KeyValuePair<string, object?>("event", "scheduled"),
                new KeyValuePair<string, object?>("attempt_number", attempt.AttemptNumber),
                new KeyValuePair<string, object?>("attempt_kind", CommunicationObservability.GetAttemptKind(attempt.AttemptNumber)),
                new KeyValuePair<string, object?>("http_status_class", CommunicationObservability.GetHttpStatusClass(result.HttpStatusCode)));
        }
    }

    private static void RecordSendOutcome(
        OutboundSendRequestedHandoff handoff,
        ConversationChannelEndpoint endpoint,
        MessageDeliveryAttempt attempt,
        DateTimeOffset attemptedAt,
        string result,
        bool accepted,
        int? httpStatusCode,
        string? responseCode,
        bool retryEligible,
        DateTimeOffset? nextRetryAt,
        TimeSpan duration)
    {
        var attemptKind = CommunicationObservability.GetAttemptKind(attempt.AttemptNumber);

        SendLatencyHistogram.Record(
            CommunicationObservability.ToMilliseconds(duration),
            new KeyValuePair<string, object?>("provider", endpoint.Provider),
            new KeyValuePair<string, object?>("channel", endpoint.Channel),
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("attempt_kind", attemptKind),
            new KeyValuePair<string, object?>("accepted", accepted));

        SendOutcomeCounter.Add(
            1,
            new KeyValuePair<string, object?>("provider", endpoint.Provider),
            new KeyValuePair<string, object?>("channel", endpoint.Channel),
            new KeyValuePair<string, object?>("message_kind", handoff.MessageKind),
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("attempt_kind", attemptKind),
            new KeyValuePair<string, object?>("accepted", accepted),
            new KeyValuePair<string, object?>("http_status_class", CommunicationObservability.GetHttpStatusClass(httpStatusCode)),
            new KeyValuePair<string, object?>("response_code", responseCode ?? "none"),
            new KeyValuePair<string, object?>("retry_eligible", retryEligible));

        if (accepted)
        {
            DeliveryLifecycleHistogram.Record(
                CommunicationObservability.ToMilliseconds(handoff.OccurredAt, attemptedAt),
                new KeyValuePair<string, object?>("provider", endpoint.Provider),
                new KeyValuePair<string, object?>("channel", endpoint.Channel),
                new KeyValuePair<string, object?>("lifecycle", "accepted"),
                new KeyValuePair<string, object?>("attempt_kind", attemptKind));
        }

        if (retryEligible && nextRetryAt.HasValue)
        {
            DeliveryLifecycleHistogram.Record(
                CommunicationObservability.ToMilliseconds(attemptedAt, nextRetryAt.Value),
                new KeyValuePair<string, object?>("provider", endpoint.Provider),
                new KeyValuePair<string, object?>("channel", endpoint.Channel),
                new KeyValuePair<string, object?>("lifecycle", "retry_delay"),
                new KeyValuePair<string, object?>("attempt_kind", attemptKind));
        }
    }

    private static string BuildRetryQueuedMetadata(MessageDeliveryAttempt priorAttempt, int nextAttemptNumber)
    {
        return JsonSerializer.Serialize(new
        {
            retry = new
            {
                sourceAttemptId = priorAttempt.Id,
                sourceAttemptNumber = priorAttempt.AttemptNumber,
                nextAttemptNumber,
                scheduledFrom = priorAttempt.NextRetryAt,
                reason = "transient-outbound-retry"
            }
        });
    }

    private static string BuildRetryPromotedMetadata(MessageDeliveryAttempt priorAttempt, MessageDeliveryAttempt retryAttempt)
    {
        return JsonSerializer.Serialize(new
        {
            retry = new
            {
                promotedToAttemptId = retryAttempt.Id,
                promotedToAttemptNumber = retryAttempt.AttemptNumber,
                finalizedReason = "retry-dispatched"
            }
        });
    }

    private static string BuildRetryExhaustedMetadata(MessageDeliveryAttempt attempt)
    {
        return JsonSerializer.Serialize(new
        {
            retry = new
            {
                attempt.AttemptNumber,
                finalizedReason = "max-attempts-reached"
            }
        });
    }

    private static OutboxMessage BuildRetryHandoff(ConversationMessage outboundMessage, MessageDeliveryAttempt retryAttempt, MessageDeliveryAttempt priorAttempt)
    {
        var occurredAt = retryAttempt.CreatedAt;

        return new OutboxMessage
        {
            TenantId = retryAttempt.TenantId,
            Type = ConversationTurnOutcomeApplier.OutboundSendRequestedType,
            Content = JsonSerializer.SerializeToDocument(new
            {
                tenantId = retryAttempt.TenantId,
                conversationId = outboundMessage.ConversationId,
                sessionId = outboundMessage.ConversationId,
                requestConversationMessageId = outboundMessage.ReplyToMessageId,
                outboundConversationMessageId = outboundMessage.Id,
                conversationChannelEndpointId = retryAttempt.ConversationChannelEndpointId,
                deliveryAttemptId = retryAttempt.Id,
                expectedControlVersion = 0,
                outcome = "RETRY",
                messageKind = outboundMessage.MessageKind,
                content = outboundMessage.TextRedacted ?? outboundMessage.TextNormalized ?? string.Empty,
                replyToMessageId = outboundMessage.ReplyToMessageId,
                relatedAiRequestId = outboundMessage.RelatedAIRequestId,
                relatedPendingActionId = outboundMessage.RelatedPendingActionId,
                correlationId = retryAttempt.CorrelationId,
                occurredAt,
                queueSeam = "COM-109-provider-send",
                retry = new
                {
                    sourceAttemptId = priorAttempt.Id,
                    sourceAttemptNumber = priorAttempt.AttemptNumber
                }
            }),
            OccurredOn = occurredAt,
            Sent = false,
            RetryCount = 0,
            CreatedAt = occurredAt,
            ModifiedAt = occurredAt,
            CreatedByUid = retryAttempt.CreatedByUid,
            ModifiedByUid = retryAttempt.ModifiedByUid,
            CorrelationId = retryAttempt.CorrelationId
        };
    }

    private static string BuildAttemptAuditMetadata(
        ConversationMessage outboundMessage,
        ConversationChannelEndpoint endpoint,
        OutboundSendRequestedHandoff handoff,
        OutboundProviderSendResult result,
        DateTimeOffset attemptedAt,
        bool isRetryEligible,
        DateTimeOffset? nextRetryAt)
    {
        return JsonSerializer.Serialize(new
        {
            handoff = new
            {
                type = ConversationTurnOutcomeApplier.OutboundSendRequestedType,
                queueSeam = handoff.QueueSeam,
                correlationId = handoff.CorrelationId,
                occurredAt = handoff.OccurredAt,
                payload = handoff
            },
            outboundMessage = new
            {
                outboundMessage.Id,
                outboundMessage.ConversationId,
                outboundMessage.Sequence,
                outboundMessage.MessageKind,
                outboundMessage.ProviderCorrelationKey,
                outboundMessage.ReplyToMessageId,
                existingMetadata = CloneJson(outboundMessage.MetadataJson)
            },
            endpoint = new
            {
                endpoint.Id,
                endpoint.Channel,
                endpoint.Provider,
                endpoint.EndpointKey
            },
            providerSend = new
            {
                attemptedAt,
                accepted = result.Accepted,
                providerMessageId = result.ProviderMessageId,
                httpStatusCode = result.HttpStatusCode,
                responseCode = result.ResponseCode,
                errorCode = result.ErrorCode,
                errorMessageRedacted = result.ErrorMessageRedacted,
                retryEligible = isRetryEligible,
                nextRetryAt,
                responseMetadata = result.ResponseMetadata ?? new Dictionary<string, object?>()
            }
        });
    }

    private static object? CloneJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static void ApplyAudit<T>(T entity, Guid actorUserId, Guid correlationId, DateTimeOffset occurredAt)
        where T : ZawatSys.MicroLib.Shared.Common.Models.TenantEntity
    {
        entity.ModifiedAt = occurredAt;
        entity.ModifiedByUid = actorUserId;
        entity.CorrelationId = correlationId;
    }

    private sealed class OutboundSendRequestedHandoff
    {
        public Guid TenantId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SessionId { get; set; }
        public Guid RequestConversationMessageId { get; set; }
        public Guid OutboundConversationMessageId { get; set; }
        public Guid ConversationChannelEndpointId { get; set; }
        public Guid DeliveryAttemptId { get; set; }
        public long ExpectedControlVersion { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string MessageKind { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Guid? ReplyToMessageId { get; set; }
        public Guid? RelatedAiRequestId { get; set; }
        public Guid? RelatedPendingActionId { get; set; }
        public Guid CorrelationId { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public string QueueSeam { get; set; } = string.Empty;
    }
}
