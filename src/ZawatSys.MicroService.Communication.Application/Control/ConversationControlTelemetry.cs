using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZawatSys.MicroLib.Communication.Domain.Entities;

namespace ZawatSys.MicroService.Communication.Application.Control;

internal static class ConversationControlTelemetry
{
    private const string MeterName = "ZawatSys.MicroService.Communication.Control";
    private const string ActivitySourceName = "ZawatSys.MicroService.Communication.Control";
    private const string TransitionCounterName = "communication.control.transitions";
    private const string TransitionActivityName = "communication.control.transition";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Counter<long> TransitionCounter = Meter.CreateCounter<long>(TransitionCounterName);

    public static void RecordTransition(
        string commandName,
        ConversationSession session,
        ConversationControl control,
        ConversationControlTransition transition,
        Guid correlationId)
    {
        using var activity = ActivitySource.StartActivity(TransitionActivityName, ActivityKind.Internal);
        activity?.SetTag("communication.command", commandName);
        activity?.SetTag("tenant.id", transition.TenantId);
        activity?.SetTag("conversation.id", transition.ConversationId);
        activity?.SetTag("correlation.id", correlationId.ToString("D"));
        activity?.SetTag("control.previous_mode", transition.PreviousMode ?? "none");
        activity?.SetTag("control.new_mode", transition.NewMode);
        activity?.SetTag("control.transition_reason", transition.TransitionReason);
        activity?.SetTag("control.triggered_by_type", transition.TriggeredByType);
        activity?.SetTag("control.version", transition.ControlVersion);
        activity?.SetTag("session.status", session.SessionStatus);
        activity?.SetTag("session.resolution_code", session.ResolutionCode);
        activity?.SetTag("control.mode", control.Mode);

        TransitionCounter.Add(
            1,
            new KeyValuePair<string, object?>("command", commandName),
            new KeyValuePair<string, object?>("previous_mode", transition.PreviousMode ?? "none"),
            new KeyValuePair<string, object?>("new_mode", transition.NewMode),
            new KeyValuePair<string, object?>("session_status", session.SessionStatus),
            new KeyValuePair<string, object?>("transition_reason", transition.TransitionReason),
            new KeyValuePair<string, object?>("triggered_by_type", transition.TriggeredByType));
    }
}
