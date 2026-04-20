using ZawatSys.MicroService.Communication.Api.Middlewares;

namespace ZawatSys.MicroService.Communication.Api.Startup;

/// <summary>
/// Injects global exception middleware without Program.cs changes.
/// </summary>
public sealed class ExceptionHandlingStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseGlobalExceptionHandler();
            next(app);
        };
    }
}
