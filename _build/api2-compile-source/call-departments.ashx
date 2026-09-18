<%@ WebHandler Language="C#" Class="CallDepartmentsHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class CallDepartmentsHandler : IHttpHandler {
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

        int? comId=null;
        { int v; if(int.TryParse(c.Request.QueryString["comId"]??"",out v)&&v>0) comId=v; }

        try {
            var items = new List<object>();
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                string sql;
                if(comId.HasValue && TableExists(con,"dbo.BranchDepartmentCompany")) {
                    sql=@"SELECT d.DeptId AS id, d.Name AS name
FROM dbo.Department d
WHERE d.Active=1
  AND EXISTS (
      SELECT 1 FROM dbo.BranchDepartmentCompany bdc
      WHERE bdc.DepartmentID=d.DeptId AND bdc.CompanyID=@ComId
  )
ORDER BY d.Name";
                } else {
                    sql="SELECT d.DeptId AS id, d.Name AS name FROM dbo.Department d WHERE d.Active=1 ORDER BY d.Name";
                }
                using(var cmd=new SqlCommand(sql,con)) {
                    if(comId.HasValue) cmd.Parameters.Add("@ComId",SqlDbType.Int).Value=comId.Value;
                    using(var r=cmd.ExecuteReader()) {
                        while(r.Read()) items.Add(new{ id=Convert.ToInt32(r["id"]), name=r["name"].ToString() });
                    }
                }
                // Fallback: if filtered query returned nothing, return all departments
                if(items.Count==0 && comId.HasValue) {
                    items.Clear();
                    using(var cmd2=new SqlCommand("SELECT d.DeptId AS id, d.Name AS name FROM dbo.Department d WHERE d.Active=1 ORDER BY d.Name",con)) {
                        using(var r2=cmd2.ExecuteReader()) {
                            while(r2.Read()) items.Add(new{ id=Convert.ToInt32(r2["id"]), name=r2["name"].ToString() });
                        }
                    }
                }
            }
            Ok(c,new{success=true,items=items});
        } catch(Exception ex){Err(c,500,"Failed to load departments: "+ex.Message);}
    }

    bool TableExists(SqlConnection con, string tableName) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=tableName;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}
