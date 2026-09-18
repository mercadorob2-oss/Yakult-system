<%@ WebHandler Language="C#" Class="DbInfoHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Script.Serialization;

public class DbInfoHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.TrySkipIisCustomErrors = true;
        context.Response.AddHeader("Access-Control-Allow-Origin", "*");
        context.Response.AddHeader("Cache-Control", "no-store");

        var result = new Dictionary<string, object>();
        result["success"] = false;
        result["configured"] = false;
        result["database"] = null;
        result["dataSource"] = null;
        result["server"] = null;
        result["port"] = null;
        result["message"] = "Database connection is not configured.";

        try
        {
            ConnectionStringSettings selected = null;
            var allConnectionStrings = ConfigurationManager.ConnectionStrings;
            for (var i = 0; i < allConnectionStrings.Count; i++)
            {
                var candidate = allConnectionStrings[i];
                if (candidate == null || string.Equals(candidate.Name, "LocalSqlServer", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(candidate.ConnectionString))
                {
                    selected = candidate;
                    break;
                }
            }

            if (selected != null)
            {
                var builder = new SqlConnectionStringBuilder(selected.ConnectionString);
                string server;
                int? port;
                ParseDataSource(builder.DataSource, out server, out port);

                result["success"] = true;
                result["configured"] = true;
                result["database"] = NullIfEmpty(builder.InitialCatalog);
                result["dataSource"] = NullIfEmpty(builder.DataSource);
                result["server"] = server;
                result["port"] = port.HasValue ? (object)port.Value : null;
                result["message"] = "Configured database properties loaded.";
            }
        }
        catch
        {
            result["message"] = "Unable to read database configuration.";
        }

        context.Response.Write(new JavaScriptSerializer().Serialize(result));
    }

    private static string NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ParseDataSource(string rawDataSource, out string server, out int? port)
    {
        server = NullIfEmpty(rawDataSource);
        port = null;
        if (server == null) return;

        if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            server = server.Substring(4).Trim();

        var comma = server.LastIndexOf(',');
        int parsedPort;
        if (comma > 0 && comma < server.Length - 1 &&
            int.TryParse(server.Substring(comma + 1).Trim(), out parsedPort))
        {
            port = parsedPort;
            server = server.Substring(0, comma).Trim();
        }
    }

    public bool IsReusable { get { return false; } }
}
