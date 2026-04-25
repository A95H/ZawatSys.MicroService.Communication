using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReopenConversation;

public sealed record ReopenConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    string ReasonCode) : IRequest<InternalResponse<long>>;
