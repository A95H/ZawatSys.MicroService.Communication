using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Communication.MessageCodes;
using ZawatSys.MicroLib.Shared.Common;
using ZawatSys.MicroLib.Shared.Common.Models;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReplyToConversation;

public sealed class ReplyToConversationCommandHandler : IRequestHandler<ReplyToConversationCommand, InternalResponse<Guid>>
{
    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ReplyToConversationCommandHandler(ICommunicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<InternalResponse<Guid>> Handle(ReplyToConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, nameof(ReplyToConversationCommand));

        if (!_currentUserService.HasPermission(CommunicationPermissions.ReplyToConversation)
            || !ConversationControlAuthorization.IsExpStaff(_currentUserService))
        {
            throw new UnauthorizedAccessException("Actor is not authorized to reply to the conversation.");
        }

        var session = await _dbContext.ConversationSessions
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == request.ConversationId && !x.IsDeleted,
                cancellationToken);

        if (session is null)
        {
            return InternalResponse.Fail<Guid>(
                MessageCodes.NOT_FOUND,
                new InternalError
                {
                    Title = "Not Found",
                    Details = $"Conversation {request.ConversationId:D} was not found."
                });
        }

        var control = await _dbContext.ConversationControls
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.ConversationId == request.ConversationId && !x.IsDeleted,
                cancellationToken);

        if (control is null)
        {
            return InternalResponse.Fail<Guid>(
                MessageCodes.NOT_FOUND,
                new InternalError
                {
                    Title = "Not Found",
                    Details = $"Conversation control for {request.ConversationId:D} was not found."
                });
        }

        ConversationControlCommandSupport.EnsureSessionOpen(session, nameof(ReplyToConversationCommand));
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeHumanActive, nameof(ReplyToConversationCommand));

        ConversationMessage? replyToMessage = null;
        if (request.ReplyToMessageId.HasValue)
        {
            replyToMessage = await _dbContext.ConversationMessages
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId
                         && x.ConversationId == request.ConversationId
                         && x.Id == request.ReplyToMessageId.Value
                         && !x.IsDeleted,
                    cancellationToken);

            if (replyToMessage is null)
            {
                return InternalResponse.Fail<Guid>(
                    MessageCodes.NOT_FOUND,
                    new InternalError
                    {
                        Title = "Not Found",
                        Details = $"Reply target message {request.ReplyToMessageId.Value:D} was not found."
                    });
            }
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);
        var nextSequence = session.LastMessageSequence + 1;
        var content = request.Content.Trim();

        session.RecordOutboundMessage(occurredAt, nextSequence, ConversationMessage.SenderTypeHuman);
        ConversationControlCommandSupport.TouchAudit(session, actorUserId, correlationId, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        var message = new ConversationMessage(
            tenantId,
            request.ConversationId,
            session.ConversationChannelEndpointId,
            nextSequence,
            session.Channel,
            ConversationMessage.DirectionOutbound,
            ConversationMessage.SenderTypeHuman,
            ConversationMessage.MessageKindText,
            occurredAt,
            senderUserId: actorUserId,
            senderDisplayName: "Staff",
            providerCorrelationKey: $"human:{request.ConversationId:D}:{nextSequence}",
            replyToMessageId: replyToMessage?.Id,
            textNormalized: content,
            textRedacted: content,
            metadataJson: JsonSerializer.Serialize(new
            {
                source = "communication.staff-reply",
                conversationId = request.ConversationId,
                actorUserId,
                replyToMessageId = replyToMessage?.Id
            }),
            processedAt: occurredAt);

        ConversationControlCommandSupport.InitializeAudit(message, tenantId, actorUserId, correlationId, occurredAt);

        var attempt = new MessageDeliveryAttempt(
            tenantId,
            message.Id,
            session.ConversationChannelEndpointId,
            attemptNumber: 1,
            deliveryStatus: MessageDeliveryAttempt.DeliveryStatusQueued,
            attemptedAt: occurredAt,
            metadataJson: JsonSerializer.Serialize(new
            {
                source = "communication.staff-reply",
                conversationId = request.ConversationId,
                messageId = message.Id
            }));

        ConversationControlCommandSupport.InitializeAudit(attempt, tenantId, actorUserId, correlationId, occurredAt);

        var handoff = new OutboxMessage
        {
            TenantId = tenantId,
            Type = CommunicationOutboxMessageTypes.OutboundSendRequested,
            Content = JsonSerializer.SerializeToDocument(new
            {
                tenantId,
                conversationId = request.ConversationId,
                sessionId = session.Id,
                requestConversationMessageId = replyToMessage?.Id ?? message.Id,
                outboundConversationMessageId = message.Id,
                conversationChannelEndpointId = session.ConversationChannelEndpointId,
                deliveryAttemptId = attempt.Id,
                expectedControlVersion = control.IntegrationVersion,
                outcome = "HUMAN_REPLY",
                messageKind = message.MessageKind,
                content,
                replyToMessageId = message.ReplyToMessageId,
                relatedAiRequestId = (Guid?)null,
                relatedPendingActionId = (Guid?)null,
                correlationId,
                occurredAt,
                queueSeam = "EXP-002-staff-reply"
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

        _dbContext.ConversationMessages.Add(message);
        _dbContext.MessageDeliveryAttempts.Add(attempt);
        _dbContext.OutboxMessages.Add(handoff);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return InternalResponse.Success(message.Id);
    }
}
