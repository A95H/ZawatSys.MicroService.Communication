using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Api.HealthChecks;

public sealed class CommunicationSecretProviderHealthCheck(
    IServiceScopeFactory scopeFactory,
    IOptions<CommunicationDependencyHealthCheckOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.GetDependencyTimeout());

        try
        {
            var hasExternalSecretReferences = await dbContext.ConversationChannelEndpoints.AnyAsync(
                endpoint => (endpoint.WebhookSecretRef != null && endpoint.WebhookSecretRef != string.Empty)
                    || (endpoint.AccessTokenSecretRef != null && endpoint.AccessTokenSecretRef != string.Empty)
                    || (endpoint.VerificationSecretRef != null && endpoint.VerificationSecretRef != string.Empty),
                timeout.Token);

            return hasExternalSecretReferences
                ? HealthCheckResult.Unhealthy("External secret references exist, but secret-provider integration is not active in Communication yet.")
                : HealthCheckResult.Healthy("No external secret-provider dependency is required by current Communication endpoint configuration.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Communication secret-provider readiness check timed out.", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Communication secret-provider readiness check failed.", ex);
        }
    }
}
