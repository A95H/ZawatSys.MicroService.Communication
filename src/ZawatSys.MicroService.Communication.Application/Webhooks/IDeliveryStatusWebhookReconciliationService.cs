namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public interface IDeliveryStatusWebhookReconciliationService
{
    Task ReconcileAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken);
}
