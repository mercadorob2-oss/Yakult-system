<%@ WebHandler Language="C#" Class="MobileCancelHandler" %>

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileCancelHandler : IHttpHandler
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
            RestoreClaimToQueue(token);
            context.Response.Write(JsonConvert.SerializeObject(new { success = true, message = "Claim cancelled, serials restored to queue." }));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Write(JsonConvert.SerializeObject(new { success = false, message = "Failed to cancel claim: " + ex.Message }));
        }
    }

    private static string GetQueuePath()
    {
        var appData = HostingEnvironment.MapPath("~/App_Data");
        var folder = Path.Combine(appData ?? Path.GetTempPath(), "mobile-serials");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        return Path.Combine(folder, "mobile-serials.json");
    }

    private static string GetClaimPath(string token)
    {
        var appData = HostingEnvironment.MapPath("~/App_Data");
        var folder = Path.Combine(appData ?? Path.GetTempPath(), "mobile-serials");
        return Path.Combine(folder, "claimed-" + token + ".json");
    }

    private static void RestoreClaimToQueue(string token)
    {
        var claimPath = GetClaimPath(token);
        var queuePath = GetQueuePath();

        lock (FileLock)
        {
            if (!File.Exists(claimPath))
                return;

            var claimJson = File.ReadAllText(claimPath);
            var claimed = new JArray();
            if (!string.IsNullOrWhiteSpace(claimJson))
            {
                try { claimed = NormalizeQueue(JArray.Parse(claimJson)); }
                catch { claimed = new JArray(); }
            }

            if (claimed.Count > 0)
            {
                var queueList = new JArray();
                if (File.Exists(queuePath))
                {
                    var queueJson = File.ReadAllText(queuePath);
                    if (!string.IsNullOrWhiteSpace(queueJson))
                    {
                        try { queueList = NormalizeQueue(JArray.Parse(queueJson)); }
                        catch { queueList = new JArray(); }
                    }
                }

                var restored = new JArray(claimed);
                foreach (var s in queueList)
                {
                    var serial = (s["serialNumber"] ?? "").ToString();
                    bool found = false;
                    foreach (var r in restored)
                        if (string.Equals((r["serialNumber"] ?? "").ToString(), serial, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (!found) restored.Add(s);
                }

                File.WriteAllText(queuePath, JsonConvert.SerializeObject(restored, Formatting.Indented), System.Text.Encoding.UTF8);
            }

            File.Delete(claimPath);
        }
    }

    private static JArray NormalizeQueue(JArray raw)
    {
        var normalized = new JArray();
        foreach (var token in raw)
        {
            if (token == null || token.Type == JTokenType.Null) continue;
            if (token.Type == JTokenType.String)
            {
                var serial = token.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(serial)) normalized.Add(new JObject { ["serialNumber"] = serial });
                continue;
            }
            var obj = token as JObject;
            if (obj != null && !string.IsNullOrWhiteSpace((obj["serialNumber"] ?? "").ToString())) normalized.Add(obj);
        }
        return normalized;
    }

    public bool IsReusable { get { return false; } }
}
