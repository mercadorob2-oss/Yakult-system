using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>Part-based repair workflow: Observations, Parts, Part notes/attachments/history,
    /// Repair Conclusion, and the aggregate Evidence Gallery. Reads are plain parameterized SQL;
    /// writes go through the sp_RepairPortal_* stored procs (see
    /// Migration_RepairPortal_PartWorkflow_StoredProcs.sql).</summary>
    public sealed partial class RepairTicketRepository
    {
        // ── Observations ─────────────────────────────────────────────────────

        public async Task<List<RepairItemObservation>> GetObservationsAsync(int repairTicketId)
        {
            const string sql = @"
SELECT o.ObservationId, o.RepairTicketId, o.SortOrder, o.ObservationText, o.CreatedAt, o.UpdatedAt, u.Name AS CreatedByName
FROM dbo.RepairItemObservation o
LEFT JOIN dbo.[User] u ON u.UserId = o.CreatedByUserId
WHERE o.RepairTicketId = @RepairTicketId
ORDER BY o.SortOrder ASC, o.ObservationId ASC;";

            var list = new List<RepairItemObservation>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        list.Add(MapObservation(reader));
                }
            }

            return list;
        }

        private static RepairItemObservation MapObservation(SqlDataReader reader)
        {
            return new RepairItemObservation
            {
                ObservationId = reader.GetInt32(0),
                RepairTicketId = reader.GetInt32(1),
                SortOrder = reader.GetInt32(2),
                ObservationText = GetStringOrNull(reader, 3),
                CreatedAt = GetLocalDateTime(reader, 4),
                UpdatedAt = GetLocalDateTime(reader, 5),
                CreatedByName = GetStringOrNull(reader, 6)
            };
        }

        public async Task AddObservationAsync(int repairTicketId, string text, int? createdByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_AddObservation", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@ObservationText", text);
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task UpdateObservationAsync(int observationId, string text)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_UpdateObservation", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@ObservationId", observationId);
                cmd.Parameters.AddWithValue("@ObservationText", text);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteObservationAsync(int observationId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_DeleteObservation", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@ObservationId", observationId);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>No dedicated stored proc — dbo.IntIdList only carries bare IDs, not order, so
        /// reordering is cheap enough as N sequential UPDATEs inside one SqlTransaction.</summary>
        public async Task ReorderObservationsAsync(int repairTicketId, List<int> orderedObservationIds)
        {
            if (orderedObservationIds == null || orderedObservationIds.Count == 0) return;

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tran = con.BeginTransaction())
                {
                    try
                    {
                        for (var i = 0; i < orderedObservationIds.Count; i++)
                        {
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.RepairItemObservation SET SortOrder = @SortOrder WHERE ObservationId = @ObservationId AND RepairTicketId = @RepairTicketId",
                                con, tran))
                            {
                                cmd.Parameters.AddWithValue("@SortOrder", i);
                                cmd.Parameters.AddWithValue("@ObservationId", orderedObservationIds[i]);
                                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        // ── Parts ────────────────────────────────────────────────────────────

        /// <summary>Days spent in the CURRENT status only — the most recent Status history row (or
        /// CreatedAt if none yet), through to now, or frozen at UpdatedAt once the part reaches a
        /// terminal status (Part has no CompletedAt column). Shared by GetPartsAsync/GetPartDetailAsync.</summary>
        private const string PartDaysInCurrentStatusSql = @"
    DATEDIFF(day,
        COALESCE(
            (SELECT TOP (1) h.ChangedAt FROM dbo.RepairPartHistory h
             WHERE h.RepairPartId = p.RepairPartId AND h.FieldName = 'Status'
             ORDER BY h.ChangedAt DESC, h.PartHistoryId DESC),
            p.CreatedAt
        ),
        CASE WHEN p.Status IN ('Repaired', 'CannotRepair') THEN p.UpdatedAt ELSE SYSUTCDATETIME() END
    ) AS DaysInCurrentStatus";

        public async Task<List<RepairPart>> GetPartsAsync(int repairTicketId)
        {
            const string sql = @"
SELECT
    p.RepairPartId, p.RepairTicketId, p.PartNumber, p.CustomLabel, p.PartDisplayName,
    p.ProblemDescription, p.Status, p.Severity, p.CreatedAt, p.UpdatedAt,
    ISNULL(att.ImageCount, 0), ISNULL(att.VideoCount, 0), ISNULL(att.DocumentCount, 0), ISNULL(nt.NoteCount, 0),
" + PartDaysInCurrentStatusSql + @"
FROM dbo.RepairPart p
OUTER APPLY (
    SELECT
        SUM(CASE WHEN a.AttachmentType = 'Image' THEN 1 ELSE 0 END) AS ImageCount,
        SUM(CASE WHEN a.AttachmentType = 'Video' THEN 1 ELSE 0 END) AS VideoCount,
        SUM(CASE WHEN a.AttachmentType = 'Document' THEN 1 ELSE 0 END) AS DocumentCount
    FROM dbo.RepairPartAttachment a
    WHERE a.RepairPartId = p.RepairPartId
) att
OUTER APPLY (
    SELECT COUNT(*) AS NoteCount
    FROM dbo.RepairPartNote n
    WHERE n.RepairPartId = p.RepairPartId
) nt
WHERE p.RepairTicketId = @RepairTicketId
ORDER BY p.PartNumber ASC;";

            var list = new List<RepairPart>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        list.Add(MapPart(reader));
                }
            }

            return list;
        }

        private static RepairPart MapPart(SqlDataReader reader)
        {
            return new RepairPart
            {
                RepairPartId = reader.GetInt32(0),
                RepairTicketId = reader.GetInt32(1),
                PartNumber = reader.GetInt32(2),
                CustomLabel = GetStringOrNull(reader, 3),
                PartDisplayName = GetStringOrNull(reader, 4),
                ProblemDescription = GetStringOrNull(reader, 5),
                Status = GetStringOrNull(reader, 6),
                Severity = GetStringOrNull(reader, 7),
                CreatedAt = GetLocalDateTime(reader, 8),
                UpdatedAt = GetLocalDateTime(reader, 9),
                ImageCount = reader.GetInt32(10),
                VideoCount = reader.GetInt32(11),
                DocumentCount = reader.GetInt32(12),
                NoteCount = reader.GetInt32(13),
                DaysInCurrentStatus = reader.GetInt32(14)
            };
        }

        public async Task<RepairPartDetail> GetPartDetailAsync(int repairPartId)
        {
            const string headerSql = @"
SELECT
    p.RepairPartId, p.RepairTicketId, p.PartNumber, p.CustomLabel, p.PartDisplayName,
    p.ProblemDescription, p.Status, p.Severity, p.CreatedAt, p.UpdatedAt,
    ISNULL(att.ImageCount, 0), ISNULL(att.VideoCount, 0), ISNULL(att.DocumentCount, 0), ISNULL(nt.NoteCount, 0),
" + PartDaysInCurrentStatusSql + @"
FROM dbo.RepairPart p
OUTER APPLY (
    SELECT
        SUM(CASE WHEN a.AttachmentType = 'Image' THEN 1 ELSE 0 END) AS ImageCount,
        SUM(CASE WHEN a.AttachmentType = 'Video' THEN 1 ELSE 0 END) AS VideoCount,
        SUM(CASE WHEN a.AttachmentType = 'Document' THEN 1 ELSE 0 END) AS DocumentCount
    FROM dbo.RepairPartAttachment a
    WHERE a.RepairPartId = p.RepairPartId
) att
OUTER APPLY (
    SELECT COUNT(*) AS NoteCount
    FROM dbo.RepairPartNote n
    WHERE n.RepairPartId = p.RepairPartId
) nt
WHERE p.RepairPartId = @RepairPartId;";

            const string notesSql = @"
SELECT n.PartNoteId, n.RepairPartId, n.RepairTicketId, n.NoteType, n.NoteText, n.CreatedAt,
       COALESCE(u.Name, emp.Name) AS CreatedByName
FROM dbo.RepairPartNote n
LEFT JOIN dbo.[User] u ON u.UserId = n.CreatedByUserId
LEFT JOIN dbo.Employee emp ON emp.EmpId = n.CreatedByEmpId
WHERE n.RepairPartId = @RepairPartId
ORDER BY n.CreatedAt DESC, n.PartNoteId DESC;";

            const string attachmentsSql = @"
SELECT a.PartAttachmentId, a.RepairPartId, a.RepairTicketId, a.AttachmentType, a.FileName, a.MimeType,
       a.FileSizeBytes, a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairPartAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.RepairPartId = @RepairPartId
ORDER BY a.SortOrder ASC, a.PartAttachmentId ASC;";

            const string historySql = @"
SELECT h.PartHistoryId, h.RepairPartId, h.RepairTicketId, h.ChangedAt, h.ChangedByUserId, u.Name AS ChangedByName,
       h.FieldName, h.OldValue, h.NewValue, h.Note
FROM dbo.RepairPartHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.RepairPartId = @RepairPartId
ORDER BY h.ChangedAt DESC, h.PartHistoryId DESC;";

            RepairPartDetail detail = null;

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(headerSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var part = MapPart(reader);
                            detail = new RepairPartDetail
                            {
                                RepairPartId = part.RepairPartId,
                                RepairTicketId = part.RepairTicketId,
                                PartNumber = part.PartNumber,
                                CustomLabel = part.CustomLabel,
                                PartDisplayName = part.PartDisplayName,
                                ProblemDescription = part.ProblemDescription,
                                Status = part.Status,
                                Severity = part.Severity,
                                CreatedAt = part.CreatedAt,
                                UpdatedAt = part.UpdatedAt,
                                ImageCount = part.ImageCount,
                                VideoCount = part.VideoCount,
                                DocumentCount = part.DocumentCount,
                                NoteCount = part.NoteCount
                            };
                        }
                    }
                }

                if (detail == null)
                    return null;

                using (var cmd = new SqlCommand(notesSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var note = new RepairPartNoteItem
                            {
                                NoteId = reader.GetInt64(0),
                                RepairPartId = reader.GetInt32(1),
                                RepairTicketId = reader.GetInt32(2),
                                NoteType = GetStringOrNull(reader, 3),
                                NoteText = GetStringOrNull(reader, 4),
                                CreatedAt = GetLocalDateTime(reader, 5),
                                CreatedByName = GetStringOrNull(reader, 6)
                            };

                            if (string.Equals(note.NoteType, "Repair", StringComparison.OrdinalIgnoreCase))
                                detail.RepairNotes.Add(note);
                            else
                                detail.DiagnosticNotes.Add(note);
                        }
                    }
                }

                using (var cmd = new SqlCommand(attachmentsSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.Attachments.Add(new RepairPartAttachmentSummary
                            {
                                PartAttachmentId = reader.GetInt32(0),
                                RepairPartId = reader.GetInt32(1),
                                RepairTicketId = reader.GetInt32(2),
                                AttachmentType = GetStringOrNull(reader, 3),
                                FileName = GetStringOrNull(reader, 4),
                                MimeType = GetStringOrNull(reader, 5),
                                FileSizeBytes = GetIntOrNull(reader, 6),
                                SortOrder = reader.GetInt32(7),
                                UploadedAt = GetLocalDateTime(reader, 8),
                                UploadedByName = GetStringOrNull(reader, 9)
                            });
                        }
                    }
                }

                using (var cmd = new SqlCommand(historySql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.History.Add(new RepairPartHistoryItem
                            {
                                PartHistoryId = reader.GetInt64(0),
                                RepairPartId = reader.GetInt32(1),
                                RepairTicketId = reader.GetInt32(2),
                                ChangedAt = GetLocalDateTime(reader, 3),
                                ChangedByUserId = GetIntOrNull(reader, 4),
                                ChangedByName = GetStringOrNull(reader, 5),
                                FieldName = GetStringOrNull(reader, 6),
                                OldValue = GetStringOrNull(reader, 7),
                                NewValue = GetStringOrNull(reader, 8),
                                Note = GetStringOrNull(reader, 9)
                            });
                        }
                    }
                }
            }

            return detail;
        }

        public async Task<RepairPart> CreatePartAsync(NewPartRequest request, int? createdByUserId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_CreatePart", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", request.RepairTicketId);
                cmd.Parameters.AddWithValue("@CustomLabel", OrDbNull(request.CustomLabel));
                cmd.Parameters.AddWithValue("@ProblemDescription", OrDbNull(request.ProblemDescription));
                cmd.Parameters.AddWithValue("@Severity", OrDbNull(request.Severity));
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RepairPart
                        {
                            RepairPartId = reader.GetInt32(0),
                            RepairTicketId = reader.GetInt32(1),
                            PartNumber = reader.GetInt32(2),
                            CustomLabel = GetStringOrNull(reader, 3),
                            PartDisplayName = GetStringOrNull(reader, 4),
                            ProblemDescription = GetStringOrNull(reader, 5),
                            Status = GetStringOrNull(reader, 6),
                            Severity = GetStringOrNull(reader, 7),
                            CreatedAt = GetLocalDateTime(reader, 8),
                            UpdatedAt = GetLocalDateTime(reader, 9)
                        };
                    }
                }
            }

            throw new InvalidOperationException("Failed to create part.");
        }

        /// <summary>RepairPart has no CompletedAt column, so for a terminal status (Repaired /
        /// CannotRepair) with a chosen completion date, the date is recorded as text appended to
        /// the history Note — no proc signature change needed for this, sp_RepairPortal_SetPartStatus
        /// already has @Note.</summary>
        public async Task SetPartStatusAsync(int repairPartId, string newStatus, int? changedByUserId, string note, DateTime? completedDate = null)
        {
            var isTerminal = string.Equals(newStatus, "Repaired", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(newStatus, "CannotRepair", StringComparison.OrdinalIgnoreCase);

            var effectiveNote = note;
            if (isTerminal && completedDate.HasValue)
            {
                var suffix = "Marked " + newStatus + " (completed " + completedDate.Value.ToString("yyyy-MM-dd") + ")";
                effectiveNote = string.IsNullOrWhiteSpace(note) ? suffix : note.Trim() + " — " + suffix;
            }

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_SetPartStatus", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));
                cmd.Parameters.AddWithValue("@Note", OrDbNull(effectiveNote));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task UpdatePartLabelAsync(int repairPartId, string newLabel, int? changedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_UpdatePartLabel", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                cmd.Parameters.AddWithValue("@NewCustomLabel", OrDbNull(newLabel));
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DeletePartAsync(int repairPartId, int? changedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_DeletePart", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task AddPartNoteAsync(int repairPartId, string noteType, string noteText, int? createdByUserId, int? createdByEmpId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_AddPartNote", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                cmd.Parameters.AddWithValue("@NoteType", noteType);
                cmd.Parameters.AddWithValue("@NoteText", noteText);
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@CreatedByEmpId", OrDbNull(createdByEmpId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> AddPartAttachmentAsync(int repairPartId, string attachmentType, string fileName, string mimeType, byte[] bytes, int? uploadedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_AddPartAttachment", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairPartId", repairPartId);
                cmd.Parameters.AddWithValue("@AttachmentType", attachmentType);
                cmd.Parameters.AddWithValue("@FileName", OrDbNull(fileName));
                cmd.Parameters.AddWithValue("@MimeType", OrDbNull(mimeType));
                cmd.Parameters.AddWithValue("@FileBytes", OrDbNull(bytes));
                cmd.Parameters.AddWithValue("@FileSizeBytes", bytes != null ? (object)bytes.Length : DBNull.Value);
                cmd.Parameters.AddWithValue("@UploadedByUserId", OrDbNull(uploadedByUserId));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return reader.GetInt32(0);
                }
            }

            return 0;
        }

        public async Task DeletePartAttachmentAsync(int partAttachmentId, int? changedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_DeletePartAttachment", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@PartAttachmentId", partAttachmentId);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<RepairPartAttachmentItem> GetPartAttachmentAsync(int partAttachmentId)
        {
            const string sql = @"
SELECT a.PartAttachmentId, a.RepairPartId, a.RepairTicketId, a.AttachmentType, a.FileName, a.MimeType,
       a.FileBytes, a.FileSizeBytes, a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairPartAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.PartAttachmentId = @PartAttachmentId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@PartAttachmentId", partAttachmentId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RepairPartAttachmentItem
                        {
                            PartAttachmentId = reader.GetInt32(0),
                            RepairPartId = reader.GetInt32(1),
                            RepairTicketId = reader.GetInt32(2),
                            AttachmentType = GetStringOrNull(reader, 3),
                            FileName = GetStringOrNull(reader, 4),
                            MimeType = GetStringOrNull(reader, 5),
                            FileBytes = GetBytesOrNull(reader, 6),
                            FileSizeBytes = GetIntOrNull(reader, 7),
                            SortOrder = reader.GetInt32(8),
                            UploadedAt = GetLocalDateTime(reader, 9),
                            UploadedByName = GetStringOrNull(reader, 10)
                        };
                    }
                }
            }

            return null;
        }

        // ── Repair Conclusion ────────────────────────────────────────────────

        public async Task<RepairConclusion> GetConclusionAsync(int repairTicketId)
        {
            const string sql = @"
SELECT c.RepairTicketId, c.RootCause, c.WorkPerformed, c.FinalOutcome, c.Recommendations,
       COALESCE(u.Name, emp.Name) AS CompletedByName, c.CompletedAt,
       c.HandedOverToVendorId, v.VendorName,
       c.Disposition, c.DispositionItemId, c.DispositionDecidedAt, c.DispositionExecutedAt,
       c.ReplacementItemId, ri.Name AS ReplacementItemName,
       c.ReplacementRequestId, c.ReplacementSetId, rs.SetCode AS ReplacementSetCode
FROM dbo.RepairConclusion c
LEFT JOIN dbo.[User] u ON u.UserId = c.CompletedByUserId
LEFT JOIN dbo.Employee emp ON emp.EmpId = c.CompletedByEmpId
LEFT JOIN dbo.Vendor v ON v.VendorID = c.HandedOverToVendorId
LEFT JOIN dbo.Item ri ON ri.ItemId = c.ReplacementItemId
LEFT JOIN dbo.[Set] rs ON rs.SetId = c.ReplacementSetId
WHERE c.RepairTicketId = @RepairTicketId;";

            RepairConclusion result = null;

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        result = new RepairConclusion
                        {
                            RepairTicketId = reader.GetInt32(0),
                            RootCause = GetStringOrNull(reader, 1),
                            WorkPerformed = GetStringOrNull(reader, 2),
                            FinalOutcome = GetStringOrNull(reader, 3),
                            Recommendations = GetStringOrNull(reader, 4),
                            CompletedByName = GetStringOrNull(reader, 5),
                            CompletedAt = GetDateOrNull(reader, 6),
                            HandedOverToVendorId = GetIntOrNull(reader, 7),
                            HandedOverToVendorName = GetStringOrNull(reader, 8),
                            Disposition = GetStringOrNull(reader, 9),
                            DispositionItemId = GetIntOrNull(reader, 10),
                            DispositionDecidedAt = GetDateOrNull(reader, 11),
                            DispositionExecutedAt = GetDateOrNull(reader, 12),
                            ReplacementItemId = GetIntOrNull(reader, 13),
                            ReplacementItemName = GetStringOrNull(reader, 14),
                            ReplacementRequestId = GetIntOrNull(reader, 15),
                            ReplacementSetId = GetIntOrNull(reader, 16),
                            ReplacementSetCode = GetStringOrNull(reader, 17)
                        };
                    }
                }
            }

            if (result == null) return null;

            const string repairedBySql = @"
SELECT r.EmpId, e.Name
FROM dbo.RepairConclusionRepairedByEmp r
INNER JOIN dbo.Employee e ON e.EmpId = r.EmpId
WHERE r.RepairTicketId = @RepairTicketId
ORDER BY e.Name;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(repairedBySql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.RepairedByEmpIds.Add(reader.GetInt32(0));
                        result.RepairedByNames.Add(reader.GetString(1));
                    }
                }
            }

            return result;
        }

        public async Task SaveConclusionAsync(RepairConclusion conclusion)
        {
            if (conclusion == null) throw new ArgumentNullException(nameof(conclusion));

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    using (var cmd = new SqlCommand("dbo.sp_RepairPortal_SaveConclusion", con, tx) { CommandType = CommandType.StoredProcedure })
                    {
                        cmd.Parameters.AddWithValue("@RepairTicketId", conclusion.RepairTicketId);
                        cmd.Parameters.AddWithValue("@RootCause", OrDbNull(conclusion.RootCause));
                        cmd.Parameters.AddWithValue("@WorkPerformed", OrDbNull(conclusion.WorkPerformed));
                        cmd.Parameters.AddWithValue("@FinalOutcome", OrDbNull(conclusion.FinalOutcome));
                        cmd.Parameters.AddWithValue("@Recommendations", OrDbNull(conclusion.Recommendations));
                        cmd.Parameters.AddWithValue("@CompletedByUserId", OrDbNull(Session.AppSession.CurrentUserId > 0 ? Session.AppSession.CurrentUserId : (int?)null));
                        cmd.Parameters.AddWithValue("@CompletedByEmpId", OrDbNull(Session.AppSession.CurrentEmployeeId));
                        cmd.Parameters.AddWithValue("@HandedOverToVendorId", OrDbNull(conclusion.HandedOverToVendorId));
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Repaired-by employee set — plain replace (delete then re-insert) rather than a
                    // table-valued parameter, matching how this small a many-to-many set is handled
                    // elsewhere in this codebase.
                    using (var cmd = new SqlCommand("DELETE FROM dbo.RepairConclusionRepairedByEmp WHERE RepairTicketId = @RepairTicketId", con, tx))
                    {
                        cmd.Parameters.AddWithValue("@RepairTicketId", conclusion.RepairTicketId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    foreach (var empId in (conclusion.RepairedByEmpIds ?? new List<int>()).Distinct())
                    {
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.RepairConclusionRepairedByEmp (RepairTicketId, EmpId) VALUES (@RepairTicketId, @EmpId)", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@RepairTicketId", conclusion.RepairTicketId);
                            cmd.Parameters.AddWithValue("@EmpId", empId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    tx.Commit();
                }
            }
        }

        // ── Aggregate Evidence Gallery ───────────────────────────────────────

        public async Task<List<AttachmentGalleryItem>> GetEvidenceGalleryAsync(int repairTicketId)
        {
            const string sql = @"
SELECT a.AttachmentId, 0 AS IsPartLevel, NULL AS RepairPartId, NULL AS PartDisplayName,
       a.AttachmentType, a.FileName, a.MimeType, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairTicketAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.RepairTicketId = @RepairTicketId

UNION ALL

SELECT pa.PartAttachmentId, 1 AS IsPartLevel, pa.RepairPartId, p.PartDisplayName,
       pa.AttachmentType, pa.FileName, pa.MimeType, pa.UploadedAt, u2.Name AS UploadedByName
FROM dbo.RepairPartAttachment pa
INNER JOIN dbo.RepairPart p ON p.RepairPartId = pa.RepairPartId
LEFT JOIN dbo.[User] u2 ON u2.UserId = pa.UploadedByUserId
WHERE pa.RepairTicketId = @RepairTicketId

ORDER BY UploadedAt DESC;";

            var list = new List<AttachmentGalleryItem>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new AttachmentGalleryItem
                        {
                            AttachmentId = reader.GetInt32(0),
                            IsPartLevel = reader.GetInt32(1) == 1,
                            RepairPartId = GetIntOrNull(reader, 2),
                            PartDisplayName = GetStringOrNull(reader, 3),
                            AttachmentType = GetStringOrNull(reader, 4),
                            FileName = GetStringOrNull(reader, 5),
                            MimeType = GetStringOrNull(reader, 6),
                            UploadedAt = GetLocalDateTime(reader, 7),
                            UploadedByName = GetStringOrNull(reader, 8)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>Sum of FileSizeBytes across every whole-equipment (RepairTicketAttachment) and
        /// Part-level (RepairPartAttachment) evidence file already attached to this ticket — backs
        /// the 50 MB per-ticket evidence cap (separate from, and on top of, the existing 20 MB
        /// per-file cap) enforced before each new upload.</summary>
        public async Task<long> GetTotalEvidenceSizeBytesAsync(int repairTicketId)
        {
            const string sql = @"
SELECT
    ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes, 0) AS BIGINT)) FROM dbo.RepairTicketAttachment WHERE RepairTicketId = @RepairTicketId), 0) +
    ISNULL((SELECT SUM(CAST(ISNULL(FileSizeBytes, 0) AS BIGINT)) FROM dbo.RepairPartAttachment WHERE RepairTicketId = @RepairTicketId), 0);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0L : Convert.ToInt64(result);
            }
        }
    }
}
