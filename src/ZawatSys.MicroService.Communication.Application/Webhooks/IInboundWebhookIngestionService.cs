namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public interface IInboundWebhookIngestionService
{
    Task IngestAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken);
}
