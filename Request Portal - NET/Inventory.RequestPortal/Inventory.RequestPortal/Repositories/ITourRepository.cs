namespace Inventory.RequestPortal.Repositories
{
    public interface ITourRepository
    {
        Task<bool> HasCompletedAsync(int userId, string tourKey);
        Task MarkCompletedAsync(int userId, string tourKey);
    }
}
