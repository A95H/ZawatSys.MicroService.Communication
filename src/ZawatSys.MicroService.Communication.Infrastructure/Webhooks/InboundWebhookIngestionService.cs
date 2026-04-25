using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.Communication.Domain.Entities;
using ZawatSys.MicroService.Communication.Application.AI;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Application.Webhooks;
using ZawatSys.MicroService.Communication.Infrastructure.Data;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class InboundWebhookIngestionService : IInboundWebhookIngestionService
{
    private const int MaxSequenceAllocationRetries = 5;
    private const string MeterName = "ZawatSys.MicroService.Communication.Webhooks";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.Webhooks";
    private const string InboundAcceptedCounterName = "communication.inbound.accepted";
    private const string InboundReceivedCounterName = "communication.inbound.received";
    private const string InboundDedupCounterName = "communication.inbound.dedup.decisions";
    private const string InboundActivityName = "communication.webhook.ingest";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Counter<long> InboundReceivedCounter = Meter.CreateCounter<long>(InboundReceivedCounterName);
    private static readonly Counter<long> InboundAcceptedCounter = Meter.CreateCounter<long>(InboundAcceptedCounterName);
    private static readonly Counter<long> InboundDedupCounter = Meter.CreateCounter<long>(InboundDedupCounterName);

    private readonly CommunicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInboundIdentityBindingResolver _identityBindingResolver;
    private readonly IConversationRoutingGate _conversationRoutingGate;
    private readonly IProcessConversationTurnClient _processConversationTurnClient;
    private readonly IConversationTurnOutcomeApplier _conversationTurnOutcomeApplier;
    private readonly ILogger<InboundWebhookIngestionService> _logger;

    public InboundWebhookIngestionService(
        CommunicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IInboundIdentityBindingResolver identityBindingResolver,
        IConversationRoutingGate conversationRoutingGate,
        IProcessConversationTurnClient processConversationTurnClient,
        IConversationTurnOutcomeApplier conversationTurnOutcomeApplier,
        ILogger<InboundWebhookIngestionService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _identityBindingResolver = identityBindingResolver;
        _conversationRoutingGate = conversationRoutingGate;
        _processConversationTurnClient = processConversationTurnClient;
        _conversationTurnOutcomeApplier = conversationTurnOutcomeApplier;
        _logger = logger;
    }

    public async Task IngestAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken)
    {
        var traceId = _currentUserService.CorrelationId.ToString("D");
        var actorUserId = _currentUserService.UserId ?? Guid.Empty;
        var processedAt = DateTimeOffset.UtcNow;

        var endpoint = await ResolveEndpointAsync(webhook, cancellationToken);
        var tenantId = endpoint.TenantId;

        foreach (var entry in webhook.Entries.Where(static x => string.Equals(x.CanonicalType, "message", StringComparison.OrdinalIgnoreCase)))
        {
            InboundReceivedCounter.Add(
                1,
                new KeyValuePair<string, object?>("provider", webhook.Provider),
                new KeyValuePair<string, object?>("endpoint_key", webhook.EndpointKey),
                new KeyValuePair<string, object?>("canonical_type", entry.CanonicalType),
                new KeyValuePair<string, object?>("message_type", MapMessageKind(entry.MessageType)),
                new KeyValuePair<string, object?>("result", "received"));

            if (string.IsNullOrWhiteSpace(entry.ProviderMessageId))
            {
                throw new ArgumentException("Inbound provider message id is required for idempotent ingestion.");
            }

            var providerMessageId = entry.ProviderMessageId.Trim();
            var dedupKey = BuildDedupKey(webhook.Provider, webhook.EndpointKey, providerMessageId);
            using var activity = ActivitySource.StartActivity(InboundActivityName, ActivityKind.Consumer);
            activity?.SetTag("provider", webhook.Provider);
            activity?.SetTag("endpoint.key", webhook.EndpointKey);
            activity?.SetTag("webhook.event_type", webhook.EventType);
            activity?.SetTag("webhook.entry_type", entry.CanonicalType);
            activity?.SetTag("message.provider_id", providerMessageId);
            activity?.SetTag("message.kind", MapMessageKind(entry.MessageType));
            activity?.SetTag("dedup.key", dedupKey);
            activity?.SetTag("correlation.id", traceId);

            var isDuplicate = await _dbContext.ConversationMessages
                .AnyAsync(
                    x => x.TenantId == tenantId
                        && x.ConversationChannelEndpointId == endpoint.Id
                        && x.ProviderMessageId == providerMessageId
                        && !x.IsDeleted,
                    cancellationToken);

            if (isDuplicate)
            {
                RecordDedupDecision(webhook, endpoint.Channel, "duplicate-noop");
                activity?.SetTag("ingest.decision", "duplicate-noop");
                LogDedupDecision("duplicate-noop", webhook, providerMessageId, dedupKey, traceId);
                continue;
            }

            var (binding, bindingCreated) = await _identityBindingResolver.ResolveAsync(
                tenantId,
                endpoint,
                entry,
                actorUserId,
                _currentUserService.CorrelationId,
                processedAt,
                cancellationToken);

            var session = await EnsureSessionAsync(
                tenantId,
                endpoint,
                binding,
                actorUserId,
                _currentUserService.CorrelationId,
                processedAt,
                cancellationToken);

            var control = await EnsureControlAsync(
                tenantId,
                endpoint,
                binding,
                session,
                actorUserId,
                _currentUserService.CorrelationId,
                processedAt,
                cancellationToken);

            var occurredAt = entry.OccurredAtUtc ?? processedAt;
            var message = await PersistInboundMessageAsync(
                webhook,
                entry,
                tenantId,
                endpoint,
                binding,
                bindingCreated,
                session,
                providerMessageId,
                dedupKey,
                traceId,
                actorUserId,
                processedAt,
                occurredAt,
                cancellationToken);

            if (message is null)
            {
                RecordDedupDecision(webhook, endpoint.Channel, "duplicate-noop");
                activity?.SetTag("ingest.decision", "duplicate-noop");
                continue;
            }

            activity?.SetTag("tenant.id", tenantId);
            activity?.SetTag("conversation.id", session.Id);
            activity?.SetTag("session.id", session.Id);
            activity?.SetTag("control.mode", control.Mode);
            activity?.SetTag("session.status", session.SessionStatus);
            activity?.SetTag("message.id", message.Id);

            var allowAiDispatch = await _conversationRoutingGate.EvaluateAndRecordAsync(
                tenantId,
                session.Id,
                actorUserId,
                _currentUserService.CorrelationId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (allowAiDispatch)
            {
                await DispatchConversationTurnAsync(webhook, entry, session, control, message, cancellationToken);
            }

            InboundAcceptedCounter.Add(
                1,
                new KeyValuePair<string, object?>("provider", webhook.Provider),
                new KeyValuePair<string, object?>("endpoint_key", webhook.EndpointKey),
                new KeyValuePair<string, object?>("channel", endpoint.Channel),
                new KeyValuePair<string, object?>("message_kind", message.MessageKind),
                new KeyValuePair<string, object?>("control_mode", control.Mode),
                new KeyValuePair<string, object?>("session_status", session.SessionStatus));
            RecordDedupDecision(webhook, endpoint.Channel, "accepted");
            activity?.SetTag("ingest.decision", "accepted");
            activity?.SetTag("routing.allow_ai_dispatch", allowAiDispatch);
            LogDedupDecision("accepted", webhook, providerMessageId, dedupKey, traceId);
        }
    }

    private async Task<ConversationChannelEndpoint> ResolveEndpointAsync(NormalizedProviderWebhook webhook, CancellationToken cancellationToken)
    {
        var matchingEndpoints = await _dbContext.ConversationChannelEndpoints
            .Where(x => x.EndpointKey == webhook.EndpointKey
                && x.InboundEnabled
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var providerMatches = matchingEndpoints
            .Where(x => string.Equals(x.Provider, webhook.Provider, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (providerMatches.Count == 0)
        {
            throw new KeyNotFoundException($"Inbound endpoint '{webhook.Provider}/{webhook.EndpointKey}' was not found.");
        }

        var currentTenantId = _currentUserService.TenantId;
        if (currentTenantId.HasValue)
        {
            return providerMatches.SingleOrDefault(x => x.TenantId == currentTenantId.Value)
                ?? throw new KeyNotFoundException(
                    $"Inbound endpoint '{webhook.Provider}/{webhook.EndpointKey}' was not found for tenant '{currentTenantId.Value:D}'.");
        }

        if (providerMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Inbound endpoint '{webhook.Provider}/{webhook.EndpointKey}' is ambiguous across tenants without tenant context.");
        }

        return providerMatches[0];
    }

    private async Task<ConversationSession> EnsureSessionAsync(
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        ExternalIdentityBinding binding,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ValidateSessionConsistency(tenantId, endpoint, binding);

        await using var sessionLock = await InboundWebhookLockCoordinator.AcquireSessionAsync(
            tenantId,
            endpoint.Id,
            binding.Id,
            cancellationToken);

        var session = await _dbContext.ConversationSessions
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.ConversationChannelEndpointId == endpoint.Id
                    && x.ExternalIdentityBindingId == binding.Id
                    && x.SessionStatus == ConversationSession.StatusOpen
                    && !x.IsDeleted,
                cancellationToken);

        if (session is not null)
        {
            return session;
        }

        session = new ConversationSession(
            tenantId,
            endpoint.Id,
            binding.Id,
            endpoint.Channel,
            ConversationSession.StatusOpen,
            timestamp);

        session.CreatedAt = timestamp;
        session.ModifiedAt = timestamp;
        session.CreatedByUid = actorUserId;
        session.ModifiedByUid = actorUserId;
        session.CorrelationId = correlationId;

        _dbContext.ConversationSessions.Add(session);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(session).State = EntityState.Detached;

            session = await _dbContext.ConversationSessions
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId
                        && x.ConversationChannelEndpointId == endpoint.Id
                        && x.ExternalIdentityBindingId == binding.Id
                        && x.SessionStatus == ConversationSession.StatusOpen
                        && !x.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("Conversation session creation conflicted but no active session was found for the resolved binding.");
        }

        return session;
    }

    private async Task<ConversationControl> EnsureControlAsync(
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        ExternalIdentityBinding binding,
        ConversationSession session,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        await using var sessionLock = await InboundWebhookLockCoordinator.AcquireSessionAsync(
            tenantId,
            endpoint.Id,
            binding.Id,
            cancellationToken);

        var control = await _dbContext.ConversationControls
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.ConversationId == session.Id
                    && !x.IsDeleted,
                cancellationToken);

        if (control is not null)
        {
            return control;
        }

        control = new ConversationControl(
            tenantId,
            session.Id,
            ConversationControl.ModeAiActive);

        control.CreatedAt = timestamp;
        control.ModifiedAt = timestamp;
        control.CreatedByUid = actorUserId;
        control.ModifiedByUid = actorUserId;
        control.CorrelationId = correlationId;

        _dbContext.ConversationControls.Add(control);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(control).State = EntityState.Detached;

            control = await _dbContext.ConversationControls
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId
                        && x.ConversationId == session.Id
                        && !x.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("Conversation control creation conflicted but no control snapshot was found for the active session.");
        }

        return control;
    }

    private async Task<ConversationMessage?> PersistInboundMessageAsync(
        NormalizedProviderWebhook webhook,
        NormalizedProviderWebhookEntry entry,
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        ExternalIdentityBinding binding,
        bool bindingCreated,
        ConversationSession session,
        string providerMessageId,
        string dedupKey,
        string traceId,
        Guid actorUserId,
        DateTimeOffset processedAt,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var sessionLock = await InboundWebhookLockCoordinator.AcquireSessionAsync(
            tenantId,
            endpoint.Id,
            binding.Id,
            cancellationToken);

        for (var attempt = 1; attempt <= MaxSequenceAllocationRetries; attempt++)
        {
            await _dbContext.Entry(session).ReloadAsync(cancellationToken);

            if (await IsPersistedDuplicateAsync(tenantId, endpoint.Id, providerMessageId, cancellationToken))
            {
                LogDedupDecision("duplicate-noop", webhook, providerMessageId, dedupKey, traceId);
                return null;
            }

            var sequence = session.LastMessageSequence + 1;
            session.RecordInboundMessage(occurredAt, sequence);
            session.ModifiedAt = processedAt;
            session.ModifiedByUid = actorUserId;
            session.CorrelationId = _currentUserService.CorrelationId;

            var message = new ConversationMessage(
                tenantId,
                session.Id,
                endpoint.Id,
                sequence,
                endpoint.Channel,
                ConversationMessage.DirectionInbound,
                ConversationMessage.SenderTypeUser,
                MapMessageKind(entry.MessageType),
                occurredAt,
                senderDisplayName: entry.DisplayName,
                providerMessageId: providerMessageId,
                providerCorrelationKey: dedupKey,
                textNormalized: entry.MessageText,
                textRedacted: entry.MessageText,
                metadataJson: BuildMetadataJson(webhook, entry, dedupKey, binding, bindingCreated, session, sequence),
                processedAt: processedAt);

            message.CreatedAt = processedAt;
            message.ModifiedAt = processedAt;
            message.CreatedByUid = actorUserId;
            message.ModifiedByUid = actorUserId;
            message.CorrelationId = _currentUserService.CorrelationId;

            _dbContext.ConversationMessages.Add(message);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return message;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSequenceAllocationRetries)
            {
                _dbContext.Entry(message).State = EntityState.Detached;
                await _dbContext.Entry(session).ReloadAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(message).State = EntityState.Detached;

                if (await IsPersistedDuplicateAsync(tenantId, endpoint.Id, providerMessageId, cancellationToken))
                {
                    await _dbContext.Entry(session).ReloadAsync(cancellationToken);
                    LogDedupDecision("duplicate-noop", webhook, providerMessageId, dedupKey, traceId);
                    return null;
                }

                if (attempt == MaxSequenceAllocationRetries)
                {
                    throw;
                }

                await _dbContext.Entry(session).ReloadAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException("Inbound message sequence allocation exceeded retry limit.");
    }

    private async Task DispatchConversationTurnAsync(
        NormalizedProviderWebhook webhook,
        NormalizedProviderWebhookEntry entry,
        ConversationSession session,
        ConversationControl control,
        ConversationMessage message,
        CancellationToken cancellationToken)
    {
        var command = new ProcessConversationTurnIntegrationCmd
        {
            TenantId = session.TenantId,
            ConversationId = session.Id,
            SessionId = session.Id,
            ConversationMessageId = message.Id,
            ExpectedControlVersion = control.IntegrationVersion,
            UserMessage = new ProcessConversationTurnUserMessageIntegrationCmd
            {
                MessageType = message.MessageKind,
                Content = message.TextNormalized ?? message.TextRedacted ?? string.Empty,
                SentAt = message.OccurredAt,
                Metadata = new Dictionary<string, string>
                {
                    ["channel"] = message.Channel,
                    ["provider"] = webhook.Provider,
                    ["endpointKey"] = webhook.EndpointKey,
                    ["providerMessageId"] = entry.ProviderMessageId ?? string.Empty,
                    ["messageKind"] = message.MessageKind,
                    ["correlationId"] = _currentUserService.CorrelationId.ToString("D")
                }
            },
            ContextSnapshot = new ProcessConversationTurnContextSnapshotIntegrationCmd
            {
                Control = new ProcessConversationTurnControlSnapshotIntegrationCmd
                {
                    Mode = control.Mode,
                    State = session.SessionStatus,
                    Reason = control.PauseReason ?? control.HandoffReason ?? session.ResolutionCode ?? "DEFAULT",
                    ControlVersion = control.IntegrationVersion
                }
            }
        };

        try
        {
            var response = await _processConversationTurnClient.ProcessConversationTurnAsync(command, cancellationToken);
            await _conversationTurnOutcomeApplier.ApplyAsync(command, response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "ProcessConversationTurn dispatch failed after inbound message persistence. TenantId: {TenantId}, ConversationId: {ConversationId}, SessionId: {SessionId}, ConversationMessageId: {ConversationMessageId}, ExpectedControlVersion: {ExpectedControlVersion}, CorrelationId: {CorrelationId}",
                command.TenantId,
                command.ConversationId,
                command.SessionId,
                command.ConversationMessageId,
                command.ExpectedControlVersion,
                _currentUserService.CorrelationId);
        }
    }

    private static void ValidateSessionConsistency(
        Guid tenantId,
        ConversationChannelEndpoint endpoint,
        ExternalIdentityBinding binding)
    {
        if (binding.TenantId != tenantId)
        {
            throw new InvalidOperationException("Resolved binding tenant does not match session tenant.");
        }

        if (binding.ConversationChannelEndpointId != endpoint.Id)
        {
            throw new InvalidOperationException("Resolved binding endpoint does not match session endpoint.");
        }

        if (!string.Equals(binding.Channel, endpoint.Channel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved binding channel does not match session endpoint channel.");
        }
    }

    private void LogDedupDecision(
        string decision,
        NormalizedProviderWebhook webhook,
        string providerMessageId,
        string dedupKey,
        string traceId)
    {
        _logger.LogInformation(
            "Inbound webhook dedup decision {Decision}. Provider: {Provider}, EndpointKey: {EndpointKey}, ProviderMessageId: {ProviderMessageId}, DedupKey: {DedupKey}, TraceId: {TraceId}, RawPayloadSha256: {RawPayloadSha256}",
            decision,
            webhook.Provider,
            webhook.EndpointKey,
            providerMessageId,
            dedupKey,
            traceId,
            webhook.RawPayloadReference.Sha256);
    }

    private static void RecordDedupDecision(NormalizedProviderWebhook webhook, string channel, string decision)
    {
        InboundDedupCounter.Add(
            1,
            new KeyValuePair<string, object?>("provider", webhook.Provider),
            new KeyValuePair<string, object?>("endpoint_key", webhook.EndpointKey),
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("decision", decision));
    }

    private async Task<bool> IsPersistedDuplicateAsync(
        Guid tenantId,
        Guid endpointId,
        string providerMessageId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConversationMessages.AnyAsync(
            x => x.TenantId == tenantId
                && x.ConversationChannelEndpointId == endpointId
                && x.ProviderMessageId == providerMessageId
                && !x.IsDeleted,
            cancellationToken);
    }

    private static string BuildDedupKey(string provider, string endpointKey, string providerMessageId)
    {
        return $"{provider.Trim().ToLowerInvariant()}:{endpointKey.Trim()}:{providerMessageId.Trim()}";
    }

    private static string MapMessageKind(string? messageType)
    {
        return messageType?.Trim().ToLowerInvariant() switch
        {
            "button" or "interactive" => ConversationMessage.MessageKindButtonReply,
            "image" or "video" or "audio" or "document" or "sticker" => ConversationMessage.MessageKindMedia,
            _ => ConversationMessage.MessageKindText
        };
    }

    private static string BuildMetadataJson(
        NormalizedProviderWebhook webhook,
        NormalizedProviderWebhookEntry entry,
        string dedupKey,
        ExternalIdentityBinding binding,
        bool bindingCreated,
        ConversationSession session,
        long sequence)
    {
        return JsonSerializer.Serialize(new
        {
            sequence,
            dedupKey,
            provider = webhook.Provider,
            endpointKey = webhook.EndpointKey,
            rawPayloadSha256 = webhook.RawPayloadReference.Sha256,
            rawPayloadCharacterCount = webhook.RawPayloadReference.CharacterCount,
            rawPayloadPath = entry.RawPayloadPath,
            providerObject = webhook.ProviderObject,
            processingContext = new
            {
                identityResolution = new
                {
                    externalIdentityBindingId = binding.Id,
                    conversationSessionId = session.Id,
                    normalizedExternalUserId = binding.NormalizedExternalUserId,
                    resolution = bindingCreated ? "created" : "existing"
                }
            },
            ingestionDecision = "accepted"
        });
    }
}
