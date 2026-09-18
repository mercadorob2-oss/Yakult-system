<%@ WebHandler Language="C#" Class="ItemMovementHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class ItemMovementHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}

        var idStr=(c.Request.QueryString["itemId"]??"").Trim();
        int itemId;
        if(!int.TryParse(idStr,out itemId)||itemId<=0){Err(c,400,"itemId is required");return;}

        var entries=new List<object>();
        using(var con=new SqlConnection(Cs())){
            con.Open();
            using(var cmd=new SqlCommand(
                "SELECT TOP 50 " +
                "inv.InvId,inv.EntryType,inv.Quantity,inv.DatePosted,inv.Description AS InvDescription," +
                "u.Name AS PostedBy," +
                "e.Name AS EmployeeName," +
                "b.Name AS BranchName," +
                "d.Name AS DepartmentName," +
                "s.SetCode,s.SetId," +
                "r.Status AS ReqStatus " +
                "FROM dbo.Inventory inv " +
                "LEFT JOIN dbo.[User] u ON u.UserId=inv.PostedBy " +
                "LEFT JOIN dbo.Request r ON r.ReqId=inv.ReqId " +
                "LEFT JOIN dbo.Employee e ON e.EmpId=r.EmpId " +
                "LEFT JOIN dbo.Branch b ON b.BranchId=e.BranchId " +
                "LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId " +
                "LEFT JOIN dbo.[Set] s ON s.SetId=inv.SetId " +
                "WHERE inv.ItemId=@ItemId AND ISNULL(inv.Active,1)=1 " +
                "ORDER BY inv.DatePosted DESC",con)){
                cmd.Parameters.AddWithValue("@ItemId",itemId);
                using(var r=cmd.ExecuteReader()){
                    while(r.Read()){
                        entries.Add(new{
                            entryType=r["EntryType"]==DBNull.Value?(string)null:r["EntryType"].ToString(),
                            quantity=r["Quantity"]==DBNull.Value?0:Convert.ToInt32(r["Quantity"]),
                            datePosted=r["DatePosted"]==DBNull.Value?(string)null:Convert.ToDateTime(r["DatePosted"]).ToString("MMM dd, yyyy HH:mm"),
                            description=r["InvDescription"]==DBNull.Value?(string)null:r["InvDescription"].ToString(),
                            postedBy=r["PostedBy"]==DBNull.Value?(string)null:r["PostedBy"].ToString(),
                            employee=r["EmployeeName"]==DBNull.Value?(string)null:r["EmployeeName"].ToString(),
                            branch=r["BranchName"]==DBNull.Value?(string)null:r["BranchName"].ToString(),
                            department=r["DepartmentName"]==DBNull.Value?(string)null:r["DepartmentName"].ToString(),
                            setCode=r["SetCode"]==DBNull.Value?(string)null:r["SetCode"].ToString(),
                            reqStatus=r["ReqStatus"]==DBNull.Value?(string)null:r["ReqStatus"].ToString()
                        });
                    }
                }
            }
        }
        Ok(c,new{success=true,itemId,movement=entries});
    }
    public bool IsReusable{get{return false;}}
}
