using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Shared.Contracts.Common;

namespace ZawatSys.MicroService.Communication.Application.AI;

public interface IConversationTurnOutcomeApplier
{
    Task ApplyAsync(
        ProcessConversationTurnIntegrationCmd request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        CancellationToken cancellationToken);
}
