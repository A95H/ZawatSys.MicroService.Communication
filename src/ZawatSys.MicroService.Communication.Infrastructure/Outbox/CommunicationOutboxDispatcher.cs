using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZawatSys.MicroLib.Shared.Infrastructure.Outbox;
using ZawatSys.MicroService.Communication.Infrastructure.AI;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Outbound;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbox;

public sealed class CommunicationOutboxDispatcher : OutboxDispatcherBase<CommunicationDbContext>, IOutboxDispatcher
{
    private readonly CommunicationDbContext _db;
    private readonly IOutboundSendHandoffProcessor _outboundSendHandoffProcessor;

    public CommunicationOutboxDispatcher(
        CommunicationDbContext db,
        IPublishEndpoint publisher,
        IOutboundSendHandoffProcessor outboundSendHandoffProcessor,
        IOptions<OutboxDispatchOptions> options,
        ILogger<OutboxDispatcherBase<CommunicationDbContext>> logger)
        : base(db, context => context.OutboxMessages, publisher, options, logger)
    {
        _db = db;
        _outboundSendHandoffProcessor = outboundSendHandoffProcessor;
    }

    public new async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        await _outboundSendHandoffProcessor.RetryPendingAsync(cancellationToken);

        var pendingHandoffs = await _db.OutboxMessages
            .Where(x => !x.Sent && x.Type == ConversationTurnOutcomeApplier.OutboundSendRequestedType)
            .OrderBy(x => x.OccurredOn)
            .ToListAsync(cancellationToken);

        foreach (var handoff in pendingHandoffs)
        {
            try
            {
                await _outboundSendHandoffProcessor.ProcessAsync(handoff, cancellationToken);
                handoff.Sent = true;
                handoff.SentOn = DateTimeOffset.UtcNow;
                handoff.Error = null;
            }
            catch (Exception ex)
            {
                handoff.RetryCount++;
                handoff.Error = ex.Message;
            }
        }

        if (pendingHandoffs.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        await base.DispatchPendingAsync(cancellationToken);
    }

    async Task ZawatSys.MicroLib.Shared.Infrastructure.Outbox.IOutboxDispatcher.DispatchPendingAsync(CancellationToken cancellationToken)
        => await DispatchPendingAsync(cancellationToken);
}
