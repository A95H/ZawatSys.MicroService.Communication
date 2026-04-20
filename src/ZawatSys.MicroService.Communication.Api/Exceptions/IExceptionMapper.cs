namespace ZawatSys.MicroService.Communication.Api.Exceptions;

public interface IExceptionMapper
{
    ExceptionMappingResult Map(Exception exception);
}
