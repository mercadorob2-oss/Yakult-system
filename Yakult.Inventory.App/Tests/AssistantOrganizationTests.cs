using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    public static class AssistantOrganizationTests
    {
        private static int _checks;
        private static void Check(bool value,string message) {if(!value) throw new InvalidOperationException("Yakumi organizations: "+message);_checks++;}
        private static readonly AssistantOrganization[] Organizations={
            new AssistantOrganization("Company",1,"Yakult Marketing Corporation","YMC"),
            new AssistantOrganization("Company",2,"Yakult Philippines","YPI"),
            new AssistantOrganization("Company",3,"YMC Services","YMCS"),
            new AssistantOrganization("Branch",10,"Pasay Center","PSY-CT"),
            new AssistantOrganization("Department",20,"Information Technology","IT"),
            new AssistantOrganization("Branch",11,"Central","HQ"),
            new AssistantOrganization("Department",21,"Central","HQ"),
            new AssistantOrganization("Company",4,"Company A","DUP"),
            new AssistantOrganization("Company",5,"Company B","DUP")
        };
        private static readonly string[] Fields={"Company","Branch","Department"};
        private sealed class Vocabulary : IAssistantOrganizationVocabulary
        {
            internal bool Unavailable;internal Action During;internal int Reads;
            public Task<AssistantOrganizationResolution> ResolveAsync(string q,IReadOnlyList<AssistantRequest> requests,IReadOnlyList<AssistantCapability> sources,AssistantContext context,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var fields=sources.Where(c=>c.IsAllowed(context)).SelectMany(c=>c.SupportedFilters).Where(Fields.Contains).Distinct().ToArray();
                if(fields.Length==0 || AssistantOrganizationVocabulary.Candidates(q,requests).Count==0) return Task.FromResult<AssistantOrganizationResolution>(null);
                Reads++;During?.Invoke();if(Unavailable) throw new IOException("Synthetic directory unavailable");
                return Task.FromResult(AssistantOrganizationVocabulary.Match(q,requests,fields,Organizations));
            }
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;internal string Json;internal string Question;internal AssistantInterpretationContext Context;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext previous,CancellationToken ct)
            {Calls++;Question=q;Context=previous;if(Json==null) throw new InvalidOperationException("Synthetic AI unavailable");return Task.FromResult(Json);}
            public Task<string> GeneralAsync(List<ChatMessage> h,string q,CancellationToken ct) {throw new InvalidOperationException("Synthetic AI unavailable");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {throw new InvalidOperationException("Synthetic AI unavailable");}
        }
        private sealed class Row
        {internal int Company,Branch,Department,Creator;internal string Status,Issue;}
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            internal bool Unavailable;internal bool AddRow;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();Queries.Add(query);if(Unavailable) throw new IOException("Synthetic database unavailable");
                var rows=new List<Row> {
                    new Row {Company=1,Branch=10,Department=20,Creator=101,Status="Open",Issue="Printer"},
                    new Row {Company=1,Branch=10,Department=21,Creator=999,Status="Solved",Issue="Network"},
                    new Row {Company=2,Branch=10,Department=20,Creator=101,Status="Open",Issue="YMC printer"},
                    new Row {Company=3,Branch=11,Department=20,Creator=101,Status="Open",Issue="Printer"}
                };
                if(AddRow) rows.Add(new Row {Company=1,Branch=10,Department=20,Creator=101,Status="Open",Issue="New ticket"});
                IEnumerable<Row> selected=rows;
                foreach(var match in query.Request.Organizations)
                {
                    if(!match.Id.HasValue) {selected=new Row[0];break;}
                    if(match.Field=="Company") selected=selected.Where(r=>r.Company==match.Id);
                    if(match.Field=="Branch") selected=selected.Where(r=>r.Branch==match.Id);
                    if(match.Field=="Department") selected=selected.Where(r=>r.Department==match.Id);
                }
                foreach(string term in AssistantQuestionLanguage.SearchTerms(query.Request.Search)) selected=selected.Where(r=>r.Issue.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0);
                if(query.Request.Status!=null) selected=selected.Where(r=>r.Status==query.Request.Status);
                if(query.Request.Personal || (query.Request.Capability=="requests" && !query.Capability.IsAllowed(ExpandedAssistantTests.Context()))) selected=selected.Where(r=>r.Creator==101);
                int total=selected.Count();return Task.FromResult(ExpandedAssistantTests.Fixture(query,total,total,query.Request.Operation==AssistantOperation.Count?0:Math.Min(total,AssistantQueryEngine.PageSize)));
            }
        }
        private static YakumiAssistantService Service(Reader reader,Vocabulary vocabulary=null,Model model=null,Func<AssistantContext,bool> current=null)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),model ?? new Model(),current ?? (_=>true),organizations:vocabulary ?? new Vocabulary());}
        private static AssistantOrganizationResolution Resolve(string question,params AssistantRequest[] previous)
        {
            var plan=AssistantPlanner.Plan(question,ExpandedAssistantTests.Context(),previous);
            return AssistantOrganizationVocabulary.Match(question,plan.Requests,Fields,Organizations);
        }
        public static string RunAll()
        {
            _checks=0;Parsing();Conversation().GetAwaiter().GetResult();Ambiguity().GetAwaiter().GetResult();Boundaries().GetAwaiter().GetResult();Sql();
            return _checks+" company, branch, department and relationship checks passed.";
        }
        private static void Parsing()
        {
            foreach(string question in new[] {"how many tickets are there in ymc","HOW MANY TICKETS IN YMC?","how many YMC tickets?","tickets from YMC","tickets for YMC","tickets at YMC","tickets under YMC","tickets within YMC","tickets in company YMC","tickets in YMC company","tickets in \"Yakult Marketing Corporation\"","tickets in Y.M.C."})
            {
                var resolved=Resolve(question);Check(resolved.Uncertain==null && resolved.Mentions.Count==1,"unique company evidence: "+question);
                var plan=AssistantPlanner.Plan(resolved.Rewrite(question),ExpandedAssistantTests.Context());
                Check(plan.Message==null && !plan.NeedsInterpretation && plan.Requests.Count==1,"resolved organization grammar is a local lookup: "+question);
                resolved.Apply(plan.Requests[0]);var request=plan.Requests[0];
                Check(request.Company=="Yakult Marketing Corporation" && string.IsNullOrEmpty(request.Search) && request.Organizations.Single().Id==1,"company alias becomes typed exact relationship, never text search: "+question);
            }
            foreach(string question in new[] {"tickets named \"YMC\"","tickets named \"in YMC\"","tickets in progress","tickets from 2026-10-01 to 2026-10-16","tickets by company this month"})
                Check(Resolve(question).Mentions.Count==0,"names, statuses, dates and grouping are not organization filters: "+question);
            var unsupported=AssistantOrganizationVocabulary.Match("stock in YMC",new[] {new AssistantRequest {Capability="items",Search="ymc"}},new string[0],Organizations);
            Check(unsupported.Mentions.Count==0,"unsupported organization fields are not invented");
            var overlapping=new[] {new AssistantOrganization("Company",17,"YMC"),new AssistantOrganization("Department",75,"YMC - HO","YMC")};
            foreach(string question in new[] {"how many tickets are there in ymc","how many YMC tickets?","tickets in company YMC"})
            {
                var plan=AssistantPlanner.Plan(question,ExpandedAssistantTests.Context());
                var resolved=AssistantOrganizationVocabulary.Match(question,plan.Requests,Fields,overlapping);
                Check(resolved.Uncertain==null && resolved.Mentions.Single().Choices.Single().Id==17,"complete company name takes precedence over a different department's acronym: "+question);
            }
            var department=AssistantOrganizationVocabulary.Match("tickets in department YMC",new AssistantRequest[0],Fields,overlapping);
            Check(department.Mentions.Single().Choices.Single().Id==75,"explicit department role overrides implicit full-name precedence");
        }
        private static async Task Conversation()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();var service=Service(reader,model:model);
            var answer=await service.AskAsync("how many tickets are there in ymc",context,CancellationToken.None);
            Check(answer.IsLive && answer.TotalRecords==2 && model.Calls==0,"reproduced question counts tickets linked to YMC with AI offline");
            var query=reader.Queries.Last();Check(query.CommandText.Contains("d.[CompanyId]=") && query.Parameters.Values.Contains(1) && !query.Parameters.Values.Contains("%ymc%"),"SQL filters the ticket company ID, not issue text or assignee company");
            Check(query.CommandText.Contains("t.ComId CompanyId") && query.CommandText.Contains("co.ComId=t.ComId"),"company link follows ticket record");
            Check(answer.Text.Contains("Yakult Marketing Corporation has 2 IT tickets") && AssistantPresentationFormatter.Format(answer).CopyText.Contains("Yakult Marketing Corporation"),"answer and Copy identify counted company");
            reader.AddRow=true;answer=await service.AskAsync("how many tickets in YMC?",context,CancellationToken.None);
            Check(answer.TotalRecords==3,"reference vocabulary never caches business totals");reader.AddRow=false;
            answer=await service.AskAsync("how many solved tickets in YMC this month?",context,CancellationToken.None);
            Check(answer.TotalRecords==1 && answer.Request.Status=="Solved" && answer.Request.FromLocal.HasValue && answer.Request.DateField=="StatusChangedAt","status and date semantics retained with company filter");
            answer=await service.AskAsync("what about YPI?",context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.NoMatches && answer.Request.Company=="Yakult Philippines" && answer.Request.Status=="Solved" && answer.Request.FromLocal.HasValue,"company replacement retains compatible filters and a true zero: "+answer.Text);
            answer=await service.AskAsync("how many tickets in YMC and in IT?",context,CancellationToken.None);
            Check(answer.TotalRecords==1 && answer.Request.Company=="Yakult Marketing Corporation" && answer.Request.Department=="Information Technology" && answer.Request.Organizations.Count==2,"company and department links combine with AND");
            answer=await service.AskAsync("how many tickets in YMC at PSY-CT in department IT?",context,CancellationToken.None);
            Check(answer.TotalRecords==1 && answer.Request.Organizations.Count==3 && reader.Queries.Last().CommandText.Contains("d.[BranchId]="),"company, branch acronym and department combine");
            answer=await service.AskAsync("how many my tickets in YMC?",context,CancellationToken.None);
            Check(answer.TotalRecords==1 && answer.Request.Personal && reader.Queries.Last().CommandText.Contains("@UserId"),"ownership still constrains company count");
            answer=await service.AskAsync("how many requests in YMC?",context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.Capability=="requests" && answer.Request.Organizations.Single().Id==1 && reader.Queries.Last().CommandText.Contains("r.ComId CompanyId"),"requests expose their actual company link");
            foreach(string source in new[] {"repairs","employees","sets"})
            {
                answer=await service.AskAsync("how many "+source+" in YMC?",context,CancellationToken.None);
                Check(answer.IsLive && answer.Request.Company=="Yakult Marketing Corporation" && reader.Queries.Last().CommandText.Contains("d.[CompanyId]="),"organization resolution reused by "+source);
            }
            answer=await service.AskAsync("how many tickets in company Atlantis?",context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.NoMatches && reader.Queries.Last().CommandText.Contains("d.[Company]=") && !reader.Queries.Last().Parameters.Values.Contains("%atlantis%"),"explicit unknown company remains an exact filter and true zero");
            answer=await service.AskAsync("show tickets named \"YMC\"",context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.Company==null && answer.Request.Organizations.Count==0 && answer.TotalRecords==1,"quoted ticket search does not become company lookup");
        }
        private static async Task Ambiguity()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            var answer=await service.AskAsync("how many tickets in HQ?",context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==0 && answer.ClarificationChoices.Count==2,"shared branch/department alias requires one focused choice");
            var choice=answer.ClarificationChoices.Single(c=>c.Label.StartsWith("Branch:"));
            var selected=await service.ResolveClarificationAsync(choice,context,CancellationToken.None);
            Check(selected.IsLive && selected.Request.Branch=="Central" && selected.Request.Department==null && selected.Request.Organizations.Single().Id==11,"choice binds the selected actual branch identity");
            int before=reader.Queries.Count;await service.ResolveClarificationAsync(choice,context,CancellationToken.None);
            Check(reader.Queries.Count==before,"organization choice can execute once");
            answer=await service.AskAsync("how many tickets in company DUP?",context,CancellationToken.None);
            Check(answer.ClarificationChoices.Count==2 && reader.Queries.Count==before,"duplicate company acronym is never guessed");
            selected=await service.AskAsync(answer.ClarificationChoices.Single(c=>c.Label.Contains("Company B")).Label,context,CancellationToken.None);
            Check(selected.Request.Organizations.Single().Id==5,"typed choice label selects distinct company");
            answer=await service.AskAsync("how many tickets in Atlantis?",context,CancellationToken.None);
            Check(answer.ClarificationChoices.Count==3,"unknown location asks company, branch or department instead of searching ticket text");
            selected=await service.ResolveClarificationAsync(answer.ClarificationChoices.Single(c=>c.Label.StartsWith("Company:")),context,CancellationToken.None);
            Check(selected.Outcome==AssistantOutcome.NoMatches && string.Equals(selected.Request.Company,"atlantis",StringComparison.OrdinalIgnoreCase),"explicit unknown-location role keeps truthful zero");
        }
        private static async Task Boundaries()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var vocabulary=new Vocabulary();var service=Service(reader,vocabulary);
            var pending=await service.AskAsync("tickets in HQ",context,CancellationToken.None);var old=pending.ClarificationChoices[0];service.Reset();
            Check((await service.ResolveClarificationAsync(old,context,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==0,"new chat expires directory choices");
            vocabulary.During=service.Reset;
            Check((await service.AskAsync("how many tickets in YMC?",context,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==0,"stale directory response cannot start a query");vocabulary.During=null;
            using(var cancellation=new CancellationTokenSource())
            {
                vocabulary.During=cancellation.Cancel;bool stopped=false;
                try {await service.AskAsync("tickets in YMC",context,cancellation.Token);} catch(OperationCanceledException) {stopped=true;}
                Check(stopped && reader.Queries.Count==0,"cancelled organization lookup cannot query tickets");vocabulary.During=null;
            }
            vocabulary.Unavailable=true;var result=await service.AskAsync("tickets in YMC",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Unavailable && reader.Queries.Count==0,"directory outage is not a company zero count");
            Check((await service.AskAsync("how many tickets?",context,CancellationToken.None)).IsLive,"clear lookups without organizations still work during directory outage");vocabulary.Unavailable=false;
            reader.Unavailable=true;Check((await service.AskAsync("tickets in YMC",context,CancellationToken.None)).Outcome==AssistantOutcome.Unavailable,"business query outage is not a zero count");reader.Unavailable=false;
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new string[0];int before=reader.Queries.Count;
            Check(!(await service.AskAsync("tickets in YMC",denied,CancellationToken.None)).IsLive && reader.Queries.Count==before,"source permissions remain enforced");
            result=await service.AskAsync("tickets in YMC except closed",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before,"exclusion is never dropped when resolving company");
            var model=new Model {Json=new JObject {["clarification"]="",["compare"]=false,["requests"]=new JArray(new JObject {["capability"]="tickets",["operation"]="Count",["sourceEvidence"]="support incidents",["inherit"]=false,["filters"]=new JObject {["company"]="YMC"}})}.ToString()};
            service=Service(reader,model:model);const string question="how many support incidents in YMC?";
            result=await service.AskAsync(question,context,CancellationToken.None);
            Check(result.IsLive && result.Request.Company=="Yakult Marketing Corporation" && model.Question==question,"unfamiliar wording keeps original question with grounded local organization evidence");
            string prompt=AssistantInterpretation.Prompt(AssistantCatalog.All.Take(6).ToList(),model.Context,question);
            Check(prompt.Contains("Company=YMC") && !prompt.Contains("Yakult Marketing Corporation") && !prompt.Contains("CompanyId"),"AI receives role and original alias, never directory records or IDs");
        }
        private static void Sql()
        {
            using(var command=AssistantOrganizationVocabulary.CreateCommand(new[] {"YMC' OR 1=1", "PSY-CT"},Fields))
            {
                Check(!command.CommandText.Contains("YMC' OR") && command.Parameters.Count==4 && command.CommandText.Contains("TOP (64)"),"reference lookup bounded and parameterized");
                Check(!command.CommandText.Contains("CallTicket") && !command.CommandText.Contains("COUNT("),"reference lookup reads names and aliases only");
            }
            var request=new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Company="Other",Organizations=new[] {Organizations[0]}};
            bool rejected=false;try {AssistantQueryEngine.BuildQuery(request,ExpandedAssistantTests.Context());} catch(ArgumentException) {rejected=true;}
            Check(rejected,"stale typed organization identity is rejected");
            request.Company=Organizations[0].Name;var copy=request.Copy();
            Check(ReferenceEquals(copy.Organizations,request.Organizations) && copy.Organizations[0].Id==1,"copied immutable organization identity remains consistent");
        }
        internal static YakumiAssistantService PreviewService() {return Service(new Reader());}
        public static async Task<string> LiveChecksAsync(CancellationToken ct)
        {
            int companyId;
            using(var connection=new SqlConnection(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString))
            using(var command=connection.CreateCommand())
            {
                command.CommandTimeout=8;command.CommandText="SELECT ComId FROM dbo.Company WHERE UPPER(Name)=@Alias OR UPPER(Acronym)=@Alias";
                command.Parameters.Add("@Alias",SqlDbType.NVarChar,20).Value="YMC";
                await connection.OpenAsync(ct).ConfigureAwait(false);
                var ids=new List<int>();using(var reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false)) while(await reader.ReadAsync(ct).ConfigureAwait(false)) ids.Add(reader.GetInt32(0));
                if(ids.Count!=1) return "YMC is not a unique company in this database. No business counts were queried; clarification remains required.";
                companyId=ids[0];
            }
            var model=new Model();var service=new YakumiAssistantService(new AssistantQueryEngine(),model,_=>true,organizations:new AssistantOrganizationVocabulary());
            var context=ExpandedAssistantTests.Context();int checks=0;
            foreach(string question in new[] {"how many tickets are there in ymc","how many YMC tickets?","how many tickets for YMC?","how many tickets in company YMC?","how many solved tickets in YMC?"})
            {
                var result=await service.AskAsync(question,context,ct).ConfigureAwait(false);
                long expected;
                using(var connection=new SqlConnection(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString))
                using(var command=connection.CreateCommand())
                {
                    command.CommandTimeout=8;command.CommandText="SELECT COUNT_BIG(*) FROM dbo.CallTicket WHERE ComId=@CompanyId"+(question.Contains("solved")?" AND Status=@Status":"");
                    command.Parameters.Add("@CompanyId",SqlDbType.Int).Value=companyId;
                    if(question.Contains("solved")) command.Parameters.Add("@Status",SqlDbType.NVarChar,50).Value="Solved";
                    await connection.OpenAsync(ct).ConfigureAwait(false);expected=Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
                }
                if(!result.IsLive || result.TotalRecords!=expected || result.Request.Organizations.SingleOrDefault(o=>o.Field=="Company")?.Id!=companyId || model.Calls!=0)
                    throw new InvalidOperationException("Live company check failed for '"+question+"': outcome="+result.Outcome+", actual="+result.TotalRecords+", expected="+expected+", companyId="+result.Request?.Organizations.SingleOrDefault(o=>o.Field=="Company")?.Id+", modelCalls="+model.Calls+", message="+result.MessageText);
                checks++;
            }
            return checks+" live YMC question variants matched independent ticket.ComId counts. Reference names and aggregate quantities only; no writes or AI calls. Operator inspection scope, not authenticated-user permission certification.";
        }
        public static Dictionary<string,string> SqlCases()
        {
            var result=new Dictionary<string,string>();
            foreach(string source in new[] {"tickets","requests","repairs","employees","sets","invoices"})
                result.Add("company relationship "+source,AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=source,Operation=AssistantOperation.Count,Company=Organizations[0].Name,Organizations=new[] {Organizations[0]}},ExpandedAssistantTests.Context()).CommandText);
            using(var command=AssistantOrganizationVocabulary.CreateCommand(new[] {"YMC","PSY-CT","IT"},Fields)) result.Add("organization reference lookup",command.CommandText);
            return result;
        }
    }
}
