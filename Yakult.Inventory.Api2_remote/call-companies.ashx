<%@ WebHandler Language="C#" Class="CallCompaniesHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class CallCompaniesHandler : IHttpHandler {
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
            var items = new List<object>();
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                using(var cmd=new SqlCommand("SELECT c.ComId AS id, c.Name AS name FROM dbo.Company c WHERE c.Active=1 ORDER BY c.Name",con)) {
                    using(var r=cmd.ExecuteReader()) {
                        while(r.Read()) {
                            items.Add(new{
                                id=Convert.ToInt32(r["id"]),
                                name=r["name"].ToString()
                            });
                        }
                    }
                }
            }
            Ok(c,new{success=true,items=items});
        } catch(Exception ex){Err(c,500,"Failed to load companies: "+ex.Message);}
    }

    public bool IsReusable{get{return false;}}
}
