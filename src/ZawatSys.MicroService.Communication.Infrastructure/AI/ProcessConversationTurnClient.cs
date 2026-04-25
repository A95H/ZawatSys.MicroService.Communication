using MassTransit;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZawatSys.MicroLib.AI.Domain.Commands;
using ZawatSys.MicroLib.AI.Domain.IntegrationEvents;
using ZawatSys.MicroLib.Shared.Common.Enums.Core;
using ZawatSys.MicroLib.Shared.Contracts.Common;
using ZawatSys.MicroService.Communication.Application.AI;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Infrastructure.Observability;

namespace ZawatSys.MicroService.Communication.Infrastructure.AI;

public sealed class ProcessConversationTurnClient : IProcessConversationTurnClient
{
    internal const string SourceName = "ZawatSys.MicroService.Communication";
    private const string MeterName = "ZawatSys.MicroService.Communication.AI";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.AI";
    private const string RequestLatencyHistogramName = "communication.ai.request.latency";
    private const string RequestOutcomeCounterName = "communication.ai.request.outcomes";
    private const string RequestActivityName = "communication.ai.process_conversation_turn";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Histogram<double> RequestLatencyHistogram = Meter.CreateHistogram<double>(RequestLatencyHistogramName, unit: "ms");
    private static readonly Counter<long> RequestOutcomeCounter = Meter.CreateCounter<long>(RequestOutcomeCounterName);

    private readonly IProcessConversationTurnRequestTransport _transport;
    private readonly ICurrentUserService _currentUserService;
    private readonly ProcessConversationTurnClientOptions _options;
    private readonly ILogger<ProcessConversationTurnClient> _logger;

    public ProcessConversationTurnClient(
        IProcessConversationTurnRequestTransport transport,
        ICurrentUserService currentUserService,
        IOptions<ProcessConversationTurnClientOptions> options,
        ILogger<ProcessConversationTurnClient> logger)
    {
        _transport = transport;
        _currentUserService = currentUserService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent>> ProcessConversationTurnAsync(
        ProcessConversationTurnIntegrationCmd command,
        CancellationToken cancellationToken)
    {
        var timeout = ResolveTimeout();
        var request = BuildRequest(command);
        var maxAttempts = Math.Max(1, _options.RetryCount + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var activity = ActivitySource.StartActivity(RequestActivityName, ActivityKind.Client);
            activity?.SetTag("messaging.system", "masstransit");
            activity?.SetTag("ai.operation", "process_conversation_turn");
            activity?.SetTag("ai.attempt", attempt);
            activity?.SetTag("ai.max_attempts", maxAttempts);
            CommunicationObservability.SetCommonConversationTags(
                activity,
                command.TenantId,
                command.ConversationId,
                command.SessionId,
                request.CorrelationId);
            activity?.SetTag("message.id", command.ConversationMessageId);
            activity?.SetTag("control.expected_version", command.ExpectedControlVersion);

            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                _logger.LogInformation(
                    "Dispatching ProcessConversationTurn to AI. Attempt: {Attempt}/{MaxAttempts}, TenantId: {TenantId}, ConversationId: {ConversationId}, SessionId: {SessionId}, ConversationMessageId: {ConversationMessageId}, ExpectedControlVersion: {ExpectedControlVersion}, CorrelationId: {CorrelationId}",
                    attempt,
                    maxAttempts,
                    command.TenantId,
                    command.ConversationId,
                    command.SessionId,
                    command.ConversationMessageId,
                    command.ExpectedControlVersion,
                    request.CorrelationId);

                var response = await _transport.SendAsync(request, timeout, cancellationToken);
                EnsureSuccessfulResponse(request, response, command);

                RecordOutcome(command, response.Data!.Outcome, attempt, maxAttempts, "success", null, Stopwatch.GetElapsedTime(startedAt));
                activity?.SetTag("ai.outcome", response.Data!.Outcome);
                activity?.SetTag("ai.result", "success");

                _logger.LogInformation(
                    "ProcessConversationTurn completed successfully. ConversationMessageId: {ConversationMessageId}, CorrelationId: {CorrelationId}, Outcome: {Outcome}",
                    command.ConversationMessageId,
                    response.CorrelationId,
                    response.Data!.Outcome);

                return response;
            }
            catch (RequestTimeoutException ex) when (attempt < maxAttempts)
            {
                RecordOutcome(command, "TIMEOUT", attempt, maxAttempts, "retry", ex.GetType().Name, Stopwatch.GetElapsedTime(startedAt));
                activity?.SetTag("ai.result", "retry");
                activity?.SetTag("ai.error_type", ex.GetType().Name);
                await DelayBeforeRetryAsync(attempt, request, command, ex, cancellationToken);
            }
            catch (TimeoutException ex) when (attempt < maxAttempts)
            {
                RecordOutcome(command, "TIMEOUT", attempt, maxAttempts, "retry", ex.GetType().Name, Stopwatch.GetElapsedTime(startedAt));
                activity?.SetTag("ai.result", "retry");
                activity?.SetTag("ai.error_type", ex.GetType().Name);
                await DelayBeforeRetryAsync(attempt, request, command, ex, cancellationToken);
            }
            catch (Exception ex)
            {
                RecordOutcome(command, "FAILED", attempt, maxAttempts, attempt < maxAttempts ? "retry" : "failure", ex.GetType().Name, Stopwatch.GetElapsedTime(startedAt));
                activity?.SetTag("ai.result", attempt < maxAttempts ? "retry" : "failure");
                activity?.SetTag("ai.error_type", ex.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        throw new InvalidOperationException("ProcessConversationTurn retry loop completed without returning a response.");
    }

    private PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd> BuildRequest(ProcessConversationTurnIntegrationCmd command)
    {
        var correlationId = _currentUserService.CorrelationId == Guid.Empty
            ? Guid.NewGuid()
            : _currentUserService.CorrelationId;

        return new PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd>
        {
            Data = command,
            CorrelationId = correlationId,
            CausationId = command.ConversationMessageId,
            IdentityContextId = _currentUserService.UserId ?? Guid.Empty,
            TenantId = command.TenantId,
            IssuedBy = IssuedBy.Microservice,
            Source = SourceName
        };
    }

    private static void EnsureSuccessfulResponse(
        PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd> request,
        PlatformIntegrationEvent<ProcessConversationTurnIntegrationEvent> response,
        ProcessConversationTurnIntegrationCmd command)
    {
        if (response.CorrelationId != request.CorrelationId)
        {
            throw new InvalidOperationException(
                $"ProcessConversationTurn correlation mismatch. Expected {request.CorrelationId:D}, actual {response.CorrelationId:D}.");
        }

        if (!response.IsSuccess || response.Data is null)
        {
            var details = response.ErrorMessages is { Count: > 0 }
                ? string.Join("; ", response.ErrorMessages)
                : "Unknown AI integration error.";

            throw new InvalidOperationException(
                $"ProcessConversationTurn failed for conversation message '{command.ConversationMessageId:D}': {details}");
        }
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        PlatformIntegrationCommand<ProcessConversationTurnIntegrationCmd> request,
        ProcessConversationTurnIntegrationCmd command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            exception,
            "ProcessConversationTurn timed out. Retrying request with same idempotency key. Attempt: {Attempt}, ConversationMessageId: {ConversationMessageId}, ExpectedControlVersion: {ExpectedControlVersion}, CorrelationId: {CorrelationId}",
            attempt,
            command.ConversationMessageId,
            command.ExpectedControlVersion,
            request.CorrelationId);

        var delay = TimeSpan.FromMilliseconds(Math.Max(0, _options.RetryDelayMilliseconds));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    private TimeSpan ResolveTimeout()
    {
        var seconds = Math.Max(1, _options.TimeoutSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    private static void RecordOutcome(
        ProcessConversationTurnIntegrationCmd command,
        string outcome,
        int attempt,
        int maxAttempts,
        string result,
        string? errorType,
        TimeSpan duration)
    {
        var attemptKind = CommunicationObservability.GetAttemptKind(attempt);
        var terminal = attempt >= maxAttempts || string.Equals(result, "success", StringComparison.Ordinal);

        RequestLatencyHistogram.Record(
            CommunicationObservability.ToMilliseconds(duration),
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("attempt_kind", attemptKind),
            new KeyValuePair<string, object?>("terminal", terminal),
            new KeyValuePair<string, object?>("outcome", outcome));

        RequestOutcomeCounter.Add(
            1,
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("attempt_kind", attemptKind),
            new KeyValuePair<string, object?>("terminal", terminal),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("error_type", errorType ?? "none"),
            new KeyValuePair<string, object?>("expected_control_version", command.ExpectedControlVersion));
    }
}
