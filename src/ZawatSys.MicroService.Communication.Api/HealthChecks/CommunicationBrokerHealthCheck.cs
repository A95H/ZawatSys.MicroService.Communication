using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ZawatSys.MicroService.Communication.Api.HealthChecks;

public sealed class CommunicationBrokerHealthCheck(
    IConfiguration configuration,
    IOptions<CommunicationDependencyHealthCheckOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var rabbitMqSection = configuration.GetSection("RabbitMq");
        var hostName = rabbitMqSection["Host"];
        var userName = rabbitMqSection["Username"];
        var password = rabbitMqSection["Password"];

        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return HealthCheckResult.Unhealthy("RabbitMQ readiness configuration is incomplete.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.GetDependencyTimeout());

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = int.TryParse(rabbitMqSection["Port"], out var port) ? port : 5672,
                UserName = userName,
                Password = password,
                VirtualHost = rabbitMqSection["VirtualHost"] ?? "/"
            };

            await using var connection = await factory.CreateConnectionAsync(timeout.Token);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: timeout.Token);

            return channel.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ broker is reachable.")
                : HealthCheckResult.Unhealthy("RabbitMQ channel is closed.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check timed out.", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed.", ex);
        }
    }
}
