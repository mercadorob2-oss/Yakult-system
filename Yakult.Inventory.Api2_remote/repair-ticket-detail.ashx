<%@ WebHandler Language="C#" Class="RepairTicketDetailHandler" %>
using System;
using System.Web;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using Newtonsoft.Json;

public class RepairTicketDetailHandler : IHttpHandler {
    string Cs() { return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString; }
    void Ok(HttpContext c, object d) { c.Response.StatusCode=200; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(d)); }
    void Err(HttpContext c, int s, string m) { c.Response.StatusCode=s; c.Response.ContentType="application/json"; c.Response.AddHeader("Access-Control-Allow-Origin","*"); c.Response.Write(JsonConvert.SerializeObject(new{success=false,message=m})); }

    public void ProcessRequest(HttpContext c) {
        c.Response.AddHeader("Access-Control-Allow-Origin","*");
        c.Response.AddHeader("Access-Control-Allow-Headers","Content-Type,Authorization");
        c.Response.AddHeader("Access-Control-Allow-Methods","GET,OPTIONS");
        if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=200;c.Response.End();return;}

        var ticketCode=(c.Request.QueryString["ticketCode"]??"").Trim();
        var token=(c.Request.QueryString["token"]??"").Trim();
        if(string.IsNullOrEmpty(ticketCode)&&string.IsNullOrEmpty(token)){Err(c,400,"ticketCode or token is required");return;}

        Guid? qrToken=null;
        if(!string.IsNullOrEmpty(token)){
            const string prefix="yakult:repair:v1:";
            if(token.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
                token=token.Substring(prefix.Length).Trim();
            Guid g;
            if(!Guid.TryParse(token,out g)){Err(c,400,"Invalid token format");return;}
            qrToken=g;
        }

        try {
            using(var con=new SqlConnection(Cs())){
                con.Open();
                var whereClause=qrToken.HasValue ? "t.QRToken=@Token" : "t.TicketCode=@TicketCode";

                using(var cmd=new SqlCommand(
                    "SELECT t.TicketCode,t.Status,t.DateReceived,t.CompletedAt," +
                    "i.Name AS ItemName,i.SerialNumber,i.ModelNumber,i.Category," +
                    "techEmp.Name AS TechnicianName," +
                    "COALESCE(reqCom.Name,reqEmpCo.Name,co.Name) AS CompanyName," +
                    "COALESCE(reqBranch.Name,reqEmpBr.Name,br.Name) AS BranchName," +
                    "COALESCE(reqDept.Name,reqEmpDe.Name,de.Name) AS DeptName," +
                    "COALESCE(reqEmp.Name,reqDept.Name) AS RequesterName," +
                    "rc.RootCause,rc.WorkPerformed,rc.Recommendations," +
                    "rc.Disposition,rc.DispositionExecutedAt," +
                    "repItem.Name AS ReplacementItemName,repItem.ModelNumber AS ReplacementModelNumber,repItem.SerialNumber AS ReplacementSerialNumber," +
                    "spareItem.Name AS SpareItemName,spareItem.ModelNumber AS SpareModelNumber,spareItem.SerialNumber AS SpareSerialNumber " +
                    "FROM dbo.RepairTicket t " +
                    "LEFT JOIN dbo.Item i ON i.ItemId=t.ItemId " +
                    "LEFT JOIN dbo.Employee techEmp ON techEmp.EmpId=t.AssignedTechEmpId " +
                    "LEFT JOIN dbo.Company co ON co.ComId=t.ComId " +
                    "LEFT JOIN dbo.Branch br ON br.BranchId=t.BranchId " +
                    "LEFT JOIN dbo.Department de ON de.DeptId=t.DeptId " +
                    "LEFT JOIN dbo.Department reqDept ON reqDept.DeptId=t.RequestedByDeptId " +
                    "LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId=t.RequestedByEmpId " +
                    "LEFT JOIN dbo.Company reqCom ON reqCom.ComId=t.RequestedByComId " +
                    "LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId=t.RequestedByBranchId " +
                    "LEFT JOIN dbo.Company reqEmpCo ON reqEmpCo.ComId=reqEmp.ComId " +
                    "LEFT JOIN dbo.Branch reqEmpBr ON reqEmpBr.BranchId=reqEmp.BranchId " +
                    "LEFT JOIN dbo.Department reqEmpDe ON reqEmpDe.DeptId=reqEmp.DeptId " +
                    "LEFT JOIN dbo.RepairConclusion rc ON rc.RepairTicketId=t.RepairTicketId " +
                    "LEFT JOIN dbo.Item repItem ON repItem.ItemId=rc.ReplacementItemId " +
                    "OUTER APPLY (SELECT TOP 1 bl.ItemId FROM dbo.BorrowLog bl WHERE bl.RepairTicketId=t.RepairTicketId AND bl.ReturnedAtUtc IS NULL ORDER BY bl.BorrowedAtUtc DESC) AS spare " +
                    "LEFT JOIN dbo.Item spareItem ON spareItem.ItemId=spare.ItemId " +
                    "WHERE "+whereClause,con)){
                    if(qrToken.HasValue) cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=qrToken.Value;
                    else                 cmd.Parameters.Add("@TicketCode",SqlDbType.NVarChar,50).Value=ticketCode;

                    using(var r=cmd.ExecuteReader()){
                        if(!r.Read()){Err(c,404,"Repair ticket not found");return;}

                        Func<string,string> s=col=>r[col]==DBNull.Value?(string)null:r[col].ToString();
                        Func<string,string> dt=col=>r[col]==DBNull.Value?(string)null:Convert.ToDateTime(r[col]).ToString("yyyy-MM-dd HH:mm");

                        var disposition=s("Disposition");
                        var dispositionExecuted=r["DispositionExecutedAt"]!=DBNull.Value;
                        string itemDisposition=null;
                        if(string.Equals(disposition,"Replace",StringComparison.OrdinalIgnoreCase) && dispositionExecuted)
                            itemDisposition=FormatLine("Item Replacement",s("ReplacementItemName"),s("ReplacementModelNumber"),s("ReplacementSerialNumber"));
                        var spareLine = s("SpareItemName")!=null ? FormatLine("Spare Assigned",s("SpareItemName"),s("SpareModelNumber"),s("SpareSerialNumber")) : null;
                        if(spareLine!=null) itemDisposition = itemDisposition==null ? spareLine : itemDisposition+"\n"+spareLine;

                        Ok(c,new{
                            success=true,
                            ticket=new{
                                ticket_code=s("TicketCode"),
                                item_name=s("ItemName"),
                                serial_number=s("SerialNumber"),
                                model_number=s("ModelNumber"),
                                category=s("Category"),
                                requester_name=s("RequesterName"),
                                department=s("DeptName"),
                                branch=s("BranchName"),
                                company=s("CompanyName"),
                                status=s("Status"),
                                date_received=dt("DateReceived"),
                                date_completed=dt("CompletedAt"),
                                technician_name=s("TechnicianName"),
                                root_cause=s("RootCause"),
                                work_performed=s("WorkPerformed"),
                                recommendations=s("Recommendations"),
                                item_disposition=itemDisposition
                            }
                        });
                    }
                }
            }
        } catch(Exception ex){Err(c,500,"Failed to load repair ticket: "+ex.Message);return;}
    }

    string FormatLine(string role,string name,string modelNumber,string serialNumber){
        var text=role+" — "+(name??"(unknown item)");
        var hasModel=!string.IsNullOrWhiteSpace(modelNumber);
        var hasSerial=!string.IsNullOrWhiteSpace(serialNumber);
        if(hasModel||hasSerial){
            text+=" (";
            if(hasModel) text+="Model: "+modelNumber;
            if(hasModel&&hasSerial) text+=", ";
            if(hasSerial) text+="Serial: "+serialNumber;
            text+=")";
        }
        return text;
    }

    public bool IsReusable{get{return false;}}
}
