$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$apiPath = Join-Path $repoPath 'Yakult.Inventory.Api2_remote'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('yakult-pullout-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$handler = Get-Content (Join-Path $apiPath 'call-ticket-action.ashx') -Raw
$handler = $handler.Substring($handler.IndexOf("`n") + 1)
# Test the real handler using disposable LocalDB data. Auth and email are test-only stubs.
$handler = $handler.Substring(0, $handler.IndexOf('public static class ItcmActionEmailHelper'))
$handlerFile = Join-Path $testDirectory 'Handler.cs'
[IO.File]::WriteAllText($handlerFile, $handler)
$testsFile = Join-Path $testDirectory 'Tests.cs'
@'
using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Data.SqlClient;
using System.Configuration;
using Newtonsoft.Json.Linq;
public class CallTicketApiUser { public int UserId=7; }
public static class CallTicketApiSecurity {
    public static bool TryRequireIt(HttpContext c,string cs,out CallTicketApiUser actor,out int status,out string message) {
        actor=new CallTicketApiUser(); status=200; message=""; return true;
    }
    public static bool IsItEmployee(SqlConnection con,int id) { return true; }
}
public static class ItcmActionEmailHelper {
    public static int Notifications;
    public static void TrySendAssignmentEmailAsync(string cs,int id,int? uid) { }
    public static void TrySendStatusUpdateEmailAsync(string cs,int id,string oldStatus,string newStatus,string note,int? uid) { Notifications++; }
}
public class JsonWorker : SimpleWorkerRequest {
    readonly byte[] body;
    public JsonWorker(string json,TextWriter output) : base("/",Path.GetTempPath(),"call-ticket-action.ashx","",output) { body=Encoding.UTF8.GetBytes(json); }
    public override string GetHttpVerbName() { return "POST"; }
    public override byte[] GetPreloadedEntityBody() { return body; }
    public override bool IsEntireEntityBodyIsPreloaded() { return true; }
    public override string GetKnownRequestHeader(int index) {
        if(index==HeaderContentType) return "application/json";
        if(index==HeaderContentLength) return body.Length.ToString();
        return base.GetKnownRequestHeader(index);
    }
}
public static class PullOutWorkflowTests {
    static string cs=ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"].ConnectionString;
    static int checks;
    static void Sql(string sql) { using(var c=new SqlConnection(cs)){c.Open();using(var q=new SqlCommand(sql,c))q.ExecuteNonQuery();} }
    static object Scalar(string sql) { using(var c=new SqlConnection(cs)){c.Open();using(var q=new SqlCommand(sql,c))return q.ExecuteScalar();} }
    static void Equal(object expected,object actual,string message) {
        if(!object.Equals(expected,actual)) throw new Exception(message+": expected "+expected+", got "+actual);
        checks++;
    }
    static void Post(string json,int status) {
        var output=new StringWriter();
        var context=new HttpContext(new JsonWorker(json,output));
        HttpContext.Current=context;
        new CallTicketActionHandler().ProcessRequest(context);
        context.Response.Flush();
        Equal(status,context.Response.StatusCode,"HTTP response: "+output);
        if(status==200) Equal(true,(bool)JObject.Parse(output.ToString())["success"],"Response success");
        HttpContext.Current=null;
    }
    public static void Main() {
        try { Run(); } catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
    }
    static void Run() {
        var builder=new SqlConnectionStringBuilder(cs);
        string database=builder.InitialCatalog;
        builder.InitialCatalog="master";
        using(var master=new SqlConnection(builder.ConnectionString)) {
            master.Open();
            using(var cmd=new SqlCommand("CREATE DATABASE ["+database+"]",master))cmd.ExecuteNonQuery();
            try {
                Sql(@"CREATE TABLE dbo.CallTicket(TicketId int PRIMARY KEY,TicketSource nvarchar(30),Status nvarchar(50),AssignedToEmpId int NULL,DeptId int NULL);
CREATE TABLE dbo.Department(DeptId int,Name nvarchar(100));
CREATE TABLE dbo.Employee(EmpId int,Name nvarchar(100));
CREATE TABLE dbo.CallTicketHistory(HistoryId int IDENTITY PRIMARY KEY,TicketId int,FieldName nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),Note nvarchar(max),ChangedByUserId int,ChangedAt datetime2 DEFAULT SYSUTCDATETIME());
CREATE TABLE dbo.CallTicketNote(NoteId int IDENTITY,TicketId int,NoteType nvarchar(100),NoteText nvarchar(max),CreatedByUserId int);
CREATE TABLE dbo.Item(ItemId int PRIMARY KEY,Name nvarchar(100),ModelNumber nvarchar(100),SerialNumber nvarchar(100),AffectsInventory bit,StockOnHand int,ConditionId int,Remarks nvarchar(max));
CREATE TABLE dbo.Inventory(ItemId int,EntryType nvarchar(50),Quantity int,DatePosted datetime2,PostedBy int,Description nvarchar(max));
INSERT dbo.CallTicket VALUES(1,'Desktop','In Progress',NULL,NULL),(2,'Desktop','In Progress',NULL,NULL),(3,'Portal','In Progress',NULL,NULL),(4,'Desktop','Resolved (Temporary)',NULL,NULL);
INSERT dbo.Item VALUES(10,'PC','M1','OLD1',1,0,1,''),(11,'Monitor','M2','OLD2',1,0,1,'');");
                Sql(@"CREATE PROCEDURE dbo.sp_Call_SetTicketStatus @TicketId int,@NewStatus nvarchar(50),@ChangedByUserId int AS
BEGIN
INSERT dbo.CallTicketHistory(TicketId,FieldName,OldValue,NewValue,ChangedByUserId) SELECT TicketId,'Status',Status,@NewStatus,@ChangedByUserId FROM dbo.CallTicket WHERE TicketId=@TicketId;
UPDATE dbo.CallTicket SET Status=@NewStatus WHERE TicketId=@TicketId;
END");
                const string pullOut="{'action':'resolution','ticketId':1,'resolutionType':'Pull Out','isTemporary':false,'remarks':'Collect faulty PC','lines':[{'oldItemId':10,'quantity':1,'oldItemConditionId':2,'oldItemRepairAction':'Unrepaired'}]}";
                Post(pullOut,200);
                Equal("Resolved (Temporary)",Scalar("SELECT Status FROM dbo.CallTicket WHERE TicketId=1"),"Pull out must remain temporary");
                Equal("Pull Out",Scalar("SELECT TOP 1 NewValue FROM dbo.CallTicketHistory WHERE TicketId=1 AND FieldName='ResolutionType' ORDER BY HistoryId DESC"),"Canonical resolution history");
                Equal(0,Scalar("SELECT StockOnHand FROM dbo.Item WHERE ItemId=10"),"Unrepaired old item stays out of stock");
                Equal(2,Scalar("SELECT ConditionId FROM dbo.Item WHERE ItemId=10"),"Old item condition recorded");
                Equal(0,Scalar("SELECT COUNT(*) FROM dbo.Inventory"),"No replacement allocation");
                Post(pullOut,409);
                Post(pullOut.Replace("'ticketId':1","'ticketId':2").Replace("'oldItemId':10","'oldItemId':10,'newItemId':11"),400);
                Equal(0,Scalar("SELECT COUNT(*) FROM dbo.Inventory"),"Rejected new-item request has no stock movement");
                Post(pullOut.Replace("'ticketId':1","'ticketId':2").Replace("'oldItemId':10","'oldItemId':11").Replace("'Unrepaired'","'Repaired - Spare inventory'"),200);
                Equal(1,Scalar("SELECT StockOnHand FROM dbo.Item WHERE ItemId=11"),"Repaired old unit returned to stock once");
                Equal(1,Scalar("SELECT COUNT(*) FROM dbo.Inventory WHERE EntryType='Positive' AND ItemId=11"),"One old-unit receipt");
                Post("{'action':'completepullout','ticketId':1}",200);
                Equal("Solved",Scalar("SELECT Status FROM dbo.CallTicket WHERE TicketId=1"),"Follow-up completes ticket");
                Equal(0,Scalar("SELECT StockOnHand FROM dbo.Item WHERE ItemId=10"),"Completion does not return old unit again");
                Post("{'action':'completepullout','ticketId':1}",409);
                Post("{'action':'completepullout','ticketId':2}",200);
                Equal(1,Scalar("SELECT StockOnHand FROM dbo.Item WHERE ItemId=11"),"Completion does not double stock");
                Equal(1,Scalar("SELECT COUNT(*) FROM dbo.Inventory"),"No inventory movements during completion");
                Sql("INSERT dbo.CallTicketHistory(TicketId,FieldName,NewValue)VALUES(4,'ResolutionType','Replacement')");
                Post("{'action':'completepullout','ticketId':4}",409);
                Equal("Resolved (Temporary)",Scalar("SELECT Status FROM dbo.CallTicket WHERE TicketId=4"),"Cannot bypass replacement returns");
                Post(pullOut.Replace("'ticketId':1","'ticketId':3"),409);
                Equal(4,ItcmActionEmailHelper.Notifications,"Resolution and completion notifications requested exactly once");
                Console.WriteLine("Pull-out SQL workflow: "+checks+" checks passed. No external email delivery or production connection.");
            } finally {
                SqlConnection.ClearAllPools();
                using(var cmd=new SqlCommand("ALTER DATABASE ["+database+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+database+"]",master))cmd.ExecuteNonQuery();
            }
        }
    }
}
'@ | Set-Content -LiteralPath $testsFile -Encoding UTF8
$executable = Join-Path $testDirectory 'PullOutTests.exe'
$testDatabase = 'Yakult_PullOutTest_' + [Guid]::NewGuid().ToString('N')
@"
<configuration><connectionStrings><add name="Yakult_Inventory_System" connectionString="Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Initial Catalog=$testDatabase" /></connectionStrings></configuration>
"@ | Set-Content -LiteralPath "$executable.config" -Encoding UTF8
[xml](Get-Content -LiteralPath "$executable.config" -Raw) | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @('System.Web.dll','System.Configuration.dll','System.Data.dll','System.Core.dll', (Join-Path $apiPath 'bin/Newtonsoft.Json.dll'))
& $compiler /nologo /target:exe "/out:$executable" @($references | ForEach-Object { '/r:' + $_ }) $handlerFile $testsFile
if ($LASTEXITCODE -ne 0) { throw 'Pull-out workflow test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $apiPath 'bin/Newtonsoft.Json.dll') -Destination $testDirectory
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Pull-out workflow checks failed.' }
