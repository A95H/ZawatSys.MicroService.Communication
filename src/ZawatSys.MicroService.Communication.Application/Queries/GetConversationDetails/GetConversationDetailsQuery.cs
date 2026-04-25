using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetConversationDetails;

public sealed record GetConversationDetailsQuery(Guid ConversationId) : IRequest<InternalResponse<ConversationDetailsDto>>;

public sealed record ConversationDetailsDto(
    Guid ConversationId,
    string Channel,
    string Provider,
    string EndpointKey,
    string ExternalUserId,
    string? ExternalDisplayName,
    string SessionStatus,
    string Mode,
    long ControlVersion,
    Guid? AssignedToUserId,
    string? AssignedQueueCode,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionCode,
    long LastMessageSequence,
    DateTimeOffset? LastInboundMessageAt,
    DateTimeOffset? LastOutboundMessageAt,
    DateTimeOffset? LastUserMessageAt,
    DateTimeOffset? LastHumanMessageAt,
    DateTimeOffset? LastAiMessageAt,
    int MessageCount);
