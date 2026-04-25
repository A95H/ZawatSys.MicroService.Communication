using System.Text.Json;
using ZawatSys.MicroLib.Shared.Common.Models;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;

namespace ZawatSys.MicroService.Communication.Infrastructure.Routing;

public sealed class OutboxConversationRoutingGateNotifier : IConversationRoutingGateNotifier
{
    private const string RoutingGateSuppressedType = "communication.routing-gate.updated";
    private const string DefaultSuppressedOutcomeCode = "AI_RESPONSE_SUPPRESSED_STALE_CONTROL";

    private readonly ICommunicationDbContext _dbContext;

    public OutboxConversationRoutingGateNotifier(ICommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task NotifyAiDispatchSuppressedAsync(
        Guid tenantId,
        Guid conversationId,
        long controlVersion,
        string mode,
        string reasonCode,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        AddRoutingGateUpdate(
            tenantId,
            conversationId,
            controlVersion,
            mode,
            allowAiDispatch: false,
            suppressionActive: true,
            reasonCode,
            actorUserId,
            correlationId,
            outcomeCode: ResolveSuppressedOutcomeCode(reasonCode),
            cancellationToken);

        return Task.CompletedTask;
    }

    public Task NotifyAiDispatchAllowedAsync(
        Guid tenantId,
        Guid conversationId,
        long controlVersion,
        string mode,
        string reasonCode,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        AddRoutingGateUpdate(
            tenantId,
            conversationId,
            controlVersion,
            mode,
            allowAiDispatch: true,
            suppressionActive: false,
            reasonCode,
            actorUserId,
            correlationId,
            outcomeCode: "AI_DISPATCH_ALLOWED",
            cancellationToken);

        return Task.CompletedTask;
    }

    private void AddRoutingGateUpdate(
        Guid tenantId,
        Guid conversationId,
        long controlVersion,
        string mode,
        bool allowAiDispatch,
        bool suppressionActive,
        string reasonCode,
        Guid actorUserId,
        Guid correlationId,
        string outcomeCode,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var occurredAt = DateTimeOffset.UtcNow;
        var outboxMessage = new OutboxMessage
        {
            TenantId = tenantId,
            Type = RoutingGateSuppressedType,
            Content = JsonSerializer.SerializeToDocument(new
            {
                tenantId,
                conversationId,
                controlVersion,
                mode,
                allowAiDispatch,
                suppressionActive,
                reasonCode,
                actorUserId,
                correlationId,
                occurredAt,
                outcomeCode
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

        _dbContext.OutboxMessages.Add(outboxMessage);
    }

    private static string ResolveSuppressedOutcomeCode(string reasonCode)
    {
        return string.IsNullOrWhiteSpace(reasonCode)
            ? DefaultSuppressedOutcomeCode
            : reasonCode.Trim();
    }
}
