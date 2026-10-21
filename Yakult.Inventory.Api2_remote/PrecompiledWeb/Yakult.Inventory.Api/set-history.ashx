<%@ WebHandler Language="C#" Class="SetHistoryHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

public class SetHistoryHandler : IHttpHandler
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
            var history = GetDeploymentHistoryAsync(parsedToken).Result;
            ReturnJson(context, new { success = true, deployment_history = history });
        }
        catch (Exception ex)
        {
            ReturnError(context, 500, "Failed to load deployment history.", "history_load_failed");
        }
    }

    private async Task<List<DeploymentHistoryDto>> GetDeploymentHistoryAsync(Guid token)
    {
        const string sql = @"
SELECT TOP 50
    su.CreatedAt AS DeployedAt,
    su.UpdatedByName AS DeployedBy,
    su.PreviousStatus,
    su.NewStatus,
    ISNULL(su.Source, 'Scanner') AS DeploymentLocation,
    su.DeviceId,
    s.SetCode
FROM dbo.[Set] s
INNER JOIN dbo.SetItemUpdate su ON su.SetId = s.SetId
WHERE s.QRToken = @Token
    AND su.NewStatus IN ('Deployed', 'Dispatched', 'Delivered', 'Confirmed')
ORDER BY su.CreatedAt DESC;";

        var list = new List<DeploymentHistoryDto>();

        using (var con = new SqlConnection(ConnectionString))
        using (var cmd = new SqlCommand(sql, con))
        {
            cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = token;
            await con.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new DeploymentHistoryDto
                    {
                        DeployedAt = reader.IsDBNull(reader.GetOrdinal("DeployedAt"))
                            ? null
                            : reader.GetDateTime(reader.GetOrdinal("DeployedAt")).ToString("yyyy-MM-dd HH:mm"),
                        DeployedBy = reader.IsDBNull(reader.GetOrdinal("DeployedBy")) ? "Unknown" : reader.GetString(reader.GetOrdinal("DeployedBy")),
                        PreviousStatus = reader.IsDBNull(reader.GetOrdinal("PreviousStatus")) ? "Pending" : reader.GetString(reader.GetOrdinal("PreviousStatus")),
                        NewStatus = reader.IsDBNull(reader.GetOrdinal("NewStatus")) ? "Deployed" : reader.GetString(reader.GetOrdinal("NewStatus")),
                        DeploymentLocation = reader.IsDBNull(reader.GetOrdinal("DeploymentLocation")) ? "Scanner" : reader.GetString(reader.GetOrdinal("DeploymentLocation")),
                        DeviceId = reader.IsDBNull(reader.GetOrdinal("DeviceId")) ? null : reader.GetString(reader.GetOrdinal("DeviceId"))
                    });
                }
            }
        }
        return list;
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

    private class DeploymentHistoryDto
    {
        [JsonProperty("deployed_at")] public string DeployedAt { get; set; }
        [JsonProperty("deployed_by")] public string DeployedBy { get; set; }
        [JsonProperty("previous_status")] public string PreviousStatus { get; set; }
        [JsonProperty("new_status")] public string NewStatus { get; set; }
        [JsonProperty("deployment_location")] public string DeploymentLocation { get; set; }
        [JsonProperty("device_id")] public string DeviceId { get; set; }
    }
}
