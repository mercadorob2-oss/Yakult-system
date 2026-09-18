<%@ WebHandler Language="C#" Class="CallBranchesHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class CallBranchesHandler : IHttpHandler {
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

        int? comId=null; { int v; if(int.TryParse(c.Request.QueryString["comId"]??"",out v)&&v>0) comId=v; }
        int? deptId=null; { int v; if(int.TryParse(c.Request.QueryString["deptId"]??"",out v)&&v>0) deptId=v; }

        try {
            var items = new List<object>();
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                if(!TableExists(con,"dbo.Branch")){Ok(c,new{success=true,items=items});return;}

                bool hasBdc=TableExists(con,"dbo.BranchDepartmentCompany");

                string sql;
                if(hasBdc) {
                    sql=@"SELECT
    b.BranchId AS id,
    CASE
        WHEN ISNULL(b.IsCenter,0)=1 THEN b.Name+' (Center)'
        WHEN ISNULL(b.IsDepot,0)=1 THEN b.Name+' (Depot)'
        WHEN ISNULL(b.IsFactory,0)=1 THEN b.Name+' (Factory)'
        WHEN ISNULL(b.IsDistributor,0)=1 THEN b.Name+' (Distributor)'
        ELSE b.Name
    END AS name
FROM dbo.Branch b
WHERE ISNULL(b.Active,1)=1
  AND (@ComId IS NULL OR EXISTS(SELECT 1 FROM dbo.BranchDepartmentCompany bdc WHERE bdc.BranchID=b.BranchId AND bdc.CompanyID=@ComId))
ORDER BY b.Name, b.BranchId";
                } else {
                    sql=@"SELECT b.BranchId AS id, b.Name AS name FROM dbo.Branch b WHERE ISNULL(b.Active,1)=1 ORDER BY b.Name, b.BranchId";
                }

                using(var cmd=new SqlCommand(sql,con)) {
                    cmd.Parameters.Add("@ComId",SqlDbType.Int).Value=comId.HasValue?(object)comId.Value:(object)DBNull.Value;
                    using(var r=cmd.ExecuteReader()) {
                        while(r.Read()) items.Add(new{ id=Convert.ToInt32(r["id"]), name=r["name"].ToString() });
                    }
                }
            }
            Ok(c,new{success=true,items=items});
        } catch(Exception ex){Err(c,500,"Failed to load branches: "+ex.Message);}
    }

    bool TableExists(SqlConnection con, string tableName) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=tableName;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}
