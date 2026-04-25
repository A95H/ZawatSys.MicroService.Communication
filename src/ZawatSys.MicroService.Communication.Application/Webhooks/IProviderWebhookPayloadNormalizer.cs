namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public interface IProviderWebhookPayloadNormalizer
{
    bool CanNormalize(string provider, string eventType);

    NormalizedProviderWebhook Normalize(ProviderWebhookEnvelope envelope);
}
