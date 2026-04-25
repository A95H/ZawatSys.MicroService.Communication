using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.TakeOverConversation;

public sealed record TakeOverConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    Guid? AssigneeUserId = null,
    string? AssignedQueueCode = null) : IRequest<InternalResponse<long>>;
