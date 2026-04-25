using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Common;
using ZawatSys.MicroLib.Shared.Common.Models;
using ZawatSys.MicroService.Communication.Application.AI;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.AI;

public sealed class ConversationTurnOutcomeApplier : IConversationTurnOutcomeApplier
{
    public const string OutboundSendRequestedType = "communication.outbound-send.requested";
    public const string SuppressionAuditType = "communication.ai-response.suppressed";

    private const string StaleControlReasonCode = "AI_RESPONSE_SUPPRESSED_STALE_CONTROL";

    private readonly CommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ConversationTurnOutcomeApplier> _logger;

    public ConversationTurnOutcomeApplier(
        CommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ConversationTurnOutcomeApplier> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task ApplyAsync(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        CancellationToken cancellationToken)
    {
        var outcome = response.Data ?? throw new InvalidOperationException("AI response payload is required.");
        ValidateResponseContract(request, response, outcome);

        var session = await _dbContext.ConversationSessions
            .SingleAsync(
                x => x.TenantId == request.TenantId
                    && x.Id == request.ConversationId
                    && !x.IsDeleted,
                cancellationToken);

        await using var sessionLock = await InboundWebhookLockCoordinator.AcquireSessionAsync(
            request.TenantId,
            session.ConversationChannelEndpointId,
            session.ExternalIdentityBindingId,
            cancellationToken);

        await _dbContext.Entry(session).ReloadAsync(cancellationToken);

        var control = await _dbContext.ConversationControls
            .SingleOrDefaultAsync(
                x => x.TenantId == request.TenantId
                    && x.ConversationId == request.ConversationId
                    && !x.IsDeleted,
                cancellationToken);

        if (control is null)
        {
            throw new InvalidOperationException($"Conversation control snapshot was not found for conversation '{request.ConversationId:D}'.");
        }

        await _dbContext.Entry(control).ReloadAsync(cancellationToken);

        var requestMessage = await _dbContext.ConversationMessages
            .SingleOrDefaultAsync(
                x => x.TenantId == request.TenantId
                    && x.Id == request.ConversationMessageId
                    && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException($"Conversation request message '{request.ConversationMessageId:D}' was not found.");

        var actorUserId = _currentUserService.UserId ?? Guid.Empty;
        var suppressionContext = EvaluateSuppression(request.ExpectedControlVersion, control, session, requestMessage, response);

        if (suppressionContext.ShouldSuppress)
        {
            var occurredAt = DateTimeOffset.UtcNow;

            _logger.LogWarning(
                "AI outcome suppressed before persistence. TenantId: {TenantId}, ConversationId: {ConversationId}, SessionId: {SessionId}, RequestConversationMessageId: {RequestConversationMessageId}, ExpectedControlVersion: {ExpectedControlVersion}, ActualControlVersion: {ActualControlVersion}, RequestSequence: {RequestSequence}, LatestSessionSequence: {LatestSessionSequence}, RequestCorrelationId: {RequestCorrelationId}, LatestSessionCorrelationId: {LatestSessionCorrelationId}, Mode: {Mode}, SessionStatus: {SessionStatus}, CorrelationId: {CorrelationId}, ReasonCode: {ReasonCode}, StaleByControlVersion: {StaleByControlVersion}, StaleBySequence: {StaleBySequence}, StaleByCorrelation: {StaleByCorrelation}, GateBlocked: {GateBlocked}",
                request.TenantId,
                request.ConversationId,
                request.SessionId,
                request.ConversationMessageId,
                outcome.ExpectedControlVersion,
                control.IntegrationVersion,
                requestMessage.Sequence,
                session.LastMessageSequence,
                suppressionContext.RequestCorrelationId,
                session.CorrelationId,
                control.Mode,
                session.SessionStatus,
                suppressionContext.EffectiveCorrelationId,
                StaleControlReasonCode,
                suppressionContext.StaleByControlVersion,
                suppressionContext.StaleBySequence,
                suppressionContext.StaleByCorrelation,
                suppressionContext.GateBlocked);

            _dbContext.OutboxMessages.Add(BuildSuppressionAuditMessage(
                request,
                response,
                control,
                session,
                requestMessage,
                suppressionContext,
                actorUserId,
                occurredAt));

            await _dbContext.SaveChangesAsync(cancellationToken);

            return;
        }

        var deliverable = ResolveDeliverableOutcome(outcome);
        var persistedAt = DateTimeOffset.UtcNow;
        var correlationId = response.CorrelationId == Guid.Empty
            ? (_currentUserService.CorrelationId == Guid.Empty ? Guid.NewGuid() : _currentUserService.CorrelationId)
            : response.CorrelationId;
        var nextSequence = session.LastMessageSequence + 1;

        session.RecordOutboundMessage(deliverable.OccurredAt, nextSequence, ConversationMessage.SenderTypeAi);
        session.ModifiedAt = persistedAt;
        session.ModifiedByUid = actorUserId;
        session.CorrelationId = correlationId;

        var outboundMessage = new ConversationMessage(
            request.TenantId,
            request.ConversationId,
            session.ConversationChannelEndpointId,
            nextSequence,
            session.Channel,
            ConversationMessage.DirectionOutbound,
            ConversationMessage.SenderTypeAi,
            deliverable.MessageKind,
            deliverable.OccurredAt,
            senderDisplayName: "AI",
            providerCorrelationKey: BuildProviderCorrelationKey(request, nextSequence),
            replyToMessageId: requestMessage.Id,
            textNormalized: deliverable.Content,
            textRedacted: deliverable.Content,
            metadataJson: BuildOutcomeMetadataJson(request, response, outcome, deliverable),
            relatedPendingActionId: outcome.PendingAction?.PendingActionId,
            relatedAiRequestId: outcome.AuditMetadata?.RunId,
            processedAt: persistedAt);

        outboundMessage.CreatedAt = persistedAt;
        outboundMessage.ModifiedAt = persistedAt;
        outboundMessage.CreatedByUid = actorUserId;
        outboundMessage.ModifiedByUid = actorUserId;
        outboundMessage.CorrelationId = correlationId;

        _dbContext.ConversationMessages.Add(outboundMessage);

        var attempt = new MessageDeliveryAttempt(
            request.TenantId,
            outboundMessage.Id,
            session.ConversationChannelEndpointId,
            attemptNumber: 1,
            MessageDeliveryAttempt.DeliveryStatusQueued,
            attemptedAt: persistedAt,
            metadataJson: BuildDeliveryAttemptMetadataJson(request, response, outcome, deliverable));

        attempt.CreatedAt = persistedAt;
        attempt.ModifiedAt = persistedAt;
        attempt.CreatedByUid = actorUserId;
        attempt.ModifiedByUid = actorUserId;
        attempt.CorrelationId = correlationId;

        _dbContext.MessageDeliveryAttempts.Add(attempt);
        _dbContext.OutboxMessages.Add(BuildOutboundSendRequestedMessage(
            request,
            response,
            outcome,
            outboundMessage,
            attempt,
            deliverable,
            actorUserId,
            correlationId,
            persistedAt));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateResponseContract(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ProcessConversationTurnIntegrationEvent outcome)
    {
        if (!string.Equals(outcome.ContractVersion, request.ContractVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AI response contract version mismatch. Expected {request.ContractVersion}, actual {outcome.ContractVersion}.");
        }

        if (!string.Equals(outcome.PayloadVersion, request.PayloadVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AI response payload version mismatch. Expected {request.PayloadVersion}, actual {outcome.PayloadVersion}.");
        }

        if (response.CausationId != Guid.Empty && response.CausationId != request.ConversationMessageId)
        {
            throw new InvalidOperationException(
                $"AI response causation mismatch. Expected {request.ConversationMessageId:D}, actual {response.CausationId:D}.");
        }

        if (outcome.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("AI response tenant does not match the originating request.");
        }

        if (outcome.ConversationId != request.ConversationId || outcome.SessionId != request.SessionId)
        {
            throw new InvalidOperationException("AI response conversation/session identifiers do not match the originating request.");
        }

        if (outcome.ConversationMessageId != request.ConversationMessageId)
        {
            throw new InvalidOperationException("AI response request message identifier does not match the originating request.");
        }

        if (outcome.ExpectedControlVersion != request.ExpectedControlVersion)
        {
            throw new InvalidOperationException("AI response ExpectedControlVersion does not match the originating request.");
        }

        if (string.IsNullOrWhiteSpace(outcome.Outcome))
        {
            throw new InvalidOperationException("AI response outcome is required.");
        }
    }

    private static SuppressionContext EvaluateSuppression(
        long expectedControlVersion,
        ConversationControl control,
        ConversationSession session,
        ConversationMessage requestMessage,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response)
    {
        var effectiveCorrelationId = response.CorrelationId;
        var requestCorrelationId = requestMessage.CorrelationId;
        var staleByControlVersion = control.HasVersionConflict(expectedControlVersion);

        return BuildSuppressionContext(control, session, requestMessage, effectiveCorrelationId, requestCorrelationId, staleByControlVersion);
    }

    private static SuppressionContext BuildSuppressionContext(
        ConversationControl control,
        ConversationSession session,
        ConversationMessage requestMessage,
        Guid effectiveCorrelationId,
        Guid requestCorrelationId,
        bool staleByControlVersion)
    {
        var gateBlocked = !string.Equals(control.Mode, ConversationControl.ModeAiActive, StringComparison.Ordinal)
            || !string.Equals(session.SessionStatus, ConversationSession.StatusOpen, StringComparison.Ordinal);
        var staleBySequence = requestMessage.Sequence < session.LastMessageSequence;
        var staleByCorrelation = requestCorrelationId != Guid.Empty
            && session.CorrelationId != Guid.Empty
            && requestCorrelationId != session.CorrelationId;

        return new SuppressionContext(
            staleByControlVersion || gateBlocked || staleBySequence,
            staleByControlVersion,
            gateBlocked,
            staleBySequence,
            staleByCorrelation,
            requestCorrelationId,
            effectiveCorrelationId == Guid.Empty ? requestCorrelationId : effectiveCorrelationId);
    }

    private static DeliverableOutcome ResolveDeliverableOutcome(ProcessConversationTurnIntegrationEvent outcome)
    {
        if (string.Equals(outcome.Outcome, ProcessConversationTurnIntegrationOutcomes.DirectReply, StringComparison.Ordinal))
        {
            var directReply = outcome.DirectReply
                ?? throw new InvalidOperationException("AI direct reply outcome is missing direct reply payload.");

            if (string.IsNullOrWhiteSpace(directReply.Content))
            {
                throw new InvalidOperationException("AI direct reply content is required.");
            }

            return new DeliverableOutcome(
                ResolveMessageKind(directReply.MessageType),
                directReply.Content.Trim(),
                directReply.GeneratedAt ?? DateTimeOffset.UtcNow,
                directReply.Metadata,
                outcome.PendingAction?.PendingActionId);
        }

        if (string.Equals(outcome.Outcome, ProcessConversationTurnIntegrationOutcomes.PendingActionCreated, StringComparison.Ordinal))
        {
            var pendingAction = outcome.PendingAction
                ?? throw new InvalidOperationException("AI pending action outcome is missing pending action payload.");

            if (string.IsNullOrWhiteSpace(pendingAction.ConfirmationPrompt))
            {
                throw new InvalidOperationException("AI pending action confirmation prompt is required.");
            }

            return new DeliverableOutcome(
                ConversationMessage.MessageKindSuggestion,
                pendingAction.ConfirmationPrompt.Trim(),
                pendingAction.ExpiresAt ?? DateTimeOffset.UtcNow,
                pendingAction.Handle?.Metadata,
                pendingAction.PendingActionId);
        }

        throw new InvalidOperationException($"AI outcome '{outcome.Outcome}' is not supported by communication persistence.");
    }

    private static string ResolveMessageKind(string messageType)
    {
        var normalized = messageType?.Trim().ToUpperInvariant();

        return normalized switch
        {
            ConversationMessage.MessageKindText => ConversationMessage.MessageKindText,
            ConversationMessage.MessageKindMedia => ConversationMessage.MessageKindMedia,
            ConversationMessage.MessageKindButtonReply => ConversationMessage.MessageKindButtonReply,
            ConversationMessage.MessageKindCommand => ConversationMessage.MessageKindCommand,
            ConversationMessage.MessageKindSystemNotice => ConversationMessage.MessageKindSystemNotice,
            ConversationMessage.MessageKindSuggestion => ConversationMessage.MessageKindSuggestion,
            _ => throw new InvalidOperationException($"AI direct reply message type '{messageType}' is not supported.")
        };
    }

    private static string BuildProviderCorrelationKey(ProcessConversationTurnIntegrationCmd request, long sequence)
        => $"ai:{request.ConversationMessageId:D}:{sequence}";

    private static string BuildOutcomeMetadataJson(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ProcessConversationTurnIntegrationEvent outcome,
        DeliverableOutcome deliverable)
    {
        return JsonSerializer.Serialize(new
        {
            source = "ai.process-conversation-turn",
            contractVersion = outcome.ContractVersion,
            payloadVersion = outcome.PayloadVersion,
            request = new
            {
                tenantId = request.TenantId,
                conversationId = request.ConversationId,
                sessionId = request.SessionId,
                conversationMessageId = request.ConversationMessageId,
                expectedControlVersion = request.ExpectedControlVersion
            },
            response = new
            {
                correlationId = response.CorrelationId,
                causationId = response.CausationId,
                outcome = outcome.Outcome,
                auditTrailId = outcome.AuditMetadata?.AuditTrailId,
                runId = outcome.AuditMetadata?.RunId,
                runStepId = outcome.AuditMetadata?.RunStepId,
                auditMetadata = outcome.AuditMetadata?.Metadata,
                deliverableMetadata = deliverable.Metadata,
                pendingActionId = deliverable.PendingActionId
            }
        });
    }

    private static string BuildDeliveryAttemptMetadataJson(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ProcessConversationTurnIntegrationEvent outcome,
        DeliverableOutcome deliverable)
    {
        return JsonSerializer.Serialize(new
        {
            handoff = "outbox",
            type = OutboundSendRequestedType,
            requestConversationMessageId = request.ConversationMessageId,
            expectedControlVersion = request.ExpectedControlVersion,
            responseCorrelationId = response.CorrelationId,
            outcome = outcome.Outcome,
            messageKind = deliverable.MessageKind,
            pendingActionId = deliverable.PendingActionId
        });
    }

    private static OutboxMessage BuildOutboundSendRequestedMessage(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ProcessConversationTurnIntegrationEvent outcome,
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        DeliverableOutcome deliverable,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset occurredAt)
    {
        return new OutboxMessage
        {
            TenantId = request.TenantId,
            Type = OutboundSendRequestedType,
            Content = JsonSerializer.SerializeToDocument(new
            {
                tenantId = request.TenantId,
                conversationId = request.ConversationId,
                sessionId = request.SessionId,
                requestConversationMessageId = request.ConversationMessageId,
                outboundConversationMessageId = outboundMessage.Id,
                conversationChannelEndpointId = outboundMessage.ConversationChannelEndpointId,
                deliveryAttemptId = attempt.Id,
                expectedControlVersion = request.ExpectedControlVersion,
                outcome = outcome.Outcome,
                messageKind = deliverable.MessageKind,
                content = deliverable.Content,
                replyToMessageId = outboundMessage.ReplyToMessageId,
                relatedAiRequestId = outboundMessage.RelatedAIRequestId,
                relatedPendingActionId = outboundMessage.RelatedPendingActionId,
                correlationId,
                occurredAt,
                queueSeam = "COM-109-provider-send"
            }),
            OccurredOn = occurredAt,
            Sent = false,
            RetryCount = 0,
            CreatedAt = occurredAt,
            ModifiedAt = occurredAt,
            CreatedByUid = actorUserId,
            ModifiedByUid = actorUserId,
            CorrelationId = correlationId
        };
    }

    private static OutboxMessage BuildSuppressionAuditMessage(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ConversationControl control,
        ConversationSession session,
        ConversationMessage requestMessage,
        SuppressionContext suppressionContext,
        Guid actorUserId,
        DateTimeOffset occurredAt)
    {
        var correlationId = suppressionContext.EffectiveCorrelationId == Guid.Empty
            ? (response.CorrelationId == Guid.Empty ? requestMessage.CorrelationId : response.CorrelationId)
            : suppressionContext.EffectiveCorrelationId;

        return new OutboxMessage
        {
            TenantId = request.TenantId,
            Type = SuppressionAuditType,
            Content = JsonSerializer.SerializeToDocument(new
            {
                tenantId = request.TenantId,
                conversationId = request.ConversationId,
                sessionId = request.SessionId,
                requestConversationMessageId = request.ConversationMessageId,
                requestSequence = requestMessage.Sequence,
                latestSessionSequence = session.LastMessageSequence,
                expectedControlVersion = request.ExpectedControlVersion,
                actualControlVersion = control.IntegrationVersion,
                mode = control.Mode,
                sessionStatus = session.SessionStatus,
                requestCorrelationId = suppressionContext.RequestCorrelationId,
                latestSessionCorrelationId = session.CorrelationId,
                responseCorrelationId = response.CorrelationId,
                staleByControlVersion = suppressionContext.StaleByControlVersion,
                staleBySequence = suppressionContext.StaleBySequence,
                staleByCorrelation = suppressionContext.StaleByCorrelation,
                gateBlocked = suppressionContext.GateBlocked,
                outcomeCode = StaleControlReasonCode,
                occurredAt,
                metric = new
                {
                    name = "communication.ai_response.suppressed",
                    reasonCode = StaleControlReasonCode
                }
            }),
            OccurredOn = occurredAt,
            Sent = false,
            RetryCount = 0,
            CreatedAt = occurredAt,
            ModifiedAt = occurredAt,
            CreatedByUid = actorUserId,
            ModifiedByUid = actorUserId,
            CorrelationId = correlationId
        };
    }

    private sealed record DeliverableOutcome(
        string MessageKind,
        string Content,
        DateTimeOffset OccurredAt,
        IReadOnlyDictionary<string, string>? Metadata,
        Guid? PendingActionId);

    private sealed record SuppressionContext(
        bool ShouldSuppress,
        bool StaleByControlVersion,
        bool GateBlocked,
        bool StaleBySequence,
        bool StaleByCorrelation,
        Guid RequestCorrelationId,
        Guid EffectiveCorrelationId);
}
