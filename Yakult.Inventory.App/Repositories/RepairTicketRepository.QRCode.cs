using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class RepairTicketRepository
    {
        /// <summary>QRToken is always present (NOT NULL, defaulted at the DB via NEWID());
        /// QRImageData is null until the technician generates the code at least once.</summary>
        public async Task<(byte[] QRImageData, Guid QRToken)> GetQrInfoAsync(int repairTicketId)
        {
            const string sql = "SELECT QRToken, QRImageData FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync()) return (null, Guid.Empty);

                        var token = reader.GetGuid(0);
                        var bytes = reader.IsDBNull(1) ? null : (byte[])reader["QRImageData"];
                        return (bytes, token);
                    }
                }
            }
        }

        public async Task UpdateQRDataAsync(int repairTicketId, byte[] qrImageData, string qrDataJson)
        {
            const string sql = @"
UPDATE dbo.RepairTicket
SET QRImageData = @QRImageData,
    QRData = @QRData
WHERE RepairTicketId = @RepairTicketId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    cmd.Parameters.AddWithValue("@QRImageData", (object)qrImageData ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@QRData", (object)qrDataJson ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
