using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    internal interface IAssistantDataReader
    {
        Task<DataTable> ReadAsync(AssistantSqlQuery query, CancellationToken cancellationToken);
    }

    internal sealed class SqlAssistantDataReader : IAssistantDataReader
    {
        public async Task<DataTable> ReadAsync(AssistantSqlQuery query, CancellationToken cancellationToken)
        {
            DatabaseConfig.EnsureConfigured();
            using (var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var connection = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var command = CreateCommand(query, connection))
            {
                budget.CancelAfter(TimeSpan.FromSeconds(20));
                await connection.OpenAsync(budget.Token).ConfigureAwait(false);
                using (var reader = await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false))
                {
                    var data = new DataTable();
                    for (int i=0;i<reader.FieldCount;i++) data.Columns.Add(reader.GetName(i),reader.GetFieldType(i));
                    while (await reader.ReadAsync(budget.Token).ConfigureAwait(false))
                    {
                        var values=new object[reader.FieldCount];reader.GetValues(values);data.Rows.Add(values);
                    }
                    return data;
                }
            }
        }

        internal static SqlCommand CreateCommand(AssistantSqlQuery query, SqlConnection connection)
        {
            var command=new SqlCommand(query.CommandText,connection) { CommandTimeout=15,CommandType=CommandType.Text };
            foreach(var p in query.Parameters)
            {
                SqlDbType type=p.Value is DateTime ? SqlDbType.DateTime2 : p.Value is long ? SqlDbType.BigInt : p.Value is int || p.Key=="@EmployeeId" || p.Key=="@UserId" ? SqlDbType.Int : SqlDbType.NVarChar;
                var parameter=command.Parameters.Add(p.Key,type);
                if(type==SqlDbType.NVarChar) parameter.Size=4000;
                parameter.Value=p.Value ?? DBNull.Value;
            }
            return command;
        }
    }

    public sealed class AssistantQueryEngine
    {
        public const int PageSize=8;
        private readonly IAssistantDataReader _reader;
        public AssistantQueryEngine() : this(new SqlAssistantDataReader()) { }
        internal AssistantQueryEngine(IAssistantDataReader reader) { _reader=reader; }

        public async Task<AssistantResult> ExecuteAsync(AssistantRequest request, AssistantContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var query=BuildQuery(request,context);
                var table=await _reader.ReadAsync(query,ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                return Render(query,table);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch(UnauthorizedAccessException ex) { return AssistantResult.Message(AssistantOutcome.Denied,ex.Message); }
            catch(ArgumentException ex) { return AssistantResult.Message(AssistantOutcome.Clarification,ex.Message); }
            catch(Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                try { Logger.LogError("[Yakumi] Read-only capability failed: "+request.Capability,ex); } catch { }
                return AssistantResult.Message(AssistantOutcome.Unavailable,"Current data is unavailable for this lookup. The connection, deployed schema, or query failed; this does not mean there are zero records. No records were changed.");
            }
        }

        internal static AssistantSqlQuery BuildQuery(AssistantRequest request, AssistantContext context)
        {
            if(request==null || context==null) throw new ArgumentException("Please specify a lookup.");
            var c=AssistantCatalog.Find(request.Capability);
            if(c==null) throw new ArgumentException("That lookup is not in Yakumi's read-only capability catalog.");
            if(!c.IsAllowed(context)) throw new UnauthorizedAccessException("Your account does not have access to "+c.Source+".");
            if(!Enum.IsDefined(typeof(AssistantOperation),request.Operation) || request.Page<0 || request.Page>100000)
                throw new ArgumentException("Please choose a valid operation and page.");
            if(request.RecordId<=0 || request.ParentRecordId<=0) throw new ArgumentException("Please provide a valid positive record number.");
            var query=new AssistantSqlQuery { Capability=c,Request=request.Copy(),TimeZone=context.TimeZone };
            var predicates=new List<string>();var scope=new List<string>();
            query.Parameters.Add("@UserId",context.UserId);
            query.Parameters.Add("@EmployeeId",(object)context.EmployeeId ?? DBNull.Value);
            if(c.Id=="warranties") query.Parameters.Add("@TodayLocal",TimeZoneInfo.ConvertTime(DateTime.UtcNow,context.TimeZone).Date);
            if(request.RecordId.HasValue) { predicates.Add("d.[Id]="+Parameter(query,request.RecordId.Value));scope.Add("record #"+request.RecordId); }
            if(request.ParentRecordId.HasValue)
            {
                if(c.ParentColumn==null) throw new ArgumentException("This lookup does not accept a parent record reference.");
                predicates.Add("d.["+c.ParentColumn+"]="+Parameter(query,request.ParentRecordId.Value));scope.Add(c.ParentColumn+" #"+request.ParentRecordId);
            }
            bool ownRequests=c.RequesterScope && !((context.HasPortal("InventorySystem") && context.HasPage("ViewRequestsPage")) ||
                (context.HasPortal("AdminPortal") && context.HasPage("DepartmentRequestHistoryPage")));
            bool personal=request.Personal || ownRequests;
            if(personal)
            {
                if(c.PersonalPredicate==null) throw new ArgumentException("This source has no personal ownership filter. Please select an employee or use the ownership/borrow lookup.");
                if(c.PersonalPredicate=="d.EmployeeId=@EmployeeId" && context.EmployeeId.GetValueOrDefault()<=0)
                    throw new ArgumentException("Your account is not linked to an employee. I cannot identify personal records by guessing a name.");
                string personalPredicate=c.PersonalPredicate;
                if(request.PersonalRelation=="Assigned")
                {
                    if(c.Id=="tickets" || c.Id=="ticket-history") personalPredicate="(d.AssignedUserId=@UserId OR d.EmployeeId=@EmployeeId)";
                    else if(c.Id=="repairs" || c.Id=="repair-history" || c.Id=="repair-parts") personalPredicate="d.EmployeeId=@EmployeeId";
                    else if(c.Id=="approvals") personalPredicate="d.AssignedUserId=@UserId";
                    else throw new ArgumentException("This source has no assignment filter for your account.");
                }
                else if(request.PersonalRelation=="Created")
                {
                    if(!c.Columns.Contains("CreatedBy")) throw new ArgumentException("This source has no creator filter for your account.");
                    personalPredicate="d.CreatedBy=@UserId";
                }
                else if(request.PersonalRelation!=null) throw new ArgumentException("Please specify a supported personal relationship.");
                predicates.Add(personalPredicate);scope.Add(request.PersonalRelation=="Assigned"?"assigned to your account":request.PersonalRelation=="Created"?"created by your account":"your account's records");
            }
            if(c.ForcedScopePredicate!=null && !context.HasPortal("CartridgeManagement"))
            { predicates.Add(c.ForcedScopePredicate);scope.Add("authorizations submitted by, for, or assigned to your account"); }
            if(request.IncludeArchived && !(context.HasPortal("InventorySystem") && context.HasPage("ViewArchivePage")))
                throw new UnauthorizedAccessException("Your account does not have access to archived records.");
            if(c.ArchiveColumn!=null && !request.IncludeArchived) predicates.Add("d.["+c.ArchiveColumn+"]=0");
            if(c.ArchiveColumn!=null && request.ArchivedOnly) predicates.Add("d.["+c.ArchiveColumn+"]=1");
            if(request.IncludeArchived && c.ArchiveColumn==null && c.Id!="archive")
                throw new ArgumentException("This source cannot include archived records. Use the archive lookup.");
            if(c.CategoryScope && c.Columns.Contains("Category") && context.DeniedCategories!=null)
                foreach(string category in context.DeniedCategories) predicates.Add("ISNULL(d.[Category],'')<>"+Parameter(query,category));
            if(c.DefaultStatus!=null && string.IsNullOrWhiteSpace(request.Status)) predicates.Add("d.[Status]="+Parameter(query,c.DefaultStatus));
            if(c.Id=="sets" || c.Id=="set-items") scope.Add("employee follows the linked request, as in the Sets grid; current branch/department follow the set");
            if(request.InStockOnly)
            {
                if(!new[] {"items","consumables","cartridges"}.Contains(c.Id)) throw new ArgumentException("This source has no available-stock filter.");
                predicates.Add("d.[Units]>0");scope.Add("positive available stock");
            }
            AddFilter(query,predicates,scope,"Status",request.Status);
            AddFilter(query,predicates,scope,"Priority",request.Priority);
            AddFilter(query,predicates,scope,"Employee",request.Employee,true);
            AddFilter(query,predicates,scope,"Department",request.Department,true);
            AddFilter(query,predicates,scope,"Branch",request.Branch,true);
            AddFilter(query,predicates,scope,"Company",request.Company,true);
            AddFilter(query,predicates,scope,"Category",request.Category,!request.ExactCategory);
            if(request.Condition!=null && c.Id!="cartridges") AddFilter(query,predicates,scope,"Condition",request.Condition);
            if(request.OutstandingOnly)
            {
                if(!c.Columns.Contains("OutstandingUnits")) throw new ArgumentException("This source has no outstanding-quantity filter.");
                predicates.Add("d.[OutstandingUnits]>0 AND d.[Status] NOT IN ('Rejected','Cancelled','Canceled','Closed')");scope.Add("outstanding quantities");
            }
            var search=AiIntentRouter.SearchTokens(request.Search);
            if(search.Count>12) throw new ArgumentException("Please use at most twelve meaningful search terms.");
            if(search.Count>0)
            {
                if(c.Id=="policies")
                    predicates.Add("EXISTS(SELECT 1 FROM dbo.CompanyPolicy policy WHERE policy.PolicyId=d.Id AND "+
                        AiContextProviders.MatchClause(search,query.Parameters,"policy.Title","policy.Category","policy.Summary","policy.Body")+")");
                else predicates.Add(AiContextProviders.MatchClause(search,query.Parameters,c.SearchColumns.Select(s=>"d.["+s+"]").ToArray()));
                scope.Add("every search term: "+string.Join(" ",search));
            }
            string date=request.DateField ?? c.DefaultDateField;
            if(request.FromLocal.HasValue || request.ToLocalExclusive.HasValue)
            {
                if(date=="StatusChangedAt" && c.Id=="tickets" && request.Status!=null)
                {
                    string history="EXISTS(SELECT 1 FROM dbo.CallTicketHistory hx WHERE hx.TicketId=d.Id AND hx.FieldName='Status' AND hx.NewValue="+Parameter(query,request.Status);
                    if(request.FromLocal.HasValue) history+=" AND hx.ChangedAt>="+Parameter(query,ToDatabaseTime(request.FromLocal.Value,true,context));
                    if(request.ToLocalExclusive.HasValue) history+=" AND hx.ChangedAt<"+Parameter(query,ToDatabaseTime(request.ToLocalExclusive.Value,true,context));
                    predicates.Add(history+")");
                }
                else
                {
                    if(date==null || !c.Columns.Contains(date) || !IsDateColumn(date)) throw new ArgumentException("Choose a date field supported by this source.");
                    if(request.FromLocal.HasValue) predicates.Add("d.["+date+"]>="+Parameter(query,ToDatabaseTime(request.FromLocal.Value,c.UtcDateColumns.Contains(date),context)));
                    if(request.ToLocalExclusive.HasValue) predicates.Add("d.["+date+"]<"+Parameter(query,ToDatabaseTime(request.ToLocalExclusive.Value,c.UtcDateColumns.Contains(date),context)));
                }
                if(request.FromLocal>=request.ToLocalExclusive) throw new ArgumentException("The start date must be earlier than the end date.");
                scope.Add(date+": "+(request.FromLocal.HasValue?request.FromLocal.Value.ToString("yyyy-MM-dd"):"beginning")+" through "+
                    (request.ToLocalExclusive.HasValue?request.ToLocalExclusive.Value.AddTicks(-1).ToString("yyyy-MM-dd"):"latest")+" ("+context.TimeZone.Id+")");
            }
            string baseSql=c.BaseSql;
            if(request.Condition!=null && c.Id=="cartridges")
            {
                if(request.Condition!="Brand New" && request.Condition!="Refilled") throw new ArgumentException("Cartridge condition must be Brand New or Refilled.");
                baseSql=baseSql.Replace("stock.BrandNewUnits+stock.RefilledUnits Units",request.Condition=="Brand New"?"stock.BrandNewUnits Units":"stock.RefilledUnits Units");
                scope.Add("issuable "+request.Condition+" units; both condition totals shown separately");
            }
            string cte="WITH Matches AS ("+baseSql+"), Scoped AS (SELECT * FROM Matches d WHERE "+(predicates.Count==0?"1=1":string.Join(" AND ",predicates.Select(p=>"("+p+")")))+")";
            string aggregates=string.Concat(c.AggregateColumns.Select(a=>",ISNULL(SUM(CAST(["+a+"] AS DECIMAL(28,4))),0) AS [Total_"+a+"]"));
            string metadata="SELECT COUNT_BIG(*) TotalMatches"+aggregates+" FROM Scoped";
            query.Parameters.Add("@Offset",checked(request.Page*PageSize));query.Parameters.Add("@PageSize",PageSize);
            query.IsGrouped=request.Operation==AssistantOperation.Summary;
            if(query.IsGrouped)
            {
                if(string.IsNullOrWhiteSpace(request.GroupBy)) throw new ArgumentException("Choose a grouping, for example 'tickets by status' or 'invoices by company'.");
                string group;
                if(request.GroupBy=="Month" && date!=null && c.Columns.Contains(date))
                {
                    string expression="["+date+"]";
                    if(c.UtcDateColumns.Contains(date)) expression="("+expression+" AT TIME ZONE 'UTC' AT TIME ZONE "+Parameter(query,context.TimeZone.Id)+")";
                    group="CONVERT(char(7),"+expression+",120)";scope.Add("month uses "+date+" in "+context.TimeZone.Id);
                }
                else if(c.GroupColumns.Contains(request.GroupBy)) group="CAST(["+request.GroupBy+"] AS NVARCHAR(300))";
                else throw new ArgumentException("Supported groupings: "+string.Join(", ",c.GroupColumns)+".");
                cte+=",Grouped AS (SELECT "+group+" GroupValue,COUNT_BIG(*) Records"+aggregates+" FROM Scoped GROUP BY "+group+")";
                query.CommandText=cte+" SELECT totals.*,(SELECT COUNT_BIG(*) FROM Grouped) TotalGroups,page.* FROM ("+metadata+") totals OUTER APPLY (SELECT * FROM Grouped ORDER BY Records DESC,GroupValue OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY) page;";
                // Group sums use a different prefix to avoid duplicate column names in the reader.
                query.CommandText=query.CommandText.Replace("SELECT * FROM Grouped ORDER BY", "SELECT GroupValue,Records"+string.Concat(c.AggregateColumns.Select(a=>",[Total_"+a+"] AS [Group_"+a+"]"))+" FROM Grouped ORDER BY");
                query.OutputColumns=new[] { "GroupValue","Records" }.Concat(c.AggregateColumns.Select(a=>"Group_"+a)).ToArray();scope.Add("grouped by "+request.GroupBy);
            }
            else if(request.Operation==AssistantOperation.Count)
            {
                query.CommandText=cte+" "+metadata+";";query.OutputColumns=new string[0];
            }
            else
            {
                string order=request.LowestStock && c.Columns.Contains("Units")?"[Units] ASC,[Id] ASC":c.Columns.Contains("CreatedAt")?"[CreatedAt] DESC,[Id] DESC":"[Id] DESC";
                query.CommandText=cte+" SELECT totals.*,page.* FROM ("+metadata+") totals OUTER APPLY (SELECT * FROM Scoped ORDER BY "+order+" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY) page;";
                query.OutputColumns=c.Columns;
            }
            if(request.LowestStock) scope.Add("lowest stock first; no threshold inferred");
            if(c.DefaultStatus!=null && request.Status==null) scope.Add("default status: "+c.DefaultStatus);
            if(c.ArchiveColumn!=null) scope.Add(request.IncludeArchived?"including archived":"excluding archived");
            query.AppliedScope=scope.Count==0?"all accessible matching records; all dates":string.Join("; ",scope);
            return query;
        }

        internal static bool IsDateColumn(string field)
        {
            return new[] { "CreatedAt","UpdatedAt","SolvedAt","CompletedAt","ReturnedAt","StartDate","EndDate","EffectiveDate" }.Contains(field);
        }
        private static DateTime ToDatabaseTime(DateTime date,bool utc,AssistantContext context)
        {
            date=DateTime.SpecifyKind(date,DateTimeKind.Unspecified);
            return utc?TimeZoneInfo.ConvertTimeToUtc(date,context.TimeZone):date;
        }
        private static string Parameter(AssistantSqlQuery query,object value)
        {
            string name="@P"+query.Parameters.Count;query.Parameters.Add(name,value);return name;
        }
        private static void AddFilter(AssistantSqlQuery query,List<string> predicates,List<string> scope,string field,string value,bool contains=false)
        {
            if(string.IsNullOrWhiteSpace(value)) return;
            if(!query.Capability.Columns.Contains(field)) throw new ArgumentException(query.Capability.Title+" does not support a "+field+" filter.");
            if(value.Length>160) throw new ArgumentException("Please shorten the "+field+" filter.");
            if(field=="Status" && value=="Open" && query.Capability.Id=="tickets")
                predicates.Add("d.[Status] NOT IN ('Solved','Closed','Resolved (Temporary)','Cancelled')");
            else if(field=="Category" && value.Equals("Cellphone",StringComparison.OrdinalIgnoreCase)) predicates.Add("REPLACE(d.[Category],' ','')="+Parameter(query,"Cellphone"));
            else if(contains) predicates.Add(AiContextProviders.MatchClause(AiIntentRouter.SearchTokens(value),query.Parameters,"d.["+field+"]"));
            else predicates.Add("d.["+field+"]="+Parameter(query,value));
            scope.Add(field+": "+value);
        }

        internal static AssistantResult Render(AssistantSqlQuery query,DataTable table)
        {
            if(table.Rows.Count==0 || !table.Columns.Contains("TotalMatches")) throw new InvalidOperationException("Missing aggregate metadata.");
            DataRow metadata=table.Rows[0];long total=Convert.ToInt64(metadata["TotalMatches"],CultureInfo.InvariantCulture);
            var totals=new Dictionary<string,decimal>();
            foreach(string column in query.Capability.AggregateColumns)
                totals[column]=metadata.IsNull("Total_"+column)?0:Convert.ToDecimal(metadata["Total_"+column],CultureInfo.InvariantCulture);
            var rows=new List<IReadOnlyDictionary<string,object>>();
            foreach(DataRow row in table.Rows)
            {
                string sentinel=query.IsGrouped?"Records":"Id";
                if(query.OutputColumns.Length==0 || !table.Columns.Contains(sentinel) || row.IsNull(sentinel)) continue;
                var data=new Dictionary<string,object>();
                foreach(string column in query.OutputColumns) data[column]=row.IsNull(column)?null:row[column];
                rows.Add(new ReadOnlyDictionary<string,object>(data));
            }
            long groups=query.IsGrouped?Convert.ToInt64(metadata["TotalGroups"],CultureInfo.InvariantCulture):0;
            var result=new AssistantResult { Outcome=total==0?AssistantOutcome.NoMatches:AssistantOutcome.Success,
                Source=query.Capability.Source,AppliedScope=query.AppliedScope,RetrievedAtUtc=DateTime.UtcNow,TotalRecords=total,TotalGroups=groups,
                Totals=new ReadOnlyDictionary<string,decimal>(totals),Rows=rows.AsReadOnly(),Request=query.Request.Copy(),
                HasNextPage=query.Request.Operation!=AssistantOperation.Count && ((long)query.Request.Page+1)*PageSize<(query.IsGrouped?groups:total) };
            var text=new StringBuilder(query.Capability.Title+": "+total.ToString(CultureInfo.InvariantCulture)+" matching records total.");
            if(query.Request.InStockOnly && totals.ContainsKey("Units"))
                text=new StringBuilder(FormatNumber("Units",totals["Units"])+" "+(query.Request.Category ?? "item")+" units in stock"+
                    (string.IsNullOrWhiteSpace(query.Request.Search)?"":" matching ‘"+query.Request.Search+"’")+".\n"+total+" matching records total.");
            foreach(var item in totals) text.Append("\n"+MetricLabel(item.Key,query.Capability.Id)+" across all matches: "+FormatNumber(item.Key,item.Value)+".");
            if(rows.Count>0) text.Append("\nShowing "+rows.Count+" "+(query.IsGrouped?"groups":"records")+" on page "+(query.Request.Page+1)+ (query.IsGrouped?" of "+groups+" groups.":"."));
            else if(total==0) text.Append("\nNo matching records within this scope.");
            else if(query.Request.Operation!=AssistantOperation.Count) text.Append("\nNo rows on this page. Choose Previous page.");
            foreach(var row in rows)
            {
                var visible=row.Where(p=>!new[] { "CreatedBy","AssignedUserId","EmployeeId","Active","Archived" }.Contains(p.Key) && p.Value!=null);
                text.Append("\n• "+string.Join("; ",visible.Select(p=>p.Key+": "+DisplayValue(p.Key,p.Value,query))));
            }
            if(result.HasNextPage) text.Append("\nMore matches are available on the next page.");
            text.Append("\nScope: "+result.AppliedScope+".\nSource: "+result.Source+". Retrieved "+TimeZoneInfo.ConvertTimeFromUtc(result.RetrievedAtUtc,query.TimeZone ?? TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss")+" ("+(query.TimeZone ?? TimeZoneInfo.Local).Id+").");
            result.Text=text.ToString();return result;
        }
        internal static string MetricLabel(string key,string capability=null)
        {
            return key=="Units"?(capability=="inventory"?"Movement units":new[] {"items","consumables","cartridges"}.Contains(capability)?"Available units":"Line quantity"):key=="AmountDue"?"Stored amount due":
                System.Text.RegularExpressions.Regex.Replace(key,"([a-z])([A-Z])","$1 $2");
        }
        internal static string FormatNumber(string key,decimal value)
        {
            bool money=new[] { "Amount","AmountDue","UnitPrice","Subtotal","Vat","Withholding","Discount" }.Contains(key.Replace("Group_",""));
            return value.ToString(money?"N2":"0.####",CultureInfo.InvariantCulture);
        }
        private static string DisplayValue(string key,object value,AssistantSqlQuery query)
        {
            if(value is DateTime)
            {
                var date=(DateTime)value;
                if(query.Capability.UtcDateColumns.Contains(key)) date=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(date,DateTimeKind.Utc),query.TimeZone ?? TimeZoneInfo.Local);
                return date.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);
            }
            if(value is decimal || value is double || value is float) return FormatNumber(key,Convert.ToDecimal(value,CultureInfo.InvariantCulture));
            return AiContextProviders.Clean(Convert.ToString(value,CultureInfo.InvariantCulture),key=="Excerpt"?1600:500);
        }
    }
}
