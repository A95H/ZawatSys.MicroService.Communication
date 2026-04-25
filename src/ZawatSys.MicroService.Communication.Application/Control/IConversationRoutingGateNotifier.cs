namespace ZawatSys.MicroService.Communication.Application.Control;

public interface IConversationRoutingGateNotifier
{
    Task NotifyAiDispatchSuppressedAsync(
        Guid tenantId,
        Guid conversationId,
        long controlVersion,
        string mode,
        string reasonCode,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task NotifyAiDispatchAllowedAsync(
        Guid tenantId,
        Guid conversationId,
        long controlVersion,
        string mode,
        string reasonCode,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken);
}
