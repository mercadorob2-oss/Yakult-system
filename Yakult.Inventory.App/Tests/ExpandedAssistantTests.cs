using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>Offline acceptance checks with synthetic context, query data and model responses.</summary>
    public static class ExpandedAssistantTests
    {
        private static int _checks;
        internal static AssistantContext Context(string module="Tickets")
        {
            return new AssistantContext {UserId=101,EmployeeId=202,EmployeeName="ROB MECA SANTOS",UserName="Rob Meca",Module=module,
                IsDeveloper=true,IsSuperAdmin=true,Portals=new[] {"InventorySystem","CallITMonitoring","RequesterPortal","CartridgeManagement","BorrowItems","RepairTechnicianPortal","Reports","AdminPortal"},
                Pages=new ReadOnlyDictionary<string,bool>(AssistantCatalog.All.SelectMany(c=>c.PageKeys).Concat(AssistantScreens.All.Select(s=>s.PageKey)).Distinct().ToDictionary(p=>p,p=>true)),
                DeniedCategories=new string[0],Roles=new[] {"Developer"},SessionStamp="offline-test",TimeZone=TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")};
        }
        private static void Check(bool condition,string message)
        {if(!condition) throw new InvalidOperationException("Expanded Yakumi regression: "+message);_checks++;}
        private static AssistantRequest Request(string question,string capability,AssistantOperation operation=AssistantOperation.List)
        {
            var plan=AssistantPlanner.Plan(question,Context(),null,new DateTime(2026,10,15));
            Check(plan.Message==null && plan.Requests.Count==1,"question did not produce one lookup: "+question+" / "+plan.Message);
            var r=plan.Requests[0];Check(r.Capability==capability,"wrong source for "+question+": "+r.Capability);
            Check(r.Operation==operation,"wrong operation for "+question+": "+r.Operation);return r;
        }
        public static string RunAll()
        {
            _checks=0;Planning();StockVocabulary();SourcesAndPermissions();AggregateResults();Conversation().GetAwaiter().GetResult();ConnectionSettings().GetAwaiter().GetResult();ConnectionSettingsUi();Knowledge();
            return _checks+" expanded assistant offline acceptance checks passed.";
        }
        public static string[] InspectQuestions(string[] questions)
        {
            return questions.Select(q=> {var p=AssistantPlanner.Plan(q,Context(),null,new DateTime(2026,10,15));return q+" => "+
                (p.Message ?? p.GuidanceQuestion ?? string.Join(" / ",p.Requests.Select(r=>r.Capability+" "+r.Operation+" search="+r.Search+" status="+r.Status+" employee="+r.Employee+" group="+r.GroupBy+" from="+r.FromLocal+" to="+r.ToLocalExclusive)));}).ToArray();
        }
        private static void Planning()
        {
            var solved=Request("How may solved tickets are there?","tickets",AssistantOperation.Count);
            Check(solved.Status=="Solved" && string.IsNullOrWhiteSpace(solved.Search),"solved typo and exact status");
            var today=Request("How many solved tickets are there today?","tickets",AssistantOperation.Count);
            Check(today.FromLocal==new DateTime(2026,10,15) && today.ToLocalExclusive==new DateTime(2026,10,16) && today.DateField=="StatusChangedAt","solved event date scope");
            var mine=Request("show my tickets","tickets");Check(mine.Personal,"personal ticket scope");
            var assigned=Request("show high priority tickets assigned to John Smith this month","tickets");
            Check(assigned.Employee=="john smith" && assigned.Priority=="High" && string.IsNullOrWhiteSpace(assigned.Search),"employee, priority and period extraction");
            Request("ticket history TCK-69","ticket-history",AssistantOperation.History);
            Check(Request("status of TCK-000069","tickets",AssistantOperation.Detail).RecordId==69,"unpadded numeric identity");
            Check(Request("SET-12 contents","set-items",AssistantOperation.History).ParentRecordId==12,"set contents parent identity");
            Check(Request("SET-12 history","ownership-history",AssistantOperation.History).ParentRecordId==12,"set ownership history is separate from contents");
            Check(Request("item #23 history","item-history",AssistantOperation.History).ParentRecordId==23,"item history uses the saved item ID");
            Check(Request("explain the borrowing policy","policies").Search=="borrowing","policy explanation retrieves published policy without model rewriting");
            Check(Request("how many ASUS laptops are in stock?","items",AssistantOperation.Count).Search=="asus laptop","brand stays intact");
            foreach(string phrase in new[] {"phones","cellphones","cell phones","smartphones","mobile phones"})
            {
                var phones=Request("how many Redmi "+phrase+" do I have in stock?","items",AssistantOperation.Count);
                Check(phones.Category=="Cellphone" && phones.Search=="redmi" && phones.InStockOnly && !phones.Personal,"phone synonym, brand and shared stock: "+phrase);
            }
            Check(Request("how many IP phones in stock?","items",AssistantOperation.Count).Category=="IP Phone","IP phones remain a distinct category");
            Check(Request("ilan ang keyboard na available?","items",AssistantOperation.Count).Search=="keyboard","Filipino stock wording");
            var toner=Request("how much HP CF276X toner stock is available?","consumables",AssistantOperation.Count);
            Check(toner.Category=="Toner" && toner.Search=="hp cf276x","consumable category and model");
            Check(Request("refilled cartridge stock","cartridges").Condition=="Refilled","cartridge condition");
            Request("show my requests","requests");Check(Request("my pending requests","requests").OutstandingOnly,"outstanding request semantics");
            Request("Do you know Rob Meca?","employees");
            var returned=Request("my returned borrows","borrows");Check(returned.Status=="Returned" && returned.Personal,"returned personal loans");
            Check(Request("borrow history","borrows").Status==null,"history includes open and returned");
            Request("show repair tickets","repairs");
            Request("repair parts for RPR-12","repair-parts");
            Request("repair history RPR-12","repair-history",AssistantOperation.History);
            Request("how many active employees?","employees",AssistantOperation.Count);
            Request("show invoices","invoices");Request("show receipts","receipts");Request("show accounts","accounts");
            Request("show role memberships","roles");Request("user activity log","audit");
            var grouped=Request("tickets by department this month","tickets",AssistantOperation.Summary);
            Check(grouped.GroupBy=="Department" && grouped.Department==null,"grouping is not a department value");
            var expired=Request("expired renewals","renewals");Check(expired.ToLocalExclusive==new DateTime(2026,10,15) && expired.Status==null,"expired dates are not renewal statuses");
            Check(Request("renewals expiring within 30 days","renewals").ToLocalExclusive==new DateTime(2026,11,15),"inclusive upcoming horizon");
            var compare=AssistantPlanner.Plan("compare invoices this month with last month",Context(),null,new DateTime(2026,10,15));
            Check(compare.Compare && compare.Requests.Count==2 && compare.Requests[0].FromLocal==new DateTime(2026,10,1) && compare.Requests[1].FromLocal==new DateTime(2026,9,1),"two period comparison");
            compare=AssistantPlanner.Plan("compare solved tickets this month with last month",Context(),null,new DateTime(2026,10,15));
            Check(compare.Requests.Count==2 && compare.Requests.All(r=>r.Status=="Solved"),"comparison inherits status into abbreviated second scope");
            Check(Request("ilang nasolve na tiket ngayong buwan?","tickets",AssistantOperation.Count).FromLocal==new DateTime(2026,10,1),"Filipino status and month");
            Check(Request("unsolved tickets","tickets").Status=="Open","unsolved means nonterminal ticket scope");
            Check(AssistantPlanner.Plan("how many resolved tickets?",Context()).Message!=null,"ambiguous resolved status is clarified");
            Check(Request("how many closed tickets?","tickets",AssistantOperation.Count).Status=="Closed","Closed stays distinct from Solved");
            Check(Request("resolved (temporary) tickets","tickets").Status=="Resolved (Temporary)","parenthesized status is recognized exactly");
            Check(AssistantPlanner.Plan("solved and closed tickets",Context()).Message!=null,"combined status scope is clarified");
            Check(Request("unfulfilled cartridge exchanges","exchanges").OutstandingOnly,"unfulfilled exchange is a quantity scope");
            Check(Request("pending license review","license-review").Status=="Pending review","license review status is source-specific");
            Check(Request("repaired items","item-repairs").Status==null,"repair-history page name is not a fabricated status");
            foreach(var pair in new[] {"set item updates|set-updates","role memberships|roles","individual permission overrides|permissions","role permission overrides|role-permissions","individual portal overrides|user-portals"})
            {var parts=pair.Split('|');Check(string.IsNullOrEmpty(Request(parts[0],parts[1]).Search),"canonical source name does not become an accidental name filter: "+parts[0]);}
            Check(Request("tickets in September 2026","tickets").FromLocal==new DateTime(2026,9,1),"named calendar month");
            var multiple=AssistantPlanner.Plan("keyboard stock and my requests",Context());Check(multiple.Requests.Count==2,"two named sources");
            Check(AssistantPlanner.Plan("stock and my requests and my tickets",Context()).Message!=null,"bounded multi-intent");
            Check(AssistantPlanner.Plan("How do I borrow equipment?",Context()).GuidanceQuestion!=null,"workflow guidance");
            Check(AssistantPlanner.Plan("What is the borrowing policy?",Context()).Requests.Single().Capability=="policies","policy retrieval precedence");
            Check(AssistantPlanner.Plan("delete TCK-69",Context()).Message.Contains("read-only"),"write action rejected");
            Check(AssistantPlanner.Plan("overdue borrows",Context()).Message.Contains("no due-date"),"no fabricated overdue status");
            Check(AssistantPlanner.Plan(new string('x',1201),Context()).Message!=null,"oversized question rejected");
            Check(AssistantPlanner.Plan("TCK-999999999999999999999999",Context()).Message!=null,"oversized record ID clarified");
            var follow=AssistantPlanner.Plan("this month only",Context(),new[] {mine},new DateTime(2026,10,15)).Requests.Single();
            Check(follow.Personal && follow.Capability=="tickets" && follow.FromLocal==new DateTime(2026,10,1),"follow-up preserves ownership");
            Check(AssistantPlanner.Plan("next page",Context(),new[] {mine}).Requests.Single().Page==1,"next page");
            Check(AssistantPlanner.Plan("how many in total?",Context(),new[] {mine}).Requests.Single().Operation==AssistantOperation.Count,"total follow-up re-query");
            Check(AssistantPlanner.Plan("next page",Context()).Message!=null,"unbound follow-up clarified");
            var selected=Context();selected.SelectedCapability="tickets";selected.SelectedRecordId=69;
            Check(AssistantPlanner.Plan("this ticket",selected).Requests.Single().RecordId==69,"saved selected record context");
            Check(AssistantPlanner.Plan("this ticket",Context()).Message!=null,"no guessed selected record");
            Check(AssistantPlanner.Plan("this invoice",selected).Message!=null,"selected record type must agree with the question");
            Check(Request("item #23","items",AssistantOperation.Detail).RecordId==23,"explicit item ID");
            Check(Request("invoice #31","invoices",AssistantOperation.Detail).RecordId==31,"explicit invoice ID");
            Check(Request("ticket history TCK-69","ticket-history",AssistantOperation.History).ParentRecordId==69,"history uses correct parent key");
            Check(Request("tickets assigned to me","tickets").PersonalRelation=="Assigned","assignment is distinct from creator");
            Check(Request("tickets created by me","tickets").PersonalRelation=="Created","creator is distinct from assignment");
            Check(Request("solved tickets by month","tickets",AssistantOperation.Summary).DateField=="SolvedAt","monthly solved scope uses recorded solved timestamp");
            var pageContext=Context();pageContext.Page="ViewInvoicePage";
            Check(AssistantPlanner.Plan("help with this page",pageContext).GuidanceQuestion.Contains("Invoices"),"active-page workflow guidance");
            foreach(var pair in new[] {"system email logs|system-email","ticket email logs|ticket-email","role portal access|role-portals","user portal access|user-portals","role permissions|role-permissions","approver titles|approver-titles","branch acronyms|branch-acronyms","department acronyms|department-acronyms","user positions|user-positions"})
            {var parts=pair.Split('|');Request(parts[0],parts[1]);}
        }
        private static void StockVocabulary()
        {
            var products=new[] {
                new AssistantProductTerm {Name="Redmi 15 5G",Model="Redmi 15",Category="Cellphone"},
                new AssistantProductTerm {Name="Yealink desk device",Model="T31",Category="IP Phone"},
                new AssistantProductTerm {Name="Zephyr Q9",Model="Q9",Category="Sensor Array"},
                new AssistantProductTerm {Name="Office audio",Model="H900",Category="Headset"},
                new AssistantProductTerm {Name="Network device",Model="SW24",Category="Switch"},
                new AssistantProductTerm {Name="Storage box",Model="DS920",Category="Network Attached Storage(NAS)"},
                new AssistantProductTerm {Name="Wireless device",Model="AP23",Category="Acces Point(AP)"},
                new AssistantProductTerm {Name="Display",Model="X50",Category="TV"}
            };
            foreach(var entry in new[] {"phones|Cellphone","cellphones|Cellphone","IP phones|IP Phone","sensor arrays|Sensor Array","headsets|Headset","switches|Switch","NAS|Network Attached Storage(NAS)","access points|Acces Point(AP)","TVs|TV"})
            {
                var pair=entry.Split('|');string q="how many "+pair[0]+" do I have in stock?";
                var hint=AssistantStockVocabulary.Match(q,products);Check(hint!=null && hint.Category==pair[1],"database category vocabulary: "+pair[0]);
                var plan=AssistantPlanner.Plan(hint.Rewrite(q),Context());
                Check(plan.Requests.Count==1 && plan.Requests[0].Operation==AssistantOperation.Count && string.IsNullOrEmpty(plan.Requests[0].Search),"category phrase removed while keeping count intent: "+pair[0]);
            }
            var redmi=AssistantStockVocabulary.Match("how many Redmi phones in stock?",products);
            Check(AssistantPlanner.Plan(redmi.Rewrite("how many Redmi phones in stock?"),Context()).Requests.Single().Search=="redmi","catalog category mapping preserves brand qualifier");
            Check(AssistantStockVocabulary.Match("how many Zephyr Q9 do we have?",products)!=null,"new product/model works without a hardcoded product alias");
            Check(AssistantStockVocabulary.Match("how many nonexistentbrand do we have?",products)==null,"unknown catalog term cannot become an unfiltered stock count");
            Check(!AssistantStockVocabulary.ShouldResolve("how many solved tickets?") && !AssistantStockVocabulary.ShouldResolve("how do I use stock?"),"vocabulary refresh restricted to factual item questions");
        }
        private static void SourcesAndPermissions()
        {
            var context=Context();
            foreach(var capability in AssistantCatalog.All)
            {
                Check(capability.Columns.Contains("Id") && capability.Columns.Contains("Label"),"source identity contract: "+capability.Id);
                Check(capability.AggregateColumns.All(capability.Columns.Contains) && capability.GroupColumns.All(capability.Columns.Contains),"registered fields: "+capability.Id);
                Check(capability.BaseSql.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase),"SELECT source: "+capability.Id);
                Check(!System.Text.RegularExpressions.Regex.IsMatch(capability.BaseSql,@"\b(Password|PasswordHash|PasswordSalt|SignatureData|QRToken|SiImage|ConnectionString)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"secret field excluded: "+capability.Id);
                var sql=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=capability.Id},context);
                using(var connection=new System.Data.SqlClient.SqlConnection())
                using(var command=SqlAssistantDataReader.CreateCommand(sql,connection))
                {Check(command.CommandTimeout==15 && connection.State==ConnectionState.Closed,"query built without connection: "+capability.Id);}
            }
            var solved=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Status="Solved"},context);
            Check(solved.Parameters.Values.Contains("Solved") && !solved.CommandText.Contains("'Closed','Resolved'"),"Solved is not a combined final-status bucket");
            Check(solved.CommandText.Contains("COUNT_BIG(*)"),"count is SQL-backed");
            context.IsDeveloper=false;context.Portals=new[] {"RequesterPortal"};
            var request=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="requests",Operation=AssistantOperation.Count},context);
            Check(request.CommandText.Contains("d.CreatedBy=@UserId"),"requester aggregate ownership restriction");
            bool denied=false;try {AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets"},context);}catch(UnauthorizedAccessException){denied=true;}
            Check(denied,"portal denial");
            context=Context();context.IsDeveloper=false;
            var pages=context.Pages.ToDictionary(p=>p.Key,p=>p.Value);pages["ViewItemsPage"]=false;context.Pages=new ReadOnlyDictionary<string,bool>(pages);
            denied=false;try {AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items"},context);}catch(UnauthorizedAccessException){denied=true;}
            Check(denied,"page denial");
            context=Context();context.DeniedCategories=new[] {"Toner"};
            var category=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="consumables",Operation=AssistantOperation.Count},context);
            Check(category.CommandText.Contains("ISNULL(d.[Category],'')<>") && category.Parameters.Values.Contains("Toner"),"category filter applies before totals");
            var names=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="employees",Search="Rob Meca"},Context());
            Check(names.Parameters.Values.Contains("%Rob%") && names.Parameters.Values.Contains("%Meca%") && names.CommandText.Contains(") AND ("),"AND employee match");
            var mismatch=new AssistantRequest {Capability="tickets",Company="'+DROP TABLE x--"};var safe=AssistantQueryEngine.BuildQuery(mismatch,Context());
            Check(!safe.CommandText.Contains("DROP TABLE") && safe.Parameters.Values.Any(v=>Convert.ToString(v).Contains("DROP")),"user text only in parameters");
            bool invalid=false;try {AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Summary,GroupBy="Status];DELETE"},Context());}catch(ArgumentException){invalid=true;}
            Check(invalid,"group field allowlist");
            var candidates=AssistantPlanner.InterpretationCandidates("count those",Context());
            invalid=false;try {AssistantPlanner.InterpretJson("count those","{\"capability\":\"tickets\",\"sql\":\"DELETE\"}",Context(),candidates,DateTime.Today);}catch(ArgumentException){invalid=true;}
            Check(invalid,"model cannot introduce SQL or filters");
            var assigned=AssistantQueryEngine.BuildQuery(Request("tickets assigned to me","tickets"),context);
            Check(assigned.CommandText.Contains("(d.AssignedUserId=@UserId OR d.EmployeeId=@EmployeeId)") && !assigned.CommandText.Contains("d.CreatedBy=@UserId OR"),"assignment restriction before aggregate");
            var developer=Context();developer.IsSuperAdmin=false;
            Check(!AssistantCatalog.Find("accounts").IsAllowed(developer) && !AssistantCatalog.Find("license-review").IsAllowed(developer),"Super Admin-only pages retain explicit guard");
            var super=Context();super.IsDeveloper=false;
            Check(!AssistantCatalog.Find("role-portals").IsAllowed(super) && !AssistantCatalog.Find("permissions").IsAllowed(super),"developer-only access sources retain explicit guard");
            var policy=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="policies",Search="borrowing overseas"},context);
            Check(policy.CommandText.Contains("policy.Body") && policy.Parameters.Values.Contains("%overseas%"),"AND policy search includes full body without exposing it");
            var month=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Summary,GroupBy="Month"},context);
            Check(month.CommandText.Contains("AT TIME ZONE") && month.Parameters.Values.Contains(context.TimeZone.Id),"UTC month grouping converts to user time zone");
            invalid=false;try {AssistantPlanner.InterpretJson("how many tickets?","{\"capability\":\"ticket-email\"}",context,AssistantCatalog.All,new DateTime(2026,10,15));}catch(ArgumentException){invalid=true;}
            Check(invalid,"model cannot override a recognized record type");
            foreach(string storedCategory in new[] {"Switch","Network Attached Storage(NAS)","Acces Point(AP)"})
            {
                var exact=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items",Category=storedCategory,ExactCategory=true,InStockOnly=true},context);
                Check(exact.CommandText.Contains("d.[Category]=@") && exact.Parameters.Values.Contains(storedCategory) && !exact.Parameters.Values.Contains("%"+storedCategory.ToLowerInvariant()+"%"),"database category is exact, not a partial category search: "+storedCategory);
            }
            var identity=Context();string original=AssistantContext.Fingerprint(identity,new DateTime(2026,10,15),"offline connection");identity.Roles=new[] {"Viewer"};
            Check(original!=AssistantContext.Fingerprint(identity,new DateTime(2026,10,15),"offline connection"),"role changes invalidate query and conversation context even when portals are unchanged");
            identity=Context();Check(original!=AssistantContext.Fingerprint(identity,new DateTime(2026,10,15),"different offline connection"),"database changes invalidate context without exposing connection details");
        }
        private static void AggregateResults()
        {
            var query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items"},Context());
            var data=Fixture(query,9,31,8);var result=AssistantQueryEngine.Render(query,data);
            Check(result.TotalRecords==9 && result.Rows.Count==8 && result.Totals["Units"]==31 && result.HasNextPage,"nine matches, eight shown, independent unit sum");
            Check(result.Text.Contains("9 matching records total") && result.Text.Contains("31"),"fact renderer uses SQL metadata");
            query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items",Page=1},Context());
            result=AssistantQueryEngine.Render(query,Fixture(query,9,31,1));Check(result.TotalRecords==9 && !result.HasNextPage,"last page total");
            query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items",Page=10},Context());
            result=AssistantQueryEngine.Render(query,Fixture(query,9,31,0));Check(result.TotalRecords==9 && result.Outcome==AssistantOutcome.Success,"empty page is not zero matches");
            result=AssistantQueryEngine.Render(query,Fixture(query,0,0,0));Check(result.Outcome==AssistantOutcome.NoMatches,"zero outcome");
            query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="invoices",Operation=AssistantOperation.Count},Context());
            result=AssistantQueryEngine.Render(query,Fixture(query,12,12345.67m,0));Check(result.Totals["AmountDue"]==12345.67m && result.Rows.Count==0,"financial decimal metadata");
            query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Summary,GroupBy="Status"},Context());
            result=AssistantQueryEngine.Render(query,Fixture(query,50,0,3));Check(result.TotalRecords==50 && result.Rows.Count==3 && result.TotalGroups==3,"group counts separate from record counts");
            query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets"},Context());
            data=Fixture(query,1,0,1);data.Rows[0]["CreatedAt"]=new DateTime(2026,10,14,18,30,0);
            Check(AssistantQueryEngine.Render(query,data).Text.Contains("CreatedAt: 2026-10-15 02:30"),"UTC display converts to local time");
            Check(AssistantQueryEngine.MetricLabel("Units","inventory")=="Movement units" && AssistantQueryEngine.MetricLabel("Units","invoice-items")=="Line quantity","quantities use their source grain");
        }
        internal static DataTable Fixture(AssistantSqlQuery query,long total,decimal sum,int rowCount)
        {
            var table=new DataTable();table.Columns.Add("TotalMatches",typeof(long));
            foreach(string aggregate in query.Capability.AggregateColumns) table.Columns.Add("Total_"+aggregate,typeof(decimal));
            if(query.IsGrouped) table.Columns.Add("TotalGroups",typeof(long));
            foreach(string column in query.OutputColumns)
                table.Columns.Add(column,column=="Id" || column=="Records"?typeof(long):column.StartsWith("Group_",StringComparison.Ordinal)?typeof(decimal):typeof(object));
            int physical=Math.Max(1,rowCount);
            for(int i=0;i<physical;i++)
            {
                var row=table.NewRow();row["TotalMatches"]=total;
                foreach(string aggregate in query.Capability.AggregateColumns) row["Total_"+aggregate]=sum;
                if(query.IsGrouped) row["TotalGroups"]=(long)rowCount;
                if(rowCount>0)
                    foreach(string column in query.OutputColumns)
                        row[column]=column=="Id"?(object)(long)(i+1):column=="Records"?(object)3L:column.StartsWith("Group_",StringComparison.Ordinal)?(object)2m:column=="Label"?"Synthetic item "+i:(object)"fixture";
                table.Rows.Add(row);
            }
            return table;
        }
        private sealed class Reader : IAssistantDataReader
        {
            internal int Calls;internal bool Fail;internal bool Timeout;internal Action BeforeReturn;internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {Calls++;Queries.Add(query);if(BeforeReturn!=null) BeforeReturn();ct.ThrowIfCancellationRequested();if(Timeout) throw new TaskCanceledException("offline simulated timeout");if(Fail) throw new InvalidOperationException("offline simulated query failure");return Task.FromResult(Fixture(query,9,31,query.Request.Operation==AssistantOperation.Count?0:8));}
        }
        private sealed class Model : IAssistantModel
        {
            internal int Explanations;internal int General;internal bool Fail=true;internal bool Timeout;internal string GuideResponse;internal string Interpretation;
            public Task<string> InterpretAsync(string question,IReadOnlyList<AssistantCapability> candidates,AssistantInterpretationContext context,CancellationToken ct) {if(Interpretation!=null) return Task.FromResult(Interpretation);throw new InvalidOperationException("offline model");}
            public Task<string> ExplainAsync(string question,IReadOnlyList<AssistantArticle> articles,CancellationToken ct) {Explanations++;if(Timeout) throw new TaskCanceledException("offline simulated model timeout");if(Fail) throw new InvalidOperationException("offline model");return Task.FromResult(GuideResponse ?? "{}");}
            public Task<string> GeneralAsync(List<ChatMessage> history,string question,CancellationToken ct) {General++;return Task.FromResult("General IT response.");}
        }
        private static async Task Conversation()
        {
            var reader=new Reader();var model=new Model();var service=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true);var context=Context();
            var result=await service.AskAsync("how many solved tickets are there?",context,CancellationToken.None);
            Check(result.IsLive && result.TotalRecords==9 && model.General==0 && model.Explanations==0,"facts bypass the model");
            await service.AskAsync("this month only",context,CancellationToken.None);Check(reader.Queries.Last().Request.Status=="Solved" && reader.Queries.Last().Request.FromLocal.HasValue,"conversation filter re-queries");
            result=await service.AskAsync("how do I borrow equipment?",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Guidance && result.Text.Contains("Guide source") && reader.Calls==2,"guidance works with Ollama offline");
            result=await service.AskAsync("Do you know Rob Meca?",context,CancellationToken.None);
            Check(result.Text.Contains("ROB MECA SANTOS") && reader.Calls==2,"self identity needs no fuzzy DB search");
            reader.Fail=true;result=await service.AskAsync("tickets",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Unavailable && !result.Text.Contains("0 matching") && model.General==0,"query failure never becomes a guessed answer");reader.Fail=false;
            bool current=true;var changing=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>current);reader.BeforeReturn=()=>current=false;
            result=await changing.AskAsync("tickets",context,CancellationToken.None);Check(result.Outcome==AssistantOutcome.Cancelled && !result.Text.Contains("Synthetic item"),"context change discards returned data");reader.BeforeReturn=null;
            using(var cts=new CancellationTokenSource())
            {reader.BeforeReturn=()=>cts.Cancel();bool cancelled=false;try {await service.AskAsync("tickets",context,cts.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"mid-query cancellation");reader.BeforeReturn=null;}
            var profileContext=Context();profileContext.IsDepartmentAccount=true;result=await service.AskAsync("who am I?",profileContext,CancellationToken.None);
            Check(result.Text.Contains("department account"),"department identity not an employee");
            model.Fail=false;model.GuideResponse="{\"answer\":\"Use Borrow Items to inspect existing loans.\",\"citations\":[\"borrow\"]}";
            result=await service.AskAsync("how do I borrow equipment?",context,CancellationToken.None);
            Check(result.Text.StartsWith("Use Borrow Items") && result.Text.Contains("Guide sources:"),"valid guide explanation is cited through the service");
            model.Timeout=true;result=await service.AskAsync("how do I borrow equipment?",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Guidance && result.Text.Contains("Guide source:"),"model timeout falls back to authored guide rather than user cancellation");model.Timeout=false;
            reader.Timeout=true;result=await service.AskAsync("tickets",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Unavailable,"database budget expiry is unavailable rather than a cancelled/zero answer");reader.Timeout=false;
            model.Interpretation="{\"capability\":\"tickets\"}";int before=reader.Calls;
            result=await service.AskAsync("how many records?",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Clarification && reader.Calls==before,"uncertain model source suggestion cannot run a database query");
        }
        private static void Knowledge()
        {
            Check(AssistantKnowledge.Articles.Count>=33 && AssistantKnowledge.Articles.Any(a=>a.Id=="ticket-status"),"embedded verified guides loaded");
            foreach(string module in new[] {"Workspace","Inventory","Requests","Consumables","Tickets","Borrowing","Repairs","Reports","Admin"})
                Check(AssistantKnowledge.Articles.Any(a=>a.Module==module),"guides cover "+module);
            Check(AssistantKnowledge.Search("how do I borrow equipment?",Context("Borrowing")).Any(a=>a.Id=="borrow"),"borrow guide retrieval");
            Check(AssistantKnowledge.Search("paano isauli ang hiniram na equipment?",Context("Borrowing")).Any(a=>a.Id=="borrow-return" || a.Id=="borrow"),"Filipino guide retrieval");
            var guide=AssistantKnowledge.Articles.First(a=>a.Id=="stock");var articles=new[] {guide};
            Check(OllamaAssistantModel.ValidateExplanation("{\"answer\":\"There are 999 keyboards.\",\"citations\":[\"stock\"]}",articles)==null,"no fabricated numbers in guide explanation");
            Check(OllamaAssistantModel.ValidateExplanation("{\"answer\":\"I have updated the stock.\",\"citations\":[\"stock\"]}",articles)==null,"no claimed write");
            Check(OllamaAssistantModel.ValidateExplanation("{\"answer\":\"Open Items to inspect recorded stock.\",\"citations\":[\"stock\"]}",articles)!=null,"cited explanation accepted");
            Check(OllamaAssistantModel.ValidateExplanation("{\"answer\":\"Open Items.\",\"citations\":[\"unknown\"]}",articles)==null,"citation allowlist");
            var restricted=Context();restricted.IsDeveloper=false;restricted.IsSuperAdmin=false;restricted.Portals=new[] {"RequesterPortal"};
            Check(AssistantKnowledge.Search("SMTP admin account configuration",restricted).All(a=>a.Module!="Admin"),"guide access checks");
            Check(AssistantScreens.SavedId(new {ItemId=-1},"items")==null && AssistantScreens.SavedId(new {ItemId=12},"items")==12,"saved positive record keys only");
            Check(YakumiAssistantHost.ModuleTypes.Values.Distinct().Count()==9,"shared host covers every module family");
            Check(AssistantScreens.All.Where(s=>s.Capability!=null).All(s=>AssistantCatalog.Find(s.Capability)!=null),"screen mappings reference registered capabilities");
        }

        private sealed class ConnectionHandler : HttpMessageHandler
        {
            internal readonly List<string> Routes=new List<string>();
            internal Func<string,CancellationToken,Task<HttpResponseMessage>> Respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            {Check(request.Method==HttpMethod.Get,"connection checks never post prompts or mutate models");Routes.Add(request.RequestUri.AbsolutePath);return Respond(request.RequestUri.AbsolutePath,ct);}
        }
        private static Task<HttpResponseMessage> ConnectionReply(string json,HttpStatusCode status=HttpStatusCode.OK)
        {return Task.FromResult(new HttpResponseMessage(status) {Content=new StringContent(json)});}
        private static async Task ConnectionSettings()
        {
            Check(OllamaConnectionSettings.NormalizeBaseUrl(" 192.168.27.80:11434/ ")=="http://192.168.27.80:11434","plain host and port accepted");
            Check(OllamaConnectionSettings.NormalizeBaseUrl("https://ai.example.test/ollama/")=="https://ai.example.test/ollama","HTTPS reverse proxy base preserved");
            int invalidIndex=0;
            foreach(string invalid in new[] {"","ftp://server","http://user:secret@server:11434","http://server:11434?token=secret","http://server:11434/#x","http://0.0.0.0:11434","http://[::]:11434","http://server:11434/api/tags","http://server:11434/api"})
            {
                bool rejected=false;try {OllamaConnectionSettings.NormalizeBaseUrl(invalid);}catch(ArgumentException) {rejected=true;}
                Check(rejected,"invalid connection address rejected before network access: "+invalidIndex++);
            }
            using(var handler=new ConnectionHandler())
            using(var client=new HttpClient(handler))
            {
                handler.Respond=(route,ct)=>ConnectionReply(route.EndsWith("/version",StringComparison.Ordinal)?"{\"version\":\"0.12.0\"}":"{\"models\":[{\"name\":\"qwen2.5:3b\"},{\"name\":\"small:latest\"}]}");
                var result=await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);
                Check(result.ModelAvailable && result.BaseUrl=="http://ai.test:11434" && result.Version=="0.12.0" && result.Model=="qwen2.5:3b","server and selected model validated together");
                Check(handler.Routes.SequenceEqual(new[] {"/api/version","/api/tags"}),"test reads only the version and installed model APIs");
                result=await OllamaConnectionSettings.ValidateAsync("ai.test:11434","small",client,CancellationToken.None);
                Check(result.Model=="small:latest","implicit latest model resolves to installed name");
                result=await OllamaConnectionSettings.ValidateAsync("ai.test:11434","missing:3b",client,CancellationToken.None);
                Check(!result.ModelAvailable && result.Models.Count==2,"reachable server with missing model is not a successful connection");
                handler.Respond=(route,ct)=>ConnectionReply(route.EndsWith("/version",StringComparison.Ordinal)?"{\"version\":\"0.12.0\"}":"{\"models\":[]}");
                result=await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);
                Check(!result.ModelAvailable && result.Models.Count==0,"empty model server cannot be saved as ready");
                foreach(string body in new[] {"<html>Proxy login</html>","{}","{\"version\":123}"})
                {
                    handler.Respond=(route,ct)=>ConnectionReply(body);
                    bool rejected=false;try {await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);}catch(InvalidOperationException) {rejected=true;}
                    Check(rejected,"non-Ollama version response rejected");
                }
                handler.Respond=(route,ct)=>ConnectionReply("{}",HttpStatusCode.Forbidden);
                bool forbidden=false;try {await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);}catch(InvalidOperationException ex) {forbidden=ex.Message.Contains("403");}
                Check(forbidden,"HTTP access failure reported");
                handler.Respond=(route,ct)=> {throw new HttpRequestException("synthetic internal transport details");};
                bool unreachable=false;try {await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);}catch(InvalidOperationException ex) {unreachable=ex.Message.Contains("firewall") && !ex.Message.Contains("synthetic");}
                Check(unreachable,"unreachable server gives actionable bounded error");
                handler.Respond=(route,ct)=>Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
                bool timeout=false;try {await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,CancellationToken.None);}catch(TimeoutException) {timeout=true;}
                Check(timeout,"connection timeout differs from user cancellation");
                using(var cancel=new CancellationTokenSource())
                {
                    cancel.Cancel();bool cancelled=false;
                    try {await OllamaConnectionSettings.ValidateAsync("ai.test:11434","qwen2.5:3b",client,cancel.Token);}catch(OperationCanceledException) {cancelled=true;}
                    Check(cancelled,"cancelled checks cannot complete as successful connections");
                }
            }
            foreach(string property in new[] {"OllamaBaseUrl","OllamaModel"})
                Check(typeof(Properties.Settings).GetProperty(property).IsDefined(typeof(System.Configuration.UserScopedSettingAttribute),false),"AI preference uses Windows user settings: "+property);
        }

        private static void ConnectionSettingsUi()
        {
            var context=Context();
            using(var view=new YakumiChatView(()=>context,new YakumiAssistantService(new AssistantQueryEngine(new Reader()),new Model(),_=>true)))
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                Func<string,object> field=name=>typeof(YakumiChatView).GetField(name,flags).GetValue(view);
                Func<string,object[],object> call=(name,args)=>typeof(YakumiChatView).GetMethod(name,flags).Invoke(view,args);
                ((System.Windows.Controls.StackPanel)field("_yakumiSettings")).Visibility=System.Windows.Visibility.Visible;
                ((System.Windows.FrameworkElement)field("_yakumiBody")).Visibility=System.Windows.Visibility.Collapsed;
                view.Measure(new System.Windows.Size(350,540));view.Arrange(new System.Windows.Rect(0,0,350,540));view.UpdateLayout();
                var first=(CancellationTokenSource)call("BeginSettingsCheck",new object[] {"Testing synthetic connection"});
                call("UpdateConnectionFields",new object[] {"http://ai.test:11434","qwen2.5:3b",new[] {"qwen2.5:3b"}});
                view.Measure(new System.Windows.Size(350,540));view.Arrange(new System.Windows.Rect(0,0,350,540));view.UpdateLayout();
                view.Dispatcher.Invoke(new Action(()=>{}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Check(!first.IsCancellationRequested && (bool)call("IsCurrentSettingsCheck",new object[] {first}),"programmatic model updates do not cancel their own connection check");
                ((System.Windows.Controls.TextBox)field("_setUrlBox")).Text="http://another.test:11434";
                Check(first.IsCancellationRequested && !(bool)call("IsCurrentSettingsCheck",new object[] {first}),"editing the server invalidates an earlier result before it can save");
                call("EndSettingsCheck",new object[] {first});
                first=(CancellationTokenSource)call("BeginSettingsCheck",new object[] {"First synthetic check"});
                var second=(CancellationTokenSource)call("BeginSettingsCheck",new object[] {"Second synthetic check"});
                call("EndSettingsCheck",new object[] {first});
                Check(!((System.Windows.Controls.Button)field("_saveSettingsButton")).IsEnabled,"old check completion cannot enable save during a newer check");
                view.Cancel();Check(second.IsCancellationRequested,"closing or cancelling chat cancels connection checks");
                call("EndSettingsCheck",new object[] {second});
                Check(((System.Windows.Controls.Button)field("_saveSettingsButton")).IsEnabled,"connection controls recover after cancellation");
            }
        }

        private static Dictionary<string,AssistantSqlQuery> QueryCases()
        {
            var cases=new Dictionary<string,AssistantSqlQuery>();var context=Context();
            foreach(var c in AssistantCatalog.All)
            {
                cases[c.Id+" list"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=c.Id},context);
                cases[c.Id+" count"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=c.Id,Operation=AssistantOperation.Count},context);
                if(c.GroupColumns.Length>0) cases[c.Id+" summary"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=c.Id,Operation=AssistantOperation.Summary,GroupBy=c.GroupColumns[0]},context);
                if(c.Columns.Contains("CreatedAt")) cases[c.Id+" dates"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=c.Id,DateField="CreatedAt",FromLocal=new DateTime(2026,10,1),ToLocalExclusive=new DateTime(2026,11,1)},context);
            }
            cases["ticket status events"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Status="Solved",DateField="StatusChangedAt",FromLocal=new DateTime(2026,10,1)},context);
            cases["ticket local months"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Summary,GroupBy="Month"},context);
            cases["policy full body search"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="policies",Search="borrow equipment"},context);
            cases["catalog category stock"]=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="items",Category="Switch",ExactCategory=true,InStockOnly=true,Operation=AssistantOperation.Count},context);
            return cases;
        }
        public static Dictionary<string,string> SqlCases() {return QueryCases().ToDictionary(p=>p.Key,p=>p.Value.CommandText);}
        public static Dictionary<string,string> SqlParameterDeclarations()
        {
            return QueryCases().ToDictionary(p=>p.Key,p=>
            {
                using(var command=SqlAssistantDataReader.CreateCommand(p.Value,null))
                    return string.Join(",",command.Parameters.Cast<System.Data.SqlClient.SqlParameter>().Select(a=>a.ParameterName+" "+
                        (a.SqlDbType==SqlDbType.Int?"int":a.SqlDbType==SqlDbType.BigInt?"bigint":a.SqlDbType==SqlDbType.DateTime2?"datetime2":"nvarchar(4000)")));
            });
        }

        public static async Task<string> OllamaSmokeAsync(CancellationToken ct)
        {
            var model=new OllamaAssistantModel();var context=Context();
            var results=new List<string>();
            foreach(string question in new[] {"How many support incidents?","Show requests awaiting fulfillment","How many items named \"Still Open\" are in stock?"})
            {
                var candidates=AssistantPlanner.InterpretationCandidates(question,context);
                string json=await model.InterpretAsync(question,candidates,new AssistantInterpretationContext(null),ct).ConfigureAwait(false);
                try
                {
                    var plan=AssistantInterpretation.Validate(question,json,context,candidates,new DateTime(2026,10,16),new AssistantInterpretationContext(null));
                    bool correct=plan.Message==null && plan.Requests.Count==1;
                    if(correct && question.Contains("incidents")) correct=plan.Requests[0].Capability=="tickets" && plan.Requests[0].Operation==AssistantOperation.Count && string.IsNullOrEmpty(plan.Requests[0].Search);
                    if(correct && question.Contains("fulfillment")) correct=plan.Requests[0].Capability=="requests" && plan.Requests[0].OutstandingOnly && string.IsNullOrEmpty(plan.Requests[0].Search);
                    if(correct && question.Contains("Still Open")) correct=plan.Requests[0].Capability=="items" && plan.Requests[0].Status==null && AssistantQuestionLanguage.SearchTerms(plan.Requests[0].Search).Contains("Still",StringComparer.OrdinalIgnoreCase);
                    results.Add(question+": "+(correct?"passed typed semantic validation":"clarification required; generic model output: "+json));
                }
                catch(ArgumentException ex) {results.Add(question+": safely rejected ("+ex.Message+")");}
            }
            var guides=AssistantKnowledge.Search("How do I borrow equipment?",context);
            var guideJson=await model.ExplainAsync("How do I borrow equipment?",guides,ct).ConfigureAwait(false);
            results.Add("Guide response: "+(OllamaAssistantModel.ValidateExplanation(guideJson,guides)!=null?"passed citation validation":"authored-guide fallback"));
            return string.Join("\n",results)+"\nOnly generic questions and curated guides were sent; no database was opened.";
        }

        public static string HostLifecycle()
        {
            var context=Context();
            return YakumiAssistantHost.VerifyIsolatedLifecycle(context,new YakumiAssistantService(new AssistantQueryEngine(new Reader()),new Model(),_=>true));
        }

        /// <summary>Explicitly invoked SELECT-only operator check; never called by offline tests or app startup.</summary>
        public static async Task<string> LiveDataChecksAsync(CancellationToken ct)
        {
            var context=Context();var engine=new AssistantQueryEngine();var lines=new List<string>();int count=0;
            foreach(var capability in AssistantCatalog.All)
            {
                var result=await engine.ExecuteAsync(new AssistantRequest {Capability=capability.Id,Operation=AssistantOperation.Count},context,ct).ConfigureAwait(false);
                if(!result.IsLive) throw new InvalidOperationException("Read-only count failed for "+capability.Id+": "+result.Outcome);
                count++;
            }
            lines.Add(count+" live capability count/aggregate queries completed without modifications (operator inspection scope, not authenticated-user permission parity).");
            var model=new Model();var service=new YakumiAssistantService(engine,model,_=>true,new AssistantStockVocabulary());
            var samples=new[] {
                new[] {"How many Redmi phones do I have in stock?","Cellphone","redmi"},
                new[] {"How many phones do I have in stock?","Cellphone",""},
                new[] {"How many cellphones do I have in stock?","Cellphone",""},
                new[] {"How many Samsung phones are in stock?","Cellphone","samsung"},
                new[] {"How many headsets are in stock?","Headset",""},
                new[] {"How many switches are in stock?","Switch",""},
                new[] {"How many access points are in stock?","Acces Point(AP)",""},
                new[] {"How many CPUs do I have in stock?","CPU",""},
                new[] {"How many SSDs do I have in stock?","SSD",""},
                new[] {"How many TVs do I have in stock?","TV",""},
                new[] {"How many NAS are in stock?","Network Attached Storage(NAS)",""},
                new[] {"How many boots are in stock?","Boots",""},
                new[] {"How many Redmi do we have?","","redmi"}
            };
            using(var connection=new System.Data.SqlClient.SqlConnection(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString))
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
                foreach(var sample in samples)
                {
                    var result=await service.AskAsync(sample[0],context,ct).ConfigureAwait(false);
                    if(!result.IsLive || result.Request.Capability!="items" || result.Request.Category!= (sample[1].Length==0?null:sample[1]) || (result.Request.Search ?? "")!=sample[2])
                        throw new InvalidOperationException("Live item interpretation failed for "+sample[0]+" ("+result.Outcome+").");
                    using(var command=connection.CreateCommand())
                    {
                        command.CommandTimeout=15;
                        command.CommandText=@"SELECT COUNT_BIG(*),ISNULL(SUM(CAST(i.StockOnHand AS decimal(28,4))),0) FROM dbo.Item i
                            WHERE i.Active=1 AND i.StockOnHand>0 AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus a WHERE a.EntityType='Item' AND a.EntityId=i.ItemId AND a.IsArchived=1)
                            AND (@Category='' OR REPLACE(i.Category,' ','')=REPLACE(@Category,' ',''))
                            AND (@Brand='' OR i.Name LIKE @Pattern OR i.ModelNumber LIKE @Pattern OR i.SerialNumber LIKE @Pattern OR i.Category LIKE @Pattern)";
                        command.Parameters.Add("@Category",SqlDbType.NVarChar,200).Value=sample[1];
                        command.Parameters.Add("@Brand",SqlDbType.NVarChar,200).Value=sample[2];
                        command.Parameters.Add("@Pattern",SqlDbType.NVarChar,200).Value="%"+sample[2]+"%";
                        using(var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
                        {
                            await reader.ReadAsync(ct).ConfigureAwait(false);
                            if(result.TotalRecords!=reader.GetInt64(0) || result.Totals["Units"]!=reader.GetDecimal(1)) throw new InvalidOperationException("Independent SQL totals disagreed for "+sample[0]+": assistant "+result.TotalRecords+" records / "+result.Totals["Units"]+" units; reference "+reader.GetInt64(0)+" records / "+reader.GetDecimal(1)+" units.");
                        }
                    }
                    lines.Add(sample[0]+" => "+result.Totals["Units"]+" units; "+result.TotalRecords+" matching records. Independent SQL agrees.");
                }
            }
            if(model.General!=0 || model.Explanations!=0) throw new InvalidOperationException("Stock questions reached a model.");
            lines.Add("No business rows or counts were sent to Ollama. No user, machine or database settings were changed.");
            return string.Join("\n",lines);
        }

        public static string RenderConnectionPreview(string path) {return RenderPreview(path,350,540,true);}
        public static string RenderPreview(string path,int width=350,int height=540,bool settings=false)
        {
            if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA) throw new InvalidOperationException("UI checks require STA.");
            var context=Context();context.Page="WpfCallMonitoringWorkspace";
            var view=new YakumiChatView(()=>context,new YakumiAssistantService(new AssistantQueryEngine(new Reader()),new Model(),_=>true));
            try
            {
                view.RefreshContext(context);
                if(settings)
                {
                    Func<string,object> field=name=>typeof(YakumiChatView).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);
                    ((System.Windows.Controls.StackPanel)field("_yakumiSettings")).Visibility=System.Windows.Visibility.Visible;
                    ((System.Windows.FrameworkElement)field("_yakumiBody")).Visibility=System.Windows.Visibility.Collapsed;
                    typeof(YakumiChatView).GetMethod("UpdateConnectionFields",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                        .Invoke(view,new object[] {"http://AI-PC:11434","qwen2.5:3b",new[] {"qwen2.5:3b"}});
                    var status=(System.Windows.Controls.TextBlock)field("_setStatusText");
                    status.Text="Connected and saved for this Windows user. qwen2.5:3b is available on AI-PC.";
                    status.Foreground=System.Windows.Media.Brushes.ForestGreen;
                }
                else view.SubmitAsync("How may solved tickets are there?").GetAwaiter().GetResult();
                view.Width=width;view.Height=height;view.Measure(new System.Windows.Size(width,height));view.Arrange(new System.Windows.Rect(0,0,width,height));view.UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(view);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using(var stream=System.IO.File.Create(path)) png.Save(stream);
                return "Isolated WPF assistant rendered with synthetic records: "+path;
            }
            finally {view.Dispose();}
        }
    }
}
