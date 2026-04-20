using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Api.Contracts;

/// <summary>
/// Standard API error payload envelope that remains compatible with InternalResponse conventions.
/// </summary>
public sealed class ApiErrorEnvelope
{
    public bool IsSuccess { get; init; }

    public string MsgCode { get; init; } = string.Empty;

    public object? Data { get; init; }

    public InternalError Error { get; init; } = new();
}
