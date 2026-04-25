using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Api.HealthChecks;

public sealed class CommunicationDatabaseHealthCheck(
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
            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            return canConnect
                ? HealthCheckResult.Healthy("Communication database is reachable.")
                : HealthCheckResult.Unhealthy("Communication database is unreachable.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Communication database health check timed out.", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Communication database health check failed.", ex);
        }
    }
}
