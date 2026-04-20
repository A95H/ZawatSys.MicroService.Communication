namespace ZawatSys.MicroService.Communication.Api.Services;

internal static class TenantContextConstants
{
    internal const string CorrelationIdHeader = "X-Correlation-ID";
    internal const string TenantIdHeader = "X-Tenant-ID";

    internal const string CorrelationIdItemKey = "CorrelationId";
    internal const string TenantIdItemKey = "TenantId";

    internal const string TenantIdClaimType = "tenant_id";
    internal const string TenantIdClaimTypeAlt = "TenantId";
    internal const string TenantIdClaimTypeAlt2 = "tenantId";
    internal const string OrganizationIdClaimType = "organization_id";
    internal const string OrganizationIdClaimTypeAlt = "OrganizationId";
}
