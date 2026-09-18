using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// (GetFulfilledSetsForNotification / GetActiveEmployeesForReceiver / UpdateReceivedByForSession /
    /// UpdateReceivedByForRequest) — backs the "Send Notifications" page under Cartridge Management.
    /// </summary>
    public interface ISendNotificationsRepository
    {
        Task<List<FulfilledSetNotificationDto>> GetFulfilledSetsForNotificationAsync();
        Task<List<EmployeeOptionDto>> GetActiveEmployeesForReceiverAsync();
        Task UpdateReceivedByForSessionAsync(Guid submissionSessionId, int? receivedById, int modifiedByUserId);
        Task UpdateReceivedByForRequestAsync(int reqId, int? receivedById, int modifiedByUserId);
    }
}
