<%@ WebHandler Language="C#" Class="SetConfirmHandler" %>

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

public class SetConfirmHandler : IHttpHandler
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

        if (context.Request.HttpMethod != "POST")
        {
            ReturnError(context, 405, "Only POST method is allowed.", "method_not_allowed");
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

        // Parse request body
        ConfirmationRequest request;
        try
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                var json = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<ConfirmationRequest>(json);
            }
        }
        catch
        {
            ReturnError(context, 400, "Invalid request body.", "invalid_request");
            return;
        }

        if (request == null)
        {
            ReturnError(context, 400, "Confirmation details are required.", "missing_request");
            return;
        }

        try
        {
            var result = ConfirmDeploymentAsync(parsedToken, request).Result;
            if (!result.Success)
            {
                ReturnError(context, 404, result.Message, "set_not_found");
                return;
            }
            ReturnJson(context, new { success = true, message = result.Message, confirmed_at = result.ConfirmedAt, confirmation_id = result.ConfirmationId });
        }
        catch (Exception ex)
        {
            ReturnError(context, 500, "Failed to confirm deployment.", "confirmation_failed");
        }
    }

    private async Task<ConfirmationResult> ConfirmDeploymentAsync(Guid token, ConfirmationRequest request)
    {
        const string getSetSql = "SELECT TOP 1 SetId, SetCode, Status FROM dbo.[Set] WHERE QRToken = @Token;";
        const string insertHistorySql = @"
INSERT INTO dbo.SetDeploymentConfirmation (SetId, ConfirmedBy, ConfirmedAt, SignatureBase64, PhotoBase64, GpsCoordinates, DeviceId, Notes)
VALUES (@SetId, @ConfirmedBy, @ConfirmedAt, @SignatureBase64, @PhotoBase64, @GpsCoordinates, @DeviceId, @Notes);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        const string updateStatusSql = "UPDATE dbo.[Set] SET Status = @Status, UpdatedAt = @UpdatedAt WHERE SetId = @SetId;";
        const string insertUpdateSql = @"
INSERT INTO dbo.SetItemUpdate (SetId, SetCode, NewStatus, PreviousStatus, UpdatedByUserId, UpdatedByName, Source, DeviceId, CreatedAt)
VALUES (@SetId, @SetCode, @NewStatus, @PreviousStatus, @UpdatedByUserId, @UpdatedByName, @Source, @DeviceId, @CreatedAt);";

        int setId;
        string setCode;
        string previousStatus;

        using (var con = new SqlConnection(ConnectionString))
        {
            await con.OpenAsync();
            using (var cmd = new SqlCommand(getSetSql, con))
            {
                cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = token;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return new ConfirmationResult { Success = false, Message = "Set not found." };
                    }
                    setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                    setCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                    previousStatus = reader.IsDBNull(reader.GetOrdinal("Status")) ? "Pending" : reader.GetString(reader.GetOrdinal("Status"));
                }
            }

            var confirmedAt = DateTime.UtcNow;
            int confirmationId;

            // Record confirmation
            using (var cmd = new SqlCommand(insertHistorySql, con))
            {
                cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                cmd.Parameters.Add("@ConfirmedBy", SqlDbType.NVarChar, 200).Value = (object)request.ConfirmedBy ?? DBNull.Value;
                cmd.Parameters.Add("@ConfirmedAt", SqlDbType.DateTime2).Value = confirmedAt;
                cmd.Parameters.Add("@SignatureBase64", SqlDbType.NVarChar, -1).Value = (object)request.SignatureBase64 ?? DBNull.Value;
                cmd.Parameters.Add("@PhotoBase64", SqlDbType.NVarChar, -1).Value = (object)request.PhotoBase64 ?? DBNull.Value;
                cmd.Parameters.Add("@GpsCoordinates", SqlDbType.NVarChar, 100).Value = (object)request.GpsCoordinates ?? DBNull.Value;
                cmd.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 200).Value = (object)request.DeviceId ?? DBNull.Value;
                cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = (object)request.Notes ?? DBNull.Value;
                confirmationId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Update set status
            using (var cmd = new SqlCommand(updateStatusSql, con))
            {
                cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = "Deployed";
                cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = confirmedAt;
                await cmd.ExecuteNonQueryAsync();
            }

            // Record in SetItemUpdate for history trail
            using (var cmd = new SqlCommand(insertUpdateSql, con))
            {
                cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                cmd.Parameters.Add("@SetCode", SqlDbType.NVarChar, 50).Value = (object)setCode ?? DBNull.Value;
                cmd.Parameters.Add("@NewStatus", SqlDbType.NVarChar, 50).Value = "Deployed";
                cmd.Parameters.Add("@PreviousStatus", SqlDbType.NVarChar, 50).Value = previousStatus;
                cmd.Parameters.Add("@UpdatedByUserId", SqlDbType.Int).Value = 0; // Scanner app doesn't have userId in token
                cmd.Parameters.Add("@UpdatedByName", SqlDbType.NVarChar, 200).Value = (object)request.ConfirmedBy ?? "Scanner App";
                cmd.Parameters.Add("@Source", SqlDbType.NVarChar, 100).Value = "Scanner Confirmation";
                cmd.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 200).Value = (object)request.DeviceId ?? DBNull.Value;
                cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = confirmedAt;
                await cmd.ExecuteNonQueryAsync();
            }

            return new ConfirmationResult
            {
                Success = true,
                Message = "Set deployment confirmed successfully.",
                ConfirmedAt = confirmedAt.ToString("o"),
                ConfirmationId = confirmationId.ToString()
            };
        }
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

    private class ConfirmationRequest
    {
        [JsonProperty("signature_base64")] public string SignatureBase64 { get; set; }
        [JsonProperty("photo_base64")] public string PhotoBase64 { get; set; }
        [JsonProperty("gps_coordinates")] public string GpsCoordinates { get; set; }
        [JsonProperty("device_id")] public string DeviceId { get; set; }
        [JsonProperty("confirmed_by")] public string ConfirmedBy { get; set; }
        [JsonProperty("notes")] public string Notes { get; set; }
    }

    private class ConfirmationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ConfirmedAt { get; set; }
        public string ConfirmationId { get; set; }
    }
}
