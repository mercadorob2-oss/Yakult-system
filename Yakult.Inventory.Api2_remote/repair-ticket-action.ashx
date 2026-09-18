<%@ WebHandler Language="C#" Class="RepairTicketActionHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json.Linq;

/// <summary>
/// Authorized mobile Repair Portal mutations. Requester users can add a requester note to a ticket
/// they own; all technician workflow mutations require the existing IT authorization model.
/// Inventory lifecycle actions are permitted only through the explicit transaction procedures
/// installed by Migration_RepairPortal_MobileLifecycle_StoredProcs.sql.
/// </summary>
public sealed class RepairTicketActionHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "POST, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;
        JObject request;
        if (!RepairMobileApiSupport.TryReadObject(context, out request))
        {
            RepairMobileApiSupport.Error(context, 400, "A valid JSON request body is required");
            return;
        }

        var action = (RepairMobileApiSupport.Text(request, "action", 50) ?? string.Empty).ToLowerInvariant();
        if (action == "timein" || action == "timeout")
        {
            HandleAttendance(context, actor, action);
            return;
        }

        var ticketId = RepairMobileApiSupport.Int(request, "ticketId");
        if (!ticketId.HasValue || ticketId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "ticketId is required");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.CanReadTicket(connection, ticketId.Value, actor))
                {
                    RepairMobileApiSupport.Error(context, 404, "Repair ticket not found");
                    return;
                }

                var requesterNote = action == "addnote" && !actor.IsItAuthorized;
                if (!requesterNote && !actor.IsItAuthorized)
                {
                    RepairMobileApiSupport.Error(context, 403, "Your account is not authorized for technician repair actions");
                    return;
                }

                switch (action)
                {
                    case "addnote":
                        AddNote(context, connection, ticketId.Value, actor, request, requesterNote);
                        return;
                    case "setstatus":
                        SetStatus(context, connection, ticketId.Value, actor, request);
                        return;
                    case "setpriority":
                        SetPriority(context, connection, ticketId.Value, actor, request);
                        return;
                    case "addobservation":
                        AddObservation(context, connection, ticketId.Value, actor, request);
                        return;
                    case "updateobservation":
                        UpdateObservation(context, connection, ticketId.Value, request);
                        return;
                    case "deleteobservation":
                        DeleteObservation(context, connection, ticketId.Value, request);
                        return;
                    case "reorderobservations":
                        ReorderObservations(context, connection, ticketId.Value, request);
                        return;
                    case "createpart":
                        CreatePart(context, connection, ticketId.Value, actor, request);
                        return;
                    case "setpartstatus":
                        SetPartStatus(context, connection, ticketId.Value, actor, request);
                        return;
                    case "addpartnote":
                        AddPartNote(context, connection, ticketId.Value, actor, request);
                        return;
                    case "updatepartlabel":
                        UpdatePartLabel(context, connection, ticketId.Value, actor, request);
                        return;
                    case "deletepart":
                        DeletePart(context, connection, ticketId.Value, actor, request);
                        return;
                    case "deleteattachment":
                        DeleteTicketAttachment(context, connection, ticketId.Value, actor, request);
                        return;
                    case "deletepartattachment":
                        DeletePartAttachment(context, connection, ticketId.Value, actor, request);
                        return;
                    case "saveconclusion":
                        SaveConclusion(context, connection, ticketId.Value, actor, request);
                        return;
                    case "updaterequestedby":
                        UpdateRequestedBy(context, connection, ticketId.Value, actor, request);
                        return;
                    case "setdisposition":
                        SetDisposition(context, connection, ticketId.Value, actor, request);
                        return;
                    case "assignspare":
                        AssignSpare(context, connection, ticketId.Value, actor, request);
                        return;
                    case "returnspare":
                        ReturnSpare(context, connection, ticketId.Value, actor);
                        return;
                    default:
                        RepairMobileApiSupport.Error(context, 400, "Unsupported repair action");
                        return;
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "The repair action could not be completed");
        }
    }

    private static void AddNote(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request, bool requesterNote)
    {
        var noteText = RepairMobileApiSupport.Text(request, "noteText", 4000);
        var noteType = RepairMobileApiSupport.Text(request, "noteType", 30) ?? "Note";
        if (string.IsNullOrWhiteSpace(noteText))
        {
            RepairMobileApiSupport.Error(context, 400, "noteText is required");
            return;
        }
        if (requesterNote) noteType = "Requester";
        using (var command = new SqlCommand("dbo.sp_RepairPortal_AddTicketNote", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@NoteText", SqlDbType.NVarChar, 4000).Value = noteText;
            command.Parameters.Add("@NoteType", SqlDbType.NVarChar, 30).Value = noteType;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@CreatedByEmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Note added" });
    }

    private static void SetStatus(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var status = RepairMobileApiSupport.Text(request, "status", 20);
        var note = RepairMobileApiSupport.Text(request, "note", 2000);
        if (string.IsNullOrWhiteSpace(status))
        {
            RepairMobileApiSupport.Error(context, 400, "status is required");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_SetTicketStatus", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@NewStatus", SqlDbType.NVarChar, 20).Value = status;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@Note", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(note);
            command.ExecuteNonQuery();
        }
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_ReturnSpare"))
        {
            try
            {
                using (var returnCommand = new SqlCommand("dbo.sp_RepairPortal_ReturnSpare", connection))
                {
                    returnCommand.CommandType = CommandType.StoredProcedure;
                    returnCommand.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
                    returnCommand.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
                    returnCommand.ExecuteNonQuery();
                }
            }
            catch { }
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Status updated" });
    }

    private static void SetPriority(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var priority = RepairMobileApiSupport.Text(request, "priority", 20);
        if (string.IsNullOrWhiteSpace(priority))
        {
            RepairMobileApiSupport.Error(context, 400, "priority is required");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_SetTicketPriority", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@NewPriority", SqlDbType.NVarChar, 20).Value = priority;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Priority updated" });
    }

    private static void AddObservation(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var text = RepairMobileApiSupport.Text(request, "text", 2000);
        if (string.IsNullOrWhiteSpace(text))
        {
            RepairMobileApiSupport.Error(context, 400, "Observation text is required");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_AddObservation", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@ObservationText", SqlDbType.NVarChar, 2000).Value = text;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
            object observationId = command.ExecuteScalar();
            RepairMobileApiSupport.Ok(context, new { success = true, message = "Observation added", observationId = observationId == null ? (int?)null : Convert.ToInt32(observationId) });
        }
    }

    private static void UpdateObservation(HttpContext context, SqlConnection connection, int ticketId, JObject request)
    {
        var observationId = RepairMobileApiSupport.Int(request, "observationId");
        var text = RepairMobileApiSupport.Text(request, "text", 2000);
        if (!observationId.HasValue || observationId.Value <= 0 || string.IsNullOrWhiteSpace(text))
        {
            RepairMobileApiSupport.Error(context, 400, "observationId and text are required");
            return;
        }
        EnsureObservationBelongsToTicket(connection, observationId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_UpdateObservation", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@ObservationId", SqlDbType.Int).Value = observationId.Value;
            command.Parameters.Add("@ObservationText", SqlDbType.NVarChar, 2000).Value = text;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Observation updated" });
    }

    private static void DeleteObservation(HttpContext context, SqlConnection connection, int ticketId, JObject request)
    {
        var observationId = RepairMobileApiSupport.Int(request, "observationId");
        if (!observationId.HasValue || observationId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "observationId is required");
            return;
        }
        EnsureObservationBelongsToTicket(connection, observationId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_DeleteObservation", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@ObservationId", SqlDbType.Int).Value = observationId.Value;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Observation deleted" });
    }

    private static void ReorderObservations(HttpContext context, SqlConnection connection, int ticketId, JObject request)
    {
        var source = request["orderedObservationIds"] as JArray;
        if (source == null || source.Count == 0 || source.Count > 100)
        {
            RepairMobileApiSupport.Error(context, 400, "orderedObservationIds must contain between 1 and 100 observation IDs");
            return;
        }
        var ids = new List<int>();
        foreach (var token in source)
        {
            int value;
            if (!int.TryParse(token.ToString(), out value) || value <= 0 || ids.Contains(value))
            {
                RepairMobileApiSupport.Error(context, 400, "orderedObservationIds contains an invalid or duplicate ID");
                return;
            }
            ids.Add(value);
        }
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                for (var index = 0; index < ids.Count; index++)
                {
                    using (var command = new SqlCommand(@"UPDATE dbo.RepairItemObservation
SET SortOrder = @SortOrder
WHERE ObservationId = @ObservationId AND RepairTicketId = @RepairTicketId;", connection, transaction))
                    {
                        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = index;
                        command.Parameters.Add("@ObservationId", SqlDbType.Int).Value = ids[index];
                        command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Observation not found");
                    }
                }
                transaction.Commit();
            }
            catch
            {
                try { transaction.Rollback(); } catch { }
                throw;
            }
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Observation order updated" });
    }

    private static void DeletePart(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var partId = RepairMobileApiSupport.Int(request, "repairPartId");
        if (!partId.HasValue || partId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "repairPartId is required");
            return;
        }
        EnsurePartBelongsToTicket(connection, partId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_DeletePart", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairPartId", SqlDbType.Int).Value = partId.Value;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Part deleted" });
    }

    private static void DeleteTicketAttachment(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var attachmentId = RepairMobileApiSupport.Int(request, "attachmentId");
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "attachmentId is required");
            return;
        }
        EnsureTicketAttachmentBelongsToTicket(connection, attachmentId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_DeleteAttachment", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = attachmentId.Value;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Evidence deleted" });
    }

    private static void DeletePartAttachment(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var attachmentId = RepairMobileApiSupport.Int(request, "partAttachmentId");
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "partAttachmentId is required");
            return;
        }
        EnsurePartAttachmentBelongsToTicket(connection, attachmentId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_DeletePartAttachment", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@PartAttachmentId", SqlDbType.Int).Value = attachmentId.Value;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Part evidence deleted" });
    }

    private static void SetDisposition(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        if (!RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_SetDispositionAtomic"))
        {
            RepairMobileApiSupport.Error(context, 503, "Repair lifecycle actions are not installed for this environment");
            return;
        }
        var disposition = RepairMobileApiSupport.Text(request, "disposition", 20);
        if (string.Equals(disposition, "clear", StringComparison.OrdinalIgnoreCase)) disposition = null;
        if (!string.IsNullOrWhiteSpace(disposition) &&
            !string.Equals(disposition, "Discard", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 400, "disposition must be Discard, Replace, or clear");
            return;
        }
        var replacementItemId = RepairMobileApiSupport.Int(request, "replacementItemId");
        var note = RepairMobileApiSupport.Text(request, "note", 2000);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_SetDispositionAtomic", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@Disposition", SqlDbType.NVarChar, 20).Value = RepairMobileApiSupport.DbValue(disposition);
            command.Parameters.Add("@ReplacementItemId", SqlDbType.Int).Value = replacementItemId.HasValue ? (object)replacementItemId.Value : DBNull.Value;
            command.Parameters.Add("@DecidedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@DecidedByDisplayName", SqlDbType.NVarChar, 200).Value = RepairMobileApiSupport.DbValue(actor.Username);
            command.Parameters.Add("@Note", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(note);
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    RepairMobileApiSupport.Error(context, 503, "Disposition did not return a result");
                    return;
                }
                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    message = disposition == null ? "Disposition unlinked" : "Disposition saved",
                    disposition = RepairMobileApiSupport.StringValue(reader, "Disposition"),
                    replacementItemId = RepairMobileApiSupport.IntValue(reader, "ReplacementItemId"),
                    replacementRequestId = RepairMobileApiSupport.IntValue(reader, "ReplacementRequestId"),
                    replacementSetId = RepairMobileApiSupport.IntValue(reader, "ReplacementSetId")
                });
            }
        }
    }

    private static void AssignSpare(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        if (!RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_AssignSpare"))
        {
            RepairMobileApiSupport.Error(context, 503, "Repair spare actions are not installed for this environment");
            return;
        }
        var itemId = RepairMobileApiSupport.Int(request, "spareItemId");
        if (!itemId.HasValue || itemId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "spareItemId is required");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_AssignSpare", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@SpareItemId", SqlDbType.Int).Value = itemId.Value;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    RepairMobileApiSupport.Error(context, 503, "Spare assignment did not return a result");
                    return;
                }
                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    message = "Spare assigned",
                    borrowId = RepairMobileApiSupport.IntValue(reader, "BorrowId"),
                    itemId = RepairMobileApiSupport.IntValue(reader, "ItemId"),
                    serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber")
                });
            }
        }
    }

    private static void ReturnSpare(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor)
    {
        if (!RepairMobileApiSupport.HasProcedure(connection, "dbo.sp_RepairPortal_ReturnSpare"))
        {
            RepairMobileApiSupport.Error(context, 503, "Repair spare actions are not installed for this environment");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_ReturnSpare", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    RepairMobileApiSupport.Error(context, 503, "Spare return did not return a result");
                    return;
                }
                var returned = reader["Returned"] != DBNull.Value && Convert.ToBoolean(reader["Returned"]);
                RepairMobileApiSupport.Ok(context, new { success = true, message = returned ? "Spare returned" : "No active spare was assigned" });
            }
        }
    }

    private static void CreatePart(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var label = RepairMobileApiSupport.Text(request, "label", 200);
        var problem = RepairMobileApiSupport.Text(request, "problemDescription", 2000);
        var severity = RepairMobileApiSupport.Text(request, "severity", 20) ?? "Medium";
        using (var command = new SqlCommand("dbo.sp_RepairPortal_CreatePart", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@CustomLabel", SqlDbType.NVarChar, 200).Value = RepairMobileApiSupport.DbValue(label);
            command.Parameters.Add("@ProblemDescription", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(problem);
            command.Parameters.Add("@Severity", SqlDbType.NVarChar, 20).Value = severity;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    RepairMobileApiSupport.Error(context, 503, "Part creation did not return a part");
                    return;
                }
                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    message = "Part added",
                    part = new
                    {
                        repairPartId = Convert.ToInt32(reader["RepairPartId"]),
                        partNumber = Convert.ToInt32(reader["PartNumber"]),
                        displayName = RepairMobileApiSupport.StringValue(reader, "PartDisplayName"),
                        status = RepairMobileApiSupport.StringValue(reader, "Status"),
                        severity = RepairMobileApiSupport.StringValue(reader, "Severity")
                    }
                });
            }
        }
    }

    private static void SetPartStatus(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var partId = RepairMobileApiSupport.Int(request, "repairPartId");
        var status = RepairMobileApiSupport.Text(request, "status", 20);
        var note = RepairMobileApiSupport.Text(request, "note", 2000);
        if (!partId.HasValue || partId.Value <= 0 || string.IsNullOrWhiteSpace(status))
        {
            RepairMobileApiSupport.Error(context, 400, "repairPartId and status are required");
            return;
        }
        EnsurePartBelongsToTicket(connection, partId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_SetPartStatus", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairPartId", SqlDbType.Int).Value = partId.Value;
            command.Parameters.Add("@NewStatus", SqlDbType.NVarChar, 20).Value = status;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@Note", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(note);
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Part status updated" });
    }

    private static void AddPartNote(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var partId = RepairMobileApiSupport.Int(request, "repairPartId");
        var text = RepairMobileApiSupport.Text(request, "noteText", 4000);
        var type = RepairMobileApiSupport.Text(request, "noteType", 30) ?? "Diagnostic";
        if (!partId.HasValue || partId.Value <= 0 || string.IsNullOrWhiteSpace(text))
        {
            RepairMobileApiSupport.Error(context, 400, "repairPartId and noteText are required");
            return;
        }
        EnsurePartBelongsToTicket(connection, partId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_AddPartNote", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairPartId", SqlDbType.Int).Value = partId.Value;
            command.Parameters.Add("@NoteType", SqlDbType.NVarChar, 30).Value = type;
            command.Parameters.Add("@NoteText", SqlDbType.NVarChar, 4000).Value = text;
            command.Parameters.Add("@CreatedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@CreatedByEmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Part note added" });
    }

    private static void UpdatePartLabel(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var partId = RepairMobileApiSupport.Int(request, "repairPartId");
        var label = RepairMobileApiSupport.Text(request, "label", 200);
        if (!partId.HasValue || partId.Value <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "repairPartId is required");
            return;
        }
        EnsurePartBelongsToTicket(connection, partId.Value, ticketId);
        using (var command = new SqlCommand("dbo.sp_RepairPortal_UpdatePartLabel", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairPartId", SqlDbType.Int).Value = partId.Value;
            command.Parameters.Add("@NewCustomLabel", SqlDbType.NVarChar, 200).Value = RepairMobileApiSupport.DbValue(label);
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Part label updated" });
    }

    private static void SaveConclusion(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var rootCause = RepairMobileApiSupport.Text(request, "rootCause", 2000);
        var workPerformed = RepairMobileApiSupport.Text(request, "workPerformed", 2000);
        var finalOutcome = RepairMobileApiSupport.Text(request, "finalOutcome", 2000);
        var recommendations = RepairMobileApiSupport.Text(request, "recommendations", 2000);
        var vendorId = RepairMobileApiSupport.Int(request, "handedOverToVendorId");
        using (var command = new SqlCommand("dbo.sp_RepairPortal_SaveConclusion", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@RootCause", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(rootCause);
            command.Parameters.Add("@WorkPerformed", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(workPerformed);
            command.Parameters.Add("@FinalOutcome", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(finalOutcome);
            command.Parameters.Add("@Recommendations", SqlDbType.NVarChar, 2000).Value = RepairMobileApiSupport.DbValue(recommendations);
            command.Parameters.Add("@CompletedByUserId", SqlDbType.Int).Value = actor.UserId;
            command.Parameters.Add("@CompletedByEmpId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
            if (ProcedureHasParameter(connection, "dbo.sp_RepairPortal_SaveConclusion", "@HandedOverToVendorId"))
                command.Parameters.Add("@HandedOverToVendorId", SqlDbType.Int).Value = vendorId.HasValue ? (object)vendorId.Value : DBNull.Value;
            command.ExecuteNonQuery();
        }

        if (RepairMobileApiSupport.HasTable(connection, "dbo.RepairConclusionRepairedByEmp") && request["repairedByEmployeeIds"] is JArray)
        {
            var employees = new List<int>();
            foreach (var token in (JArray)request["repairedByEmployeeIds"])
            {
                int employeeId;
                if (int.TryParse(token.ToString(), out employeeId) && employeeId > 0 && !employees.Contains(employeeId)) employees.Add(employeeId);
            }
            using (var transaction = connection.BeginTransaction())
            {
                using (var delete = new SqlCommand("DELETE FROM dbo.RepairConclusionRepairedByEmp WHERE RepairTicketId = @TicketId;", connection, transaction))
                {
                    delete.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                    delete.ExecuteNonQuery();
                }
                foreach (var employeeId in employees)
                {
                    using (var insert = new SqlCommand("INSERT dbo.RepairConclusionRepairedByEmp (RepairTicketId, EmpId) VALUES (@TicketId, @EmployeeId);", connection, transaction))
                    {
                        insert.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
                        insert.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = employeeId;
                        insert.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Conclusion saved" });
    }

    private static void UpdateRequestedBy(HttpContext context, SqlConnection connection, int ticketId, CallTicketApiUser actor, JObject request)
    {
        var type = RepairMobileApiSupport.Text(request, "requestedByType", 20);
        var departmentId = RepairMobileApiSupport.Int(request, "requestedByDepartmentId");
        var employeeId = RepairMobileApiSupport.Int(request, "requestedByEmployeeId");
        var companyId = RepairMobileApiSupport.Int(request, "requestedByCompanyId");
        var branchId = RepairMobileApiSupport.Int(request, "requestedByBranchId");
        if (string.IsNullOrWhiteSpace(type))
        {
            RepairMobileApiSupport.Error(context, 400, "requestedByType is required");
            return;
        }
        using (var command = new SqlCommand("dbo.sp_RepairPortal_UpdateRequestedBy", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@RepairTicketId", SqlDbType.Int).Value = ticketId;
            command.Parameters.Add("@RequestedByType", SqlDbType.NVarChar, 20).Value = type;
            command.Parameters.Add("@RequestedByDeptId", SqlDbType.Int).Value = departmentId.HasValue ? (object)departmentId.Value : DBNull.Value;
            command.Parameters.Add("@RequestedByEmpId", SqlDbType.Int).Value = employeeId.HasValue ? (object)employeeId.Value : DBNull.Value;
            command.Parameters.Add("@ChangedByUserId", SqlDbType.Int).Value = actor.UserId;
            if (ProcedureHasParameter(connection, "dbo.sp_RepairPortal_UpdateRequestedBy", "@RequestedByComId"))
                command.Parameters.Add("@RequestedByComId", SqlDbType.Int).Value = companyId.HasValue ? (object)companyId.Value : DBNull.Value;
            if (ProcedureHasParameter(connection, "dbo.sp_RepairPortal_UpdateRequestedBy", "@RequestedByBranchId"))
                command.Parameters.Add("@RequestedByBranchId", SqlDbType.Int).Value = branchId.HasValue ? (object)branchId.Value : DBNull.Value;
            command.ExecuteNonQuery();
        }
        RepairMobileApiSupport.Ok(context, new { success = true, message = "Requester updated" });
    }

    private static void HandleAttendance(HttpContext context, CallTicketApiUser actor, string action)
    {
        if (!actor.IsItAuthorized || !actor.EmployeeId.HasValue)
        {
            RepairMobileApiSupport.Error(context, 403, "An IT employee account is required for repair attendance");
            return;
        }
        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                var procedure = action == "timein" ? "dbo.sp_RepairPortal_TimeIn" : "dbo.sp_RepairPortal_TimeOut";
                using (var command = new SqlCommand(procedure, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.Value;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            RepairMobileApiSupport.Error(context, 503, "Attendance did not return a result");
                            return;
                        }
                        RepairMobileApiSupport.Ok(context, new
                        {
                            success = true,
                            message = action == "timein" ? "Time in recorded" : "Time out recorded",
                            attendance = new
                            {
                                attendanceId = RepairMobileApiSupport.IntValue(reader, "AttendanceId"),
                                workDate = RepairMobileApiSupport.IsoDate(reader["WorkDate"]),
                                timeIn = RepairMobileApiSupport.IsoUtc(reader["TimeIn"]),
                                timeOut = RepairMobileApiSupport.IsoUtc(reader["TimeOut"])
                            }
                        });
                    }
                }
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Attendance could not be updated");
        }
    }

    private static void EnsureObservationBelongsToTicket(SqlConnection connection, int observationId, int ticketId)
    {
        using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.RepairItemObservation WHERE ObservationId = @ObservationId AND RepairTicketId = @TicketId;", connection))
        {
            command.Parameters.Add("@ObservationId", SqlDbType.Int).Value = observationId;
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            if (Convert.ToInt32(command.ExecuteScalar()) == 0) throw new InvalidOperationException("Observation not found");
        }
    }

    private static void EnsureTicketAttachmentBelongsToTicket(SqlConnection connection, int attachmentId, int ticketId)
    {
        using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.RepairTicketAttachment WHERE AttachmentId = @AttachmentId AND RepairTicketId = @TicketId;", connection))
        {
            command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = attachmentId;
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            if (Convert.ToInt32(command.ExecuteScalar()) == 0) throw new InvalidOperationException("Attachment not found");
        }
    }

    private static void EnsurePartAttachmentBelongsToTicket(SqlConnection connection, int attachmentId, int ticketId)
    {
        using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.RepairPartAttachment WHERE PartAttachmentId = @AttachmentId AND RepairTicketId = @TicketId;", connection))
        {
            command.Parameters.Add("@AttachmentId", SqlDbType.Int).Value = attachmentId;
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            if (Convert.ToInt32(command.ExecuteScalar()) == 0) throw new InvalidOperationException("Attachment not found");
        }
    }

    private static void EnsurePartBelongsToTicket(SqlConnection connection, int partId, int ticketId)
    {
        using (var command = new SqlCommand("SELECT COUNT(1) FROM dbo.RepairPart WHERE RepairPartId = @PartId AND RepairTicketId = @TicketId;", connection))
        {
            command.Parameters.Add("@PartId", SqlDbType.Int).Value = partId;
            command.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticketId;
            if (Convert.ToInt32(command.ExecuteScalar()) == 0) throw new InvalidOperationException("Part not found");
        }
    }

    private static bool ProcedureHasParameter(SqlConnection connection, string procedureName, string parameterName)
    {
        const string sql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM sys.parameters
    WHERE object_id = OBJECT_ID(@ProcedureName) AND name = @ParameterName
) THEN 1 ELSE 0 END;";
        using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@ProcedureName", SqlDbType.NVarChar, 256).Value = procedureName;
            command.Parameters.Add("@ParameterName", SqlDbType.NVarChar, 128).Value = parameterName;
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
    }

    public bool IsReusable { get { return false; } }
}
