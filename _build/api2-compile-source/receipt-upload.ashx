<%@ WebHandler Language="C#" Class="ReceiptUploadHandler" %>

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using Newtonsoft.Json;

public class ReceiptUploadHandler : IHttpHandler
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
            ReturnError(context, 503, "Receipt upload service is not configured. Please contact IT.", "service_unavailable");
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

        if (token == "null") token = null;
        if (setCode == "null") setCode = null;
        if (documentNumber == "null") documentNumber = null;

        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(setCode) && string.IsNullOrWhiteSpace(documentNumber))
        {
            ReturnError(context, 400, "Token, set_code, or document_number is required.", "missing_identifier");
            return;
        }

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

        // "PDF" is the combined-document category (mirrors the desktop Receipt Set Viewer's
        // "PDF Document" tab) -- one file that replaces SI/DR/PO entirely, rather than being
        // filed under one of them.
        var docType = (request.DocType ?? "").Trim().ToUpperInvariant();
        if (docType != "SI" && docType != "DR" && docType != "PO" && docType != "PDF")
        {
            ReturnError(context, 400, "doc_type must be SI, DR, PO, or PDF.", "invalid_doc_type");
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
            // Unlike set-image-upload.ashx, this intentionally does NOT filter on IsInvoice --
            // receipts (SI/DR/PO) apply to both regular Sets and Invoice rows in dbo.[Set].
            const string getSetByTokenSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE QRToken = @Token;";
            const string getSetByCodeSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE SetCode = @SetCode;";
            const string getSetByDocumentNumberSql = "SELECT TOP 1 SetId, SetCode FROM dbo.[Set] WHERE DocumentNumber = @DocumentNumber;";

            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();

                int setId;
                string resolvedSetCode;

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

                bool hasDocImageTable;
                using (var cmd = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'dbo' AND t.name = 'ReceiptSetDocumentImage') THEN 1 ELSE 0 END",
                    con))
                {
                    hasDocImageTable = Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }

                if (!hasDocImageTable)
                {
                    ReturnError(context, 503,
                        "Receipt multi-image storage is not available on this server. Run Migration_ReceiptSetMultiImage.sql first.",
                        "receipt_storage_unavailable");
                    return;
                }

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

                // If the caller already has a ReceiptSetId (from a prior page in the same scan
                // batch), reuse it so multiple pages of the same SI/DR/PO group under one
                // ReceiptSet instead of fragmenting into one ReceiptSet per page. Verify it's
                // actually linked to this Set first so a stale/foreign id can't attach here.
                int receiptSetId = 0;
                if (request.ReceiptSetId.HasValue && request.ReceiptSetId.Value > 0)
                {
                    using (var cmd = new SqlCommand(
                        "SELECT ReceiptSetId FROM dbo.ReceiptSetLink WHERE ReceiptSetId = @ReceiptSetId AND SetId = @SetId;",
                        con))
                    {
                        cmd.Parameters.Add("@ReceiptSetId", SqlDbType.Int).Value = request.ReceiptSetId.Value;
                        cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                        var existing = cmd.ExecuteScalar();
                        if (existing != null)
                            receiptSetId = Convert.ToInt32(existing);
                    }
                }

                // No batch-carried id (or it didn't resolve) -- before creating a brand new
                // ReceiptSet, check whether this Set already has ANY linked ReceiptSet from a
                // previous mobile session (or desktop). Reusing it keeps "one ReceiptSet per Set"
                // true across separate app sessions too, not just within a single scan batch --
                // otherwise a second mobile session for the same Set would silently create a
                // second ReceiptSet that desktop's single-result GetBySetId lookup would never
                // surface, making the first session's pages effectively invisible there.
                if (receiptSetId == 0)
                {
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 ReceiptSetId FROM dbo.ReceiptSetLink WHERE SetId = @SetId ORDER BY CreatedAt DESC;",
                        con))
                    {
                        cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                        var existing = cmd.ExecuteScalar();
                        if (existing != null)
                            receiptSetId = Convert.ToInt32(existing);
                    }
                }

                if (receiptSetId == 0)
                {
                    // Simple v1 path: the first page of a batch creates a fresh ReceiptSet + link,
                    // with NULL coverage dates. This intentionally does not replicate the desktop
                    // app's coverage-period-aware AttachReceiptSetToSet (which reuses/merges into
                    // an existing ReceiptSet based on renewal periods) -- that logic lives only in
                    // the desktop app's C# repository and isn't available to this handler. NULL
                    // coverage dates are exempt from UX_ReceiptSetLink_Set_Coverage (a filtered
                    // unique index that only applies when both coverage dates are non-null), so
                    // this never collides with desktop-created links. Separate mobile scan
                    // sessions for the same Set will still produce separate ReceiptSet rows
                    // rather than merging into one; reconcile/merge from the desktop app if needed.
                    const string insertReceiptSetSql = @"
INSERT INTO dbo.ReceiptSet (Supplier, SiNumber, DrNumber, PoNumber, CreatedAt)
VALUES (@Supplier, @SiNumber, @DrNumber, @PoNumber, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    using (var cmd = new SqlCommand(insertReceiptSetSql, con))
                    {
                        cmd.Parameters.Add("@Supplier", SqlDbType.NVarChar, 200).Value = (object)request.Supplier ?? DBNull.Value;
                        cmd.Parameters.Add("@SiNumber", SqlDbType.NVarChar, 50).Value = (object)request.SiNumber ?? DBNull.Value;
                        cmd.Parameters.Add("@DrNumber", SqlDbType.NVarChar, 50).Value = (object)request.DrNumber ?? DBNull.Value;
                        cmd.Parameters.Add("@PoNumber", SqlDbType.NVarChar, 50).Value = (object)request.PoNumber ?? DBNull.Value;
                        receiptSetId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.ReceiptSetLink (ReceiptSetId, SetId, CoverageStartDate, CoverageEndDate) VALUES (@ReceiptSetId, @SetId, NULL, NULL);",
                        con))
                    {
                        cmd.Parameters.Add("@ReceiptSetId", SqlDbType.Int).Value = receiptSetId;
                        cmd.Parameters.Add("@SetId", SqlDbType.Int).Value = setId;
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Reusing an existing ReceiptSet (same batch, or a later session for the same
                    // Set/Invoice) -- merge in any newly-provided field values instead of silently
                    // dropping them. NULLIF(@Value, '') + COALESCE only overwrites when the caller
                    // actually sent a non-blank value, so an earlier session's Supplier/SI/DR/PO
                    // never gets clobbered by a later page upload that left those fields blank.
                    const string updateReceiptSetSql = @"
UPDATE dbo.ReceiptSet
SET
    Supplier = COALESCE(NULLIF(@Supplier, ''), Supplier),
    SiNumber = COALESCE(NULLIF(@SiNumber, ''), SiNumber),
    DrNumber = COALESCE(NULLIF(@DrNumber, ''), DrNumber),
    PoNumber = COALESCE(NULLIF(@PoNumber, ''), PoNumber)
WHERE ReceiptSetId = @ReceiptSetId;";
                    using (var cmd = new SqlCommand(updateReceiptSetSql, con))
                    {
                        cmd.Parameters.Add("@ReceiptSetId", SqlDbType.Int).Value = receiptSetId;
                        cmd.Parameters.Add("@Supplier", SqlDbType.NVarChar, 200).Value = (object)request.Supplier ?? string.Empty;
                        cmd.Parameters.Add("@SiNumber", SqlDbType.NVarChar, 50).Value = (object)request.SiNumber ?? string.Empty;
                        cmd.Parameters.Add("@DrNumber", SqlDbType.NVarChar, 50).Value = (object)request.DrNumber ?? string.Empty;
                        cmd.Parameters.Add("@PoNumber", SqlDbType.NVarChar, 50).Value = (object)request.PoNumber ?? string.Empty;
                        cmd.ExecuteNonQuery();
                    }
                }

                int nextSort;
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(MAX(SortOrder) + 1, 0) FROM dbo.ReceiptSetDocumentImage WHERE ReceiptSetId = @ReceiptSetId AND DocType = @DocType;",
                    con))
                {
                    cmd.Parameters.Add("@ReceiptSetId", SqlDbType.Int).Value = receiptSetId;
                    cmd.Parameters.Add("@DocType", SqlDbType.VarChar, 10).Value = docType;
                    nextSort = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // MimeType is a newer column (AlterTable_ReceiptSetDocumentImage_AddMimeTypeColumn.sql);
                // fall back to writing without it if that migration hasn't been run on this server yet.
                bool hasMimeTypeColumn;
                using (var cmd = new SqlCommand(
                    "SELECT CASE WHEN COL_LENGTH('dbo.ReceiptSetDocumentImage', 'MimeType') IS NOT NULL THEN 1 ELSE 0 END",
                    con))
                {
                    hasMimeTypeColumn = Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }

                var mimeType = string.IsNullOrWhiteSpace(request.MimeType) ? "image/jpeg" : request.MimeType.Trim();

                int imageId;
                string insertImageSql = hasMimeTypeColumn
                    ? @"
INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImageBytes, MimeType, SortOrder, CreatedAt)
VALUES (@ReceiptSetId, @DocType, @ImageBytes, @MimeType, @SortOrder, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);"
                    : @"
INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImageBytes, SortOrder, CreatedAt)
VALUES (@ReceiptSetId, @DocType, @ImageBytes, @SortOrder, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                using (var cmd = new SqlCommand(insertImageSql, con))
                {
                    cmd.Parameters.Add("@ReceiptSetId", SqlDbType.Int).Value = receiptSetId;
                    cmd.Parameters.Add("@DocType", SqlDbType.VarChar, 10).Value = docType;
                    cmd.Parameters.Add("@ImageBytes", SqlDbType.VarBinary, -1).Value = imageBytes;
                    cmd.Parameters.Add("@SortOrder", SqlDbType.Int).Value = nextSort;
                    if (hasMimeTypeColumn)
                        cmd.Parameters.Add("@MimeType", SqlDbType.NVarChar, 50).Value = mimeType;
                    imageId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                ReturnJson(context, new
                {
                    success = true,
                    message = "Receipt image uploaded successfully.",
                    receipt_set_id = receiptSetId,
                    image_id = imageId,
                    set_code = resolvedSetCode,
                    doc_type = docType
                });
            }
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
                if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
                File.AppendAllText(Path.Combine(logPath, "upload-errors.log"),
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] RECEIPT ERROR: {1}\nStack: {2}\n\n", DateTime.Now, ex.Message, ex.StackTrace));
            }
            catch { }

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
        [JsonProperty("doc_type")] public string DocType { get; set; }
        [JsonProperty("supplier")] public string Supplier { get; set; }
        [JsonProperty("si_number")] public string SiNumber { get; set; }
        [JsonProperty("dr_number")] public string DrNumber { get; set; }
        [JsonProperty("po_number")] public string PoNumber { get; set; }
        [JsonProperty("image_base64")] public string ImageBase64 { get; set; }
        [JsonProperty("receipt_set_id")] public int? ReceiptSetId { get; set; }
        [JsonProperty("mime_type")] public string MimeType { get; set; }
    }
}
