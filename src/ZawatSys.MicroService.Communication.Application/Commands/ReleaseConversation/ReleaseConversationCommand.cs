using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReleaseConversation;

public sealed record ReleaseConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    string ReasonCode) : IRequest<InternalResponse<long>>;
