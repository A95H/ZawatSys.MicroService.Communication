namespace ZawatSys.MicroService.Communication.Application.Control;

public interface IConversationRoutingGate
{
    Task<bool> EvaluateAndRecordAsync(
        Guid tenantId,
        Guid conversationId,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken);
}
