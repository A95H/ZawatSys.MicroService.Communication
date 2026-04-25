using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public interface IInboundIdentityBindingResolver
{
    Task<(ExternalIdentityBinding Binding, bool Created)> ResolveAsync(
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        NormalizedProviderWebhookEntry entry,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
