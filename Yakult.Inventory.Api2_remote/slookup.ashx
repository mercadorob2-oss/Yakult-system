<%@ WebHandler Language="C#" Class="SerialLookupHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using Newtonsoft.Json;

public class SerialLookupHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode = 200; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode = s; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    static string DateOnly(object value) {
        if (value == null || value == DBNull.Value) return null;
        return Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    static string WarrantyStatus(object startValue, object endValue) {
        if (startValue == null || startValue == DBNull.Value || endValue == null || endValue == DBNull.Value)
            return "notEncoded";

        var startDate = Convert.ToDateTime(startValue, CultureInfo.InvariantCulture).Date;
        var endDate = Convert.ToDateTime(endValue, CultureInfo.InvariantCulture).Date;
        if (endDate < startDate) return "notEncoded";

        // Windows' Singapore Standard Time is the Philippine (UTC+08:00) time zone.
        var philippines = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, philippines).Date;
        if (endDate < today) return "expired";
        if (endDate <= today.AddDays(30)) return "expiringSoon";
        return "good";
    }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="GET"){Err(c,405,"Method not allowed");return;}

        var serial=(c.Request.QueryString["serial"]??"").Trim();
        if(string.IsNullOrEmpty(serial)){Err(c,400,"serial is required");return;}

        using(var con=new SqlConnection(Cs())){
            con.Open();
            object item=null;
            var sets=new List<object>();
            using(var cmd=new SqlCommand(
                "WITH MatchedItem AS (" +
                "SELECT TOP 1 i.* FROM dbo.Item i " +
                "WHERE i.SerialNumber=@Serial " +
                "ORDER BY i.ItemId DESC" +
                ") " +
                "SELECT i.ItemId,i.Name,i.Description,i.ModelNumber,i.ItemType,i.SerialNumber,i.CellPhoneNumber,i.IMEI1,i.IMEI2,i.Active," +
                "i.WarrantyStartDate,i.WarrantyEndDate," +
                "ic.Name AS Category,cn.ConditionName," +
                "s.SetId,s.SetCode,s.Status,s.DispatchDate,s.Remarks,s.Site,s.QRToken," +
                "b.Name AS CurrentBranch,d.Name AS CurrentDepartment " +
                "FROM MatchedItem i " +
                "LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId=i.CategoryId " +
                "LEFT JOIN dbo.[Condition] cn ON cn.ConditionID=i.ConditionID " +
                "LEFT JOIN dbo.SetItem si ON si.ItemId=i.ItemId " +
                "LEFT JOIN dbo.[Set] s ON s.SetId=si.SetId " +
                "LEFT JOIN dbo.Branch b ON b.BranchId=s.CurrentBranchId " +
                "LEFT JOIN dbo.Department d ON d.DeptId=s.CurrentDepartmentId " +
                "ORDER BY s.SetId DESC",con)){
                cmd.Parameters.AddWithValue("@Serial",serial);
                using(var r=cmd.ExecuteReader()){
                    while(r.Read()){
                        if(item==null)item=new{
                            itemId=Convert.ToInt32(r["ItemId"]),
                            serialNumber=r["SerialNumber"]==DBNull.Value?(string)null:r["SerialNumber"].ToString(),
                            cellPhoneNumber=r["CellPhoneNumber"]==DBNull.Value?(string)null:r["CellPhoneNumber"].ToString(),
                            imei1=r["IMEI1"]==DBNull.Value?(string)null:r["IMEI1"].ToString(),
                            imei2=r["IMEI2"]==DBNull.Value?(string)null:r["IMEI2"].ToString(),
                            name=r["Name"]==DBNull.Value?(string)null:r["Name"].ToString(),
                            description=r["Description"]==DBNull.Value?(string)null:r["Description"].ToString(),
                            modelNumber=r["ModelNumber"]==DBNull.Value?(string)null:r["ModelNumber"].ToString(),
                            itemType=r["ItemType"]==DBNull.Value?(string)null:r["ItemType"].ToString(),
                            category=r["Category"]==DBNull.Value?(string)null:r["Category"].ToString(),
                            condition=r["ConditionName"]==DBNull.Value?(string)null:r["ConditionName"].ToString(),
                            warrantyStartDate=DateOnly(r["WarrantyStartDate"]),
                            warrantyEndDate=DateOnly(r["WarrantyEndDate"]),
                            warrantyStatus=WarrantyStatus(r["WarrantyStartDate"],r["WarrantyEndDate"]),
                            active=r["Active"]!=DBNull.Value&&(bool)r["Active"]
                        };
                        if(r["SetId"]!=DBNull.Value){
                            var dd=r["DispatchDate"];
                            sets.Add(new{
                                setId=Convert.ToInt32(r["SetId"]),
                                setCode=r["SetCode"]==DBNull.Value?(string)null:r["SetCode"].ToString(),
                                status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString(),
                                dispatchDate=dd==DBNull.Value?(string)null:Convert.ToDateTime(dd).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture),
                                remarks=r["Remarks"]==DBNull.Value?(string)null:r["Remarks"].ToString(),
                                site=r["Site"]==DBNull.Value?(string)null:r["Site"].ToString(),
                                qrToken=r["QRToken"]==DBNull.Value?(string)null:r["QRToken"].ToString(),
                                currentBranch=r["CurrentBranch"]==DBNull.Value?(string)null:r["CurrentBranch"].ToString(),
                                currentDepartment=r["CurrentDepartment"]==DBNull.Value?(string)null:r["CurrentDepartment"].ToString()
                            });
                        }
                    }
                }
            }
            Ok(c,new{found=item!=null,lookup=serial,item,isInSet=sets.Count>0,sets});
        }
    }
    public bool IsReusable{get{return false;}}
}
