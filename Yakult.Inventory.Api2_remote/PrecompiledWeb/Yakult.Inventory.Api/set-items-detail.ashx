<%@ WebHandler Language="C#" Class="SetItemsDetailHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

public class SetItemsDetailHandler : IHttpHandler
{
    private string ConnectionString => ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]?.ConnectionString;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ReturnError(context, 503, "Scanner service is not configured right now. Please contact IT.", "service_unavailable");
            return;
        }

        var token = context.Request.QueryString["token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            ReturnError(context, 400, "Token is required.", "missing_token");
            return;
        }

        Guid parsedToken;
        if (!TryParseScannerToken(token, out parsedToken, out var message, out var errorCode))
        {
            ReturnError(context, 400, message, errorCode);
            return;
        }

        try
        {
            var result = GetDetailedItemsAsync(parsedToken).Result;
            ReturnJson(context, new { success = true, set_code = result.SetCode, metadata = result.Metadata, items = result.Items });
        }
        catch (Exception ex)
        {
            ReturnError(context, 500, "Failed to load item details.", "items_load_failed");
        }
    }

    private async Task<DetailResult> GetDetailedItemsAsync(Guid token)
    {
        const string sql = @"
SELECT
    s.SetCode,
    s.SetId,
    s.Status,
    s.SetType,
    s.DispatchDate,
    s.CreatedAt,
    i.ItemType,
    i.Name AS ItemName,
    i.Category,
    r.Quantity,
    s.ComputerName,
    s.IPAddress,
    i.ModelNumber,
    i.SerialNumber,
    r.Description,
    r.Status AS ItemRequestStatus,
    ISNULL(c.ConditionName, 'Unknown') AS ItemCondition,
    ISNULL((SELECT COUNT(*) FROM dbo.ItemRepairHistory h WHERE h.SerialNumber = i.SerialNumber), 0) AS RepairCount,
    (SELECT TOP 1 h.RepairAction FROM dbo.ItemRepairHistory h WHERE h.SerialNumber = i.SerialNumber ORDER BY h.CreatedAt DESC) AS LastRepairAction
FROM dbo.[Set] s
INNER JOIN dbo.Request r ON r.SetId = s.SetId
INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
LEFT JOIN dbo.[Condition] c ON c.ConditionId = i.ConditionId
WHERE s.QRToken = @Token
ORDER BY r.ReqId ASC;";

        var items = new List<ItemDetailDto>();
        string setCode = null;
        int setId = 0;
        string status = null;
        DateTime? dispatchDate = null;

        using (var con = new SqlConnection(ConnectionString))
        using (var cmd = new SqlCommand(sql, con))
        {
            cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = token;
            await con.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                int id = 1;
                while (await reader.ReadAsync())
                {
                    if (setCode == null)
                    {
                        setCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? "Unknown" : reader.GetString(reader.GetOrdinal("SetCode"));
                        setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                        status = reader.IsDBNull(reader.GetOrdinal("Status")) ? reader.GetString(reader.GetOrdinal("SetType")) : reader.GetString(reader.GetOrdinal("Status"));
                        dispatchDate = reader.IsDBNull(reader.GetOrdinal("DispatchDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DispatchDate"));
                    }
                    items.Add(new ItemDetailDto
                    {
                        ItemId = id++,
                        ItemType = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? null : reader.GetString(reader.GetOrdinal("ItemName")),
                        ItemCategory = reader.IsDBNull(reader.GetOrdinal("Category")) ? "N/A" : reader.GetString(reader.GetOrdinal("Category")),
                        Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("Quantity")),
                        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "N/A" : reader.GetString(reader.GetOrdinal("Description")),
                        ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? "N/A" : reader.GetString(reader.GetOrdinal("ModelNumber")),
                        SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? "N/A" : reader.GetString(reader.GetOrdinal("SerialNumber")),
                        ItemStatus = reader.IsDBNull(reader.GetOrdinal("ItemRequestStatus")) ? "N/A" : reader.GetString(reader.GetOrdinal("ItemRequestStatus")),
                        ComputerName = reader.IsDBNull(reader.GetOrdinal("ComputerName")) ? null : reader.GetString(reader.GetOrdinal("ComputerName")),
                        IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress")) ? null : reader.GetString(reader.GetOrdinal("IPAddress")),
                        ItemCondition = reader.IsDBNull(reader.GetOrdinal("ItemCondition")) ? "Unknown" : reader.GetString(reader.GetOrdinal("ItemCondition")),
                        RepairCount = reader.IsDBNull(reader.GetOrdinal("RepairCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("RepairCount")),
                        LastRepairAction = reader.IsDBNull(reader.GetOrdinal("LastRepairAction")) ? null : reader.GetString(reader.GetOrdinal("LastRepairAction"))
                    });
                }
            }
        }

        return new DetailResult
        {
            SetCode = setCode,
            Items = items,
            Metadata = new SetMetadataDto
            {
                ItemCount = items.Count,
                Priority = GetPriorityFromStatus(status),
                ExpectedDeliveryUtc = dispatchDate?.ToString("yyyy-MM-dd")
            }
        };
    }

    private string GetPriorityFromStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Normal";
        var s = status.ToLowerInvariant();
        if (s.Contains("urgent") || s.Contains("rush")) return "Urgent";
        if (s.Contains("high")) return "High";
        return "Normal";
    }

    private static bool TryParseScannerToken(string rawValue, out Guid token, out string message, out string errorCode)
    {
        token = Guid.Empty;
        message = null;
        errorCode = null;
        var value = (rawValue ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            message = "No QR code data was received. Please scan again.";
            errorCode = "empty_qr_data";
            return false;
        }
        const string prefix = "yakult:set:v1:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value.Substring(prefix.Length).Trim();
        if (Guid.TryParse(value, out token))
            return true;
        message = "This QR code is not a valid Yakult dispatch QR code.";
        errorCode = "invalid_qr_format";
        return false;
    }

    private void ReturnJson(HttpContext context, object data)
    {
        context.Response.Write(JsonConvert.SerializeObject(data));
    }

    private void ReturnError(HttpContext context, int statusCode, string message, string errorCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Write(JsonConvert.SerializeObject(new { success = false, message, error = errorCode }));
    }

    public bool IsReusable => false;

    private class DetailResult
    {
        public string SetCode { get; set; }
        public SetMetadataDto Metadata { get; set; }
        public List<ItemDetailDto> Items { get; set; }
    }

    private class SetMetadataDto
    {
        [JsonProperty("item_count")] public int ItemCount { get; set; }
        [JsonProperty("priority")] public string Priority { get; set; }
        [JsonProperty("delivery_notes")] public string DeliveryNotes { get; set; }
        [JsonProperty("expected_delivery_utc")] public string ExpectedDeliveryUtc { get; set; }
    }

    private class ItemDetailDto
    {
        [JsonProperty("item_id")] public int ItemId { get; set; }
        [JsonProperty("item_type")] public string ItemType { get; set; }
        [JsonProperty("item_category")] public string ItemCategory { get; set; }
        [JsonProperty("quantity")] public int Quantity { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("model_number")] public string ModelNumber { get; set; }
        [JsonProperty("serial_number")] public string SerialNumber { get; set; }
        [JsonProperty("item_status")] public string ItemStatus { get; set; }
        [JsonProperty("computer_name")] public string ComputerName { get; set; }
        [JsonProperty("ip_address")] public string IPAddress { get; set; }
        [JsonProperty("item_condition")] public string ItemCondition { get; set; }
        [JsonProperty("repair_count")] public int RepairCount { get; set; }
        [JsonProperty("last_repair_action")] public string LastRepairAction { get; set; }
    }
}
