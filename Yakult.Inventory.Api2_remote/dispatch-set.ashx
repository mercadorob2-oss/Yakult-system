<%@ WebHandler Language="C#" Class="DispatchSetHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class DispatchSetHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}

        var setCode=(c.Request.QueryString["setCode"]??"").Trim();
        var token=(c.Request.QueryString["token"]??"").Trim();
        if(string.IsNullOrEmpty(setCode)&&string.IsNullOrEmpty(token)){Err(c,400,"setCode or token is required");return;}

        Guid? qrToken=null;
        if(!string.IsNullOrEmpty(token)){
            const string prefix="yakult:set:v1:";
            if(token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
                token=token.Substring(prefix.Length).Trim();
            Guid g;
            if(!Guid.TryParse(token,out g)){Err(c,400,"Invalid token format");return;}
            qrToken=g;
        }

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                var whereClause=qrToken.HasValue ? "s.QRToken=@Token" : "s.SetCode=@SetCode";

                var resultFound=false;
                string sc=null,empName=null,dept=null,branch=null,status=null,createdBy=null,reqDate=null,createdDate=null,company=null;

                using(var cmd=new SqlCommand(
                    "SELECT s.SetCode,s.Status,s.DispatchDate,s.CreatedAt," +
                    "e.Name AS EmployeeName,b.Name AS BranchName,d.Name AS DepartmentName,u.Name AS CreatedByName,co.Name AS CompanyName " +
                    "FROM dbo.[Set] s " +
                    "OUTER APPLY (SELECT TOP 1 r.EmpId,r.ReceivedById,r.ComId,r.DeptId,r.BranchId FROM dbo.Request r WHERE r.SetId=s.SetId OR r.ReqId=s.ReqId ORDER BY CASE WHEN r.SetId=s.SetId THEN 0 ELSE 1 END,r.ReqId) AS req " +
                    "LEFT JOIN dbo.Employee e ON e.EmpId=COALESCE(s.ReceivedById,req.ReceivedById,req.EmpId) " +
                    "LEFT JOIN dbo.Branch b ON b.BranchId=COALESCE(s.CurrentBranchId,e.BranchId,req.BranchId) " +
                    "LEFT JOIN dbo.Department d ON d.DeptId=COALESCE(s.CurrentDepartmentId,e.DeptId,req.DeptId) " +
                    "LEFT JOIN dbo.[User] u ON u.UserId=s.CreatedBy " +
                    "LEFT JOIN dbo.Company co ON co.ComId=COALESCE(s.ComId,e.ComId,req.ComId) " +
                    "WHERE "+whereClause,con)){
                    if(qrToken.HasValue) cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken.Value;
                    else                 cmd.Parameters.Add("@SetCode",SqlDbType.NVarChar,50).Value=setCode;
                    using(var r=cmd.ExecuteReader()){
                        if(!r.Read()){Err(c,404,"Set not found");return;}
                        resultFound=true;
                        sc=r["SetCode"].ToString();
                        empName=r["EmployeeName"]==DBNull.Value?(string)null:r["EmployeeName"].ToString();
                        dept=r["DepartmentName"]==DBNull.Value?(string)null:r["DepartmentName"].ToString();
                        branch=r["BranchName"]==DBNull.Value?(string)null:r["BranchName"].ToString();
                        status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString();
                        createdBy=r["CreatedByName"]==DBNull.Value?(string)null:r["CreatedByName"].ToString();
                        reqDate=r["DispatchDate"]==DBNull.Value?(string)null:Convert.ToDateTime(r["DispatchDate"]).ToString("yyyy-MM-dd");
                        createdDate=r["CreatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["CreatedAt"]).ToString("yyyy-MM-dd");
                        company=r["CompanyName"]==DBNull.Value?(string)null:r["CompanyName"].ToString();
                    }
                }
                if(!resultFound){Err(c,404,"Set not found");return;}

                var items=new List<object>();
                int rowNum=1;
                using(var cmd=new SqlCommand(
                    "SELECT i.ItemType AS item_type,ic.Name AS item_category,r.Quantity," +
                    "r.Description,i.ModelNumber AS model_number,i.SerialNumber AS serial_number," +
                    "r.Status AS item_status,s.ComputerName AS computer_name,s.IPAddress AS ip_address " +
                    "FROM dbo.[Set] s " +
                    "INNER JOIN dbo.Request r ON r.SetId=s.SetId " +
                    "INNER JOIN dbo.Item i ON i.ItemId=r.ItemId " +
                    "LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId=i.CategoryId " +
                    "WHERE "+whereClause+" ORDER BY r.ReqId",con)){
                    if(qrToken.HasValue) cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken.Value;
                    else                 cmd.Parameters.Add("@SetCode",SqlDbType.NVarChar,50).Value=setCode;
                    using(var r=cmd.ExecuteReader()){
                        while(r.Read()){
                            items.Add(new{
                                item_id=rowNum++,
                                item_type=r["item_type"]==DBNull.Value?(string)null:r["item_type"].ToString(),
                                item_category=r["item_category"]==DBNull.Value?(string)null:r["item_category"].ToString(),
                                quantity=r["Quantity"]==DBNull.Value?0:Convert.ToInt32(r["Quantity"]),
                                description=r["Description"]==DBNull.Value?(string)null:r["Description"].ToString(),
                                model_number=r["model_number"]==DBNull.Value?(string)null:r["model_number"].ToString(),
                                serial_number=r["serial_number"]==DBNull.Value?(string)null:r["serial_number"].ToString(),
                                item_status=r["item_status"]==DBNull.Value?(string)null:r["item_status"].ToString(),
                                computer_name=r["computer_name"]==DBNull.Value?(string)null:r["computer_name"].ToString(),
                                ip_address=r["ip_address"]==DBNull.Value?(string)null:r["ip_address"].ToString()
                            });
                        }
                    }
                }

                Ok(c,new{
                    success=true,
                    set=new{
                        set_code=sc,
                        employee_name=empName,
                        department=dept,
                        branch=branch,
                        status=status,
                        created_by=createdBy,
                        company=company,
                        logistics_provider=(string)null,
                        Request_date=reqDate,
                        created_date=createdDate,
                        image_count=0,
                        items=items
                    }
                });
            }
        } catch(Exception ex){Err(c,500,"Failed to load dispatch set: "+ex.Message);return;}
    }
    public bool IsReusable{get{return false;}}
}
