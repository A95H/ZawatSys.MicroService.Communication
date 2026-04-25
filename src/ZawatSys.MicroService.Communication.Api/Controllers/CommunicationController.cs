using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZawatSys.MicroService.Communication.Api.Contracts.Communication;
using ZawatSys.MicroService.Communication.Application.Commands.PauseAIForConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ReassignConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ReleaseConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ReopenConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ReplyToConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ResolveConversation;
using ZawatSys.MicroService.Communication.Application.Commands.ResumeAIForConversation;
using ZawatSys.MicroService.Communication.Application.Commands.TakeOverConversation;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Queries.GetCommunicationInbox;
using ZawatSys.MicroService.Communication.Application.Queries.GetConversationDetails;
using ZawatSys.MicroService.Communication.Application.Queries.GetConversationMessages;

namespace ZawatSys.MicroService.Communication.Api.Controllers;

[Authorize]
public sealed class CommunicationController : BaseApiController
{
    private readonly ISender _sender;

    public CommunicationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("inbox")]
    [Authorize(Policy = CommunicationPermissions.ReadConversations)]
    public async Task<IActionResult> GetInbox([FromQuery] CommunicationInboxRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetCommunicationInboxQuery(request.PageNumber, request.PageSize, request.Search), cancellationToken);
        return MapToHttpResponse(response);
    }

    [HttpGet("conversations/{conversationId:guid}")]
    [Authorize(Policy = CommunicationPermissions.ReadConversations)]
    public async Task<IActionResult> GetConversationDetails(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetConversationDetailsQuery(conversationId), cancellationToken);
        return MapToHttpResponse(response);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [Authorize(Policy = CommunicationPermissions.ReadConversations)]
    public async Task<IActionResult> GetConversationMessages(
        Guid conversationId,
        [FromQuery] ConversationMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetConversationMessagesQuery(conversationId, request.PageNumber, request.PageSize), cancellationToken);
        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [Authorize(Policy = CommunicationPermissions.ReplyToConversation)]
    public async Task<IActionResult> ReplyToConversation(
        Guid conversationId,
        [FromBody] ReplyToConversationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ReplyToConversationCommand(conversationId, request.Content ?? string.Empty, request.ReplyToMessageId),
            cancellationToken);

        return MapToCreatedHttpResponse(response, $"/api/communication/conversations/{conversationId:D}/messages/{response.Data:D}");
    }

    [HttpPost("conversations/{conversationId:guid}/actions/takeover")]
    [Authorize(Policy = CommunicationPermissions.TakeOverConversation)]
    public async Task<IActionResult> TakeOverConversation(
        Guid conversationId,
        [FromBody] TakeOverConversationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new TakeOverConversationCommand(conversationId, request.ExpectedControlVersion, request.AssigneeUserId, request.AssignedQueueCode),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/pause")]
    [Authorize(Policy = CommunicationPermissions.PauseAiConversation)]
    public async Task<IActionResult> PauseConversation(
        Guid conversationId,
        [FromBody] ReasonedConversationActionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new PauseAIForConversationCommand(conversationId, request.ExpectedControlVersion, request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/release")]
    [Authorize(Policy = CommunicationPermissions.ReleaseConversation)]
    public async Task<IActionResult> ReleaseConversation(
        Guid conversationId,
        [FromBody] ReasonedConversationActionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ReleaseConversationCommand(conversationId, request.ExpectedControlVersion, request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/resume")]
    [Authorize(Policy = CommunicationPermissions.ResumeAiConversation)]
    public async Task<IActionResult> ResumeConversation(
        Guid conversationId,
        [FromBody] ReasonedConversationActionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ResumeAIForConversationCommand(conversationId, request.ExpectedControlVersion, request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/reassign")]
    [Authorize(Policy = CommunicationPermissions.ReassignConversation)]
    public async Task<IActionResult> ReassignConversation(
        Guid conversationId,
        [FromBody] ReassignConversationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ReassignConversationCommand(
                conversationId,
                request.ExpectedControlVersion,
                request.AssigneeUserId,
                request.AssignedQueueCode,
                request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/resolve")]
    [Authorize(Policy = CommunicationPermissions.ResolveConversation)]
    public async Task<IActionResult> ResolveConversation(
        Guid conversationId,
        [FromBody] ReasonedConversationActionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ResolveConversationCommand(conversationId, request.ExpectedControlVersion, request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }

    [HttpPost("conversations/{conversationId:guid}/actions/reopen")]
    [Authorize(Policy = CommunicationPermissions.ReopenConversation)]
    public async Task<IActionResult> ReopenConversation(
        Guid conversationId,
        [FromBody] ReasonedConversationActionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new ReopenConversationCommand(conversationId, request.ExpectedControlVersion, request.ReasonCode ?? string.Empty),
            cancellationToken);

        return MapToHttpResponse(response);
    }
}
