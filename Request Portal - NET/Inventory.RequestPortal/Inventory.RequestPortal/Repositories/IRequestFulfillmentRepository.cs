using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Fulfillment loop for Ink / Toner / Print Head requests (dbo.Request, WorkflowType =
    /// 'RequestSetManagement'), plus the cartridge lines of mixed portal submissions (listed here,
    /// but issued through ICartridgeExchangeRepository.IssueMixedCartridgeLineAsync; FulfillRequestAsync
    /// refuses them). Cartridge-only exchange requests are handled by ICartridgeFulfillmentRepository.
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
