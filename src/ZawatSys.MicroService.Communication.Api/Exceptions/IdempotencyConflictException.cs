namespace ZawatSys.MicroService.Communication.Api.Exceptions;

/// <summary>
/// Raised when an idempotency key conflicts with an existing operation state.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }
}
