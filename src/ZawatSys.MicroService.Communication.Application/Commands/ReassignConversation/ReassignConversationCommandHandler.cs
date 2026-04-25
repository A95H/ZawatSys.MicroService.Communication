using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReassignConversation;

public sealed class ReassignConversationCommandHandler : IRequestHandler<ReassignConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "ReassignConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConversationStaffDirectory _staffDirectory;
    private readonly ILogger<ReassignConversationCommandHandler> _logger;

    public ReassignConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IConversationStaffDirectory staffDirectory,
        ILogger<ReassignConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _staffDirectory = staffDirectory;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(ReassignConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!ConversationControlAuthorization.CanReassign(_currentUserService))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        if (!string.Equals(request.ReasonCode?.Trim(), ConversationControlReasonCodes.Reassign, StringComparison.Ordinal))
        {
            throw new ArgumentException("ReasonCode is required and must be a canonical TransitionReasonCode value.", nameof(request.ReasonCode));
        }

        var reasonCode = request.ReasonCode.Trim();

        if (!await _staffDirectory.IsActiveAuthorizedStaffAsync(tenantId, request.AssigneeUserId, cancellationToken))
        {
            throw new InvalidOperationException("Target assignee must be active authorized staff.");
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

        var activeAssignment = await _dbContext.ConversationAssignments
            .Where(x =>
                x.TenantId == tenantId &&
                x.ConversationId == request.ConversationId &&
                x.AssignmentRole == ConversationAssignment.RoleOwner &&
                x.IsActive &&
                !x.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken);

        ConversationControlCommandSupport.EnsureSessionOpen(session, CommandName);
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeHumanActive, CommandName);

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);

        ConversationControlCommandSupport.EnsureExpectedVersion(control, request.ExpectedControlVersion, CommandName);

        var previousAssigneeUserId = activeAssignment?.AssignedToUserId;

        if (activeAssignment is not null)
        {
            activeAssignment.Release(occurredAt, reasonCode);
            ConversationControlCommandSupport.TouchAudit(activeAssignment, actorUserId, correlationId, occurredAt);
        }

        control.ReassignHumanOwner(request.AssigneeUserId, request.AssignedQueueCode, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        var replacementAssignment = new ConversationAssignment(
            tenantId,
            request.ConversationId,
            request.AssigneeUserId,
            request.AssignedQueueCode,
            ConversationAssignment.RoleOwner,
            actorUserId,
            occurredAt);
        ConversationControlCommandSupport.InitializeAudit(replacementAssignment, tenantId, actorUserId, correlationId, occurredAt);

        var transition = ConversationControlCommandSupport.CreateTransitionAudit(
            _currentUserService,
            tenantId,
            request.ConversationId,
            actorUserId,
            correlationId,
            previousMode: ConversationControl.ModeHumanActive,
            newMode: ConversationControl.ModeHumanActive,
            transitionReason: reasonCode,
            controlVersion: control.IntegrationVersion,
            occurredAt: occurredAt,
            noteRedacted: $"{previousAssigneeUserId?.ToString() ?? "UNASSIGNED"}->{request.AssigneeUserId}");

        _dbContext.ConversationAssignments.Add(replacementAssignment);
        _dbContext.ConversationControlTransitions.Add(transition);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }
}
