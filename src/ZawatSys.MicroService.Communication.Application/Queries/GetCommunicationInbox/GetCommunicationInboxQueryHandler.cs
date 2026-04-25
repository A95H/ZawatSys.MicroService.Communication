using MediatR;
using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Queries.Common;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetCommunicationInbox;

public sealed class GetCommunicationInboxQueryHandler : IRequestHandler<GetCommunicationInboxQuery, InternalResponse<CommunicationPagedResult<CommunicationInboxItemDto>>>
{
    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetCommunicationInboxQueryHandler(ICommunicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<InternalResponse<CommunicationPagedResult<CommunicationInboxItemDto>>> Handle(GetCommunicationInboxQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required for communication inbox queries.");

        var query =
            from session in _dbContext.ConversationSessions.AsNoTracking()
            join control in _dbContext.ConversationControls.AsNoTracking() on new { session.TenantId, ConversationId = session.Id }
                equals new { control.TenantId, control.ConversationId }
            join binding in _dbContext.ExternalIdentityBindings.AsNoTracking() on new { session.TenantId, session.ExternalIdentityBindingId }
                equals new { binding.TenantId, ExternalIdentityBindingId = binding.Id }
            join endpoint in _dbContext.ConversationChannelEndpoints.AsNoTracking() on new { session.TenantId, session.ConversationChannelEndpointId }
                equals new { endpoint.TenantId, ConversationChannelEndpointId = endpoint.Id }
            where session.TenantId == tenantId
                  && !session.IsDeleted
                  && !control.IsDeleted
                  && !binding.IsDeleted
                  && !endpoint.IsDeleted
            select new CommunicationInboxItemDto(
                session.Id,
                session.Channel,
                endpoint.Provider,
                endpoint.EndpointKey,
                binding.ExternalUserId,
                binding.ExternalDisplayName,
                session.SessionStatus,
                control.Mode,
                control.IntegrationVersion,
                control.AssignedToUserId,
                control.AssignedQueueCode,
                session.LastMessageSequence,
                session.OpenedAt,
                session.LastInboundMessageAt,
                session.LastOutboundMessageAt,
                session.LastUserMessageAt,
                session.LastHumanMessageAt,
                session.LastAIMessageAt,
                session.ResolutionCode);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.ExternalUserId.Contains(search)
                || (item.ExternalDisplayName != null && item.ExternalDisplayName.Contains(search))
                || item.Channel.Contains(search)
                || item.EndpointKey.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.LastInboundMessageAt ?? item.LastOutboundMessageAt ?? item.OpenedAt)
            .ThenByDescending(item => item.OpenedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return InternalResponse.Success(new CommunicationPagedResult<CommunicationInboxItemDto>(
            request.PageNumber,
            request.PageSize,
            totalCount,
            items));
    }
}
