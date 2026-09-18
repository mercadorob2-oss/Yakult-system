using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Newtonsoft.Json;

public class CallEscalationSettingsHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="GET"){Err(c,405,"Method not allowed");return;}

        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        try {
            int daysToSup=2, daysMgr=3;
            string supPos="IT Supervisor", mgrPos="IT Manager";

            using(var con=new SqlConnection(Cs())) {
                con.Open();
                if(TableExists(con,"dbo.CallEscalationSettings")) {
                    using(var cmd=new SqlCommand("EXEC dbo.sp_Call_GetEscalationSettings",con)) {
                        using(var r=cmd.ExecuteReader()) {
                            if(r.Read()) {
                                daysToSup = r["DaysToSupervisor"]==DBNull.Value ? 2 : Convert.ToInt32(r["DaysToSupervisor"]);
                                daysMgr   = r["DaysToManager"]==DBNull.Value    ? 3 : Convert.ToInt32(r["DaysToManager"]);
                                supPos    = r["SupervisorPosition"]==DBNull.Value ? "IT Supervisor" : r["SupervisorPosition"].ToString();
                                mgrPos    = r["ManagerPosition"]==DBNull.Value    ? "IT Manager"    : r["ManagerPosition"].ToString();
                            }
                        }
                    }
                }
            }
            Ok(c, new { success=true, daysToSupervisor=daysToSup, daysToManager=daysMgr, supervisorPosition=supPos, managerPosition=mgrPos });
        } catch(Exception ex) { Err(c,500,"Failed to load escalation settings: "+ex.Message); }
    }

    bool TableExists(SqlConnection con, string t) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=t;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}
