<%@ WebHandler Language="C#" Class="GetImageHandler" %>

using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Web;

public class GetImageHandler : IHttpHandler
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
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            context.Response.StatusCode = 503;
            context.Response.Write("Database connection not configured.");
            return;
        }

        string idStr = context.Request.QueryString["id"];
        int imageId;
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out imageId))
        {
            context.Response.StatusCode = 400;
            context.Response.Write("Valid image 'id' parameter is required.");
            return;
        }

        string imagePath = null;
        byte[] imageData = null;
        string mimeType = null;
        try
        {
            using (var con = new SqlConnection(ConnectionString))
            {
                con.Open();
                const string sql = "SELECT TOP 1 ImagePath, ImageData, MimeType FROM dbo.SetImages WHERE ImageId = @ImageId";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ImageId", imageId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            imagePath = reader.IsDBNull(0) ? null : reader.GetString(0);
                            imageData = reader.IsDBNull(1) ? null : (byte[])reader[1];
                            mimeType = reader.IsDBNull(2) ? null : reader.GetString(2);
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            context.Response.Write("Database error.");
            return;
        }

        // Preferred path: image bytes stored directly in the database
        if (imageData != null && imageData.Length > 0)
        {
            context.Response.ContentType = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType;
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            context.Response.Cache.SetMaxAge(TimeSpan.FromDays(7));
            context.Response.BinaryWrite(imageData);
            return;
        }

        // Fallback: legacy rows that only have a filesystem path
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            context.Response.StatusCode = 404;
            context.Response.Write("Image not found in database.");
            return;
        }

        string physicalPath;
        if (imagePath.StartsWith("~/"))
        {
            physicalPath = context.Server.MapPath(imagePath);
        }
        else
        {
            physicalPath = imagePath;
        }

        if (!File.Exists(physicalPath))
        {
            context.Response.StatusCode = 404;
            context.Response.Write("Image file not found on server.");
            return;
        }

        string ext = Path.GetExtension(physicalPath).ToLower();
        string contentType = "image/jpeg"; // Default

        switch (ext)
        {
            case ".png": contentType = "image/png"; break;
            case ".gif": contentType = "image/gif"; break;
            case ".bmp": contentType = "image/bmp"; break;
        }

        context.Response.ContentType = contentType;

        // Let the browser cache the image to save bandwidth
        context.Response.Cache.SetCacheability(HttpCacheability.Public);
        context.Response.Cache.SetMaxAge(TimeSpan.FromDays(7));

        context.Response.TransmitFile(physicalPath);
    }

    public bool IsReusable { get { return false; } }
}
