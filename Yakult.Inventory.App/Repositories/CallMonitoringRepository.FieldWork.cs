using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task<bool> FieldWorkSchemaExistsAsync()
        {
            using (var con = new SqlConnection(ConnectionString))
            {
                return await CallSchemaGate.TableExistsAsync(con, "dbo.CallFieldVisit");
            }
        }

        private static string GetStringOrNull(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
        private static int? GetIntOrNull(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(reader.GetValue(ordinal));
        private static DateTime? GetDateOrNull(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(ordinal));

        public async Task<List<CallFieldVisitItem>> GetFieldVisitsAsync(int ticketId)
        {
            const string sql = @"
SELECT v.FieldVisitId, v.TicketId, v.Status, v.ScheduledAt, v.CompletedAt, v.Notes, v.TechnicianEmpId, te.Name AS TechName, d.Name AS Department, b.Name AS Branch, v.CreatedAt, v.UpdatedAt,
       CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSignature
FROM dbo.CallFieldVisit v
LEFT JOIN dbo.Employee te ON te.EmpId=v.TechnicianEmpId
LEFT JOIN dbo.CallTicket ct ON ct.TicketId=v.TicketId
LEFT JOIN dbo.Department d ON d.DeptId=ct.DeptId
LEFT JOIN dbo.Branch b ON b.BranchId=ct.BranchId
WHERE v.TicketId=@TicketId ORDER BY v.CreatedAt DESC, v.FieldVisitId DESC;";
            var list = new List<CallFieldVisitItem>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TicketId", ticketId);
                await con.OpenAsync();
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        list.Add(new CallFieldVisitItem
                        {
                            FieldVisitId = Convert.ToInt32(r["FieldVisitId"]),
                            TicketId = Convert.ToInt32(r["TicketId"]),
                            Status = r["Status"] as string,
                            ScheduledAt = r["ScheduledAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ScheduledAt"]),
                            CompletedAt = r["CompletedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["CompletedAt"]),
                            Notes = r["Notes"] as string,
                            TechnicianEmpId = r["TechnicianEmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TechnicianEmpId"]),
                            TechnicianName = r["TechName"] as string,
                            Department = r["Department"] as string,
                            Branch = r["Branch"] as string,
                            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
                            UpdatedAt = Convert.ToDateTime(r["UpdatedAt"]),
                            HasSignature = r["HasSignature"] != DBNull.Value && Convert.ToInt32(r["HasSignature"]) == 1
                        });
                    }
                }
            }
            return list;
        }

        public async Task<CallFieldVisitItem> GetFieldVisitAsync(int fieldVisitId)
        {
            const string sql = @"
SELECT v.FieldVisitId, v.TicketId, v.Status, v.ScheduledAt, v.CompletedAt, v.Notes, v.TechnicianEmpId, te.Name AS TechName, d.Name AS Department, b.Name AS Branch, v.CreatedAt, v.UpdatedAt,
       CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSignature
FROM dbo.CallFieldVisit v
LEFT JOIN dbo.Employee te ON te.EmpId=v.TechnicianEmpId
LEFT JOIN dbo.CallTicket ct ON ct.TicketId=v.TicketId
LEFT JOIN dbo.Department d ON d.DeptId=ct.DeptId
LEFT JOIN dbo.Branch b ON b.BranchId=ct.BranchId
WHERE v.FieldVisitId=@Id;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", fieldVisitId);
                await con.OpenAsync();
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        return new CallFieldVisitItem
                        {
                            FieldVisitId = Convert.ToInt32(r["FieldVisitId"]),
                            TicketId = Convert.ToInt32(r["TicketId"]),
                            Status = r["Status"] as string,
                            ScheduledAt = r["ScheduledAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ScheduledAt"]),
                            CompletedAt = r["CompletedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["CompletedAt"]),
                            Notes = r["Notes"] as string,
                            TechnicianEmpId = r["TechnicianEmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TechnicianEmpId"]),
                            TechnicianName = r["TechName"] as string,
                            Department = r["Department"] as string,
                            Branch = r["Branch"] as string,
                            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),
                            UpdatedAt = Convert.ToDateTime(r["UpdatedAt"]),
                            HasSignature = r["HasSignature"] != DBNull.Value && Convert.ToInt32(r["HasSignature"]) == 1
                        };
                    }
                }
            }
            return null;
        }

        public async Task<CallFieldVisitItem> ScheduleFieldVisitAsync(int ticketId, int? technicianEmpId, DateTime? scheduledAt, string notes, int? createdByUserId)
        {
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("dbo.sp_Call_FieldVisit_Schedule", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@TicketId", ticketId);
                cmd.Parameters.AddWithValue("@TechnicianEmpId", (object)technicianEmpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ScheduledAt", (object)scheduledAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedByUserId", (object)createdByUserId ?? DBNull.Value);
                await con.OpenAsync();
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        return new CallFieldVisitItem
                        {
                            FieldVisitId = Convert.ToInt32(r["FieldVisitId"]),
                            TicketId = Convert.ToInt32(r["TicketId"]),
                            Status = r["Status"] as string,
                            ScheduledAt = r["ScheduledAt"] as DateTime?,
                            CompletedAt = r["CompletedAt"] as DateTime?,
                            Notes = r["Notes"] as string,
                            TechnicianEmpId = r["TechnicianEmpId"] as int?,
                            CreatedAt = (DateTime)r["CreatedAt"],
                            UpdatedAt = (DateTime)r["UpdatedAt"]
                        };
                    }
                }
            }
            // The proc always returns the new row: no row means the schedule
            // genuinely failed, so throw instead of presenting some older
            // visit as the scheduled one.
            throw new InvalidOperationException("Schedule did not return a visit. Please try again.");
        }

        public async Task<CallFieldVisitItem> SetFieldVisitStatusAsync(int fieldVisitId, string newStatus, int? changedByUserId, string notes = null, int? technicianEmpId = null, DateTime? scheduledAt = null, DateTime? completedAt = null)
        {
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("dbo.sp_Call_FieldVisit_SetStatus", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@FieldVisitId", fieldVisitId);
                cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                cmd.Parameters.AddWithValue("@ChangedByUserId", (object)changedByUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TechnicianEmpId", (object)technicianEmpId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ScheduledAt", (object)scheduledAt ?? DBNull.Value);
                // Only send the override when backdating: older proc versions
                // reject unknown parameters (8144), and normal flows must keep
                // working against them.
                if (completedAt.HasValue)
                    cmd.Parameters.AddWithValue("@CompletedAtOverride", completedAt.Value);
                await con.OpenAsync();
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            return new CallFieldVisitItem
                            {
                                FieldVisitId = Convert.ToInt32(r["FieldVisitId"]),
                                TicketId = Convert.ToInt32(r["TicketId"]),
                                Status = r["Status"] as string,
                                ScheduledAt = r["ScheduledAt"] as DateTime?,
                                CompletedAt = r["CompletedAt"] as DateTime?,
                                Notes = r["Notes"] as string,
                                TechnicianEmpId = r["TechnicianEmpId"] as int?,
                                CreatedAt = (DateTime)r["CreatedAt"],
                                UpdatedAt = (DateTime)r["UpdatedAt"]
                            };
                        }
                    }
                }
                catch (SqlException ex) when (ex.Number == 8144 && completedAt.HasValue)
                {
                    // Old proc version with no @CompletedAtOverride: backdated
                    // completion is impossible until the migration runs.
                    throw new InvalidOperationException(
                        "Backdated completion needs the updated database procedure. Run Migration_ITCM_FieldVisit_CompletedAtOverride on this database, then try again.",
                        ex);
                }
            }
            return await GetFieldVisitAsync(fieldVisitId);
        }

        public async Task<List<CallFieldVisitAttachmentItem>> GetFieldVisitAttachmentsAsync(int ticketId)        {
            const string sql = @"
SELECT a.AttachmentId, a.FieldVisitId, a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.CallFieldVisitAttachment a
INNER JOIN dbo.CallFieldVisit v ON v.FieldVisitId=a.FieldVisitId
LEFT JOIN dbo.[User] u ON u.UserId=a.UploadedByUserId
WHERE v.TicketId=@TicketId ORDER BY a.UploadedAt DESC, a.AttachmentId DESC;";
            var list = new List<CallFieldVisitAttachmentItem>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TicketId", ticketId);
                await con.OpenAsync();
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        list.Add(new CallFieldVisitAttachmentItem
                        {
                            AttachmentId = r.GetInt32(0),
                            FieldVisitId = r.GetInt32(1),
                            FileName = GetStringOrNull(r, 2),
                            MimeType = GetStringOrNull(r, 3),
                            FileSizeBytes = GetIntOrNull(r, 4),
                            UploadedAt = r.GetDateTime(5),
                            UploadedByName = GetStringOrNull(r, 6)
                        });
                    }
                }
            }
            return list;
        }

        public async Task<List<CallFieldVisitAttachmentItem>> GetFieldVisitAttachmentsByVisitAsync(int fieldVisitId)
        {
            const string sql = @"
SELECT a.AttachmentId, a.FieldVisitId, a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.CallFieldVisitAttachment a
LEFT JOIN dbo.[User] u ON u.UserId=a.UploadedByUserId
WHERE a.FieldVisitId=@VisitId ORDER BY a.UploadedAt DESC, a.AttachmentId DESC;";
            var list = new List<CallFieldVisitAttachmentItem>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@VisitId", fieldVisitId);
                await con.OpenAsync();
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        list.Add(new CallFieldVisitAttachmentItem
                        {
                            AttachmentId = r.GetInt32(0),
                            FieldVisitId = r.GetInt32(1),
                            FileName = GetStringOrNull(r, 2),
                            MimeType = GetStringOrNull(r, 3),
                            FileSizeBytes = GetIntOrNull(r, 4),
                            UploadedAt = r.GetDateTime(5),
                            UploadedByName = GetStringOrNull(r, 6)
                        });
                    }
                }
            }
            return list;
        }

        public async Task<int> UploadFieldVisitPhotoAsync(int fieldVisitId, string fileName, string mimeType, byte[] fileBytes, int? uploadedByUserId)
        {
            const int MaxPhotosPerVisit = 20;
            const int MaxPhotoBytes = 20 * 1024 * 1024;

            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("Photo file is empty.", nameof(fileBytes));
            if (fileBytes.Length > MaxPhotoBytes)
                throw new InvalidOperationException("Photo exceeds the 20 MB limit.");
            // Sniff content: declared mime is not trusted (a .bmp/.exe must
            // not be stored as image/jpeg). PNG and JPEG only.
            mimeType = DetectPhotoMimeType(fileBytes, fileName);
            if (mimeType == null)
                throw new InvalidOperationException("Only PNG and JPEG photos are accepted.");

            const string sql = @"
INSERT dbo.CallFieldVisitAttachment (FieldVisitId, FileName, MimeType, FileBytes, ThumbnailBytes, FileSizeBytes, UploadedByUserId)
VALUES (@VisitId, @FileName, @MimeType, @FileBytes, @Thumb, @Size, @UserId);
SELECT SCOPE_IDENTITY();";
            byte[] thumb = null;
            try
            {
                thumb = CreateThumbnailBytes(fileBytes, 400, 85);
            }
            catch { thumb = null; }

            using (var con = new SqlConnection(ConnectionString))
            {
                await con.OpenAsync();
                using (var gate = new SqlCommand("SELECT Status FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id;", con))
                {
                    gate.Parameters.AddWithValue("@Id", fieldVisitId);
                    var status = (await gate.ExecuteScalarAsync() as string ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(status))
                        throw new InvalidOperationException("Field visit not found.");
                    if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Photos cannot be added to a Completed visit.");
                }
                using (var cap = new SqlCommand("SELECT COUNT(1) FROM dbo.CallFieldVisitAttachment WHERE FieldVisitId=@Id;", con))
                {
                    cap.Parameters.AddWithValue("@Id", fieldVisitId);
                    if (Convert.ToInt32(await cap.ExecuteScalarAsync()) >= MaxPhotosPerVisit)
                        throw new InvalidOperationException("Maximum 20 photos per visit (20/20).");
                }
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@VisitId", fieldVisitId);
                    cmd.Parameters.AddWithValue("@FileName", fileName ?? "field-photo.jpg");
                    cmd.Parameters.AddWithValue("@MimeType", (object)mimeType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FileBytes", (object)fileBytes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Thumb", (object)thumb ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Size", fileBytes?.Length ?? 0);
                    cmd.Parameters.AddWithValue("@UserId", (object)uploadedByUserId ?? DBNull.Value);
                    var id = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(id);
                }
            }
        }

        private static string DetectPhotoMimeType(byte[] fileBytes, string fileName)
        {
            if (fileBytes == null || fileBytes.Length < 4)
                return null;
            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (fileBytes.Length >= 8
                && fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47
                && fileBytes[4] == 0x0D && fileBytes[5] == 0x0A && fileBytes[6] == 0x1A && fileBytes[7] == 0x0A)
                return "image/png";
            // JPEG signature: FF D8 FF
            if (fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
                return "image/jpeg";
            return null;
        }

        public async Task<byte[]> GetFieldVisitAttachmentBytesAsync(int attachmentId)
        {
            const string sql = "SELECT FileBytes FROM dbo.CallFieldVisitAttachment WHERE AttachmentId=@Id;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", attachmentId);
                await con.OpenAsync();
                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null || obj == DBNull.Value) return null;
                return (byte[])obj;
            }
        }

        public async Task<byte[]> GetFieldVisitAttachmentThumbnailBytesAsync(int attachmentId)
        {
            const string sql = "SELECT COALESCE(ThumbnailBytes, FileBytes) FROM dbo.CallFieldVisitAttachment WHERE AttachmentId=@Id;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", attachmentId);
                await con.OpenAsync();
                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null || obj == DBNull.Value) return null;
                return (byte[])obj;
            }
        }

        public async Task<byte[]> GetFieldVisitSignatureBytesAsync(int fieldVisitId)
        {
            const string sql = "SELECT CustomerSignature FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Id", fieldVisitId);
                await con.OpenAsync();
                var obj = await cmd.ExecuteScalarAsync();
                if (obj == null || obj == DBNull.Value) return null;
                return (byte[])obj;
            }
        }

        public async Task SaveFieldVisitSignatureAsync(int fieldVisitId, byte[] signaturePngBytes, int? changedByUserId)
        {
            // Normalize empty captures to NULL so HasSignature (DATALENGTH>0)
            // stays truthful everywhere it is read.
            if (signaturePngBytes != null && signaturePngBytes.Length == 0)
                signaturePngBytes = null;
            const string sql = "UPDATE dbo.CallFieldVisit SET CustomerSignature=@Sig WHERE FieldVisitId=@Id;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Sig", (object)signaturePngBytes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", fieldVisitId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            // Also log to history
            var visit = await GetFieldVisitAsync(fieldVisitId);
            if (visit != null)
            {
                try
                {
                    using (var con = new SqlConnection(ConnectionString))
                    using (var cmd = new SqlCommand("INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note) VALUES (@TicketId,@UserId,'FieldVisitSignature','', 'Saved', NULL);", con))
                    {
                        cmd.Parameters.AddWithValue("@TicketId", visit.TicketId);
                        cmd.Parameters.AddWithValue("@UserId", (object)changedByUserId ?? DBNull.Value);
                        await con.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch { }
            }
        }

        public async Task<string> ValidateTicketSignOffAsync(int ticketId, int fieldVisitId)
        {
            const string sql = @"
SELECT ct.Status, ct.SolvedAt, v.Status AS VisitStatus,
       CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSig,
       (SELECT COUNT(1) FROM dbo.CallFieldVisit o WHERE o.TicketId=@TicketId AND o.Status='Scheduled') AS OpenCount
FROM dbo.CallTicket ct
LEFT JOIN dbo.CallFieldVisit v ON v.FieldVisitId=@VisitId AND v.TicketId=@TicketId
WHERE ct.TicketId=@TicketId;";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TicketId", ticketId);
                cmd.Parameters.AddWithValue("@VisitId", fieldVisitId);
                await con.OpenAsync();
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    if (!await rd.ReadAsync()) return "Ticket not found.";
                    var st = (rd["Status"] as string ?? "").Trim();
                    var isFinal = st.Equals("Solved", StringComparison.OrdinalIgnoreCase) || st.Equals("Closed", StringComparison.OrdinalIgnoreCase);
                    var isTemp = st.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
                    if (!isFinal && !isTemp) return "Ticket must be Solved or Closed (current: " + st + ").";
                    if (rd["SolvedAt"] == DBNull.Value && isFinal) return "Ticket has no Solved date.";
                    var visitStatus = rd["VisitStatus"] as string;
                    if (string.IsNullOrEmpty(visitStatus)) return "Field visit not found.";
                    if (!visitStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return "Field visit must be Completed.";
                    if ((int)rd["HasSig"] == 0) return "Customer signature is required. Capture the signature on the completed visit, then sign off.";
                    if ((int)rd["OpenCount"] > 0) return "An open (Scheduled) field visit exists.";
                    if (isTemp)
                    {
                        var preview = await GetTemporaryReplacementReturnPreviewAsync(ticketId);
                        // Service-Only temporaries issue no parts (NewItemId <= 0), so
                        // there is nothing to return - only Replacement-backed
                        // temporaries gate sign-off on the part return.
                        if (preview.NewItemId > 0 && !preview.AlreadyReturned) return "Temporary replacement has not been returned yet.";
                    }
                    return null;
                }
            }
        }

        public async Task RecordTicketSignOffAsync(int ticketId, int fieldVisitId, string reportHash, string customerName, string techName, int? changedByUserId)
        {
            var block = await ValidateTicketSignOffAsync(ticketId, fieldVisitId);
            if (!string.IsNullOrEmpty(block)) throw new InvalidOperationException(block);
            // De-dupe: double-clicks / concurrent sign-offs for the same visit
            // must not write phantom duplicate rows.
            const string sql = @"
IF NOT EXISTS (
    SELECT 1 FROM dbo.CallTicketHistory
    WHERE TicketId = @TicketId
      AND FieldName = 'SignOff'
      AND NewValue = 'Signed'
      AND Note LIKE '%visit=' + CONVERT(nvarchar(20), @VisitId) + ' %'
)
INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note) VALUES (@TicketId,@UserId,'SignOff','Unsigned','Signed',@Note);";
            var note = (reportHash ?? "-") + " customer=" + (customerName ?? "-") + " tech=" + (techName ?? "-") + " visit=" + fieldVisitId + " " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@TicketId", ticketId);
                cmd.Parameters.AddWithValue("@VisitId", fieldVisitId);
                cmd.Parameters.AddWithValue("@UserId", (object)changedByUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Note", note);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<CallFieldVisitReportRow>> GetFieldWorkReportAsync(DateTime fromUtc, DateTime toUtc, string statusFilter, int? technicianEmpId, int maxRows = 2000)
        {
            const string sql = @"
SELECT TOP (@MaxRows) v.FieldVisitId, v.TicketId, ct.TicketCode, ct.Issue, d.Name AS Department, b.Name AS Branch,
       v.TechnicianEmpId, te.Name AS TechnicianName, v.Status, v.ScheduledAt, v.CompletedAt,
       CASE WHEN DATALENGTH(v.CustomerSignature) > 0 THEN 1 ELSE 0 END AS HasSignature,
       (SELECT COUNT(1) FROM dbo.CallFieldVisitAttachment a WHERE a.FieldVisitId=v.FieldVisitId) AS AttachmentCount,
       v.Notes, v.CreatedAt
FROM dbo.CallFieldVisit v
LEFT JOIN dbo.CallTicket ct ON ct.TicketId=v.TicketId
LEFT JOIN dbo.Department d ON d.DeptId=ct.DeptId
LEFT JOIN dbo.Branch b ON b.BranchId=ct.BranchId
LEFT JOIN dbo.Employee te ON te.EmpId=v.TechnicianEmpId
WHERE COALESCE(v.ScheduledAt, v.CreatedAt) >= @FromUtc AND COALESCE(v.ScheduledAt, v.CreatedAt) < @ToUtc
  AND (@Status IS NULL OR @Status='' OR v.Status=@Status)
  AND (@TechId IS NULL OR v.TechnicianEmpId=@TechId)
ORDER BY COALESCE(v.ScheduledAt, v.CreatedAt) DESC, v.FieldVisitId DESC;";
            var list = new List<CallFieldVisitReportRow>();
            using (var con = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@MaxRows", maxRows);
                cmd.Parameters.AddWithValue("@FromUtc", fromUtc);
                cmd.Parameters.AddWithValue("@ToUtc", toUtc);
                cmd.Parameters.AddWithValue("@Status", (object)statusFilter ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TechId", (object)technicianEmpId ?? DBNull.Value);
                await con.OpenAsync();
                if (!await CallSchemaGate.TableExistsAsync(con, "dbo.CallFieldVisit")) return list;
                using (var r = await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                    {
                        list.Add(new CallFieldVisitReportRow
                        {
                            FieldVisitId = Convert.ToInt32(r["FieldVisitId"]),
                            TicketId = Convert.ToInt32(r["TicketId"]),
                            TicketCode = r["TicketCode"] as string,
                            Issue = r["Issue"] as string,
                            Department = r["Department"] as string,
                            Branch = r["Branch"] as string,
                            TechnicianEmpId = r["TechnicianEmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["TechnicianEmpId"]),
                            TechnicianName = r["TechnicianName"] as string,
                            Status = r["Status"] as string,
                            ScheduledAt = r["ScheduledAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ScheduledAt"]),
                            CompletedAt = r["CompletedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["CompletedAt"]),
                            HasSignature = r["HasSignature"] != DBNull.Value && Convert.ToInt32(r["HasSignature"]) == 1,
                            AttachmentCount = r["AttachmentCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["AttachmentCount"]),
                            Notes = r["Notes"] as string,
                            CreatedAt = Convert.ToDateTime(r["CreatedAt"])
                        });
                    }
                }
            }
            return list;
        }

        private static byte[] CreateThumbnailBytes(byte[] src, int maxDim, long quality)
        {
            using (var ms = new System.IO.MemoryStream(src))
            using (var img = System.Drawing.Image.FromStream(ms))
            {
                var scale = Math.Min((double)maxDim / img.Width, (double)maxDim / img.Height);
                if (scale >= 1) scale = 1;
                var w = Math.Max(1, (int)(img.Width * scale));
                var h = Math.Max(1, (int)(img.Height * scale));
                using (var thumb = new System.Drawing.Bitmap(w, h))
                using (var g = System.Drawing.Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, 0, 0, w, h);
                    var enc = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
                    System.Drawing.Imaging.ImageCodecInfo jpeg = null;
                    foreach (var e in enc) if (e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid) { jpeg = e; break; }
                    using (var outMs = new System.IO.MemoryStream())
                    using (var eps = new System.Drawing.Imaging.EncoderParameters(1))
                    {
                        eps.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                        if (jpeg != null) thumb.Save(outMs, jpeg, eps);
                        else thumb.Save(outMs, System.Drawing.Imaging.ImageFormat.Jpeg);
                        return outMs.ToArray();
                    }
                }
            }
        }
    }
}
