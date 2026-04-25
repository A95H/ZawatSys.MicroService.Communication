using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Queries.Common;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetConversationMessages;

public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<InternalResponse<CommunicationPagedResult<ConversationMessageDto>>>;

public sealed record ConversationMessageDto(
    Guid MessageId,
    long Sequence,
    string Direction,
    string SenderType,
    Guid? SenderUserId,
    string? SenderDisplayName,
    string MessageKind,
    Guid? ReplyToMessageId,
    string? ProviderMessageId,
    string? Text,
    bool IsInternalOnly,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ProcessedAt);
