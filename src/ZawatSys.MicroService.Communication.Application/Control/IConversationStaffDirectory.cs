namespace ZawatSys.MicroService.Communication.Application.Control;

public interface IConversationStaffDirectory
{
    Task<bool> IsActiveAuthorizedStaffAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
