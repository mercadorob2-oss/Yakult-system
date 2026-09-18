using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

/// <summary>
/// Repository interface for user authentication.
/// </summary>
public interface IUserRepository
{
    Task<AuthResult> AuthenticateAsync(string username, string password);
}
