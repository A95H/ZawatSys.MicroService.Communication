namespace ZawatSys.MicroService.Communication.Api.Contracts.Communication;

public sealed class ConversationMessagesRequest
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
