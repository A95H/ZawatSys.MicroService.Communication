using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.TakeOverConversation;

public sealed class TakeOverConversationCommandHandler : IRequestHandler<TakeOverConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "TakeOverConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TakeOverConversationCommandHandler> _logger;

    public TakeOverConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<TakeOverConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(TakeOverConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!ConversationControlAuthorization.CanTakeOver(_currentUserService))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        var session = await _dbContext.ConversationSessions
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == request.ConversationId && !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation session {request.ConversationId} was not found.");

        var control = await _dbContext.ConversationControls
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.ConversationId == request.ConversationId && !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation control for {request.ConversationId} was not found.");

        ConversationControlCommandSupport.EnsureSessionOpen(session, CommandName);
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeAiActive, CommandName);

        var assigneeUserId = ResolveAssignee(actorUserId, request.AssigneeUserId);
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);

        ConversationControlCommandSupport.EnsureExpectedVersion(control, request.ExpectedControlVersion, CommandName);
        control.SetHumanActive(assigneeUserId, request.AssignedQueueCode, ConversationControlReasonCodes.Takeover, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        var activeAssignments = await _dbContext.ConversationAssignments
            .Where(x =>
                x.TenantId == tenantId &&
                x.ConversationId == request.ConversationId &&
                x.AssignmentRole == ConversationAssignment.RoleOwner &&
                x.IsActive &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var activeAssignment in activeAssignments)
        {
            activeAssignment.Release(occurredAt, ConversationControlReasonCodes.Takeover);
            ConversationControlCommandSupport.TouchAudit(activeAssignment, actorUserId, correlationId, occurredAt);
        }

        var assignment = new ConversationAssignment(
            tenantId,
            request.ConversationId,
            assigneeUserId,
            request.AssignedQueueCode,
            ConversationAssignment.RoleOwner,
            actorUserId,
            occurredAt);
        ConversationControlCommandSupport.InitializeAudit(assignment, tenantId, actorUserId, correlationId, occurredAt);

        var transition = ConversationControlCommandSupport.CreateTransitionAudit(
            _currentUserService,
            tenantId,
            request.ConversationId,
            actorUserId,
            correlationId,
            previousMode: ConversationControl.ModeAiActive,
            newMode: ConversationControl.ModeHumanActive,
            transitionReason: ConversationControlReasonCodes.Takeover,
            controlVersion: control.IntegrationVersion,
            occurredAt: occurredAt,
            noteRedacted: request.AssignedQueueCode);

        _dbContext.ConversationAssignments.Add(assignment);
        _dbContext.ConversationControlTransitions.Add(transition);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }

    private Guid ResolveAssignee(Guid actorUserId, Guid? requestedAssigneeUserId)
    {
        if (ConversationControlAuthorization.IsBot(_currentUserService))
        {
            return requestedAssigneeUserId
                   ?? throw new UnauthorizedAccessException("Bot takeover requires an explicit assignee user id.");
        }

        if (!requestedAssigneeUserId.HasValue)
        {
            return actorUserId;
        }

        if (requestedAssigneeUserId.Value == actorUserId)
        {
            return actorUserId;
        }

        if (!ConversationControlAuthorization.CanAssignAnotherUser(_currentUserService))
        {
            throw new UnauthorizedAccessException("Actor is not allowed to assign takeover to another user.");
        }

        return requestedAssigneeUserId.Value;
    }
}
