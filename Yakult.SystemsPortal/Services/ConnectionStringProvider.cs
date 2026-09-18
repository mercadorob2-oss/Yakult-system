using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Resolves and switches the active portal database connection. An encrypted
/// local profile takes precedence over appsettings, allowing a successful
/// switch to survive process restarts without exposing credentials in HTML.
/// </summary>
public sealed class ConnectionStringProvider : IConnectionStringProvider
{
    private readonly IConfiguration _configuration;
    private readonly IProtectedPortalStateStore _stateStore;

    public ConnectionStringProvider(
        IConfiguration configuration,
        IProtectedPortalStateStore stateStore)
    {
        _configuration = configuration;
        _stateStore = stateStore;
    }

    public string GetConnectionString() => GetConnectionStringOrNull()
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not configured.");

    public string GetCurrentDatabaseName()
    {
        var builder = new SqlConnectionStringBuilder(GetConnectionString());
        return builder.InitialCatalog;
    }

    public bool IsUsingLocalDatabase() => false;

    public AdminConnectionPropertiesViewModel GetConnectionProperties()
    {
        var state = _stateStore.Load();
        var raw = GetConnectionStringOrNull();
        var profile = state.ConnectionProfile;
        var profileStatus = new ConnectionProfileStatusViewModel
        {
            HasEncryptedProfile = profile != null,
            HasRollbackProfile = !string.IsNullOrWhiteSpace(profile?.PreviousConnectionString),
            Source = profile == null ? "Application configuration" : "Encrypted active profile",
            UpdatedAtUtc = profile?.UpdatedAtUtc
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AdminConnectionPropertiesViewModel
            {
                IsConfigured = false,
                ProfileStatus = profileStatus
            };
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(raw);
            return new AdminConnectionPropertiesViewModel
            {
                IsConfigured = true,
                ServerAddress = builder.DataSource,
                DatabaseName = builder.InitialCatalog,
                Encrypt = builder.Encrypt,
                TrustServerCertificate = builder.TrustServerCertificate,
                MultipleActiveResultSets = builder.MultipleActiveResultSets,
                ConnectTimeoutSeconds = builder.ConnectTimeout,
                ApplicationName = builder.ApplicationName,
                ProfileStatus = profileStatus
            };
        }
        catch (ArgumentException)
        {
            profileStatus.Source = "Invalid connection configuration";
            return new AdminConnectionPropertiesViewModel
            {
                IsConfigured = true,
                ProfileStatus = profileStatus
            };
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string? connectionString = null)
    {
        var raw = string.IsNullOrWhiteSpace(connectionString)
            ? GetConnectionStringOrNull()
            : connectionString.Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return new ConnectionTestResult { Success = false, Message = "No connection string configured." };

        try
        {
            _ = new SqlConnectionStringBuilder(raw);
        }
        catch (ArgumentException ex)
        {
            return new ConnectionTestResult { Success = false, Message = $"Invalid connection string: {ex.Message}" };
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var connection = new SqlConnection(raw);
            await connection.OpenAsync();
            stopwatch.Stop();
            return new ConnectionTestResult
            {
                Success = true,
                Message = "Connected successfully.",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ServerVersion = connection.ServerVersion
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ConnectionTestResult
            {
                Success = false,
                Message = ex.Message,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public void ApplyConnectionString(string connectionString, int? userId)
    {
        var candidate = NormalizeAndValidate(connectionString);
        var state = _stateStore.Load();
        var current = GetConnectionStringOrNull();

        state.ConnectionProfile = new ConnectionProfileState
        {
            ConnectionString = candidate,
            PreviousConnectionString = string.IsNullOrWhiteSpace(current) ? null : current,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = userId
        };
        _stateStore.Save(state);
    }

    public async Task<ConnectionTestResult> TestRollbackConnectionAsync()
    {
        var previous = _stateStore.Load().ConnectionProfile?.PreviousConnectionString;
        return string.IsNullOrWhiteSpace(previous)
            ? new ConnectionTestResult { Success = false, Message = "No rollback connection profile is available." }
            : await TestConnectionAsync(previous);
    }

    public void RollbackConnection(int? userId)
    {
        var state = _stateStore.Load();
        var profile = state.ConnectionProfile;
        if (profile == null || string.IsNullOrWhiteSpace(profile.PreviousConnectionString))
            throw new InvalidOperationException("No rollback connection profile is available.");

        profile.ConnectionString = NormalizeAndValidate(profile.PreviousConnectionString);
        profile.PreviousConnectionString = null;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.UpdatedByUserId = userId;
        _stateStore.Save(state);
    }

    private string? GetConnectionStringOrNull()
    {
        var profile = _stateStore.Load().ConnectionProfile;
        if (!string.IsNullOrWhiteSpace(profile?.ConnectionString))
            return profile.ConnectionString;

        var configured = _configuration.GetConnectionString("DefaultConnection");
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private static string NormalizeAndValidate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        var builder = new SqlConnectionStringBuilder(connectionString.Trim());
        if (string.IsNullOrWhiteSpace(builder.DataSource))
            throw new ArgumentException("Connection string must specify a server/data source.", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new ArgumentException("Connection string must specify a database/catalog.", nameof(connectionString));

        return builder.ConnectionString;
    }
}
