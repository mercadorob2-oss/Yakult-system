using Microsoft.Data.SqlClient;

namespace Yakult.Inventory.Gateway.Data;

/// <summary>
/// One database the gateway can sign users into (the desktop's database switcher
/// picks one by Name). Connection strings never leave the server except
/// ClientConnection, which the migration bridge hands to signed-in desktops.
/// </summary>
public sealed record GatewayEnvironment(
    string Name,
    string DisplayName,
    string GatewayConnection,
    string? ClientConnection,
    bool IsDefault)
{
    /// <summary>
    /// Same rule as the desktop's DatabaseSetupForm: YIMS_PROD is the dummy database,
    /// any Yakult_* name is production (its "_DEV" suffix is historical).
    /// </summary>
    public string Kind
    {
        get
        {
            string catalog;
            try { catalog = new SqlConnectionStringBuilder(GatewayConnection).InitialCatalog ?? ""; }
            catch (ArgumentException) { return "Unknown"; }

            if (catalog.Equals("YIMS_PROD", StringComparison.OrdinalIgnoreCase)) return "Test";
            if (catalog.StartsWith("Yakult", StringComparison.OrdinalIgnoreCase)) return "Production";
            return "Unknown";
        }
    }
}

/// <summary>
/// Reads the environments from configuration on every call, so edits to
/// appsettings.Local.json apply without a restart:
///
///   "ConnectionStrings": { "Yakult_Inventory_System": ..., "LegacyClient": ... }   → "Production" (default)
///   "Environments": { "Test": { "DisplayName": ..., "Yakult_Inventory_System": ..., "LegacyClient": ... } }
/// </summary>
public sealed class GatewayEnvironments
{
    public const string DefaultName = "Production";

    private readonly IConfiguration _configuration;

    public GatewayEnvironments(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IReadOnlyList<GatewayEnvironment> All()
    {
        var list = new List<GatewayEnvironment>();

        var defaultConnection = _configuration.GetConnectionString("Yakult_Inventory_System");
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            list.Add(new GatewayEnvironment(
                DefaultName,
                _configuration["Gateway:DefaultEnvironmentDisplayName"] ?? DefaultName,
                defaultConnection,
                _configuration.GetConnectionString("LegacyClient"),
                IsDefault: true));
        }

        foreach (var section in _configuration.GetSection("Environments").GetChildren())
        {
            var connection = section["Yakult_Inventory_System"];
            if (string.IsNullOrWhiteSpace(connection)) continue;
            if (list.Any(e => e.Name.Equals(section.Key, StringComparison.OrdinalIgnoreCase))) continue;

            list.Add(new GatewayEnvironment(
                section.Key,
                section["DisplayName"] ?? section.Key,
                connection,
                section["LegacyClient"],
                IsDefault: false));
        }

        return list;
    }

    /// <summary>Blank name = the default environment. Null when the name is unknown.</summary>
    public GatewayEnvironment? Find(string? name)
    {
        var all = All();
        return string.IsNullOrWhiteSpace(name)
            ? all.FirstOrDefault(e => e.IsDefault) ?? all.FirstOrDefault()
            : all.FirstOrDefault(e => e.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
