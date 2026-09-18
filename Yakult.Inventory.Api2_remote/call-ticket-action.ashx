<%@ WebHandler Language="C#" Class="CallTicketActionHandler" %>
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
                    ItcmActionEmailHelper.TrySendStatusUpdateEmailAsync(Cs(),ticketId,oldStatus,newStatus,note,userId);
                    return;
                }
                if(action=="priority"){
                    string newPriority=body.ContainsKey("newPriority")?body["newPriority"].ToString():"";
                    if(string.IsNullOrWhiteSpace(newPriority)){Err(c,400,"newPriority is required");return;}
                    // Mirror the proc allowlist (sp_Call_SetTicketPriority canonicalises case and
                    // THROWs 50014 otherwise) so API callers get a clean 400 instead of a 500.
                    string npCheck=newPriority.Trim();
                    if(!npCheck.Equals("Low",StringComparison.OrdinalIgnoreCase)
                        &&!npCheck.Equals("Medium",StringComparison.OrdinalIgnoreCase)
                        &&!npCheck.Equals("High",StringComparison.OrdinalIgnoreCase)
                        &&!npCheck.Equals("Critical",StringComparison.OrdinalIgnoreCase)){Err(c,400,"newPriority must be Low, Medium, High, or Critical");return;}
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
                if(action=="assign"){
                    // Mobile triage parity with desktop AssignTicketAndNotifyAsync. Portal tickets
                    // are deliberately assignable here: assignment is what unblocks them for work.
                    int? newAssignee=null;
                    if(body.ContainsKey("assignedToEmpId")&&body["assignedToEmpId"]!=null){
                        int v; if(!int.TryParse(body["assignedToEmpId"].ToString(),out v)||v<=0){Err(c,400,"assignedToEmpId must be a positive employee id, or omit it to unassign");return;}
                        newAssignee=v;
                    }
                    if(newAssignee.HasValue && !CallTicketApiSecurity.IsItEmployee(con,newAssignee.Value)){
                        Err(c,400,"assignedToEmpId must reference an active IT employee.");return;
                    }
                    // Mirror desktop: no assignment changes on final tickets (proc itself only
                    // blocks Solved/Resolved (Temporary); Closed is blocked here for parity).
                    if(string.Equals(currentTicketStatus,"Solved",StringComparison.OrdinalIgnoreCase)
                        ||string.Equals(currentTicketStatus,"Resolved (Temporary)",StringComparison.OrdinalIgnoreCase)
                        ||string.Equals(currentTicketStatus,"Closed",StringComparison.OrdinalIgnoreCase)){Err(c,409,"Assignment cannot be changed on a final ticket. Reopen it first.");return;}
                    // No-op when unchanged; skip the email like the desktop does.
                    if(assignedToEmpId==newAssignee){Ok(c,new{success=true,message="Already assigned"});return;}
                    using(var cmd=new SqlCommand("dbo.sp_Call_AssignTicket",con)){
                        cmd.CommandType=CommandType.StoredProcedure;
                        cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                        cmd.Parameters.Add("@AssignedToEmpId",SqlDbType.Int).Value=newAssignee==null?(object)DBNull.Value:(object)newAssignee.Value;
                        cmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                        cmd.ExecuteNonQuery();
                    }
                    Ok(c,new{success=true,message=newAssignee.HasValue?"Ticket assigned":"Ticket unassigned"});
                    // Desktop skips the notify when the target is unassigned; same here.
                    if(newAssignee.HasValue)
                        ItcmActionEmailHelper.TrySendAssignmentEmailAsync(Cs(),ticketId,userId);
                    return;
                }
                if(action=="resolution"){
                    string resolutionType=body.ContainsKey("resolutionType")?body["resolutionType"].ToString():"";
                    if(string.IsNullOrWhiteSpace(resolutionType)){Err(c,400,"resolutionType is required");return;}
                    if(resolutionType!="Service Only"&&resolutionType!="Replacement"){Err(c,400,"resolutionType must be 'Service Only' or 'Replacement'");return;}

                    string remarks=body.ContainsKey("remarks")?body["remarks"].ToString():"";
                    bool isTemporary=false; if(body.ContainsKey("isTemporary")) bool.TryParse(body["isTemporary"].ToString(),out isTemporary);
                    int? forwardedRepairTicketId=null, forwardedOldItemId=null, forwardedRepairItemId=null;
                    string forwardedRepairTicketCode=null, parentOutcome=null;
                    if(body.ContainsKey("forwardedRepairTicketId")&&body["forwardedRepairTicketId"]!=null){int v;if(int.TryParse(body["forwardedRepairTicketId"].ToString(),out v)&&v>0)forwardedRepairTicketId=v;}
                    if(body.ContainsKey("forwardedOldItemId")&&body["forwardedOldItemId"]!=null){int v;if(int.TryParse(body["forwardedOldItemId"].ToString(),out v)&&v>0)forwardedOldItemId=v;}
                    bool isForwardedToRepair=forwardedRepairTicketId.HasValue;
                    if(isForwardedToRepair){
                        parentOutcome=body.ContainsKey("parentOutcome")&&body["parentOutcome"]!=null?body["parentOutcome"].ToString().Trim():"";
                        if(parentOutcome!="Forwarded to Repair"&&parentOutcome!="Solved"&&parentOutcome!="Resolved (Temporary)"){Err(c,400,"parentOutcome must be Forwarded to Repair, Solved, or Resolved (Temporary)");return;}
                        if(resolutionType=="Replacement"&&!forwardedOldItemId.HasValue){Err(c,400,"forwardedOldItemId is required when forwarding a replacement");return;}
                        if(!TableExists(con,"dbo.RepairTicket")||!ColumnExists(con,"dbo.RepairTicket","CallTicketId")){Err(c,503,"IT CALL repair forwarding is not installed for this environment");return;}
                        using(var forwardCmd=new SqlCommand("SELECT RepairTicketId,TicketCode,ItemId FROM dbo.RepairTicket WHERE RepairTicketId=@RepairTicketId AND CallTicketId=@CallTicketId",con)){
                            forwardCmd.Parameters.Add("@RepairTicketId",SqlDbType.Int).Value=forwardedRepairTicketId.Value;
                            forwardCmd.Parameters.Add("@CallTicketId",SqlDbType.Int).Value=ticketId;
                            using(var forwardReader=forwardCmd.ExecuteReader()){
                                if(!forwardReader.Read()){Err(c,409,"The selected Repair Ticket is not linked to this IT Call");return;}
                                forwardedRepairTicketCode=forwardReader["TicketCode"]==DBNull.Value?null:forwardReader["TicketCode"].ToString();
                                forwardedRepairItemId=Convert.ToInt32(forwardReader["ItemId"]);
                            }
                        }
                        var forwardSuffix="Forwarded to Repair Ticket "+(forwardedRepairTicketCode??forwardedRepairTicketId.Value.ToString())+" | IT Call outcome: "+parentOutcome;
                        remarks=string.IsNullOrWhiteSpace(remarks)?forwardSuffix:remarks.Trim()+" | "+forwardSuffix;
                    }
                    var resolutionStatus=isForwardedToRepair?parentOutcome:(isTemporary?"Resolved (Temporary)":"Solved");
                    if(!IsStatusTransitionAllowed(currentTicketStatus,resolutionStatus)){
                        Err(c,409,"This ticket cannot be resolved from its current status. Reopen a final ticket first.");return;
                    }
                    if(!assignedToEmpId.HasValue && string.Equals(ticketSource,"Portal",StringComparison.OrdinalIgnoreCase)){
                        Err(c,409,"Portal tickets must be assigned to an IT employee before resolution.");return;
                    }
                    bool useUnlisted=false; if(body.ContainsKey("useUnlistedOldItem")) bool.TryParse(body["useUnlistedOldItem"].ToString(),out useUnlisted);
                    if(forwardedOldItemId.HasValue) useUnlisted=false;

                    using(var tx=con.BeginTransaction()){
                        try{
                            // Snapshot the department and assignee for resolution history.
                            // Those are display fields from vw_Call_TicketList, not columns on CallTicket.
                            string resDept="", resPerson="";
                            using(var snapCmd=new SqlCommand(@"
SELECT ISNULL(d.Name,''), ISNULL(e.Name,'')
FROM dbo.CallTicket t
LEFT JOIN dbo.Department d ON d.DeptId=t.DeptId
LEFT JOIN dbo.Employee e ON e.EmpId=t.AssignedToEmpId
WHERE t.TicketId=@Tid",con,tx)){
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
                                // Canonical type value (no suffix): the repair link, if any,
                                // already travels in the remarks above, and reports match
                                // on exact 'Service Only'. FieldName matches desktop.
                                using(var hCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,'ResolutionType',@Resolution,@Uid)",con,tx)){
                                    hCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    hCmd.Parameters.Add("@Resolution",SqlDbType.NVarChar).Value="Service Only";
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
                                    sCmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=resolutionStatus;
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
                                // A physical unit already linked to Repair must remain out of available
                                // inventory while its technician assessment is pending, regardless of the
                                // original replacement-screen default.
                                if(isForwardedToRepair) oldItemRepairAction="Unrepaired";
                                bool isSpare=oldItemRepairAction.IndexOf("Spare",StringComparison.OrdinalIgnoreCase)>=0;

                                if(!newItemId.HasValue||newItemId.Value<=0){Err(c,400,"newItemId is required for Replacement");return;}
                                if(quantity<=0){Err(c,400,"quantity must be > 0");return;}
                                if(!oldItemConditionId.HasValue||oldItemConditionId.Value<=0){Err(c,400,"oldItemConditionId is required");return;}

                                // Determine old item ID. A forward of an unlisted old unit has
                                // already created that unit during the explicit Repair Ticket step;
                                // reuse it here instead of creating a duplicate inventory record.
                                int oldItemId;
                                if(forwardedOldItemId.HasValue){
                                    oldItemId=forwardedOldItemId.Value;
                                }else if(useUnlisted){
                                    string uname=body.ContainsKey("unlistedOldItemName")?body["unlistedOldItemName"].ToString():"";
                                    string udesc=body.ContainsKey("unlistedOldItemDescription")?body["unlistedOldItemDescription"].ToString():"";
                                    int? ucatId=null; if(body.ContainsKey("unlistedOldItemCategoryId")&&body["unlistedOldItemCategoryId"]!=null){int v; if(int.TryParse(body["unlistedOldItemCategoryId"].ToString(),out v))ucatId=v;}
                                    string ucatName=body.ContainsKey("unlistedOldItemCategoryName")?body["unlistedOldItemCategoryName"].ToString():"";
                                    string userial=body.ContainsKey("unlistedOldItemSerialNumber")?body["unlistedOldItemSerialNumber"].ToString():"";
                                    string umodel=body.ContainsKey("unlistedOldItemModelNumber")?body["unlistedOldItemModelNumber"].ToString():"";
                                    string uunit=body.ContainsKey("unlistedOldItemUnitOfMeasure")?body["unlistedOldItemUnitOfMeasure"].ToString():"Unit";
                                    if(string.IsNullOrWhiteSpace(uname)){Err(c,400,"unlistedOldItemName is required");return;}
                                    if(string.IsNullOrWhiteSpace(umodel)){Err(c,400,"unlistedOldItemModelNumber is required");return;}
                                    if(string.IsNullOrWhiteSpace(uunit)){Err(c,400,"unlistedOldItemUnitOfMeasure is required");return;}
                                    if(!ucatId.HasValue||ucatId.Value<=0){Err(c,400,"unlistedOldItemCategoryId is required");return;}

                                    using(var iCmd=new SqlCommand(@"
INSERT INTO dbo.Item(Name,Description,CategoryId,Category,SerialNumber,ModelNumber,UnitOfMeasure,ItemType,StockOnHand,Active,DateCreated,CreatedBy,DateModified,ModifiedBy,ConditionID,Remarks,AffectsInventory,IsTrackedAsset)
VALUES(@Name,@Desc,@CatId,@CatName,@Serial,@Model,@Unit,'Hardware',0,1,SYSUTCDATETIME(),@UserId,SYSUTCDATETIME(),@UserId,@ConditionId,@CondRemarks,1,0);
SELECT CAST(SCOPE_IDENTITY() AS INT)",con,tx)){
                                        iCmd.Parameters.Add("@Name",SqlDbType.NVarChar).Value=uname;
                                        iCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value=(object)udesc??DBNull.Value;
                                        iCmd.Parameters.Add("@CatId",SqlDbType.Int).Value=ucatId.Value;
                                        iCmd.Parameters.Add("@CatName",SqlDbType.VarChar).Value=ucatName;
                                        iCmd.Parameters.Add("@Serial",SqlDbType.VarChar).Value=(object)userial??DBNull.Value;
                                        iCmd.Parameters.Add("@Model",SqlDbType.NVarChar).Value=umodel;
                                        iCmd.Parameters.Add("@Unit",SqlDbType.NVarChar).Value=uunit;
                                        iCmd.Parameters.Add("@UserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                        iCmd.Parameters.Add("@ConditionId",SqlDbType.Int).Value=oldItemConditionId.Value;
                                        iCmd.Parameters.Add("@CondRemarks",SqlDbType.NVarChar).Value=(object)oldItemConditionRemarks??DBNull.Value;
                                        oldItemId=(int)iCmd.ExecuteScalar();
                                    }
                                }else{
                                    if(!body.ContainsKey("oldItemId")||!int.TryParse(body["oldItemId"].ToString(),out oldItemId)||oldItemId<=0){Err(c,400,"oldItemId is required");return;}
                                }

                                if(isForwardedToRepair && (!forwardedRepairItemId.HasValue || forwardedRepairItemId.Value!=oldItemId)){
                                    tx.Rollback();
                                    Err(c,409,"The linked Repair Ticket must match the physical old item being replaced");
                                    return;
                                }
                                if(oldItemId==newItemId.Value){
                                    tx.Rollback();
                                    Err(c,400,"Old and new items must be different");
                                    return;
                                }
                                bool oldAffectsInventory=false, newAffectsInventory=false;
                                using(var affectsCmd=new SqlCommand("SELECT AffectsInventory FROM dbo.Item WHERE ItemId=@Id",con,tx)){
                                    affectsCmd.Parameters.Add("@Id",SqlDbType.Int).Value=oldItemId;
                                    object affectsValue=affectsCmd.ExecuteScalar();
                                    if(affectsValue==null||affectsValue==DBNull.Value){tx.Rollback();Err(c,400,"Old item not found");return;}
                                    oldAffectsInventory=Convert.ToBoolean(affectsValue);
                                }
                                using(var affectsCmd=new SqlCommand("SELECT AffectsInventory FROM dbo.Item WHERE ItemId=@Id",con,tx)){
                                    affectsCmd.Parameters.Add("@Id",SqlDbType.Int).Value=newItemId.Value;
                                    object affectsValue=affectsCmd.ExecuteScalar();
                                    if(affectsValue==null||affectsValue==DBNull.Value){tx.Rollback();Err(c,400,"Replacement item not found");return;}
                                    newAffectsInventory=Convert.ToBoolean(affectsValue);
                                }
                                if(!oldAffectsInventory||!newAffectsInventory){
                                    tx.Rollback();
                                    Err(c,400,"Replacement items must affect inventory stock");
                                    return;
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
                                            throw new Exception("Insufficient stock. Item has " + currentStock + " in stock, but " + quantity + " requested.");
                                        }
                                    }
                                }

                                // Inventory OUT for new item
                                using(var invCmd=new SqlCommand("INSERT dbo.Inventory(ItemId,EntryType,Quantity,DatePosted,PostedBy,Description)VALUES(@ItemId,'Negative',@Qty,SYSUTCDATETIME(),@Uid,@Desc)",con,tx)){
                                    invCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=newItemId.Value;
                                    invCmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                    invCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    invCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value="Call ticket #"+ticketId+" resolution - new item allocation";
                                    invCmd.ExecuteNonQuery();
                                }

                                bool isUnrepaired=oldItemRepairAction.Equals("Unrepaired",StringComparison.OrdinalIgnoreCase);
                                // Old item condition and stock mirror the desktop transaction.
                                using(var oldUpd=new SqlCommand("UPDATE dbo.Item SET ConditionId=@CondId,Remarks=CASE WHEN @Remarks IS NULL OR LTRIM(RTRIM(@Remarks))='' THEN Remarks ELSE @Remarks END,StockOnHand=CASE WHEN @Unrepaired=1 THEN 0 ELSE ISNULL(StockOnHand,0)+@Qty END WHERE ItemId=@OldId",con,tx)){
                                    oldUpd.Parameters.Add("@OldId",SqlDbType.Int).Value=oldItemId;
                                    oldUpd.Parameters.Add("@CondId",SqlDbType.Int).Value=oldItemConditionId.Value;
                                    oldUpd.Parameters.Add("@Remarks",SqlDbType.NVarChar).Value=(object)oldItemConditionRemarks??DBNull.Value;
                                    oldUpd.Parameters.Add("@Unrepaired",SqlDbType.Bit).Value=isUnrepaired;
                                    oldUpd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                    oldUpd.ExecuteNonQuery();
                                }

                                if(!isUnrepaired){
                                    // Inventory IN for old item pullout
                                    string invDesc=isSpare?"Call ticket #"+ticketId+" resolution - old unit to spare inventory":"Call ticket #"+ticketId+" resolution - old unit pullout (repaired)";
                                    using(var invCmd=new SqlCommand("INSERT dbo.Inventory(ItemId,EntryType,Quantity,DatePosted,PostedBy,Description)VALUES(@ItemId,'Positive',@Qty,SYSUTCDATETIME(),@Uid,@Desc)",con,tx)){
                                        invCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=oldItemId;
                                        invCmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity;
                                        invCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                        invCmd.Parameters.Add("@Desc",SqlDbType.NVarChar).Value=invDesc;
                                        invCmd.ExecuteNonQuery();
                                    }
                                }

                                // Canonical replacement history fields used by desktop reports.
                                string oldText="", newText="";
                                using(var textCmd=new SqlCommand("SELECT ISNULL(Name,'') + CASE WHEN ISNULL(ModelNumber,'')='' THEN '' ELSE ' ('+ModelNumber+')' END FROM dbo.Item WHERE ItemId=@Id",con,tx)){
                                    textCmd.Parameters.Add("@Id",SqlDbType.Int).Value=oldItemId;
                                    object textValue=textCmd.ExecuteScalar();
                                    oldText=textValue==null||textValue==DBNull.Value?"":textValue.ToString();
                                    textCmd.Parameters.Clear();
                                    textCmd.Parameters.Add("@Id",SqlDbType.Int).Value=newItemId.Value;
                                    textValue=textCmd.ExecuteScalar();
                                    newText=textValue==null||textValue==DBNull.Value?"":textValue.ToString();
                                }
                                using(var hCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,Note,ChangedByUserId)VALUES(@Tid,'ResolutionType','Replacement',@Note,@Uid)",con,tx)){
                                    hCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    hCmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=(object)remarks??DBNull.Value;
                                    hCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    hCmd.ExecuteNonQuery();
                                }
                                using(var hCmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,ChangedByUserId)VALUES(@Tid,@Field,@Value,@Uid)",con,tx)){
                                    hCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    hCmd.Parameters.Add("@Field",SqlDbType.NVarChar).Value="ReplacementOldItemId";
                                    hCmd.Parameters.Add("@Value",SqlDbType.NVarChar).Value=oldItemId.ToString();
                                    hCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    hCmd.ExecuteNonQuery();
                                    hCmd.Parameters["@Field"].Value="ReplacementNewItemId"; hCmd.Parameters["@Value"].Value=newItemId.Value.ToString(); hCmd.ExecuteNonQuery();
                                    hCmd.Parameters["@Field"].Value="ReplacementQty"; hCmd.Parameters["@Value"].Value=quantity.ToString(); hCmd.ExecuteNonQuery();
                                    hCmd.Parameters["@Field"].Value="ReplacementOldItemConditionId"; hCmd.Parameters["@Value"].Value=oldItemConditionId.Value.ToString(); hCmd.ExecuteNonQuery();
                                    hCmd.Parameters["@Field"].Value="ReplacementOldItemRepairAction"; hCmd.Parameters["@Value"].Value=oldItemRepairAction; hCmd.ExecuteNonQuery();
                                }
                                string replacementNote="Replacement swap completed.\r\nOld: "+oldText+"\r\nNew: "+newText+"\r\nOld ConditionId: "+oldItemConditionId.Value+"\r\nQty: "+quantity+(string.IsNullOrWhiteSpace(remarks)?"":"\r\n\r\nRemarks: "+remarks);
                                using(var nCmd=new SqlCommand("INSERT dbo.CallTicketNote(TicketId,NoteType,NoteText,CreatedByUserId)VALUES(@Tid,'Replacement',@Note,@Uid)",con,tx)){
                                    nCmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
                                    nCmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=replacementNote;
                                    nCmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    nCmd.ExecuteNonQuery();
                                }

                                // Desktop-compatible side effects are best effort. Each helper uses its own savepoint.
                                string swappedSetCode=null;
                                try{TrySwapReplacementInActiveSet(con,tx,oldItemId,newItemId.Value,quantity,ticketId,userId,oldItemRepairAction,out swappedSetCode);}catch{}
                                try{TryLogReplacementAudit(con,tx,ticketId,oldItemId,newItemId.Value,quantity,oldText,newText,oldItemRepairAction,resDept,resPerson,userId,swappedSetCode);}catch{}
                                try{TryLogReplacementRepairHistory(con,tx,ticketId,oldItemId,oldItemConditionId.Value,oldItemConditionRemarks,oldItemRepairAction,userId,swappedSetCode);}catch{}

                                // Status update
                                string newStatus=isForwardedToRepair?parentOutcome:(isTemporary?"Resolved (Temporary)":"Solved");
                                using(var sCmd=new SqlCommand("dbo.sp_Call_SetTicketStatus",con,tx){CommandType=CommandType.StoredProcedure}){
                                    sCmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                                    sCmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=newStatus;
                                    sCmd.Parameters.Add("@ChangedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                                    sCmd.ExecuteNonQuery();
                                }
                            }

                            tx.Commit();
                            string msg=isForwardedToRepair
                                ? "Repair Ticket "+(forwardedRepairTicketCode??forwardedRepairTicketId.Value.ToString())+" remains linked. IT Call outcome: "+parentOutcome+"."
                                : (resolutionType=="Service Only"
                                    ? (isTemporary?"Ticket resolved as Service Only (Temporary).":"Ticket resolved as Service Only.")
                                    : (isTemporary?"Ticket resolved as Replacement (Temporary).":"Ticket resolved as Replacement."));
                            Ok(c,new{success=true,message=msg,repairTicket=isForwardedToRepair?new{repairTicketId=forwardedRepairTicketId,ticketCode=forwardedRepairTicketCode}:null,parentOutcome=isForwardedToRepair?parentOutcome:null});
                            // Mirror the desktop resolve path, which notifies on every resolution.
                            // resolutionStatus covers Service Only, Replacement and forwarded outcomes;
                            // currentTicketStatus is the pre-resolution status read at request start.
                            ItcmActionEmailHelper.TrySendStatusUpdateEmailAsync(Cs(),ticketId,currentTicketStatus,resolutionStatus,remarks,userId);
                        }catch(Exception){
                            try{tx.Rollback();}catch{}
                            Err(c,503,"Resolution could not be completed");
                        }
                    }
                    return;
                }
            }
        } catch { Err(c,503,"Action could not be completed"); }

    }

    static bool TableExists(SqlConnection con, string name) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@Name,'U') IS NULL THEN 0 ELSE 1 END",con)){
            cmd.Parameters.Add("@Name",SqlDbType.NVarChar).Value=name;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    static bool ColumnExists(SqlConnection con, string tableName, string columnName) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN COL_LENGTH(@TableName,@ColumnName) IS NULL THEN 0 ELSE 1 END",con)){
            cmd.Parameters.Add("@TableName",SqlDbType.NVarChar).Value=tableName;
            cmd.Parameters.Add("@ColumnName",SqlDbType.NVarChar).Value=columnName;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    static bool TableExistsTx(SqlConnection con, SqlTransaction tx, string name) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@Name,'U') IS NULL THEN 0 ELSE 1 END",con,tx)){
            cmd.Parameters.Add("@Name",SqlDbType.NVarChar).Value=name;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    static bool ColumnExistsTx(SqlConnection con, SqlTransaction tx, string tableName, string columnName) {
        using(var cmd=new SqlCommand("SELECT CASE WHEN COL_LENGTH(@TableName,@ColumnName) IS NULL THEN 0 ELSE 1 END",con,tx)){
            cmd.Parameters.Add("@TableName",SqlDbType.NVarChar).Value=tableName;
            cmd.Parameters.Add("@ColumnName",SqlDbType.NVarChar).Value=columnName;
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }
    }

    static void InsertReplacementHistory(SqlConnection con, SqlTransaction tx, int ticketId, int? userId, string fieldName, string value, string note) {
        if(!TableExistsTx(con,tx,"dbo.CallTicketHistory")) return;
        using(var cmd=new SqlCommand("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue,Note,ChangedByUserId)VALUES(@Tid,@Field,@Value,@Note,@Uid)",con,tx)){
            cmd.Parameters.Add("@Tid",SqlDbType.Int).Value=ticketId;
            cmd.Parameters.Add("@Field",SqlDbType.NVarChar).Value=fieldName;
            cmd.Parameters.Add("@Value",SqlDbType.NVarChar).Value=(object)value??DBNull.Value;
            cmd.Parameters.Add("@Note",SqlDbType.NVarChar).Value=(object)note??DBNull.Value;
            cmd.Parameters.Add("@Uid",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
            cmd.ExecuteNonQuery();
        }
    }

    static void TrySwapReplacementInActiveSet(SqlConnection con, SqlTransaction tx, int fromItemId, int toItemId, int quantity, int ticketId, int? userId, string actionLabel, out string setCode) {
        setCode=null;
        if(fromItemId<=0||toItemId<=0||fromItemId==toItemId) return;
        if(quantity<1) quantity=1;
        if(!TableExistsTx(con,tx,"dbo.Set")) return;

        bool hasRequest=TableExistsTx(con,tx,"dbo.Request");
        bool hasSetItem=TableExistsTx(con,tx,"dbo.SetItem");
        bool hasArchive=TableExistsTx(con,tx,"dbo.ArchiveStatus");
        bool hasActive=ColumnExistsTx(con,tx,"dbo.Set","Active");
        bool hasCreated=ColumnExistsTx(con,tx,"dbo.Set","CreatedAt");
        bool hasCode=ColumnExistsTx(con,tx,"dbo.Set","SetCode");
        if(!hasRequest&&!hasSetItem) return;

        string activeWhere=hasActive?" AND s.Active=1":"";
        string archiveJoin=hasArchive?" LEFT JOIN dbo.ArchiveStatus a ON a.EntityType='Set' AND a.EntityId=s.SetId AND a.IsArchived=1":"";
        string archiveWhere=hasArchive?" AND a.EntityId IS NULL":"";
        string codeSelect=hasCode?"s.SetCode":"CAST(NULL AS nvarchar(50))";
        string orderBy=hasCreated?"s.CreatedAt DESC,s.SetId DESC":"s.SetId DESC";
        int setId=0;

        string requestSql="SELECT TOP 1 s.SetId,"+codeSelect+" AS SetCode FROM dbo.Request r INNER JOIN dbo.[Set] s ON s.SetId=r.SetId"+archiveJoin+" WHERE r.ItemId=@ItemId AND r.SetId IS NOT NULL"+activeWhere+archiveWhere+" ORDER BY "+orderBy;
        if(hasRequest){
            using(var cmd=new SqlCommand(requestSql,con,tx)){
                cmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=fromItemId;
                using(var r=cmd.ExecuteReader()){
                    if(r.Read()){setId=Convert.ToInt32(r[0]);setCode=r[1]==DBNull.Value?null:r[1].ToString();}
                }
            }
        }
        if(setId<=0&&hasSetItem){
            string setItemSql="SELECT TOP 1 s.SetId,"+codeSelect+" AS SetCode FROM dbo.SetItem si INNER JOIN dbo.[Set] s ON s.SetId=si.SetId"+archiveJoin+" WHERE si.ItemId=@ItemId"+activeWhere+archiveWhere+" ORDER BY "+orderBy;
            using(var cmd=new SqlCommand(setItemSql,con,tx)){
                cmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=fromItemId;
                using(var r=cmd.ExecuteReader()){
                    if(r.Read()){setId=Convert.ToInt32(r[0]);setCode=r[1]==DBNull.Value?null:r[1].ToString();}
                }
            }
        }
        if(setId<=0) return;

        tx.Save("CallMonitoring_SetSwap");
        try{
            int expectedRequests=0,expectedSetItems=0,updatedRequests=0,updatedSetItems=0;
            if(hasRequest){
                using(var countCmd=new SqlCommand("SELECT COUNT(1) FROM dbo.Request WHERE SetId=@SetId AND ItemId=@ItemId",con,tx)){
                    countCmd.Parameters.Add("@SetId",SqlDbType.Int).Value=setId; countCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=fromItemId; expectedRequests=Convert.ToInt32(countCmd.ExecuteScalar());
                }
                bool hasDateModified=ColumnExistsTx(con,tx,"dbo.Request","DateModified");
                bool hasModifiedBy=ColumnExistsTx(con,tx,"dbo.Request","ModifiedBy");
                string auditSet=(hasDateModified&&hasModifiedBy)?",DateModified=SYSUTCDATETIME(),ModifiedBy=@UserId":"";
                string sql=";WITH Target AS (SELECT TOP (@Qty) r.ReqId FROM dbo.Request r WHERE r.SetId=@SetId AND r.ItemId=@FromId ORDER BY r.ReqId DESC) UPDATE r SET r.ItemId=@ToId"+auditSet+" FROM dbo.Request r INNER JOIN Target t ON t.ReqId=r.ReqId;";
                using(var cmd=new SqlCommand(sql,con,tx)){
                    cmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity; cmd.Parameters.Add("@SetId",SqlDbType.Int).Value=setId; cmd.Parameters.Add("@FromId",SqlDbType.Int).Value=fromItemId; cmd.Parameters.Add("@ToId",SqlDbType.Int).Value=toItemId;
                    if(hasDateModified&&hasModifiedBy) cmd.Parameters.Add("@UserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;
                    updatedRequests=cmd.ExecuteNonQuery();
                }
            }
            if(hasSetItem){
                using(var countCmd=new SqlCommand("SELECT COUNT(1) FROM dbo.SetItem WHERE SetId=@SetId AND ItemId=@ItemId",con,tx)){
                    countCmd.Parameters.Add("@SetId",SqlDbType.Int).Value=setId; countCmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=fromItemId; expectedSetItems=Convert.ToInt32(countCmd.ExecuteScalar());
                }
                const string sql=";WITH Target AS (SELECT TOP (@Qty) si.SetItemId FROM dbo.SetItem si WHERE si.SetId=@SetId AND si.ItemId=@FromId ORDER BY si.SetItemId DESC) UPDATE si SET si.ItemId=@ToId FROM dbo.SetItem si INNER JOIN Target t ON t.SetItemId=si.SetItemId;";
                using(var cmd=new SqlCommand(sql,con,tx)){
                    cmd.Parameters.Add("@Qty",SqlDbType.Int).Value=quantity; cmd.Parameters.Add("@SetId",SqlDbType.Int).Value=setId; cmd.Parameters.Add("@FromId",SqlDbType.Int).Value=fromItemId; cmd.Parameters.Add("@ToId",SqlDbType.Int).Value=toItemId; updatedSetItems=cmd.ExecuteNonQuery();
                }
            }
            if((expectedRequests>0&&updatedRequests<=0)||(expectedSetItems>0&&updatedSetItems<=0)){
                tx.Rollback("CallMonitoring_SetSwap");
                return;
            }
            InsertReplacementHistory(con,tx,ticketId,userId,"ReplacementSetId",setId.ToString(),setCode);
            InsertReplacementHistory(con,tx,ticketId,userId,"ReplacementSetSwapUpdatedRequests",updatedRequests.ToString(),null);
            InsertReplacementHistory(con,tx,ticketId,userId,"ReplacementSetSwapUpdatedSetItems",updatedSetItems.ToString(),null);
            InsertReplacementHistory(con,tx,ticketId,userId,"ReplacementSetSwapOutcome",(updatedRequests>0||updatedSetItems>0)?"Swapped":"NoRowsUpdated",null);
            if(!string.IsNullOrWhiteSpace(actionLabel)) InsertReplacementHistory(con,tx,ticketId,userId,"ReplacementOldUnitAction",actionLabel.Trim(),null);
            try{
                using(var noteCmd=new SqlCommand("dbo.sp_Call_AddTicketNote",con,tx)){noteCmd.CommandType=CommandType.StoredProcedure; noteCmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId; noteCmd.Parameters.Add("@NoteType",SqlDbType.NVarChar).Value="Replacement"; noteCmd.Parameters.Add("@NoteText",SqlDbType.NVarChar).Value="Set updated: "+(setCode??setId.ToString())+" (swap ItemId "+fromItemId+" -> "+toItemId+")"; noteCmd.Parameters.Add("@CreatedByUserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value; noteCmd.ExecuteNonQuery();
                }
            }catch{}
        }catch{try{tx.Rollback("CallMonitoring_SetSwap");}catch{} throw;}
    }

    static string GetScalarStringTx(SqlConnection con, SqlTransaction tx, string sql, string parameter, int value) {
        using(var cmd=new SqlCommand(sql,con,tx)){cmd.Parameters.Add(parameter,SqlDbType.Int).Value=value; object v=cmd.ExecuteScalar(); return v==null||v==DBNull.Value?null:v.ToString();}
    }

    static void TryLogReplacementAudit(SqlConnection con, SqlTransaction tx, int ticketId, int oldItemId, int newItemId, int quantity, string oldText, string newText, string repairAction, string department, string responsiblePerson, int? userId, string setCode) {
        if(!TableExistsTx(con,tx,"dbo.ItemAuditTrail")) return;
        tx.Save("CallMonitoring_ReplacementAudit");
        try{
            string oldSerial=GetScalarStringTx(con,tx,"SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId=@Id","@Id",oldItemId);
            string newSerial=GetScalarStringTx(con,tx,"SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId=@Id","@Id",newItemId);
            string createdBy=null;
            if(userId.HasValue&&TableExistsTx(con,tx,"dbo.User")) createdBy=GetScalarStringTx(con,tx,"SELECT TOP 1 Name FROM dbo.[User] WHERE UserId=@Id","@Id",userId.Value);
            if(string.IsNullOrWhiteSpace(createdBy)) createdBy=userId.HasValue?userId.Value.ToString():"System";
            const string sql="INSERT dbo.ItemAuditTrail(ItemId,SerialNumber,Action,ActionTime,EmployeeName,DepartmentName,Direction,Status,ReferenceType,ReferenceId,SetCode,Notes,CreatedBy)VALUES(@ItemId,@Serial,@Action,@Time,@Employee,@Department,@Direction,@Status,@ReferenceType,@ReferenceId,@SetCode,@Notes,@CreatedBy)";
            using(var cmd=new SqlCommand(sql,con,tx)){
                cmd.Parameters.Add("@ItemId",SqlDbType.Int); cmd.Parameters.Add("@Serial",SqlDbType.NVarChar); cmd.Parameters.Add("@Action",SqlDbType.VarChar); cmd.Parameters.Add("@Time",SqlDbType.DateTime).Value=DateTime.UtcNow; cmd.Parameters.Add("@Employee",SqlDbType.VarChar); cmd.Parameters.Add("@Department",SqlDbType.VarChar); cmd.Parameters.Add("@Direction",SqlDbType.VarChar); cmd.Parameters.Add("@Status",SqlDbType.VarChar); cmd.Parameters.Add("@ReferenceType",SqlDbType.VarChar).Value="CallTicket"; cmd.Parameters.Add("@ReferenceId",SqlDbType.Int).Value=ticketId; cmd.Parameters.Add("@SetCode",SqlDbType.VarChar); cmd.Parameters.Add("@Notes",SqlDbType.Text); cmd.Parameters.Add("@CreatedBy",SqlDbType.VarChar).Value=createdBy;
                cmd.Parameters["@ItemId"].Value=oldItemId; cmd.Parameters["@Serial"].Value=(object)oldSerial??DBNull.Value; cmd.Parameters["@Action"].Value="Call Ticket Replacement Pullout"; cmd.Parameters["@Employee"].Value=(object)responsiblePerson??DBNull.Value; cmd.Parameters["@Department"].Value=(object)department??DBNull.Value; cmd.Parameters["@Direction"].Value="IN"; cmd.Parameters["@Status"].Value=(object)repairAction??DBNull.Value; cmd.Parameters["@SetCode"].Value=(object)setCode??DBNull.Value; cmd.Parameters["@Notes"].Value="Pulled out for replacement | Qty "+quantity+" | Old: "+(oldText??""); cmd.ExecuteNonQuery();
                cmd.Parameters["@ItemId"].Value=newItemId; cmd.Parameters["@Serial"].Value=(object)newSerial??DBNull.Value; cmd.Parameters["@Action"].Value="Call Ticket Replacement Allocation"; cmd.Parameters["@Direction"].Value="OUT"; cmd.Parameters["@Status"].Value="Completed"; cmd.Parameters["@Notes"].Value="Allocated as replacement | Qty "+quantity+" | New: "+(newText??"")+" | Replaced: "+(oldText??""); cmd.ExecuteNonQuery();
            }
        }catch{try{tx.Rollback("CallMonitoring_ReplacementAudit");}catch{} }
    }

    static void TryLogReplacementRepairHistory(SqlConnection con, SqlTransaction tx, int ticketId, int itemId, int conditionId, string remarks, string repairAction, int? userId, string setCode) {
        if(!TableExistsTx(con,tx,"dbo.ItemRepairHistory")||!TableExistsTx(con,tx,"dbo.SetItemUpdate")) return;
        string serial=GetScalarStringTx(con,tx,"SELECT TOP 1 SerialNumber FROM dbo.Item WHERE ItemId=@Id","@Id",itemId);
        if(string.IsNullOrWhiteSpace(serial)) return;
        string conditionName=null;
        if(TableExistsTx(con,tx,"dbo.Condition")) conditionName=GetScalarStringTx(con,tx,"SELECT TOP 1 ConditionName FROM dbo.[Condition] WHERE ConditionID=@Id","@Id",conditionId);
        if(string.IsNullOrWhiteSpace(conditionName)) conditionName=conditionId.ToString();
        string safeSetCode=string.IsNullOrWhiteSpace(setCode)?"TCK-"+ticketId:setCode;
        string safeRemark=string.IsNullOrWhiteSpace(remarks)?"Call ticket replacement pullout (Ticket "+ticketId+")":"Call ticket replacement pullout (Ticket "+ticketId+") | "+remarks.Trim();
        tx.Save("CallMonitoring_RepairBridge");
        try{
            string updateSql="INSERT dbo.SetItemUpdate(SetId,SetCode,ItemId,SerialNumber,ModelNumber,PreviousStatus,NewStatus,Remark,UpdatedByUserId,UpdatedByName,Source,Processed,ProcessedBy,ProcessedAt)VALUES(NULL,@SetCode,@ItemId,@Serial,NULL,'Replacement Pullout',@NewStatus,@Remark,@UserId,NULL,'CallMonitoring',1,NULL,SYSUTCDATETIME());SELECT CAST(SCOPE_IDENTITY() AS INT);";
            int updateId;
            using(var cmd=new SqlCommand(updateSql,con,tx)){cmd.Parameters.Add("@SetCode",SqlDbType.NVarChar).Value=safeSetCode;cmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=itemId;cmd.Parameters.Add("@Serial",SqlDbType.NVarChar).Value=serial;cmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=repairAction;cmd.Parameters.Add("@Remark",SqlDbType.NVarChar).Value=safeRemark;cmd.Parameters.Add("@UserId",SqlDbType.NVarChar).Value=userId==null?(object)DBNull.Value:userId.Value.ToString();updateId=Convert.ToInt32(cmd.ExecuteScalar());}
            const string historySql="INSERT dbo.ItemRepairHistory(UpdateId,ItemId,SerialNumber,SetId,SetCode,PreviousStatus,NewStatus,ConditionId,ConditionName,RepairAction,Remark,CreatedAt,ProcessedByUserId,ProcessedByName)VALUES(@UpdateId,@ItemId,@Serial,NULL,@SetCode,'Replacement Pullout',@NewStatus,@ConditionId,@ConditionName,@RepairAction,@Remark,SYSUTCDATETIME(),@UserId,NULL);";
            using(var cmd=new SqlCommand(historySql,con,tx)){cmd.Parameters.Add("@UpdateId",SqlDbType.Int).Value=updateId;cmd.Parameters.Add("@ItemId",SqlDbType.Int).Value=itemId;cmd.Parameters.Add("@Serial",SqlDbType.NVarChar).Value=serial;cmd.Parameters.Add("@SetCode",SqlDbType.NVarChar).Value=safeSetCode;cmd.Parameters.Add("@NewStatus",SqlDbType.NVarChar).Value=repairAction;cmd.Parameters.Add("@ConditionId",SqlDbType.Int).Value=conditionId;cmd.Parameters.Add("@ConditionName",SqlDbType.NVarChar).Value=conditionName;cmd.Parameters.Add("@RepairAction",SqlDbType.NVarChar).Value=repairAction;cmd.Parameters.Add("@Remark",SqlDbType.NVarChar).Value=safeRemark;cmd.Parameters.Add("@UserId",SqlDbType.Int).Value=userId==null?(object)DBNull.Value:(object)userId.Value;cmd.ExecuteNonQuery();}
        }catch{try{tx.Rollback("CallMonitoring_RepairBridge");}catch{} }
    }

    static int StatusRank(string status) {
        var s=(status??string.Empty).Trim();
        if(s.Equals("Pending",StringComparison.OrdinalIgnoreCase)) return 0;
        if(s.Equals("In Progress",StringComparison.OrdinalIgnoreCase)||s.Equals("Reopened",StringComparison.OrdinalIgnoreCase)) return 1;
        if(s.Equals("Escalated",StringComparison.OrdinalIgnoreCase)||s.Equals("Forwarded to Repair",StringComparison.OrdinalIgnoreCase)) return 2;
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
            ||s.Equals("Forwarded to Repair",StringComparison.OrdinalIgnoreCase)
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

public static class ItcmActionEmailHelper
{
    public static void TrySendAssignmentEmailAsync(string connectionString, int ticketId, int? userId)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { SendEmail(connectionString, ticketId, "Assignment", null, userId); }
            catch { }
        });
    }

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
            var employeeRecipients = ResolveEmployeeRecipients(con, GetAssignedEmployeeId(con, ticketId));
            var recipients = new List<string>();
            AddDistinct(recipients, employeeRecipients);
            AddDistinct(recipients, ResolveTicketContactRecipients(con, ticketId));
            if (recipients.Count == 0)
            { LogEmail(con, ticketId, templateType, null, null, "Skipped", "No active employee email recipient was resolved.", userId); return; }
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
        if (templateType == "Assignment") return true;
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

    private static int GetAssignedEmployeeId(SqlConnection con, int ticketId)
    {
        using (var cmd = new SqlCommand("SELECT AssignedToEmpId FROM dbo.CallTicket WHERE TicketId=@Id", con))
        {
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = ticketId;
            var value = cmd.ExecuteScalar();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }
    }

    private static List<string> ResolveEmployeeRecipients(SqlConnection con, int empId)
    {
        var result = new List<string>();
        if (empId <= 0) return result;

        using (var cmd = new SqlCommand(@"
SELECT TOP 1 ea.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsPrimary = 1
  AND ee.IsActive = 1
  AND ea.IsActive = 1
ORDER BY ee.EmployeeEmailId;", con))
        {
            cmd.Parameters.Add("@EmpId", SqlDbType.Int).Value = empId;
            var value = cmd.ExecuteScalar();
            if (value != null && value != DBNull.Value)
            {
                try { result.Add(new MailAddress(value.ToString().Trim()).Address); }
                catch { }
            }
        }

        return result;
    }

    private static List<string> ResolveTicketContactRecipients(SqlConnection con, int ticketId)
    {
        var result = new List<string>();
        if (ticketId <= 0) return result;
        try
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 ContactEmail FROM dbo.CallTicket WHERE TicketId=@Id", con))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = ticketId;
                var value = cmd.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                {
                    try { result.Add(new MailAddress(value.ToString().Trim()).Address); }
                    catch { }
                }
            }
        }
        catch (SqlException)
        {
            // ContactEmail is optional until the portal contact-email migration is installed.
        }
        return result;
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value, StringComparer.OrdinalIgnoreCase))
                target.Add(value);
        }
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
        // The view exists in all environments but older revisions lack the
        // AssignedTo/ProvidedSolution/DeptId/LastReminderSentAt columns the
        // view query selects. Require them explicitly; otherwise fall back to
        // the base table (whose columns are guaranteed) instead of throwing
        // inside the fire-and-forget email sender (which would swallow the
        // error and skip the email with no log row, e.g. TCK-000069).
        bool hasView = ObjExists(con, "dbo.vw_Call_TicketList", null)
            && ColExists(con, "dbo.vw_Call_TicketList", "AssignedTo")
            && ColExists(con, "dbo.vw_Call_TicketList", "ProvidedSolution")
            && ColExists(con, "dbo.vw_Call_TicketList", "DeptId")
            && ColExists(con, "dbo.vw_Call_TicketList", "LastReminderSentAt");
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
        string fromEmail = !string.IsNullOrWhiteSpace(cfg.FromEmail) ? cfg.FromEmail.Trim() : cfg.Username;
        if (!string.IsNullOrWhiteSpace(cfg.FromEmail))
        {
            try
            {
                var address = new MailAddress(fromEmail);
                if (!string.Equals(address.Address, fromEmail, StringComparison.OrdinalIgnoreCase))
                    throw new FormatException();
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("FromEmail is invalid. Correct the SMTP sender address or leave it blank to use the SMTP Username.");
            }
        }

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

    private static bool ColExists(SqlConnection con, string table, string column)
    {
        using (var cmd = new SqlCommand("SELECT CASE WHEN COL_LENGTH(@T,@C) IS NOT NULL THEN 1 ELSE 0 END", con))
        { cmd.Parameters.Add("@T", SqlDbType.NVarChar).Value = table; cmd.Parameters.Add("@C", SqlDbType.NVarChar).Value = column; return Convert.ToInt32(cmd.ExecuteScalar()) == 1; }
    }

    private sealed class SmtpCfg
    {
        public string Server, Username, Password, FromName, FromEmail;
        public int    Port;
        public bool   UseSsl;
    }
}
