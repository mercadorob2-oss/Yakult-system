namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Pure SQL-fragment builders for the optional Employee.DistributorId link.
    /// Older databases predate the column, so every read/write stays conditional
    /// on a live COL_LENGTH check (same pattern as RequestRepository).
    /// No database access here — callers pass the already-detected flag in.
    /// </summary>
    public static class EmployeeDistributorHelper
    {
        public struct InsertFragments
        {
            public readonly string ColumnFragment;
            public readonly string ValueFragment;

            public InsertFragments(string columnFragment, string valueFragment)
            {
                ColumnFragment = columnFragment;
                ValueFragment = valueFragment;
            }
        }

        public static string BuildDistributorSelectClause(bool hasColumn)
        {
            return hasColumn
                ? "dist.DistributorId,\n                    dist.Name AS DistributorName,"
                : "CAST(NULL AS INT) AS DistributorId,\n                    CAST(NULL AS NVARCHAR(200)) AS DistributorName,";
        }

        public static string BuildDistributorJoinClause(bool hasColumn)
        {
            return hasColumn
                ? "LEFT JOIN dbo.Distributor dist ON dist.DistributorId = e.DistributorId"
                : string.Empty;
        }

        public static InsertFragments BuildDistributorInsertFragments(bool hasColumn)
        {
            return hasColumn
                ? new InsertFragments("\n                         DistributorId,", "\n                         @DistributorId,")
                : new InsertFragments(string.Empty, string.Empty);
        }

        public static string BuildDistributorUpdateSetClause(bool hasColumn)
        {
            return hasColumn ? "DistributorId = @DistributorId," : string.Empty;
        }

        /// <summary>
        /// Picker "None" (Id 0) and garbage map to NULL: only a selected few
        /// employees carry a distributor, everyone else stays NULL.
        /// </summary>
        public static int? NormalizeDistributorId(int? pickedId)
        {
            return pickedId.HasValue && pickedId.Value > 0 ? pickedId : null;
        }
    }
}
