using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Repository interface for Request data access.
    /// Connects directly to the database.
    /// </summary>
    public interface IRequestRepository
    {
        int AddRequest(RequestDto request);
        RequestDto? GetRequestById(int reqId);
    }
}
