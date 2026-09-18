<%@ WebHandler Language="C#" Class="CallDashboardHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using Newtonsoft.Json;

public class CallDashboardHandler : IHttpHandler
{
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode = 200; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode = s; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(new { success = false, message = m })); }

    public void ProcessRequest(HttpContext c)
    {
        c.Response.AddHeader("Access-Control-Allow-Origin", "*");
        c.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods", "GET,OPTIONS");
        if (c.Request.HttpMethod == "OPTIONS") { c.Response.StatusCode = 200; c.Response.End(); return; }
        if (c.Request.HttpMethod != "GET") { Err(c, 405, "Method not allowed"); return; }

        CallTicketApiUser actor; int authStatus; string authMessage;
        if (!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)) { Err(c, authStatus, authMessage); return; }

        try
        {
            int openTickets = 0, criticalTickets = 0, todaysVolume = 0;
            double? avgResolutionMinutes = null;

            using (var con = new SqlConnection(Cs()))
            {
                con.Open();

                // 1. Dashboard metrics view
                using (var cmd = new SqlCommand("SELECT TOP 1 OpenTickets, CriticalTickets, AvgResolutionMinutes, TodaysVolume FROM dbo.vw_Call_DashboardMetrics;", con))
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        openTickets = r["OpenTickets"] == DBNull.Value ? 0 : Convert.ToInt32(r["OpenTickets"]);
                        criticalTickets = r["CriticalTickets"] == DBNull.Value ? 0 : Convert.ToInt32(r["CriticalTickets"]);
                        avgResolutionMinutes = r["AvgResolutionMinutes"] == DBNull.Value ? (double?)null : Convert.ToDouble(r["AvgResolutionMinutes"]);
                        todaysVolume = r["TodaysVolume"] == DBNull.Value ? 0 : Convert.ToInt32(r["TodaysVolume"]);
                    }
                }

                // 2. Ticket volume for last 7 days (fills missing days with zero)
                var volume = new List<object>();
                var toUtcExclusive = DateTime.UtcNow.Date.AddDays(1);
                var fromUtc = toUtcExclusive.AddDays(-7);
                var countsByDay = new Dictionary<DateTime, int>();
                using (var cmd = new SqlCommand(@"
SELECT CONVERT(date, t.CreatedAt) AS [Day], COUNT(*) AS TicketCount
FROM dbo.CallTicket t
WHERE t.CreatedAt >= @FromUtc AND t.CreatedAt < @ToUtcExclusive
GROUP BY CONVERT(date, t.CreatedAt);", con))
                {
                    cmd.Parameters.AddWithValue("@FromUtc", fromUtc);
                    cmd.Parameters.AddWithValue("@ToUtcExclusive", toUtcExclusive);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            var day = Convert.ToDateTime(r["Day"]).Date;
                            countsByDay[day] = Convert.ToInt32(r["TicketCount"]);
                        }
                }

                for (var day = fromUtc.Date; day <= toUtcExclusive.Date.AddDays(-1); day = day.AddDays(1))
                {
                    int count;
                    countsByDay.TryGetValue(day, out count);
                    volume.Add(new { day = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ticketCount = count });
                }

                // 3. Issue type distribution (open tickets only)
                var issueTypes = new List<object>();
                using (var cmd = new SqlCommand("SELECT IssueType, TicketCount FROM dbo.vw_Call_IssueTypeDistribution ORDER BY TicketCount DESC;", con))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        issueTypes.Add(new
                        {
                            issueType = r["IssueType"] == DBNull.Value ? "Unspecified" : r["IssueType"].ToString(),
                            ticketCount = Convert.ToInt32(r["TicketCount"])
                        });

                Ok(c, new
                {
                    success = true,
                    generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    metrics = new
                    {
                        openTickets,
                        criticalTickets,
                        todaysVolume,
                        avgResolutionMinutes
                    },
                    volume,
                    issueTypes
                });
            }
        }
        catch (Exception ex) { Err(c, 500, "Call dashboard failed: " + ex.Message); }
    }

    public bool IsReusable { get { return false; } }
}