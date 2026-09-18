<%@ WebHandler Language="C#" Class="SetImageUploadHandler" %>

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
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
        var documentNumber = context.Request.QueryString["document_number"];

        // Treat "null" string as actual null (Retrofit can send this)
        if (token == "null") token = null;
        if (setCode == "null") setCode = null;
        if (documentNumber == "null") documentNumber = null;

        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(setCode) && string.IsNullOrWhiteSpace(documentNumber))
        {
            ReturnError(context, 400, "Token, set_code, or document_number is required.", "missing_identifier");
            return;
        }

        // Parse the JSON request body
        UploadRequest request;
        using (var reader = new StreamReader(context.Request.InputStream))
        {
            var json = reader.ReadToEnd();
            request = JsonConvert.DeserializeObject<UploadRequest>(json);
        }

        if (request == null || string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            ReturnError(context, 400, "Image data is required.", "missing_image");
            return;
        }

        Guid? parsedToken = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            Guid tokenGuid;
            string message;
            string errorCode;
            if (!TryParseScannerToken(token, out tokenGuid, out message, out errorCode))
            {
                ReturnError(context, 400, message, errorCode);
                return;
            }
            parsedToken = tokenGuid;
        }

        try
        {
            const string getSetByTokenSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE QRToken = @Token AND (IsInvoice = 0 OR IsInvoice IS NULL);";
            const string getSetByCodeSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE SetCode = @SetCode AND (IsInvoice = 0 OR IsInvoice IS NULL);";
            // Invoices are rows in dbo.[Set] too (IsInvoice = 1), identified by DocumentNumber
            // instead of the QR-based SetCode/token flow -- no IsInvoice filter here since this
            // path exists specifically to resolve Invoices.
            const string getSetByDocumentNumberSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE DocumentNumber = @DocumentNumber;";
            const string insertImageSql = @"
INSERT INTO dbo.SetImages (SetId, ImageData, MimeType, OriginalFileName, FileSizeBytes, ImageType, UploadedBy, UploadDate)
VALUES (@SetId, @ImageData, @MimeType, @OriginalFileName, @FileSizeBytes, @ImageType, @UploadedBy, @UploadDate);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int setId;
            string resolvedSetCode;

            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();
                
                // Get set info from token or setCode
                if (parsedToken.HasValue)
                {
                    using (var cmd = new SqlCommand(getSetByTokenSql, con))
                    {
                        cmd.Parameters.Add("@Token", SqlDbType.UniqueIdentifier).Value = parsedToken.Value;
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                ReturnError(context, 404, "Set not found.", "set_not_found");
                                return;
                            }
                            setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                            resolvedSetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(setCode))
                {
                    using (var cmd = new SqlCommand(getSetByCodeSql, con))
                    {
                        cmd.Parameters.Add("@SetCode", SqlDbType.VarChar, 50).Value = setCode;
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                ReturnError(context, 404, "Set not found.", "set_not_found");
                                return;
                            }
                            setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                            resolvedSetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                        }
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand(getSetByDocumentNumberSql, con))
                    {
                        cmd.Parameters.Add("@DocumentNumber", SqlDbType.NVarChar, 100).Value = documentNumber;
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                ReturnError(context, 404, "Invoice not found.", "invoice_not_found");
                                return;
                            }
                            setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                            resolvedSetCode = reader.IsDBNull(reader.GetOrdinal("SetCode")) ? null : reader.GetString(reader.GetOrdinal("SetCode"));
                        }
                    }
                }

                // Decode base64 image
                byte[] imageBytes;
                try
                {
                    var base64Data = request.ImageBase64;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }
                    imageBytes = Convert.FromBase64String(base64Data);
                }
                catch
                {
                    ReturnError(context, 400, "Invalid image data.", "invalid_image");
                    return;
                }

                var mimeType = string.IsNullOrWhiteSpace(request.MimeType) ? "image/jpeg" : request.MimeType.Trim();
                var extension = mimeType == "application/pdf" ? "pdf" : mimeType == "image/png" ? "png" : "jpg";
                var fileName = string.Format("{0}_{1:yyyyMMdd_HHmmss}_{2}.{3}", resolvedSetCode, DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8), extension);
                var uploadDate = DateTime.UtcNow;

                // Insert record - image bytes stored directly in the database, no local disk write
                int imageId;
                using (var cmd = new SqlCommand(insertImageSql, con))
                {
                    cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                    cmd.Parameters.Add("@ImageData", SqlDbType.VarBinary, -1).Value = imageBytes;
                    cmd.Parameters.Add("@MimeType", SqlDbType.NVarChar, 50).Value = mimeType;
                    cmd.Parameters.Add("@OriginalFileName", SqlDbType.NVarChar, 260).Value = fileName;
                    cmd.Parameters.Add("@FileSizeBytes", SqlDbType.Int).Value = imageBytes.Length;
                    cmd.Parameters.Add("@ImageType", SqlDbType.VarChar, 50).Value = (object)(request.ImageType != null ? request.ImageType : "MobileUpload");
                    cmd.Parameters.Add("@UploadedBy", SqlDbType.VarChar, 100).Value = (object)(request.UploadedBy != null ? request.UploadedBy : "Mobile App");
                    cmd.Parameters.Add("@UploadDate", SqlDbType.DateTime).Value = uploadDate;
                    imageId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                ReturnJson(context, new { 
                    success = true, 
                    message = "Image uploaded successfully.", 
                    image_id = imageId.ToString(),
                    uploaded_at = uploadDate.ToString("o")
                });
            }
        }
        catch (Exception ex)
        {
            // Log detailed error for debugging
            try {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
                if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
                File.AppendAllText(Path.Combine(logPath, "upload-errors.log"), 
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] ERROR: {1}\nStack: {2}\n\n", DateTime.Now, ex.Message, ex.StackTrace));
            } catch { }
            
            ReturnError(context, 500, "Upload failed: " + ex.Message, "upload_failed");
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

    public bool IsReusable { get { return false; } }

    private class UploadRequest
    {
        [JsonProperty("image_base64")] public string ImageBase64 { get; set; }
        [JsonProperty("image_type")] public string ImageType { get; set; }
        [JsonProperty("uploaded_by")] public string UploadedBy { get; set; }
        [JsonProperty("mime_type")] public string MimeType { get; set; }
    }
}
