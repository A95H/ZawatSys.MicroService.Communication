using ZawatSys.MicroLib.Shared.Common.Models;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public interface IOutboundSendHandoffProcessor
{
    Task ProcessAsync(OutboxMessage handoffMessage, CancellationToken cancellationToken);

    Task<int> RetryPendingAsync(CancellationToken cancellationToken);
}
