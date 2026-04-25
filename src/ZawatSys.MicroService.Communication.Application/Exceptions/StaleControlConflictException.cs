namespace ZawatSys.MicroService.Communication.Application.Exceptions;

public sealed class StaleControlConflictException : Exception
{
    public StaleControlConflictException(string message)
        : base(message)
    {
    }
}
