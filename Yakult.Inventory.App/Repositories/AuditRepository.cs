using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    public class AuditRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public AuditRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task LogAsync(AuditEntry entry)
        {
            const string sql = @"
                INSERT INTO AuditTrail 
                (Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, OldValues, NewValues, IpAddress)
                VALUES 
                (@Action, @EntityId, @EntityType, @UserId, @UserName, @Timestamp, @Notes, @OldValues, @NewValues, @IpAddress)";

            using (var connection = new SqlConnection(GetConnectionString()))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Action", (object)entry.Action ?? DBNull.Value);
                command.Parameters.AddWithValue("@EntityId", (object)entry.EntityId ?? DBNull.Value);
                command.Parameters.AddWithValue("@EntityType", (object)entry.EntityType ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserId", (object)entry.UserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserName", (object)entry.UserName ?? DBNull.Value);
                command.Parameters.AddWithValue("@Timestamp", entry.Timestamp);
                command.Parameters.AddWithValue("@Notes", (object)entry.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("@OldValues", (object)entry.OldValues ?? DBNull.Value);
                command.Parameters.AddWithValue("@NewValues", (object)entry.NewValues ?? DBNull.Value);
                command.Parameters.AddWithValue("@IpAddress", (object)GetIpAddress() ?? DBNull.Value);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<AuditEntry>> GetHistoryAsync(string entityType, int? entityId = null, int top = 200)
        {
            const string sql = @"
                SELECT TOP (@Top) Id, Action, EntityId, EntityType, UserId, UserName, Timestamp, Notes, OldValues, NewValues, IpAddress
                FROM dbo.AuditTrail
                WHERE (@EntityType IS NULL OR EntityType = @EntityType)
                  AND (@EntityId IS NULL OR EntityId = @EntityId)
                ORDER BY Timestamp DESC";

            var results = new List<AuditEntry>();

            using (var connection = new SqlConnection(GetConnectionString()))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@EntityType", (object)entityType ?? DBNull.Value);
                command.Parameters.AddWithValue("@EntityId", (object)entityId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Top", top);

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new AuditEntry
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Action = reader.IsDBNull(reader.GetOrdinal("Action")) ? null : reader.GetString(reader.GetOrdinal("Action")),
                            EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EntityId")),
                            EntityType = reader.IsDBNull(reader.GetOrdinal("EntityType")) ? null : reader.GetString(reader.GetOrdinal("EntityType")),
                            UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("UserId")),
                            UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
                            Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                            OldValues = reader.IsDBNull(reader.GetOrdinal("OldValues")) ? null : reader.GetString(reader.GetOrdinal("OldValues")),
                            NewValues = reader.IsDBNull(reader.GetOrdinal("NewValues")) ? null : reader.GetString(reader.GetOrdinal("NewValues")),
                            IpAddress = reader.IsDBNull(reader.GetOrdinal("IpAddress")) ? null : reader.GetString(reader.GetOrdinal("IpAddress"))
                        });
                    }
                }
            }

            return results;
        }

        private string GetIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}

