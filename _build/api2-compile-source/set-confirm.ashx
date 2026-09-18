<%@ WebHandler Language="C#" Class="SetConfirmHandler" %>
using System;
using System.IO;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Newtonsoft.Json;

public class SetConfirmHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    class ConfirmBody {
        public string signature_base64;
        public string photo_base64;
        public string gps_coordinates;
        public string device_id;
        public string confirmed_by;
        public string notes;
    }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Method not allowed");return;}

        var token=(c.Request.QueryString["token"]??"").Trim();
        if(string.IsNullOrEmpty(token)){Err(c,400,"token is required");return;}

        const string prefix="yakult:set:v1:";
        if(token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            token=token.Substring(prefix.Length).Trim();

        Guid qrToken;
        if(!Guid.TryParse(token,out qrToken)){Err(c,400,"Invalid token format");return;}

        ConfirmBody body=null;
        try {
            string raw;
            using(var sr=new StreamReader(c.Request.InputStream)) raw=sr.ReadToEnd();
            if(!string.IsNullOrWhiteSpace(raw)) body=JsonConvert.DeserializeObject<ConfirmBody>(raw);
        } catch{}
        if(body==null) body=new ConfirmBody();

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();

                string setCode=null;
                int setId=0;
                using(var cmd=new SqlCommand(
                    "SELECT SetId,SetCode FROM dbo.[Set] WHERE QRToken=@Token",con)){
                    cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken;
                    using(var r=cmd.ExecuteReader()){
                        if(!r.Read()){Err(c,404,"Set not found");return;}
                        setId=Convert.ToInt32(r["SetId"]);
                        setCode=r["SetCode"].ToString();
                    }
                }

                var sql="UPDATE dbo.[Set] SET Status='Received'" +
                    (string.IsNullOrWhiteSpace(body.notes) ? "" : ",Remarks=ISNULL(Remarks,'')+@RemarksAppend") +
                    " WHERE SetId=@SetId";
                using(var cmd=new SqlCommand(sql,con)){
                    cmd.Parameters.Add("@SetId",SqlDbType.Int).Value=setId;
                    if(!string.IsNullOrWhiteSpace(body.notes))
                        cmd.Parameters.Add("@RemarksAppend",SqlDbType.NVarChar,500).Value=" | Confirmed: "+body.notes;
                    cmd.ExecuteNonQuery();
                }

                Ok(c,new{
                    success=true,
                    message="Delivery confirmed successfully.",
                    confirmed_at=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    confirmation_id=setCode
                });
            }
        } catch(Exception ex){Err(c,500,"Confirmation failed: "+ex.Message);}
    }
    public bool IsReusable{get{return false;}}
}
