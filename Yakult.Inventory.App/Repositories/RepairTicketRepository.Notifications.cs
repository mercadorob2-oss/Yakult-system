using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Scan queries for RepairPortalNotificationPoller. Both return rows within a bounded
    /// lookback window (the poller passes "now minus 12 hours"); the poller itself de-dupes
    /// against dbo.Notification so re-reading the same window every 60s is cheap and harmless.
    ///
    /// Timestamps are compared in UTC (CreatedAt / CompletedAt are stored via SYSUTCDATETIME());
    /// EventAtUtc is returned raw UTC — the poller only uses it for ordering, not display.
    /// </summary>
    public sealed partial class RepairTicketRepository
    {
        public async Task<List<RepairTicketNotificationRow>> GetTicketsCreatedSinceAsync(DateTime sinceUtc)
        {
            const string sql = @"
                SELECT t.RepairTicketId, t.TicketCode, t.ItemNameSnapshot, t.CreatedAt
                FROM dbo.RepairTicket t
                WHERE t.CreatedAt >= @SinceUtc
                ORDER BY t.CreatedAt ASC";

            return await ReadNotificationRowsAsync(sql, sinceUtc);
        }

        public async Task<List<RepairTicketNotificationRow>> GetTicketsCompletedSinceAsync(DateTime sinceUtc)
        {
            const string sql = @"
                SELECT t.RepairTicketId, t.TicketCode, t.ItemNameSnapshot, t.CompletedAt
                FROM dbo.RepairTicket t
                WHERE t.Status = 'Completed'
                  AND t.CompletedAt IS NOT NULL
                  AND t.CompletedAt >= @SinceUtc
                ORDER BY t.CompletedAt ASC";

            return await ReadNotificationRowsAsync(sql, sinceUtc);
        }

        private static async Task<List<RepairTicketNotificationRow>> ReadNotificationRowsAsync(string sql, DateTime sinceUtc)
        {
            var list = new List<RepairTicketNotificationRow>();

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SinceUtc",
                    DateTime.SpecifyKind(sinceUtc, DateTimeKind.Unspecified));

                await con.OpenAsync();
                using (var reader = (SqlDataReader)await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new RepairTicketNotificationRow
                        {
                            RepairTicketId = reader.GetInt32(0),
                            TicketCode     = GetStringOrNull(reader, 1),
                            ItemName       = GetStringOrNull(reader, 2),
                            EventAtUtc     = reader.GetDateTime(3),
                        });
                    }
                }
            }

            return list;
        }
    }
}
