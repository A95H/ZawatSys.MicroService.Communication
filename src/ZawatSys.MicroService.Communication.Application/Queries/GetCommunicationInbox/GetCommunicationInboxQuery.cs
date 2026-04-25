using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Queries.Common;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetCommunicationInbox;

public sealed record GetCommunicationInboxQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null) : IRequest<InternalResponse<CommunicationPagedResult<CommunicationInboxItemDto>>>;

public sealed record CommunicationInboxItemDto(
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
    long LastMessageSequence,
    DateTimeOffset OpenedAt,
    DateTimeOffset? LastInboundMessageAt,
    DateTimeOffset? LastOutboundMessageAt,
    DateTimeOffset? LastUserMessageAt,
    DateTimeOffset? LastHumanMessageAt,
    DateTimeOffset? LastAiMessageAt,
    string? ResolutionCode);
