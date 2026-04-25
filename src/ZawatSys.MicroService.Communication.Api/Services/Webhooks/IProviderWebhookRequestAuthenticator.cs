namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public interface IProviderWebhookRequestAuthenticator
{
    bool CanHandle(string provider);

    Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken);
}
