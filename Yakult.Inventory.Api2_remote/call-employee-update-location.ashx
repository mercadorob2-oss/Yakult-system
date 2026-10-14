<%@ WebHandler Language="C#" Class="CallEmployeeUpdateLocationHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class CallEmployeeUpdateLocationHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Method not allowed");return;}

        // IT staff only, like desktop Update-Employee-Location (CanUpdateLocation
        // requires CallITMonitoring portal access).
        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        Dictionary<string,object> body;
        try { body=JsonConvert.DeserializeObject<Dictionary<string,object>>(new System.IO.StreamReader(c.Request.InputStream).ReadToEnd()); }
        catch { body=null; }
        if(body==null){Err(c,400,"Invalid request body");return;}

        int? empId=ReqId(body,"empId"); int? comId=ReqId(body,"comId"); int? branchId=ReqId(body,"branchId");
        bool badDept; int? deptId=OptId(body,"deptId",out badDept);
        if(!empId.HasValue||empId.Value<=0){Err(c,400,"empId is required and must be a positive employee id");return;}
        if(!comId.HasValue||comId.Value<=0){Err(c,400,"comId is required and must be a positive company id");return;}
        if(!branchId.HasValue||branchId.Value<=0){Err(c,400,"branchId is required and must be a positive branch id");return;}
        if(badDept){Err(c,400,"deptId must be an integer");return;}

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                if(!CallEmployeeWrite.TableExists(con,"dbo.Employee")){Err(c,404,"Employee not found");return;}
                foreach(string col in new[]{"ComId","BranchId","DeptId"}){
                    if(!CallEmployeeWrite.ColumnExists(con,"dbo.Employee",col)){Err(c,400,"Employee schema is not installed in this database (missing "+col+")");return;}
                }
                // Employee must exist and be active.
                bool exists, active=true;
                try {
                    bool hasActive=CallEmployeeWrite.ColumnExists(con,"dbo.Employee","Active");
                    using(var cmd=new SqlCommand("SELECT TOP 1 "+(hasActive?"Active":"1")+" FROM dbo.Employee WHERE EmpId=@Id",con)){
                        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=empId.Value;
                        object v=cmd.ExecuteScalar();
                        exists=v!=null&&v!=DBNull.Value;
                        if(exists&&hasActive) active=Convert.ToBoolean(v);
                    }
                } catch { Err(c,404,"Employee not found"); return; }
                if(!exists||!active){Err(c,404,"Employee not found");return;}

                if(!CallEmployeeWrite.RefExists(con,"dbo.Company","ComId",comId.Value)){Err(c,400,"comId does not reference an existing company");return;}
                if(!CallEmployeeWrite.RefExists(con,"dbo.Branch","BranchId",branchId.Value)){Err(c,400,"branchId does not reference an existing branch");return;}
                if(deptId.HasValue&&deptId.Value>0&&!CallEmployeeWrite.RefExists(con,"dbo.Department","DeptId",deptId.Value)){Err(c,400,"deptId does not reference an existing department");return;}
                // Mapping ensured first on the same connection, mirroring desktop.
                CallEmployeeWrite.EnsureMapping(con,comId.Value,branchId.Value,deptId);

                // Mirrors EmployeeRepository.Location UpdateLocationAsync columns
                // ComId/DeptId/BranchId by EmpId, plus ModifiedBy/DateModified when
                // those audit columns exist (JWT actor as the modifier).
                object deptParam=deptId.HasValue&&deptId.Value>0?(object)deptId.Value:DBNull.Value;
                bool hasModifiedBy=CallEmployeeWrite.ColumnExists(con,"dbo.Employee","ModifiedBy");
                bool hasDateModified=CallEmployeeWrite.ColumnExists(con,"dbo.Employee","DateModified");
                string set="ComId = @CompanyId, DeptId = @DepartmentId, BranchId = @BranchId";
                if(hasModifiedBy) set+=", ModifiedBy = @UserId";
                if(hasDateModified) set+=", DateModified = SYSUTCDATETIME()";
                try {
                    using(var cmd=new SqlCommand("UPDATE dbo.Employee SET "+set+" WHERE EmpId = @EmpId",con)){
                        cmd.Parameters.Add("@CompanyId",SqlDbType.Int).Value=comId.Value;
                        cmd.Parameters.Add("@DepartmentId",SqlDbType.Int).Value=deptParam;
                        cmd.Parameters.Add("@BranchId",SqlDbType.Int).Value=branchId.Value;
                        cmd.Parameters.Add("@EmpId",SqlDbType.Int).Value=empId.Value;
                        if(hasModifiedBy) cmd.Parameters.Add("@UserId",SqlDbType.Int).Value=actor.UserId;
                        cmd.ExecuteNonQuery();
                    }
                } catch(SqlException ex) {
                    if(ex.Number==547){Err(c,400,"Referenced company, branch, or department no longer exists");return;}
                    throw;
                }
                Ok(c,new{success=true,employee=new{
                    empId=empId.Value,
                    comId=comId.Value,
                    branchId=branchId.Value,
                    deptId=deptId.HasValue&&deptId.Value>0?deptId:(int?)null
                }});
            }
        } catch(Exception ex){Err(c,500,"Failed to update employee location: "+ex.Message);}
    }

    int? ReqId(Dictionary<string,object> b, string k) {
        object v; if(!b.TryGetValue(k,out v)||v==null) return null;
        string s=v.ToString().Trim(); if(s.Length==0) return null;
        int n; if(!int.TryParse(s,out n)) return null;
        return n;
    }
    int? OptId(Dictionary<string,object> b, string k, out bool malformed) {
        malformed=false; object v;
        if(!b.TryGetValue(k,out v)||v==null) return null;
        string s=v.ToString().Trim(); if(s.Length==0) return null;
        int n; if(!int.TryParse(s,out n)){malformed=true;return null;}
        return n;
    }
    public bool IsReusable{get{return false;}}
}
