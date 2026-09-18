<%@ WebHandler Language="C#" Class="SetItemsDetailHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class SetItemsDetailHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="GET"){Err(c,405,"Method not allowed");return;}

        var token=(c.Request.QueryString["token"]??"").Trim();
        if(string.IsNullOrEmpty(token)){Err(c,400,"token is required");return;}

        const string prefix="yakult:set:v1:";
        if(token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            token=token.Substring(prefix.Length).Trim();

        Guid qrToken;
        if(!Guid.TryParse(token,out qrToken)){Err(c,400,"Invalid token format");return;}

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();

                string setCode=null;
                string remarks=null;
                DateTime? endDate=null;

                using(var cmd=new SqlCommand(
                    "SELECT s.SetCode,s.Remarks,s.EndDate FROM dbo.[Set] s WHERE s.QRToken=@Token",con)){
                    cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken;
                    using(var r=cmd.ExecuteReader()){
                        if(!r.Read()){Err(c,404,"Set not found");return;}
                        setCode=r["SetCode"].ToString();
                        remarks=r["Remarks"]==DBNull.Value?(string)null:r["Remarks"].ToString();
                        endDate=r["EndDate"]==DBNull.Value?(DateTime?)null:Convert.ToDateTime(r["EndDate"]);
                    }
                }

                var items=new List<object>();
                int rowNum=1;
                using(var cmd=new SqlCommand(
                    "SELECT i.ItemId,i.ItemType AS item_type,ic.Name AS item_category,r.Quantity," +
                    "r.Description,i.ModelNumber AS model_number,i.SerialNumber AS serial_number," +
                    "r.Status AS item_status,s.ComputerName AS computer_name,s.IPAddress AS ip_address," +
                    "con.ConditionName AS item_condition," +
                    "ISNULL(rph.RepairCount,0) AS repair_count,rph.LastRepairAction AS last_repair_action " +
                    "FROM dbo.[Set] s " +
                    "INNER JOIN dbo.Request r ON r.SetId=s.SetId " +
                    "INNER JOIN dbo.Item i ON i.ItemId=r.ItemId " +
                    "LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId=i.CategoryId " +
                    "LEFT JOIN dbo.[Condition] con ON con.ConditionID=i.ConditionID " +
                    "OUTER APPLY (" +
                    "  SELECT COUNT(*) AS RepairCount," +
                    "  (SELECT TOP 1 h2.RepairAction FROM dbo.ItemRepairHistory h2 WHERE h2.ItemId=i.ItemId ORDER BY h2.CreatedAt DESC) AS LastRepairAction " +
                    "  FROM dbo.ItemRepairHistory h WHERE h.ItemId=i.ItemId" +
                    ") rph " +
                    "WHERE s.QRToken=@Token ORDER BY r.ReqId",con)){
                    cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken;
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
                                ip_address=r["ip_address"]==DBNull.Value?(string)null:r["ip_address"].ToString(),
                                item_condition=r["item_condition"]==DBNull.Value?(string)null:r["item_condition"].ToString(),
                                repair_count=r["repair_count"]==DBNull.Value?0:Convert.ToInt32(r["repair_count"]),
                                last_repair_action=r["last_repair_action"]==DBNull.Value?(string)null:r["last_repair_action"].ToString()
                            });
                        }
                    }
                }

                Ok(c,new{
                    success=true,
                    set_code=setCode,
                    metadata=new{
                        item_count=items.Count,
                        priority="Normal",
                        delivery_notes=remarks,
                        expected_delivery_utc=endDate.HasValue?endDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"):(string)null
                    },
                    items=items
                });
            }
        } catch(Exception ex){Err(c,500,"Failed to load set items: "+ex.Message);}
    }
    public bool IsReusable{get{return false;}}
}
