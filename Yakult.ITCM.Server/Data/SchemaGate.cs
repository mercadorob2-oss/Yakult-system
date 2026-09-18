using Microsoft.Data.SqlClient;

namespace Yakult.ITCM.Server.Data;

public sealed class SchemaGate
{
    public static async Task<bool> TableExistsAsync(SqlConnection connection, string schemaAndTable, SqlTransaction? tx = null)
    {
        var parts = schemaAndTable.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = @Schema AND t.name = @Table) THEN 1 ELSE 0 END AS bit);";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        if (tx != null) cmd.Transaction = tx;
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    public static async Task<bool> ViewExistsAsync(SqlConnection connection, string schemaAndView)
    {
        var parts = schemaAndView.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var view = parts.Length > 1 ? parts[1] : parts[0];

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.views v JOIN sys.schemas s ON s.schema_id = v.schema_id WHERE s.name = @Schema AND v.name = @View) THEN 1 ELSE 0 END AS bit);";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@View", view);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    public static async Task<bool> ColumnExistsAsync(SqlConnection connection, string schemaAndTable, string columnName, SqlTransaction? tx = null)
    {
        var parts = schemaAndTable.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = @Schema AND t.name = @Table AND c.name = @Column
) THEN 1 ELSE 0 END AS bit);";
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        cmd.Parameters.AddWithValue("@Column", columnName);
        if (tx != null) cmd.Transaction = tx;
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    public static async Task<bool> TableHasColumnAsync(SqlConnection connection, string schemaAndTable, string columnName, SqlTransaction? tx = null)
    {
        return await ColumnExistsAsync(connection, schemaAndTable, columnName, tx);
    }
}
