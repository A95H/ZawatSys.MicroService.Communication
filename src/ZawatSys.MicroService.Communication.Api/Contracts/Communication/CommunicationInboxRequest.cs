namespace ZawatSys.MicroService.Communication.Api.Contracts.Communication;

public sealed class CommunicationInboxRequest
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }
}
