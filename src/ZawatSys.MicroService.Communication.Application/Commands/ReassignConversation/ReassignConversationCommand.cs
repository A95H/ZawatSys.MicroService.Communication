using MediatR;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Application.Commands.ReassignConversation;

public sealed record ReassignConversationCommand(
    Guid ConversationId,
    long ExpectedControlVersion,
    Guid AssigneeUserId,
    string? AssignedQueueCode,
    string ReasonCode) : IRequest<InternalResponse<long>>;
