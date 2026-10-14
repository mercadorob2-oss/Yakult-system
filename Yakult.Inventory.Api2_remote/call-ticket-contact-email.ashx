<%@ WebHandler Language="C#" Class="CallTicketContactEmailHandler" %>
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class CallTicketContactEmailHandler : IHttpHandler {
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
        if(!CallTicketApiSecurity.TryRequireAuthenticated(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        int? callerEmpId=null; if(!string.IsNullOrWhiteSpace(c.Request.QueryString["callerEmpId"])){int v; if(int.TryParse(c.Request.QueryString["callerEmpId"],out v))callerEmpId=v;}
        int? comId=null; if(!string.IsNullOrWhiteSpace(c.Request.QueryString["comId"])){int v; if(int.TryParse(c.Request.QueryString["comId"],out v))comId=v;}
        int? deptId=null; if(!string.IsNullOrWhiteSpace(c.Request.QueryString["deptId"])){int v; if(int.TryParse(c.Request.QueryString["deptId"],out v))deptId=v;}
        int? branchId=null; if(!string.IsNullOrWhiteSpace(c.Request.QueryString["branchId"])){int v; if(int.TryParse(c.Request.QueryString["branchId"],out v))branchId=v;}

        string email="",source="";
        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                string e,s;
                if(CallTicketContactEmail.TryResolve(con,callerEmpId,comId,deptId,branchId,out e,out s)){email=e;source=s;}
            }
        } catch(Exception ex){Err(c,500,"Failed to resolve contact email: "+ex.Message);return;}
        Ok(c,new{success=true,email=email,source=source});
    }

    public bool IsReusable{get{return false;}}
}
