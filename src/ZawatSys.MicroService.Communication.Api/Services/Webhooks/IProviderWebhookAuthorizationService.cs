namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public interface IProviderWebhookAuthorizationService
{
    Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken);
}
