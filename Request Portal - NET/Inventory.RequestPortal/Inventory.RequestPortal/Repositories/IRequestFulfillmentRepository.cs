using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Fulfillment loop for Ink / Toner / Print Head requests (dbo.Request, WorkflowType =
    /// 'RequestSetManagement'). Cartridge exchange requests are handled separately by
    /// ICartridgeFulfillmentRepository.
    /// PORTED FROM: Yakult.Inventory.App/Repositories/RequestRepository.cs
    /// (FulfillmentTrackedRequestsCte, GetUnfulfilledRequestsFullSet,
    /// GetPartiallyFulfilledRequestsFullSet, FulfillRequest, GetItemStockOnHand)
    /// </summary>
    public interface IRequestFulfillmentRepository
    {
        Task<List<RequestDto>> GetUnfulfilledRequestsFullSetAsync();
        Task<List<RequestDto>> GetPartiallyFulfilledRequestsFullSetAsync();
        Task FulfillRequestAsync(int reqId, int additionalIssuedQty, int modifiedByUserId, string? remarks);
        Task<int> GetItemStockOnHandAsync(int itemId);
    }
}
