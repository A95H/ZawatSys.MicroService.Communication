using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Common.Models;
using ZawatSys.MicroService.Communication.Application.Exceptions;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Control;

internal static class ConversationControlCommandSupport
{
    public static Guid RequireTenantId(ICurrentUserService currentUser)
    {
        return currentUser.TenantId
               ?? throw new UnauthorizedAccessException("Tenant context is required for conversation control commands.");
    }

    public static Guid RequireActorUserId(ICurrentUserService currentUser, string commandName)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException($"Authenticated actor is required for command {commandName}.");
        }

        return currentUser.UserId
               ?? throw new UnauthorizedAccessException($"Actor user id is required for command {commandName}.");
    }

    public static void EnsureSessionOpen(ConversationSession session, string commandName)
    {
        if (!string.Equals(session.SessionStatus, ConversationSession.StatusOpen, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Command {commandName} requires session status OPEN.");
        }
    }

    public static void EnsureSessionStatus(ConversationSession session, string expectedStatus, string commandName)
    {
        if (!string.Equals(session.SessionStatus, expectedStatus, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Command {commandName} requires session status {expectedStatus}.");
        }
    }

    public static void EnsureMode(ConversationControl control, string expectedMode, string commandName)
    {
        if (!string.Equals(control.Mode, expectedMode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Command {commandName} is not allowed from current mode {control.Mode}.");
        }
    }

    public static void EnsureExpectedVersion(ConversationControl control, long expectedControlVersion, string commandName)
    {
        if (!control.TryAdvanceIntegrationVersion(expectedControlVersion))
        {
            throw new StaleControlConflictException(
                $"ExpectedControlVersion mismatch for command {commandName}. Expected {expectedControlVersion}, actual {control.IntegrationVersion}.");
        }
    }

    public static string ResolveTriggeredByType(ICurrentUserService currentUser)
    {
        return ConversationControlAuthorization.IsBot(currentUser)
            ? ConversationControlTransition.TriggeredByTypeSystem
            : ConversationControlTransition.TriggeredByTypeHuman;
    }

    public static Guid ResolveCorrelationId(ICurrentUserService currentUser)
    {
        return currentUser.CorrelationId == Guid.Empty
            ? Guid.NewGuid()
            : currentUser.CorrelationId;
    }

    public static ConversationControlTransition CreateTransitionAudit(
        ICurrentUserService currentUser,
        Guid tenantId,
        Guid conversationId,
        Guid actorUserId,
        Guid correlationId,
        string? previousMode,
        string newMode,
        string transitionReason,
        long controlVersion,
        DateTimeOffset occurredAt,
        string? noteRedacted = null,
        Guid? relatedMessageId = null,
        Guid? relatedAiRequestId = null)
    {
        var transition = new ConversationControlTransition(
            tenantId,
            conversationId,
            previousMode,
            newMode,
            transitionReason,
            ResolveTriggeredByType(currentUser),
            controlVersion,
            occurredAt,
            actorUserId,
            relatedMessageId,
            relatedAiRequestId,
            noteRedacted);

        InitializeAudit(transition, tenantId, actorUserId, correlationId, occurredAt);

        return transition;
    }

    public static void LogTransitionAuditPersisted(
        ILogger logger,
        string commandName,
        ConversationSession session,
        ConversationControl control,
        ConversationControlTransition transition,
        Guid actorUserId,
        Guid correlationId)
    {
        logger.LogInformation(
            "Conversation control transition audit persisted. CommandName: {CommandName}, TenantId: {TenantId}, ConversationId: {ConversationId}, CorrelationId: {CorrelationId}, ActorUserId: {ActorUserId}, TriggeredByType: {TriggeredByType}, PreviousMode: {PreviousMode}, NewMode: {NewMode}, TransitionReason: {TransitionReason}, ControlVersion: {ControlVersion}, SessionStatus: {SessionStatus}, SessionResolutionCode: {SessionResolutionCode}, AssignedToUserId: {AssignedToUserId}, NoteRedacted: {NoteRedacted}, OccurredAt: {OccurredAt}",
            commandName,
            transition.TenantId,
            transition.ConversationId,
            correlationId,
            actorUserId,
            transition.TriggeredByType,
            transition.PreviousMode,
            transition.NewMode,
            transition.TransitionReason,
            transition.ControlVersion,
            session.SessionStatus,
            session.ResolutionCode,
            control.AssignedToUserId,
            transition.NoteRedacted,
            transition.OccurredAt);

        ConversationControlTelemetry.RecordTransition(commandName, session, control, transition, correlationId);
    }

    public static void InitializeAudit(TenantEntity entity, Guid tenantId, Guid actorUserId, Guid correlationId, DateTimeOffset timestamp)
    {
        entity.TenantId = tenantId;
        entity.CreatedAt = timestamp;
        entity.ModifiedAt = timestamp;
        entity.CreatedByUid = actorUserId;
        entity.ModifiedByUid = actorUserId;
        entity.CorrelationId = correlationId;
    }

    public static void TouchAudit(TenantEntity entity, Guid actorUserId, Guid correlationId, DateTimeOffset timestamp)
    {
        entity.ModifiedAt = timestamp;
        entity.ModifiedByUid = actorUserId;
        entity.CorrelationId = correlationId;
    }
}
