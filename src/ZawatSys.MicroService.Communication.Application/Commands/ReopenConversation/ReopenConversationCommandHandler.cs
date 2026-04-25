using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReopenConversation;

public sealed class ReopenConversationCommandHandler : IRequestHandler<ReopenConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "ReopenConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ReopenConversationCommandHandler> _logger;

    public ReopenConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ReopenConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(ReopenConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!ConversationControlAuthorization.CanReopen(_currentUserService))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        if (!string.Equals(request.ReasonCode?.Trim(), ConversationControlReasonCodes.Reopen, StringComparison.Ordinal))
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

        ConversationControlCommandSupport.EnsureSessionStatus(session, ConversationSession.StatusResolved, CommandName);
        ConversationControlCommandSupport.EnsureMode(control, ConversationControl.ModeAiPaused, CommandName);

        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = ConversationControlCommandSupport.ResolveCorrelationId(_currentUserService);

        ConversationControlCommandSupport.EnsureExpectedVersion(control, request.ExpectedControlVersion, CommandName);
        session.Reopen(reasonCode);
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
            noteRedacted: $"session:{ConversationSession.StatusOpen}");

        _dbContext.ConversationControlTransitions.Add(transition);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }
}
