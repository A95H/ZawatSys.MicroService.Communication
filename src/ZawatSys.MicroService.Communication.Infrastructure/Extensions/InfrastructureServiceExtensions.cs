using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZawatSys.MicroLib.Shared.Infrastructure.Outbox;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Outbox;

namespace ZawatSys.MicroService.Communication.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddCommunicationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommunicationDbContext>(options =>
            options.UseNpgsql(
                GetRequiredConnectionString(configuration, "DefaultConnection"),
                b => b.MigrationsAssembly(typeof(CommunicationDbContext).Assembly.FullName)));

        services.AddScoped<DbContext>(provider => provider.GetRequiredService<CommunicationDbContext>());

        services.AddMassTransitWithRabbitMq(configuration);

        services.Configure<OutboxDispatchOptions>(options =>
        {
            options.SourceServiceName = "CommunicationService";
            options.BatchSize = int.TryParse(configuration.GetSection("Outbox")["BatchSize"], out var batchSize)
                ? batchSize
                : 50;
        });

        services.Configure<OutboxWorkerOptions>(options =>
        {
            var pollingSeconds = int.TryParse(configuration.GetSection("Outbox")["PollingIntervalSeconds"], out var parsed)
                ? parsed
                : 10;

            options.PollingInterval = TimeSpan.FromSeconds(pollingSeconds);
        });

        services.AddScoped<ZawatSys.MicroService.Communication.Infrastructure.Outbox.IOutboxDispatcher, CommunicationOutboxDispatcher>();
        services.AddScoped<ZawatSys.MicroLib.Shared.Infrastructure.Outbox.IOutboxDispatcher>(sp =>
            sp.GetRequiredService<ZawatSys.MicroService.Communication.Infrastructure.Outbox.IOutboxDispatcher>());
        services.AddHostedService<OutboxWorkerHostedService>();

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string connectionName)
    {
        return configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is missing.");
    }
}
