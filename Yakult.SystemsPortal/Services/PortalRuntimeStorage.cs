using Microsoft.Extensions.Configuration;

namespace Yakult.SystemsPortal.Services;

/// <summary>
/// Resolves a writable machine-local directory for encrypted runtime state and
/// Data Protection keys. It prefers the configured/production location, but falls
/// back to a writable temporary location instead of preventing the ASP.NET app
/// from starting when IIS permissions have not been prepared yet.
/// </summary>
public static class PortalRuntimeStorage
{
    public static string Resolve(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Portal:RuntimeStatePath"];
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(configured.Trim());

        var machineRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(machineRoot))
            candidates.Add(Path.Combine(machineRoot, "Yakult", "SystemsPortal"));

        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(userRoot))
            candidates.Add(Path.Combine(userRoot, "Yakult", "SystemsPortal"));

        candidates.Add(Path.Combine(Path.GetTempPath(), "Yakult.SystemsPortal"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryPrepareWritableDirectory(candidate))
                return candidate;
        }

        // The temp directory should always be writable. Keep a deterministic path
        // so the app remains startable even if the probe itself is restricted.
        return Path.Combine(Path.GetTempPath(), "Yakult.SystemsPortal");
    }

    private static bool TryPrepareWritableDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
