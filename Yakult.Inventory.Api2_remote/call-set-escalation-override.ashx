<%@ WebHandler Language="C#" Class="CallSetEscalationOverrideHandler" %>
using System;
using System.IO;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CallSetEscalationOverrideHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Method not allowed");return;}

        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        try {
            string body = new StreamReader(c.Request.InputStream).ReadToEnd();
            var json = JObject.Parse(body);

            int ticketId = json["ticketId"]?.Value<int>() ?? 0;
            if(ticketId<=0){Err(c,400,"ticketId is required");return;}

            int? daysSup         = json["daysToSupervisor"]?.Value<int?>();
            int? daysMgr         = json["daysToManager"]?.Value<int?>();
            string reason        = json["reason"]?.Value<string>();
            int? changedByUserId = actor.UserId;
            bool clear           = json["clear"]?.Value<bool>() ?? false;

            if(string.IsNullOrWhiteSpace(reason)){Err(c,400,"reason is required");return;}

            using(var con=new SqlConnection(Cs())) {
                con.Open();
                if(!TableExists(con,"dbo.CallTicketEscalationOverride")){
                    Err(c,503,"Escalation override schema is not installed in this database.");return;
                }
                using(var cmd=new SqlCommand("dbo.sp_Call_SetTicketEscalationOverride",con)) {
                    cmd.CommandType=CommandType.StoredProcedure;
                    cmd.Parameters.Add("@TicketId",          SqlDbType.Int).Value          = ticketId;
                    cmd.Parameters.Add("@DaysToSupervisor",  SqlDbType.Int).Value          = daysSup.HasValue ? (object)daysSup.Value : DBNull.Value;
                    cmd.Parameters.Add("@DaysToManager",     SqlDbType.Int).Value          = daysMgr.HasValue ? (object)daysMgr.Value : DBNull.Value;
                    cmd.Parameters.Add("@Reason",            SqlDbType.NVarChar,400).Value = (object)reason ?? DBNull.Value;
                    cmd.Parameters.Add("@ChangedByUserId",   SqlDbType.Int).Value          = changedByUserId.HasValue ? (object)changedByUserId.Value : DBNull.Value;
                    cmd.Parameters.Add("@Clear",             SqlDbType.Bit).Value          = clear;
                    cmd.ExecuteNonQuery();
                }
            }
            Ok(c, new { success=true, message="Escalation override saved." });
        } catch(SqlException ex) when(ex.Number>=50021 && ex.Number<=50028) {
            Err(c,400,ex.Message);
        } catch(Exception ex) { Err(c,500,"Failed to set escalation override: "+ex.Message); }
    }

    bool TableExists(SqlConnection con, string t) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=t;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}
