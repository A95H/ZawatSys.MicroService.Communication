namespace ZawatSys.MicroService.Communication.Api.Contracts.Communication;

public sealed class ReasonedConversationActionRequest
{
    public long ExpectedControlVersion { get; set; }

    public string? ReasonCode { get; set; }
}
