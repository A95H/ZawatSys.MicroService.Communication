using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReplyToConversation;

public sealed record ReplyToConversationCommand(
    Guid ConversationId,
    string Content,
    Guid? ReplyToMessageId = null) : IRequest<InternalResponse<Guid>>;
