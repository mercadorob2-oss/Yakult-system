using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Stores the small amount of bootstrap state that must remain available even
/// while the portal is switching databases. The file contents are encrypted by
/// ASP.NET Core Data Protection and written atomically.
/// </summary>
public interface IProtectedPortalStateStore
{
    ProtectedPortalState Load();
    void Save(ProtectedPortalState state);
}

public sealed class ProtectedPortalStateStore : IProtectedPortalStateStore
{
    private readonly string _statePath;
    private readonly IDataProtector _protector;
    private readonly ILogger<ProtectedPortalStateStore> _logger;
    private readonly object _sync = new();

    public ProtectedPortalStateStore(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ProtectedPortalStateStore> logger)
    {
        var stateDirectory = PortalRuntimeStorage.Resolve(configuration, environment);
        Directory.CreateDirectory(stateDirectory);
        _statePath = Path.Combine(stateDirectory, "portal-state.dat");
        _protector = dataProtectionProvider.CreateProtector("Yakult.SystemsPortal.ProtectedPortalState.v1");
        _logger = logger;
    }

    public ProtectedPortalState Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_statePath))
                return new ProtectedPortalState();

            try
            {
                var protectedPayload = File.ReadAllText(_statePath);
                if (string.IsNullOrWhiteSpace(protectedPayload))
                    return new ProtectedPortalState();

                var json = _protector.Unprotect(protectedPayload);
                return JsonSerializer.Deserialize<ProtectedPortalState>(json) ?? new ProtectedPortalState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to read the encrypted portal state file.");
                return new ProtectedPortalState();
            }
        }
    }

    public void Save(ProtectedPortalState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = false });
            var protectedPayload = _protector.Protect(json);
            var tempPath = _statePath + ".tmp";
            File.WriteAllText(tempPath, protectedPayload);

            if (File.Exists(_statePath))
                File.Replace(tempPath, _statePath, null);
            else
                File.Move(tempPath, _statePath);
        }
    }
}
