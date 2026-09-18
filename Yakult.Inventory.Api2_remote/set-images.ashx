<%@ WebHandler Language="C#" Class="SetImagesHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class SetImagesHandler : IHttpHandler
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
            ReturnError(context, 503, "Image service is not configured.", "service_unavailable");
            return;
        }

        // Support GET requests to fetch images
        if (!context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            ReturnError(context, 405, "Only GET requests are supported.", "method_not_allowed");
            return;
        }

        // Get query parameters
        string token = context.Request.QueryString["token"];
        string setCode = context.Request.QueryString["set_code"];

        // Handle "null" string as actual null
        if (token == "null") token = null;
        if (setCode == "null") setCode = null;

        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(setCode))
        {
            ReturnError(context, 400, "Either 'token' or 'set_code' query parameter is required.", "missing_parameters");
            return;
        }

        try
        {
            List<SetImageRecord> images = new List<SetImageRecord>();
            string resolvedSetCode = null;

            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();

                // First, get the SetId
                int setId = 0;

                if (!string.IsNullOrWhiteSpace(token))
                {
                    const string getSetByTokenSql = @"
                        SELECT TOP 1 SetId, SetCode 
                        FROM dbo.[Set] 
                        WHERE QRToken = @Token AND (IsInvoice = 0 OR IsInvoice IS NULL);";

                    using (var cmd = new SqlCommand(getSetByTokenSql, con))
                    {
                        Guid tokenGuid;
                        if (!Guid.TryParse(token, out tokenGuid))
                        {
                            ReturnError(context, 400, "Invalid token format.", "invalid_token");
                            return;
                        }
                        cmd.Parameters.AddWithValue("@Token", tokenGuid);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                setId = Convert.ToInt32(reader["SetId"]);
                                resolvedSetCode = reader["SetCode"] as string;
                            }
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(setCode))
                {
                    const string getSetByCodeSql = @"
                        SELECT TOP 1 SetId, SetCode 
                        FROM dbo.[Set] 
                        WHERE SetCode = @SetCode AND (IsInvoice = 0 OR IsInvoice IS NULL);";

                    using (var cmd = new SqlCommand(getSetByCodeSql, con))
                    {
                        cmd.Parameters.AddWithValue("@SetCode", setCode);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                setId = Convert.ToInt32(reader["SetId"]);
                                resolvedSetCode = reader["SetCode"] as string;
                            }
                        }
                    }
                }

                if (setId == 0)
                {
                    ReturnError(context, 404, "Set not found.", "set_not_found");
                    return;
                }

                // Get images for this set
                const string getImagesSql = @"
                    SELECT ImageId, SetId, ImagePath, ImageType, UploadedBy, UploadDate
                    FROM dbo.SetImages
                    WHERE SetId = @SetId
                    ORDER BY UploadDate DESC;";

                using (var cmd = new SqlCommand(getImagesSql, con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            images.Add(new SetImageRecord
                            {
                                ImageId = Convert.ToInt32(reader["ImageId"]),
                                SetId = Convert.ToInt32(reader["SetId"]),
                                ImagePath = reader["ImagePath"] as string,
                                ImageType = reader["ImageType"] as string,
                                UploadedBy = reader["UploadedBy"] as string,
                                UploadDate = reader["UploadDate"] as DateTime?
                            });
                        }
                    }
                }
            }

            // Return success response
            var response = new
            {
                success = true,
                set_code = resolvedSetCode,
                image_count = images.Count,
                images = images
            };

            context.Response.Write(JsonConvert.SerializeObject(response));
        }
        catch (Exception ex)
        {
            ReturnError(context, 500, "Failed to retrieve images: " + ex.Message, "server_error");
        }
    }

    private void ReturnError(HttpContext context, int httpCode, string message, string errorCode)
    {
        context.Response.StatusCode = httpCode;
        context.Response.Write(JsonConvert.SerializeObject(new
        {
            success = false,
            error_code = errorCode,
            message = message
        }));
    }

    public bool IsReusable { get { return false; } }
}

public class SetImageRecord
{
    [JsonProperty("image_id")]
    public int ImageId { get; set; }

    [JsonProperty("set_id")]
    public int SetId { get; set; }

    [JsonProperty("image_path")]
    public string ImagePath { get; set; }

    [JsonProperty("image_type")]
    public string ImageType { get; set; }

    [JsonProperty("uploaded_by")]
    public string UploadedBy { get; set; }

    [JsonProperty("upload_date")]
    public DateTime? UploadDate { get; set; }
}
