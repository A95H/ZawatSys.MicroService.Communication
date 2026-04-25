using ZawatSys.MicroService.Communication.Application.Control;
using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Infrastructure.Control;

internal sealed class CurrentUserBackedConversationStaffDirectory : IConversationStaffDirectory
{
    private static readonly string[] ExpStaffRoles = ["EXP_STAFF", "EXP Staff", "Supervisor", "SUPERVISOR", "Admin", "ADMIN"];

    private readonly ICurrentUserService _currentUserService;

    public CurrentUserBackedConversationStaffDirectory(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task<bool> IsActiveAuthorizedStaffAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var isCurrentTenant = _currentUserService.TenantId == tenantId;
        var isCurrentUser = _currentUserService.UserId == userId;
        var isExpStaff = _currentUserService.Roles.Any(role => ExpStaffRoles.Contains(role, StringComparer.Ordinal));

        return Task.FromResult(isCurrentTenant && isCurrentUser && isExpStaff);
    }
}
