<%@ WebHandler Language="C#" Class="ItcmHealthHandler" %>
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

public class ItcmHealthHandler : IHttpHandler
{
    string Cs() { return System.Configuration.ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }

    public void ProcessRequest(HttpContext c)
    {
        c.Response.AddHeader("Access-Control-Allow-Origin", "*");
        c.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods", "GET,OPTIONS");
        if (c.Request.HttpMethod == "OPTIONS") { c.Response.StatusCode = 200; c.Response.End(); return; }

        c.Response.ContentType = "application/json";

        var result = new
        {
            success = true,
            generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            health = "Unknown",
            severity = "Unknown",
            checks = new object[] { },
            metrics = new object { },
            version = new object { }
        };

        try
        {
            using (var con = new SqlConnection(Cs()))
            {
                con.Open();

                // 1. Core schema check
                bool hasCallTicket = false, hasEmailLog = false, hasEmailSettings = false;
                try
                {
                    using (var cmd = new SqlCommand("SELECT COUNT(1) FROM sys.objects WHERE name = 'CallTicket' AND type = 'U'", con))
                        hasCallTicket = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    using (var cmd = new SqlCommand("SELECT COUNT(1) FROM sys.objects WHERE name = 'CallEmailLog' AND type = 'U'", con))
                        hasEmailLog = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    using (var cmd = new SqlCommand("SELECT COUNT(1) FROM sys.objects WHERE name = 'CallEmailSettings' AND type = 'U'", con))
                        hasEmailSettings = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
                catch { }

                // 2. Ticket metrics
                int openTickets = 0, pendingTickets = 0, criticalTickets = 0;
                if (hasCallTicket)
                {
                    try
                    {
                        using (var cmd = new SqlCommand(@"
SELECT
    SUM(CASE WHEN Status NOT IN ('Solved','Resolved (Temporary)') THEN 1 ELSE 0 END) AS OpenTickets,
    SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS PendingTickets,
    SUM(CASE WHEN Status NOT IN ('Solved','Resolved (Temporary)') AND Priority = 'Critical' THEN 1 ELSE 0 END) AS CriticalTickets
FROM dbo.CallTicket", con))
                        using (var r = cmd.ExecuteReader())
                            if (r.Read())
                            {
                                openTickets = r["OpenTickets"] != DBNull.Value ? Convert.ToInt32(r["OpenTickets"]) : 0;
                                pendingTickets = r["PendingTickets"] != DBNull.Value ? Convert.ToInt32(r["PendingTickets"]) : 0;
                                criticalTickets = r["CriticalTickets"] != DBNull.Value ? Convert.ToInt32(r["CriticalTickets"]) : 0;
                            }
                    }
                    catch { }
                }

                // 3. Last email failure
                string lastEmailStatus = "Unknown";
                DateTime? lastEmailTime = null;
                if (hasEmailLog)
                {
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT TOP 1 Status, DateSent FROM dbo.CallEmailLog WHERE Status = 'Failed' ORDER BY DateSent DESC", con))
                        using (var r = cmd.ExecuteReader())
                            if (r.Read())
                            {
                                lastEmailStatus = "Failed";
                                lastEmailTime = r["DateSent"] != DBNull.Value ? Convert.ToDateTime(r["DateSent"]) : (DateTime?)null;
                            }
                            else
                            {
                                lastEmailStatus = "OK";
                            }
                    }
                    catch { }
                }

                // 4. Scheduler heartbeat
                bool hasHeartbeat = false;
                DateTime? lastHeartbeat = null;
                try
                {
                    using (var cmd = new SqlCommand("SELECT COUNT(1) FROM sys.objects WHERE name = 'CallSchedulerHeartbeat' AND type = 'U'", con))
                        hasHeartbeat = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    if (hasHeartbeat)
                    {
                        using (var cmd = new SqlCommand("SELECT TOP 1 ISNULL(LastStartedUtc, LastFinishedUtc) AS LastActivity FROM dbo.CallSchedulerHeartbeat ORDER BY ISNULL(LastStartedUtc, LastFinishedUtc) DESC", con))
                        using (var r = cmd.ExecuteReader())
                            if (r.Read() && r["LastActivity"] != DBNull.Value)
                                lastHeartbeat = Convert.ToDateTime(r["LastActivity"]);
                    }
                }
                catch { }

                // 5. Determine overall health
                string overallHealth = "Healthy";
                string overallSeverity = "Healthy";
                var checks = new System.Collections.Generic.List<object>();

                // Schema
                checks.Add(new { area = "Schema", name = "Core tables", status = hasCallTicket ? "OK" : "MISSING", severity = hasCallTicket ? "Healthy" : "Critical", message = hasCallTicket ? "CallTicket available" : "CallTicket table missing" });
                checks.Add(new { area = "Schema", name = "Email tables", status = hasEmailLog && hasEmailSettings ? "OK" : "PARTIAL", severity = hasEmailLog && hasEmailSettings ? "Healthy" : "Warning", message = hasEmailLog && hasEmailSettings ? "Email schema complete" : "Email schema incomplete" });

                // Tickets
                checks.Add(new { area = "Tickets", name = "Open tickets", status = openTickets.ToString(), severity = "Healthy", message = openTickets + " open tickets" });
                checks.Add(new { area = "Tickets", name = "Pending tickets", status = pendingTickets.ToString(), severity = pendingTickets > 10 ? "Warning" : "Healthy", message = pendingTickets + " pending tickets" });
                checks.Add(new { area = "Tickets", name = "Critical tickets", status = criticalTickets.ToString(), severity = criticalTickets > 0 ? "Warning" : "Healthy", message = criticalTickets + " critical tickets" });

                // Email
                bool recentEmailFailure = lastEmailTime.HasValue && (DateTime.UtcNow - lastEmailTime.Value).TotalHours <= 24;
                checks.Add(new { area = "Email", name = "Recent failures", status = lastEmailStatus, severity = recentEmailFailure ? "Warning" : "Healthy", message = recentEmailFailure ? "Email failed within last 24h" : "No recent email failures" });

                // Scheduler
                checks.Add(new { area = "Scheduler", name = "Heartbeat", status = hasHeartbeat ? "OK" : "NOT DETECTED", severity = hasHeartbeat ? "Healthy" : "Warning", message = hasHeartbeat ? "Central scheduler heartbeat detected" : "No scheduler heartbeat table" });
                if (hasHeartbeat && lastHeartbeat.HasValue)
                {
                    var hoursSince = (DateTime.UtcNow - lastHeartbeat.Value).TotalHours;
                    checks.Add(new { area = "Scheduler", name = "Last activity", status = hoursSince.ToString("F1") + "h ago", severity = hoursSince > 48 ? "Warning" : "Healthy", message = "Last scheduler activity " + hoursSince.ToString("F1") + " hours ago" });
                }

                // Determine overall health
                if (!hasCallTicket)
                {
                    overallHealth = "Unhealthy - Core schema missing";
                    overallSeverity = "Critical";
                }
                else if (recentEmailFailure)
                {
                    overallHealth = "Degraded - Recent email failure";
                    overallSeverity = "Warning";
                }
                else if (criticalTickets > 0)
                {
                    overallHealth = "Healthy - Attention: critical tickets present";
                    overallSeverity = "Warning";
                }
                else if (!hasHeartbeat)
                {
                    overallHealth = "Healthy - Scheduler heartbeat not configured";
                    overallSeverity = "Warning";
                }
                else
                {
                    overallHealth = "Healthy";
                    overallSeverity = "Healthy";
                }

                result = new
                {
                    success = true,
                    generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    health = overallHealth,
                    severity = overallSeverity,
                    checks = checks.ToArray(),
                    metrics = new
                    {
                        openTickets,
                        pendingTickets,
                        criticalTickets,
                        lastEmailStatus,
                        lastEmailTimeUtc = lastEmailTime?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        schedulerHeartbeatDetected = hasHeartbeat,
                        lastSchedulerActivityUtc = lastHeartbeat?.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    },
                    version = new
                    {
                        api = "2.0",
                        endpoint = "itcm-health.ashx"
                    }
                };
            }
        }
        catch (Exception ex)
        {
            c.Response.StatusCode = 500;
            result = new
            {
                success = false,
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                health = "Error",
                severity = "Critical",
                checks = new[] { new { area = "System", name = "Health probe", status = "ERROR", severity = "Critical", message = ex.Message } },
                metrics = new object { },
                version = new { api = "2.0", endpoint = "itcm-health.ashx" }
            };
        }

        c.Response.Write(JsonConvert.SerializeObject(result, Formatting.Indented));
    }

    public bool IsReusable { get { return false; } }
}
