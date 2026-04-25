using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Infrastructure.Routing;

public sealed class ConversationRoutingGate : IConversationRoutingGate
{
    private const string MeterName = "ZawatSys.MicroService.Communication.Routing";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.Routing";
    private const string AiDispatchDecisionCounterName = "communication.routing.ai_dispatch.decisions";
    private const string RoutingActivityName = "communication.routing.evaluate";
    private const string DefaultAllowedReason = "AI_DISPATCH_ALLOWED";
    private const string HumanActiveBlockedReason = "AI_RESPONSE_SUPPRESSED_HUMAN_ACTIVE";
    private const string AiPausedBlockedReason = "AI_RESPONSE_SUPPRESSED_AI_PAUSED";
    private const string ResolvedBlockedReason = "AI_RESPONSE_SUPPRESSED_RESOLVED";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Counter<long> AiDispatchDecisionCounter = Meter.CreateCounter<long>(AiDispatchDecisionCounterName);

    private readonly CommunicationDbContext _dbContext;
    private readonly IConversationRoutingGateNotifier _routingGateNotifier;
    private readonly ILogger<ConversationRoutingGate> _logger;

    public ConversationRoutingGate(
        CommunicationDbContext dbContext,
        IConversationRoutingGateNotifier routingGateNotifier,
        ILogger<ConversationRoutingGate> logger)
    {
        _dbContext = dbContext;
        _routingGateNotifier = routingGateNotifier;
        _logger = logger;
    }

    public async Task<bool> EvaluateAndRecordAsync(
        Guid tenantId,
        Guid conversationId,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.ConversationSessions
            .SingleAsync(
                x => x.TenantId == tenantId
                    && x.Id == conversationId
                    && !x.IsDeleted,
                cancellationToken);

        var control = await _dbContext.ConversationControls
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.ConversationId == conversationId
                    && !x.IsDeleted,
                cancellationToken);

        var effectiveMode = control?.Mode ?? ConversationControl.ModeAiActive;
        var controlVersion = control?.IntegrationVersion ?? 0L;

        var decision = DetermineDecision(session, effectiveMode);

        using var activity = ActivitySource.StartActivity(RoutingActivityName, ActivityKind.Internal);
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("correlation.id", correlationId.ToString("D"));
        activity?.SetTag("control.mode", effectiveMode);
        activity?.SetTag("control.version", controlVersion);
        activity?.SetTag("session.status", session.SessionStatus);
        activity?.SetTag("routing.allow_ai_dispatch", decision.AllowAiDispatch);
        activity?.SetTag("routing.reason_code", decision.ReasonCode);
        activity?.SetTag("routing.decision", decision.AllowAiDispatch ? "allowed" : "suppressed");

        if (decision.AllowAiDispatch)
        {
            await _routingGateNotifier.NotifyAiDispatchAllowedAsync(
                tenantId,
                conversationId,
                controlVersion,
                effectiveMode,
                decision.ReasonCode,
                actorUserId,
                correlationId,
                cancellationToken);
        }
        else
        {
            await _routingGateNotifier.NotifyAiDispatchSuppressedAsync(
                tenantId,
                conversationId,
                controlVersion,
                effectiveMode,
                decision.ReasonCode,
                actorUserId,
                correlationId,
                cancellationToken);
        }

        AiDispatchDecisionCounter.Add(
            1,
            new KeyValuePair<string, object?>("mode", effectiveMode),
            new KeyValuePair<string, object?>("session_status", session.SessionStatus),
            new KeyValuePair<string, object?>("decision", decision.AllowAiDispatch ? "allowed" : "suppressed"),
            new KeyValuePair<string, object?>("allow_ai_dispatch", decision.AllowAiDispatch),
            new KeyValuePair<string, object?>("reason_code", decision.ReasonCode));

        _logger.LogInformation(
            "Conversation routing gate decision recorded. TenantId: {TenantId}, ConversationId: {ConversationId}, CorrelationId: {CorrelationId}, ActorUserId: {ActorUserId}, ControlVersion: {ControlVersion}, Mode: {Mode}, SessionStatus: {SessionStatus}, AllowAiDispatch: {AllowAiDispatch}, RoutingReasonCode: {RoutingReasonCode}",
            tenantId,
            conversationId,
            correlationId,
            actorUserId,
            controlVersion,
            effectiveMode,
            session.SessionStatus,
            decision.AllowAiDispatch,
            decision.ReasonCode);

        return decision.AllowAiDispatch;
    }

    private static RoutingDecision DetermineDecision(ConversationSession session, string effectiveMode)
    {
        if (string.Equals(session.SessionStatus, ConversationSession.StatusResolved, StringComparison.Ordinal))
        {
            return new RoutingDecision(false, ResolvedBlockedReason);
        }

        if (!string.Equals(session.SessionStatus, ConversationSession.StatusOpen, StringComparison.Ordinal))
        {
            return new RoutingDecision(false, ResolvedBlockedReason);
        }

        if (string.Equals(effectiveMode, ConversationControl.ModeHumanActive, StringComparison.Ordinal))
        {
            return new RoutingDecision(false, HumanActiveBlockedReason);
        }

        if (string.Equals(effectiveMode, ConversationControl.ModeAiPaused, StringComparison.Ordinal))
        {
            return new RoutingDecision(false, AiPausedBlockedReason);
        }

        return new RoutingDecision(true, DefaultAllowedReason);
    }

    private readonly record struct RoutingDecision(bool AllowAiDispatch, string ReasonCode);
}
