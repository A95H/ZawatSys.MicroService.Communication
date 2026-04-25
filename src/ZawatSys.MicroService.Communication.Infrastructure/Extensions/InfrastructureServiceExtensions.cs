using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZawatSys.MicroService.Communication.Application.AI;
using ZawatSys.MicroLib.Shared.Infrastructure.Outbox;
using ZawatSys.MicroService.Communication.Application.Abstractions;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Infrastructure.AI;
using ZawatSys.MicroService.Communication.Infrastructure.Control;
using ZawatSys.MicroService.Communication.Infrastructure.Data;
using ZawatSys.MicroService.Communication.Infrastructure.Outbox;
using ZawatSys.MicroService.Communication.Infrastructure.Outbound;
using ZawatSys.MicroService.Communication.Infrastructure.Routing;
using ZawatSys.MicroService.Communication.Infrastructure.Webhooks;
using ZawatSys.MicroService.Communication.Application.Webhooks;

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
        services.AddScoped<ICommunicationDbContext>(provider => provider.GetRequiredService<CommunicationDbContext>());
        services.AddScoped<IConversationRoutingGateNotifier, OutboxConversationRoutingGateNotifier>();
        services.AddScoped<IConversationRoutingGate, ConversationRoutingGate>();
        services.AddScoped<IConversationStaffDirectory, CurrentUserBackedConversationStaffDirectory>();
        services.Configure<ProcessConversationTurnClientOptions>(options =>
        {
            var section = configuration.GetSection(ProcessConversationTurnClientOptions.SectionName);

            if (int.TryParse(section["TimeoutSeconds"], out var timeoutSeconds))
            {
                options.TimeoutSeconds = timeoutSeconds;
            }

            if (int.TryParse(section["RetryCount"], out var retryCount))
            {
                options.RetryCount = retryCount;
            }

            if (int.TryParse(section["RetryDelayMilliseconds"], out var retryDelayMilliseconds))
            {
                options.RetryDelayMilliseconds = retryDelayMilliseconds;
            }
        });
        services.AddScoped<IProcessConversationTurnRequestTransport, MassTransitProcessConversationTurnRequestTransport>();
        services.AddScoped<IProcessConversationTurnClient, ProcessConversationTurnClient>();
        services.AddScoped<IConversationTurnOutcomeApplier, ConversationTurnOutcomeApplier>();
        services.AddScoped<IProviderWebhookPayloadNormalizer, MetaProviderWebhookPayloadNormalizer>();
        services.AddScoped<IProviderWebhookPayloadNormalizer, TelegramProviderWebhookPayloadNormalizer>();
        services.AddScoped<IInboundIdentityBindingResolver, InboundIdentityBindingResolver>();
        services.AddScoped<IInboundWebhookIngestionService, InboundWebhookIngestionService>();
        services.AddScoped<IDeliveryStatusWebhookReconciliationService, DeliveryStatusWebhookReconciliationService>();
        services.AddScoped<IProviderWebhookNormalizationPipeline, LoggingProviderWebhookNormalizationPipeline>();
        services.AddScoped<IOutboundProviderAdapter, MetaOutboundProviderAdapter>();
        services.AddScoped<IOutboundProviderAdapter, TelegramOutboundProviderAdapter>();
        services.Configure<OutboundRetryOptions>(options =>
        {
            var section = configuration.GetSection(OutboundRetryOptions.SectionName);

            if (int.TryParse(section["MaxAttempts"], out var maxAttempts))
            {
                options.MaxAttempts = maxAttempts;
            }

            if (int.TryParse(section["BaseDelaySeconds"], out var baseDelaySeconds))
            {
                options.BaseDelay = TimeSpan.FromSeconds(baseDelaySeconds);
            }

            if (int.TryParse(section["MaxDelaySeconds"], out var maxDelaySeconds))
            {
                options.MaxDelay = TimeSpan.FromSeconds(maxDelaySeconds);
            }
        });
        services.AddScoped<IOutboundSendHandoffProcessor, OutboundSendHandoffProcessor>();

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

        var telegramPollingOptions = TelegramPollingOptions.FromConfiguration(configuration);
        services.AddSingleton(telegramPollingOptions);
        services.AddSingleton<ITelegramPollingWebhookDispatcher, TelegramPollingWebhookDispatcher>();

        if (telegramPollingOptions.Enabled)
        {
            services.AddHostedService<TelegramPollingHostedService>();
        }

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string connectionName)
    {
        return configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is missing.");
    }
}
