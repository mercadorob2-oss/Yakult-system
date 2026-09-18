<%@ WebHandler Language="C#" Class="RepairTicketMobileDetailHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>
/// Full mobile-safe Repair Portal read model. Evidence is represented by metadata only; binary
/// content is deliberately not embedded in a detail response. The existing authenticated photo
/// upload flow remains the write path for part evidence.
/// </summary>
public sealed class RepairTicketMobileDetailHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "GET, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;
        int ticketId;
        if (!int.TryParse(context.Request.QueryString["ticketId"], out ticketId) || ticketId <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "ticketId is required");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.CanReadTicket(connection, ticketId, actor))
                {
                    RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                    return;
                }

                var hasCallLink = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId");
                var linkedCallSelect = hasCallLink
                    ? "t.CallTicketId AS LinkedCallTicketId"
                    : "CAST(NULL AS INT) AS LinkedCallTicketId";
                var headerSql = @"
SELECT
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot,
    t.SetCode, t.Problem, t.Diagnosis, t.Resolution, t.PartsUsed, t.Priority, t.Status,
    t.DateReceived, t.CreatedAt, t.UpdatedAt, t.CompletedAt,
    t.SubmittedByUserId, t.SubmittedByEmpId, t.AssignedTechEmpId,
    t.RequestedByType, t.RequestedByComId, t.RequestedByBranchId, t.RequestedByDeptId, t.RequestedByEmpId,
    i.Name AS CurrentItemName, i.SerialNumber AS CurrentSerialNumber, i.ModelNumber, i.Category,
    subEmp.Name AS SubmittedByName, assignedEmp.Name AS AssignedTechnicianName,
    reqEmp.Name AS RequestedByEmployeeName, reqDept.Name AS RequestedByDepartmentName,
    reqCom.Name AS RequestedByCompanyName, reqBranch.Name AS RequestedByBranchName,
    " + linkedCallSelect + @"
FROM dbo.RepairTicket t
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
LEFT JOIN dbo.Employee subEmp ON subEmp.EmpId = t.SubmittedByEmpId
LEFT JOIN dbo.Employee assignedEmp ON assignedEmp.EmpId = t.AssignedTechEmpId
LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = t.RequestedByEmpId
LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = t.RequestedByDeptId
LEFT JOIN dbo.Company reqCom ON reqCom.ComId = t.RequestedByComId
LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId = t.RequestedByBranchId
WHERE t.RepairTicketId = @TicketId;";

                object ticket = null;
                int? linkedCallTicketId = null;
                using (var command = new SqlCommand(headerSql, connection))
                {
                    command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                            return;
                        }
                        linkedCallTicketId = RepairMobileApiSupport.IntValue(reader, "LinkedCallTicketId");
                        ticket = new
                        {
                            repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                            ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                            itemId = Convert.ToInt32(reader["ItemId"]),
                            itemName = RepairMobileApiSupport.StringValue(reader, "ItemNameSnapshot") ?? RepairMobileApiSupport.StringValue(reader, "CurrentItemName"),
                            serialNumber = RepairMobileApiSupport.StringValue(reader, "ItemSerialSnapshot") ?? RepairMobileApiSupport.StringValue(reader, "CurrentSerialNumber"),
                            modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                            category = RepairMobileApiSupport.StringValue(reader, "Category"),
                            setCode = RepairMobileApiSupport.StringValue(reader, "SetCode"),
                            problem = RepairMobileApiSupport.StringValue(reader, "Problem"),
                            diagnosis = RepairMobileApiSupport.StringValue(reader, "Diagnosis"),
                            resolution = RepairMobileApiSupport.StringValue(reader, "Resolution"),
                            partsUsed = RepairMobileApiSupport.StringValue(reader, "PartsUsed"),
                            priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                            status = RepairMobileApiSupport.StringValue(reader, "Status"),
                            dateReceived = RepairMobileApiSupport.IsoDate(reader["DateReceived"]),
                            createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                            updatedAt = RepairMobileApiSupport.IsoUtc(reader["UpdatedAt"]),
                            completedAt = RepairMobileApiSupport.IsoUtc(reader["CompletedAt"]),
                            submittedByUserId = RepairMobileApiSupport.IntValue(reader, "SubmittedByUserId"),
                            submittedByEmployeeId = RepairMobileApiSupport.IntValue(reader, "SubmittedByEmpId"),
                            submittedByName = RepairMobileApiSupport.StringValue(reader, "SubmittedByName"),
                            assignedTechnicianId = RepairMobileApiSupport.IntValue(reader, "AssignedTechEmpId"),
                            assignedTechnicianName = RepairMobileApiSupport.StringValue(reader, "AssignedTechnicianName"),
                            requestedByType = RepairMobileApiSupport.StringValue(reader, "RequestedByType"),
                            requestedByEmployeeId = RepairMobileApiSupport.IntValue(reader, "RequestedByEmpId"),
                            requestedByEmployeeName = RepairMobileApiSupport.StringValue(reader, "RequestedByEmployeeName"),
                            requestedByDepartmentId = RepairMobileApiSupport.IntValue(reader, "RequestedByDeptId"),
                            requestedByDepartmentName = RepairMobileApiSupport.StringValue(reader, "RequestedByDepartmentName"),
                            requestedByCompanyId = RepairMobileApiSupport.IntValue(reader, "RequestedByComId"),
                            requestedByCompanyName = RepairMobileApiSupport.StringValue(reader, "RequestedByCompanyName"),
                            requestedByBranchId = RepairMobileApiSupport.IntValue(reader, "RequestedByBranchId"),
                            requestedByBranchName = RepairMobileApiSupport.StringValue(reader, "RequestedByBranchName"),
                            linkedCallTicketId = linkedCallTicketId
                        };
                    }
                }

                var notes = ReadNotes(connection, ticketId);
                var history = ReadHistory(connection, ticketId);
                var observations = ReadObservations(connection, ticketId);
                var attachments = ReadTicketAttachments(connection, ticketId);
                var parts = ReadParts(connection, ticketId);
                var partNotes = ReadPartNotes(connection, ticketId);
                var partHistory = ReadPartHistory(connection, ticketId);
                var partAttachments = ReadPartAttachments(connection, ticketId);
                var conclusion = ReadConclusion(connection, ticketId);
                var linkedCall = linkedCallTicketId.HasValue ? ReadLinkedCall(connection, linkedCallTicketId.Value) : null;
                var spare = ReadActiveSpare(connection, ticketId);

                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    isTechnician = actor.IsItAuthorized,
                    ticket = ticket,
                    notes = notes,
                    history = history,
                    observations = observations,
                    attachments = attachments,
                    parts = parts,
                    partNotes = partNotes,
                    partHistory = partHistory,
                    partAttachments = partAttachments,
                    conclusion = conclusion,
                    linkedCall = linkedCall,
                    activeSpare = spare
                });
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair ticket details are temporarily unavailable");
        }
    }

    private static List<object> ReadNotes(SqlConnection connection, int ticketId)
    {
        const string sql = @"
SELECT n.NoteId, n.NoteType, n.NoteText, n.CreatedAt, COALESCE(u.Name, e.Name) AS CreatedByName
FROM dbo.RepairTicketNote n
LEFT JOIN dbo.[User] u ON u.UserId = n.CreatedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId = n.CreatedByEmpId
WHERE n.RepairTicketId = @TicketId
ORDER BY n.CreatedAt DESC, n.NoteId DESC;";
        var rows = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    noteId = Convert.ToInt64(reader["NoteId"]),
                    noteType = RepairMobileApiSupport.StringValue(reader, "NoteType"),
                    noteText = RepairMobileApiSupport.StringValue(reader, "NoteText"),
                    createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                    createdByName = RepairMobileApiSupport.StringValue(reader, "CreatedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadHistory(SqlConnection connection, int ticketId)
    {
        const string sql = @"
SELECT h.HistoryId, h.ChangedAt, h.FieldName, h.OldValue, h.NewValue, h.Note, u.Name AS ChangedByName
FROM dbo.RepairTicketHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.RepairTicketId = @TicketId
ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";
        var rows = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    historyId = Convert.ToInt64(reader["HistoryId"]),
                    changedAt = RepairMobileApiSupport.IsoUtc(reader["ChangedAt"]),
                    fieldName = RepairMobileApiSupport.StringValue(reader, "FieldName"),
                    oldValue = RepairMobileApiSupport.StringValue(reader, "OldValue"),
                    newValue = RepairMobileApiSupport.StringValue(reader, "NewValue"),
                    note = RepairMobileApiSupport.StringValue(reader, "Note"),
                    changedByName = RepairMobileApiSupport.StringValue(reader, "ChangedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadObservations(SqlConnection connection, int ticketId)
    {
        var rows = new List<object>();
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairItemObservation")) return rows;
        const string sql = @"
SELECT o.ObservationId, o.SortOrder, o.ObservationText, o.CreatedAt, o.UpdatedAt, u.Name AS CreatedByName
FROM dbo.RepairItemObservation o
LEFT JOIN dbo.[User] u ON u.UserId = o.CreatedByUserId
WHERE o.RepairTicketId = @TicketId
ORDER BY o.SortOrder, o.ObservationId;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    observationId = Convert.ToInt32(reader["ObservationId"]),
                    sortOrder = Convert.ToInt32(reader["SortOrder"]),
                    text = RepairMobileApiSupport.StringValue(reader, "ObservationText"),
                    createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                    updatedAt = RepairMobileApiSupport.IsoUtc(reader["UpdatedAt"]),
                    createdByName = RepairMobileApiSupport.StringValue(reader, "CreatedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadTicketAttachments(SqlConnection connection, int ticketId)
    {
        const string sql = @"
SELECT a.AttachmentId, a.AttachmentType, a.FileName, a.MimeType, a.FileSizeBytes, a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairTicketAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.RepairTicketId = @TicketId
ORDER BY a.SortOrder, a.AttachmentId;";
        var rows = new List<object>();
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    attachmentId = Convert.ToInt32(reader["AttachmentId"]),
                    attachmentType = RepairMobileApiSupport.StringValue(reader, "AttachmentType"),
                    fileName = RepairMobileApiSupport.StringValue(reader, "FileName"),
                    mimeType = RepairMobileApiSupport.StringValue(reader, "MimeType"),
                    fileSizeBytes = RepairMobileApiSupport.IntValue(reader, "FileSizeBytes"),
                    sortOrder = Convert.ToInt32(reader["SortOrder"]),
                    uploadedAt = RepairMobileApiSupport.IsoUtc(reader["UploadedAt"]),
                    uploadedByName = RepairMobileApiSupport.StringValue(reader, "UploadedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadParts(SqlConnection connection, int ticketId)
    {
        var rows = new List<object>();
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairPart")) return rows;
        const string sql = @"
SELECT p.RepairPartId, p.PartNumber, p.CustomLabel, p.PartDisplayName, p.ProblemDescription, p.Status, p.Severity, p.CreatedAt, p.UpdatedAt,
       (SELECT COUNT(1) FROM dbo.RepairPartNote n WHERE n.RepairPartId = p.RepairPartId) AS NoteCount,
       (SELECT COUNT(1) FROM dbo.RepairPartAttachment a WHERE a.RepairPartId = p.RepairPartId) AS AttachmentCount
FROM dbo.RepairPart p
WHERE p.RepairTicketId = @TicketId
ORDER BY p.PartNumber;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    repairPartId = Convert.ToInt32(reader["RepairPartId"]),
                    partNumber = Convert.ToInt32(reader["PartNumber"]),
                    customLabel = RepairMobileApiSupport.StringValue(reader, "CustomLabel"),
                    displayName = RepairMobileApiSupport.StringValue(reader, "PartDisplayName"),
                    problemDescription = RepairMobileApiSupport.StringValue(reader, "ProblemDescription"),
                    status = RepairMobileApiSupport.StringValue(reader, "Status"),
                    severity = RepairMobileApiSupport.StringValue(reader, "Severity"),
                    createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                    updatedAt = RepairMobileApiSupport.IsoUtc(reader["UpdatedAt"]),
                    noteCount = Convert.ToInt32(reader["NoteCount"]),
                    attachmentCount = Convert.ToInt32(reader["AttachmentCount"])
                });
        }
        return rows;
    }

    private static List<object> ReadPartNotes(SqlConnection connection, int ticketId)
    {
        var rows = new List<object>();
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairPartNote")) return rows;
        const string sql = @"
SELECT n.PartNoteId, n.RepairPartId, n.NoteType, n.NoteText, n.CreatedAt, COALESCE(u.Name, e.Name) AS CreatedByName
FROM dbo.RepairPartNote n
LEFT JOIN dbo.[User] u ON u.UserId = n.CreatedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId = n.CreatedByEmpId
WHERE n.RepairTicketId = @TicketId
ORDER BY n.CreatedAt DESC, n.PartNoteId DESC;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    partNoteId = Convert.ToInt64(reader["PartNoteId"]),
                    repairPartId = Convert.ToInt32(reader["RepairPartId"]),
                    noteType = RepairMobileApiSupport.StringValue(reader, "NoteType"),
                    noteText = RepairMobileApiSupport.StringValue(reader, "NoteText"),
                    createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                    createdByName = RepairMobileApiSupport.StringValue(reader, "CreatedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadPartHistory(SqlConnection connection, int ticketId)
    {
        var rows = new List<object>();
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairPartHistory")) return rows;
        const string sql = @"
SELECT h.PartHistoryId, h.RepairPartId, h.ChangedAt, h.FieldName, h.OldValue, h.NewValue, h.Note, u.Name AS ChangedByName
FROM dbo.RepairPartHistory h
LEFT JOIN dbo.[User] u ON u.UserId = h.ChangedByUserId
WHERE h.RepairTicketId = @TicketId
ORDER BY h.ChangedAt DESC, h.PartHistoryId DESC;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    partHistoryId = Convert.ToInt64(reader["PartHistoryId"]),
                    repairPartId = Convert.ToInt32(reader["RepairPartId"]),
                    changedAt = RepairMobileApiSupport.IsoUtc(reader["ChangedAt"]),
                    fieldName = RepairMobileApiSupport.StringValue(reader, "FieldName"),
                    oldValue = RepairMobileApiSupport.StringValue(reader, "OldValue"),
                    newValue = RepairMobileApiSupport.StringValue(reader, "NewValue"),
                    note = RepairMobileApiSupport.StringValue(reader, "Note"),
                    changedByName = RepairMobileApiSupport.StringValue(reader, "ChangedByName")
                });
        }
        return rows;
    }

    private static List<object> ReadPartAttachments(SqlConnection connection, int ticketId)
    {
        var rows = new List<object>();
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairPartAttachment")) return rows;
        const string sql = @"
SELECT a.PartAttachmentId, a.RepairPartId, a.AttachmentType, a.FileName, a.MimeType, a.FileSizeBytes, a.SortOrder, a.UploadedAt, u.Name AS UploadedByName
FROM dbo.RepairPartAttachment a
LEFT JOIN dbo.[User] u ON u.UserId = a.UploadedByUserId
WHERE a.RepairTicketId = @TicketId
ORDER BY a.RepairPartId, a.SortOrder, a.PartAttachmentId;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
                while (reader.Read()) rows.Add(new
                {
                    partAttachmentId = Convert.ToInt32(reader["PartAttachmentId"]),
                    repairPartId = Convert.ToInt32(reader["RepairPartId"]),
                    attachmentType = RepairMobileApiSupport.StringValue(reader, "AttachmentType"),
                    fileName = RepairMobileApiSupport.StringValue(reader, "FileName"),
                    mimeType = RepairMobileApiSupport.StringValue(reader, "MimeType"),
                    fileSizeBytes = RepairMobileApiSupport.IntValue(reader, "FileSizeBytes"),
                    sortOrder = Convert.ToInt32(reader["SortOrder"]),
                    uploadedAt = RepairMobileApiSupport.IsoUtc(reader["UploadedAt"]),
                    uploadedByName = RepairMobileApiSupport.StringValue(reader, "UploadedByName")
                });
        }
        return rows;
    }

    private static object ReadConclusion(SqlConnection connection, int ticketId)
    {
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairConclusion")) return null;
        var hasVendor = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairConclusion", "HandedOverToVendorId");
        var hasVendorLookup = hasVendor && RepairMobileApiSupport.HasTable(connection, "dbo.Vendor");
        var hasDisposition = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairConclusion", "Disposition");
        var sql = @"
SELECT c.RootCause, c.WorkPerformed, c.FinalOutcome, c.Recommendations, c.CompletedAt,
       COALESCE(u.Name, e.Name) AS CompletedByName,
       " + (hasVendor ? "c.HandedOverToVendorId" : "CAST(NULL AS INT)") + @" AS HandedOverToVendorId,
       " + (hasVendorLookup ? "v.VendorName" : "CAST(NULL AS NVARCHAR(200))") + @" AS HandedOverToVendorName,
       " + (hasDisposition ? "c.Disposition, c.ReplacementItemId" : "CAST(NULL AS NVARCHAR(20)) AS Disposition, CAST(NULL AS INT) AS ReplacementItemId") + @"
FROM dbo.RepairConclusion c
LEFT JOIN dbo.[User] u ON u.UserId = c.CompletedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId = c.CompletedByEmpId
" + (hasVendorLookup ? "LEFT JOIN dbo.Vendor v ON v.VendorID = c.HandedOverToVendorId" : string.Empty) + @"
WHERE c.RepairTicketId = @TicketId;";

        string rootCause;
        string workPerformed;
        string finalOutcome;
        string recommendations;
        string completedAt;
        string completedByName;
        int? handedOverToVendorId;
        string handedOverToVendorName;
        string disposition;
        int? replacementItemId;
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                rootCause = RepairMobileApiSupport.StringValue(reader, "RootCause");
                workPerformed = RepairMobileApiSupport.StringValue(reader, "WorkPerformed");
                finalOutcome = RepairMobileApiSupport.StringValue(reader, "FinalOutcome");
                recommendations = RepairMobileApiSupport.StringValue(reader, "Recommendations");
                completedAt = RepairMobileApiSupport.IsoUtc(reader["CompletedAt"]);
                completedByName = RepairMobileApiSupport.StringValue(reader, "CompletedByName");
                handedOverToVendorId = RepairMobileApiSupport.IntValue(reader, "HandedOverToVendorId");
                handedOverToVendorName = RepairMobileApiSupport.StringValue(reader, "HandedOverToVendorName");
                disposition = RepairMobileApiSupport.StringValue(reader, "Disposition");
                replacementItemId = RepairMobileApiSupport.IntValue(reader, "ReplacementItemId");
            }
        }

        var repairedBy = new List<object>();
        if (RepairMobileApiSupport.HasTable(connection, "dbo.RepairConclusionRepairedByEmp"))
        {
            using (var repairedByCommand = new SqlCommand(@"
SELECT e.EmpId, e.Name FROM dbo.RepairConclusionRepairedByEmp r
INNER JOIN dbo.Employee e ON e.EmpId = r.EmpId
WHERE r.RepairTicketId = @TicketId ORDER BY e.Name;", connection))
            {
                repairedByCommand.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                using (var repairedByReader = repairedByCommand.ExecuteReader())
                    while (repairedByReader.Read()) repairedBy.Add(new
                    {
                        employeeId = Convert.ToInt32(repairedByReader["EmpId"]),
                        name = RepairMobileApiSupport.StringValue(repairedByReader, "Name")
                    });
            }
        }
        return new
        {
            rootCause = rootCause,
            workPerformed = workPerformed,
            finalOutcome = finalOutcome,
            recommendations = recommendations,
            completedAt = completedAt,
            completedByName = completedByName,
            handedOverToVendorId = handedOverToVendorId,
            handedOverToVendorName = handedOverToVendorName,
            disposition = disposition,
            replacementItemId = replacementItemId,
            repairedBy = repairedBy
        };
    }

    private static object ReadLinkedCall(SqlConnection connection, int callTicketId)
    {
        const string sql = "SELECT TicketId, TicketCode, Status, Priority, Issue, CallerName, CreatedAt FROM dbo.CallTicket WHERE TicketId = @TicketId;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = callTicketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new
                {
                    ticketId = Convert.ToInt32(reader["TicketId"]),
                    ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                    status = RepairMobileApiSupport.StringValue(reader, "Status"),
                    priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                    issue = RepairMobileApiSupport.StringValue(reader, "Issue"),
                    callerName = RepairMobileApiSupport.StringValue(reader, "CallerName"),
                    createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                    isForwardedToRepair = true
                };
            }
        }
    }

    private static object ReadActiveSpare(SqlConnection connection, int ticketId)
    {
        if (!RepairMobileApiSupport.HasTable(connection, "dbo.BorrowLog") ||
            !RepairMobileApiSupport.HasColumn(connection, "dbo.BorrowLog", "RepairTicketId")) return null;
        const string sql = @"
SELECT TOP (1) BorrowId, ItemId, SerialNumber, ItemName, ModelNumber, BorrowedAtUtc, BorrowedByEmpName, BorrowedByDeptName
FROM dbo.BorrowLog
WHERE RepairTicketId = @TicketId AND ReturnedAtUtc IS NULL
ORDER BY BorrowedAtUtc DESC;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new
                {
                    borrowId = Convert.ToInt32(reader["BorrowId"]),
                    itemId = Convert.ToInt32(reader["ItemId"]),
                    serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber"),
                    itemName = RepairMobileApiSupport.StringValue(reader, "ItemName"),
                    modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                    borrowedAt = RepairMobileApiSupport.IsoUtc(reader["BorrowedAtUtc"]),
                    borrowerName = RepairMobileApiSupport.StringValue(reader, "BorrowedByEmpName") ?? RepairMobileApiSupport.StringValue(reader, "BorrowedByDeptName")
                };
            }
        }
    }

    public bool IsReusable { get { return false; } }
}
