using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ResumeAIForConversation;

public sealed record ResumeAIForConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    string ReasonCode) : IRequest<InternalResponse<long>>;
