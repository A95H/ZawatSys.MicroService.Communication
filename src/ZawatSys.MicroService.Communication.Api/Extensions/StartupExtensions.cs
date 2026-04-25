using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZawatSys.MicroService.Communication.Api.HealthChecks;
using ZawatSys.MicroLib.Communication.Extensions;
using ZawatSys.MicroService.Communication.Api.Exceptions;
using ZawatSys.MicroService.Communication.Api.Routing;
using ZawatSys.MicroService.Communication.Api.Services;
using ZawatSys.MicroService.Communication.Api.Services.Webhooks;
using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Extensions;
using ZawatSys.MicroService.Communication.Application.Services;
using ZawatSys.MicroService.Communication.Infrastructure.Extensions;

namespace ZawatSys.MicroService.Communication.Api.Extensions;

public static class StartupExtensions
{
    private static readonly string[] PermissionClaimTypes = ["permission", "permissions", "scope", "scp"];

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
        services.AddSingleton<IProviderWebhookRequestAuthenticator, MetaProviderWebhookRequestAuthenticator>();
        services.AddSingleton<IProviderWebhookRequestAuthenticator, TelegramProviderWebhookRequestAuthenticator>();
        services.AddSingleton<IProviderWebhookAuthorizationService, ProviderWebhookAuthorizationService>();
        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap[ProviderWebhookRouting.RouteConstraintName] = typeof(SupportedWebhookProviderRouteConstraint);
        });

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

        services.AddAuthorization(options =>
        {
            foreach (var permission in CommunicationPermissions.GetAll())
            {
                options.AddPolicy(permission, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context => HasPermission(context.User, permission));
                });
            }
        });

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
        services.Configure<CommunicationDependencyHealthCheckOptions>(
            configuration.GetSection(CommunicationDependencyHealthCheckOptions.SectionName));

        var dependencyTimeout = configuration
            .GetSection(CommunicationDependencyHealthCheckOptions.SectionName)
            .GetValue<int?>(nameof(CommunicationDependencyHealthCheckOptions.DependencyTimeoutSeconds));

        var timeout = TimeSpan.FromSeconds(Math.Max(1, dependencyTimeout ?? 5));

        services.AddHealthChecks()
            .AddCheck(
                "communication-api-live",
                () => HealthCheckResult.Healthy("Communication API process is running."),
                tags: ["live", "communication"])
            .AddCheck<CommunicationDatabaseHealthCheck>(
                name: "communication-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db", "postgres", "communication"],
                timeout: timeout)
            .AddCheck<CommunicationBrokerHealthCheck>(
                name: "communication-rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "messaging", "rabbitmq", "communication"],
                timeout: timeout)
            .AddCheck<CommunicationSecretProviderHealthCheck>(
                name: "communication-secret-provider",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "security", "secrets", "communication"],
                timeout: timeout);

        return services;
    }

    public static IEndpointRouteBuilder MapCommunicationHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", CreateHealthCheckOptions(tag: "live", includeDurations: false));
        endpoints.MapHealthChecks("/health/live", CreateHealthCheckOptions(tag: "live", includeDurations: false));
        endpoints.MapHealthChecks("/health/ready", CreateHealthCheckOptions(tag: "ready", includeDurations: true));

        return endpoints;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string connectionName)
    {
        return configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is missing.");
    }

    private static HealthCheckOptions CreateHealthCheckOptions(string tag, bool includeDurations)
    {
        return new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase),
            AllowCachingResponses = false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = (context, report) => WriteHealthResponseAsync(context, report, includeDurations)
        };
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report, bool includeDurations)
    {
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = includeDurations ? entry.Value.Duration.TotalMilliseconds : (double?)null
                }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds
        });

        return context.Response.WriteAsync(payload);
    }

    private static bool HasPermission(ClaimsPrincipal user, string requiredPermission)
    {
        return user.Claims
            .Where(claim => PermissionClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(requiredPermission, StringComparer.Ordinal);
    }
}
