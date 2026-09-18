<%@ WebHandler Language="C#" Class="CallEmployeesHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class CallEmployeesHandler : IHttpHandler {
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

        int? comId=null;   { int v; if(int.TryParse(c.Request.QueryString["comId"]??"",out v)&&v>0) comId=v; }
        int? deptId=null;  { int v; if(int.TryParse(c.Request.QueryString["deptId"]??"",out v)&&v>0) deptId=v; }
        int? branchId=null;{ int v; if(int.TryParse(c.Request.QueryString["branchId"]??"",out v)&&v>0) branchId=v; }

        if(!deptId.HasValue){Err(c,400,"deptId is required");return;}

        try {
            var items = new List<object>();
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                if(!TableExists(con,"dbo.Employee")){Ok(c,new{success=true,items=items});return;}

                bool hasBranchId = ColumnExists(con,"dbo.Employee","BranchId");
                bool hasComId    = ColumnExists(con,"dbo.Employee","ComId");
                bool hasBdc      = TableExists(con,"dbo.BranchDepartmentCompany");

                string branchClause = hasBranchId ? " AND (@BranchId IS NULL OR e.BranchId=@BranchId)" : "";
                string comClause = (hasBdc && hasBranchId)
                    ? @" AND (@ComId IS NULL OR EXISTS(
    SELECT 1 FROM dbo.BranchDepartmentCompany bdc
    WHERE bdc.CompanyID=@ComId AND bdc.DepartmentID=@DeptId
      AND bdc.BranchID=COALESCE(@BranchId,e.BranchId)))"
                    : (hasComId ? " AND (@ComId IS NULL OR e.ComId=@ComId)" : "");

                string sql = "SELECT e.EmpId AS id, e.Name AS name FROM dbo.Employee e WHERE e.Active=1 AND e.DeptId=@DeptId"
                    + branchClause + comClause + " ORDER BY e.Name, e.EmpId";

                using(var cmd=new SqlCommand(sql,con)) {
                    cmd.Parameters.Add("@DeptId",SqlDbType.Int).Value=deptId.Value;
                    cmd.Parameters.Add("@BranchId",SqlDbType.Int).Value=branchId.HasValue?(object)branchId.Value:(object)DBNull.Value;
                    cmd.Parameters.Add("@ComId",SqlDbType.Int).Value=comId.HasValue?(object)comId.Value:(object)DBNull.Value;
                    using(var r=cmd.ExecuteReader()) {
                        while(r.Read()) items.Add(new{ id=Convert.ToInt32(r["id"]), name=r["name"].ToString() });
                    }
                }
            }
            Ok(c,new{success=true,items=items});
        } catch(Exception ex){Err(c,500,"Failed to load employees: "+ex.Message);}
    }

    bool TableExists(SqlConnection con, string t) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=t;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    bool ColumnExists(SqlConnection con, string table, string col) {
        using(var cmd=new SqlCommand(@"SELECT CASE WHEN EXISTS(
SELECT 1 FROM sys.columns c INNER JOIN sys.objects o ON o.object_id=c.object_id
WHERE o.object_id=OBJECT_ID(@T) AND c.name=@C) THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=table;
            cmd.Parameters.Add("@C",SqlDbType.NVarChar).Value=col;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}
