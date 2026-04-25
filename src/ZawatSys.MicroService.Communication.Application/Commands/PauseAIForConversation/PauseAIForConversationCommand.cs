using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.PauseAIForConversation;

public sealed record PauseAIForConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    string ReasonCode) : IRequest<InternalResponse<long>>;
