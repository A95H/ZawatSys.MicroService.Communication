using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ResolveConversation;

public sealed class ResolveConversationCommandHandler : IRequestHandler<ResolveConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "ResolveConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ResolveConversationCommandHandler> _logger;

    public ResolveConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ResolveConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(ResolveConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!ConversationControlAuthorization.CanResolve(_currentUserService))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        if (!string.Equals(request.ReasonCode?.Trim(), ConversationControlReasonCodes.Resolve, StringComparison.Ordinal))
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

        ConversationControlCommandSupport.EnsureSessionOpen(session, CommandName);
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeAiPaused, CommandName);

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);

        ConversationControlCommandSupport.EnsureExpectedVersion(control, request.ExpectedControlVersion, CommandName);
        session.MarkResolved(reasonCode, occurredAt);
        ConversationControlCommandSupport.TouchAudit(session, actorUserId, correlationId, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        var transition = ConversationControlCommandSupport.CreateTransitionAudit(
            _currentUserService,
            tenantId,
            request.ConversationId,
            actorUserId,
            correlationId,
            previousMode: ConversationControl.ModeAiPaused,
            newMode: ConversationControl.ModeAiPaused,
            transitionReason: reasonCode,
            controlVersion: control.IntegrationVersion,
            occurredAt: occurredAt,
            noteRedacted: $"session:{ConversationSession.StatusResolved}");

        _dbContext.ConversationControlTransitions.Add(transition);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }
}
