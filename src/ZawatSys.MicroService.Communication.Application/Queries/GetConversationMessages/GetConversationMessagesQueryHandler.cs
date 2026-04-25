using MediatR;
using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Shared.Contracts.Responses;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Queries.Common;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Queries.GetConversationMessages;

public sealed class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, InternalResponse<CommunicationPagedResult<ConversationMessageDto>>>
{
    private readonly ICommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationMessagesQueryHandler(ICommunicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<InternalResponse<CommunicationPagedResult<ConversationMessageDto>>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required for conversation message queries.");

        var query = _dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message =>
                message.TenantId == tenantId
                && message.ConversationId == request.ConversationId
                && !message.IsDeleted)
            .Select(message => new ConversationMessageDto(
                message.Id,
                message.Sequence,
                message.Direction,
                message.SenderType,
                message.SenderUserId,
                message.SenderDisplayName,
                message.MessageKind,
                message.ReplyToMessageId,
                message.ProviderMessageId,
                message.TextRedacted,
                message.IsInternalOnly,
                message.OccurredAt,
                message.ProcessedAt));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(message => message.Sequence)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return InternalResponse.Success(new CommunicationPagedResult<ConversationMessageDto>(
            request.PageNumber,
            request.PageSize,
            totalCount,
            items));
    }
}
