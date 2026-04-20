using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZawatSys.MicroLib.Communication.Extensions;
using ZawatSys.MicroService.Communication.Api.Exceptions;
using ZawatSys.MicroService.Communication.Api.Services;
using ZawatSys.MicroService.Communication.Application.Extensions;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Infrastructure.Extensions;

namespace ZawatSys.MicroService.Communication.Api.Extensions;

public static class StartupExtensions
{
    /// <summary>
    /// Registers Communication host dependencies.
    ///
    /// Licensing contract:
    /// This method fails fast during startup when LicenseContent is missing,
    /// so the service cannot boot in an unlicensed state.
    /// </summary>
    public static IServiceCollection AddCommunicationMicroHost(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Fail-fast license guard: no runtime boot without explicit license content.
        var licenseContent = configuration["LicenseContent"];
        if (string.IsNullOrWhiteSpace(licenseContent))
        {
            throw new InvalidOperationException("LicenseContent is missing in configuration");
        }

        services.AddCommunicationMicroLibWithLicenseContent(
            licenseContent,
            allowPlaceholderPublicKeyForDevelopment: environment.IsDevelopment(),
            publicKeyPemOverride: configuration["License:PublicKeyPem"]);

        services.AddCommunicationTenantContext();
        services.AddCommunicationApiConsistency();
        services.AddCommunicationInfrastructure(configuration);
        services.AddCommunicationApplication(configuration);

        return services;
    }

    /// <summary>
    /// Registers request tenant/current-user context services and middleware hook.
    /// Middleware is wired through IStartupFilter so Program.cs remains unchanged.
    /// </summary>
    public static IServiceCollection AddCommunicationTenantContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }

    /// <summary>
    /// Registers API consistency components (global exception mapping + envelope conventions).
    /// Middleware is wired through IStartupFilter so Program.cs remains unchanged.
    /// </summary>
    public static IServiceCollection AddCommunicationApiConsistency(this IServiceCollection services)
    {
        services.AddSingleton<IExceptionMapper, DeterministicExceptionMapper>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is missing");

        var jwtIssuer = configuration["Jwt:Issuer"] ?? "ZawatSys.Communication.Api";
        var jwtAudience = configuration["Jwt:Audience"] ?? "ZawatSys.Communication.Api";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ZawatSys Communication Microservice API",
                Version = "v1",
                Description = "Communication orchestration APIs"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });

            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static IServiceCollection AddHealthChecksWithDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck(
                "communication-api-live",
                () => HealthCheckResult.Healthy("Communication API process is running."),
                tags: ["live", "communication"])
            .AddNpgSql(
                GetRequiredConnectionString(configuration, "DefaultConnection"),
                name: "communication-db",
                tags: ["ready", "db", "postgres", "communication"])
            .AddRabbitMQ(
                sp =>
                {
                    var rabbitConfig = configuration.GetSection("RabbitMq");
                    var factory = new RabbitMQ.Client.ConnectionFactory
                    {
                        HostName = rabbitConfig["Host"]!,
                        Port = int.TryParse(rabbitConfig["Port"], out var port) ? port : 5672,
                        UserName = rabbitConfig["Username"]!,
                        Password = rabbitConfig["Password"]!,
                        VirtualHost = rabbitConfig["VirtualHost"] ?? "/"
                    };

                    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
                },
                name: "communication-rabbitmq",
                tags: ["ready", "messaging", "rabbitmq", "communication"]);

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string connectionName)
    {
        return configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is missing.");
    }
}
