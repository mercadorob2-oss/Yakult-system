<%@ WebHandler Language="C#" Class="MobileConfirmHandler" %>

using System;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;

public class MobileConfirmHandler : IHttpHandler
{
    private static readonly object FileLock = new object();

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");

        var token = (context.Request.QueryString["token"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = 400;
            context.Response.Write(JsonConvert.SerializeObject(new { success = false, message = "Token is required." }));
            return;
        }

        try
        {
            DeleteClaimFile(token);
            context.Response.Write(JsonConvert.SerializeObject(new { success = true, message = "Claim confirmed." }));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Write(JsonConvert.SerializeObject(new { success = false, message = "Failed to confirm claim: " + ex.Message }));
        }
    }

    private static string GetClaimPath(string token)
    {
        var appData = HostingEnvironment.MapPath("~/App_Data");
        var folder = Path.Combine(appData ?? Path.GetTempPath(), "mobile-serials");
        return Path.Combine(folder, "claimed-" + token + ".json");
    }

    private static void DeleteClaimFile(string token)
    {
        var path = GetClaimPath(token);
        lock (FileLock)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public bool IsReusable { get { return false; } }
}
