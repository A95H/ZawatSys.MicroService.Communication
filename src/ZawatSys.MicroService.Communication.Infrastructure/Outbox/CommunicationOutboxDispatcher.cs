using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZawatSys.MicroLib.Shared.Infrastructure.Outbox;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbox;

public sealed class CommunicationOutboxDispatcher : OutboxDispatcherBase<CommunicationDbContext>, IOutboxDispatcher
{
    public CommunicationOutboxDispatcher(
        CommunicationDbContext db,
        IPublishEndpoint publisher,
        IOptions<OutboxDispatchOptions> options,
        ILogger<OutboxDispatcherBase<CommunicationDbContext>> logger)
        : base(db, context => context.OutboxMessages, publisher, options, logger)
    {
    }
}
