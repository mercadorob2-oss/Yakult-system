<%@ WebHandler Language="C#" Class="CallEmployeeCreateHandler" %>
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using Newtonsoft.Json;

public class CallEmployeeCreateHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Method not allowed");return;}

        // Any active account, like desktop QuickAdd which needs only login.
        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireAuthenticated(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        Dictionary<string,object> body;
        try { body=JsonConvert.DeserializeObject<Dictionary<string,object>>(new System.IO.StreamReader(c.Request.InputStream).ReadToEnd()); }
        catch { body=null; }
        if(body==null){Err(c,400,"Invalid request body");return;}

        string name=S(body,"name");
        int? comId=ReqId(body,"comId"); int? branchId=ReqId(body,"branchId");
        bool badDept, badTitle;
        int? deptId=OptId(body,"deptId",out badDept); int? titleId=OptId(body,"titleId",out badTitle);
        string employeeNumber=S(body,"employeeNumber"); string position=S(body,"position"); string description=S(body,"description");

        if(string.IsNullOrWhiteSpace(name)){Err(c,400,"name is required");return;}
        if(name.Length>150){Err(c,400,"name is limited to 150 characters");return;}
        if(employeeNumber.Length>20){Err(c,400,"employeeNumber is limited to 20 characters");return;}
        if(position.Length>50){Err(c,400,"position is limited to 50 characters");return;}
        if(description.Length>400){Err(c,400,"description is limited to 400 characters");return;}
        if(!comId.HasValue||comId.Value<=0){Err(c,400,"comId is required and must be a positive company id");return;}
        if(!branchId.HasValue||branchId.Value<=0){Err(c,400,"branchId is required and must be a positive branch id");return;}
        if(badDept){Err(c,400,"deptId must be an integer");return;}
        if(badTitle){Err(c,400,"titleId must be an integer");return;}
        // createdByUserId is NOT accepted from the body: audit identity comes from the JWT.
        int createdBy=actor.UserId;

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                if(!CallEmployeeWrite.TableExists(con,"dbo.Employee")){Err(c,400,"Employee store is not installed in this database");return;}
                // Core write columns must exist or the INSERT below would 500 on schema drift.
                foreach(string col in new[]{"Name","EmployeeNumber","Position","Description","DateCreated","Createdby","ComId","BranchId","DeptId"}){
                    if(!CallEmployeeWrite.ColumnExists(con,"dbo.Employee",col)){Err(c,400,"Employee schema is not installed in this database (missing "+col+")");return;}
                }
                if(!CallEmployeeWrite.RefExists(con,"dbo.Company","ComId",comId.Value)){Err(c,400,"comId does not reference an existing company");return;}
                if(!CallEmployeeWrite.RefExists(con,"dbo.Branch","BranchId",branchId.Value)){Err(c,400,"branchId does not reference an existing branch");return;}
                if(deptId.HasValue&&deptId.Value>0&&!CallEmployeeWrite.RefExists(con,"dbo.Department","DeptId",deptId.Value)){Err(c,400,"deptId does not reference an existing department");return;}
                if(titleId.HasValue&&titleId.Value>0&&!CallEmployeeWrite.RefExists(con,"dbo.Title","TitleId",titleId.Value)){Err(c,400,"titleId does not reference an existing title");return;}
                // Same open connection, mapping ensured BEFORE the INSERT,
                // mirroring QuickAdd (best-effort when the mapping table is absent).
                CallEmployeeWrite.EnsureMapping(con,comId.Value,branchId.Value,deptId);

                bool hasTitle=CallEmployeeWrite.ColumnExists(con,"dbo.Employee","TitleId");
                bool hasDistributor=CallEmployeeWrite.ColumnExists(con,"dbo.Employee","DistributorId");
                string cols="Name, EmployeeNumber, Position, Description, DateCreated, Createdby, ComId, BranchId, DeptId";
                string vals="@Name, @EmployeeNumber, @Position, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId";
                if(hasTitle){cols+=", TitleId";vals+=", @TitleId";}
                if(hasDistributor){cols+=", DistributorId";vals+=", @DistributorId";}
                int newId;
                using(var cmd=new SqlCommand("INSERT INTO dbo.Employee ("+cols+") OUTPUT INSERTED.EmpId VALUES ("+vals+")",con)){
                    cmd.Parameters.Add("@Name",SqlDbType.NVarChar,150).Value=name;
                    cmd.Parameters.Add("@EmployeeNumber",SqlDbType.VarChar,20).Value=CallEmployeeWrite.NullIfBlank(employeeNumber);
                    cmd.Parameters.Add("@Position",SqlDbType.NVarChar,50).Value=CallEmployeeWrite.NullIfBlank(position);
                    cmd.Parameters.Add("@Description",SqlDbType.NVarChar,400).Value=CallEmployeeWrite.NullIfBlank(description);
                    cmd.Parameters.Add("@DateCreated",SqlDbType.DateTime2).Value=DateTime.Now;
                    cmd.Parameters.Add("@Createdby",SqlDbType.Int).Value=createdBy;
                    cmd.Parameters.Add("@ComId",SqlDbType.Int).Value=comId.Value;
                    cmd.Parameters.Add("@BranchId",SqlDbType.Int).Value=branchId.Value;
                    cmd.Parameters.Add("@DeptId",SqlDbType.Int).Value=deptId.HasValue&&deptId.Value>0?(object)deptId.Value:DBNull.Value;
                    if(hasTitle) cmd.Parameters.Add("@TitleId",SqlDbType.Int).Value=titleId.HasValue&&titleId.Value>0?(object)titleId.Value:DBNull.Value;
                    if(hasDistributor) cmd.Parameters.Add("@DistributorId",SqlDbType.Int).Value=DBNull.Value;
                    try { newId=(int)cmd.ExecuteScalar(); }
                    catch(SqlException ex) {
                        // Race between the RefExists checks and the INSERT
                        // (row deleted concurrently) surfaces as FK 547.
                        if(ex.Number==547){Err(c,400,"Referenced company, branch, department, or title no longer exists");return;}
                        throw;
                    }
                }
                Ok(c,new{success=true,employee=new{
                    empId=newId,
                    name=name,
                    employeeNumber=string.IsNullOrWhiteSpace(employeeNumber)?(string)null:employeeNumber,
                    comId=comId.Value,
                    branchId=branchId.Value,
                    deptId=deptId.HasValue&&deptId.Value>0?deptId:(int?)null
                }});
            }
        } catch(Exception ex){Err(c,500,"Failed to create employee: "+ex.Message);}
    }

    string S(Dictionary<string,object> b, string k) {
        object v; if(!b.TryGetValue(k,out v)||v==null) return "";
        return v.ToString().Trim();
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
