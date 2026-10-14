using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;

namespace Yakult.Inventory.App.Repositories
{
    public sealed class AdminAlertDto
    {
        public int BroadcastId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Surprise admin alerts: one broadcast row per prank, one ack row per
    /// device that already showed it. All methods silently degrade (empty /
    /// zero) when the tables do not exist yet so older databases keep working.
    /// </summary>
    public class AdminAlertRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        private static bool TablesExist(SqlConnection con, SqlTransaction tx = null)
        {
            const string sql = @"
                SELECT CASE WHEN OBJECT_ID('dbo.AdminAlertBroadcast') IS NOT NULL
                             AND OBJECT_ID('dbo.AdminAlertAck') IS NOT NULL
                            THEN 1 ELSE 0 END";
            using (var cmd = tx == null ? new SqlCommand(sql, con) : new SqlCommand(sql, con, tx))
            {
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        public int SendStopAlert(string targetMachine, string targetUser, int createdBy, string createdByName, string title = "STOP", string message = null, int expireMinutes = 10)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    con.Open();
                    if (!TablesExist(con)) return 0;
                    const string sql = @"
                        INSERT INTO dbo.AdminAlertBroadcast
                            (Title, Message, TargetMachine, TargetUser, CreatedBy, CreatedByName, ExpiresAt)
                        VALUES
                            (@Title, @Message, @TargetMachine, @TargetUser, @CreatedBy, @CreatedByName,
                             DATEADD(minute, @ExpireMinutes, SYSUTCDATETIME()));
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Title", (object)title ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Message", (object)message ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TargetMachine", (object)targetMachine ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@TargetUser", (object)targetUser ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@CreatedByName", (object)createdByName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ExpireMinutes", expireMinutes);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[AdminAlert] SendStopAlert failed.", ex);
                return 0;
            }
        }

        public List<AdminAlertDto> GetPendingAlerts(string machineName, string userName)
        {
            var list = new List<AdminAlertDto>();
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    con.Open();
                    if (!TablesExist(con)) return list;
                    const string sql = @"
                        SELECT b.BroadcastId, b.Title, b.Message, b.CreatedByName, b.CreatedAt
                        FROM dbo.AdminAlertBroadcast b
                        WHERE b.ExpiresAt > SYSUTCDATETIME()
                          AND (b.TargetMachine IS NULL OR b.TargetMachine = @MachineName)
                          AND (b.TargetUser IS NULL OR b.TargetUser = @UserName)
                          AND NOT EXISTS (
                                SELECT 1 FROM dbo.AdminAlertAck a
                                WHERE a.BroadcastId = b.BroadcastId
                                  AND a.MachineName = @MachineName
                                  AND a.UserName = @UserName)
                        ORDER BY b.BroadcastId;";
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@MachineName", (object)machineName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserName", (object)userName ?? DBNull.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new AdminAlertDto
                                {
                                    BroadcastId = reader.GetInt32(0),
                                    Title = reader.IsDBNull(1) ? "STOP" : reader.GetString(1),
                                    Message = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    CreatedByName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CreatedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[AdminAlert] GetPendingAlerts failed.", ex);
            }
            return list;
        }

        public void AcknowledgeAlert(int broadcastId, string machineName, string userName)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    con.Open();
                    if (!TablesExist(con)) return;
                    const string sql = @"
                        IF NOT EXISTS (
                            SELECT 1 FROM dbo.AdminAlertAck
                            WHERE BroadcastId = @BroadcastId AND MachineName = @MachineName AND UserName = @UserName)
                        INSERT INTO dbo.AdminAlertAck (BroadcastId, MachineName, UserName)
                        VALUES (@BroadcastId, @MachineName, @UserName);";
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@BroadcastId", broadcastId);
                        cmd.Parameters.AddWithValue("@MachineName", (object)machineName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserName", (object)userName ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[AdminAlert] AcknowledgeAlert failed.", ex);
            }
        }
    }
}
