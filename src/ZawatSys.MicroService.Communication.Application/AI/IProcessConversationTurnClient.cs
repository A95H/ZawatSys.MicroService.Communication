using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Shared.Contracts.Common;

namespace ZawatSys.MicroService.Communication.Application.AI;

public interface IProcessConversationTurnClient
{
    Task<PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent>> ProcessConversationTurnAsync(
        ProcessConversationTurnIntegrationCmd command,
        CancellationToken cancellationToken);
}
