using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZawatSys.MicroService.Communication.Application.Commands.DispatchProviderWebhookToNormalization;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class TelegramPollingWebhookDispatcher : ITelegramPollingWebhookDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramPollingWebhookDispatcher(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task DispatchAsync(ProviderWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new DispatchProviderWebhookToNormalizationCommand(envelope), cancellationToken);
    }
}
