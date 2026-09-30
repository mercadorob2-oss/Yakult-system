using Microsoft.Data.SqlClient;

namespace Yakult.Inventory.Gateway.Data;

/// <summary>
/// Opens connections for a GatewayEnvironment. Connection strings come from
/// appsettings.Local.json beside the exe (or environment variables), never
/// from a tracked file.
/// </summary>
public sealed class SqlConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(GatewayEnvironment environment, CancellationToken ct = default)
    {
        var con = new SqlConnection(environment.GatewayConnection);
        try
        {
            await con.OpenAsync(ct);
            return con;
        }
        catch
        {
            await con.DisposeAsync();
            throw;
        }
    }
}
