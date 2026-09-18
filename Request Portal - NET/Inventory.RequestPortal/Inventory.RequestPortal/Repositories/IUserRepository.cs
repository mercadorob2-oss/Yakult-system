using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Repository interface for User authentication.
    /// </summary>
    public interface IUserRepository
    {
        Task<AuthResult> AuthenticateAsync(string username, string password);
        Task<UserAccountDto?> GetAccountAsync(int userId);
        Task<bool> UpdateEmailAsync(int userId, string email);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    }
}
