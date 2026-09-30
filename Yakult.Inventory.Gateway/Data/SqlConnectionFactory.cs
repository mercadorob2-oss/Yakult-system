using Microsoft.Data.SqlClient;

namespace Yakult.Inventory.Gateway.Data;

/// <summary>
/// The only place the gateway reads its connection string. It comes from
/// appsettings.Local.json beside the exe (or an environment variable), never
/// from a tracked file.
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    private string? ConnectionString => _configuration.GetConnectionString("Yakult_Inventory_System");

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var cs = ConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'Yakult_Inventory_System' is not configured. Set it in appsettings.Local.json.");

        var con = new SqlConnection(cs);
        await con.OpenAsync(ct);
        return con;
    }
}
