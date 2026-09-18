using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;

public class CallTicketDetailHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}
        if(c.Request.HttpMethod!="GET"){Err(c,405,"Method not allowed");return;}

        CallTicketApiUser actor; int authStatus; string authMessage;
        if(!CallTicketApiSecurity.TryRequireIt(c, Cs(), out actor, out authStatus, out authMessage)){Err(c,authStatus,authMessage);return;}

        int ticketId;
        if(!int.TryParse(c.Request.QueryString["ticketId"]??"",out ticketId)||ticketId<=0){Err(c,400,"ticketId is required");return;}

        object ticket=null;
        var notes=new List<object>();
        var history=new List<object>();
        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                // Ticket detail
                using(var cmd=new SqlCommand(@"
SELECT t.TicketId,t.TicketCode,t.ComId,COALESCE(c.Name,'') AS Company,
       t.DeptId,COALESCE(d.Name,'') AS Department,
       t.BranchId,
       COALESCE(CASE WHEN b.BranchId IS NULL THEN NULL
                     WHEN ISNULL(b.IsCenter,0)=1 THEN b.Name+' (Center)'
                     WHEN ISNULL(b.IsDepot,0)=1 THEN b.Name+' (Depot)'
                     WHEN ISNULL(b.IsFactory,0)=1 THEN b.Name+' (Factory)'
                     WHEN ISNULL(b.IsDistributor,0)=1 THEN b.Name+' (Distributor)'
                     ELSE b.Name END,'') AS Branch,
       t.CallerName,t.Issue,t.ProvidedSolution,t.IssueType,t.Priority,t.Status,
       COALESCE(e.Name,'') AS AssignedTo,
       t.CreatedAt,t.UpdatedAt,t.SolvedAt
FROM dbo.CallTicket t
LEFT JOIN dbo.Company c ON c.ComId=t.ComId
LEFT JOIN dbo.Department d ON d.DeptId=t.DeptId
LEFT JOIN dbo.Branch b ON b.BranchId=t.BranchId
LEFT JOIN dbo.Employee e ON e.EmpId=t.AssignedToEmpId
WHERE t.TicketId=@TicketId",con)){
                    cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                    using(var r=cmd.ExecuteReader()){
                        if(r.Read()){
                            ticket=new{
                                ticketId=r["TicketId"]==DBNull.Value?0:Convert.ToInt32(r["TicketId"]),
                                ticketCode=r["TicketCode"]==DBNull.Value?(string)null:r["TicketCode"].ToString(),
                                company=r["Company"]==DBNull.Value?(string)null:r["Company"].ToString(),
                                department=r["Department"]==DBNull.Value?(string)null:r["Department"].ToString(),
                                branch=r["Branch"]==DBNull.Value?(string)null:r["Branch"].ToString(),
                                callerName=r["CallerName"]==DBNull.Value?(string)null:r["CallerName"].ToString(),
                                issue=r["Issue"]==DBNull.Value?(string)null:r["Issue"].ToString(),
                                providedSolution=r["ProvidedSolution"]==DBNull.Value?(string)null:r["ProvidedSolution"].ToString(),
                                issueType=r["IssueType"]==DBNull.Value?(string)null:r["IssueType"].ToString(),
                                priority=r["Priority"]==DBNull.Value?(string)null:r["Priority"].ToString(),
                                status=r["Status"]==DBNull.Value?(string)null:r["Status"].ToString(),
                                assignedTo=r["AssignedTo"]==DBNull.Value?(string)null:r["AssignedTo"].ToString(),
                                createdAt=r["CreatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["CreatedAt"]).ToString("yyyy-MM-dd HH:mm"),
                                updatedAt=r["UpdatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["UpdatedAt"]).ToString("yyyy-MM-dd HH:mm"),
                                solvedAt=r["SolvedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["SolvedAt"]).ToString("yyyy-MM-dd HH:mm")
                            };
                        }
                    }
                }
                if(ticket==null){Err(c,404,"Ticket not found");return;}

                // Notes
                using(var cmd=new SqlCommand(@"
SELECT n.NoteId,n.NoteType,n.NoteText,n.CreatedByUserId,COALESCE(e.Name,'System') AS CreatedBy,n.CreatedAt
FROM dbo.CallTicketNote n
LEFT JOIN dbo.[User] u ON u.UserId=n.CreatedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId=u.EmpId
WHERE n.TicketId=@TicketId
ORDER BY n.CreatedAt DESC",con)){
                    cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                    using(var r=cmd.ExecuteReader()){
                        while(r.Read()){
                            notes.Add(new{
                                noteId=r["NoteId"]==DBNull.Value?0:Convert.ToInt32(r["NoteId"]),
                                noteType=r["NoteType"]==DBNull.Value?(string)null:r["NoteType"].ToString(),
                                noteText=r["NoteText"]==DBNull.Value?(string)null:r["NoteText"].ToString(),
                                createdBy=r["CreatedBy"]==DBNull.Value?(string)null:r["CreatedBy"].ToString(),
                                createdAt=r["CreatedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["CreatedAt"]).ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }

                // History
                using(var cmd=new SqlCommand(@"
SELECT h.HistoryId,h.FieldName,h.OldValue,h.NewValue,h.ChangedByUserId,COALESCE(e.Name,'System') AS ChangedBy,h.ChangedAt
FROM dbo.CallTicketHistory h
LEFT JOIN dbo.[User] u ON u.UserId=h.ChangedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId=u.EmpId
WHERE h.TicketId=@TicketId
ORDER BY h.ChangedAt DESC",con)){
                    cmd.Parameters.Add("@TicketId",SqlDbType.Int).Value=ticketId;
                    using(var r=cmd.ExecuteReader()){
                        while(r.Read()){
                            history.Add(new{
                                historyId=r["HistoryId"]==DBNull.Value?0:Convert.ToInt32(r["HistoryId"]),
                                fieldName=r["FieldName"]==DBNull.Value?(string)null:r["FieldName"].ToString(),
                                oldValue=r["OldValue"]==DBNull.Value?(string)null:r["OldValue"].ToString(),
                                newValue=r["NewValue"]==DBNull.Value?(string)null:r["NewValue"].ToString(),
                                changedBy=r["ChangedBy"]==DBNull.Value?(string)null:r["ChangedBy"].ToString(),
                                changedAt=r["ChangedAt"]==DBNull.Value?(string)null:Convert.ToDateTime(r["ChangedAt"]).ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
            }
        } catch(Exception ex){Err(c,500,"Failed to load ticket detail: "+ex.Message);return;}
        Ok(c,new{success=true,ticket=ticket,notes=notes,history=history});
    }

    public bool IsReusable{get{return false;}}
}
