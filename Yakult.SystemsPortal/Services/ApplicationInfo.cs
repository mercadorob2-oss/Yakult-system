using System.Reflection;

namespace Yakult.SystemsPortal.Services;

public sealed class ApplicationInfo
{
    public string Version { get; }

    public string FullVersion { get; }

    public DateTime DeployedAt { get; }

    public string AssemblyLocation { get; }

    public ApplicationInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var assemblyVersion = assembly.GetName().Version?.ToString();

        FullVersion = !string.IsNullOrWhiteSpace(infoVersion) ? infoVersion
                    : !string.IsNullOrWhiteSpace(assemblyVersion) ? assemblyVersion
                    : "0.0.0";

        var plusIndex = FullVersion.IndexOf('+');
        Version = plusIndex > 0 ? FullVersion[..plusIndex] : FullVersion;

        AssemblyLocation = assembly.Location;
        DeployedAt = !string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location)
            ? File.GetLastWriteTime(assembly.Location)
            : DateTime.UtcNow;
    }
}
