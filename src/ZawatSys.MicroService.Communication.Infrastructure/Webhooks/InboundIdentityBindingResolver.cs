using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroService.Communication.Application.Webhooks;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class InboundIdentityBindingResolver : IInboundIdentityBindingResolver
{
    private readonly CommunicationDbContext _dbContext;

    public InboundIdentityBindingResolver(CommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(ExternalIdentityBinding Binding, bool Created)> ResolveAsync(
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        NormalizedProviderWebhookEntry entry,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        if (endpoint.TenantId != tenantId)
        {
            throw new InvalidOperationException("Inbound endpoint tenant does not match identity resolution tenant.");
        }

        if (string.IsNullOrWhiteSpace(entry.ExternalUserId))
        {
            throw new ArgumentException("Inbound external user id is required for identity resolution.", nameof(entry));
        }

        var externalUserId = entry.ExternalUserId.Trim();
        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        if (string.IsNullOrWhiteSpace(normalizedExternalUserId))
        {
            throw new ArgumentException("Inbound external user id could not be normalized for identity resolution.", nameof(entry));
        }

        await using var bindingLock = await InboundWebhookLockCoordinator.AcquireBindingAsync(
            tenantId,
            endpoint.Id,
            normalizedExternalUserId,
            cancellationToken);

        var matchingBindings = await _dbContext.ExternalIdentityBindings
            .Where(x => x.TenantId == tenantId
                && x.ConversationChannelEndpointId == endpoint.Id
                && x.NormalizedExternalUserId == normalizedExternalUserId
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var activeBinding = matchingBindings.SingleOrDefault(x => !x.IsBlocked);
        if (activeBinding is not null)
        {
            ValidateBindingConsistency(activeBinding, tenantId, endpoint);
            activeBinding.MarkResolved(timestamp, entry.DisplayName);
            ApplyAudit(activeBinding, actorUserId, correlationId, timestamp);
            return (activeBinding, false);
        }

        if (matchingBindings.Count != 0)
        {
            throw new InvalidOperationException(
                $"Inbound identity '{normalizedExternalUserId}' is blocked for tenant '{tenantId:D}' and endpoint '{endpoint.Id:D}'.");
        }

        var createdBinding = new ExternalIdentityBinding(
            tenantId,
            endpoint.Id,
            endpoint.Channel,
            externalUserId,
            normalizedExternalUserId,
            bindingKind: "Guest",
            verificationStatus: "Unverified",
            externalDisplayName: entry.DisplayName,
            lastResolvedAt: timestamp,
            metadataJson: BuildBindingMetadataJson(endpoint, normalizedExternalUserId));

        ApplyAudit(createdBinding, actorUserId, correlationId, timestamp);
        _dbContext.ExternalIdentityBindings.Add(createdBinding);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (createdBinding, true);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(createdBinding).State = EntityState.Detached;

            var persistedBinding = await _dbContext.ExternalIdentityBindings
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId
                        && x.ConversationChannelEndpointId == endpoint.Id
                        && x.NormalizedExternalUserId == normalizedExternalUserId
                        && !x.IsBlocked
                        && !x.IsDeleted,
                    cancellationToken);

            if (persistedBinding is null)
            {
                throw;
            }

            ValidateBindingConsistency(persistedBinding, tenantId, endpoint);
            persistedBinding.MarkResolved(timestamp, entry.DisplayName);
            ApplyAudit(persistedBinding, actorUserId, correlationId, timestamp);
            return (persistedBinding, false);
        }
    }

    private static void ValidateBindingConsistency(
        ExternalIdentityBinding binding,
        Guid tenantId,
        ConversationChannelEndpoint endpoint)
    {
        if (binding.TenantId != tenantId)
        {
            throw new InvalidOperationException("Resolved identity binding tenant does not match inbound endpoint tenant.");
        }

        if (binding.ConversationChannelEndpointId != endpoint.Id)
        {
            throw new InvalidOperationException("Resolved identity binding endpoint does not match inbound endpoint.");
        }

        if (!string.Equals(binding.Channel, endpoint.Channel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved identity binding channel does not match inbound endpoint channel.");
        }
    }

    private static string NormalizeExternalUserId(string externalUserId)
    {
        return new string(externalUserId
            .Trim()
            .Where(static ch => char.IsLetterOrDigit(ch))
            .Select(static ch => char.ToLowerInvariant(ch))
            .ToArray());
    }

    private static string BuildBindingMetadataJson(
        ConversationChannelEndpoint endpoint,
        string normalizedExternalUserId)
    {
        return JsonSerializer.Serialize(new
        {
            source = "webhook-inbound",
            endpointId = endpoint.Id,
            channel = endpoint.Channel,
            normalizedExternalUserId
        });
    }

    private static void ApplyAudit(
        ExternalIdentityBinding binding,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset timestamp)
    {
        binding.ModifiedAt = timestamp;
        binding.ModifiedByUid = actorUserId;
        binding.CorrelationId = correlationId;

        if (binding.CreatedAt == default)
        {
            binding.CreatedAt = timestamp;
        }

        if (binding.CreatedByUid == Guid.Empty)
        {
            binding.CreatedByUid = actorUserId;
        }
    }
}
