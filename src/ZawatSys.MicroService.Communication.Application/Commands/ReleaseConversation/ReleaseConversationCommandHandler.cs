using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReleaseConversation;

public sealed class ReleaseConversationCommandHandler : IRequestHandler<ReleaseConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "ReleaseConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReleaseConversationCommandHandler> _logger;

    public ReleaseConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ReleaseConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(ReleaseConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!string.Equals(request.ReasonCode?.Trim(), ConversationControlReasonCodes.Release, StringComparison.Ordinal))
        {
            throw new ArgumentException("ReasonCode is required and must be a canonical TransitionReasonCode value.", nameof(request.ReasonCode));
        }

        var reasonCode = request.ReasonCode.Trim();

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

        if (!ConversationControlAuthorization.CanRelease(_currentUserService, activeAssignment?.AssignedToUserId, actorUserId))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        ConversationControlCommandSupport.EnsureSessionOpen(session, CommandName);
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeHumanActive, CommandName);

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);

        ConversationControlCommandSupport.EnsureExpectedVersion(control, request.ExpectedControlVersion, CommandName);
        control.ReleaseToAiPaused(reasonCode, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        if (activeAssignment is not null)
        {
            activeAssignment.Release(occurredAt, reasonCode);
            ConversationControlCommandSupport.TouchAudit(activeAssignment, actorUserId, correlationId, occurredAt);
        }

        var transition = ConversationControlCommandSupport.CreateTransitionAudit(
            _currentUserService,
            tenantId,
            request.ConversationId,
            actorUserId,
            correlationId,
            previousMode: ConversationControl.ModeHumanActive,
            newMode: ConversationControl.ModeAiPaused,
            transitionReason: reasonCode,
            controlVersion: control.IntegrationVersion,
            occurredAt: occurredAt,
            noteRedacted: activeAssignment?.AssignedToUserId?.ToString());

        _dbContext.ConversationControlTransitions.Add(transition);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }
}
