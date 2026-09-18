using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using Newtonsoft.Json;

public class CallTicketActionHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="POST"){Err(c,405,"Method not allowed");return;}

        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        var body=JsonConvert.DeserializeObject<Dictionary<string,object>>(new System.IO.StreamReader(c.Request.InputStream).ReadToEnd());
        if(body==null||!body.ContainsKey("action")){Err(c,400,"action is required");return;}
        var action=body["action"].ToString().ToLowerInvariant();

        int ticketId; if(!body.ContainsKey("ticketId")||!int.TryParse(body["ticketId"].ToString(),out ticketId)||ticketId<=0){Err(c,400,"ticketId is required");return;}
        int? userId=actor.UserId;

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                string ticketSource, currentTicketStatus; int? assignedToEmpId;
                if(!TryReadTicketWorkflowState(con,ticketId,out ticketSource,out currentTicketStatus,out assignedToEmpId)){
                    Err(c,404,"Ticket not found");return;
                }
                if(action=="status"){
                    string newStatus=body.ContainsKey("newStatus")?body["newStatus"].ToString():"";
                    string note=body.ContainsKey("note")?body["note"].ToString():"";
                    if(string.IsNullOrWhiteSpace(newStatus)){Err(c,400,"newStatus is required");return;}
                    if(!IsStatusTransitionAllowed(currentTicketStatus,newStatus)){
                        Err(c,409,"This workflow blocks moving the ticket backward. Use Reopened for final tickets.");return;
                    }
                    if(IsPortalWorkStart(ticketSource,newStatus) && !assignedToEmpId.HasValue){
                        Err(c,409,"Portal tickets must be assigned to an IT employee before work starts or resolution.");return;
                    }
                    string oldStatus=currentTicketStatus;
                    using(var qCmd=new SqlCommand("SELECT TOP 1 Status FROM dbo.CallTicket WHERE TicketId=@Id",con)){
                        qCmd.Parameters.Add("@Id",SqlDbType.Int).Value=ticketId;
                        object sv=qCmd.ExecuteScalar();
                        if(sv!=null&&sv!=DBNull.Value) oldStatus=sv.ToString();
                    }
                    using(var cmd=new SqlCommand("dbo.sp_Call_SetTicketStatus",con)){
                        cmd.CommandType=CommandType.StoredProcedure;
                        cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                        cmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=newStatus;
                        cmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                        if(HasParam(con,"dbo.sp_Call_SetTicketStatus","@Note"))
                            cmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=note;
                        cmd.ExecuteNonQuery();
                    }
                    Ok(c,new{success=true,message="Status updated"});
                    ItcmEmailHelper.TrySendStatusUpdateEmailAsync(Cs(),ticketId,oldStatus,newStatus,note,userId);
                    return;
                }
                if(action=="priority"){
                    string newPriority=body.ContainsKey("newPriority")?body["newPriority"].ToString():"";
                    if(string.IsNullOrWhiteSpace(newPriority)){Err(c,400,"newPriority is required");return;}
                    using(var cmd=new SqlCommand("dbo.sp_Call_SetTicketPriority",con)){
                        cmd.CommandType=CommandType.StoredProcedure;
                        cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                        cmd.Parameters.Add("@NewPriority",SqlDbType.NVarChar).Value=newPriority;
                        cmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                        cmd.ExecuteNonQuery();
                    }
                    Ok(c,new{success=true,message="Priority updated"});
                    return;
                }
                if(action=="note"){
                    string noteType=body.ContainsKey("noteType")?body["noteType"].ToString():"Internal";
                    string noteText=body.ContainsKey("noteText")?body["noteText"].ToString():"";
                    if(string.IsNullOrWhiteSpace(noteText)){Err(c,400,"noteText is required");return;}
                    using(var cmd=new SqlCommand("dbo.sp_Call_AddTicketNote",con)){
                        cmd.CommandType=CommandType.StoredProcedure;
                        cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                        cmd.Parameters.Add("@NoteType",SqlDbType.NVarChar).Value=noteType;
                        cmd.Parameters.Add("@NoteText",SqlDbType.NVarChar).Value=noteText;
                        cmd.Parameters.Add("@CreatedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                        cmd.ExecuteNonQuery();
                    }
                    Ok(c,new{success=true,message="Note added"});
                    return;
                }
                if(action=="resolution"){
                    string resolutionType=body.ContainsKey("resolutionType")?body["resolutionType"].ToString():"";
                    if(string.IsNullOrWhiteSpace(resolutionType)){Err(c,400,"resolutionType is required");return;}
                    if(resolutionType!="Service Only"&&resolutionType!="Replacement"){Err(c,400,"resolutionType must be 'Service Only' or 'Replacement'");return;}

                    string remarks=body.ContainsKey("remarks")?body["remarks"].ToString():"";
                    bool isTemporary=false; if(body.ContainsKey("isTemporary")) bool.TryParse(body["isTemporary"].ToString(),out isTemporary);
                    var resolutionStatus=isTemporary?"Resolved (Temporary)":"Solved";
                    if(!IsStatusTransitionAllowed(currentTicketStatus,resolutionStatus)){
                        Err(c,409,"This ticket cannot be resolved from its current status. Reopen a final ticket first.");return;
                    }
                    if(!assignedToEmpId.HasValue && string.Equals(ticketSource,"Portal",StringComparison.OrdinalIgnoreCase)){
                        Err(c,409,"Portal tickets must be assigned to an IT employee before resolution.");return;
                    }
                    bool useUnlisted=false; if(body.ContainsKey("useUnlistedOldItem")) bool.TryParse(body["useUnlistedOldItem"].ToString(),out useUnlisted);

                    using(var tx=con.BeginTransaction()){
                        try{
                            // Capture snapshot of Department and ResponsiblePerson for history
                            string resDept="", resPerson="";
                            using(var snapCmd=new SqlCommand("SELECT ISNULL(Department,''),ISNULL(ResponsiblePerson,'') FROM dbo.CallTicket WHERE TicketId=@Tid",con,tx)){
                                snapCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                using(var sr=snapCmd.ExecuteReader()){if(sr.Read()){resDept=sr[0].ToString();resPerson=sr[1].ToString();}}
                            }
                            // Log ResolutionDepartment
                            if(!string.IsNullOrWhiteSpace(resDept)){
                                using(var dCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,'ResolutionDepartment',@Val,@Uid)",con,tx)){
                                    dCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    dCmd.Parameters.Add("@Val",SqlDbType.NVarChar).Value=resDept;
                                    dCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    dCmd.ExecuteNonQuery();
                                }
                            }
                            // Log ResolutionResponsiblePerson
                            if(!string.IsNullOrWhiteSpace(resPerson)){
                                using(var pCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,'ResolutionResponsiblePerson',@Val,@Uid)",con,tx)){
                                    pCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    pCmd.Parameters.Add("@Val",SqlDbType.NVarChar).Value=resPerson;
                                    pCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    pCmd.ExecuteNonQuery();
                                }
                            }

                            if(resolutionType=="Service Only"){
                                using(var hCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,'Resolution','Service Only',@Uid)",con,tx)){
                                    hCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    hCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    hCmd.ExecuteNonQuery();
                                }
                                if(!string.IsNullOrWhiteSpace(remarks)){
                                    using(var nCmd=new SqlCommand("INSERT dbo.CallTicketNote(TicketId,NoteType,NoteText,CreatedByUserId)VALUES(@Tid,'Solution',@Note,@Uid)",con,tx)){
                                        nCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                        nCmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=remarks;
                                        nCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                        nCmd.ExecuteNonQuery();
                                    }
                                }
                                using(var sCmd=new SqlCommand("dbo.sp_Call_SetTicketStatus",con,tx){CommandType=CommandType.StoredProcedure}){
                                    sCmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                                    sCmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value="Solved";
                                    sCmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    sCmd.ExecuteNonQuery();
                                }
                            }else{
                                // Parse replacement fields
                                int quantity=1; if(body.ContainsKey("quantity")) int.TryParse(body["quantity"].ToString(),out quantity);
                                int? newItemId=null; if(body.ContainsKey("newItemId")&&body["newItemId"]!=null){int v; if(int.TryParse(body["newItemId"].ToString(),out v))newItemId=v;}
                                int? oldItemConditionId=null; if(body.ContainsKey("oldItemConditionId")&&body["oldItemConditionId"]!=null){int v; if(int.TryParse(body["oldItemConditionId"].ToString(),out v))oldItemConditionId=v;}
                                string oldItemConditionRemarks=body.ContainsKey("oldItemConditionRemarks")?body["oldItemConditionRemarks"].ToString():"";
                                string oldItemRepairAction=body.ContainsKey("oldItemRepairAction")?body["oldItemRepairAction"].ToString():"Repaired";
                                bool isSpare=oldItemRepairAction.IndexOf("Spare",StringComparison.OrdinalIgnoreCase)>=0;

                                if(!newItemId.HasValue||newItemId.Value<=0){Err(c,400,"newItemId is required for Replacement");return;}
                                if(quantity<=0){Err(c,400,"quantity must be > 0");return;}
                                if(!oldItemConditionId.HasValue||oldItemConditionId.Value<=0){Err(c,400,"oldItemConditionId is required");return;}

                                // Determine old item ID
                                int oldItemId;
                                if(useUnlisted){
                                    string uname=body.ContainsKey("unlistedOldItemName")?body["unlistedOldItemName"].ToString():"";
                                    string udesc=body.ContainsKey("unlistedOldItemDescription")?body["unlistedOldItemDescription"].ToString():"";
                                    int? ucatId=null; if(body.ContainsKey("unlistedOldItemCategoryId")&&body["unlistedOldItemCategoryId"]!=null){int v; if(int.TryParse(body["unlistedOldItemCategoryId"].ToString(),out v))ucatId=v;}
                                    string ucatName=body.ContainsKey("unlistedOldItemCategoryName")?body["unlistedOldItemCategoryName"].ToString():"";
                                    string userial=body.ContainsKey("unlistedOldItemSerialNumber")?body["unlistedOldItemSerialNumber"].ToString():"";
                                    string umodel=body.ContainsKey("unlistedOldItemModelNumber")?body["unlistedOldItemModelNumber"].ToString():"";
                                    string uunit=body.ContainsKey("unlistedOldItemUnitOfMeasure")?body["unlistedOldItemUnitOfMeasure"].ToString():"Unit";
                                    if(string.IsNullOrWhiteSpace(uname)){Err(c,400,"unlistedOldItemName is required");return;}

                                    using(var iCmd=new SqlCommand(@"
INSERT INTO dbo.Item(Name,Description,CategoryId,SerialNumber,ModelNumber,UnitOfMeasure,ItemType,StockOnHand)
VALUES(@Name,@Desc,@CatId,@Serial,@Model,@Unit,'Hardware',0);
SELECT CAST(SCOPE_IDENTITY() AS INT)",con,tx)){
                                        iCmd.Parameters.Add("@Name",SqlDbType.NVarChar).Value=uname;
                                        iCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value=(object)udesc??DBNull.Value;
                                        iCmd.Parameters.Add("@CatId",SqlDbType.Int).Value=ucatId.HasValue?(object)ucatId.Value:DBNull.Value;
                                        iCmd.Parameters.Add("@Serial",SqlDbType.NVarChar).Value=(object)userial??DBNull.Value;
                                        iCmd.Parameters.Add("@Model",SqlDbType.NVarChar).Value=(object)umodel??DBNull.Value;
                                        iCmd.Parameters.Add("@Unit",SqlDbType.NVarChar).Value=uunit;
                                        oldItemId=(int)iCmd.ExecuteScalar();
                                    }
                                }else{
                                    if(!body.ContainsKey("oldItemId")||!int.TryParse(body["oldItemId"].ToString(),out oldItemId)||oldItemId<=0){Err(c,400,"oldItemId is required");return;}
                                }

                                // Concurrency-safe stock deduction for new item
                                using(var uCmd=new SqlCommand("UPDATE dbo.Item SET StockOnHand=ISNULL(StockOnHand,0)-@Qty WHERE ItemId=@NewId AND ISNULL(StockOnHand,0)>=@Qty",con,tx)){
                                    uCmd.Parameters.Add("@NewId",SqlDbType.Int).Value=newItemId.Value;
                                    uCmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                    int affected=uCmd.ExecuteNonQuery();
                                    if(affected==0){
                                        // Check current stock to give meaningful error
                                        using(var ckCmd=new SqlCommand("SELECT ISNULL(StockOnHand,0) FROM dbo.Item WHERE ItemId=@Id",con,tx)){
                                            ckCmd.Parameters.Add("@Id",SqlDbType.Int).Value=newItemId.Value;
                                            object sv=ckCmd.ExecuteScalar();
                                            int currentStock=sv!=null&&sv!=DBNull.Value?Convert.ToInt32(sv):0;
                                            throw new Exception($"Insufficient stock. Item has {currentStock} in stock, but {quantity} requested.");
                                        }
                                    }
                                }

                                // Inventory OUT for new item
                                using(var invCmd=new SqlCommand("INSERT dbo.Inventory(ItemId,EntryType,Quantity,DatePosted,Description)VALUES(@ItemId,'OUT',@Qty,GETDATE(),@Desc)",con,tx)){
                                    invCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=newItemId.Value;
                                    invCmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                    invCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value="Call ticket #"+ticketId+" resolution - new item allocation";
                                    invCmd.ExecuteNonQuery();
                                }

                                // Old item: update condition and stock (except if Unrepaired)
                                bool isUnrepaired=oldItemRepairAction.Equals("Unrepaired",StringComparison.OrdinalIgnoreCase);
                                if(!isUnrepaired){
                                    using(var oldUpd=new SqlCommand("UPDATE dbo.Item SET ConditionId=@CondId,Remarks=@Remarks,StockOnHand=ISNULL(StockOnHand,0)+@Qty WHERE ItemId=@OldId",con,tx)){
                                        oldUpd.Parameters.Add("@OldId",SqlDbType.Int).Value=oldItemId;
                                        oldUpd.Parameters.Add("@CondId",SqlDbType.Int).Value=oldItemConditionId.Value;
                                        oldUpd.Parameters.Add("@Remarks",SqlDbType.NVarChar).Value=(object)oldItemConditionRemarks??DBNull.Value;
                                        oldUpd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                        oldUpd.ExecuteNonQuery();
                                    }

                                    // Inventory IN for old item pullout
                                    string invDesc=isSpare?"Call ticket #"+ticketId+" resolution - old unit to spare inventory":"Call ticket #"+ticketId+" resolution - old unit pullout (repaired)";
                                    using(var invCmd=new SqlCommand("INSERT dbo.Inventory(ItemId,EntryType,Quantity,DatePosted,Description)VALUES(@ItemId,'IN',@Qty,GETDATE(),@Desc)",con,tx)){
                                        invCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=oldItemId;
                                        invCmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                        invCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value=invDesc;
                                        invCmd.ExecuteNonQuery();
                                    }
                                }else{
                                    // Just update condition without stock change
                                    using(var oldUpd=new SqlCommand("UPDATE dbo.Item SET ConditionId=@CondId,Remarks=@Remarks WHERE ItemId=@OldId",con,tx)){
                                        oldUpd.Parameters.Add("@OldId",SqlDbType.Int).Value=oldItemId;
                                        oldUpd.Parameters.Add("@CondId",SqlDbType.Int).Value=oldItemConditionId.Value;
                                        oldUpd.Parameters.Add("@Remarks",SqlDbType.NVarChar).Value=(object)oldItemConditionRemarks??DBNull.Value;
                                        oldUpd.ExecuteNonQuery();
                                    }
                                }

                                // History
                                string resolutionLabel=isTemporary?"Replacement (Temporary)":"Replacement";
                                using(var hCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,'Resolution',@Val,@Uid)",con,tx)){
                                    hCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    hCmd.Parameters.Add("@Val",SqlDbType.NVarChar).Value=resolutionLabel;
                                    hCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    hCmd.ExecuteNonQuery();
                                }

                                // Note
                                if(!string.IsNullOrWhiteSpace(remarks)){
                                    using(var nCmd=new SqlCommand("INSERT dbo.CallTicketNote(TicketId,NoteType,NoteText,CreatedByUserId)VALUES(@Tid,'Solution',@Note,@Uid)",con,tx)){
                                        nCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                        nCmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=remarks;
                                        nCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                        nCmd.ExecuteNonQuery();
                                    }
                                }

                                // Status update
                                string newStatus=isTemporary?"Resolved (Temporary)":"Solved";
                                using(var sCmd=new SqlCommand("dbo.sp_Call_SetTicketStatus",con,tx){CommandType=CommandType.StoredProcedure}){
                                    sCmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                                    sCmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=newStatus;
                                    sCmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    sCmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            string msg=resolutionType=="Service Only"?"Ticket resolved as Service Only.":(isTemporary?"Ticket resolved as Replacement (Temporary).":"Ticket resolved as Replacement.");
                            Ok(c,new{success=true,message=msg});
                        }catch(Exception ex){
                            try{tx.Rollback();}catch{}
                            Err(c,500,"Resolution failed: "+ex.Message);
                        }
                    }
                    return;
                }
            }
        } catch(Exception ex){Err(c,500,"Action failed: "+ex.Message);}
    }

    static int StatusRank(string status) {
        var s=(status??string.Empty).Trim();
        if(s.Equals("Pending",StringComparison.OrdinalIgnoreCase)) return 0;
        if(s.Equals("In Progress",StringComparison.OrdinalIgnoreCase)||s.Equals("Reopened",StringComparison.OrdinalIgnoreCase)) return 1;
        if(s.Equals("Escalated",StringComparison.OrdinalIgnoreCase)) return 2;
        if(s.Equals("Resolved (Temporary)",StringComparison.OrdinalIgnoreCase)) return 3;
        if(s.Equals("Solved",StringComparison.OrdinalIgnoreCase)||s.Equals("Closed",StringComparison.OrdinalIgnoreCase)) return 4;
        return -1;
    }

    static bool IsStatusTransitionAllowed(string currentStatus,string newStatus) {
        var cur=(currentStatus??string.Empty).Trim();
        var next=(newStatus??string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(next)||next.Equals(cur,StringComparison.OrdinalIgnoreCase)) return true;
        if(next.Equals("Reopened",StringComparison.OrdinalIgnoreCase))
            return cur.Equals("Solved",StringComparison.OrdinalIgnoreCase)
                ||cur.Equals("Resolved (Temporary)",StringComparison.OrdinalIgnoreCase)
                ||cur.Equals("Closed",StringComparison.OrdinalIgnoreCase);
        var curRank=StatusRank(cur); var nextRank=StatusRank(next);
        if(curRank<0||nextRank<0) return true;
        return nextRank>=curRank;
    }

    static bool IsPortalWorkStart(string source,string newStatus) {
        if(!string.Equals((source??"Portal").Trim(),"Portal",StringComparison.OrdinalIgnoreCase)) return false;
        var s=(newStatus??string.Empty).Trim();
        return s.Equals("In Progress",StringComparison.OrdinalIgnoreCase)
            ||s.Equals("Escalated",StringComparison.OrdinalIgnoreCase)
            ||s.Equals("Resolved (Temporary)",StringComparison.OrdinalIgnoreCase)
            ||s.Equals("Solved",StringComparison.OrdinalIgnoreCase)
            ||s.Equals("Closed",StringComparison.OrdinalIgnoreCase);
    }

    static bool TryReadTicketWorkflowState(SqlConnection con,int ticketId,out string source,out string status,out int? assignedToEmpId) {
        source="Portal"; status=""; assignedToEmpId=null;
        using(var cmd=new SqlCommand("SELECT TOP 1 ISNULL(TicketSource,'Portal'),ISNULL(Status,''),AssignedToEmpId FROM dbo.CallTicket WHERE TicketId=@TicketId",con)) {
            cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
            using(var r=cmd.ExecuteReader()) {
                if(!r.Read()) return false;
                source=r[0]==DBNull.Value?"Portal":r[0].ToString();
                status=r[1]==DBNull.Value?"":r[1].ToString();
                assignedToEmpId=r[2]==DBNull.Value?(int?)null:Convert.ToInt32(r[2]);
                return true;
            }
        }
    }

    bool HasParam(SqlConnection con,string spName,string paramName) {
        using(var cmd=new SqlCommand(@"
SELECT CASE WHEN EXISTS(
    SELECT 1 FROM sys.parameters p
    INNER JOIN sys.objects o ON o.object_id=p.object_id
    INNER JOIN sys.schemas s ON s.schema_id=o.schema_id
    WHERE s.name='dbo' AND o.name=OBJECT_NAME(OBJECT_ID(@SpName)) AND o.type='P' AND p.name=@ParamName
) THEN 1 ELSE 0 END",con)){
            cmd.Parameters.Add("@SpName",SqlDbType.NVarChar).Value=spName;
            cmd.Parameters.Add("@ParamName",SqlDbType.NVarChar).Value=paramName;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    public bool IsReusable{get{return false;}}
}

public static class ItcmEmailHelper
{
    private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("Yakult.Inventory.App|SecretProtector|v1");
    private static readonly Regex PlaceholderRx = new Regex(@"\{\{\s*(?<k>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<k2>[A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

    public static void TrySendNewTicketEmailAsync(string connectionString, int ticketId, int? userId)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { SendEmail(connectionString, ticketId, "NewTicket", null, userId); }
            catch { }
        });
    }

    public static void TrySendStatusUpdateEmailAsync(string connectionString, int ticketId,
        string oldStatus, string newStatus, string note, int? userId)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                bool isEscalation = string.Equals((newStatus ?? "").Trim(), "Escalated", StringComparison.OrdinalIgnoreCase);
                string templateType = isEscalation ? "Escalation" : "StatusUpdate";
                var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                extra["OldStatus"] = oldStatus ?? string.Empty;
                extra["NewStatus"] = newStatus ?? string.Empty;
                extra["Note"]      = note ?? string.Empty;
                SendEmail(connectionString, ticketId, templateType, extra, userId);
            }
            catch { }
        });
    }

    private static void SendEmail(string cs, int ticketId, string templateType,
        Dictionary<string, string> extra, int? userId)
    {
        using (var con = new SqlConnection(cs))
        {
            con.Open();
            if (!IsEmailTypeEnabled(con, templateType))
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "Notifications disabled for " + templateType + ".", userId); return; }
            string subject, body;
            if (!GetTemplate(con, templateType, out subject, out body))
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "Template missing or inactive: " + templateType + ".", userId); return; }
            TicketRow ticket = GetTicket(con, ticketId);
            if (ticket == null)
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "Ticket not found.", userId); return; }
            SmtpCfg smtp = ResolveSmtp(con, ticket.BranchId, ticket.DeptId);
            if (smtp == null)
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "SMTP not configured.", userId); return; }
            bool preferEscalation = string.Equals(templateType, "Escalation", StringComparison.OrdinalIgnoreCase);
            List<string> recipients = ResolveRecipients(con, ticket.BranchId, ticket.DeptId, preferEscalation);
            if (recipients.Count == 0)
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "No recipients configured.", userId); return; }
            var ph = BuildPlaceholders(ticket);
            if (extra != null) foreach (var kv in extra) ph[kv.Key] = kv.Value ?? string.Empty;
            string rendSubject = Render(subject, ph);
            string rendBody    = Render(body, ph);
            if (string.IsNullOrWhiteSpace(rendSubject))
                rendSubject = "[IT Call Monitoring] " + templateType + " - Ticket #" + ticketId;
            string recipientStr = string.Join(", ", recipients);
            try { Send(smtp, recipients, rendSubject, rendBody); LogEmail(con, ticketId, templateType, recipientStr, rendSubject, "Sent", null, userId); }
            catch (Exception ex) { LogEmail(con, ticketId, templateType, recipientStr, rendSubject, "Failed", ex.Message, userId); }
        }
    }

    private static bool IsEmailTypeEnabled(SqlConnection con, string templateType)
    {
        if (!ObjExists(con, "dbo.CallNotificationRules", "U")) return false;
        string col = templateType == "NewTicket" ? "NotifyOnNewTicket"
                   : templateType == "StatusUpdate" ? "NotifyOnStatusChange"
                   : templateType == "Escalation" ? "NotifyOnEscalation" : null;
        if (col == null) return false;
        using (var cmd = new SqlCommand("SELECT TOP 1 [" + col + "] FROM dbo.CallNotificationRules ORDER BY RulesId DESC", con))
        { object v = cmd.ExecuteScalar(); return v != null && v != DBNull.Value && Convert.ToBoolean(v); }
    }

    private static bool GetTemplate(SqlConnection con, string templateType, out string subject, out string body)
    {
        subject = body = null;
        if (!ObjExists(con, "dbo.CallEmailTemplate", "U")) return false;
        using (var cmd = new SqlCommand("SELECT TOP 1 Subject,Body,IsActive FROM dbo.CallEmailTemplate WHERE TemplateType=@T ORDER BY TemplateId DESC", con))
        {
            cmd.Parameters.Add("@T", SqlDbType.NVarChar).Value = templateType;
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return false;
                if (r["IsActive"] == DBNull.Value || !Convert.ToBoolean(r["IsActive"])) return false;
                subject = r["Subject"] == DBNull.Value ? null : r["Subject"].ToString();
                body    = r["Body"]    == DBNull.Value ? null : r["Body"].ToString();
                return true;
            }
        }
    }

    private static SmtpCfg ResolveSmtp(SqlConnection con, int? branchId, int? deptId)
    {
        SmtpCfg cfg;
        if (branchId > 0 && ObjExists(con, "dbo.CallBranchSmtpProfileLink", "U") && ObjExists(con, "dbo.CallSmtpProfile", "U"))
        { cfg = QuerySmtp(con, "SELECT TOP 1 p.SmtpServer,p.SmtpPort,p.UseSsl,p.SmtpUsername,p.SmtpPasswordEnc,p.FromName,p.FromEmail FROM dbo.CallBranchSmtpProfileLink l INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId=l.ProfileId WHERE l.BranchId=@Id AND l.IsActive=1 AND p.IsActive=1 ORDER BY p.UpdatedAt DESC,p.ProfileId DESC", branchId.Value); if (cfg != null) return cfg; }
        if (branchId > 0 && ObjExists(con, "dbo.CallBranchSmtpProfile", "U"))
        { cfg = QuerySmtp(con, "SELECT TOP 1 SmtpServer,SmtpPort,UseSsl,SmtpUsername,SmtpPasswordEnc,FromName,FromEmail FROM dbo.CallBranchSmtpProfile WHERE BranchId=@Id ORDER BY UpdatedAt DESC,ProfileId DESC", branchId.Value); if (cfg != null) return cfg; }
        if (deptId > 0 && ObjExists(con, "dbo.CallDepartmentSmtpProfileLink", "U") && ObjExists(con, "dbo.CallSmtpProfile", "U"))
        { cfg = QuerySmtp(con, "SELECT TOP 1 p.SmtpServer,p.SmtpPort,p.UseSsl,p.SmtpUsername,p.SmtpPasswordEnc,p.FromName,p.FromEmail FROM dbo.CallDepartmentSmtpProfileLink l INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId=l.ProfileId WHERE l.DeptId=@Id AND l.IsActive=1 AND p.IsActive=1 ORDER BY p.UpdatedAt DESC,p.ProfileId DESC", deptId.Value); if (cfg != null) return cfg; }
        if (deptId > 0 && ObjExists(con, "dbo.CallDepartmentSmtpProfile", "U"))
        { cfg = QuerySmtp(con, "SELECT TOP 1 SmtpServer,SmtpPort,UseSsl,SmtpUsername,SmtpPasswordEnc,FromName,FromEmail FROM dbo.CallDepartmentSmtpProfile WHERE DeptId=@Id ORDER BY UpdatedAt DESC,ProfileId DESC", deptId.Value); if (cfg != null) return cfg; }
        if (ObjExists(con, "dbo.CallEmailSettings", "U"))
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 SmtpServer,SmtpPort,UseSsl,SmtpUsername,SmtpPasswordEnc,FromName,FromEmail FROM dbo.CallEmailSettings ORDER BY SettingsId DESC", con))
            using (var r = cmd.ExecuteReader()) { if (r.Read()) return ReadSmtpRow(r); }
        }
        return null;
    }

    private static SmtpCfg QuerySmtp(SqlConnection con, string sql, int id)
    {
        using (var cmd = new SqlCommand(sql, con)) { cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id; using (var r = cmd.ExecuteReader()) { if (r.Read()) return ReadSmtpRow(r); } }
        return null;
    }

    private static SmtpCfg ReadSmtpRow(System.Data.IDataReader r)
    {
        string server = r["SmtpServer"] == DBNull.Value ? null : r["SmtpServer"].ToString();
        if (string.IsNullOrWhiteSpace(server)) return null;
        byte[] enc = r["SmtpPasswordEnc"] == DBNull.Value ? null : (byte[])r["SmtpPasswordEnc"];
        return new SmtpCfg { Server = server, Port = r["SmtpPort"] == DBNull.Value ? 587 : Convert.ToInt32(r["SmtpPort"]), UseSsl = r["UseSsl"] != DBNull.Value && Convert.ToBoolean(r["UseSsl"]), Username = r["SmtpUsername"] == DBNull.Value ? null : r["SmtpUsername"].ToString(), Password = Decrypt(enc), FromName = r["FromName"] == DBNull.Value ? null : r["FromName"].ToString(), FromEmail = r["FromEmail"] == DBNull.Value ? null : r["FromEmail"].ToString() };
    }

    private static List<string> ResolveRecipients(SqlConnection con, int? branchId, int? deptId, bool preferEscalation)
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ObjExists(con, "dbo.CallNotificationRules", "U"))
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 GroupEmail,EscalationEmail FROM dbo.CallNotificationRules ORDER BY RulesId DESC", con))
            using (var r = cmd.ExecuteReader()) { if (r.Read()) AddEmails(all, preferEscalation ? r["EscalationEmail"] : r["GroupEmail"]); }
        }
        if (branchId > 0 && ObjExists(con, "dbo.CallBranchNotificationRecipient", "U"))
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 RecipientEmails,EscalationEmails FROM dbo.CallBranchNotificationRecipient WHERE BranchId=@Id AND IsActive=1 ORDER BY UpdatedAt DESC", con))
            { cmd.Parameters.Add("@Id", SqlDbType.Int).Value = branchId.Value; using (var r = cmd.ExecuteReader()) { if (r.Read()) { if (preferEscalation) { AddEmails(all, r["EscalationEmails"]); AddEmails(all, r["RecipientEmails"]); } else AddEmails(all, r["RecipientEmails"]); } } }
        }
        if (deptId > 0 && ObjExists(con, "dbo.CallDepartmentNotificationRecipient", "U"))
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 RecipientEmails,EscalationEmails FROM dbo.CallDepartmentNotificationRecipient WHERE DeptId=@Id AND IsActive=1 ORDER BY UpdatedAt DESC", con))
            { cmd.Parameters.Add("@Id", SqlDbType.Int).Value = deptId.Value; using (var r = cmd.ExecuteReader()) { if (r.Read()) { if (preferEscalation) { AddEmails(all, r["EscalationEmails"]); AddEmails(all, r["RecipientEmails"]); } else AddEmails(all, r["RecipientEmails"]); } } }
        }
        if (all.Count == 0 && branchId > 0 && ObjExists(con, "dbo.Branch", "U"))
        { using (var cmd = new SqlCommand("SELECT TOP 1 Email FROM dbo.Branch WHERE BranchId=@Id", con)) { cmd.Parameters.Add("@Id", SqlDbType.Int).Value = branchId.Value; object v = cmd.ExecuteScalar(); if (v != null && v != DBNull.Value) AddEmails(all, v); } }
        return all.ToList();
    }

    private static void AddEmails(HashSet<string> set, object val)
    {
        if (val == null || val == DBNull.Value) return;
        foreach (var part in val.ToString().Split(new[] { ';', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        { string t = part.Trim(); if (string.IsNullOrWhiteSpace(t)) continue; try { string a = new MailAddress(t).Address; if (!string.IsNullOrWhiteSpace(a)) set.Add(a); } catch { } }
    }

    private sealed class TicketRow
    {
        public int TicketId;
        public int? BranchId, DeptId;
        public string TicketCode, Company, Department, Branch, CallerName, Issue,
                      IssueType, Priority, Status, AssignedTo, ProvidedSolution;
        public DateTime CreatedAt, UpdatedAt;
        public DateTime? LastContactAt, LastReminderSentAt, SolvedAt;
    }

    private static TicketRow GetTicket(SqlConnection con, int ticketId)
    {
        bool hasView = ObjExists(con, "dbo.vw_Call_TicketList", null);
        string sql = hasView
            ? "SELECT TOP 1 TicketId,TicketCode,COALESCE(Company,'') AS Company,COALESCE(Department,'') AS Department,COALESCE(Branch,'') AS Branch,CallerName,Issue,IssueType,Priority,Status,AssignedTo,ProvidedSolution,BranchId,DeptId,CreatedAt,UpdatedAt,LastContactAt,LastReminderSentAt,SolvedAt FROM dbo.vw_Call_TicketList WHERE TicketId=@Id"
            : "SELECT TOP 1 TicketId,TicketCode,'' AS Company,'' AS Department,'' AS Branch,CallerName,Issue,IssueType,Priority,Status,NULL AS AssignedTo,ProvidedSolution,BranchId,DeptId,CreatedAt,UpdatedAt,LastContactAt,NULL AS LastReminderSentAt,SolvedAt FROM dbo.CallTicket WHERE TicketId=@Id";
        using (var cmd = new SqlCommand(sql, con))
        {
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = ticketId;
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new TicketRow {
                    TicketId = ticketId, TicketCode = r["TicketCode"] == DBNull.Value ? null : r["TicketCode"].ToString(),
                    Company = r["Company"] == DBNull.Value ? null : r["Company"].ToString(), Department = r["Department"] == DBNull.Value ? null : r["Department"].ToString(),
                    Branch = r["Branch"] == DBNull.Value ? null : r["Branch"].ToString(), CallerName = r["CallerName"] == DBNull.Value ? null : r["CallerName"].ToString(),
                    Issue = r["Issue"] == DBNull.Value ? null : r["Issue"].ToString(), IssueType = r["IssueType"] == DBNull.Value ? null : r["IssueType"].ToString(),
                    Priority = r["Priority"] == DBNull.Value ? null : r["Priority"].ToString(), Status = r["Status"] == DBNull.Value ? null : r["Status"].ToString(),
                    AssignedTo = r["AssignedTo"] == DBNull.Value ? null : r["AssignedTo"].ToString(), ProvidedSolution = r["ProvidedSolution"] == DBNull.Value ? null : r["ProvidedSolution"].ToString(),
                    BranchId = r["BranchId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["BranchId"]), DeptId = r["DeptId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["DeptId"]),
                    CreatedAt = r["CreatedAt"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(r["CreatedAt"]), UpdatedAt = r["UpdatedAt"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(r["UpdatedAt"]),
                    LastContactAt = r["LastContactAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastContactAt"]),
                    LastReminderSentAt = r["LastReminderSentAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastReminderSentAt"]),
                    SolvedAt = r["SolvedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["SolvedAt"])
                };
            }
        }
    }

    private static Dictionary<string, string> BuildPlaceholders(TicketRow t)
    {
        var now = DateTime.UtcNow;
        int idleDays = t.LastContactAt.HasValue ? Math.Max(0, (int)Math.Floor((now - t.LastContactAt.Value).TotalDays)) : 0;
        var ph = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ph["TicketId"]           = t.TicketId.ToString();
        ph["TicketCode"]         = t.TicketCode ?? "";
        ph["Company"]            = t.Company ?? "";
        ph["Department"]         = t.Department ?? "";
        ph["Branch"]             = t.Branch ?? "";
        ph["CallerName"]         = t.CallerName ?? "";
        ph["Issue"]              = t.Issue ?? "";
        ph["IssueType"]          = t.IssueType ?? "";
        ph["Priority"]           = t.Priority ?? "";
        ph["Status"]             = t.Status ?? "";
        ph["AssignedTo"]         = t.AssignedTo ?? "";
        ph["ProvidedSolution"]   = t.ProvidedSolution ?? "";
        ph["CreatedAt"]          = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        ph["UpdatedAt"]          = t.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
        ph["LastContactAt"]      = t.LastContactAt.HasValue ? t.LastContactAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
        ph["LastReminderSentAt"] = t.LastReminderSentAt.HasValue ? t.LastReminderSentAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
        ph["IdleDays"]           = idleDays.ToString();
        ph["SolvedAt"]           = t.SolvedAt.HasValue ? t.SolvedAt.Value.ToString("yyyy-MM-dd HH:mm") : "";
        return ph;
    }

    private static string Render(string template, Dictionary<string, string> vals)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (vals == null || vals.Count == 0) return template;
        return PlaceholderRx.Replace(template, m =>
        {
            string key = m.Groups["k"].Success ? m.Groups["k"].Value : m.Groups["k2"].Value;
            string v; return vals.TryGetValue(key, out v) ? (v ?? string.Empty) : m.Value;
        });
    }

    private static void Send(SmtpCfg cfg, List<string> recipients, string subject, string body)
    {
        string fromEmail = !string.IsNullOrWhiteSpace(cfg.FromEmail) ? cfg.FromEmail : cfg.Username;
        if (string.IsNullOrWhiteSpace(fromEmail)) throw new InvalidOperationException("FromEmail is not configured.");
        using (var msg = new MailMessage())
        {
            msg.From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(cfg.FromName) ? "IT Call Monitoring" : cfg.FromName);
            foreach (var to in recipients.Where(r => !string.IsNullOrWhiteSpace(r))) msg.To.Add(new MailAddress(to));
            if (msg.To.Count == 0) throw new InvalidOperationException("No valid To addresses.");
            msg.Subject = subject; msg.Body = body; msg.BodyEncoding = Encoding.UTF8; msg.SubjectEncoding = Encoding.UTF8; msg.IsBodyHtml = false;
            using (var smtp = new SmtpClient(cfg.Server, cfg.Port <= 0 ? 587 : cfg.Port))
            {
                smtp.EnableSsl = cfg.UseSsl;
                if (!string.IsNullOrWhiteSpace(cfg.Username)) { smtp.UseDefaultCredentials = false; smtp.Credentials = new NetworkCredential(cfg.Username, cfg.Password ?? string.Empty); }
                else smtp.UseDefaultCredentials = true;
                smtp.Send(msg);
            }
        }
    }

    private static void LogEmail(SqlConnection con, int ticketId, string emailType, string recipient, string subject, string status, string error, int? userId)
    {
        try
        {
            if (!ObjExists(con, "dbo.CallEmailLog", "U")) return;
            using (var cmd = new SqlCommand("INSERT dbo.CallEmailLog (TicketId,EmailType,Recipient,Subject,Status,ErrorMessage,CreatedByUserId) VALUES (@TicketId,@EmailType,@Recipient,@Subject,@Status,@ErrorMessage,@UserId)", con))
            {
                cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = (object)ticketId;
                cmd.Parameters.Add("@EmailType", SqlDbType.NVarChar).Value = emailType ?? string.Empty;
                cmd.Parameters.Add("@Recipient", SqlDbType.NVarChar).Value = (object)recipient ?? DBNull.Value;
                cmd.Parameters.Add("@Subject", SqlDbType.NVarChar).Value = (object)subject ?? DBNull.Value;
                cmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = status ?? string.Empty;
                cmd.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar).Value = (object)error ?? DBNull.Value;
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = (object)userId ?? DBNull.Value;
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static string Decrypt(byte[] enc)
    {
        if (enc == null || enc.Length == 0) return null;
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(enc, DpapiEntropy, DataProtectionScope.LocalMachine)); } catch { }
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(enc, DpapiEntropy, DataProtectionScope.CurrentUser)); } catch { return null; }
    }

    private static bool ObjExists(SqlConnection con, string name, string type)
    {
        string sql = type != null ? "SELECT CASE WHEN OBJECT_ID(@N,@T) IS NOT NULL THEN 1 ELSE 0 END" : "SELECT CASE WHEN OBJECT_ID(@N) IS NOT NULL THEN 1 ELSE 0 END";
        using (var cmd = new SqlCommand(sql, con))
        { cmd.Parameters.Add("@N", SqlDbType.NVarChar).Value = name; if (type != null) cmd.Parameters.Add("@T", SqlDbType.NVarChar).Value = type; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
    }

    private sealed class SmtpCfg
    {
        public string Server, Username, Password, FromName, FromEmail;
        public int    Port;
        public bool   UseSsl;
    }
}
