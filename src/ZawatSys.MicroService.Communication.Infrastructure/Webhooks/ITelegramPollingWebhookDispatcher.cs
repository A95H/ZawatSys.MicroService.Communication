using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public interface ITelegramPollingWebhookDispatcher
{
    Task DispatchAsync(ProviderWebhookEnvelope envelope, CancellationToken cancellationToken);
}
