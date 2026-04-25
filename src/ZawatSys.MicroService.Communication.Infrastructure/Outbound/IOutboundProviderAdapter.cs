using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public interface IOutboundProviderAdapter
{
    bool CanHandle(string provider);

    Task<OutboundProviderSendResult> SendAsync(
        ConversationChannelEndpoint endpoint,
        ConversationMessage outboundMessage,
        MessageDeliveryAttempt attempt,
        OutboundProviderSendRequest request,
        CancellationToken cancellationToken);
}
