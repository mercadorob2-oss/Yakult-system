using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Provides the SQL Server connection string for database authentication.
/// </summary>
public interface IConnectionStringProvider
{
    string GetConnectionString();
    string GetCurrentDatabaseName();
    bool IsUsingLocalDatabase();

    /// <summary>
    /// Returns safe, non-secret connection metadata (server, database, encryption
    /// settings, timeout) for display on the admin "Database Connection" page.
    /// Never includes the user id or password.
    /// </summary>
    Yakult.SystemsPortal.Models.AdminConnectionPropertiesViewModel GetConnectionProperties();

    /// <summary>
    /// Opens a short-lived connection to verify connectivity and reports the
    /// result (success/failure, elapsed time, server version) without exposing
    /// credentials.
    /// </summary>
    Task<Yakult.SystemsPortal.Models.ConnectionTestResult> TestConnectionAsync(string? connectionString = null);

    void ApplyConnectionString(string connectionString, int? userId);
    Task<ConnectionTestResult> TestRollbackConnectionAsync();
    void RollbackConnection(int? userId);
}
