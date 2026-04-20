using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZawatSys.MicroService.Communication.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddCommunicationApplication(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;

        var assembly = typeof(ApplicationServiceExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
