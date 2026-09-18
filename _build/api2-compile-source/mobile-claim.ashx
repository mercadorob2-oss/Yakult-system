<%@ WebHandler Language="C#" Class="MobileClaimHandler" %>

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MobileClaimHandler : IHttpHandler
{
    private static readonly object FileLock = new object();

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");

        try
        {
            var claimToken = Guid.NewGuid().ToString("N");
            var serials = ClaimSerials(claimToken);

            if (serials == null || serials.Count == 0)
            {
                context.Response.Write(JsonConvert.SerializeObject(new
                {
                    success = false,
                    claimToken = (string)null,
                    serials = (object)null
                }));
                return;
            }

            context.Response.Write(JsonConvert.SerializeObject(new
            {
                success = true,
                claimToken,
                serials = serials
            }));
        }
        catch (IOException)
        {
            context.Response.StatusCode = 503;
            context.Response.Write(JsonConvert.SerializeObject(new
            {
                success = false,
                message = "Could not claim serials right now. Please try again."
            }));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Write(JsonConvert.SerializeObject(new
            {
                success = false,
                message = "Something went wrong: " + ex.Message
            }));
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
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        return Path.Combine(folder, "claimed-" + token + ".json");
    }

    private static JArray ClaimSerials(string claimToken)
    {
        var queuePath = GetQueuePath();
        var claimPath = GetClaimPath(claimToken);

        lock (FileLock)
        {
            if (!File.Exists(queuePath))
                return new JArray();

            var json = File.ReadAllText(queuePath);
            var list = new JArray();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { list = NormalizeQueue(JArray.Parse(json)); }
                catch { list = new JArray(); }
            }

            if (list.Count == 0)
                return list;

            File.WriteAllText(claimPath, JsonConvert.SerializeObject(list, Formatting.Indented), System.Text.Encoding.UTF8);
            File.WriteAllText(queuePath, "[]");
            return list;
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
