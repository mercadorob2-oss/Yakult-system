using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Models;

namespace Yakult.ITCM.Server.Services;

public interface IItcmAuthenticationService
{
    Task<ItcmAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}

public sealed class ItcmAuthenticationResult
{
    public bool Success { get; init; }
    public bool IsAdministrator { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public ItcmUserIdentity? User { get; init; }

    public static ItcmAuthenticationResult Failed(string reason) => new()
    {
        Success = false,
        FailureReason = reason
    };
}

/// <summary>
/// Authenticates against the same dbo.User/UserRole/Role identity source used by
/// the Systems Portal. ITCM deliberately issues its own cookie and does not
/// reuse the Portal cookie.
/// </summary>
public sealed class ItcmAuthenticationService : IItcmAuthenticationService
{
    private readonly IItcmRepository _repository;
    private readonly ILogger<ItcmAuthenticationService> _logger;

    public ItcmAuthenticationService(IItcmRepository repository, ILogger<ItcmAuthenticationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItcmAuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return ItcmAuthenticationResult.Failed("invalid-credentials");

        try
        {
            await using var connection = new SqlConnection(_repository.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Preserve compatibility with the existing Systems Portal identity
            // source while refusing inactive accounts.
            var user = await FindUserAsync(connection, username, password, legacyPassword: true, cancellationToken);
            if (user is null)
                user = await FindUserAsync(connection, username, password, legacyPassword: false, cancellationToken);

            if (user is null)
                return ItcmAuthenticationResult.Failed("invalid-credentials");

            var roles = await LoadRolesAsync(connection, user.UserId, cancellationToken);
            var identity = new ItcmUserIdentity
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                IsDeveloper = user.IsDeveloper,
                Roles = roles
            };

            // Developers administer ITCM; every other active account signs in
            // as a read-only viewer. The administrator policy is granted by
            // claim, never by the mere fact of being authenticated.
            if (!user.IsDeveloper)
                return new ItcmAuthenticationResult
                {
                    Success = true,
                    IsAdministrator = false,
                    User = identity
                };

            return new ItcmAuthenticationResult
            {
                Success = true,
                IsAdministrator = true,
                User = identity
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ITCM authentication failed for username {Username}", username);
            throw new InvalidOperationException("Authentication is temporarily unavailable.", ex);
        }
    }

    private static async Task<DatabaseUser?> FindUserAsync(
        SqlConnection connection,
        string username,
        string password,
        bool legacyPassword,
        CancellationToken cancellationToken)
    {
        var passwordPredicate = legacyPassword
            ? "AND u.[Password] = CONVERT(VARBINARY(MAX), @Password)"
            : "AND u.PasswordHash IS NOT NULL AND u.PasswordSalt IS NOT NULL";

        var sql = $@"
SELECT TOP (1)
    u.UserId,
    u.Name,
    ISNULL(u.EmailAddress, ''),
    ISNULL(u.IsDeveloper, 0),
    u.PasswordHash,
    u.PasswordSalt
FROM dbo.[User] u
WHERE u.Name = @Name
  AND ISNULL(u.IsActive, 1) = 1
  {passwordPredicate};";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", username);
        if (legacyPassword)
            command.Parameters.AddWithValue("@Password", password);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var user = new DatabaseUser
        {
            UserId = reader.GetInt32(0),
            Name = reader.IsDBNull(1) ? username : reader.GetString(1),
            Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            IsDeveloper = reader.GetBoolean(3),
            PasswordHash = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4),
            PasswordSalt = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5)
        };

        if (!legacyPassword && (user.PasswordHash is null || user.PasswordSalt is null ||
            !VerifyPassword(password, user.PasswordHash, user.PasswordSalt)))
            return null;

        return user;
    }

    private static async Task<IReadOnlyList<string>> LoadRolesAsync(
        SqlConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT r.RoleName
FROM dbo.UserRole ur
INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
WHERE ur.UserId = @UserId AND r.IsActive = 1
ORDER BY r.RoleName;";

        var roles = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            roles.Add(reader.GetString(0));

        return roles;
    }

    private static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[passwordBytes.Length + storedSalt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Buffer.BlockCopy(storedSalt, 0, combined, passwordBytes.Length, storedSalt.Length);

        var computed = SHA256.HashData(combined);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }

    private sealed class DatabaseUser
    {
        public int UserId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool IsDeveloper { get; init; }
        public byte[]? PasswordHash { get; init; }
        public byte[]? PasswordSalt { get; init; }
    }
}
