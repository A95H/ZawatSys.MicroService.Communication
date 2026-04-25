using Microsoft.Extensions.Logging;

namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public sealed class ProviderWebhookAuthorizationService : IProviderWebhookAuthorizationService
{
    private readonly IReadOnlyList<IProviderWebhookRequestAuthenticator> _authenticators;
    private readonly ILogger<ProviderWebhookAuthorizationService> _logger;

    public ProviderWebhookAuthorizationService(
        IEnumerable<IProviderWebhookRequestAuthenticator> authenticators,
        ILogger<ProviderWebhookAuthorizationService> logger)
    {
        _authenticators = authenticators.ToList();
        _logger = logger;
    }

    public async Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var authenticator = _authenticators.FirstOrDefault(candidate => candidate.CanHandle(request.Provider));
        if (authenticator is null)
        {
            _logger.LogWarning(
                "Rejecting webhook request because no authenticator is registered. Provider: {Provider}, EndpointKey: {EndpointKey}, EventType: {EventType}",
                request.Provider,
                request.EndpointKey,
                request.EventType);

            return false;
        }

        return await authenticator.AuthorizeAsync(request, cancellationToken);
    }
}
