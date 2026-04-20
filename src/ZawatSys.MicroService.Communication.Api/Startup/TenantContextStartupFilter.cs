using ZawatSys.MicroService.Communication.Api.Middlewares;

namespace ZawatSys.MicroService.Communication.Api.Startup;

/// <summary>
/// Injects tenant context middleware without Program.cs changes.
/// </summary>
public sealed class TenantContextStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseTenantContext();
            next(app);
        };
    }
}
