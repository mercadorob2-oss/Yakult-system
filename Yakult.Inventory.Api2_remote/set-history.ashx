<%@ WebHandler Language="C#" Class="SetItemHistoryHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class SetItemHistoryHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}

        var token=(c.Request.QueryString["token"]??"").Trim();
        if(string.IsNullOrEmpty(token)){Err(c,400,"token is required");return;}

        const string prefix="yakult:set:v1:";
        if(token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            token=token.Substring(prefix.Length).Trim();

        Guid qrToken;
        if(!Guid.TryParse(token,out qrToken)){Err(c,400,"Not a valid dispatch QR code.");return;}

        var history=new List<object>();
        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                using(var cmd=new SqlCommand(
                    "SELECT TOP 50 su.CreatedAt AS DeployedAt,su.UpdatedByName AS DeployedBy," +
                    "su.PreviousStatus,su.NewStatus,ISNULL(su.Source,'Scanner') AS DeploymentLocation,su.Remark " +
                    "FROM dbo.[Set] s " +
                    "INNER JOIN dbo.SetItemUpdate su ON su.SetId=s.SetId " +
                    "WHERE s.QRToken=@Token ORDER BY su.CreatedAt DESC",con)){
                    cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken;
                    using(var r=cmd.ExecuteReader()){
                        while(r.Read()){
                            history.Add(new{
                                deployed_at=r["DeployedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["DeployedAt"]).ToString("yyyy-MM-dd HH:mm"),
                                deployed_by=r["DeployedBy"]==DBNull.Value?"Unknown":r["DeployedBy"].ToString(),
                                previous_status=r["PreviousStatus"]==DBNull.Value?"Pending":r["PreviousStatus"].ToString(),
                                new_status=r["NewStatus"]==DBNull.Value?"Deployed":r["NewStatus"].ToString(),
                                deployment_location=r["DeploymentLocation"]==DBNull.Value?"Scanner":r["DeploymentLocation"].ToString(),
                                device_id=r["Remark"]==DBNull.Value?(string)null:r["Remark"].ToString()
                            });
                        }
                    }
                }
            }
        } catch(Exception){Err(c,500,"Failed to load deployment history.");return;}
        Ok(c,new{success=true,deployment_history=history});
    }
    public bool IsReusable{get{return false;}}
}
