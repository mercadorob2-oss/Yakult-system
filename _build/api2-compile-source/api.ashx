<%@ WebHandler Language="C#" Class="ApiRouterHandler" %>

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ApiRouterHandler : IHttpHandler
{
    static string Cs() { var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]; return cs == null ? "" : cs.ConnectionString; }
    static string Secret() { return ConfigurationManager.AppSettings["AuthTokenSecret"] ?? "DEV_YAKULT_SECRET_7326_ABC123XYZ789"; }
    static int ExpHrs() { int h; return int.TryParse(ConfigurationManager.AppSettings["AuthTokenExpiryHours"], out h) ? h : 12; }

    void J(HttpContext c, int s, object d) { c.Response.StatusCode = s; c.Response.TrySkipIisCustomErrors = true; c.Response.ContentType = "application/json"; c.Response.AddHeader("Access-Control-Allow-Origin", "*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Ok(HttpContext c, object d) { J(c, 200, d); }
    void Err(HttpContext c, int s, string m) { J(c, s, new { success = false, message = m }); }
    string Body(HttpContext c) { using (var r = new StreamReader(c.Request.InputStream, c.Request.ContentEncoding)) return r.ReadToEnd(); }

    bool Auth(HttpContext c) {
        var a = c.Request.Headers["Authorization"] ?? "";
        var t = a.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? a.Substring(7).Trim() : "";
        if (string.IsNullOrEmpty(t)) { Err(c, 401, "Unauthorized"); return false; }
        var p = DecodeJwt(t);
        if (p == null) { Err(c, 401, "Invalid or expired token"); return false; }
        c.Items["__jwt"] = p; return true;
    }
    JObject Me(HttpContext c) { return c.Items["__jwt"] as JObject; }
    static string TokString(JToken t) { return t == null ? null : t.ToString(); }
    static int TokInt(JToken t) { return t == null ? 0 : t.Value<int>(); }
    static object DbTokString(JToken t) { var s = TokString(t); return string.IsNullOrEmpty(s) ? (object)DBNull.Value : s; }
    static object DbTokInt(JToken t) { return t == null ? (object)DBNull.Value : t.Value<int>(); }
    static string DbString(object v) { return v == null || v == DBNull.Value ? null : v.ToString(); }
    static int JwtSub(JObject p) { return p == null || p["sub"] == null ? 0 : p["sub"].Value<int>(); }
    static string JwtUsername(JObject p) { return p == null || p["username"] == null ? "" : p["username"].ToString(); }

    // JWT
    static string B64E(byte[] d) { return Convert.ToBase64String(d).TrimEnd('=').Replace('+','-').Replace('/','_'); }
    static byte[] B64D(string s) { s=s.Replace('-','+').Replace('_','/'); switch(s.Length%4){case 2:s+="==";break;case 3:s+="=";break;} return Convert.FromBase64String(s); }
    string MakeJwt(int uid, string uname, string name, string email) {
        var h=B64E(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new{alg="HS256",typ="JWT"})));
        var exp=DateTime.UtcNow.AddHours(ExpHrs()).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var p=B64E(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new{sub=uid,username=uname,name,email,exp})));
        var si=h+"."+p; using(var m=new HMACSHA256(Encoding.UTF8.GetBytes(Secret()))){return si+"."+B64E(m.ComputeHash(Encoding.UTF8.GetBytes(si)));}
    }
    JObject DecodeJwt(string t) { try { var p=t.Split('.'); if(p.Length!=3)return null; var si=p[0]+"."+p[1];
        using(var m=new HMACSHA256(Encoding.UTF8.GetBytes(Secret()))){if(B64E(m.ComputeHash(Encoding.UTF8.GetBytes(si)))!=p[2])return null;}
        var j=JObject.Parse(Encoding.UTF8.GetString(B64D(p[1]))); var e=TokString(j["exp"]); return e!=null&&DateTime.Parse(e)<DateTime.UtcNow?null:j; } catch{return null;} }

    // Password
    static byte[] Salt() { var b=new byte[16]; using(var r=RandomNumberGenerator.Create())r.GetBytes(b); return b; }
    static byte[] Hash(string pw, byte[] sl) { using(var s=SHA256.Create()){var pb=Encoding.UTF8.GetBytes(pw);var d=new byte[pb.Length+sl.Length];Buffer.BlockCopy(pb,0,d,0,pb.Length);Buffer.BlockCopy(sl,0,d,pb.Length,sl.Length);return s.ComputeHash(d);} }
    static byte[] HashOldApi(string pw, byte[] sl) { using(var s=SHA256.Create()){var d=new byte[sl.Length+Encoding.UTF8.GetByteCount(pw)];Buffer.BlockCopy(sl,0,d,0,sl.Length);Encoding.UTF8.GetBytes(pw).CopyTo(d,sl.Length);return s.ComputeHash(s.ComputeHash(d));} }
    static bool Same(byte[] a, byte[] b) { if(a==null||b==null||a.Length!=b.Length)return false; var diff=0; for(int i=0;i<a.Length;i++)diff|=a[i]^b[i]; return diff==0; }
    static bool Verify(string pw, byte[] h, byte[] s, out bool rehash) { rehash=false; if(h==null||s==null)return false; if(Same(Hash(pw,s),h))return true; if(Same(HashOldApi(pw,s),h)){rehash=true;return true;} return false; }
    static bool Verify(string pw, byte[] h, byte[] s) { bool rehash; return Verify(pw,h,s,out rehash); }
    static void MigrateUserPassword(SqlConnection con, int userId, string pw) {
        var ns=Salt(); var nh=Hash(pw,ns);
        using(var cmd=new SqlCommand("UPDATE dbo.[User] SET PasswordHash=@H,PasswordSalt=@S,[Password]=NULL WHERE UserId=@Id",con)){
            cmd.Parameters.AddWithValue("@H",nh); cmd.Parameters.AddWithValue("@S",ns); cmd.Parameters.AddWithValue("@Id",userId); cmd.ExecuteNonQuery();
        }
    }

    public void ProcessRequest(HttpContext ctx) {
        var m=(ctx.Request.HttpMethod??"GET").ToUpper();
        var r=(ctx.Request.QueryString["__route"]??"").TrimStart('/');
        if(m=="OPTIONS"){ctx.Response.StatusCode=204;ctx.Response.AddHeader("Access-Control-Allow-Origin","*");ctx.Response.AddHeader("Access-Control-Allow-Headers","Content-Type, Authorization");ctx.Response.AddHeader("Access-Control-Allow-Methods","GET,POST,PUT,DELETE,OPTIONS");ctx.Response.End();return;}
        try {
            if(r.StartsWith("api/health",StringComparison.OrdinalIgnoreCase)) Health(ctx);
            else if(r.StartsWith("api/auth/login",StringComparison.OrdinalIgnoreCase)&&m=="POST") Login(ctx);
            else if(r.StartsWith("api/auth/register",StringComparison.OrdinalIgnoreCase)&&m=="POST") Register(ctx);
            else if(r.StartsWith("api/SetUpdates",StringComparison.OrdinalIgnoreCase)){if(!Auth(ctx))return;SetUpdates(ctx,m,r);}
            else if(m=="POST"&&r.EndsWith("api/Items/ReceiveSerialFromMobile",StringComparison.OrdinalIgnoreCase)) ItSerial(ctx);
            else if(r.StartsWith("api/Items",StringComparison.OrdinalIgnoreCase)){if(!Auth(ctx))return;Items(ctx,m,r);}
            else if(r.StartsWith("api/sets/by-token/",StringComparison.OrdinalIgnoreCase)){if(!Auth(ctx))return;SetByToken(ctx,r);}
            else if(r.StartsWith("api/dispatch",StringComparison.OrdinalIgnoreCase)){if(!Auth(ctx))return;Dispatch(ctx,m,r);}
            else if(r.StartsWith("api/Borrow",StringComparison.OrdinalIgnoreCase)){if(!Auth(ctx))return;Borrow(ctx,m,r);}
            else if(r.StartsWith("api/Reports",StringComparison.OrdinalIgnoreCase)) Reports(ctx,r);
            else Err(ctx,404,"Not found: "+r);
        } catch(Exception ex){Err(ctx,500,ex.Message);}
    }

    void Health(HttpContext c) {
        var db="Unknown"; try{using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT DB_NAME()",con))db=(string)cmd.ExecuteScalar();}}catch{}
        Ok(c,new{ok=true,utc=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),database=db});
    }

    void Login(HttpContext c) {
        var b=Body(c); JObject j; try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}
        var u=(j["username"]??"").ToString().Trim();
        var pw=(j["password"]??"").ToString().Trim();
        if(string.IsNullOrEmpty(u)||string.IsNullOrEmpty(pw)){Err(c,400,"Username and password required");return;}
        using(var con=new SqlConnection(Cs())){con.Open();
            int? uid=null; string nm=null,em=null; string dbg="no_user_found";

            int? userId=null; string userName=null,userEmail=null; bool hasLegacy=false; byte[] userHash=null,userSalt=null;
            using(var cmd=new SqlCommand(@"SELECT UserId,Name,EmailAddress,PasswordHash,PasswordSalt,CASE WHEN [Password] IS NOT NULL THEN 1 ELSE 0 END AS HasLegacy FROM dbo.[User] WHERE Name COLLATE Latin1_General_CS_AS=@U COLLATE Latin1_General_CS_AS AND ISNULL(IsActive,1)=1",con)){
                cmd.Parameters.AddWithValue("@U",u);
                using(var r=cmd.ExecuteReader())if(r.Read()){userId=Convert.ToInt32(r["UserId"]);userName=DbString(r["Name"]);userEmail=DbString(r["EmailAddress"]);userHash=r["PasswordHash"]as byte[];userSalt=r["PasswordSalt"]as byte[];hasLegacy=Convert.ToInt32(r["HasLegacy"])==1;}}
            if(userId!=null){
                int legacyMatch=0;
                if(hasLegacy){using(var cmd=new SqlCommand("SELECT COUNT(1) FROM dbo.[User] WHERE UserId=@Id AND [Password]=CONVERT(VARBINARY(MAX),@Pw)",con)){cmd.Parameters.AddWithValue("@Id",userId.Value);cmd.Parameters.AddWithValue("@Pw",pw);legacyMatch=Convert.ToInt32(cmd.ExecuteScalar());}}
                if(legacyMatch>0){MigrateUserPassword(con,userId.Value,pw);uid=userId;nm=userName;em=userEmail;}
                else{bool rehash=false;if(userHash!=null&&userSalt!=null&&Verify(pw,userHash,userSalt,out rehash)){uid=userId;nm=userName;em=userEmail;if(rehash)MigrateUserPassword(con,uid.Value,pw);}else{dbg="user_found_pw_mismatch";}}
            }

            if(uid==null){
                int? deptId=null,deptUserId=null; string deptName=null,deptEmail=null; byte[] deptUserHash=null,deptUserSalt=null,deptHash=null,deptSalt=null;
                using(var cmd=new SqlCommand(@"SELECT da.Id,u.UserId,u.Name,u.EmailAddress,u.PasswordHash,u.PasswordSalt,da.PasswordHash AS DaHash,da.PasswordSalt AS DaSalt FROM dbo.DepartmentAccount da INNER JOIN dbo.[User] u ON u.UserId=da.UserId WHERE da.Username COLLATE Latin1_General_CS_AS=@U COLLATE Latin1_General_CS_AS AND da.UserId IS NOT NULL AND ISNULL(da.IsActive,1)=1 AND ISNULL(u.IsActive,1)=1",con)){
                    cmd.Parameters.AddWithValue("@U",u);using(var r=cmd.ExecuteReader())if(r.Read()){deptId=Convert.ToInt32(r["Id"]);deptUserId=Convert.ToInt32(r["UserId"]);deptName=DbString(r["Name"]);deptEmail=DbString(r["EmailAddress"]);deptUserHash=r["PasswordHash"]as byte[];deptUserSalt=r["PasswordSalt"]as byte[];deptHash=r["DaHash"]as byte[];deptSalt=r["DaSalt"]as byte[];}}
                if(deptUserId!=null){
                    bool rehash=false;
                    bool ok=(deptUserHash!=null&&deptUserSalt!=null&&Verify(pw,deptUserHash,deptUserSalt,out rehash))||(deptHash!=null&&deptSalt!=null&&Verify(pw,deptHash,deptSalt,out rehash));
                    if(ok){uid=deptUserId;nm=deptName;em=deptEmail;if(rehash||deptUserHash==null||deptUserSalt==null){var ns=Salt();var nh=Hash(pw,ns);using(var up=new SqlCommand("UPDATE dbo.[User] SET PasswordHash=@H,PasswordSalt=@S WHERE UserId=@Id; UPDATE dbo.DepartmentAccount SET PasswordHash=@H,PasswordSalt=@S WHERE Id=@Did",con)){up.Parameters.AddWithValue("@H",nh);up.Parameters.AddWithValue("@S",ns);up.Parameters.AddWithValue("@Id",uid.Value);up.Parameters.AddWithValue("@Did",deptId.Value);up.ExecuteNonQuery();}}}
                    else{dbg="dept_found_pw_mismatch";}
                }
            }

            if(uid==null){
                int? oldDeptId=null; byte[] oldDeptHash=null,oldDeptSalt=null; bool oldDeptActive=false;
                using(var cmd=new SqlCommand("SELECT Id,PasswordHash,PasswordSalt,IsActive FROM dbo.DepartmentAccount WHERE Username COLLATE Latin1_General_CS_AS=@U COLLATE Latin1_General_CS_AS AND UserId IS NULL",con)){
                    cmd.Parameters.AddWithValue("@U",u);using(var r=cmd.ExecuteReader())if(r.Read()){oldDeptId=Convert.ToInt32(r["Id"]);oldDeptHash=r["PasswordHash"]as byte[];oldDeptSalt=r["PasswordSalt"]as byte[];oldDeptActive=r["IsActive"]!=DBNull.Value&&Convert.ToBoolean(r["IsActive"]);}}
                bool rehash=false;if(oldDeptId!=null&&oldDeptActive&&oldDeptHash!=null&&oldDeptSalt!=null&&Verify(pw,oldDeptHash,oldDeptSalt,out rehash)){
                    var ns=Salt();var nh=Hash(pw,ns);
                    using(var c2=new SqlCommand("INSERT INTO dbo.[User](Name,PasswordHash,PasswordSalt,IsActive,DateCreated) VALUES(@N,@H,@S,1,GETDATE());SELECT CAST(SCOPE_IDENTITY() AS INT)",con)){c2.Parameters.AddWithValue("@N",u);c2.Parameters.AddWithValue("@H",nh);c2.Parameters.AddWithValue("@S",ns);uid=(int)c2.ExecuteScalar();}
                    using(var c3=new SqlCommand("UPDATE dbo.DepartmentAccount SET UserId=@U,PasswordHash=@H,PasswordSalt=@S WHERE Id=@Id",con)){c3.Parameters.AddWithValue("@U",uid.Value);c3.Parameters.AddWithValue("@H",nh);c3.Parameters.AddWithValue("@S",ns);c3.Parameters.AddWithValue("@Id",oldDeptId.Value);c3.ExecuteNonQuery();}
                    nm=u;
                }else if(oldDeptId!=null){dbg=oldDeptActive?"legacy_dept_pw_mismatch":"legacy_dept_inactive";}
            }
            if(uid!=null){var tk=MakeJwt(uid.Value,u,nm??u,em??"");Ok(c,new{success=true,message="Login successful",token=tk,expiresUtc=DateTime.UtcNow.AddHours(ExpHrs()).ToString("yyyy-MM-ddTHH:mm:ssZ"),userId=uid.Value,name=nm??u,email=em??""});}
            else J(c,401,new{success=false,message="Invalid username or password",error="invalid_credentials",debug=dbg});
        }
    }
    void Register(HttpContext c) {
        var b=Body(c); JObject j; try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}
        var u=(j["username"]??"").ToString().Trim();
        var em=(j["email"]??"").ToString().Trim();
        var pw=(j["password"]??"").ToString().Trim();
        if(string.IsNullOrEmpty(u)||string.IsNullOrEmpty(pw)){Err(c,400,"Username and password required");return;}
        using(var con=new SqlConnection(Cs())){con.Open();
            using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.[User] WHERE Name=@N",con)){cmd.Parameters.AddWithValue("@N",u);if((int)cmd.ExecuteScalar()>0){Err(c,400,"Username already exists");return;}}
            var sl=Salt();var h=Hash(pw,sl);
            using(var cmd=new SqlCommand("INSERT INTO dbo.[User](Name,EmailAddress,PasswordHash,PasswordSalt,IsActive,DateCreated) VALUES(@N,@E,@H,@S,1,GETDATE());SELECT CAST(SCOPE_IDENTITY() AS INT)",con)){
                cmd.Parameters.AddWithValue("@N",u);cmd.Parameters.AddWithValue("@E",(object)em??DBNull.Value);cmd.Parameters.AddWithValue("@H",h);cmd.Parameters.AddWithValue("@S",sl);
                Ok(c,new{success=true,message="Registration successful",userId=(int)cmd.ExecuteScalar()});}
        }
    }

    void SetUpdates(HttpContext c,string m,string r) {
        if(m=="POST"&&r.EndsWith("/Upload",StringComparison.OrdinalIgnoreCase)) SuUpload(c);
        else if(m=="GET"&&r.EndsWith("/PendingCount",StringComparison.OrdinalIgnoreCase)){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE Processed=0",con))Ok(c,new{count=(int)cmd.ExecuteScalar()});}}
        else if(m=="GET"&&r.EndsWith("/Processed",StringComparison.OrdinalIgnoreCase)) SuList(c,true);
        else if(m=="GET"&&r.EndsWith("/Pending",StringComparison.OrdinalIgnoreCase)) SuList(c,false);
        else if(m=="GET") SuByCode(c);
        else Err(c,404,r);
    }
    void SuByCode(HttpContext c){var sc=c.Request.QueryString["setCode"]??"";
        using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT UpdateId,SetId,SetCode,ItemId,ItemType,SerialNumber,ModelNumber,PreviousStatus,NewStatus,Remark,UpdatedByUserId,UpdatedByName,Source,CreatedAt,Processed,ProcessedBy,ProcessedAt FROM dbo.SetItemUpdate WHERE SetCode=@C ORDER BY CreatedAt DESC",con)){cmd.Parameters.AddWithValue("@C",sc);var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{updateId=r["UpdateId"],setId=r["SetId"],setCode=r["SetCode"],itemId=r["ItemId"],itemType=r["ItemType"],serialNumber=r["SerialNumber"],modelNumber=r["ModelNumber"],previousStatus=r["PreviousStatus"],newStatus=r["NewStatus"],remark=r["Remark"],updatedByUserId=r["UpdatedByUserId"],updatedByName=r["UpdatedByName"],source=r["Source"],createdAt=r["CreatedAt"],processed=r["Processed"],processedBy=r["ProcessedBy"],processedAt=r["ProcessedAt"]});Ok(c,l);}}}
    void SuList(HttpContext c,bool proc){using(var con=new SqlConnection(Cs())){con.Open();var sql=proc?"SELECT UpdateId,SetId,SetCode,ItemId,ItemType,SerialNumber,ModelNumber,PreviousStatus,NewStatus,Remark,UpdatedByUserId,UpdatedByName,Source,CreatedAt,Processed,ProcessedBy,ProcessedAt FROM dbo.SetItemUpdate WHERE Processed=1 ORDER BY CreatedAt DESC":"SELECT UpdateId,SetId,SetCode,ItemId,ItemType,SerialNumber,ModelNumber,PreviousStatus,NewStatus,Remark,UpdatedByUserId,UpdatedByName,Source,CreatedAt,Processed,ProcessedBy,ProcessedAt FROM dbo.SetItemUpdate WHERE Processed=0 ORDER BY CreatedAt DESC";using(var cmd=new SqlCommand(sql,con)){var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{updateId=r["UpdateId"],setId=r["SetId"],setCode=r["SetCode"],itemId=r["ItemId"],itemType=r["ItemType"],serialNumber=r["SerialNumber"],modelNumber=r["ModelNumber"],previousStatus=r["PreviousStatus"],newStatus=r["NewStatus"],remark=r["Remark"],updatedByUserId=r["UpdatedByUserId"],updatedByName=r["UpdatedByName"],source=r["Source"],createdAt=r["CreatedAt"],processed=r["Processed"],processedBy=r["ProcessedBy"],processedAt=r["ProcessedAt"]});Ok(c,l);}}}
    void SuUpload(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var sc=(j["setCode"]??"").ToString().Trim();var items=j["items"]as Newtonsoft.Json.Linq.JArray;if(string.IsNullOrEmpty(sc)||items==null||items.Count==0){Err(c,400,"setCode and items required");return;}var p=Me(c);var uid=JwtSub(p);var cr=0;var fl=new List<string>();using(var con=new SqlConnection(Cs())){con.Open();foreach(var i in items){try{using(var cmd=new SqlCommand(@"INSERT INTO dbo.SetItemUpdate(SetId,SetCode,ItemId,ItemType,SerialNumber,ModelNumber,PreviousStatus,NewStatus,Remark,UpdatedByUserId,UpdatedByName,Source,CreatedAt,Processed) SELECT TOP 1 s.SetId,@SetCode,i.ItemId,i.ItemType,i.SerialNumber,i.ModelNumber,i.Status,@NewStatus,@Remark,@UserId,@UserName,'Mobile',GETDATE(),0 FROM dbo.[Set] s LEFT JOIN dbo.Item i ON i.SerialNumber=@Serial WHERE s.SetCode=@SetCode",con)){cmd.Parameters.AddWithValue("@SetCode",sc);cmd.Parameters.AddWithValue("@Serial",(i["serialNumber"]??"").ToString());cmd.Parameters.AddWithValue("@NewStatus",(i["newStatus"]??"Pending").ToString());cmd.Parameters.AddWithValue("@Remark",DbTokString(i["remark"]));cmd.Parameters.AddWithValue("@UserId",uid);cmd.Parameters.AddWithValue("@UserName",JwtUsername(p));cr+=cmd.ExecuteNonQuery();}}catch(Exception ex){fl.Add(ex.Message);}}Ok(c,new{Success=true,Message="Upload processed",CreatedCount=cr,FailedItems=fl});}}

    void Items(HttpContext c,string m,string r) {
        if(m=="POST"&&r.EndsWith("/CreateBatch",StringComparison.OrdinalIgnoreCase)) ItBatch(c);
        else if(m=="POST"&&r.EndsWith("/ReceiveSerialFromMobile",StringComparison.OrdinalIgnoreCase)) ItSerial(c);
        else if(m=="GET"&&r.EndsWith("/Categories",StringComparison.OrdinalIgnoreCase)) ItCats(c);
        else if(m=="GET"&&r.EndsWith("/Conditions",StringComparison.OrdinalIgnoreCase)) ItConds(c);
        else if(m=="GET"&&r.EndsWith("/Vendors",StringComparison.OrdinalIgnoreCase)) ItVendors(c);
        else if(m=="GET"&&r.EndsWith("/SerialLookup",StringComparison.OrdinalIgnoreCase)) ItSerialLookup(c);
        else Err(c,404,r);
    }
    void ItBatch(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var items=j["items"]as Newtonsoft.Json.Linq.JArray;if(items==null||items.Count==0){Err(c,400,"items required");return;}var p=Me(c);var uid=JwtSub(p);var cr=0;var fl=new List<string>();using(var con=new SqlConnection(Cs())){con.Open();foreach(var i in items){try{using(var cmd=new SqlCommand("INSERT INTO dbo.Item(SerialNumber,Name,Description,ModelNumber,CategoryId,ItemType,Status,Active,DateCreated,CreatedBy,CellPhoneNumber,IMEI1,IMEI2) VALUES(@S,@N,@D,@M,@C,@T,@ST,1,GETDATE(),@U,@CP,@I1,@I2)",con)){cmd.Parameters.AddWithValue("@S",(i["serialNumber"]??"").ToString());cmd.Parameters.AddWithValue("@N",(i["name"]??"").ToString());cmd.Parameters.AddWithValue("@D",DbTokString(i["description"]));cmd.Parameters.AddWithValue("@M",DbTokString(i["modelNumber"]));cmd.Parameters.AddWithValue("@C",DbTokInt(i["categoryId"]));cmd.Parameters.AddWithValue("@T",DbTokString(i["itemType"]));cmd.Parameters.AddWithValue("@ST",(object)(TokString(i["status"]) ?? "Available"));cmd.Parameters.AddWithValue("@U",uid);cmd.Parameters.AddWithValue("@CP",DbTokString(i["cellPhoneNumber"]));cmd.Parameters.AddWithValue("@I1",DbTokString(i["imei1"]));cmd.Parameters.AddWithValue("@I2",DbTokString(i["imei2"]));cr+=cmd.ExecuteNonQuery();}}catch(Exception ex){fl.Add(ex.Message);}}Ok(c,new{Success=true,Message="Batch processed",CreatedCount=cr,FailedItems=fl});}}
    void ItSerial(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var sn=(j["serialNumber"]??"").ToString().Trim();if(string.IsNullOrEmpty(sn)){Err(c,400,"serialNumber required");return;}try{var ad=System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data");var f=System.IO.Path.Combine(ad??System.IO.Path.GetTempPath(),"mobile-serials");if(!System.IO.Directory.Exists(f))System.IO.Directory.CreateDirectory(f);var qp=System.IO.Path.Combine(f,"mobile-serials.json");JArray l;lock(this){if(System.IO.File.Exists(qp)){try{l=JArray.Parse(System.IO.File.ReadAllText(qp));}catch{l=new JArray();}}else l=new JArray();var item=new JObject();item["serialNumber"]=sn;var cp=(j["cellPhoneNumber"]??"").ToString().Trim();var i1=(j["imei1"]??"").ToString().Trim();var i2=(j["imei2"]??"").ToString().Trim();if(!string.IsNullOrEmpty(cp))item["cellPhoneNumber"]=cp;if(!string.IsNullOrEmpty(i1))item["imei1"]=i1;if(!string.IsNullOrEmpty(i2))item["imei2"]=i2;l.Add(item);System.IO.File.WriteAllText(qp,JsonConvert.SerializeObject(l,Formatting.Indented),Encoding.UTF8);}Ok(c,new{success=true,message="Serial received",serialNumber=sn});}catch(Exception ex){Err(c,500,"Failed: "+ex.Message);}}
    void ItCats(HttpContext c){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT CategoryId,Name,Active FROM dbo.ItemCategory WHERE Active=1 ORDER BY Name",con)){var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{categoryId=r["CategoryId"],name=r["Name"],active=r["Active"]});Ok(c,l);}}}
    void ItConds(HttpContext c){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT ConditionId,ConditionName,Active FROM dbo.[Condition] WHERE Active=1 ORDER BY ConditionName",con)){var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{conditionId=r["ConditionId"],conditionName=r["ConditionName"],active=r["Active"]});Ok(c,l);}}}
    void ItVendors(HttpContext c){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT VendorId,VendorName,Active FROM dbo.Vendor WHERE Active=1 ORDER BY VendorName",con)){var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{vendorId=r["VendorId"],vendorName=r["VendorName"],active=r["Active"]});Ok(c,l);}}}

    void ItSerialLookup(HttpContext c){var identifier=(c.Request.QueryString["serial"]??"").Trim();if(string.IsNullOrEmpty(identifier))identifier=(c.Request.QueryString["imei"]??"").Trim();if(string.IsNullOrEmpty(identifier))identifier=(c.Request.QueryString["identifier"]??"").Trim();if(string.IsNullOrEmpty(identifier)){Err(c,400,"serial or imei is required");return;}using(var con=new SqlConnection(Cs())){con.Open();object item=null;var sets=new List<object>();using(var cmd=new SqlCommand("WITH MatchedItem AS (SELECT TOP 1 i.* FROM dbo.Item i WHERE i.SerialNumber=@Identifier OR i.IMEI1=@Identifier OR i.IMEI2=@Identifier ORDER BY CASE WHEN i.SerialNumber=@Identifier THEN 0 WHEN i.IMEI1=@Identifier THEN 1 ELSE 2 END, i.ItemId DESC) SELECT i.ItemId,i.Name,i.Description,i.ModelNumber,i.ItemType,i.SerialNumber,i.CellPhoneNumber,i.IMEI1,i.IMEI2,i.Active,ic.Name AS Category,cn.ConditionName,s.SetId,s.SetCode,s.Status,s.DispatchDate,s.Remarks,s.Site,s.QRToken,b.Name AS CurrentBranch,d.Name AS CurrentDepartment FROM MatchedItem i LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId=i.CategoryId LEFT JOIN dbo.[Condition] cn ON cn.ConditionID=i.ConditionID LEFT JOIN dbo.SetItem si ON si.ItemId=i.ItemId LEFT JOIN dbo.[Set] s ON s.SetId=si.SetId LEFT JOIN dbo.Branch b ON b.BranchId=s.CurrentBranchId LEFT JOIN dbo.Department d ON d.DeptId=s.CurrentDepartmentId ORDER BY s.SetId DESC",con)){cmd.Parameters.AddWithValue("@Identifier",identifier);using(var r=cmd.ExecuteReader()){while(r.Read()){if(item==null)item=new{itemId=Convert.ToInt32(r["ItemId"]),serialNumber=r["SerialNumber"]==DBNull.Value?(string)null:r["SerialNumber"].ToString(),cellPhoneNumber=r["CellPhoneNumber"]==DBNull.Value?(string)null:r["CellPhoneNumber"].ToString(),imei1=r["IMEI1"]==DBNull.Value?(string)null:r["IMEI1"].ToString(),imei2=r["IMEI2"]==DBNull.Value?(string)null:r["IMEI2"].ToString(),name=r["Name"].ToString(),description=r["Description"]==DBNull.Value?(string)null:r["Description"].ToString(),modelNumber=r["ModelNumber"]==DBNull.Value?(string)null:r["ModelNumber"].ToString(),itemType=r["ItemType"]==DBNull.Value?(string)null:r["ItemType"].ToString(),category=r["Category"]==DBNull.Value?(string)null:r["Category"].ToString(),condition=r["ConditionName"]==DBNull.Value?(string)null:r["ConditionName"].ToString(),active=r["Active"]!=DBNull.Value&&(bool)r["Active"]};if(r["SetId"]!=DBNull.Value){var dd=r["DispatchDate"];sets.Add(new{setId=Convert.ToInt32(r["SetId"]),setCode=r["SetCode"].ToString(),status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString(),dispatchDate=dd==DBNull.Value?(string)null:Convert.ToDateTime(dd).ToString("MMM dd, yyyy"),remarks=r["Remarks"]==DBNull.Value?(string)null:r["Remarks"].ToString(),site=r["Site"]==DBNull.Value?(string)null:r["Site"].ToString(),qrToken=r["QRToken"]==DBNull.Value?(string)null:r["QRToken"].ToString(),currentBranch=r["CurrentBranch"]==DBNull.Value?(string)null:r["CurrentBranch"].ToString(),currentDepartment=r["CurrentDepartment"]==DBNull.Value?(string)null:r["CurrentDepartment"].ToString()});}}Ok(c,new{found=item!=null,lookup=identifier,item,isInSet=sets.Count>0,sets});}}}}
    void SetByToken(HttpContext c,string r){var tk=r.Substring(r.IndexOf("by-token/")+9).Trim();using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT SetId,SetCode,QRToken,Status,DispatchDate,Remarks,CreatedBy,CreatedAt FROM dbo.[Set] WHERE QRToken=@T",con)){cmd.Parameters.AddWithValue("@T",tk);using(var rd=cmd.ExecuteReader())if(rd.Read())Ok(c,new{setId=rd["SetId"],setCode=rd["SetCode"],qrToken=rd["QRToken"],status=rd["Status"],dispatchDate=rd["DispatchDate"],remarks=rd["Remarks"],createdBy=rd["CreatedBy"],createdAt=rd["CreatedAt"]});else Err(c,404,"Set not found");}}}

    void Dispatch(HttpContext c,string m,string r){
        if(m=="PUT"&&!r.Contains("resolve-token")&&!r.EndsWith("/status",StringComparison.OrdinalIgnoreCase)){var ps=r.Split('/');int sid;if(!int.TryParse(ps.Length>2?ps[2]:"",out sid)){Err(c,400,"Invalid setId");return;}using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("UPDATE dbo.[Set] SET Status='Dispatched',DispatchDate=GETDATE() WHERE SetId=@Id",con)){cmd.Parameters.AddWithValue("@Id",sid);var n=cmd.ExecuteNonQuery();if(n>0)Ok(c,new{success=true,message="Set deployed"});else Err(c,404,"Set not found");}}}
        else if(m=="GET"&&r.Contains("resolve-token/")){var tk=r.Substring(r.IndexOf("resolve-token/")+14).Trim();using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT SetId FROM dbo.[Set] WHERE QRToken=@T",con)){cmd.Parameters.AddWithValue("@T",tk);var v=cmd.ExecuteScalar();if(v!=null)Ok(c,new{setId=Convert.ToInt32(v)});else Err(c,404,"Token not found");}}}
        else if(m=="GET"&&r.EndsWith("/status",StringComparison.OrdinalIgnoreCase)){var codes=c.Request.QueryString["setCodes"]??"";using(var con=new SqlConnection(Cs())){con.Open();var l=new List<object>();if(!string.IsNullOrEmpty(codes)){var ca=codes.Split(',');var pn=new List<string>();for(int i=0;i<ca.Length&&i<50;i++)pn.Add("@c"+i);using(var cmd=new SqlCommand("SELECT SetCode,Status FROM dbo.[Set] WHERE SetCode IN("+string.Join(",",pn)+")",con)){for(int i=0;i<ca.Length&&i<50;i++)cmd.Parameters.AddWithValue("@c"+i,ca[i].Trim());using(var rd=cmd.ExecuteReader())while(rd.Read())l.Add(new{set_code=rd["SetCode"],status=rd["Status"]});}}Ok(c,l);}}
        else if(m=="GET"&&r.EndsWith("/Count",StringComparison.OrdinalIgnoreCase)) DiCount(c);
        else Err(c,404,r);
    }
    void DiCount(HttpContext c){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.[Set] WHERE Status='Dispatched' AND CAST(DispatchDate AS DATE)=CAST(GETDATE() AS DATE)",con))Ok(c,new{todayCount=(int)cmd.ExecuteScalar()});}}

    void Borrow(HttpContext c,string m,string r) {
        if(m=="GET"&&r.EndsWith("/Resolve",StringComparison.OrdinalIgnoreCase)) BoResolve(c);
        else if(m=="GET"&&r.EndsWith("/Employees",StringComparison.OrdinalIgnoreCase)) BoEmps(c);
        else if(m=="POST"&&r.EndsWith("/Employees",StringComparison.OrdinalIgnoreCase)) BoEmpCreate(c);
        else if(m=="GET"&&r.EndsWith("/Companies",StringComparison.OrdinalIgnoreCase)) BoComps(c);
        else if(m=="GET"&&r.EndsWith("/Branches",StringComparison.OrdinalIgnoreCase)) BoBranches(c);
        else if(m=="GET"&&r.EndsWith("/Departments",StringComparison.OrdinalIgnoreCase)) BoDepts(c);
        else if(m=="POST"&&r.Equals("api/Borrow",StringComparison.OrdinalIgnoreCase)) BoCreate(c);
        else if(m=="POST"&&r.EndsWith("/Return",StringComparison.OrdinalIgnoreCase)) BoReturn(c);
        else if(m=="POST"&&r.EndsWith("/Delete",StringComparison.OrdinalIgnoreCase)) BoDelete(c);
        else if(m=="GET"&&r.EndsWith("/Open",StringComparison.OrdinalIgnoreCase)) BoOpen(c);
        else if(m=="GET"&&r.EndsWith("/Home",StringComparison.OrdinalIgnoreCase)) BoHome(c);
        else if(m=="GET"&&r.EndsWith("/History",StringComparison.OrdinalIgnoreCase)) BoHistory(c);
        else Err(c,404,r);
    }
    void BoResolve(HttpContext c){var sn=c.Request.QueryString["serial"]??"";using(var con=new SqlConnection(Cs())){con.Open();object item=null;using(var cmd=new SqlCommand("SELECT TOP 1 ItemId,SerialNumber,Name,Description,ModelNumber FROM dbo.Item WHERE SerialNumber=@S",con)){cmd.Parameters.AddWithValue("@S",sn);using(var r=cmd.ExecuteReader())if(r.Read())item=new{itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["Name"],itemDescription=r["Description"],modelNumber=r["ModelNumber"]};}object ob=null;using(var cmd=new SqlCommand("SELECT TOP 1 BorrowId,ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc,ReturnedAtUtc FROM dbo.BorrowLog WHERE SerialNumber=@S AND ReturnedAtUtc IS NULL ORDER BY BorrowedAtUtc DESC",con)){cmd.Parameters.AddWithValue("@S",sn);using(var r=cmd.ExecuteReader())if(r.Read())ob=new{borrowId=r["BorrowId"],itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["ItemName"],itemDescription=r["ItemDescription"],modelNumber=r["ModelNumber"],borrowedByEmpId=r["BorrowedByEmpId"],borrowedByEmpName=r["BorrowedByEmpName"],borrowedByDeptId=r["BorrowedByDeptId"],borrowedByDeptName=r["BorrowedByDeptName"],borrowEncodedByUserId=r["BorrowEncodedByUserId"],borrowEncodedByUserName=r["BorrowEncodedByUserName"],borrowedAtUtc=r["BorrowedAtUtc"],isOpen=r["ReturnedAtUtc"]==DBNull.Value};}Ok(c,new{item,openBorrow=ob});}}
    void BoEmps(HttpContext c){var q=c.Request.QueryString["q"]??"";var ci=c.Request.QueryString["companyId"];var di=c.Request.QueryString["departmentId"];using(var con=new SqlConnection(Cs())){con.Open();var sql="SELECT TOP 500 e.EmpId,e.Name AS EmployeeName,ISNULL(e.ComId,0)AS ComId,c.Name AS CompanyName,e.DeptId,d.Name AS DepartmentName FROM dbo.Employee e LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId LEFT JOIN dbo.Company c ON c.ComId=e.ComId WHERE e.Active=1";if(!string.IsNullOrEmpty(q))sql+=" AND e.Name LIKE @Q";if(!string.IsNullOrEmpty(ci))sql+=" AND e.ComId=@CI";if(!string.IsNullOrEmpty(di))sql+=" AND e.DeptId=@DI";sql+=" ORDER BY e.Name";using(var cmd=new SqlCommand(sql,con)){if(!string.IsNullOrEmpty(q))cmd.Parameters.AddWithValue("@Q","%"+q+"%");if(!string.IsNullOrEmpty(ci))cmd.Parameters.AddWithValue("@CI",int.Parse(ci));if(!string.IsNullOrEmpty(di))cmd.Parameters.AddWithValue("@DI",int.Parse(di));var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{empId=r["EmpId"],employeeName=r["EmployeeName"],comId=r["ComId"],companyName=r["CompanyName"],deptId=r["DeptId"],departmentName=r["DepartmentName"]});Ok(c,l);}}}
    void BoEmpCreate(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var nm=(j["employeeName"]??"").ToString().Trim();var ci=TokInt(j["comId"]);var bi=TokInt(j["branchId"]);var di=TokInt(j["deptId"]);if(string.IsNullOrEmpty(nm)){Err(c,400,"employeeName required");return;}using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("INSERT INTO dbo.Employee(Name,ComId,BranchId,DeptId,Active) VALUES(@N,@C,@B,@D,1);SELECT CAST(SCOPE_IDENTITY() AS INT)",con)){cmd.Parameters.AddWithValue("@N",nm);cmd.Parameters.AddWithValue("@C",ci);cmd.Parameters.AddWithValue("@B",bi);cmd.Parameters.AddWithValue("@D",di);Ok(c,new{empId=(int)cmd.ExecuteScalar(),employeeName=nm,comId=ci,companyName=(string)null,branchId=bi,branchName=(string)null,deptId=di,departmentName=(string)null});}}}
    void BoComps(HttpContext c){using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT ComId,Name AS CompanyName FROM dbo.Company WHERE ISNULL(Active,1)=1 ORDER BY Name",con)){var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{comId=r["ComId"],companyName=r["CompanyName"]});Ok(c,l);}}}
    void BoBranches(HttpContext c){var ci=c.Request.QueryString["companyId"]??"0";using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT BranchId,ComId,Name AS BranchName FROM dbo.Branch WHERE ComId=@C ORDER BY Name",con)){cmd.Parameters.AddWithValue("@C",int.Parse(ci));var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{branchId=r["BranchId"],comId=r["ComId"],branchName=r["BranchName"]});Ok(c,l);}}}
    void BoDepts(HttpContext c){var ci=c.Request.QueryString["companyId"]??"0";using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("SELECT DeptId,ComId,Name AS DepartmentName FROM dbo.Department WHERE ComId=@C ORDER BY Name",con)){cmd.Parameters.AddWithValue("@C",int.Parse(ci));var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{deptId=r["DeptId"],comId=r["ComId"],departmentName=r["DepartmentName"]});Ok(c,l);}}}
    void BoCreate(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var sn=(j["serialNumber"]??"").ToString().Trim();var ei=TokInt(j["borrowedByEmpId"]);if(string.IsNullOrEmpty(sn)||ei==0){Err(c,400,"serialNumber and borrowedByEmpId required");return;}var p=Me(c);var uid=JwtSub(p);using(var con=new SqlConnection(Cs())){con.Open();string iname=null,idesc=null,imodel=null,ename=null,dname=null;int?deptId=null,itemId=null;using(var cmd=new SqlCommand("SELECT ItemId,Name,Description,ModelNumber FROM dbo.Item WHERE SerialNumber=@S",con)){cmd.Parameters.AddWithValue("@S",sn);using(var r=cmd.ExecuteReader())if(r.Read()){itemId=r["ItemId"]as int?;iname=DbString(r["Name"]);idesc=DbString(r["Description"]);imodel=DbString(r["ModelNumber"]);}}using(var cmd=new SqlCommand("SELECT Name,DeptId FROM dbo.Employee WHERE EmpId=@E",con)){cmd.Parameters.AddWithValue("@E",ei);using(var r=cmd.ExecuteReader())if(r.Read()){ename=DbString(r["Name"]);deptId=r["DeptId"]as int?;}}if(deptId.HasValue)using(var cmd=new SqlCommand("SELECT Name FROM dbo.Department WHERE DeptId=@D",con)){cmd.Parameters.AddWithValue("@D",deptId.Value);dname=DbString(cmd.ExecuteScalar());}using(var cmd=new SqlCommand(@"INSERT INTO dbo.BorrowLog(ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc) VALUES(@II,@S,@IN,@ID,@MN,@EI,@EN,@DI,@DN,@UI,@UN,GETUTCDATE());SELECT CAST(SCOPE_IDENTITY() AS INT)",con)){cmd.Parameters.AddWithValue("@II",(object)itemId??DBNull.Value);cmd.Parameters.AddWithValue("@S",sn);cmd.Parameters.AddWithValue("@IN",(object)iname??DBNull.Value);cmd.Parameters.AddWithValue("@ID",(object)idesc??DBNull.Value);cmd.Parameters.AddWithValue("@MN",(object)imodel??DBNull.Value);cmd.Parameters.AddWithValue("@EI",ei);cmd.Parameters.AddWithValue("@EN",(object)ename??DBNull.Value);cmd.Parameters.AddWithValue("@DI",(object)deptId??DBNull.Value);cmd.Parameters.AddWithValue("@DN",(object)dname??DBNull.Value);cmd.Parameters.AddWithValue("@UI",uid);cmd.Parameters.AddWithValue("@UN",JwtUsername(p));var bid=(int)cmd.ExecuteScalar();Ok(c,new{success=true,message="Borrow created",borrow=new{borrowId=bid,itemId,serialNumber=sn,itemName=iname,itemDescription=idesc,modelNumber=imodel,borrowedByEmpId=ei,borrowedByEmpName=ename,borrowedByDeptId=deptId,borrowedByDeptName=dname,borrowEncodedByUserId=uid,borrowEncodedByUserName=JwtUsername(p),borrowedAtUtc=DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),isOpen=true}});}}}
    void BoReturn(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var bid=TokInt(j["borrowId"]);var ei=TokInt(j["returnedByEmpId"]);if(bid==0){Err(c,400,"borrowId required");return;}var p=Me(c);var uid=JwtSub(p);using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("UPDATE dbo.BorrowLog SET ReturnedByEmpId=@EI,ReturnEncodedByUserId=@UI,ReturnEncodedByUserName=@UN,ReturnedAtUtc=GETUTCDATE() WHERE BorrowId=@B AND ReturnedAtUtc IS NULL",con)){cmd.Parameters.AddWithValue("@EI",(object)ei??DBNull.Value);cmd.Parameters.AddWithValue("@UI",uid);cmd.Parameters.AddWithValue("@UN",JwtUsername(p));cmd.Parameters.AddWithValue("@B",bid);var n=cmd.ExecuteNonQuery();Ok(c,new{success=n>0,message=n>0?"Borrow returned":"Not found or already returned"});}}}
    void BoDelete(HttpContext c){var b=Body(c);JObject j;try{j=JObject.Parse(b);}catch{Err(c,400,"Invalid JSON");return;}var bid=TokInt(j["borrowId"]);if(bid==0){Err(c,400,"borrowId required");return;}using(var con=new SqlConnection(Cs())){con.Open();using(var cmd=new SqlCommand("DELETE FROM dbo.BorrowLog WHERE BorrowId=@B",con)){cmd.Parameters.AddWithValue("@B",bid);var n=cmd.ExecuteNonQuery();Ok(c,new{success=n>0,message=n>0?"Deleted":"Not found"});}}}
    void BoOpen(HttpContext c){var q=c.Request.QueryString["serialContains"]??"";var pi=int.Parse(c.Request.QueryString["pageIndex"]??"0");var ps=int.Parse(c.Request.QueryString["pageSize"]??"25");using(var con=new SqlConnection(Cs())){con.Open();var sql="SELECT BorrowId,ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL";if(!string.IsNullOrEmpty(q))sql+=" AND(SerialNumber LIKE @Q OR ItemName LIKE @Q OR BorrowedByEmpName LIKE @Q)";sql+=" ORDER BY BorrowedAtUtc DESC OFFSET @O ROWS FETCH NEXT @P ROWS ONLY";using(var cmd=new SqlCommand(sql,con)){if(!string.IsNullOrEmpty(q))cmd.Parameters.AddWithValue("@Q","%"+q+"%");cmd.Parameters.AddWithValue("@O",pi*ps);cmd.Parameters.AddWithValue("@P",ps);var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{borrowId=r["BorrowId"],itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["ItemName"],itemDescription=r["ItemDescription"],modelNumber=r["ModelNumber"],borrowedByEmpId=r["BorrowedByEmpId"],borrowedByEmpName=r["BorrowedByEmpName"],borrowedByDeptId=r["BorrowedByDeptId"],borrowedByDeptName=r["BorrowedByDeptName"],borrowEncodedByUserId=r["BorrowEncodedByUserId"],borrowEncodedByUserName=r["BorrowEncodedByUserName"],borrowedAtUtc=r["BorrowedAtUtc"],isOpen=true});var cs2="SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL";if(!string.IsNullOrEmpty(q))cs2+=" AND(SerialNumber LIKE @Q OR ItemName LIKE @Q OR BorrowedByEmpName LIKE @Q)";using(var c2=new SqlCommand(cs2,con)){if(!string.IsNullOrEmpty(q))c2.Parameters.AddWithValue("@Q","%"+q+"%");Ok(c,new{totalCount=(int)c2.ExecuteScalar(),rows=l,access=new{canDeleteOpenBorrow=false,canExportCsv=false}});}}}}
    void BoHome(HttpContext c){var rc=int.Parse(c.Request.QueryString["recentCount"]??"5");using(var con=new SqlConnection(Cs())){con.Open();int oc=0,odc=0,rt=0;using(var cmd=new SqlCommand("SELECT COUNT(*)FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL",con))oc=(int)cmd.ExecuteScalar();using(var cmd=new SqlCommand("SELECT COUNT(*)FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL AND BorrowedAtUtc<DATEADD(day,-7,GETUTCDATE())",con))odc=(int)cmd.ExecuteScalar();using(var cmd=new SqlCommand("SELECT COUNT(*)FROM dbo.BorrowLog WHERE ReturnedAtUtc>=CAST(GETUTCDATE()AS DATE)",con))rt=(int)cmd.ExecuteScalar();var recent=new List<object>();using(var cmd=new SqlCommand("SELECT TOP(@N) BorrowId,ItemId,SerialNumber,ItemName,BorrowedByEmpName,BorrowedAtUtc,ReturnedAtUtc FROM dbo.BorrowLog ORDER BY BorrowedAtUtc DESC",con)){cmd.Parameters.AddWithValue("@N",rc);using(var r=cmd.ExecuteReader())while(r.Read())recent.Add(new{borrowId=r["BorrowId"],itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["ItemName"],borrowedByEmpName=r["BorrowedByEmpName"],borrowedAtUtc=r["BorrowedAtUtc"],returnedAtUtc=r["ReturnedAtUtc"],isOpen=r["ReturnedAtUtc"]==DBNull.Value});}Ok(c,new{openCount=oc,overdueCount=odc,returnedTodayCount=rt,recentRows=recent,access=new{canDeleteOpenBorrow=false,canExportCsv=false}});}}
    void BoHistory(HttpContext c){var q=c.Request.QueryString["serialContains"]??"";var pi=int.Parse(c.Request.QueryString["pageIndex"]??"0");var ps=int.Parse(c.Request.QueryString["pageSize"]??"25");using(var con=new SqlConnection(Cs())){con.Open();var sql="SELECT BorrowId,ItemId,SerialNumber,ItemName,ItemDescription,ModelNumber,BorrowedByEmpId,BorrowedByEmpName,BorrowedByDeptId,BorrowedByDeptName,BorrowEncodedByUserId,BorrowEncodedByUserName,BorrowedAtUtc,ReturnedByEmpId,ReturnedByEmpName,ReturnedAtUtc FROM dbo.BorrowLog WHERE 1=1";if(!string.IsNullOrEmpty(q))sql+=" AND(SerialNumber LIKE @Q OR ItemName LIKE @Q OR BorrowedByEmpName LIKE @Q)";sql+=" ORDER BY BorrowedAtUtc DESC OFFSET @O ROWS FETCH NEXT @P ROWS ONLY";using(var cmd=new SqlCommand(sql,con)){if(!string.IsNullOrEmpty(q))cmd.Parameters.AddWithValue("@Q","%"+q+"%");cmd.Parameters.AddWithValue("@O",pi*ps);cmd.Parameters.AddWithValue("@P",ps);var l=new List<object>();using(var r=cmd.ExecuteReader())while(r.Read())l.Add(new{borrowId=r["BorrowId"],itemId=r["ItemId"],serialNumber=r["SerialNumber"],itemName=r["ItemName"],itemDescription=r["ItemDescription"],modelNumber=r["ModelNumber"],borrowedByEmpId=r["BorrowedByEmpId"],borrowedByEmpName=r["BorrowedByEmpName"],borrowedByDeptId=r["BorrowedByDeptId"],borrowedByDeptName=r["BorrowedByDeptName"],borrowEncodedByUserId=r["BorrowEncodedByUserId"],borrowEncodedByUserName=r["BorrowEncodedByUserName"],borrowedAtUtc=r["BorrowedAtUtc"],returnedByEmpId=r["ReturnedByEmpId"],returnedByEmpName=r["ReturnedByEmpName"],returnedAtUtc=r["ReturnedAtUtc"],isOpen=r["ReturnedAtUtc"]==DBNull.Value});var cs2="SELECT COUNT(*)FROM dbo.BorrowLog WHERE 1=1";if(!string.IsNullOrEmpty(q))cs2+=" AND(SerialNumber LIKE @Q OR ItemName LIKE @Q OR BorrowedByEmpName LIKE @Q)";using(var c2=new SqlCommand(cs2,con)){if(!string.IsNullOrEmpty(q))c2.Parameters.AddWithValue("@Q","%"+q+"%");Ok(c,new{totalCount=(int)c2.ExecuteScalar(),rows=l,access=new{canDeleteOpenBorrow=false,canExportCsv=false}});}}}}

    void Reports(HttpContext c,string r) {
        if(r.EndsWith("/Summary",StringComparison.OrdinalIgnoreCase)) ReportsSummary(c);
        else if(r.Contains("/Module/")) ReportsModule(c, r.Substring(r.LastIndexOf('/') + 1));
        else Err(c,404,r);
    }

    string ReportRangeKey(HttpContext c) {
        var range=(c.Request.QueryString["range"]??"7d").Trim().ToLowerInvariant();
        if(range=="today"||range=="30d")return range;
        return "7d";
    }

    int ReportRangeDays(HttpContext c) {
        var range=ReportRangeKey(c);
        if(range=="today")return 1;
        if(range=="30d")return 30;
        return 7;
    }

    string ReportRangeLabel(HttpContext c) {
        var range=ReportRangeKey(c);
        if(range=="today")return "today";
        if(range=="30d")return "the last 30 days";
        return "the last 7 days";
    }

    DateTime ReportRangeStart(HttpContext c) {
        var days=ReportRangeDays(c);
        var today=DateTime.UtcNow.Date;
        return days<=1 ? today : today.AddDays(-(days-1));
    }

    object ReportKpi(string title,string value,string delta,string tone) { return new{title=title,value=value,delta=delta,tone=tone}; }
    object ReportRow(string title,string subtitle,string value,string tone) { return new{title=title,subtitle=subtitle,value=value,tone=tone}; }
    object ReportTrendPoint(string label,int value) { return new{label=label,value=value}; }

    string ReadText(SqlDataReader rd,string name) {
        var v=rd[name];
        return v==null||v==DBNull.Value ? "" : v.ToString();
    }

    int ScalarInt(SqlConnection con,string sql,DateTime start) {
        using(var cmd=new SqlCommand(sql,con)){
            cmd.Parameters.AddWithValue("@Start",start);
            var v=cmd.ExecuteScalar();
            return v==null||v==DBNull.Value ? 0 : Convert.ToInt32(v);
        }
    }

    int ScalarIntBetween(SqlConnection con,string sql,DateTime from,DateTime to) {
        using(var cmd=new SqlCommand(sql,con)){
            cmd.Parameters.AddWithValue("@From",from);
            cmd.Parameters.AddWithValue("@To",to);
            var v=cmd.ExecuteScalar();
            return v==null||v==DBNull.Value ? 0 : Convert.ToInt32(v);
        }
    }

    List<object> ReportRows(SqlConnection con,string sql,DateTime start,string emptyTitle,string emptySubtitle,string emptyValue,string emptyTone) {
        var rows=new List<object>();
        using(var cmd=new SqlCommand(sql,con)){
            cmd.Parameters.AddWithValue("@Start",start);
            using(var rd=cmd.ExecuteReader()){
                while(rd.Read())rows.Add(ReportRow(ReadText(rd,"Title"),ReadText(rd,"Subtitle"),ReadText(rd,"Value"),ReadText(rd,"Tone")));
            }
        }
        if(rows.Count==0)rows.Add(ReportRow(emptyTitle,emptySubtitle,emptyValue,emptyTone));
        return rows;
    }

    List<object> DispatchTrend(SqlConnection con,HttpContext c,DateTime start) {
        var points=new List<object>();
        var days=ReportRangeDays(c);
        if(days<=1){
            var day=DateTime.UtcNow.Date;
            for(var i=0;i<6;i++){
                var from=day.AddHours(i*4);
                var to=from.AddHours(4);
                var count=ScalarIntBetween(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(DispatchDate,CreatedAt)>=@From AND ISNULL(DispatchDate,CreatedAt)<@To",from,to);
                points.Add(ReportTrendPoint(from.ToString("HH")+":00",count));
            }
            return points;
        }
        var bucketCount=days>=30 ? 6 : 7;
        var bucketSize=(int)Math.Ceiling(days/(double)bucketCount);
        var cursor=start.Date;
        var limit=DateTime.UtcNow.Date.AddDays(1);
        for(var i=0;i<bucketCount&&cursor<limit;i++){
            var from=cursor;
            var to=cursor.AddDays(bucketSize);
            if(to>limit)to=limit;
            var count=ScalarIntBetween(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(DispatchDate,CreatedAt)>=@From AND ISNULL(DispatchDate,CreatedAt)<@To",from,to);
            points.Add(ReportTrendPoint(from.ToString("MMM d"),count));
            cursor=to;
        }
        return points;
    }

    List<object> MixedTimelineRows(SqlConnection con,DateTime start) {
        return ReportRows(con,@"SELECT TOP 5 Title,Subtitle,[Value],Tone FROM (
SELECT 'Set '+ISNULL(SetCode,'') AS Title,'Status: '+ISNULL(Status,'No status') AS Subtitle,CONVERT(varchar(16),ISNULL(DispatchDate,CreatedAt),120) AS [Value],'#7A2C24' AS Tone,ISNULL(DispatchDate,CreatedAt) AS EventAt FROM dbo.[Set] WHERE ISNULL(Active,1)=1
UNION ALL
SELECT 'Update '+ISNULL(SetCode,'') AS Title,ISNULL(NewStatus,'Item update') AS Subtitle,CONVERT(varchar(16),CreatedAt,120) AS [Value],'#A63F2A' AS Tone,CreatedAt AS EventAt FROM dbo.SetItemUpdate
UNION ALL
SELECT 'Borrow '+ISNULL(SerialNumber,'') AS Title,'Borrowed by '+ISNULL(BorrowedByEmpName,'Unknown') AS Subtitle,CONVERT(varchar(16),BorrowedAtUtc,120) AS [Value],'#265D73' AS Tone,BorrowedAtUtc AS EventAt FROM dbo.BorrowLog
) x WHERE EventAt>=@Start ORDER BY EventAt DESC",start,"No recent activity","No report timeline rows for this range.","--","#7A2C24");
    }

    void ReportsSummary(HttpContext c) {
        var start=ReportRangeStart(c);
        var range=ReportRangeKey(c);
        var label=ReportRangeLabel(c);
        using(var con=new SqlConnection(Cs())){con.Open();
            var activeItems=ScalarInt(con,"SELECT COUNT(*) FROM dbo.Item WHERE ISNULL(Active,1)=1",start);
            var rangeItems=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItem si INNER JOIN dbo.[Set] s ON s.SetId=si.SetId WHERE ISNULL(s.Active,1)=1 AND ISNULL(s.DispatchDate,s.CreatedAt)>=@Start",start);
            var totalSets=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1",start);
            var rangeSets=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(DispatchDate,CreatedAt)>=@Start",start);
            var dispatchedSets=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(Status,'')='Dispatched' AND ISNULL(DispatchDate,CreatedAt)>=@Start",start);
            var openIssues=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE Processed=0",start);
            var newIssues=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start",start);
            var activeBorrows=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL",start);
            var borrowed=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE BorrowedAtUtc>=@Start",start);
            var returned=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc>=@Start",start);
            var encoderActions=newIssues+borrowed+returned+dispatchedSets;

            var kpis=new List<object>{
                ReportKpi("Dispatched Sets",dispatchedSets.ToString(),rangeSets+" sets active in "+label,"#7A2C24"),
                ReportKpi("Open Issues",openIssues.ToString(),newIssues+" updates in "+label,"#A63F2A"),
                ReportKpi("Active Borrows",activeBorrows.ToString(),borrowed+" borrowed in "+label,"#265D73"),
                ReportKpi("Encoder Activity",encoderActions.ToString(),"Recorded mobile/report actions in "+label,"#466B3C")
            };
            var highlights=new List<string>{
                rangeSets+" sets and "+rangeItems+" set items are represented in "+label+".",
                openIssues+" open item updates need review.",
                activeBorrows+" borrows are currently active and "+returned+" were returned in "+label+"."
            };
            var healthSignals=new List<object>{
                ReportRow("Inventory coverage",activeItems+" active inventory records",totalSets+" active sets","#2E7D32"),
                ReportRow("Dispatch throughput",dispatchedSets+" dispatched sets in "+label,rangeSets.ToString(),"#7A2C24"),
                ReportRow("Issue queue",newIssues+" new updates in "+label,openIssues.ToString(),"#A63F2A"),
                ReportRow("Borrow workload",borrowed+" borrow actions in "+label,activeBorrows.ToString(),"#265D73")
            };
            var issueRows=ReportRows(con,"SELECT TOP 4 ISNULL(NULLIF(NewStatus,''),'Unspecified') AS Title,'Item update status' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#A63F2A' AS Tone FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start GROUP BY ISNULL(NULLIF(NewStatus,''),'Unspecified') ORDER BY COUNT(1) DESC",start,"No item updates","No issue rows for this range.","0","#A63F2A");
            var borrowRows=ReportRows(con,"SELECT TOP 4 ISNULL(NULLIF(BorrowedByEmpName,''),'Unknown employee') AS Title,'Open borrow holder' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#265D73' AS Tone FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL GROUP BY ISNULL(NULLIF(BorrowedByEmpName,''),'Unknown employee') ORDER BY COUNT(1) DESC",start,"No active borrows","No one has an open borrow right now.","0","#265D73");
            var activityRows=ReportRows(con,"SELECT TOP 4 ISNULL(NULLIF(UpdatedByName,''),'Unknown encoder') AS Title,'Mobile item updates' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#466B3C' AS Tone FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start GROUP BY ISNULL(NULLIF(UpdatedByName,''),'Unknown encoder') ORDER BY COUNT(1) DESC",start,"No encoder activity","No item update activity for this range.","0","#466B3C");
            var actionRows=new List<object>{
                ReportRow("Pending item updates","Unprocessed mobile updates",openIssues.ToString(),"#A63F2A"),
                ReportRow("Returned borrows","Items returned in "+label,returned.ToString(),"#265D73"),
                ReportRow("Dispatched sets","Sets dispatched in "+label,dispatchedSets.ToString(),"#7A2C24")
            };
            Ok(c,new{range=range,kpis=kpis,executiveSummary="Reports are loaded from live inventory, dispatch, borrow, and mobile update records for "+label+".",highlights=highlights,healthSignals=healthSignals,dispatchTrend=DispatchTrend(con,c,start),issueRows=issueRows,borrowRows=borrowRows,activityRows=activityRows,actionRows=actionRows,timelineRows=MixedTimelineRows(con,start)});
        }
    }

    void ReportsModule(HttpContext c,string moduleId) {
        var module=(moduleId??"").Trim().ToLowerInvariant();
        var start=ReportRangeStart(c);
        var label=ReportRangeLabel(c);
        using(var con=new SqlConnection(Cs())){con.Open();
            if(module=="dispatch"){
                var rangeSets=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(DispatchDate,CreatedAt)>=@Start",start);
                var dispatched=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(Status,'')='Dispatched' AND ISNULL(DispatchDate,CreatedAt)>=@Start",start);
                var pending=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(Status,'')<>'Dispatched'",start);
                var keyRows=new List<object>{ReportRow("Sets in range","Created or dispatched in "+label,rangeSets.ToString(),"#7A2C24"),ReportRow("Dispatched","Completed dispatches in "+label,dispatched.ToString(),"#2E7D32"),ReportRow("Pending","Sets not yet dispatched",pending.ToString(),"#A63F2A")};
                var leaders=ReportRows(con,"SELECT TOP 5 ISNULL(NULLIF(Status,''),'No status') AS Title,'Set status' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#7A2C24' AS Tone FROM dbo.[Set] WHERE ISNULL(Active,1)=1 GROUP BY ISNULL(NULLIF(Status,''),'No status') ORDER BY COUNT(1) DESC",start,"No set statuses","No dispatch status rows available.","0","#7A2C24");
                var timeline=ReportRows(con,"SELECT TOP 5 'Set '+ISNULL(SetCode,'') AS Title,'Status: '+ISNULL(Status,'No status') AS Subtitle,CONVERT(varchar(16),ISNULL(DispatchDate,CreatedAt),120) AS [Value],'#7A2C24' AS Tone FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(DispatchDate,CreatedAt)>=@Start ORDER BY ISNULL(DispatchDate,CreatedAt) DESC",start,"No dispatch timeline","No sets found for this range.","--","#7A2C24");
                Ok(c,new{moduleId=module,title="Dispatch Performance",summary="Set creation and dispatch movement for "+label+".",heroLabel="Dispatched sets",heroValue=dispatched.ToString(),heroTone="#7A2C24",keyRows=keyRows,leaderboardRows=leaders,timelineRows=timeline});return;
            }
            if(module=="issues"){
                var openIssues=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE Processed=0",start);
                var newIssues=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start",start);
                var processed=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE Processed=1 AND CreatedAt>=@Start",start);
                var keyRows=new List<object>{ReportRow("Open issues","Unprocessed item updates",openIssues.ToString(),"#A63F2A"),ReportRow("New updates",label,newIssues.ToString(),"#A63F2A"),ReportRow("Processed",label,processed.ToString(),"#2E7D32")};
                var leaders=ReportRows(con,"SELECT TOP 5 ISNULL(NULLIF(NewStatus,''),'Unspecified') AS Title,'Item update status' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#A63F2A' AS Tone FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start GROUP BY ISNULL(NULLIF(NewStatus,''),'Unspecified') ORDER BY COUNT(1) DESC",start,"No issue statuses","No issue status rows for this range.","0","#A63F2A");
                var timeline=ReportRows(con,"SELECT TOP 5 'Update '+ISNULL(SetCode,'') AS Title,ISNULL(NewStatus,'Item update') AS Subtitle,CONVERT(varchar(16),CreatedAt,120) AS [Value],'#A63F2A' AS Tone FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start ORDER BY CreatedAt DESC",start,"No issue timeline","No item updates found for this range.","--","#A63F2A");
                Ok(c,new{moduleId=module,title="Issue Monitoring",summary="Pending and processed item update activity for "+label+".",heroLabel="Open issues",heroValue=openIssues.ToString(),heroTone="#A63F2A",keyRows=keyRows,leaderboardRows=leaders,timelineRows=timeline});return;
            }
            if(module=="borrow"){
                var active=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL",start);
                var borrowed=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE BorrowedAtUtc>=@Start",start);
                var returned=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc>=@Start",start);
                var overdue=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL AND BorrowedAtUtc<DATEADD(day,-7,GETUTCDATE())",start);
                var keyRows=new List<object>{ReportRow("Active borrows","Currently open",active.ToString(),"#265D73"),ReportRow("Borrowed",label,borrowed.ToString(),"#265D73"),ReportRow("Returned",label,returned.ToString(),"#2E7D32"),ReportRow("Over 7 days","Open longer than one week",overdue.ToString(),"#A63F2A")};
                var leaders=ReportRows(con,"SELECT TOP 5 ISNULL(NULLIF(BorrowedByEmpName,''),'Unknown employee') AS Title,'Open borrow holder' AS Subtitle,CONVERT(varchar(20),COUNT(1)) AS [Value],'#265D73' AS Tone FROM dbo.BorrowLog WHERE ReturnedAtUtc IS NULL GROUP BY ISNULL(NULLIF(BorrowedByEmpName,''),'Unknown employee') ORDER BY COUNT(1) DESC",start,"No open borrows","No active borrow leaderboard rows.","0","#265D73");
                var timeline=ReportRows(con,"SELECT TOP 5 'Borrow '+ISNULL(SerialNumber,'') AS Title,'Borrowed by '+ISNULL(BorrowedByEmpName,'Unknown') AS Subtitle,CONVERT(varchar(16),BorrowedAtUtc,120) AS [Value],'#265D73' AS Tone FROM dbo.BorrowLog WHERE BorrowedAtUtc>=@Start ORDER BY BorrowedAtUtc DESC",start,"No borrow timeline","No borrow activity found for this range.","--","#265D73");
                Ok(c,new{moduleId=module,title="Borrow Insights",summary="Borrow workload, returns, and overdue handovers for "+label+".",heroLabel="Active borrows",heroValue=active.ToString(),heroTone="#265D73",keyRows=keyRows,leaderboardRows=leaders,timelineRows=timeline});return;
            }
            if(module=="activity"){
                var updates=ScalarInt(con,"SELECT COUNT(*) FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start",start);
                var borrowed=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE BorrowedAtUtc>=@Start",start);
                var returned=ScalarInt(con,"SELECT COUNT(*) FROM dbo.BorrowLog WHERE ReturnedAtUtc>=@Start",start);
                var dispatched=ScalarInt(con,"SELECT COUNT(*) FROM dbo.[Set] WHERE ISNULL(Active,1)=1 AND ISNULL(Status,'')='Dispatched' AND ISNULL(DispatchDate,CreatedAt)>=@Start",start);
                var total=updates+borrowed+returned+dispatched;
                var keyRows=new List<object>{ReportRow("Total activity",label,total.ToString(),"#466B3C"),ReportRow("Item updates",label,updates.ToString(),"#A63F2A"),ReportRow("Borrow actions",label,(borrowed+returned).ToString(),"#265D73"),ReportRow("Dispatch actions",label,dispatched.ToString(),"#7A2C24")};
                var leaders=ReportRows(con,@"SELECT TOP 5 Person AS Title,'Recorded actions' AS Subtitle,CONVERT(varchar(20),SUM(ActionCount)) AS [Value],'#466B3C' AS Tone FROM (
SELECT ISNULL(NULLIF(UpdatedByName,''),'Unknown encoder') AS Person,COUNT(1) AS ActionCount FROM dbo.SetItemUpdate WHERE CreatedAt>=@Start GROUP BY ISNULL(NULLIF(UpdatedByName,''),'Unknown encoder')
UNION ALL
SELECT ISNULL(NULLIF(BorrowEncodedByUserName,''),'Unknown encoder') AS Person,COUNT(1) AS ActionCount FROM dbo.BorrowLog WHERE BorrowedAtUtc>=@Start GROUP BY ISNULL(NULLIF(BorrowEncodedByUserName,''),'Unknown encoder')
) x GROUP BY Person ORDER BY SUM(ActionCount) DESC",start,"No user activity","No encoder activity for this range.","0","#466B3C");
                Ok(c,new{moduleId=module,title="User Activity",summary="Mobile update, borrow, return, and dispatch activity for "+label+".",heroLabel="Recorded actions",heroValue=total.ToString(),heroTone="#466B3C",keyRows=keyRows,leaderboardRows=leaders,timelineRows=MixedTimelineRows(con,start)});return;
            }
        }
        Err(c,404,"Unknown report module: "+moduleId);
    }

    public bool IsReusable { get { return false; } }
}

