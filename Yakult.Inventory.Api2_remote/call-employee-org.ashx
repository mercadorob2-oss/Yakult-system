<%@ WebHandler Language="C#" Class="CallEmployeeOrgHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Newtonsoft.Json;

public class CallEmployeeOrgHandler : IHttpHandler {
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

        int empId = 0;
        int.TryParse(c.Request.QueryString["empId"] ?? c.Request.QueryString["id"] ?? "", out empId);
        if(empId <= 0){ Err(c,400,"empId is required"); return; }

        try {
            using(var con=new SqlConnection(Cs())) {
                con.Open();
                if(!TableExists(con,"dbo.Employee")){ Err(c,404,"Employee table not found"); return; }
                bool hasBranchId = ColumnExists(con,"dbo.Employee","BranchId");
                bool hasComId = ColumnExists(con,"dbo.Employee","ComId");
                bool hasBdc = TableExists(con,"dbo.BranchDepartmentCompany");

                string comSelect;
                if(hasBdc && hasBranchId) {
                    string fb = hasComId ? "e.ComId" : "CAST(NULL AS int)";
                    comSelect = "COALESCE((SELECT TOP 1 bdc.CompanyID FROM dbo.BranchDepartmentCompany bdc WHERE bdc.BranchID=e.BranchId AND (bdc.DepartmentID=e.DeptId OR bdc.DepartmentID IS NULL) ORDER BY CASE WHEN bdc.DepartmentID=e.DeptId THEN 0 ELSE 1 END, bdc.CompanyID ASC), " + fb + ") AS comId,";
                } else {
                    comSelect = hasComId ? "e.ComId AS comId," : "CAST(NULL AS int) AS comId,";
                }
                string branchSelect = hasBranchId ? "e.BranchId AS branchId" : "CAST(NULL AS int) AS branchId";

                string sql = "SELECT TOP 1 e.EmpId AS empId, e.Name AS employeeName, e.DeptId AS deptId, " + branchSelect + ", " + comSelect + " e.Name AS dummy FROM dbo.Employee e WHERE e.EmpId=@EmpId";

                using(var cmd=new SqlCommand(sql, con)) {
                    cmd.Parameters.Add("@EmpId",SqlDbType.Int).Value = empId;
                    using(var r=cmd.ExecuteReader()) {
                        if(!r.Read()){ Err(c,404,"Employee not found"); return; }
                        var result = new{
                            success=true,
                            empId=Convert.ToInt32(r["empId"]),
                            employeeName=r["employeeName"].ToString(),
                            deptId = r["deptId"]==DBNull.Value ? (int?)null : Convert.ToInt32(r["deptId"]),
                            branchId = r["branchId"]==DBNull.Value ? (int?)null : Convert.ToInt32(r["branchId"]),
                            comId = r["comId"]==DBNull.Value ? (int?)null : Convert.ToInt32(r["comId"])
                        };
                        Ok(c,result);
                    }
                }
            }
        } catch(Exception ex){ Err(c,500,"Failed to load employee org: "+ex.Message); }
    }

    bool TableExists(SqlConnection con, string t) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@T,'U') IS NOT NULL THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=t;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }
    bool ColumnExists(SqlConnection con, string table, string col) {
        using(var cmd=new SqlCommand(@"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.columns c INNER JOIN sys.objects o ON o.object_id=c.object_id WHERE o.object_id=OBJECT_ID(@T) AND c.name=@C) THEN 1 ELSE 0 END",con)) {
            cmd.Parameters.Add("@T",SqlDbType.NVarChar).Value=table;
            cmd.Parameters.Add("@C",SqlDbType.NVarChar).Value=col;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }
    public bool IsReusable{get{return false;}}
}
