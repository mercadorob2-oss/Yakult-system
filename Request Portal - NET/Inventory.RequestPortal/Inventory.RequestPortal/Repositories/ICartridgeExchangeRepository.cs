using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// The "first-pass" cartridge exchange queue — brand-new/approved portal requests that have
    /// never had a fulfillment attempt. Once attempted, a request permanently moves to the
    /// UnfulfilledCartridgeExchange backlog (see ICartridgeFulfillmentRepository) and never
    /// returns here. Also covers the read-only fulfilled-history view.
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// </summary>
    public interface ICartridgeExchangeRepository
    {
        Task<List<CartridgeRequestDto>> GetPendingCartridgeRequestsAsync();
        Task<int> GetAvailableIssuableStockByConditionAsync(int? cartridgeModelId, string condition);
        Task<List<int>> GetIssuableItemIdsByConditionAsync(int? cartridgeModelId, string condition, int quantity);
        Task<int> EnsureSharedSetForPortalCartridgeGroupAsync(int reqId, int userId);
        Task<int> FulfillCartridgeExchangeByConditionAsync(
            int reqId, int returnedQuantity,
            List<int> issuedBrandNewIds, List<int> issuedRefilledIds,
            int issuedBrandNewQty, int issuedRefilledQty,
            int userId, int? requestModelId, string? fulfillmentRemarks);

        Task<List<FulfilledCartridgeRowDto>> GetFulfilledCartridgeHistoryAsync();
        Task<FulfilledCartridgeDetailDto?> GetFulfilledCartridgeDetailAsync(int setId);

        /// <summary>
        /// Data-entry-error escape hatch: permanently deletes a cartridge Request row, reversing
        /// issued stock and returned-empty bookkeeping. Destructive — see desktop's
        /// ForceDeleteCartridgeRequest for the exact confirmation text this must be paired with.
        /// </summary>
        Task<(bool Success, string Message)> ForceDeleteCartridgeRequestAsync(int reqId, int userId);
    }
}
