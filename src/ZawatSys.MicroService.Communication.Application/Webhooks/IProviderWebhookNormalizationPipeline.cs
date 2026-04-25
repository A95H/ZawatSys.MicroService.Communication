namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public interface IProviderWebhookNormalizationPipeline
{
    Task ProcessAsync(ProviderWebhookEnvelope envelope, CancellationToken cancellationToken);
}
