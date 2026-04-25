using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZawatSys.MicroService.Communication.Application.Behaviors;

namespace ZawatSys.MicroService.Communication.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddCommunicationApplication(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;

        var assembly = typeof(ApplicationServiceExtensions).Assembly;

        try
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(assembly);
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });

            services.AddValidatorsFromAssembly(assembly);
        }
        catch (ReflectionTypeLoadException ex)
        {
            throw new InvalidOperationException(
                BuildTypeLoadErrorMessage(assembly, ex),
                ex);
        }

        return services;
    }

    private static string BuildTypeLoadErrorMessage(Assembly assembly, ReflectionTypeLoadException ex)
    {
        var messages = new List<string>
        {
            $"Failed to scan assembly '{assembly.FullName}' for MediatR handlers/validators."
        };

        for (var i = 0; i < ex.LoaderExceptions.Length; i++)
        {
            var loaderException = ex.LoaderExceptions[i];
            if (loaderException is null)
            {
                continue;
            }

            messages.Add($"LoaderException[{i}]: {loaderException.GetType().Name}: {loaderException.Message}");
        }

        return string.Join(Environment.NewLine, messages);
    }
}
