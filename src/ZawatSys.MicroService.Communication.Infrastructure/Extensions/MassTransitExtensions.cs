using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZawatSys.MicroService.Communication.Infrastructure.Extensions;

public static class MassTransitExtensions
{
    private const string CommunicationQueuePrefix = "communication-write";

    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var massTransitSection = configuration.GetSection("MassTransit");
        var requestTimeoutSeconds = int.TryParse(massTransitSection["RequestTimeoutSeconds"], out var reqTimeout)
            ? reqTimeout
            : 120;

        var consumerTimeoutSeconds = int.TryParse(massTransitSection["ConsumerTimeoutSeconds"], out var consTimeout)
            ? consTimeout
            : requestTimeoutSeconds;

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(CommunicationQueuePrefix, false));
            x.AddConsumers(typeof(MassTransitExtensions).Assembly);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("RabbitMq");

                cfg.Host(rabbitMqConfig["Host"], rabbitMqConfig["VirtualHost"] ?? "/", h =>
                {
                    h.Username(rabbitMqConfig["Username"] ?? "guest");
                    h.Password(rabbitMqConfig["Password"] ?? "guest");
                });

                cfg.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMinutes(1)));

                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromMilliseconds(500),
                    maxInterval: TimeSpan.FromSeconds(10),
                    intervalDelta: TimeSpan.FromSeconds(1)));

                cfg.UseTimeout(t => t.Timeout = TimeSpan.FromSeconds(consumerTimeoutSeconds));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
