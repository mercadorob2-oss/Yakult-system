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

public class CallTicketsHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,POST,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}

        if(c.Request.HttpMethod=="GET"){
            CallTicketApiUser actor; int authStatus; string authMessage;
            if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)) { Err(c,authStatus,authMessage); return; }
            HandleGet(c);
            return;
        }
        if(c.Request.HttpMethod=="POST"){HandlePost(c);return;}
        Err(c,405,"Method not allowed");
    }

    void HandleGet(HttpContext c) {
        var status=(c.Request.QueryString["status"]??"").Trim();
        var search=(c.Request.QueryString["search"]??"").Trim();
        var priority=(c.Request.QueryString["priority"]??"").Trim();
        var issueType=(c.Request.QueryString["issueType"]??"").Trim();
        int page; int.TryParse(c.Request.QueryString["page"]??"1",out page); if(page<1)page=1;
        int pageSize; int.TryParse(c.Request.QueryString["pageSize"]??"25",out pageSize); if(pageSize<1||pageSize>100)pageSize=25;
        int offset=(page-1)*pageSize;

        var tickets=new List<object>();
        int totalCount=0;
        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                using(var cmd=new SqlCommand()){
                    cmd.Connection=con;
                    var sql=@"SELECT v.*, COUNT(*) OVER() AS TotalCount FROM dbo.vw_Call_TicketList v WHERE 1=1";
                    if(status=="pending") sql+=" AND v.Status NOT IN ('Solved','Resolved (Temporary)','Closed')";
                    else if(status=="solved") sql+=" AND v.Status IN ('Solved','Resolved (Temporary)','Closed')";
                    if(!string.IsNullOrEmpty(search)){
                        sql+=" AND (v.TicketCode LIKE @Search OR v.Issue LIKE @Search OR v.CallerName LIKE @Search OR v.Company LIKE @Search OR v.Department LIKE @Search OR v.Branch LIKE @Search)";
                        cmd.Parameters.Add("@Search",SqlDbType.NVarChar).Value="%"+search+"%";
                    }
                    if(!string.IsNullOrEmpty(priority)){sql+=" AND v.Priority=@Priority";cmd.Parameters.Add("@Priority",SqlDbType.NVarChar).Value=priority;}
                    if(!string.IsNullOrEmpty(issueType)){sql+=" AND v.IssueType=@IssueType";cmd.Parameters.Add("@IssueType",SqlDbType.NVarChar).Value=issueType;}
                    sql+=" ORDER BY v.CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                    cmd.Parameters.Add("@Offset",SqlDbType.Int).Value=offset;
                    cmd.Parameters.Add("@PageSize",SqlDbType.Int).Value=pageSize;
                    cmd.CommandText=sql;
                    using(var r=cmd.ExecuteReader()){
                        while(r.Read()){
                            if(totalCount==0) totalCount=r["TotalCount"]==DBNull.Value?0:Convert.ToInt32(r["TotalCount"]);
                            tickets.Add(new{
                                ticketId=r["TicketId"]==DBNull.Value?0:Convert.ToInt32(r["TicketId"]),
                                ticketCode=r["TicketCode"]==DBNull.Value?(string)null:r["TicketCode"].ToString(),
                                company=r["Company"]==DBNull.Value?(string)null:r["Company"].ToString(),
                                issue=r["Issue"]==DBNull.Value?(string)null:r["Issue"].ToString(),
                                department=r["Department"]==DBNull.Value?(string)null:r["Department"].ToString(),
                                branch=r["Branch"]==DBNull.Value?(string)null:r["Branch"].ToString(),
                                responsiblePerson=r["ResponsiblePerson"]==DBNull.Value?(string)null:r["ResponsiblePerson"].ToString(),
                                status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString(),
                                priority=r["Priority"]==DBNull.Value?(string)null:r["Priority"].ToString(),
                                callerName=r["CallerName"]==DBNull.Value?(string)null:r["CallerName"].ToString(),
                                issueType=r["IssueType"]==DBNull.Value?(string)null:r["IssueType"].ToString(),
                                createdAt=r["CreatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["CreatedAt"]).ToString("yyyy-MM-dd HH:mm"),
                                solvedAt=r["SolvedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["SolvedAt"]).ToString("yyyy-MM-dd HH:mm"),
                                ticketAgeDays=r["TicketAgeDays"]==DBNull.Value?0:Convert.ToInt32(r["TicketAgeDays"]),
                                idleDays=r["IdleDays"]==DBNull.Value?0:Convert.ToInt32(r["IdleDays"]),
                                lastContactAt=r["LastContactAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["LastContactAt"]).ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
            }
        } catch(Exception ex){Err(c,500,"Failed to load tickets: "+ex.Message);return;}
        Ok(c,new{success=true,tickets=tickets,totalCount=totalCount,page=page,pageSize=pageSize});
    }

    void HandlePost(HttpContext c) {
        var body=JsonConvert.DeserializeObject<Dictionary<string,object>>(new System.IO.StreamReader(c.Request.InputStream).ReadToEnd());
        if(body==null){Err(c,400,"Invalid request body");return;}

        string requestedSource=body.ContainsKey("ticketSource")&&body["ticketSource"]!=null?body["ticketSource"].ToString():"Portal";
        bool isCallIt=string.Equals(requestedSource,"CallIT",StringComparison.OrdinalIgnoreCase);
        CallTicketApiUser actor=null;
        if(isCallIt){
            int authStatus; string authMessage;
            if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}
        }

        string company=body.ContainsKey("company")?body["company"].ToString():"";
        string callerName=body.ContainsKey("callerName")?body["callerName"].ToString():"";
        string issue=body.ContainsKey("issue")?body["issue"].ToString():"";
        if(string.IsNullOrWhiteSpace(company)||string.IsNullOrWhiteSpace(callerName)||string.IsNullOrWhiteSpace(issue)){Err(c,400,"company, callerName and issue are required");return;}

        int? comId=null; if(body.ContainsKey("comId")&&body["comId"]!=null){int v; if(int.TryParse(body["comId"].ToString(),out v))comId=v;}
        int? deptId=null; if(body.ContainsKey("deptId")&&body["deptId"]!=null){int v; if(int.TryParse(body["deptId"].ToString(),out v))deptId=v;}
        int? branchId=null; if(body.ContainsKey("branchId")&&body["branchId"]!=null){int v; if(int.TryParse(body["branchId"].ToString(),out v))branchId=v;}
        int? assignedToEmpId=null; if(body.ContainsKey("assignedToEmpId")&&body["assignedToEmpId"]!=null){int v; if(int.TryParse(body["assignedToEmpId"].ToString(),out v))assignedToEmpId=v;}
        // The authenticated account, not the request body, owns the audit identity.
        int? createdByUserId=actor==null?(int?)null:actor.UserId;
        string providedSolution=body.ContainsKey("providedSolution")?body["providedSolution"].ToString():"";
        string issueType=body.ContainsKey("issueType")?body["issueType"].ToString():"Other";
        string priority=body.ContainsKey("priority")?body["priority"].ToString():"Medium";
        string ticketSource=isCallIt?"CallIT":"Portal";

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                if(isCallIt){
                    if(!HasParam(con,"dbo.sp_Call_CreateTicket","@TicketSource")){
                        Err(c,503,"Call IT ticket source schema is not installed in this database.");return;
                    }
                    if(assignedToEmpId.HasValue && !CallTicketApiSecurity.IsItEmployee(con,assignedToEmpId.Value)){
                        Err(c,400,"assignedToEmpId must reference an active IT employee.");return;
                    }
                }
                using(var cmd=new SqlCommand("dbo.sp_Call_CreateTicket",con)){
                    cmd.CommandType=CommandType.StoredProcedure;
                    cmd.Parameters.Add("@ComId",SqlDbType.Int).Value=comId==null?(object)DBNull.Value:(object)comId.Value;
                    cmd.Parameters.Add("@DeptId",SqlDbType.Int).Value=deptId==null?(object)DBNull.Value:(object)deptId.Value;
                    if(HasParam(con,"dbo.sp_Call_CreateTicket","@BranchId"))
                        cmd.Parameters.Add("@BranchId",SqlDbType.Int).Value=branchId==null?(object)DBNull.Value:(object)branchId.Value;
                    cmd.Parameters.Add("@CallerName",SqlDbType.NVarChar).Value=callerName;
                    cmd.Parameters.Add("@Issue",SqlDbType.NVarChar).Value=issue;
                    cmd.Parameters.Add("@ProvidedSolution",SqlDbType.NVarChar).Value=providedSolution;
                    cmd.Parameters.Add("@IssueType",SqlDbType.NVarChar).Value=issueType;
                    cmd.Parameters.Add("@Priority",SqlDbType.NVarChar).Value=priority;
                    cmd.Parameters.Add("@CreatedByUserId",SqlDbType.Int).Value=createdByUserId==null?(object)DBNull.Value:(object)createdByUserId.Value;
                    if(HasParam(con,"dbo.sp_Call_CreateTicket","@AssignedToEmpId"))
                        cmd.Parameters.Add("@AssignedToEmpId",SqlDbType.Int).Value=assignedToEmpId==null?(object)DBNull.Value:(object)assignedToEmpId.Value;
                    else
                        cmd.Parameters.Add("@AssignedToUserId",SqlDbType.Int).Value=assignedToEmpId==null?(object)DBNull.Value:(object)assignedToEmpId.Value;
                    if(HasParam(con,"dbo.sp_Call_CreateTicket","@TicketSource"))
                        cmd.Parameters.Add("@TicketSource",SqlDbType.NVarChar,20).Value=ticketSource;
                    using(var r=cmd.ExecuteReader()){
                        if(r.Read()){
                            int newTicketId=r["TicketId"]==DBNull.Value?0:Convert.ToInt32(r["TicketId"]);
                            Ok(c,new{success=true,ticket=new{
                                ticketId=newTicketId,
                                ticketCode=r["TicketCode"]==DBNull.Value?(string)null:r["TicketCode"].ToString(),
                                company=r["Company"]==DBNull.Value?(string)null:r["Company"].ToString(),
                                issue=r["Issue"]==DBNull.Value?(string)null:r["Issue"].ToString(),
                                department=r["Department"]==DBNull.Value?(string)null:r["Department"].ToString(),
                                branch=r["Branch"]==DBNull.Value?(string)null:r["Branch"].ToString(),
                                status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString(),
                                priority=r["Priority"]==DBNull.Value?(string)null:r["Priority"].ToString(),
                                callerName=r["CallerName"]==DBNull.Value?(string)null:r["CallerName"].ToString(),
                                issueType=r["IssueType"]==DBNull.Value?(string)null:r["IssueType"].ToString(),
                                createdAt=r["CreatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["CreatedAt"]).ToString("yyyy-MM-dd HH:mm")
                            }});
                            if(newTicketId>0)
                                ItcmEmailHelper.TrySendNewTicketEmailAsync(Cs(),newTicketId,createdByUserId);
                        } else {
                            Err(c,500,"Stored procedure did not return a ticket");
                        }
                    }
                }
            }
        } catch(Exception ex){Err(c,500,"Failed to create ticket: "+ex.Message);}
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
