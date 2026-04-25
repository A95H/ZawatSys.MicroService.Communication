using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Commands.ResumeAIForConversation;

public sealed class ResumeAIForConversationCommandHandler : IRequestHandler<ResumeAIForConversationCommand, InternalResponse<long>>
{
    private const string CommandName = "ResumeAIForConversation";

    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConversationRoutingGateNotifier _routingGateNotifier;
    private readonly ILogger<ResumeAIForConversationCommandHandler> _logger;

    public ResumeAIForConversationCommandHandler(
        ICommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IConversationRoutingGateNotifier routingGateNotifier,
        ILogger<ResumeAIForConversationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _routingGateNotifier = routingGateNotifier;
        _logger = logger;
    }

    public async Task<InternalResponse<long>> Handle(ResumeAIForConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = ConversationControlCommandSupport.RequireTenantId(_currentUserService);
        var actorUserId = ConversationControlCommandSupport.RequireActorUserId(_currentUserService, CommandName);

        if (!ConversationControlAuthorization.CanResume(_currentUserService))
        {
            throw new UnauthorizedAccessException($"Actor is not authorized to execute {CommandName}.");
        }

        if (!string.Equals(request.ReasonCode?.Trim(), ConversationControlReasonCodes.Resume, StringComparison.Ordinal))
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
        control.SetAiActive(reasonCode, occurredAt);
        ConversationControlCommandSupport.TouchAudit(control, actorUserId, correlationId, occurredAt);

        var transition = ConversationControlCommandSupport.CreateTransitionAudit(
            _currentUserService,
            tenantId,
            request.ConversationId,
            actorUserId,
            correlationId,
            previousMode: ConversationControl.ModeAiPaused,
            newMode: ConversationControl.ModeAiActive,
            transitionReason: reasonCode,
            controlVersion: control.IntegrationVersion,
            occurredAt: occurredAt,
            noteRedacted: "routing:AI_ALLOWED");

        _dbContext.ConversationControlTransitions.Add(transition);

        await _routingGateNotifier.NotifyAiDispatchAllowedAsync(
            tenantId,
            request.ConversationId,
            control.IntegrationVersion,
            control.Mode,
            reasonCode,
            actorUserId,
            correlationId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        ConversationControlCommandSupport.LogTransitionAuditPersisted(_logger, CommandName, session, control, transition, actorUserId, correlationId);

        return InternalResponse.Success(control.IntegrationVersion);
    }
}
