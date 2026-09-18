<%@ WebHandler Language="C#" Class="CallConditionsHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class CallConditionsHandler : IHttpHandler
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
            using (var con = new SqlConnection(Cs()))
            {
                con.Open();

                string sql;
                using (var chk = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.Condition','U') IS NOT NULL THEN 1 ELSE 0 END", con))
                {
                    sql = Convert.ToInt32(chk.ExecuteScalar()) == 1
                        ? "SELECT ConditionID, ConditionName FROM dbo.Condition ORDER BY ConditionName"
                        : "SELECT ConditionID, ConditionName FROM dbo.ItemCondition ORDER BY ConditionName";
                }

                using (var cmd = new SqlCommand(sql, con))
                using (var r = cmd.ExecuteReader())
                {
                    var conditions = new List<object>();
                    while (r.Read())
                    {
                        conditions.Add(new
                        {
                            conditionId = Convert.ToInt32(r["ConditionId"]),
                            conditionName = r["ConditionName"] == DBNull.Value ? "" : r["ConditionName"].ToString()
                        });
                    }
                    Ok(c, new { success = true, conditions });
                }
            }
        }
        catch (Exception ex) { Err(c, 500, "Conditions lookup failed: " + ex.Message); }
    }

    public bool IsReusable { get { return false; } }
}
