namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Decrypted in-memory representation of the encrypted bootstrap state file.
/// This type is never sent to a browser.
/// </summary>
public sealed class ProtectedPortalState
{
    public ConnectionProfileState? ConnectionProfile { get; set; }
    public ConnectionPassphraseState? ConnectionPassphrase { get; set; }
}

public sealed class ConnectionProfileState
{
    public string ConnectionString { get; set; } = string.Empty;
    public string? PreviousConnectionString { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public sealed class ConnectionPassphraseState
{
    public int Version { get; set; } = 1;
    public int Iterations { get; set; }
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

public sealed class ConnectionProfileStatusViewModel
{
    public bool HasEncryptedProfile { get; set; }
    public bool HasRollbackProfile { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class AdminConnectionEditorViewModel
{
    public AdminConnectionPropertiesViewModel Properties { get; set; } = new();
    public ConnectionProfileStatusViewModel Profile { get; set; } = new();
}

public sealed class ConnectionProfileRequest
{
    public string ConnectionString { get; set; } = string.Empty;
}
