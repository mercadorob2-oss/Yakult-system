using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class RepairTicketRepository
    {
        /// <summary>Assembles the raw data the printable Repair Report needs for one ticket.
        /// Deliberately does no "is this worth printing" filtering/formatting here — that decision
        /// (dedup, N/A handling, which sections to show) lives in RepairReportBuilder, which owns
        /// the report's presentation. Returns null if the ticket doesn't exist.</summary>
        public async Task<RepairReportData> GetReportDataAsync(int repairTicketId, string generatedByName)
        {
            var detail = await GetTicketDetailAsync(repairTicketId);
            if (detail == null) return null;

            var itemLookup = await GetItemLookupByIdAsync(detail.ItemId);

            var data = new RepairReportData
            {
                RepairTicketId = detail.RepairTicketId,
                TicketCode = detail.TicketCode,
                Status = detail.Status,
                CreatedAt = detail.CreatedAt,
                CompletedAt = detail.CompletedAt,
                GeneratedAt = DateTime.Now,
                GeneratedByName = generatedByName,
                TechnicianName = detail.AssignedTechName,

                ItemName = detail.ItemNameSnapshot ?? itemLookup?.Name,
                Category = detail.Category,
                ModelNumber = itemLookup?.ModelNumber,
                SerialNumber = detail.ItemSerialSnapshot ?? itemLookup?.SerialNumber,
                DeptName = detail.RequestedByDeptName ?? detail.DeptName,
                DeptId = detail.RequestedByDeptId,
                BranchName = detail.RequestedByBranchName ?? detail.BranchName,
                AssignedUserName = detail.RequestedByEmpName ?? detail.RequestedByDeptName,

                ProblemFreeText = detail.Problem,
                WorkPerformed = detail.Conclusion?.WorkPerformed,
                RootCause = detail.Conclusion?.RootCause,
                ResolutionSummary = detail.Conclusion?.FinalOutcome,
                Recommendations = detail.Conclusion?.Recommendations,
                HandedOverToVendorName = detail.Conclusion?.HandedOverToVendorName
            };

            if (detail.Conclusion?.RepairedByNames != null)
                data.RepairedByNames.AddRange(detail.Conclusion.RepairedByNames);

            foreach (var o in detail.Observations.OrderBy(o => o.SortOrder))
                data.ObservationTexts.Add(o.ObservationText);

            foreach (var part in detail.Parts.OrderBy(p => p.PartNumber))
            {
                data.Parts.Add(new RepairReportPartSection
                {
                    PartDisplayName = part.PartDisplayName,
                    Status = part.Status
                });
            }

            data.Attachments = await GetReportAttachmentRowsAsync(repairTicketId);
            data.ItemDispositionText = await BuildItemDispositionTextAsync(repairTicketId, detail.Conclusion);

            return data;
        }

        /// <summary>Combines the "Item Replacement" (Unrepairable disposition) and/or "Spare
        /// Assigned" (temporary loaner while Repairing) lines into one report block — either, both,
        /// or neither may apply to a given ticket. See RepairTicketRepository.Disposition.cs and
        /// RepairTicketRepository.SpareAssignment.cs for where each is recorded.</summary>
        private async Task<string> BuildItemDispositionTextAsync(int repairTicketId, RepairConclusion conclusion)
        {
            var lines = new List<string>();

            if (string.Equals(conclusion?.Disposition, "Replace", StringComparison.OrdinalIgnoreCase) && conclusion.ReplacementItemId.HasValue)
            {
                var replacementLookup = await GetItemLookupByIdAsync(conclusion.ReplacementItemId.Value);
                lines.Add(FormatDispositionLine("Item Replacement", conclusion.ReplacementItemName ?? replacementLookup?.Name,
                    replacementLookup?.ModelNumber, replacementLookup?.SerialNumber));
            }

            var spare = await GetMostRecentSpareAsync(repairTicketId);
            if (spare != null)
                lines.Add(FormatDispositionLine("Spare Assigned", spare.ItemName, spare.ModelNumber, spare.SerialNumber));

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        private static string FormatDispositionLine(string role, string name, string modelNumber, string serialNumber)
        {
            var text = $"{role} — {name ?? "(unknown item)"}";
            var hasModel = !string.IsNullOrWhiteSpace(modelNumber);
            var hasSerial = !string.IsNullOrWhiteSpace(serialNumber);
            if (hasModel || hasSerial)
            {
                text += " (";
                if (hasModel) text += $"Model: {modelNumber}";
                if (hasModel && hasSerial) text += ", ";
                if (hasSerial) text += $"Serial: {serialNumber}";
                text += ")";
            }
            return text;
        }

        /// <summary>Small dedicated query for report thumbnails — pulls Image-type attachments from
        /// both ticket-level and part-level evidence in one shot. dbo.RepairTicketAttachment already
        /// has a precomputed ThumbnailBytes column (added for the Gallery list view's own latency
        /// fix — see Migration_RepairPortal_Attachment_ThumbnailBytes.sql) which is used directly
        /// when present, so the full multi-MB FileBytes is never even streamed back for those rows.
        /// dbo.RepairPartAttachment has no such column yet, so part-level images (and any legacy
        /// ticket-level image uploaded before that migration) still fall back to pulling FileBytes
        /// and resizing in C#. This matters far more for the batch/requester-grouped report, which
        /// calls this once per selected ticket — the old "resize cost is negligible for one ticket"
        /// assumption doesn't hold once it's paid N times in a row.</summary>
        private async Task<List<RepairReportAttachmentRow>> GetReportAttachmentRowsAsync(int repairTicketId)
        {
            var rows = new List<RepairReportAttachmentRow>();

            const string sql = @"
SELECT FileName, AttachmentType, ThumbnailBytes,
       CASE WHEN ThumbnailBytes IS NULL THEN FileBytes ELSE NULL END AS FallbackFileBytes,
       'Whole-Equipment' AS SourceLabel, AttachmentId, CAST(0 AS BIT) AS IsPartAttachment
FROM dbo.RepairTicketAttachment WHERE RepairTicketId = @RepairTicketId
UNION ALL
SELECT pa.FileName, pa.AttachmentType, NULL AS ThumbnailBytes, pa.FileBytes AS FallbackFileBytes,
       p.PartDisplayName AS SourceLabel, pa.PartAttachmentId AS AttachmentId, CAST(1 AS BIT) AS IsPartAttachment
FROM dbo.RepairPartAttachment pa
INNER JOIN dbo.RepairPart p ON p.RepairPartId = pa.RepairPartId
WHERE pa.RepairTicketId = @RepairTicketId
ORDER BY SourceLabel, AttachmentType, FileName;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var attachmentType = GetStringOrNull(reader, 1);
                        byte[] precomputedThumbnail = reader.IsDBNull(2) ? null : (byte[])reader[2];
                        byte[] fallbackFileBytes = reader.IsDBNull(3) ? null : (byte[])reader[3];

                        byte[] thumbnail = null;
                        if (string.Equals(attachmentType, "Image", StringComparison.OrdinalIgnoreCase))
                            thumbnail = precomputedThumbnail ?? CreateThumbnailBytes(fallbackFileBytes);

                        rows.Add(new RepairReportAttachmentRow
                        {
                            FileName = GetStringOrNull(reader, 0),
                            AttachmentType = attachmentType,
                            ThumbnailBytes = thumbnail,
                            SourceLabel = GetStringOrNull(reader, 4),
                            AttachmentId = reader.GetInt32(5),
                            IsPartAttachment = reader.GetBoolean(6)
                        });
                    }
                }
            }

            return rows;
        }

        /// <summary>Fetches ONE attachment's full-resolution FileBytes — used only for the handful
        /// of images a technician actually picks in RepairReportAttachmentPickerDialog, to rebuild
        /// their report montage entry at print quality instead of reusing the small (400px/85%)
        /// Gallery thumbnail. Never called for attachments that aren't part of a confirmed
        /// selection, so this never touches the bulk-listing/Gallery load path.</summary>
        public async Task<byte[]> GetFullAttachmentBytesAsync(int attachmentId, bool isPartAttachment)
        {
            var sql = isPartAttachment
                ? "SELECT FileBytes FROM dbo.RepairPartAttachment WHERE PartAttachmentId = @Id"
                : "SELECT FileBytes FROM dbo.RepairTicketAttachment WHERE AttachmentId = @Id";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", attachmentId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? null : (byte[])result;
            }
        }

        /// <summary>Re-resizes a report attachment row from its original full-resolution source at
        /// print quality (larger cap + higher JPEG quality than the 400px/85% Gallery thumbnail),
        /// replacing its ThumbnailBytes in place. Only ever called for rows the technician actually
        /// confirmed in the picker, so this stays cheap even though it's a full-resolution fetch.
        ///
        /// Side effect (whole-equipment attachments only, since only dbo.RepairTicketAttachment has
        /// a ThumbnailBytes column to persist to): also regenerates and PERSISTS a fresh 400px/85%
        /// Gallery-quality thumbnail from the same full-resolution source, using the same
        /// already-fixed CreateThumbnailBytes logic. This is deliberate — it means generating a
        /// report for a ticket organically self-heals that attachment's stale/black-background
        /// thumbnail for Gallery cards and the picker's own preview too, without needing the
        /// dev-only RegenerateAttachmentThumbnailsAsync bulk tool for every ticket someone happens
        /// to print a report for.</summary>
        public async Task UpgradeAttachmentForPrintAsync(RepairReportAttachmentRow row)
        {
            if (row == null) return;
            try
            {
                var fullBytes = await GetFullAttachmentBytesAsync(row.AttachmentId, row.IsPartAttachment);
                if (fullBytes == null) return;

                var upgraded = CreateThumbnailBytes(fullBytes, maxDimension: 900, quality: 95L);
                if (upgraded != null) row.ThumbnailBytes = upgraded;

                if (!row.IsPartAttachment)
                {
                    var galleryThumbnail = CreateThumbnailBytes(fullBytes);
                    if (galleryThumbnail != null)
                    {
                        using (var con = new SqlConnection(GetConnectionString()))
                        using (var cmd = new SqlCommand("UPDATE dbo.RepairTicketAttachment SET ThumbnailBytes = @Thumb WHERE AttachmentId = @Id", con))
                        {
                            cmd.Parameters.AddWithValue("@Thumb", galleryThumbnail);
                            cmd.Parameters.AddWithValue("@Id", row.AttachmentId);
                            await con.OpenAsync();
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch
            {
                // Non-critical — the report still renders using whatever thumbnail it already had.
            }
        }
    }
}
