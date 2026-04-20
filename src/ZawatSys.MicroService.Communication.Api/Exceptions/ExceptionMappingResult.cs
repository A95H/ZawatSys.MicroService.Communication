namespace ZawatSys.MicroService.Communication.Api.Exceptions;

/// <summary>
/// Deterministic exception mapping result.
/// </summary>
public sealed record ExceptionMappingResult(
    int StatusCode,
    string MsgCode,
    string Title,
    string Details,
    IReadOnlyList<string> Errors
);
