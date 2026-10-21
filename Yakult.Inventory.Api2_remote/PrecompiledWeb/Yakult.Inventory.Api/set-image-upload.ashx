<%@ WebHandler Language="C#" Class="SetImageUploadHandler" %>

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

public class SetImageUploadHandler : IHttpHandler
{
    private string ConnectionString
    {
        get
        {
            var settings = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"];
            return settings != null ? settings.ConnectionString : null;
        }
    }

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            ReturnError(context, 503, "Image upload service is not configured. Please contact IT.", "service_unavailable");
            return;
        }

        if (context.Request.HttpMethod != "POST")
        {
            ReturnError(context, 405, "Only POST method is allowed.", "method_not_allowed");
            return;
        }

        var token = context.Request.QueryString["token"];
        var setCode = context.Request.QueryString["set_code"];

        // Retrofit may send the literal string "null" for absent nullable params
        if (token == "null") token = null;
        if (setCode == "null") setCode = null;

        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(setCode))
        {
            ReturnError(context, 400, "Token or set_code is required.", "missing_identifier");
            return;
        }

        // Parse JSON body first
        UploadRequest request;
        try
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                var json = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<UploadRequest>(json);
            }
        }
        catch
        {
            ReturnError(context, 400, "Invalid request body.", "invalid_request");
            return;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            ReturnError(context, 400, "Image data is required.", "missing_image");
            return;
        }

        // Validate token (only if provided)
        Guid? parsedToken = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            Guid tokenGuid;
            if (!TryParseScannerToken(token, out tokenGuid, out var message, out var errorCode))
            {
                ReturnError(context, 400, message, errorCode);
                return;
            }
            parsedToken = tokenGuid;
        }

        try
        {
            var result = UploadImageAsync(parsedToken, setCode, request).Result;
            if (!result.Success)
            {
                ReturnError(context, 404, result.Message, "upload_failed");
                return;
            }

            ReturnJson(context, new {
                success = true,
                message = result.Message,
                image_id = result.ImageId,
                uploaded_at = result.UploadedAt
            });
        }
        catch (Exception ex)
        {
            ReturnError(context, 500, "Failed to upload image.", "upload_failed");
        }
    }

    private async Task<UploadResult> UploadImageAsync(Guid? token, string setCode, UploadRequest request)
    {
        const string getSetByTokenSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE QRToken = @Token;";
        const string getSetByCodeSql  = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE SetCode = @SetCode;";
        const string insertImageSql = @"
INSERT INTO dbo.SetImages (SetId, ImagePath, ImageType, UploadedBy, UploadDate)
VALUES (@SetId, @ImagePath, @ImageType, @UploadedBy, @UploadDate);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        int setId;
        string resolvedSetCode;

        using (var con = new SqlConnection(ConnectionString))
        {
            await con.OpenAsync();

            // Look up Set — prefer token, fall back to setCode
            if (token.HasValue)
            {
                using (var cmd = new SqlCommand(getSetByTokenSql, con))
                {
                    cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = token.Value;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return new UploadResult { Success = false, Message = "Set not found for the provided QR token." };
                        setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                        resolvedSetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                    }
                }
            }
            else
            {
                using (var cmd = new SqlCommand(getSetByCodeSql, con))
                {
                    cmd.Parameters.Add("@SetCode", SqlDbType.VarChar, 50).Value = setCode;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return new UploadResult { Success = false, Message = "Set not found for the provided set code." };
                        setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                        resolvedSetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                    }
                }
            }

            // Decode base64 image
            byte[] imageBytes;
            try
            {
                // Handle data URI scheme (data:image/jpeg;base64,...)
                var base64Data = request.ImageBase64;
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch
            {
                return new UploadResult { Success = false, Message = "Invalid image data." };
            }

            // Create file path - save to App_Data/SetImages
            // Use HttpContext.Current since we're in an async method that may lose the original context parameter
            var serverPath = HttpContext.Current?.Server.MapPath("~") ?? AppDomain.CurrentDomain.BaseDirectory;
            var uploadFolder = Path.Combine(serverPath, "App_Data", "SetImages");
            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var fileName = string.Format("{0}_{1:yyyyMMdd_HHmmss}_{2}.jpg", resolvedSetCode, DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            var filePath = Path.Combine(uploadFolder, fileName);
            
            // Save file
            File.WriteAllBytes(filePath, imageBytes);

            // Store relative path in database
            var relativePath = string.Format("~/App_Data/SetImages/{0}", fileName);
            var uploadDate = DateTime.UtcNow;

            // Insert record
            int imageId;
            using (var cmd = new SqlCommand(insertImageSql, con))
            {
                cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                cmd.Parameters.Add("@ImagePath", SqlDbType.VarChar, 500).Value = relativePath;
                cmd.Parameters.Add("@ImageType", SqlDbType.VarChar, 50).Value = (object)request.ImageType ?? "MobileUpload";
                cmd.Parameters.Add("@UploadedBy", SqlDbType.VarChar, 100).Value = (object)request.UploadedBy ?? "Mobile App";
                cmd.Parameters.Add("@UploadDate", SqlDbType.DateTime).Value = uploadDate;
                imageId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            return new UploadResult
            {
                Success = true,
                Message = "Image uploaded successfully.",
                ImageId = imageId.ToString(),
                UploadedAt = uploadDate.ToString("o")
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

    private HttpContext context;
    public void Init(HttpContext context) { this.context = context; }

    private class UploadRequest
    {
        [JsonProperty("image_base64")] public string ImageBase64 { get; set; }
        [JsonProperty("image_type")] public string ImageType { get; set; }
        [JsonProperty("uploaded_by")] public string UploadedBy { get; set; }
    }

    private class UploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ImageId { get; set; }
        public string UploadedAt { get; set; }
    }
}
