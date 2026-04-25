namespace ZawatSys.MicroService.Communication.Api.Contracts.Communication;

public sealed class ReassignConversationRequest
{
    public long ExpectedControlVersion { get; set; }

    public Guid AssigneeUserId { get; set; }

    public string? AssignedQueueCode { get; set; }

    public string? ReasonCode { get; set; }
}
