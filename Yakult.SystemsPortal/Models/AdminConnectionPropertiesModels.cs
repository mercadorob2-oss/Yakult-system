namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Read-only connection properties shown on the admin "Database Connection" page.
/// Intentionally excludes credentials (user id / password) - only safe metadata
/// (server, database, encryption/timeout settings) plus a live connectivity
/// test result are exposed, even to developer-only admins.
/// </summary>
public sealed class AdminConnectionPropertiesViewModel
{
    public string ServerAddress { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; }
    public bool MultipleActiveResultSets { get; set; }
    public int ConnectTimeoutSeconds { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public ConnectionProfileStatusViewModel ProfileStatus { get; set; } = new();
    public string? CandidateConnectionString { get; set; }
}

public sealed class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public string? ServerVersion { get; set; }
}

/// <summary>
/// Passphrase setup/unlock form for the Database Connection page. The secret is
/// intentionally separate from portal user-account passwords.
/// </summary>
public sealed class ConnectionPageUnlockViewModel
{
    public string? Passphrase { get; set; }
    public string? ConfirmPassphrase { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsStorageAvailable { get; set; }
    public string? StorageMessage { get; set; }

    public bool IsSetupMode => IsStorageAvailable && !IsConfigured;
}

/// <summary>
/// Safe operational state for the Database Connection passphrase store.
/// </summary>
public sealed class ConnectionPageAccessState
{
    public bool IsStorageAvailable { get; set; }
    public bool IsConfigured { get; set; }
    public string? StorageMessage { get; set; }
}
