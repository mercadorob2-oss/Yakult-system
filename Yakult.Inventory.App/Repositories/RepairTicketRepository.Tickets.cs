using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class RepairTicketRepository
    {
        /// <summary>Days spent in the CURRENT status only (not a full per-status breakdown) —
        /// the most recent Status history row (or CreatedAt if none yet), through to now, or frozen
        /// at CompletedAt once the ticket reaches a terminal status. Shared fragment for both the
        /// list and detail header queries so the definition can't drift between them.</summary>
        private const string TicketDaysInCurrentStatusSql = @"
    DATEDIFF(day,
        COALESCE(
            (SELECT TOP (1) h.ChangedAt FROM dbo.RepairTicketHistory h
             WHERE h.RepairTicketId = t.RepairTicketId AND h.FieldName = 'Status'
             ORDER BY h.ChangedAt DESC, h.HistoryId DESC),
            t.CreatedAt
        ),
        CASE WHEN t.Status IN ('Completed', 'Unrepairable') THEN COALESCE(t.CompletedAt, SYSUTCDATETIME()) ELSE SYSUTCDATETIME() END
    ) AS DaysInCurrentStatus";

        private const string TicketListSelectSql = @"
SELECT
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot,
    t.SetCode,
    -- Prefer the explicit Requested By org (deliberately picked by the technician at intake) —
    -- falling back to the requester Employee's own org when RequestedByType='Employee' set only
    -- RequestedByEmpId without also duplicating org IDs onto the ticket — then finally the item's
    -- own stored Company/Branch/Dept, which is frequently unset on older items.
    COALESCE(reqCom.Name, reqEmpCo.Name, co.Name) AS CompanyName,
    COALESCE(reqBranch.Name, reqEmpBr.Name, br.Name) AS BranchName,
    COALESCE(reqDept.Name, reqEmpDe.Name, de.Name) AS DeptName,
    t.Problem, t.Priority, t.Status,
    subEmp.Name AS SubmittedByName, techEmp.Name AS AssignedTechName,
    t.DateReceived, t.UpdatedAt,
    thumb.FileBytes AS ThumbnailImageBytes, thumb.MimeType AS ThumbnailMimeType,
" + TicketDaysInCurrentStatusSql + @",
    i.Category, t.RequestedByType, reqEmp.Name AS RequestedByEmpName, reqDept.Name AS RequestedByDeptName
FROM dbo.RepairTicket t
LEFT JOIN dbo.Company co ON co.ComId = t.ComId
LEFT JOIN dbo.Branch br ON br.BranchId = t.BranchId
LEFT JOIN dbo.Department de ON de.DeptId = t.DeptId
LEFT JOIN dbo.Employee subEmp ON subEmp.EmpId = t.SubmittedByEmpId
LEFT JOIN dbo.Employee techEmp ON techEmp.EmpId = t.AssignedTechEmpId
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = t.RequestedByEmpId
LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = t.RequestedByDeptId
LEFT JOIN dbo.Company reqCom ON reqCom.ComId = t.RequestedByComId
LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId = t.RequestedByBranchId
LEFT JOIN dbo.Company reqEmpCo ON reqEmpCo.ComId = reqEmp.ComId
LEFT JOIN dbo.Branch reqEmpBr ON reqEmpBr.BranchId = reqEmp.BranchId
LEFT JOIN dbo.Department reqEmpDe ON reqEmpDe.DeptId = reqEmp.DeptId
OUTER APPLY (
    -- ThumbnailBytes (a small pre-resized JPEG, generated at upload time — see
    -- AddAttachmentAsync/CreateThumbnailBytes), NOT the full-resolution FileBytes: streaming a
    -- multi-MB original for every row in the list is what made this query slow. Attachments
    -- uploaded before this column existed have NULL here and just show the placeholder icon.
    SELECT TOP (1) a.ThumbnailBytes AS FileBytes, a.MimeType
    FROM dbo.RepairTicketAttachment a
    WHERE a.RepairTicketId = t.RepairTicketId AND a.AttachmentType = 'Image'
    ORDER BY a.SortOrder ASC, a.AttachmentId ASC
) thumb";

        public async Task<List<RepairTicketListItem>> GetTicketsAsync(RepairTicketFilterCriteria criteria)
        {
            criteria = criteria ?? new RepairTicketFilterCriteria();

            var where = new StringBuilder(" WHERE 1 = 1");
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(criteria.Status))
            {
                where.Append(" AND t.Status = @Status");
                parameters.Add(new SqlParameter("@Status", criteria.Status.Trim()));
            }

            if (criteria.HideCompleted)
                where.Append(" AND t.Status <> 'Completed'");

            // Prefer RequestedBy*, fall back to the item's own * — must match the same priority
            // used for the displayed CompanyName/BranchName/DeptName columns (COALESCE(reqCom.Name,
            // co.Name) etc. in TicketListSelectSql), otherwise filtering by the Company/Branch/
            // Department shown on screen finds nothing: the ticket's own t.ComId/BranchId/DeptId is
            // frequently NULL on items with no assigned org, even when RequestedByComId etc. IS set
            // and is exactly what's rendered in the Company/Branch/Department columns.
            // Written as a SARGable OR (not ISNULL(...) = @x) so SQL Server can still seek either
            // IX_RepairTicket_ComBranchDept or IX_RepairTicket_RequestedByComBranchDept instead of
            // falling back to a full scan.
            if (criteria.ComId.HasValue)
            {
                where.Append(" AND (t.RequestedByComId = @ComId OR (t.RequestedByComId IS NULL AND t.ComId = @ComId))");
                parameters.Add(new SqlParameter("@ComId", criteria.ComId.Value));
            }

            if (criteria.BranchId.HasValue)
            {
                where.Append(" AND (t.RequestedByBranchId = @BranchId OR (t.RequestedByBranchId IS NULL AND t.BranchId = @BranchId))");
                parameters.Add(new SqlParameter("@BranchId", criteria.BranchId.Value));
            }

            if (criteria.DeptId.HasValue)
            {
                where.Append(" AND (t.RequestedByDeptId = @DeptId OR (t.RequestedByDeptId IS NULL AND t.DeptId = @DeptId))");
                parameters.Add(new SqlParameter("@DeptId", criteria.DeptId.Value));
            }

            if (!string.IsNullOrWhiteSpace(criteria.Priority))
            {
                where.Append(" AND t.Priority = @Priority");
                parameters.Add(new SqlParameter("@Priority", criteria.Priority.Trim()));
            }

            if (criteria.AssignedTechEmpId.HasValue)
            {
                where.Append(" AND t.AssignedTechEmpId = @AssignedTechEmpId");
                parameters.Add(new SqlParameter("@AssignedTechEmpId", criteria.AssignedTechEmpId.Value));
            }

            if (criteria.Categories != null && criteria.Categories.Count > 0)
            {
                var catParamNames = new List<string>();
                for (var i = 0; i < criteria.Categories.Count; i++)
                {
                    var paramName = "@Cat" + i;
                    catParamNames.Add(paramName);
                    parameters.Add(new SqlParameter(paramName, criteria.Categories[i]));
                }
                where.Append(" AND i.Category IN (" + string.Join(", ", catParamNames) + ")");
            }

            switch ((criteria.QuickFilterChip ?? string.Empty).Trim())
            {
                case "Waiting":
                    where.Append(" AND t.Status = 'Waiting'");
                    break;
                case "Diagnosing":
                    where.Append(" AND t.Status = 'Diagnosing'");
                    break;
                case "Repairing":
                    where.Append(" AND t.Status = 'Repairing'");
                    break;
                case "Completed":
                    where.Append(" AND t.Status = 'Completed'");
                    break;
                case "NeedsParts":
                    where.Append(" AND t.Status = 'AwaitingParts'");
                    break;
                case "Unrepairable":
                    where.Append(" AND t.Status = 'Unrepairable'");
                    break;
                case "HighPriority":
                    where.Append(" AND t.Priority IN ('High', 'Critical')");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                where.Append(" AND (t.TicketCode LIKE @Search OR t.ItemNameSnapshot LIKE @Search OR t.ItemSerialSnapshot LIKE @Search OR t.Problem LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", "%" + criteria.SearchText.Trim() + "%"));
            }

            var orderBy = " ORDER BY t.DateReceived DESC";
            switch ((criteria.SortKey ?? string.Empty).Trim())
            {
                case "DateReceivedAsc":
                    orderBy = " ORDER BY t.DateReceived ASC";
                    break;
                case "PriorityDesc":
                    orderBy = " ORDER BY CASE t.Priority WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 WHEN 'Low' THEN 4 ELSE 5 END ASC, t.DateReceived DESC";
                    break;
                case "StatusAsc":
                    orderBy = " ORDER BY t.Status ASC, t.DateReceived DESC";
                    break;
            }

            var sql = TicketListSelectSql + where + orderBy + ";";

            var list = new List<RepairTicketListItem>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters.ToArray());
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        list.Add(MapListItem(reader));
                }
            }

            return list;
        }

        private static RepairTicketListItem MapListItem(SqlDataReader reader)
        {
            return new RepairTicketListItem
            {
                RepairTicketId = reader.GetInt32(0),
                TicketCode = GetStringOrNull(reader, 1),
                ItemId = reader.GetInt32(2),
                ItemName = GetStringOrNull(reader, 3),
                ItemSerial = GetStringOrNull(reader, 4),
                SetCode = GetStringOrNull(reader, 5),
                CompanyName = GetStringOrNull(reader, 6),
                BranchName = GetStringOrNull(reader, 7),
                DeptName = GetStringOrNull(reader, 8),
                Problem = GetStringOrNull(reader, 9),
                Priority = GetStringOrNull(reader, 10),
                Status = GetStringOrNull(reader, 11),
                SubmittedByName = GetStringOrNull(reader, 12),
                AssignedTechName = GetStringOrNull(reader, 13),
                // DateReceived is a technician-entered wall-clock value (back-dated intake), never
                // converted to/from UTC on write — unlike CreatedAt/UpdatedAt (SYSUTCDATETIME()),
                // running it through GetLocalDateTime's UTC->Manila shift would offset it by 8
                // hours for no reason. Read it raw instead.
                DateReceived = reader.GetDateTime(14),
                UpdatedAt = GetLocalDateTime(reader, 15),
                ThumbnailImageBytes = GetBytesOrNull(reader, 16),
                ThumbnailMimeType = GetStringOrNull(reader, 17),
                DaysInCurrentStatus = reader.GetInt32(18),
                Category = GetStringOrNull(reader, 19),
                RequestedByType = GetStringOrNull(reader, 20),
                RequestedByEmpName = GetStringOrNull(reader, 21),
                RequestedByDeptName = GetStringOrNull(reader, 22)
            };
        }

        /// <summary>Distinct item Categories currently represented among repair tickets — drives the
        /// Category filter's checkbox list, so it only ever shows categories that actually have a
        /// ticket right now (e.g. just "Laptop"/"Mouse" if that's all there is), not the full
        /// system-wide category catalog.</summary>
        public async Task<List<string>> GetDistinctTicketCategoriesAsync()
        {
            const string sql = @"
SELECT DISTINCT i.Category
FROM dbo.RepairTicket t
INNER JOIN dbo.Item i ON i.ItemId = t.ItemId
WHERE i.Category IS NOT NULL AND LTRIM(RTRIM(i.Category)) <> ''
ORDER BY i.Category;";

            var list = new List<string>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        list.Add(reader.GetString(0));
                }
            }

            return list;
        }

        public async Task<RepairPortalSummaryCounts> GetSummaryCountsAsync()
        {
            const string sql = @"
SELECT
    SUM(CASE WHEN Status = 'Waiting' THEN 1 ELSE 0 END) AS Waiting,
    SUM(CASE WHEN Status = 'Diagnosing' THEN 1 ELSE 0 END) AS Diagnosing,
    SUM(CASE WHEN Status = 'Repairing' THEN 1 ELSE 0 END) AS Repairing,
    SUM(CASE WHEN Status = 'AwaitingParts' THEN 1 ELSE 0 END) AS AwaitingParts,
    SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN Status = 'Unrepairable' THEN 1 ELSE 0 END) AS Unrepairable
FROM dbo.RepairTicket;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RepairPortalSummaryCounts
                        {
                            Waiting = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            Diagnosing = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            Repairing = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            AwaitingParts = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            Completed = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            Unrepairable = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                        };
                    }
                }
            }

            return new RepairPortalSummaryCounts();
        }

        public async Task<RepairTicketDetail> GetTicketDetailAsync(int repairTicketId)
        {
            const string headerSql = @"
SELECT
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot, t.SetCode,
    -- Same three-tier fallback as TicketListSelectSql above (Requested By org -> requester
    -- Employee's own org -> the ticket's bare ComId/BranchId/DeptId) — this query used to be
    -- plain co.Name/br.Name/de.Name with no fallback at all, so an Employee-mode request whose
    -- RequestedByComId/BranchId/DeptId were never duplicated onto the ticket showed a blank
    -- Company/Branch/Department line even though the requester's own org was fully known.
    COALESCE(reqCom.Name, reqEmpCo.Name, co.Name) AS CompanyName,
    COALESCE(reqBranch.Name, reqEmpBr.Name, br.Name) AS BranchName,
    COALESCE(reqDept.Name, reqEmpDe.Name, de.Name) AS DeptName,
    t.Problem, t.Diagnosis, t.Resolution, t.PartsUsed, t.Priority, t.Status,
    subEmp.Name AS SubmittedByName, techEmp.Name AS AssignedTechName,
    t.DateReceived, t.CreatedAt, t.UpdatedAt, t.CompletedAt,
    t.RequestedByType, t.RequestedByDeptId, t.RequestedByEmpId,
    reqDept.Name AS RequestedByDeptName, reqEmp.Name AS RequestedByEmpName,
" + TicketDaysInCurrentStatusSql + @",
    t.RequestedByComId, t.RequestedByBranchId, reqCom.Name AS RequestedByComName, reqBranch.Name AS RequestedByBranchName,
    i.Category, i.ModelNumber
FROM dbo.RepairTicket t
LEFT JOIN dbo.Company co ON co.ComId = t.ComId
LEFT JOIN dbo.Branch br ON br.BranchId = t.BranchId
LEFT JOIN dbo.Department de ON de.DeptId = t.DeptId
LEFT JOIN dbo.Employee subEmp ON subEmp.EmpId = t.SubmittedByEmpId
LEFT JOIN dbo.Employee techEmp ON techEmp.EmpId = t.AssignedTechEmpId
LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = t.RequestedByDeptId
LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = t.RequestedByEmpId
LEFT JOIN dbo.Company reqCom ON reqCom.ComId = t.RequestedByComId
LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId = t.RequestedByBranchId
LEFT JOIN dbo.Company reqEmpCo ON reqEmpCo.ComId = reqEmp.ComId
LEFT JOIN dbo.Branch reqEmpBr ON reqEmpBr.BranchId = reqEmp.BranchId
LEFT JOIN dbo.Department reqEmpDe ON reqEmpDe.DeptId = reqEmp.DeptId
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
WHERE t.RepairTicketId = @RepairTicketId;";

            const string attachmentsSql = @"
SELECT a.AttachmentId, a.RepairTicketId, a.AttachmentType, a.FileName, a.MimeType, a.FileSizeBytes,
       a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairTicketAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.RepairTicketId = @RepairTicketId
ORDER BY a.SortOrder ASC, a.AttachmentId ASC;";

            const string notesSql = @"
SELECT n.NoteId, n.RepairTicketId, n.NoteType, n.NoteText, n.CreatedAt,
       COALESCE(u.Name, emp.Name) AS CreatedByName
FROM dbo.RepairTicketNote n
LEFT JOIN dbo.[User] u ON u.UserId = n.CreatedByUserId
LEFT JOIN dbo.Employee emp ON emp.EmpId = n.CreatedByEmpId
WHERE n.RepairTicketId = @RepairTicketId
ORDER BY n.CreatedAt DESC, n.NoteId DESC;";

            const string historySql = @"
SELECT h.HistoryId, h.RepairTicketId, h.ChangedAt, h.ChangedByUserId, u.Name AS ChangedByName,
       h.FieldName, h.OldValue, h.NewValue, h.Note
FROM dbo.RepairTicketHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.RepairTicketId = @RepairTicketId
ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";

            RepairTicketDetail detail = null;

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(headerSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            detail = new RepairTicketDetail
                            {
                                RepairTicketId = reader.GetInt32(0),
                                TicketCode = GetStringOrNull(reader, 1),
                                ItemId = reader.GetInt32(2),
                                ItemNameSnapshot = GetStringOrNull(reader, 3),
                                ItemSerialSnapshot = GetStringOrNull(reader, 4),
                                SetCode = GetStringOrNull(reader, 5),
                                CompanyName = GetStringOrNull(reader, 6),
                                BranchName = GetStringOrNull(reader, 7),
                                DeptName = GetStringOrNull(reader, 8),
                                Problem = GetStringOrNull(reader, 9),
                                Diagnosis = GetStringOrNull(reader, 10),
                                Resolution = GetStringOrNull(reader, 11),
                                PartsUsed = GetStringOrNull(reader, 12),
                                Priority = GetStringOrNull(reader, 13),
                                Status = GetStringOrNull(reader, 14),
                                SubmittedByName = GetStringOrNull(reader, 15),
                                AssignedTechName = GetStringOrNull(reader, 16),
                                // Raw read, not GetLocalDateTime — see the identical comment in MapListItem.
                                DateReceived = reader.GetDateTime(17),
                                CreatedAt = GetLocalDateTime(reader, 18),
                                UpdatedAt = GetLocalDateTime(reader, 19),
                                CompletedAt = GetDateOrNull(reader, 20),
                                RequestedByType = GetStringOrNull(reader, 21),
                                RequestedByDeptId = GetIntOrNull(reader, 22),
                                RequestedByEmpId = GetIntOrNull(reader, 23),
                                DaysInCurrentStatus = reader.GetInt32(26),
                                RequestedByComId = GetIntOrNull(reader, 27),
                                RequestedByBranchId = GetIntOrNull(reader, 28),
                                RequestedByComName = GetStringOrNull(reader, 29),
                                RequestedByBranchName = GetStringOrNull(reader, 30),
                                Category = GetStringOrNull(reader, 31),
                                ModelNumber = GetStringOrNull(reader, 32)
                            };

                            var reqDeptName = GetStringOrNull(reader, 24);
                            var reqEmpName = GetStringOrNull(reader, 25);
                            detail.RequestedByDeptName = reqDeptName;
                            detail.RequestedByEmpName = reqEmpName;
                            if (string.Equals(detail.RequestedByType, "Department", StringComparison.OrdinalIgnoreCase))
                            {
                                // No "Department:"/"Employee:" prefix here — the UI already labels
                                // this line "Requested By:", so the type word was redundant.
                                var label = reqDeptName ?? "(unknown)";
                                if (!string.IsNullOrEmpty(detail.RequestedByComName) || !string.IsNullOrEmpty(detail.RequestedByBranchName))
                                {
                                    label += " (" + (detail.RequestedByComName ?? "(unknown company)");
                                    if (!string.IsNullOrEmpty(detail.RequestedByBranchName))
                                        label += " / " + detail.RequestedByBranchName;
                                    label += ")";
                                }
                                detail.RequestedByLabel = label;
                            }
                            else if (string.Equals(detail.RequestedByType, "Employee", StringComparison.OrdinalIgnoreCase))
                            {
                                detail.RequestedByLabel = reqEmpName ?? "(unknown)";
                            }
                            else
                            {
                                detail.RequestedByLabel = "Not set";
                            }
                        }
                    }
                }

                if (detail == null)
                    return null;

                using (var cmd = new SqlCommand(attachmentsSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.Attachments.Add(new RepairTicketAttachmentSummary
                            {
                                AttachmentId = reader.GetInt32(0),
                                RepairTicketId = reader.GetInt32(1),
                                AttachmentType = GetStringOrNull(reader, 2),
                                FileName = GetStringOrNull(reader, 3),
                                MimeType = GetStringOrNull(reader, 4),
                                FileSizeBytes = GetIntOrNull(reader, 5),
                                SortOrder = reader.GetInt32(6),
                                UploadedAt = GetLocalDateTime(reader, 7),
                                UploadedByName = GetStringOrNull(reader, 8)
                            });
                        }
                    }
                }

                using (var cmd = new SqlCommand(notesSql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.Notes.Add(new RepairTicketNoteItem
                            {
                                NoteId = reader.GetInt64(0),
                                RepairTicketId = reader.GetInt32(1),
                                NoteType = GetStringOrNull(reader, 2),
                                NoteText = GetStringOrNull(reader, 3),
                                CreatedAt = GetLocalDateTime(reader, 4),
                                CreatedByName = GetStringOrNull(reader, 5)
                            });
                        }
                    }
                }

                using (var cmd = new SqlCommand(historySql, con))
                {
                    cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            detail.History.Add(new RepairTicketHistoryItem
                            {
                                HistoryId = reader.GetInt64(0),
                                RepairTicketId = reader.GetInt32(1),
                                ChangedAt = GetLocalDateTime(reader, 2),
                                ChangedByUserId = GetIntOrNull(reader, 3),
                                ChangedByName = GetStringOrNull(reader, 4),
                                FieldName = GetStringOrNull(reader, 5),
                                OldValue = GetStringOrNull(reader, 6),
                                NewValue = GetStringOrNull(reader, 7),
                                Note = GetStringOrNull(reader, 8)
                            });
                        }
                    }
                }
            }

            // Part-based repair workflow additions: Observations, Parts summary, Conclusion.
            // Loaded via the same partial class's Parts.cs methods (separate connections — kept
            // simple/consistent with the rest of this method's style rather than threading one
            // connection through three more method calls).
            detail.Observations = await GetObservationsAsync(repairTicketId);
            detail.Parts = await GetPartsAsync(repairTicketId);
            detail.Conclusion = await GetConclusionAsync(repairTicketId);

            return detail;
        }

        public async Task<RepairTicketAttachmentItem> GetAttachmentAsync(int attachmentId)
        {
            const string sql = @"
SELECT a.AttachmentId, a.RepairTicketId, a.AttachmentType, a.FileName, a.MimeType, a.FileBytes,
       a.FileSizeBytes, a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairTicketAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.AttachmentId = @AttachmentId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RepairTicketAttachmentItem
                        {
                            AttachmentId = reader.GetInt32(0),
                            RepairTicketId = reader.GetInt32(1),
                            AttachmentType = GetStringOrNull(reader, 2),
                            FileName = GetStringOrNull(reader, 3),
                            MimeType = GetStringOrNull(reader, 4),
                            FileBytes = GetBytesOrNull(reader, 5),
                            FileSizeBytes = GetIntOrNull(reader, 6),
                            SortOrder = reader.GetInt32(7),
                            UploadedAt = GetLocalDateTime(reader, 8),
                            UploadedByName = GetStringOrNull(reader, 9)
                        };
                    }
                }
            }

            return null;
        }

        public async Task<RepairTicketListItem> CreateTicketAsync(NewRepairTicketRequest request, int? createdByUserId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            int newTicketId;
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_CreateTicket", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                cmd.Parameters.AddWithValue("@Problem", request.Problem);
                cmd.Parameters.AddWithValue("@Priority", OrDbNull(request.Priority));
                cmd.Parameters.AddWithValue("@SubmittedByEmpId", OrDbNull(request.SubmittedByEmpId));
                cmd.Parameters.AddWithValue("@SubmittedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@RequestedByType", OrDbNull(request.RequestedByType));
                cmd.Parameters.AddWithValue("@RequestedByDeptId", OrDbNull(request.RequestedByDeptId));
                cmd.Parameters.AddWithValue("@RequestedByEmpId", OrDbNull(request.RequestedByEmpId));
                cmd.Parameters.AddWithValue("@DateReceived", OrDbNull(request.DateReceived));
                cmd.Parameters.AddWithValue("@RequestedByComId", OrDbNull(request.RequestedByComId));
                cmd.Parameters.AddWithValue("@RequestedByBranchId", OrDbNull(request.RequestedByBranchId));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("Failed to create repair ticket.");

                    newTicketId = reader.GetInt32(0);
                }
            }

            var criteria = new RepairTicketFilterCriteria();
            var all = await GetTicketsAsync(criteria);
            return all.Find(x => x.RepairTicketId == newTicketId) ?? await GetSingleTicketAsync(newTicketId);
        }

        /// <summary>
        /// Creates the repair workflow for an IT Call, or returns its existing linked workflow when
        /// the operator retries the forward action. The database procedure owns the unique link and
        /// writes history records on both tickets in one transaction.
        /// </summary>
        public async Task<RepairTicketListItem> CreateTicketFromCallTicketAsync(NewRepairTicketRequest request, int? createdByUserId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.CallTicketId.HasValue || request.CallTicketId.Value <= 0)
                throw new ArgumentException("CallTicketId is required when forwarding an IT Call to Repair.", nameof(request));

            int repairTicketId;
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_CreateTicketFromCallTicket", con)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                cmd.Parameters.AddWithValue("@CallTicketId", request.CallTicketId.Value);
                cmd.Parameters.AddWithValue("@ItemId", request.ItemId);
                cmd.Parameters.AddWithValue("@Problem", request.Problem);
                cmd.Parameters.AddWithValue("@Priority", OrDbNull(request.Priority));
                cmd.Parameters.AddWithValue("@SubmittedByEmpId", OrDbNull(request.SubmittedByEmpId));
                cmd.Parameters.AddWithValue("@SubmittedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@RequestedByType", OrDbNull(request.RequestedByType));
                cmd.Parameters.AddWithValue("@RequestedByDeptId", OrDbNull(request.RequestedByDeptId));
                cmd.Parameters.AddWithValue("@RequestedByEmpId", OrDbNull(request.RequestedByEmpId));
                cmd.Parameters.AddWithValue("@DateReceived", OrDbNull(request.DateReceived));
                cmd.Parameters.AddWithValue("@RequestedByComId", OrDbNull(request.RequestedByComId));
                cmd.Parameters.AddWithValue("@RequestedByBranchId", OrDbNull(request.RequestedByBranchId));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("The linked repair ticket was not returned by the database.");

                    repairTicketId = reader.GetInt32(0);
                }
            }

            return await GetSingleTicketAsync(repairTicketId);
        }

        /// <summary>Returns the permanent Repair Ticket code for each linked IT Call. The result
        /// is used by IT Call Monitoring as an independent forwarding indicator; it is not derived
        /// from the parent CallTicket status, so it remains available after Solved/Temporary states.</summary>
        public async Task<Dictionary<int, string>> GetLinkedRepairTicketCodesByCallTicketIdsAsync(IEnumerable<int> callTicketIds)
        {
            var ids = (callTicketIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var links = new Dictionary<int, string>();
            if (ids.Count == 0)
                return links;

            var parameterNames = new List<string>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = con;
                for (var index = 0; index < ids.Count; index++)
                {
                    var parameterName = "@CallTicketId" + index;
                    parameterNames.Add(parameterName);
                    cmd.Parameters.AddWithValue(parameterName, ids[index]);
                }

                cmd.CommandText = @"
SELECT CallTicketId, TicketCode
FROM dbo.RepairTicket
WHERE CallTicketId IN (" + string.Join(", ", parameterNames) + @");";

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0))
                            links[reader.GetInt32(0)] = GetStringOrNull(reader, 1) ?? string.Empty;
                    }
                }
            }

            return links;
        }

        private async Task<RepairTicketListItem> GetSingleTicketAsync(int repairTicketId)
        {
            var sql = TicketListSelectSql + " WHERE t.RepairTicketId = @RepairTicketId;";
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return MapListItem(reader);
                }
            }

            return null;
        }

        public async Task SetStatusAsync(int repairTicketId, string newStatus, int? changedByUserId, string note, DateTime? completedAtOverride = null, bool skipConditionReset = false)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_SetTicketStatus", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));
                cmd.Parameters.AddWithValue("@Note", OrDbNull(note));
                cmd.Parameters.AddWithValue("@CompletedAtOverride", OrDbNull(completedAtOverride));
                cmd.Parameters.AddWithValue("@SkipConditionReset", skipConditionReset);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>Permanently deletes a repair ticket and everything under it (observations,
        /// parts, notes, evidence, timeline, conclusion — all cascade via FK ON DELETE CASCADE).
        /// For correcting an accidental intake mistake (e.g. wrong item selected).</summary>
        public async Task DeleteTicketAsync(int repairTicketId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_DeleteTicket", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>Edits the "Requested By" field on an existing ticket — same validation as
        /// CreateTicketAsync's RequestedBy* params, via sp_RepairPortal_UpdateRequestedBy.</summary>
        public async Task UpdateRequestedByAsync(int repairTicketId, string requestedByType, int? deptId, int? empId, int? changedByUserId, int? comId = null, int? branchId = null)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_UpdateRequestedBy", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@RequestedByType", OrDbNull(requestedByType));
                cmd.Parameters.AddWithValue("@RequestedByDeptId", OrDbNull(deptId));
                cmd.Parameters.AddWithValue("@RequestedByEmpId", OrDbNull(empId));
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));
                cmd.Parameters.AddWithValue("@RequestedByComId", OrDbNull(comId));
                cmd.Parameters.AddWithValue("@RequestedByBranchId", OrDbNull(branchId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task SetPriorityAsync(int repairTicketId, string newPriority, int? changedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_SetTicketPriority", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@NewPriority", newPriority);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task AddNoteAsync(int repairTicketId, string noteText, string noteType, int? createdByUserId, int? createdByEmpId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_AddTicketNote", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@NoteText", noteText);
                cmd.Parameters.AddWithValue("@NoteType", OrDbNull(noteType));
                cmd.Parameters.AddWithValue("@CreatedByUserId", OrDbNull(createdByUserId));
                cmd.Parameters.AddWithValue("@CreatedByEmpId", OrDbNull(createdByEmpId));

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> AddAttachmentAsync(int repairTicketId, string attachmentType, string fileName, string mimeType, byte[] bytes, int? uploadedByUserId)
        {
            // Only ticket-level Image attachments feed the list-view thumbnail (see
            // TicketListSelectSql's OUTER APPLY) — generating this once at upload time means the
            // list query never has to stream a full-resolution photo just to show a 96px preview.
            var thumbnailBytes = string.Equals(attachmentType, "Image", StringComparison.OrdinalIgnoreCase)
                ? CreateThumbnailBytes(bytes)
                : null;

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_AddAttachment", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@AttachmentType", attachmentType);
                cmd.Parameters.AddWithValue("@FileName", OrDbNull(fileName));
                cmd.Parameters.AddWithValue("@MimeType", OrDbNull(mimeType));
                cmd.Parameters.AddWithValue("@FileBytes", OrDbNull(bytes));
                cmd.Parameters.AddWithValue("@FileSizeBytes", bytes != null ? (object)bytes.Length : DBNull.Value);
                cmd.Parameters.AddWithValue("@UploadedByUserId", OrDbNull(uploadedByUserId));
                cmd.Parameters.AddWithValue("@ThumbnailBytes", OrDbNull(thumbnailBytes));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return reader.GetInt32(0);
                }
            }

            return 0;
        }

        /// <summary>Downscales an image to fit within maxDimension x maxDimension and re-encodes as
        /// JPEG at the given quality. Defaults (400px/85) are the list-view thumbnail — typically
        /// tens of KB instead of several MB, while still looking sharp at the Gallery card's ~300px
        /// display width. RepairReportBuilder calls this with a larger maxDimension/quality for the
        /// handful of images actually picked for a printed report, where softness/black-flattening
        /// artifacts are far more visible than at Gallery-card size. Returns null if the source
        /// bytes aren't a decodable image (caller then falls back to a placeholder).</summary>
        private static byte[] CreateThumbnailBytes(byte[] sourceBytes, int maxDimension = 400, long quality = 85L)
        {
            if (sourceBytes == null || sourceBytes.Length == 0) return null;

            try
            {
                using (var sourceStream = new System.IO.MemoryStream(sourceBytes))
                using (var source = System.Drawing.Image.FromStream(sourceStream))
                {
                    var scale = Math.Min(1.0, (double)maxDimension / Math.Max(source.Width, source.Height));
                    var width = Math.Max(1, (int)(source.Width * scale));
                    var height = Math.Max(1, (int)(source.Height * scale));

                    using (var thumb = new System.Drawing.Bitmap(width, height))
                    using (var g = System.Drawing.Graphics.FromImage(thumb))
                    {
                        // JPEG has no alpha channel — without this, transparent PNG regions default
                        // to black once flattened, showing up as black borders/corners in the
                        // Gallery thumbnail even though the original image looks fine.
                        g.Clear(System.Drawing.Color.White);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, width, height);

                        var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                            .FirstOrDefault(e => e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                        using (var encoderParams = new System.Drawing.Imaging.EncoderParameters(1))
                        using (var outStream = new System.IO.MemoryStream())
                        {
                            encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                            if (jpegEncoder != null)
                                thumb.Save(outStream, jpegEncoder, encoderParams);
                            else
                                thumb.Save(outStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                            return outStream.ToArray();
                        }
                    }
                }
            }
            catch
            {
                // Not a decodable image (or a format GDI+ can't read) — no thumbnail, list view
                // falls back to the placeholder icon.
                return null;
            }
        }

        public async Task DeleteAttachmentAsync(int attachmentId, int? changedByUserId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_DeleteAttachment", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(changedByUserId));
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<RepairTicketHistoryItem>> GetHistoryAsync(int repairTicketId)
        {
            const string sql = @"
SELECT h.HistoryId, h.RepairTicketId, h.ChangedAt, h.ChangedByUserId, u.Name AS ChangedByName,
       h.FieldName, h.OldValue, h.NewValue, h.Note
FROM dbo.RepairTicketHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.RepairTicketId = @RepairTicketId
ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";

            var list = new List<RepairTicketHistoryItem>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new RepairTicketHistoryItem
                        {
                            HistoryId = reader.GetInt64(0),
                            RepairTicketId = reader.GetInt32(1),
                            ChangedAt = GetLocalDateTime(reader, 2),
                            ChangedByUserId = GetIntOrNull(reader, 3),
                            ChangedByName = GetStringOrNull(reader, 4),
                            FieldName = GetStringOrNull(reader, 5),
                            OldValue = GetStringOrNull(reader, 6),
                            NewValue = GetStringOrNull(reader, 7),
                            Note = GetStringOrNull(reader, 8)
                        });
                    }
                }
            }

            return list;
        }
    }
}
