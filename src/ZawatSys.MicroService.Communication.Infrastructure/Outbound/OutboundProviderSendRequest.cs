namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed record OutboundProviderSendRequest(
    Guid TenantId,
    Guid ConversationId,
    Guid SessionId,
    Guid OutboundConversationMessageId,
    Guid DeliveryAttemptId,
    Guid ConversationChannelEndpointId,
    string Channel,
    string Provider,
    string MessageKind,
    string Content,
    string ProviderCorrelationKey,
    Guid? ReplyToMessageId,
    Guid CorrelationId,
    DateTimeOffset OccurredAt);
