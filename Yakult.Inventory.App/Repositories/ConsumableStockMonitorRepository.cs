using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Read-only snapshot source for the Admin Portal's Consumable Stock Monitor.
    /// Returns one row per active dbo.Item linked to a ConsumableModel or a CartridgeModel,
    /// with its current StockOnHand. The monitor polls this on a timer and diffs successive
    /// snapshots to produce its live change feed — no schema changes, no history table.
    /// </summary>
    public class ConsumableStockMonitorRepository
    {
        private readonly string _connectionString;

        public ConsumableStockMonitorRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        public async Task<List<ConsumableStockItemDto>> GetItemStockSnapshotAsync()
        {
            var rows = new List<ConsumableStockItemDto>();

            const string sql = @"
                SELECT
                    i.ItemId,
                    i.Name AS ItemName,
                    COALESCE(com.ModelNumber, crm.ModelNumber, i.ModelNumber) AS ModelNumber,
                    CASE
                        WHEN i.CartridgeModelId IS NOT NULL THEN 'Cartridge'
                        WHEN com.Category IS NOT NULL       THEN com.Category
                        ELSE i.Category
                    END AS RawCategory,
                    i.StockOnHand,
                    i.ConsumableModelId,
                    i.CartridgeModelId
                FROM dbo.Item i
                LEFT JOIN dbo.ConsumableModel com ON i.ConsumableModelId = com.ConsumableModelId
                LEFT JOIN dbo.CartridgeModel  crm ON i.CartridgeModelId  = crm.CartridgeModelId
                WHERE i.Active = 1
                  AND (i.ConsumableModelId IS NOT NULL OR i.CartridgeModelId IS NOT NULL)
                ORDER BY i.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add(new ConsumableStockItemDto
                        {
                            ItemId            = reader.GetInt32(0),
                            ItemName          = reader.IsDBNull(1) ? null : reader.GetString(1),
                            ModelNumber       = reader.IsDBNull(2) ? null : reader.GetString(2),
                            RawCategory       = reader.IsDBNull(3) ? null : reader.GetString(3),
                            StockOnHand       = reader.GetInt32(4),
                            ConsumableModelId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                            CartridgeModelId  = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                        });
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// Posted stock movements for consumable + cartridge items in a time window — backs
        /// the monitor's "Today" / "Yesterday" / "Last 7 days" ranges. Three sources merge:
        ///   • dbo.Inventory ledger  (Positive / Negative / Fixed Assets entries, any item)
        ///   • dbo.CartridgeMovement (Issued / Returned / StockIn / RefillIn / Adjustment)
        ///   • dbo.Request outstanding lines (Quantity not yet fully issued) — flagged
        ///     IsPending, so combo / assisted-request sets show up before they are fulfilled.
        ///     The ledger only gets rows at fulfilment (Set dispatch / cartridge issue), so a
        ///     still-Pending request otherwise shows nothing.
        /// Items are classified by free-text Category / ItemType (same LIKE logic as the
        /// orphan diagnostics), NOT by a ConsumableModelId / CartridgeModelId FK — most
        /// items with movement history are not linked to a model. All three timestamp
        /// columns are treated as server-local wall-clock (dbo.Inventory / dbo.CartridgeMovement
        /// use GETDATE / SYSDATETIME; the portal request flow writes dbo.Request.DateCreated
        /// as DateTime.Now). <paramref name="from"/> / <paramref name="to"/> are local,
        /// half-open [from, to).
        /// </summary>
        public async Task<List<ConsumableStockHistoryDto>> GetHistoryAsync(DateTime from, DateTime to)
        {
            var rows = new List<ConsumableStockHistoryDto>();

            // The category test shared by both sources — Ink / Toner / Printhead / Cartridge.
            const string consumableItemPredicate = @"(
                    i.ConsumableModelId IS NOT NULL
                 OR i.CartridgeModelId IS NOT NULL
                 OR i.ItemType = 'Cartridge'
                 OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
                 OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
                 OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
                 OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%cartridge%'
                  )";

            string sql = $@"
;WITH Ledger AS (
    SELECT
        inv.DatePosted AS EventAt,
        i.ItemId,
        i.Name AS ItemName,
        COALESCE(com.ModelNumber, crm.ModelNumber, i.ModelNumber) AS ModelNumber,
        CASE WHEN i.CartridgeModelId IS NOT NULL AND (i.Category IS NULL OR i.Category = '') THEN 'Cartridge'
             WHEN com.Category IS NOT NULL THEN com.Category
             ELSE i.Category END AS RawCategory,
        CASE WHEN i.CartridgeModelId IS NOT NULL OR i.ItemType = 'Cartridge' THEN 1 ELSE 0 END AS CartridgeLinked,
        inv.EntryType,
        CASE WHEN inv.EntryType = 'Negative' THEN -inv.Quantity ELSE inv.Quantity END AS SignedQty,
        inv.Description,
        u.Name AS PostedByName,
        CAST(0 AS BIT) AS IsPending
    FROM dbo.Inventory inv
    INNER JOIN dbo.Item i ON inv.ItemId = i.ItemId
    LEFT JOIN dbo.ConsumableModel com ON i.ConsumableModelId = com.ConsumableModelId
    LEFT JOIN dbo.CartridgeModel  crm ON i.CartridgeModelId  = crm.CartridgeModelId
    LEFT JOIN dbo.[User] u ON inv.PostedBy = u.UserId
    WHERE inv.Active = 1
      AND inv.DatePosted >= @From AND inv.DatePosted < @To
      AND {consumableItemPredicate}
),
Movement AS (
    SELECT
        cmv.CreatedAt AS EventAt,
        i.ItemId,
        i.Name AS ItemName,
        COALESCE(crm.ModelNumber, i.ModelNumber) AS ModelNumber,
        CASE WHEN i.Category IS NULL OR i.Category = '' THEN 'Cartridge' ELSE i.Category END AS RawCategory,
        1 AS CartridgeLinked,
        cmv.MovementType AS EntryType,
        CASE WHEN cmv.MovementType = 'Issued' THEN -cmv.Quantity ELSE cmv.Quantity END AS SignedQty,
        cmv.Remarks AS Description,
        u.Name AS PostedByName,
        CAST(0 AS BIT) AS IsPending
    FROM dbo.CartridgeMovement cmv
    INNER JOIN dbo.Item i ON cmv.ItemId = i.ItemId
    LEFT JOIN dbo.CartridgeModel crm ON i.CartridgeModelId = crm.CartridgeModelId
    LEFT JOIN dbo.[User] u ON cmv.CreatedBy = u.UserId
    WHERE cmv.CreatedAt >= @From AND cmv.CreatedAt < @To
),
Requested AS (
    SELECT
        r.DateCreated AS EventAt,
        i.ItemId,
        i.Name AS ItemName,
        COALESCE(com.ModelNumber, crm.ModelNumber, i.ModelNumber) AS ModelNumber,
        CASE WHEN i.CartridgeModelId IS NOT NULL AND (i.Category IS NULL OR i.Category = '') THEN 'Cartridge'
             WHEN com.Category IS NOT NULL THEN com.Category
             ELSE i.Category END AS RawCategory,
        CASE WHEN i.CartridgeModelId IS NOT NULL OR i.ItemType = 'Cartridge' THEN 1 ELSE 0 END AS CartridgeLinked,
        'Requested (' + ISNULL(NULLIF(LTRIM(RTRIM(r.Status)), ''), 'Pending') + ')' AS EntryType,
        -(r.Quantity - ISNULL(r.IssuedQty, 0)) AS SignedQty,
        'PENDING - Request #' + CAST(r.ReqId AS varchar(12))
            + CASE WHEN s.SetCode IS NOT NULL AND s.SetCode <> '' THEN ' (' + s.SetCode + ')' ELSE '' END
            + CASE WHEN r.Description IS NOT NULL AND LTRIM(RTRIM(r.Description)) <> '' THEN ' - ' + r.Description ELSE '' END AS Description,
        u.Name AS PostedByName,
        CAST(1 AS BIT) AS IsPending
    FROM dbo.Request r
    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
    LEFT JOIN dbo.ConsumableModel com ON i.ConsumableModelId = com.ConsumableModelId
    LEFT JOIN dbo.CartridgeModel  crm ON i.CartridgeModelId  = crm.CartridgeModelId
    LEFT JOIN dbo.[Set] s ON r.SetId = s.SetId
    LEFT JOIN dbo.[User] u ON r.CreatedBy = u.UserId
    WHERE r.Active = 1
      AND (r.Quantity - ISNULL(r.IssuedQty, 0)) > 0
      -- dbo.Request.DateCreated is written as server-local wall-clock by the portal flow
      -- (RequesterPortalService passes DateTime.Now), same as dbo.Inventory / dbo.CartridgeMovement.
      AND r.DateCreated >= @From AND r.DateCreated < @To
      AND {consumableItemPredicate}
)
SELECT EventAt, ItemId, ItemName, ModelNumber, RawCategory, CartridgeLinked, EntryType, SignedQty, Description, PostedByName, IsPending
FROM (
    SELECT * FROM Ledger
    UNION ALL
    SELECT * FROM Movement
    UNION ALL
    SELECT * FROM Requested
) x
ORDER BY EventAt DESC";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.Add("@From", SqlDbType.DateTime2).Value = from;
                    cmd.Parameters.Add("@To",   SqlDbType.DateTime2).Value = to;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rows.Add(new ConsumableStockHistoryDto
                            {
                                PostedAtLocal   = reader.GetDateTime(0),
                                ItemId          = reader.GetInt32(1),
                                ItemName        = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ModelNumber     = reader.IsDBNull(3) ? null : reader.GetString(3),
                                RawCategory     = reader.IsDBNull(4) ? null : reader.GetString(4),
                                CartridgeLinked = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                                EntryType       = reader.IsDBNull(6) ? null : reader.GetString(6),
                                SignedQty       = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                                Description     = reader.IsDBNull(8) ? null : reader.GetString(8),
                                PostedByName    = reader.IsDBNull(9) ? null : reader.GetString(9),
                                IsPending       = !reader.IsDBNull(10) && reader.GetBoolean(10),
                            });
                        }
                    }
                }
            }

            return rows;
        }
    }
}
