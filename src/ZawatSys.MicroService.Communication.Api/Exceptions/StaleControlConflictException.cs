namespace ZawatSys.MicroService.Communication.Api.Exceptions;

/// <summary>
/// Raised when expected control version mismatches persisted version.
/// </summary>
public sealed class StaleControlConflictException : Exception
{
    public StaleControlConflictException(string message)
        : base(message)
    {
    }
}
