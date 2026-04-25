using ZawatSys.MicroService.Communication.Application.Services;

namespace ZawatSys.MicroService.Communication.Application.Control;

public static class ConversationControlAuthorization
{
    public const string PermissionTakeOverConversation = CommunicationPermissions.TakeOverConversation;
    public const string PermissionPauseAiConversation = CommunicationPermissions.PauseAiConversation;
    public const string PermissionResumeAiConversation = CommunicationPermissions.ResumeAiConversation;
    public const string PermissionReleaseConversation = CommunicationPermissions.ReleaseConversation;
    public const string PermissionReassignConversation = CommunicationPermissions.ReassignConversation;
    public const string PermissionResolveConversation = CommunicationPermissions.ResolveConversation;
    public const string PermissionReopenConversation = CommunicationPermissions.ReopenConversation;

    private static readonly string[] ExpStaffRoles = ["EXP_STAFF", "EXP Staff", "Supervisor", "SUPERVISOR", "Admin", "ADMIN"];
    private static readonly string[] SupervisorRoles = ["Supervisor", "SUPERVISOR", "Admin", "ADMIN"];
    private static readonly string[] BotRoles = ["BOT", "EXP_BOT", "SYSTEM_BOT"];

    public static bool CanTakeOver(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionTakeOverConversation)
               && (IsExpStaff(currentUser) || IsBot(currentUser));
    }

    public static bool CanPause(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionPauseAiConversation)
               && (IsExpStaff(currentUser) || IsBot(currentUser));
    }

    public static bool CanResume(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionResumeAiConversation)
               && (IsExpStaff(currentUser) || IsBot(currentUser));
    }

    public static bool CanAssignAnotherUser(ICurrentUserService currentUser)
    {
        return currentUser.Roles.Any(role => SupervisorRoles.Contains(role, StringComparer.Ordinal));
    }

    public static bool CanRelease(ICurrentUserService currentUser, Guid? currentAssigneeUserId, Guid actorUserId)
    {
        if (!currentUser.HasPermission(PermissionReleaseConversation))
        {
            return false;
        }

        if (CanAssignAnotherUser(currentUser))
        {
            return true;
        }

        return currentAssigneeUserId.HasValue && currentAssigneeUserId.Value == actorUserId && IsExpStaff(currentUser);
    }

    public static bool CanResolve(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionResolveConversation)
               && (IsExpStaff(currentUser) || IsBot(currentUser));
    }

    public static bool CanReopen(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionReopenConversation)
               && (IsExpStaff(currentUser)
                   || CanAssignAnotherUser(currentUser)
                   || IsBot(currentUser));
    }

    public static bool CanReassign(ICurrentUserService currentUser)
    {
        return currentUser.HasPermission(PermissionReassignConversation)
               && CanAssignAnotherUser(currentUser);
    }

    public static bool IsBot(ICurrentUserService currentUser)
    {
        return currentUser.Roles.Any(role => BotRoles.Contains(role, StringComparer.Ordinal));
    }

    public static bool IsExpStaff(ICurrentUserService currentUser)
    {
        return currentUser.Roles.Any(role => ExpStaffRoles.Contains(role, StringComparer.Ordinal));
    }
}
