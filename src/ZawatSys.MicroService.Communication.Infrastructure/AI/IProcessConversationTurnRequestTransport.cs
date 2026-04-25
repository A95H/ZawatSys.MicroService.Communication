using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Shared.Contracts.Common;

namespace ZawatSys.MicroService.Communication.Infrastructure.AI;

public interface IProcessConversationTurnRequestTransport
{
    Task<PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent>> SendAsync(
        PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd> request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
