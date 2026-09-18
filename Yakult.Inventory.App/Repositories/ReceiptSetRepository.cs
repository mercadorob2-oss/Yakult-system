using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    public class ReceiptSetRepository
    {
        private static bool? _hasRenewedFromReceiptSetIdColumn;

        public sealed class RenewalSummary
        {
            public int TotalItems { get; set; }
            public int RenewedItems { get; set; }
        }

        public sealed class CoveragePeriod
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ReceiptSetRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        private bool HasRenewedFromReceiptSetIdColumn()
        {
            if (_hasRenewedFromReceiptSetIdColumn.HasValue)
                return _hasRenewedFromReceiptSetIdColumn.Value;

            const string sql = "SELECT CASE WHEN COL_LENGTH('dbo.ReceiptSet', 'RenewedFromReceiptSetId') IS NULL THEN 0 ELSE 1 END;";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    var result = cmd.ExecuteScalar();
                    _hasRenewedFromReceiptSetIdColumn = Convert.ToInt32(result) == 1;
                }
            }
            catch
            {
                _hasRenewedFromReceiptSetIdColumn = false;
            }

            return _hasRenewedFromReceiptSetIdColumn.Value;
        }

        public List<ReceiptSetDto> GetAll()
        {
            var list = new List<ReceiptSetDto>();

            const string sql = @"
SELECT
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    agg.SetCodes AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    rs.SiImage,
    rs.DrImage,
    rs.PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
OUTER APPLY (
    SELECT STRING_AGG(s.SetCode, ', ') WITHIN GROUP (ORDER BY s.SetCode) AS SetCodes
    FROM dbo.ReceiptSetLink l
    INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
    WHERE l.ReceiptSetId = rs.ReceiptSetId
) agg
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns receipt sets for list pages without loading image blobs.
        /// Use this for grids to avoid UI freezes and high memory usage.
        /// </summary>
        public List<ReceiptSetDto> GetAllMetadata()
        {
            var list = new List<ReceiptSetDto>();

            const string sql = @"
SELECT
    rs.ReceiptSetId,
    primaryLink.SetId AS SetId,
    agg.SetCodes AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    ren.TotalItems,
    ren.RenewedItems,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
OUTER APPLY (
    SELECT STRING_AGG(s.SetCode, ', ') WITHIN GROUP (ORDER BY s.SetCode) AS SetCodes
    FROM dbo.ReceiptSetLink l
    INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
    WHERE l.ReceiptSetId = rs.ReceiptSetId
) agg
OUTER APPLY (
    SELECT TOP 1 l2.SetId
    FROM dbo.ReceiptSetLink l2
    INNER JOIN dbo.[Set] s2 ON l2.SetId = s2.SetId
    WHERE l2.ReceiptSetId = rs.ReceiptSetId
    ORDER BY s2.SetCode
) primaryLink
OUTER APPLY (
    SELECT
        TotalItems = COUNT(1),
        RenewedItems = SUM(CASE WHEN latest.RenewalStatus = 'Renewed' THEN 1 ELSE 0 END)
    FROM dbo.SetItem si
    OUTER APPLY (
        SELECT TOP 1 r.RenewalStatus
        FROM dbo.Renewals r
        WHERE r.ItemId = si.ItemId
          AND r.IsArchived = 0
        ORDER BY r.CreatedAt DESC, r.RenewalId DESC
    ) latest
    WHERE si.SetId = primaryLink.SetId
) ren
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns "current" receipt sets per linked Set (latest coverage) plus all unlinked receipt sets.
        /// This matches the UI expectation that renewing replaces the "current" receipt while older
        /// receipts remain accessible via Previous/History in the viewer.
        /// </summary>
        public List<ReceiptSetDto> GetAllMetadataCurrentPerSet()
        {
            var list = new List<ReceiptSetDto>();

            string unlinkedHeadFilter = HasRenewedFromReceiptSetIdColumn()
                ? "  AND NOT EXISTS (SELECT 1 FROM dbo.ReceiptSet newer WHERE newer.RenewedFromReceiptSetId = rs.ReceiptSetId)\n"
                : string.Empty;

            string sql = @"
;WITH LatestPerSet AS (
    SELECT
        l.SetId,
        l.ReceiptSetId,
        l.CoverageStartDate,
        l.CoverageEndDate,
        l.CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY l.SetId
            ORDER BY
                CASE WHEN l.CoverageEndDate IS NULL THEN 0 ELSE 1 END DESC,
                l.CoverageEndDate DESC,
                l.CoverageStartDate DESC,
                l.CreatedAt DESC,
                l.ReceiptSetId DESC
        ) AS rn
    FROM dbo.ReceiptSetLink l
)
SELECT
    rs.ReceiptSetId,
    lp.SetId AS SetId,
    s.SetCode AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    ren.TotalItems,
    ren.RenewedItems,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM LatestPerSet lp
INNER JOIN dbo.ReceiptSet rs ON rs.ReceiptSetId = lp.ReceiptSetId
LEFT JOIN dbo.[Set] s ON s.SetId = lp.SetId
OUTER APPLY (
    SELECT
        TotalItems = COUNT(1),
        RenewedItems = SUM(CASE WHEN latest.RenewalStatus = 'Renewed' THEN 1 ELSE 0 END)
    FROM dbo.SetItem si
    OUTER APPLY (
        SELECT TOP 1 r.RenewalStatus
        FROM dbo.Renewals r
        WHERE r.ItemId = si.ItemId
          AND r.IsArchived = 0
        ORDER BY r.CreatedAt DESC, r.RenewalId DESC
    ) latest
    WHERE si.SetId = lp.SetId
) ren
WHERE lp.rn = 1

UNION ALL

SELECT
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    CAST(NULL AS nvarchar(200)) AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    CAST(NULL AS int) AS TotalItems,
    CAST(NULL AS int) AS RenewedItems,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
 FROM dbo.ReceiptSet rs
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ReceiptSetLink l WHERE l.ReceiptSetId = rs.ReceiptSetId)
" + unlinkedHeadFilter + @"
 ORDER BY CreatedAt DESC, ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        public List<ReceiptSetDto> GetReceiptSetsAvailableForSet(int setId)
        {
            var list = new List<ReceiptSetDto>();

            const string sql = @"
SELECT
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    agg.SetCodes AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    rs.SiImage,
    rs.DrImage,
    rs.PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
OUTER APPLY (
    SELECT STRING_AGG(s.SetCode, ', ') WITHIN GROUP (ORDER BY s.SetCode) AS SetCodes
    FROM dbo.ReceiptSetLink l
    INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
    WHERE l.ReceiptSetId = rs.ReceiptSetId
) agg
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ReceiptSetLink l
    WHERE l.SetId = @SetId
      AND l.ReceiptSetId = rs.ReceiptSetId
)
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Same as GetReceiptSetsAvailableForSet, but does not load image blobs.
        /// Use this for selection dialogs to avoid UI freezes and high memory usage.
        /// </summary>
        public List<ReceiptSetDto> GetReceiptSetsAvailableForSetMetadata(int setId)
        {
            var list = new List<ReceiptSetDto>();

            const string sql = @"
SELECT
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    agg.SetCodes AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
OUTER APPLY (
    SELECT STRING_AGG(s.SetCode, ', ') WITHIN GROUP (ORDER BY s.SetCode) AS SetCodes
    FROM dbo.ReceiptSetLink l
    INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
    WHERE l.ReceiptSetId = rs.ReceiptSetId
) agg
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ReceiptSetLink l
    WHERE l.SetId = @SetId
      AND l.ReceiptSetId = rs.ReceiptSetId
)
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns EVERY ReceiptSet ever linked to any of the given SetIds — not just the
        /// current/latest coverage period per Set. Used by the Renewal Chain Receipts window
        /// to list every scanned document attached across an entire renewal chain (Original,
        /// each renewal, and Latest), grouped by which Set each receipt belongs to.
        /// Metadata only (no image blobs) — callers open ReceiptSetViewerWindow for full detail.
        /// </summary>
        public List<ReceiptSetDto> GetReceiptSetsForSetIds(IEnumerable<int> setIds)
        {
            var list = new List<ReceiptSetDto>();
            var idList = string.Join(",", setIds ?? Enumerable.Empty<int>());
            if (string.IsNullOrEmpty(idList))
                return list;

            string sql = $@"
SELECT
    rs.ReceiptSetId,
    l.SetId AS SetId,
    s.SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSetLink l
INNER JOIN dbo.ReceiptSet rs ON rs.ReceiptSetId = l.ReceiptSetId
INNER JOIN dbo.[Set] s ON s.SetId = l.SetId
WHERE l.SetId IN ({idList})
ORDER BY l.SetId, rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(Map(reader));
                }
            }

            return list;
        }

        public ReceiptSetDto GetBySetId(int setId)
        {
            const string sql = @"
SELECT TOP 1
    rs.ReceiptSetId,
    l.SetId AS SetId,
    s.SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    rs.SiImage,
    rs.DrImage,
    rs.PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
INNER JOIN dbo.ReceiptSetLink l ON rs.ReceiptSetId = l.ReceiptSetId
INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
WHERE l.SetId = @SetId
ORDER BY
    CASE WHEN l.CoverageStartDate IS NULL THEN 0 ELSE 1 END DESC,
    l.CoverageStartDate DESC,
    rs.CreatedAt DESC,
    rs.ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Map(reader);
                    }
                }
            }

            return null;
        }

        public ReceiptSetDto GetBySetIdCoverage(int setId, DateTime coverageStartDate, DateTime coverageEndDate)
        {
            const string sql = @"
 SELECT TOP 1
     rs.ReceiptSetId,
     l.SetId AS SetId,
     s.SetCode,
     rs.Supplier,
     rs.SiNumber,
     rs.DrNumber,
     rs.PoNumber,
     rs.SiImagePath,
     rs.DrImagePath,
     rs.PoImagePath,
     rs.SiImage,
     rs.DrImage,
     rs.PoImage,
     rs.CreatedAt,
     rs.CreatedBy,
     rs.ModifiedAt,
     rs.ModifiedBy
 FROM dbo.ReceiptSet rs
 INNER JOIN dbo.ReceiptSetLink l ON rs.ReceiptSetId = l.ReceiptSetId
 INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
 WHERE l.SetId = @SetId
   AND CONVERT(date, l.CoverageStartDate) = @CoverageStartDate
   AND CONVERT(date, l.CoverageEndDate) = @CoverageEndDate
 ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@CoverageStartDate", coverageStartDate.Date);
                cmd.Parameters.AddWithValue("@CoverageEndDate", coverageEndDate.Date);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        return Map(reader);
                }
            }

            return null;
        }

        public ReceiptSetDto GetMetadataBySetIdCoverage(int setId, DateTime coverageStartDate, DateTime coverageEndDate)
        {
            const string sql = @"
SELECT TOP 1
    rs.ReceiptSetId,
    l.SetId AS SetId,
    s.SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
INNER JOIN dbo.ReceiptSetLink l ON rs.ReceiptSetId = l.ReceiptSetId
INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
WHERE l.SetId = @SetId
  AND CONVERT(date, l.CoverageStartDate) = @CoverageStartDate
  AND CONVERT(date, l.CoverageEndDate) = @CoverageEndDate
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@CoverageStartDate", coverageStartDate.Date);
                cmd.Parameters.AddWithValue("@CoverageEndDate", coverageEndDate.Date);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        return Map(reader);
                }
            }

            return null;
        }

        public Dictionary<int, string> GetUserNamesByIds(IEnumerable<int> userIds)
        {
            var result = new Dictionary<int, string>();
            if (userIds == null)
                return result;

            var ids = userIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return result;

            var parameters = new List<string>();
            for (int i = 0; i < ids.Count; i++)
                parameters.Add("@id" + i);

            string sql = $@"
SELECT u.UserId, u.Name
FROM dbo.[User] u
WHERE u.UserId IN ({string.Join(", ", parameters)});";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                for (int i = 0; i < ids.Count; i++)
                    cmd.Parameters.AddWithValue(parameters[i], ids[i]);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? null : reader.GetString(1);
                        if (!result.ContainsKey(id))
                            result[id] = name;
                    }
                }
            }

            return result;
        }

        public ReceiptSetDto GetByReceiptSetId(int receiptSetId)
        {
            string renewedFromSelect = HasRenewedFromReceiptSetIdColumn()
                ? "    rs.RenewedFromReceiptSetId,\n"
                : "    CAST(NULL AS int) AS RenewedFromReceiptSetId,\n";

            string sql = @"
SELECT TOP 1
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    agg.SetCodes AS SetCode,
" + renewedFromSelect + @"
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    rs.SiImage,
    rs.DrImage,
    rs.PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
OUTER APPLY (
    SELECT STRING_AGG(s.SetCode, ', ') WITHIN GROUP (ORDER BY s.SetCode) AS SetCodes
    FROM dbo.ReceiptSetLink l
    INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
    WHERE l.ReceiptSetId = rs.ReceiptSetId
) agg
WHERE rs.ReceiptSetId = @ReceiptSetId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Map(reader);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the unlinked "renewal" chain for a receipt set (current -> previous -> older...).
        /// This is used to populate Previous/History tabs for unlinked receipts.
        /// </summary>
        public List<ReceiptSetDto> GetReceiptRenewalChainMetadata(int receiptSetId)
        {
            var list = new List<ReceiptSetDto>();

            if (!HasRenewedFromReceiptSetIdColumn())
                return list;

            const string sql = @"
;WITH Chain AS (
    SELECT
        Depth = 0,
        rs.ReceiptSetId,
        rs.RenewedFromReceiptSetId,
        rs.Supplier,
        rs.SiNumber,
        rs.DrNumber,
        rs.PoNumber,
        rs.SiImagePath,
        rs.DrImagePath,
        rs.PoImagePath,
        rs.CreatedAt,
        rs.CreatedBy,
        rs.ModifiedAt,
        rs.ModifiedBy
    FROM dbo.ReceiptSet rs
    WHERE rs.ReceiptSetId = @ReceiptSetId

    UNION ALL

    SELECT
        Depth = c.Depth + 1,
        prev.ReceiptSetId,
        prev.RenewedFromReceiptSetId,
        prev.Supplier,
        prev.SiNumber,
        prev.DrNumber,
        prev.PoNumber,
        prev.SiImagePath,
        prev.DrImagePath,
        prev.PoImagePath,
        prev.CreatedAt,
        prev.CreatedBy,
        prev.ModifiedAt,
        prev.ModifiedBy
    FROM dbo.ReceiptSet prev
    INNER JOIN Chain c ON prev.ReceiptSetId = c.RenewedFromReceiptSetId
)
SELECT
    ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    CAST(NULL AS nvarchar(200)) AS SetCode,
    RenewedFromReceiptSetId,
    Supplier,
    SiNumber,
    DrNumber,
    PoNumber,
    SiImagePath,
    DrImagePath,
    PoImagePath,
    CAST(NULL AS varbinary(max)) AS SiImage,
    CAST(NULL AS varbinary(max)) AS DrImage,
    CAST(NULL AS varbinary(max)) AS PoImage,
    CAST(NULL AS int) AS TotalItems,
    CAST(NULL AS int) AS RenewedItems,
    CreatedAt,
    CreatedBy,
    ModifiedAt,
    ModifiedBy
FROM Chain
ORDER BY Depth
OPTION (MAXRECURSION 100);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Swaps the head (current) and its immediate previous version for an unlinked receipt set chain.
        /// After this, the previous version becomes the new head/current.
        /// </summary>
        public int RevertUnlinkedReceiptSetToPrevious(int currentReceiptSetId, int? userId)
        {
            if (currentReceiptSetId <= 0) throw new ArgumentOutOfRangeException(nameof(currentReceiptSetId));
            if (!HasRenewedFromReceiptSetIdColumn()) throw new InvalidOperationException("Renewal versioning is not enabled in the database.");

            const string sql = @"
BEGIN TRY
    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.ReceiptSetLink WHERE ReceiptSetId = @CurrentReceiptSetId)
        THROW 54030, 'This receipt set is linked to a Set. Revert is supported via coverage periods for linked receipts.', 1;

    DECLARE @PrevId INT = (SELECT RenewedFromReceiptSetId FROM dbo.ReceiptSet WHERE ReceiptSetId = @CurrentReceiptSetId);
    IF @PrevId IS NULL
        THROW 54031, 'No previous receipt set exists to revert to.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.ReceiptSet WHERE ReceiptSetId = @PrevId)
        THROW 54032, 'Previous receipt set record was not found.', 1;

    -- Ensure current is the head (no newer record points to it)
    IF EXISTS (SELECT 1 FROM dbo.ReceiptSet WHERE RenewedFromReceiptSetId = @CurrentReceiptSetId)
        THROW 54033, 'This receipt set is not the current version.', 1;

    DECLARE @PrevPrevId INT = (SELECT RenewedFromReceiptSetId FROM dbo.ReceiptSet WHERE ReceiptSetId = @PrevId);

    -- Current now points to what previous used to point to
    UPDATE dbo.ReceiptSet
    SET
        RenewedFromReceiptSetId = @PrevPrevId,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE ReceiptSetId = @CurrentReceiptSetId;

    -- Previous now points to current (becomes head)
    UPDATE dbo.ReceiptSet
    SET
        RenewedFromReceiptSetId = @CurrentReceiptSetId,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE ReceiptSetId = @PrevId;

    COMMIT;
    SELECT @PrevId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CurrentReceiptSetId", currentReceiptSetId);
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);
                con.Open();

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Swaps coverage periods between two receipt sets linked to the same Set.
        /// Used to "revert" linked receipts so the previous receipt becomes current for the coverage period.
        /// </summary>
        public void SwapReceiptSetCoverageLinks(int setId,
            int currentReceiptSetId, DateTime currentCoverageStart, DateTime currentCoverageEnd,
            int previousReceiptSetId, DateTime previousCoverageStart, DateTime previousCoverageEnd,
            int? userId)
        {
            if (setId <= 0) throw new ArgumentOutOfRangeException(nameof(setId));
            if (currentReceiptSetId <= 0) throw new ArgumentOutOfRangeException(nameof(currentReceiptSetId));
            if (previousReceiptSetId <= 0) throw new ArgumentOutOfRangeException(nameof(previousReceiptSetId));

            currentCoverageStart = currentCoverageStart.Date;
            currentCoverageEnd = currentCoverageEnd.Date;
            previousCoverageStart = previousCoverageStart.Date;
            previousCoverageEnd = previousCoverageEnd.Date;

            const string sql = @"
BEGIN TRY
    BEGIN TRAN;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ReceiptSetLink l
        WHERE l.SetId = @SetId
          AND l.ReceiptSetId = @CurrentReceiptSetId
          AND CONVERT(date, l.CoverageStartDate) = @CurrentCoverageStart
          AND CONVERT(date, l.CoverageEndDate) = @CurrentCoverageEnd
    )
        THROW 54040, 'Current receipt set link does not match the expected coverage period.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ReceiptSetLink l
        WHERE l.SetId = @SetId
          AND l.ReceiptSetId = @PreviousReceiptSetId
          AND CONVERT(date, l.CoverageStartDate) = @PreviousCoverageStart
          AND CONVERT(date, l.CoverageEndDate) = @PreviousCoverageEnd
    )
        THROW 54041, 'Previous receipt set link does not match the expected coverage period.', 1;

    -- Move current out of the unique filtered index temporarily
    UPDATE dbo.ReceiptSetLink
    SET
        CoverageStartDate = NULL,
        CoverageEndDate = NULL,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE SetId = @SetId
      AND ReceiptSetId = @CurrentReceiptSetId;

    -- Put previous into current period
    UPDATE dbo.ReceiptSetLink
    SET
        CoverageStartDate = @CurrentCoverageStart,
        CoverageEndDate = @CurrentCoverageEnd,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE SetId = @SetId
      AND ReceiptSetId = @PreviousReceiptSetId;

    -- Put current into previous period
    UPDATE dbo.ReceiptSetLink
    SET
        CoverageStartDate = @PreviousCoverageStart,
        CoverageEndDate = @PreviousCoverageEnd,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE SetId = @SetId
      AND ReceiptSetId = @CurrentReceiptSetId;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@CurrentReceiptSetId", currentReceiptSetId);
                cmd.Parameters.AddWithValue("@PreviousReceiptSetId", previousReceiptSetId);
                cmd.Parameters.AddWithValue("@CurrentCoverageStart", currentCoverageStart);
                cmd.Parameters.AddWithValue("@CurrentCoverageEnd", currentCoverageEnd);
                cmd.Parameters.AddWithValue("@PreviousCoverageStart", previousCoverageStart);
                cmd.Parameters.AddWithValue("@PreviousCoverageEnd", previousCoverageEnd);
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<ReceiptSetDto> GetUnlinkedReceiptSets()
        {
            var list = new List<ReceiptSetDto>();

            const string sql = @"
SELECT
    rs.ReceiptSetId,
    CAST(NULL AS INT) AS SetId,
    CAST(NULL AS NVARCHAR(50)) AS SetCode,
    rs.Supplier,
    rs.SiNumber,
    rs.DrNumber,
    rs.PoNumber,
    rs.SiImagePath,
    rs.DrImagePath,
    rs.PoImagePath,
    rs.SiImage,
    rs.DrImage,
    rs.PoImage,
    rs.CreatedAt,
    rs.CreatedBy,
    rs.ModifiedAt,
    rs.ModifiedBy
FROM dbo.ReceiptSet rs
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ReceiptSetLink l
    WHERE l.ReceiptSetId = rs.ReceiptSetId
)
ORDER BY rs.CreatedAt DESC, rs.ReceiptSetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(Map(reader));
                    }
                }
            }

            return list;
        }

        public void AttachReceiptSetToSet(int receiptSetId, int setId, int? userId)
        {
            var period = ResolveDefaultCoverageForSet(setId);
            AttachReceiptSetToSet(receiptSetId, setId, period?.StartDate, period?.EndDate, userId);
        }

        public void AttachReceiptSetToSet(int receiptSetId, int setId, DateTime? coverageStartDate, DateTime? coverageEndDate, int? userId)
        {
            // Normalize to date-only to avoid time component mismatches (e.g., 08:00 vs 00:00).
            if (coverageStartDate.HasValue) coverageStartDate = coverageStartDate.Value.Date;
            if (coverageEndDate.HasValue) coverageEndDate = coverageEndDate.Value.Date;

            const string sql = @"
-- Enforce: one receipt set per Set per coverage period
IF (@CoverageStartDate IS NOT NULL AND @CoverageEndDate IS NOT NULL)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.ReceiptSetLink l
        WHERE l.SetId = @SetId
          AND CONVERT(date, l.CoverageStartDate) = CONVERT(date, @CoverageStartDate)
          AND CONVERT(date, l.CoverageEndDate) = CONVERT(date, @CoverageEndDate)
          AND l.ReceiptSetId <> @ReceiptSetId
    )
    BEGIN
        THROW 54010, 'A receipt set already exists for this Set and coverage period.', 1;
    END
END

IF EXISTS (
    SELECT 1
    FROM dbo.ReceiptSetLink
    WHERE ReceiptSetId = @ReceiptSetId
      AND SetId = @SetId
)
BEGIN
    UPDATE dbo.ReceiptSetLink
    SET
        CoverageStartDate = @CoverageStartDate,
        CoverageEndDate = @CoverageEndDate,
        ModifiedAt = GETDATE(),
        ModifiedBy = @UserId
    WHERE ReceiptSetId = @ReceiptSetId
      AND SetId = @SetId;
END
ELSE
BEGIN
    INSERT INTO dbo.ReceiptSetLink (ReceiptSetId, SetId, CoverageStartDate, CoverageEndDate, CreatedAt, CreatedBy)
    VALUES (@ReceiptSetId, @SetId, @CoverageStartDate, @CoverageEndDate, GETDATE(), @UserId);
END

UPDATE dbo.ReceiptSet
SET
    ModifiedAt = GETDATE(),
    ModifiedBy = @UserId
WHERE ReceiptSetId = @ReceiptSetId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CoverageStartDate", (object)coverageStartDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CoverageEndDate", (object)coverageEndDate ?? DBNull.Value);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public ReceiptSetLinkedSetDto GetSetLinkInfo(int setId)
        {
            const string sql = @"SELECT SetId, SetCode, DocumentNumber, IsInvoice FROM dbo.[Set] WHERE SetId = @SetId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new ReceiptSetLinkedSetDto
                    {
                        SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                        SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode")),
                        DocumentNumber = reader.IsDBNull(reader.GetOrdinal("DocumentNumber")) ? null : reader.GetString(reader.GetOrdinal("DocumentNumber")),
                        IsInvoice = !reader.IsDBNull(reader.GetOrdinal("IsInvoice")) && reader.GetBoolean(reader.GetOrdinal("IsInvoice"))
                    };
                }
            }
        }

        public CoveragePeriod ResolveDefaultCoverageForSet(int setId)
        {
            // Default coverage: latest renewed period (from items in the set); fallback to Set.StartDate/EndDate.
            var periods = GetRenewedCoveragePeriodsForSet(setId);
            if (periods != null && periods.Count > 0)
                return periods[0];

            return GetSetCoveragePeriod(setId);
        }

        public RenewalSummary GetRenewalSummaryForSet(int setId)
        {
            const string sql = @"
SELECT
    TotalItems = COUNT(1),
    RenewedItems = SUM(CASE WHEN latest.RenewalStatus = 'Renewed' THEN 1 ELSE 0 END)
FROM dbo.SetItem si
OUTER APPLY (
    SELECT TOP 1
        r.RenewalStatus
    FROM dbo.Renewals r
    WHERE r.ItemId = si.ItemId
      AND r.IsArchived = 0
    ORDER BY r.CreatedAt DESC, r.RenewalId DESC
) latest
WHERE si.SetId = @SetId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return new RenewalSummary { TotalItems = 0, RenewedItems = 0 };

                    var total = reader.IsDBNull(reader.GetOrdinal("TotalItems")) ? 0 : Convert.ToInt32(reader["TotalItems"]);
                    var renewed = reader.IsDBNull(reader.GetOrdinal("RenewedItems")) ? 0 : Convert.ToInt32(reader["RenewedItems"]);
                    return new RenewalSummary { TotalItems = total, RenewedItems = renewed };
                }
            }
        }

        public List<CoveragePeriod> GetRenewedCoveragePeriodsForSet(int setId)
        {
            var list = new List<CoveragePeriod>();

            const string sql = @"
SELECT DISTINCT
    CONVERT(date, r.NewStartDate) AS NewStartDate,
    CONVERT(date, r.NewEndDate) AS NewEndDate
FROM dbo.SetItem si
INNER JOIN dbo.Renewals r ON r.ItemId = si.ItemId
WHERE si.SetId = @SetId
  AND r.IsArchived = 0
  AND r.RenewalStatus = 'Renewed'
  AND r.NewStartDate IS NOT NULL
  AND r.NewEndDate IS NOT NULL
ORDER BY NewStartDate DESC, NewEndDate DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var start = reader.GetDateTime(reader.GetOrdinal("NewStartDate"));
                        var end = reader.GetDateTime(reader.GetOrdinal("NewEndDate"));
                        list.Add(new CoveragePeriod { StartDate = start, EndDate = end });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns coverage periods derived from ReceiptSetLink records for a given Set.
        /// Used as a fallback when the Set itself has no StartDate/EndDate and no renewal-
        /// based coverage periods exist.
        /// </summary>
        public List<CoveragePeriod> GetLinkCoveragePeriodsForSet(int setId)
        {
            var list = new List<CoveragePeriod>();

            const string sql = @"
SELECT
    l.CoverageStartDate,
    l.CoverageEndDate
FROM dbo.ReceiptSetLink l
WHERE l.SetId = @SetId
ORDER BY
    CASE WHEN l.CoverageEndDate IS NULL THEN 0 ELSE 1 END DESC,
    l.CoverageEndDate DESC,
    l.CoverageStartDate DESC,
    l.ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(reader.GetOrdinal("CoverageStartDate")) ||
                            reader.IsDBNull(reader.GetOrdinal("CoverageEndDate")))
                            continue;
                        var start = reader.GetDateTime(reader.GetOrdinal("CoverageStartDate"));
                        var end = reader.GetDateTime(reader.GetOrdinal("CoverageEndDate"));
                        list.Add(new CoveragePeriod { StartDate = start, EndDate = end });
                    }
                }
            }

            return list;
        }

        public CoveragePeriod GetSetCoveragePeriod(int setId)
        {
            const string sql = @"SELECT StartDate, EndDate FROM dbo.[Set] WHERE SetId = @SetId;";
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    if (reader.IsDBNull(reader.GetOrdinal("StartDate")) || reader.IsDBNull(reader.GetOrdinal("EndDate")))
                        return null;

                    return new CoveragePeriod
                    {
                        StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")).Date,
                        EndDate = reader.GetDateTime(reader.GetOrdinal("EndDate")).Date
                    };
                }
            }
        }

        public void Delete(int receiptSetId)
        {
            string sql = HasRenewedFromReceiptSetIdColumn()
                ? @"
UPDATE dbo.ReceiptSet
SET RenewedFromReceiptSetId = NULL
WHERE RenewedFromReceiptSetId = @ReceiptSetId;

DELETE FROM dbo.ReceiptSet WHERE ReceiptSetId = @ReceiptSetId;"
                : @"DELETE FROM dbo.ReceiptSet WHERE ReceiptSetId = @ReceiptSetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UnlinkReceiptSet(int receiptSetId, int setId, int? userId)
        {
            const string sql = @"
DELETE FROM dbo.ReceiptSetLink
WHERE ReceiptSetId = @ReceiptSetId
  AND SetId = @SetId;

UPDATE dbo.ReceiptSet
SET
    ModifiedAt = GETDATE(),
    ModifiedBy = @UserId
WHERE ReceiptSetId = @ReceiptSetId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<ReceiptSetLinkedSetDto> GetLinkedSets(int receiptSetId)
        {
            var list = new List<ReceiptSetLinkedSetDto>();

            const string sql = @"
SELECT
    s.SetId,
    s.SetCode
FROM dbo.ReceiptSetLink l
INNER JOIN dbo.[Set] s ON l.SetId = s.SetId
WHERE l.ReceiptSetId = @ReceiptSetId
ORDER BY s.SetCode";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ReceiptSetLinkedSetDto
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.GetString(reader.GetOrdinal("SetCode"))
                        });
                    }
                }
            }

            return list;
        }

        public int? GetLinkedReceiptSetId(int setId)
        {
            const string sql = @"
SELECT TOP 1 ReceiptSetId
FROM dbo.ReceiptSetLink
WHERE SetId = @SetId
ORDER BY CreatedAt DESC, ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                con.Open();

                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return null;

                return Convert.ToInt32(result);
            }
        }

        public ReceiptSetLinkedSetDto FindInvoiceSetByDocumentNumber(string documentNumber)
        {
            if (string.IsNullOrWhiteSpace(documentNumber))
                return null;

            const string sql = @"
SELECT TOP 1
    s.SetId,
    s.SetCode
FROM dbo.[Set] s
WHERE s.IsInvoice = 1
  AND LTRIM(RTRIM(s.DocumentNumber)) = @DocumentNumber
ORDER BY s.SetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@DocumentNumber", documentNumber.Trim());
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new ReceiptSetLinkedSetDto
                    {
                        SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                        SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"))
                    };
                }
            }
        }

        // ─── Multi-image support (SI/DR/PO can each have multiple pages/photos) ───────────

        private static bool? _hasReceiptSetDocumentImageTable;

        private bool HasReceiptSetDocumentImageTable()
        {
            if (_hasReceiptSetDocumentImageTable.HasValue)
                return _hasReceiptSetDocumentImageTable.Value;

            const string sql = "SELECT CASE WHEN OBJECT_ID('dbo.ReceiptSetDocumentImage', 'U') IS NULL THEN 0 ELSE 1 END;";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    var result = cmd.ExecuteScalar();
                    _hasReceiptSetDocumentImageTable = Convert.ToInt32(result) == 1;
                }
            }
            catch
            {
                _hasReceiptSetDocumentImageTable = false;
            }

            return _hasReceiptSetDocumentImageTable.Value;
        }

        private bool? _hasMimeTypeColumn;

        private bool HasMimeTypeColumn()
        {
            if (_hasMimeTypeColumn.HasValue)
                return _hasMimeTypeColumn.Value;

            const string sql = "SELECT CASE WHEN COL_LENGTH('dbo.ReceiptSetDocumentImage', 'MimeType') IS NULL THEN 0 ELSE 1 END;";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    var result = cmd.ExecuteScalar();
                    _hasMimeTypeColumn = Convert.ToInt32(result) == 1;
                }
            }
            catch
            {
                _hasMimeTypeColumn = false;
            }

            return _hasMimeTypeColumn.Value;
        }

        /// <summary>
        /// Returns all images for a receipt set, optionally filtered to a single document type
        /// ("SI", "DR", or "PO"). Ordered by SortOrder then ImageId. Returns an empty list if the
        /// multi-image table has not been migrated yet (Migration_ReceiptSetMultiImage.sql).
        /// </summary>
        public List<Yakult.Inventory.App.Models.ReceiptSetImageDto> GetImagesForReceiptSet(int receiptSetId, string docType = null)
        {
            var list = new List<Yakult.Inventory.App.Models.ReceiptSetImageDto>();

            if (!HasReceiptSetDocumentImageTable())
                return list;

            string mimeTypeColumn = HasMimeTypeColumn() ? ", MimeType" : ", CAST(NULL AS NVARCHAR(50)) AS MimeType";
            string sql = @"
SELECT ImageId, ReceiptSetId, DocType, ImagePath, ImageBytes, SortOrder, CreatedAt, CreatedBy" + mimeTypeColumn + @"
FROM dbo.ReceiptSetDocumentImage
WHERE ReceiptSetId = @ReceiptSetId"
                + (string.IsNullOrWhiteSpace(docType) ? string.Empty : " AND DocType = @DocType")
                + "\nORDER BY DocType, SortOrder, ImageId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                if (!string.IsNullOrWhiteSpace(docType))
                    cmd.Parameters.AddWithValue("@DocType", docType);

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapImage(reader));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Adds a new image for the given receipt set/document type. Returns the new ImageId.
        /// Appends to the end (SortOrder = max existing + 1) unless sortOrder is explicitly provided.
        /// </summary>
        public int AddImage(int receiptSetId, string docType, byte[] imageBytes, string imagePath, int? userId, int? sortOrder = null)
        {
            if (receiptSetId <= 0) throw new ArgumentOutOfRangeException(nameof(receiptSetId));
            if (string.IsNullOrWhiteSpace(docType)) throw new ArgumentNullException(nameof(docType));
            if (!HasReceiptSetDocumentImageTable())
                throw new InvalidOperationException("Multi-image storage is not available. Run Migration_ReceiptSetMultiImage.sql first.");

            const string sql = @"
DECLARE @NextSort INT = ISNULL(
    (SELECT MAX(SortOrder) + 1 FROM dbo.ReceiptSetDocumentImage WHERE ReceiptSetId = @ReceiptSetId AND DocType = @DocType),
    0);

INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImagePath, ImageBytes, SortOrder, CreatedAt, CreatedBy)
VALUES (@ReceiptSetId, @DocType, @ImagePath, @ImageBytes, COALESCE(@SortOrder, @NextSort), SYSUTCDATETIME(), @UserId);

SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                cmd.Parameters.AddWithValue("@DocType", docType);
                cmd.Parameters.AddWithValue("@ImagePath", (object)imagePath ?? DBNull.Value);
                cmd.Parameters.Add("@ImageBytes", SqlDbType.VarBinary, -1).Value = (object)imageBytes ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@SortOrder", (object)sortOrder ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);

                con.Open();
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Deletes a single image by ImageId.
        /// </summary>
        public void DeleteImage(int imageId)
        {
            if (!HasReceiptSetDocumentImageTable())
                return;

            const string sql = "DELETE FROM dbo.ReceiptSetDocumentImage WHERE ImageId = @ImageId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ImageId", imageId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Deletes all images for a receipt set (used when a receipt set itself is deleted, or to clear a doc type).
        /// </summary>
        public void DeleteImagesForReceiptSet(int receiptSetId, string docType = null)
        {
            if (!HasReceiptSetDocumentImageTable())
                return;

            string sql = "DELETE FROM dbo.ReceiptSetDocumentImage WHERE ReceiptSetId = @ReceiptSetId"
                + (string.IsNullOrWhiteSpace(docType) ? string.Empty : " AND DocType = @DocType") + ";";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                if (!string.IsNullOrWhiteSpace(docType))
                    cmd.Parameters.AddWithValue("@DocType", docType);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Persists a full reorder of images for a document type. Pass the ImageIds in the desired
        /// display order; SortOrder is rewritten to match the position in the list.
        /// </summary>
        public void ReorderImages(int receiptSetId, string docType, IReadOnlyList<int> orderedImageIds, int? userId)
        {
            if (!HasReceiptSetDocumentImageTable())
                return;
            if (orderedImageIds == null || orderedImageIds.Count == 0)
                return;

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        for (int i = 0; i < orderedImageIds.Count; i++)
                        {
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.ReceiptSetDocumentImage SET SortOrder = @SortOrder WHERE ImageId = @ImageId AND ReceiptSetId = @ReceiptSetId AND DocType = @DocType;",
                                con, tx))
                            {
                                cmd.Parameters.AddWithValue("@SortOrder", i);
                                cmd.Parameters.AddWithValue("@ImageId", orderedImageIds[i]);
                                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                                cmd.Parameters.AddWithValue("@DocType", docType);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a count of images per document type for a receipt set, e.g. {"SI":2,"DR":1,"PO":0}.
        /// Falls back to the legacy single-image columns (0 or 1 per type) if the multi-image table
        /// has not been migrated yet.
        /// </summary>
        public Dictionary<string, int> GetImageCountsForReceiptSet(int receiptSetId)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["SI"] = 0,
                ["DR"] = 0,
                ["PO"] = 0
            };

            if (HasReceiptSetDocumentImageTable())
            {
                const string sql = @"
SELECT DocType, COUNT(1) AS Cnt
FROM dbo.ReceiptSetDocumentImage
WHERE ReceiptSetId = @ReceiptSetId
GROUP BY DocType;";

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var type = reader.GetString(reader.GetOrdinal("DocType"));
                            var cnt = Convert.ToInt32(reader["Cnt"]);
                            counts[type] = cnt;
                        }
                    }
                }

                return counts;
            }

            // Legacy fallback: single-image columns.
            var legacy = GetByReceiptSetId(receiptSetId);
            if (legacy != null)
            {
                counts["SI"] = (legacy.SiImage != null && legacy.SiImage.Length > 0) || !string.IsNullOrWhiteSpace(legacy.SiImagePath) ? 1 : 0;
                counts["DR"] = (legacy.DrImage != null && legacy.DrImage.Length > 0) || !string.IsNullOrWhiteSpace(legacy.DrImagePath) ? 1 : 0;
                counts["PO"] = (legacy.PoImage != null && legacy.PoImage.Length > 0) || !string.IsNullOrWhiteSpace(legacy.PoImagePath) ? 1 : 0;
            }

            return counts;
        }

        private static Yakult.Inventory.App.Models.ReceiptSetImageDto MapImage(SqlDataReader reader)
        {
            return new Yakult.Inventory.App.Models.ReceiptSetImageDto
            {
                ImageId = reader.GetInt32(reader.GetOrdinal("ImageId")),
                ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                DocType = reader.GetString(reader.GetOrdinal("DocType")),
                ImagePath = reader.IsDBNull(reader.GetOrdinal("ImagePath")) ? null : reader.GetString(reader.GetOrdinal("ImagePath")),
                ImageBytes = reader.IsDBNull(reader.GetOrdinal("ImageBytes")) ? null : (byte[])reader["ImageBytes"],
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                MimeType = reader.IsDBNull(reader.GetOrdinal("MimeType")) ? null : reader.GetString(reader.GetOrdinal("MimeType"))
            };
        }

        /// <summary>
        /// Looks up another receipt set that already uses the given SI number, so the caller can
        /// warn the user before saving a duplicate. Excludes <paramref name="excludeReceiptSetId"/>
        /// (the receipt currently being edited, if any) and ignores blank SI numbers.
        /// </summary>
        public ReceiptSetDto FindReceiptSetBySiNumber(string siNumber, int excludeReceiptSetId)
        {
            var si = (siNumber ?? string.Empty).Trim();
            if (si.Length == 0)
                return null;

            const string sql = @"
SELECT TOP 1
    rs.ReceiptSetId, rs.Supplier, rs.SiNumber, rs.DrNumber, rs.PoNumber, rs.CreatedAt
FROM dbo.ReceiptSet rs
WHERE rs.ReceiptSetId <> @ExcludeReceiptSetId
  AND LTRIM(RTRIM(rs.SiNumber)) = @SiNumber
ORDER BY rs.ReceiptSetId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SiNumber", si);
                cmd.Parameters.AddWithValue("@ExcludeReceiptSetId", excludeReceiptSetId);
                con.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new ReceiptSetDto
                    {
                        ReceiptSetId = reader.GetInt32(0),
                        Supplier = reader.IsDBNull(1) ? null : reader.GetString(1),
                        SiNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                        DrNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                        PoNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.IsDBNull(5) ? default(DateTime) : reader.GetDateTime(5)
                    };
                }
            }
        }

        public int Save(ReceiptSetDto dto, int? userId)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = con;
                con.Open();

                bool hasRenewedFrom = HasRenewedFromReceiptSetIdColumn();

                if (dto.ReceiptSetId > 0)
                {
                    cmd.CommandText = @"
UPDATE dbo.ReceiptSet
SET
    Supplier = @Supplier,
    SiNumber = @SiNumber,
    DrNumber = @DrNumber,
    PoNumber = @PoNumber,
    SiImagePath = @SiImagePath,
    DrImagePath = @DrImagePath,
    PoImagePath = @PoImagePath,
    SiImage = @SiImage,
    DrImage = @DrImage,
    PoImage = @PoImage,
    ModifiedAt = GETDATE(),
    ModifiedBy = @UserId
WHERE ReceiptSetId = @ReceiptSetId;
SELECT @ReceiptSetId;";

                    cmd.Parameters.AddWithValue("@ReceiptSetId", dto.ReceiptSetId);
                }
                else
                {
                    if (hasRenewedFrom)
                    {
                        cmd.CommandText = @"
INSERT INTO dbo.ReceiptSet
    (RenewedFromReceiptSetId,
     Supplier, SiNumber, DrNumber, PoNumber,
     SiImagePath, DrImagePath, PoImagePath,
     SiImage, DrImage, PoImage,
     CreatedAt, CreatedBy)
VALUES
    (@RenewedFromReceiptSetId,
     @Supplier, @SiNumber, @DrNumber, @PoNumber,
     @SiImagePath, @DrImagePath, @PoImagePath,
     @SiImage, @DrImage, @PoImage,
     GETDATE(), @UserId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        cmd.Parameters.AddWithValue("@RenewedFromReceiptSetId", (object)dto.RenewedFromReceiptSetId ?? DBNull.Value);
                    }
                    else
                    {
                        cmd.CommandText = @"
INSERT INTO dbo.ReceiptSet
    (Supplier, SiNumber, DrNumber, PoNumber,
     SiImagePath, DrImagePath, PoImagePath,
     SiImage, DrImage, PoImage,
     CreatedAt, CreatedBy)
VALUES
    (@Supplier, @SiNumber, @DrNumber, @PoNumber,
     @SiImagePath, @DrImagePath, @PoImagePath,
     @SiImage, @DrImage, @PoImage,
     GETDATE(), @UserId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    }
                }

                cmd.Parameters.AddWithValue("@Supplier", (object)dto.Supplier ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SiNumber", (object)dto.SiNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DrNumber", (object)dto.DrNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PoNumber", (object)dto.PoNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SiImagePath", (object)dto.SiImagePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DrImagePath", (object)dto.DrImagePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PoImagePath", (object)dto.PoImagePath ?? DBNull.Value);

                // IMPORTANT: Explicitly type blob parameters. AddWithValue + DBNull can infer NVARCHAR
                // which causes "Implicit conversion from nvarchar to varbinary(max) is not allowed."
                cmd.Parameters.Add("@SiImage", SqlDbType.VarBinary, -1).Value = (object)dto.SiImage ?? DBNull.Value;
                cmd.Parameters.Add("@DrImage", SqlDbType.VarBinary, -1).Value = (object)dto.DrImage ?? DBNull.Value;
                cmd.Parameters.Add("@PoImage", SqlDbType.VarBinary, -1).Value = (object)dto.PoImage ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@UserId", (object)userId ?? DBNull.Value);

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Receipt documents that still have an image stored only as a local file path, with no
        /// bytes yet in the database. Covers both the legacy single-image columns on dbo.ReceiptSet
        /// (SiImage/DrImage/PoImage) and the multi-image dbo.ReceiptSetDocumentImage table.
        /// </summary>
        public async Task<List<Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto>> GetReceiptsPendingBackfillAsync()
        {
            var list = new List<Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto>();

            const string legacySql = @"
SELECT ReceiptSetId, 'SI' AS DocType, SiImagePath AS ImagePath FROM dbo.ReceiptSet WHERE SiImagePath IS NOT NULL AND SiImage IS NULL
UNION ALL
SELECT ReceiptSetId, 'DR' AS DocType, DrImagePath AS ImagePath FROM dbo.ReceiptSet WHERE DrImagePath IS NOT NULL AND DrImage IS NULL
UNION ALL
SELECT ReceiptSetId, 'PO' AS DocType, PoImagePath AS ImagePath FROM dbo.ReceiptSet WHERE PoImagePath IS NOT NULL AND PoImage IS NULL;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(legacySql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto
                        {
                            ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                            DocType = reader.GetString(reader.GetOrdinal("DocType")),
                            ImagePath = reader.GetString(reader.GetOrdinal("ImagePath"))
                        });
                    }
                }
            }

            if (HasReceiptSetDocumentImageTable())
            {
                const string multiSql = @"
SELECT ImageId, ReceiptSetId, DocType, ImagePath
FROM dbo.ReceiptSetDocumentImage
WHERE ImagePath IS NOT NULL AND ImageBytes IS NULL;";

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(multiSql, con))
                {
                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto
                            {
                                ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                                ImageId = reader.GetInt32(reader.GetOrdinal("ImageId")),
                                DocType = reader.GetString(reader.GetOrdinal("DocType")),
                                ImagePath = reader.GetString(reader.GetOrdinal("ImagePath"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Same as GetReceiptsPendingBackfillAsync, but scoped to a single receipt set — used by the
        /// "Copy Local Receipt to DB" action in the receipt viewer, which backfills just the receipt
        /// currently open rather than scanning the whole database.
        /// </summary>
        public async Task<List<Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto>> GetReceiptsPendingBackfillForReceiptSetAsync(int receiptSetId)
        {
            var list = new List<Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto>();

            const string legacySql = @"
SELECT ReceiptSetId, 'SI' AS DocType, SiImagePath AS ImagePath FROM dbo.ReceiptSet WHERE ReceiptSetId = @ReceiptSetId AND SiImagePath IS NOT NULL AND SiImage IS NULL
UNION ALL
SELECT ReceiptSetId, 'DR' AS DocType, DrImagePath AS ImagePath FROM dbo.ReceiptSet WHERE ReceiptSetId = @ReceiptSetId AND DrImagePath IS NOT NULL AND DrImage IS NULL
UNION ALL
SELECT ReceiptSetId, 'PO' AS DocType, PoImagePath AS ImagePath FROM dbo.ReceiptSet WHERE ReceiptSetId = @ReceiptSetId AND PoImagePath IS NOT NULL AND PoImage IS NULL;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(legacySql, con))
            {
                cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto
                        {
                            ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                            DocType = reader.GetString(reader.GetOrdinal("DocType")),
                            ImagePath = reader.GetString(reader.GetOrdinal("ImagePath"))
                        });
                    }
                }
            }

            if (HasReceiptSetDocumentImageTable())
            {
                const string multiSql = @"
SELECT ImageId, ReceiptSetId, DocType, ImagePath
FROM dbo.ReceiptSetDocumentImage
WHERE ReceiptSetId = @ReceiptSetId AND ImagePath IS NOT NULL AND ImageBytes IS NULL;";

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(multiSql, con))
                {
                    cmd.Parameters.AddWithValue("@ReceiptSetId", receiptSetId);
                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto
                            {
                                ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                                ImageId = reader.GetInt32(reader.GetOrdinal("ImageId")),
                                DocType = reader.GetString(reader.GetOrdinal("DocType")),
                                ImagePath = reader.GetString(reader.GetOrdinal("ImagePath"))
                            });
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Backfills a single receipt document's bytes from a locally-read file. Guarded by
        /// "bytes IS NULL" so it's safe to re-run without clobbering an already-migrated row.
        /// Returns false if another run already migrated this row first.
        /// </summary>
        public async Task<bool> BackfillReceiptBytesAsync(Yakult.Inventory.App.Pages.ReceiptBackfillCandidateDto candidate, byte[] bytes)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            string sql;
            if (candidate.ImageId.HasValue)
            {
                sql = @"
UPDATE dbo.ReceiptSetDocumentImage
SET ImageBytes = @Bytes
WHERE ImageId = @ImageId AND ImageBytes IS NULL;";
            }
            else
            {
                string column;
                switch (candidate.DocType)
                {
                    case "SI": column = "SiImage"; break;
                    case "DR": column = "DrImage"; break;
                    case "PO": column = "PoImage"; break;
                    default: throw new ArgumentOutOfRangeException(nameof(candidate.DocType), candidate.DocType, "Expected SI, DR, or PO.");
                }

                sql = $@"
UPDATE dbo.ReceiptSet
SET {column} = @Bytes
WHERE ReceiptSetId = @ReceiptSetId AND {column} IS NULL;";
            }

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
            {
                cmd.Parameters.Add("@Bytes", SqlDbType.VarBinary, -1).Value = bytes;
                if (candidate.ImageId.HasValue)
                    cmd.Parameters.AddWithValue("@ImageId", candidate.ImageId.Value);
                else
                    cmd.Parameters.AddWithValue("@ReceiptSetId", candidate.ReceiptSetId);

                await con.OpenAsync();
                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
        }

        private static ReceiptSetDto Map(SqlDataReader reader)
        {
            int Ord(string name)
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return -1;
            }

                return new ReceiptSetDto
                {
                    ReceiptSetId = reader.GetInt32(reader.GetOrdinal("ReceiptSetId")),
                    SetId = reader.IsDBNull(reader.GetOrdinal("SetId"))
                        ? (int?)null
                        : reader.GetInt32(reader.GetOrdinal("SetId")),
                    SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("SetCode")),
                    RenewedFromReceiptSetId = (Ord("RenewedFromReceiptSetId") >= 0 && !reader.IsDBNull(Ord("RenewedFromReceiptSetId")))
                        ? (int?)Convert.ToInt32(reader["RenewedFromReceiptSetId"])
                        : null,
                    Supplier = reader.IsDBNull(reader.GetOrdinal("Supplier"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Supplier")),
                    SiNumber = reader.IsDBNull(reader.GetOrdinal("SiNumber"))
                        ? null
                    : reader.GetString(reader.GetOrdinal("SiNumber")),
                DrNumber = reader.IsDBNull(reader.GetOrdinal("DrNumber"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("DrNumber")),
                PoNumber = reader.IsDBNull(reader.GetOrdinal("PoNumber"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("PoNumber")),
                SiImagePath = reader.IsDBNull(reader.GetOrdinal("SiImagePath"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("SiImagePath")),
                DrImagePath = reader.IsDBNull(reader.GetOrdinal("DrImagePath"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("DrImagePath")),
                PoImagePath = reader.IsDBNull(reader.GetOrdinal("PoImagePath"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("PoImagePath")),
                SiImage = reader.IsDBNull(reader.GetOrdinal("SiImage"))
                    ? null
                    : (byte[])reader["SiImage"],
                DrImage = reader.IsDBNull(reader.GetOrdinal("DrImage"))
                    ? null
                    : (byte[])reader["DrImage"],
                PoImage = reader.IsDBNull(reader.GetOrdinal("PoImage"))
                    ? null
                    : (byte[])reader["PoImage"],
                TotalItems = (Ord("TotalItems") >= 0 && !reader.IsDBNull(Ord("TotalItems")))
                    ? (int?)Convert.ToInt32(reader["TotalItems"])
                    : null,
                RenewedItems = (Ord("RenewedItems") >= 0 && !reader.IsDBNull(Ord("RenewedItems")))
                    ? (int?)Convert.ToInt32(reader["RenewedItems"])
                    : null,
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy"))
                    ? (int?)null
                    : reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                ModifiedAt = reader.IsDBNull(reader.GetOrdinal("ModifiedAt"))
                    ? (DateTime?)null
                    : reader.GetDateTime(reader.GetOrdinal("ModifiedAt")),
                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy"))
                    ? (int?)null
                    : reader.GetInt32(reader.GetOrdinal("ModifiedBy"))
            };
        }
    }
}
