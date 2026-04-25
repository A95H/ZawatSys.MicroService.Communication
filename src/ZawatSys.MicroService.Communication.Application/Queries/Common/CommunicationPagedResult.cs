namespace ZawatSys.MicroService.Communication.Application.Queries.Common;

public sealed record CommunicationPagedResult<T>(
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyList<T> Items);
