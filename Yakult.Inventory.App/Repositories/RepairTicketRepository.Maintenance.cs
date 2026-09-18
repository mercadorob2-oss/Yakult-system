using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>One-time data-repair utilities — not part of the normal Repair Portal workflow.
    /// Triggered manually by a developer via a hidden button (see RepairReportsListViewModel),
    /// not run automatically on any schedule.</summary>
    public sealed partial class RepairTicketRepository
    {
        /// <summary>Re-resizes every existing dbo.RepairTicketAttachment Image row's ThumbnailBytes
        /// from its original FileBytes using the CURRENT CreateThumbnailBytes logic (white-flatten
        /// fix for transparent PNGs, HighQualityBicubic interpolation). Fixes black backgrounds on
        /// thumbnails that were precomputed before that fix existed — Gallery cards and the report
        /// attachment picker both read ThumbnailBytes directly, so neither benefits from the fix
        /// until this has run. dbo.RepairPartAttachment has NO ThumbnailBytes column (per
        /// Migration_RepairPortal_Attachment_ThumbnailBytes.sql — only RepairTicketAttachment got
        /// one) — part-level images are always resized fresh on demand already using the current
        /// logic, so there's nothing stale to regenerate there; this deliberately only touches
        /// RepairTicketAttachment. Processes one row at a time (fetch full bytes, resize, update)
        /// rather than loading every attachment's FileBytes into memory at once — this table can
        /// hold many multi-MB rows. Returns (updated, failed) counts; progress is reported per row.</summary>
        public async Task<(int updated, int failed)> RegenerateAttachmentThumbnailsAsync(System.IProgress<string> progress = null)
        {
            int updated = 0, failed = 0;

            var attachmentIds = await GetImageAttachmentIdsAsync();
            foreach (var id in attachmentIds)
            {
                if (await RegenerateOneAsync(id, progress))
                    updated++;
                else
                    failed++;
            }

            return (updated, failed);
        }

        private async Task<List<int>> GetImageAttachmentIdsAsync()
        {
            var ids = new List<int>();
            const string sql = "SELECT AttachmentId FROM dbo.RepairTicketAttachment WHERE AttachmentType = 'Image'";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        ids.Add(reader.GetInt32(0));
                }
            }

            return ids;
        }

        private async Task<bool> RegenerateOneAsync(int attachmentId, System.IProgress<string> progress)
        {
            try
            {
                byte[] fileBytes;
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand("SELECT FileBytes FROM dbo.RepairTicketAttachment WHERE AttachmentId = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", attachmentId);
                    await con.OpenAsync();
                    var result = await cmd.ExecuteScalarAsync();
                    fileBytes = result as byte[];
                }

                if (fileBytes == null) return false;

                // Same 400px/85% as the original upload-time thumbnail — this call only fixes the
                // black-background/interpolation bug, it isn't meant to change Gallery quality.
                var thumbnail = CreateThumbnailBytes(fileBytes);
                if (thumbnail == null) return false;

                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand("UPDATE dbo.RepairTicketAttachment SET ThumbnailBytes = @Thumb WHERE AttachmentId = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Thumb", thumbnail);
                    cmd.Parameters.AddWithValue("@Id", attachmentId);
                    await con.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }

                progress?.Report($"Attachment #{attachmentId}: updated");
                return true;
            }
            catch (System.Exception ex)
            {
                progress?.Report($"Attachment #{attachmentId}: FAILED — {ex.Message}");
                return false;
            }
        }
    }
}
