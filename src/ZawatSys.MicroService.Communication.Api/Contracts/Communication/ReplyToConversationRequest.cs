namespace ZawatSys.MicroService.Communication.Api.Contracts.Communication;

public sealed class ReplyToConversationRequest
{
    public string? Content { get; set; }

    public Guid? ReplyToMessageId { get; set; }
}
