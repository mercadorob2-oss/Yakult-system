using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Backs the home dashboard's notification bell (Wpf\NotificationCenter) and
    /// Services\HomeNotificationPoller — the exact same three queries, extracted here so
    /// both consumers stay in sync instead of drifting apart as separate copies. The panel
    /// re-runs these on demand each time the bell is opened; the poller re-runs them hourly
    /// to decide whether anything new needs a toast.
    /// </summary>
    public class ExpiryNotificationRepository
    {
        // TOP 10, ≤90 days remaining or already overdue (negative DaysRemaining).
        // IsSet: false = standalone Item, true = Set. Drives navigation destination
        // (Renewal page for sets, Items page for standalone items) in callers.
        public async Task<List<ExpiryNotificationItem>> GetLicenseExpiryAsync(SqlConnection con)
        {
            const string sql = @"
                SELECT TOP 10 ItemName, ItemType, DaysRemaining, IsSet
                FROM (
                    SELECT
                        i.Name AS ItemName,
                        i.ItemType,
                        DATEDIFF(DAY, GETDATE(), i.EndDate) AS DaysRemaining,
                        i.EndDate AS ExpiryDate,
                        CAST(0 AS BIT) AS IsSet
                    FROM dbo.Item i
                    LEFT JOIN dbo.ArchiveStatus arc
                        ON arc.EntityType = 'Item' AND arc.EntityId = i.ItemId AND arc.IsArchived = 1
                    WHERE i.EndDate IS NOT NULL
                      AND i.Active = 1
                      AND arc.ArchiveId IS NULL
                      AND DATEDIFF(DAY, GETDATE(), i.EndDate) <= 90
                      AND i.ItemId NOT IN (
                          SELECT DISTINCT si.ItemId
                          FROM dbo.SetItem si
                          INNER JOIN dbo.[Set] s ON si.SetId = s.SetId
                          WHERE s.Active = 1
                            AND (s.SetType = 'Software/License' OR s.SetType = 'Service')
                      )

                    UNION ALL

                    SELECT
                        s.SetCode AS ItemName,
                        s.SetType AS ItemType,
                        DATEDIFF(DAY, GETDATE(), s.EndDate) AS DaysRemaining,
                        s.EndDate AS ExpiryDate,
                        CAST(1 AS BIT) AS IsSet
                    FROM dbo.[Set] s
                    LEFT JOIN dbo.ArchiveStatus arc
                        ON arc.EntityType = 'Set' AND arc.EntityId = s.SetId AND arc.IsArchived = 1
                    WHERE s.EndDate IS NOT NULL
                      AND s.Active = 1
                      AND arc.ArchiveId IS NULL
                      AND (s.SetType = 'Software/License' OR s.SetType = 'Service')
                      AND DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90
                ) AS CombinedExpiry
                ORDER BY ExpiryDate ASC";

            var result = new List<ExpiryNotificationItem>();
            using (var cmd = new SqlCommand(sql, con))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    result.Add(new ExpiryNotificationItem
                    {
                        ItemName      = reader.GetString(0),
                        ItemType      = reader.IsDBNull(1) ? "Hardware" : reader.GetString(1),
                        DaysRemaining = reader.GetInt32(2),
                        IsSet         = reader.GetBoolean(3),
                    });
                }
            }
            return result;
        }

        // TOP 5, ≤90 days remaining or already overdue.
        // Effective end = Set.EndDate when invoiced, else Item.WarrantyEndDate.
        public async Task<List<ExpiryNotificationItem>> GetWarrantyExpiryAsync(SqlConnection con)
        {
            const string sql = @"
                SELECT TOP 5
                    i.Name AS ItemName,
                    i.ItemType,
                    DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) AS DaysRemaining
                FROM dbo.Item i
                LEFT JOIN dbo.SetItem si  ON i.ItemId = si.ItemId
                LEFT JOIN dbo.[Set]  s    ON si.SetId = s.SetId AND s.IsInvoice = 1
                LEFT JOIN dbo.ArchiveStatus arc
                    ON arc.EntityType = 'Item' AND arc.EntityId = i.ItemId AND arc.IsArchived = 1
                WHERE i.Active = 1
                  AND arc.ArchiveId IS NULL
                  AND (
                      (i.ItemType = 'Software/License' AND (s.EndDate IS NOT NULL OR i.EndDate IS NOT NULL))
                      OR (i.ItemType <> 'Software/License' AND (i.WarrantyYears > 0 OR s.EndDate IS NOT NULL))
                  )
                  AND COALESCE(s.EndDate, i.WarrantyEndDate) IS NOT NULL
                  AND DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) <= 90
                ORDER BY COALESCE(s.EndDate, i.WarrantyEndDate) ASC";

            var result = new List<ExpiryNotificationItem>();
            using (var cmd = new SqlCommand(sql, con))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    result.Add(new ExpiryNotificationItem
                    {
                        ItemName      = reader.GetString(0),
                        ItemType      = reader.IsDBNull(1) ? "Hardware" : reader.GetString(1),
                        DaysRemaining = reader.GetInt32(2),
                    });
                }
            }
            return result;
        }

        // TOP 10 unprocessed mobile status updates, most recent first.
        public async Task<List<MobileUpdateNotificationItem>> GetUnprocessedMobileUpdatesAsync(SqlConnection con)
        {
            const string sql = @"
                SELECT TOP 10
                    u.SetCode,
                    u.SerialNumber,
                    u.NewStatus,
                    u.CreatedAt
                FROM dbo.SetItemUpdate u
                WHERE u.Processed = 0
                ORDER BY u.CreatedAt DESC";

            var result = new List<MobileUpdateNotificationItem>();
            using (var cmd = new SqlCommand(sql, con))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    result.Add(new MobileUpdateNotificationItem
                    {
                        SetCode      = reader.IsDBNull(0) ? null : reader.GetString(0),
                        SerialNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                        NewStatus    = reader.IsDBNull(2) ? null : reader.GetString(2),
                        CreatedAt    = reader.GetDateTime(3),
                    });
                }
            }
            return result;
        }
    }
}
