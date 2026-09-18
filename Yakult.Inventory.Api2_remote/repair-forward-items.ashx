<%@ WebHandler Language="C#" Class="RepairForwardItemsHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>IT CALL → Repair Portal physical-item selector. This endpoint validates the source
/// IT CALL and returns only active hardware candidates; final creation independently verifies the
/// selected item before linking it.</summary>
public sealed class RepairForwardItemsHandler : IHttpHandler
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
        if (!RepairMobileApiSupport.TryRequireTechnician(context, out actor)) return;
        int callTicketId;
        if (!int.TryParse(context.Request.QueryString["ticketId"], out callTicketId) || callTicketId <= 0)
        {
            RepairMobileApiSupport.Error(context, 400, "ticketId is required");
            return;
        }
        var query = (context.Request.QueryString["query"] ?? string.Empty).Trim();
        if (query.Length > 200)
        {
            RepairMobileApiSupport.Error(context, 400, "query is too long");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                using (var exists = new SqlCommand("SELECT COUNT(1) FROM dbo.CallTicket WHERE TicketId=@TicketId;", connection))
                {
                    exists.Parameters.Add("@TicketId", SqlDbType.Int).Value = callTicketId;
                    if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
                    {
                        RepairMobileApiSupport.Error(context, 404, "IT Call ticket not found");
                        return;
                    }
                }

                object existingLink = null;
                if (RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId"))
                {
                    using (var link = new SqlCommand("SELECT TOP (1) RepairTicketId, TicketCode, ItemId FROM dbo.RepairTicket WHERE CallTicketId=@TicketId;", connection))
                    {
                        link.Parameters.Add("@TicketId", SqlDbType.Int).Value = callTicketId;
                        using (var reader = link.ExecuteReader())
                            if (reader.Read()) existingLink = new
                            {
                                repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                                ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                                itemId = Convert.ToInt32(reader["ItemId"])
                            };
                    }
                }

                const string sql = @"
SELECT TOP (50) ItemId, Name, ModelNumber, SerialNumber, Category, ConditionID, StockOnHand
FROM dbo.Item
WHERE ISNULL(Active,1)=1
  AND (ISNULL(ItemType,'Hardware')='Hardware' OR ItemType IS NULL)
  AND (@Query='' OR Name LIKE @LikeQuery OR ModelNumber LIKE @LikeQuery OR SerialNumber LIKE @LikeQuery)
ORDER BY Name, ModelNumber, SerialNumber;";
                var items = new List<object>();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.Add("@Query", SqlDbType.NVarChar, 200).Value = query;
                    command.Parameters.Add("@LikeQuery", SqlDbType.NVarChar, 450).Value = "%" + query + "%";
                    using (var reader = command.ExecuteReader())
                        while (reader.Read()) items.Add(new
                        {
                            itemId = Convert.ToInt32(reader["ItemId"]),
                            displayText = BuildDisplay(reader),
                            name = RepairMobileApiSupport.StringValue(reader, "Name"),
                            modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                            serialNumber = RepairMobileApiSupport.StringValue(reader, "SerialNumber"),
                            category = RepairMobileApiSupport.StringValue(reader, "Category"),
                            conditionId = RepairMobileApiSupport.IntValue(reader, "ConditionID"),
                            stockOnHand = RepairMobileApiSupport.IntValue(reader, "StockOnHand")
                        });
                }
                RepairMobileApiSupport.Ok(context, new { success = true, items = items, existingRepairTicket = existingLink });
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair forwarding items are temporarily unavailable");
        }
    }

    private static string BuildDisplay(IDataRecord reader)
    {
        var name = RepairMobileApiSupport.StringValue(reader, "Name") ?? "Unnamed item";
        var model = RepairMobileApiSupport.StringValue(reader, "ModelNumber");
        var serial = RepairMobileApiSupport.StringValue(reader, "SerialNumber");
        var result = name;
        if (!string.IsNullOrWhiteSpace(model)) result += " • " + model;
        if (!string.IsNullOrWhiteSpace(serial)) result += " • S/N " + serial;
        return result;
    }

    public bool IsReusable { get { return false; } }
}
