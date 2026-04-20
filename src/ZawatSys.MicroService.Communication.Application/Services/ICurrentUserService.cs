namespace ZawatSys.MicroService.Communication.Application.Services;

/// <summary>
/// Provides current request user/tenant context for handlers and policies.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    Guid? OrganizationId { get; }

    bool IsAuthenticated { get; }

    Guid CorrelationId { get; }
}
