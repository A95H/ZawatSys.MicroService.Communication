using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ResolveConversation;

public sealed record ResolveConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    string ReasonCode) : IRequest<InternalResponse<long>>;
