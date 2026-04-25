using MediatR;
using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Shared.Common;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetConversationDetails;

public sealed class GetConversationDetailsQueryHandler : IRequestHandler<GetConversationDetailsQuery, InternalResponse<ConversationDetailsDto>>
{
    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationDetailsQueryHandler(ICommunicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<InternalResponse<ConversationDetailsDto>> Handle(GetConversationDetailsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required for conversation detail queries.");

        var conversation = await (
            from session in _dbContext.ConversationSessions.AsNoTracking()
            join control in _dbContext.ConversationControls.AsNoTracking() on new { session.TenantId, ConversationId = session.Id }
                equals new { control.TenantId, control.ConversationId }
            join binding in _dbContext.ExternalIdentityBindings.AsNoTracking() on new { session.TenantId, session.ExternalIdentityBindingId }
                equals new { binding.TenantId, ExternalIdentityBindingId = binding.Id }
            join endpoint in _dbContext.ConversationChannelEndpoints.AsNoTracking() on new { session.TenantId, session.ConversationChannelEndpointId }
                equals new { endpoint.TenantId, ConversationChannelEndpointId = endpoint.Id }
            where session.TenantId == tenantId
                  && session.Id == request.ConversationId
                  && !session.IsDeleted
                  && !control.IsDeleted
                  && !binding.IsDeleted
                  && !endpoint.IsDeleted
            select new
            {
                Session = session,
                Control = control,
                Binding = binding,
                Endpoint = endpoint
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return InternalResponse.Fail<ConversationDetailsDto>(
                MessageCodes.NOT_FOUND,
                new InternalError
                {
                    Title = "Not Found",
                    Details = $"Conversation {request.ConversationId:D} was not found."
                });
        }

        var messageCount = await _dbContext.ConversationMessages
            .AsNoTracking()
            .CountAsync(
                message => message.TenantId == tenantId && message.ConversationId == request.ConversationId && !message.IsDeleted,
                cancellationToken);

        return InternalResponse.Success(new ConversationDetailsDto(
            conversation.Session.Id,
            conversation.Session.Channel,
            conversation.Endpoint.Provider,
            conversation.Endpoint.EndpointKey,
            conversation.Binding.ExternalUserId,
            conversation.Binding.ExternalDisplayName,
            conversation.Session.SessionStatus,
            conversation.Control.Mode,
            conversation.Control.IntegrationVersion,
            conversation.Control.AssignedToUserId,
            conversation.Control.AssignedQueueCode,
            conversation.Session.OpenedAt,
            conversation.Session.ResolvedAt,
            conversation.Session.ResolutionCode,
            conversation.Session.LastMessageSequence,
            conversation.Session.LastInboundMessageAt,
            conversation.Session.LastOutboundMessageAt,
            conversation.Session.LastUserMessageAt,
            conversation.Session.LastHumanMessageAt,
            conversation.Session.LastAIMessageAt,
            messageCount));
    }
}
