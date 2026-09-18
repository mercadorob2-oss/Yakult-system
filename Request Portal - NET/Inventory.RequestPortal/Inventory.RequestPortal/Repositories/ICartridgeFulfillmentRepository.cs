using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Fulfillment loop for the cartridge-exchange backlog (dbo.UnfulfilledCartridgeExchange).
    /// Only serves requests that already had a first-pass fulfillment attempt on the desktop
    /// Cartridge Management portal — see UnfulfilledCartridgeExchangeRepository.cs on desktop
    /// for the first-pass queue, which is out of scope for this web port.
    /// PORTED FROM: Yakult.Inventory.App/Repositories/UnfulfilledCartridgeExchangeRepository.cs
    /// and CartridgeManagementRepository.cs (stock lookup only).
    /// </summary>
    public interface ICartridgeFulfillmentRepository
    {
        Task<List<UnfulfilledCartridgeExchangeDto>> GetPurelyUnfulfilledExchangesAsync();
        Task<List<UnfulfilledCartridgeExchangeDto>> GetPartiallyFulfilledExchangesFullSetAsync();
        Task<List<UnfulfilledCartridgeExchangeDto>> GetFulfilledExchangesAsync();
        Task<List<UnfulfilledCartridgeExchangeDto>> GetAllExchangesAsync();
        Task FulfillExchangeAsync(int unfulfilledId, int additionalIssuedQty, int fulfilledBy, string? fulfilledRemarks);
        Task<int?> GetCartridgeModelIdByModelNumberAsync(string modelNumber);
        Task<int> GetAvailableIssuableStockAsync(int? cartridgeModelId);

        /// <summary>
        /// Same stock pool as GetAvailableIssuableStockAsync, split by condition — mirrors desktop's
        /// CartridgeManagementRepository.GetAvailableIssuableStockByCondition. condition is
        /// "Brand New" (Item.RefillStatus IS NULL) or "Refilled" (Item.RefillStatus = 'Available').
        /// </summary>
        Task<int> GetAvailableIssuableStockByConditionAsync(int? cartridgeModelId, string condition);

        /// <summary>Always scoped to Status='Pending', independent of any status filter — mirrors desktop's stat cards.</summary>
        Task<(int PendingCount, int TotalUnfulfilledQty)> GetPendingSummaryAsync();
        Task<List<(string Model, int PendingCount, int UnfulfilledQty)>> GetPendingSummaryByModelAsync();
    }
}
