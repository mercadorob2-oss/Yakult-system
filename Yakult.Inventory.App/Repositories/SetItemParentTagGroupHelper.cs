using System.Data.SqlClient;
using Dapper;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Shared find-or-create logic for dbo.SetItemParentTagGroup — every code path that
    /// inserts dbo.SetItem rows tagged with a Parent Tag (ServiceSetRepository's create/update
    /// paths, InvoiceRepository's Bulk Add and Convert-to-Group) needs to resolve the same
    /// group row instead of each maintaining its own copy of this SQL. Mirrors
    /// SetItemSubTypeGroupHelper, but simpler: Parent Tag is a free-text label only, with no
    /// date range and no financial override columns.
    /// </summary>
    public static class SetItemParentTagGroupHelper
    {
        /// <summary>
        /// Finds the dbo.SetItemParentTagGroup row matching this SetId+Label (trimmed,
        /// case-insensitive), or creates one. Returns null if label is blank. Must run on the
        /// same connection/transaction as the SetItem insert that will reference the returned
        /// ParentTagGroupId.
        /// </summary>
        public static int? FindOrCreateGroupId(
            SqlConnection connection, SqlTransaction transaction,
            int setId, string label, int? createdBy)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            label = label.Trim();

            var parameters = new
            {
                SetId = setId,
                Label = label,
                CreatedBy = createdBy
            };

            const string findSql = @"
                SELECT TOP 1 ParentTagGroupId FROM dbo.SetItemParentTagGroup
                WHERE SetId = @SetId AND Label = @Label";
            var existing = connection.QuerySingleOrDefault<int?>(findSql, parameters, transaction);
            if (existing.HasValue) return existing;

            const string insertSql = @"
                INSERT INTO dbo.SetItemParentTagGroup (SetId, Label, CreatedBy, CreatedAt)
                VALUES (@SetId, @Label, @CreatedBy, sysutcdatetime());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return connection.QuerySingle<int>(insertSql, parameters, transaction);
        }
    }
}
