using System.Security.Claims;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Api.Services;

/// <summary>
/// Reads current user and tenant context from HTTP request scope.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private static readonly string[] RoleClaimTypes = [ClaimTypes.Role, "role", "roles"];
    private static readonly string[] PermissionClaimTypes = ["permission", "permissions", "scope", "scp"];

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => FindGuidClaim(
        ClaimTypes.NameIdentifier,
        "sub",
        "UserId",
        "user_id",
        "userid");

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var tenantFromItems = httpContext?.Items[TenantContextConstants.TenantIdItemKey]?.ToString();

            if (Guid.TryParse(tenantFromItems, out var tenantIdFromItems))
            {
                return tenantIdFromItems;
            }

            return FindGuidClaim(
                TenantContextConstants.TenantIdClaimType,
                TenantContextConstants.TenantIdClaimTypeAlt,
                TenantContextConstants.TenantIdClaimTypeAlt2);
        }
    }

    public Guid? OrganizationId => FindGuidClaim(
        TenantContextConstants.OrganizationIdClaimType,
        TenantContextConstants.OrganizationIdClaimTypeAlt,
        "organization_id");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public Guid CorrelationId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var correlationIdCandidate =
                httpContext?.Items[TenantContextConstants.CorrelationIdItemKey]?.ToString()
                ?? httpContext?.Items[TenantContextConstants.CorrelationIdHeader]?.ToString();

            if (Guid.TryParse(correlationIdCandidate, out var parsed))
            {
                return parsed;
            }

            var traceIdentifier = httpContext?.TraceIdentifier;
            return Guid.TryParse(traceIdentifier, out parsed) ? parsed : Guid.NewGuid();
        }
    }

    public IReadOnlyCollection<string> Roles => GetClaimValues(RoleClaimTypes);

    public IReadOnlyCollection<string> Permissions => GetClaimValues(PermissionClaimTypes);

    public bool IsInRole(string role)
    {
        return Roles.Contains(role, StringComparer.Ordinal);
    }

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.Ordinal);
    }

    private Guid? FindGuidClaim(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private IReadOnlyCollection<string> GetClaimValues(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return Array.Empty<string>();
        }

        return user.Claims
            .Where(claim => claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
