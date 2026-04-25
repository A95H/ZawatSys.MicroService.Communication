using MassTransit;
using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Shared.Contracts.Common;

namespace ZawatSys.MicroService.Communication.Infrastructure.AI;

public sealed class MassTransitProcessConversationTurnRequestTransport : IProcessConversationTurnRequestTransport
{
    private readonly IRequestClient<PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd>> _requestClient;

    public MassTransitProcessConversationTurnRequestTransport(
        IRequestClient<PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd>> requestClient)
    {
        _requestClient = requestClient;
    }

    public async Task<PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent>> SendAsync(
        PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd> request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var response = await _requestClient.GetResponse<PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent>>(
            request,
            cancellationToken,
            RequestTimeout.After(s: (int)Math.Ceiling(timeout.TotalSeconds)));

        return response.Message;
    }
}
