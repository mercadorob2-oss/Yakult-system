using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    public static class AssistantWordMappingTests
    {
        private static int _checks;
        private static readonly DateTime Today=new DateTime(2026,10,17);
        private static void Check(bool condition,string message)
        {if(!condition) throw new InvalidOperationException("Yakumi word mapping: "+message);_checks++;}
        private static readonly string[][] Pairs={
            new[] {"Pls show my tix","show my tickets"},
            new[] {"Plz count tkts","count tickets"},
            new[] {"Kindly hw mny tckts","how many tickets"},
            new[] {"Can u count my requets","how many my requests"},
            new[] {"Could u give me the number of reqs","how many requests"},
            new[] {"Show me my requestes","show my requests"},
            new[] {"Lemme see my tix","show my tickets"},
            new[] {"Pakita ng mga tix","show tickets"},
            new[] {"Ipakita ang mga reqs","show requests"},
            new[] {"Patingin ng mga tkts","show tickets"},
            new[] {"Ilan ang mga pending reqs ngayong buwan","how many pending requests this month"},
            new[] {"Ilang mga tix ngayong linggo","how many tickets this week"},
            new[] {"Gaano karaming reqs nakaraang linggo","how many requests last week"},
            new[] {"How many ongoing tix","how many in progress tickets"},
            new[] {"Show tickets wip","show in progress tickets"},
            new[] {"Count inprogress tickets","count in progress tickets"},
            new[] {"Count in-progress tickets","count in progress tickets"},
            new[] {"How many helpdesk tickets","how many tickets"},
            new[] {"Show IT support tickets","show tickets"},
            new[] {"Show service desk tickets","show tickets"},
            new[] {"Count trouble tickets","count tickets"},
            new[] {"Show requests waiting for fulfillment","show unfulfilled requests"},
            new[] {"Show requests awaiting fulfillment","show unfulfilled requests"},
            new[] {"Count requests not yet fulfilled","count unfulfilled requests"},
            new[] {"How many LX310 on hand","how many LX310 in stock"},
            new[] {"How many LX310 items on-hand","how many LX310 items in stock"},
            new[] {"How many tickets assigned to myself this wk","how many tickets assigned to me this week"},
            new[] {"How many tickets raised by me last mo","how many tickets created by me last month"},
            new[] {"Show my requests filed by me previous week","show my requests created by me last week"},
            new[] {"Count tickets logged by me","count tickets created by me"},
            new[] {"Count requests submitted by me","count requests created by me"},
            new[] {"Show tickets handled by me","show tickets assigned to me"},
            new[] {"Count tickets broken down by dept","count tickets by department"},
            new[] {"Count tickets for each department","count tickets by department"},
            new[] {"Show tickets split by status","show tickets by status"},
            new[] {"Count tickets prio High","count high priority tickets"},
            new[] {"Show tickets current month","show tickets this month"},
            new[] {"Show requests previous year","show requests last year"},
            new[] {"Count requests this yr","count requests this year"},
            new[] {"Show tickets ngayong araw","show tickets today"},
            new[] {"Show tickets nakaraang taon","show tickets last year"},
            new[] {"Show tickets right now","show tickets"},
            new[] {"Show items in stock at the moment","show items in stock"},
            new[] {"Show tix #69","TCK-69"},
            new[] {"Show reqs #17","REQ-17"},
            new[] {"Show repair #12","RPR-12"},
            new[] {"Count tickets MTD","count tickets from 2026-10-01 to 2026-10-17"},
            new[] {"Count tickets WTD","count tickets from 2026-10-12 to 2026-10-17"},
            new[] {"Count tickets YTD","count tickets from 2026-01-01 to 2026-10-17"}
        };
        private static AssistantRequest Request(string question,IReadOnlyList<AssistantRequest> previous=null)
        {
            var plan=AssistantPlanner.Plan(question,ExpandedAssistantTests.Context(),previous,Today);
            Check(plan.Message==null && !plan.NeedsInterpretation && plan.Requests.Count==1,"clear local wording: "+question+" / "+plan.Message+" / "+plan.UnresolvedMeaning);
            return plan.Requests.Single();
        }
        private static void Equivalent(AssistantRequest actual,AssistantRequest expected,string question)
        {
            var context=ExpandedAssistantTests.Context();var first=AssistantQueryEngine.BuildQuery(actual,context);var second=AssistantQueryEngine.BuildQuery(expected,context);
            Check(actual.Capability==expected.Capability && actual.Operation==expected.Operation && actual.Search==expected.Search && actual.Status==expected.Status && actual.PersonalRelation==expected.PersonalRelation && actual.GroupBy==expected.GroupBy,"same typed intent and meaningful filters: "+question);
            Check(first.CommandText==second.CommandText && first.Parameters.OrderBy(p=>p.Key).SequenceEqual(second.Parameters.OrderBy(p=>p.Key)),"same parameterized SQL and scope: "+question);
            var result=AssistantQueryEngine.Render(first,ExpandedAssistantTests.Fixture(first,2,254,0));
            Check(result.TotalRecords==2 && (!actual.InStockOnly || result.Totals["Units"]==254),"synthetic count and stock quantity stay distinct: "+question);
        }
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();internal int Records=2;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {ct.ThrowIfCancellationRequested();Queries.Add(query);return Task.FromResult(ExpandedAssistantTests.Fixture(query,Records,Records==0?0:254,query.Request.Operation==AssistantOperation.Count?0:Math.Min(Records,1)));}
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext previous,CancellationToken ct)
            {Calls++;throw new InvalidOperationException("Synthetic offline AI");}
            public Task<string> GeneralAsync(List<ChatMessage> h,string q,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline AI");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {return Task.FromResult("{}");}
        }
        private static YakumiAssistantService Service(Reader reader,Model model)
        {
            var records=new[] {new AssistantOrganization("Company",17,"Yakult Marketing Corporation","YMC"),new AssistantOrganization("Company",18,"Yakult Philippines","YPI"),
                new AssistantOrganization("Department",75,"Information Technology","IT"),new AssistantOrganization("Branch",10,"Main Office","MO")};
            var directory=new AssistantOrganizationVocabulary((values,fields,ct)=>Task.FromResult<IReadOnlyList<AssistantOrganization>>(records.Where(r=>fields.Contains(r.Field) && values.Any(v=>v.Equals(r.Name,StringComparison.OrdinalIgnoreCase) || v.Equals(r.Acronym,StringComparison.OrdinalIgnoreCase))).ToArray()));
            return new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true,organizations:directory);
        }
        public static string RunAll()
        {
            _checks=0;Mappings();Protection();FollowUps();Conversation().GetAwaiter().GetResult();
            return _checks+" everyday word mapping, date, relationship and clarification checks passed.";
        }
        private static void Mappings()
        {
            foreach(var pair in Pairs)
            {
                var expected=Request(pair[1]);
                foreach(string question in new[] {pair[0],pair[0].ToUpperInvariant()+"?!"}) Equivalent(Request(question),expected,question);
            }
            var mtd=Request("count tickets month to date");
            Check(mtd.FromLocal==new DateTime(2026,10,1) && mtd.ToLocalExclusive==Today.AddDays(1),"to-date periods stop after today rather than including the rest of the month");
            Check(AssistantEnglish.Period(mtd).Contains("October 17, 2026"),"date wording explains the actual inclusive final day");
            Check(AssistantPlanner.InterpretationCandidates("show complicated reqs",ExpandedAssistantTests.Context()).Any(c=>c.Id=="requests"),"AI candidate selection understands mapped source words too");
            foreach(string q in new[] {"count tickets MTD last month","count tickets WTD today","count tickets YTD this year"})
                Check(AssistantPlanner.Plan(q,ExpandedAssistantTests.Context(),todayOverride:Today).Message!=null,"conflicting periods require a choice: "+q);
        }
        private static void Protection()
        {
            foreach(string name in new[] {"tix","reqs","ongoing","urgent","done","on hand","Current Month","MTD","Dept IT","Can u count","Not Yet Fulfilled","Still Open"})
            {
                var request=Request("count items named \""+name+"\"");var query=AssistantQueryEngine.BuildQuery(request,ExpandedAssistantTests.Context());
                Check(request.Status==null && request.Condition==null && !request.FromLocal.HasValue && request.Search=="\""+name+"\"","quoted names never become mapped filters: "+name);
                foreach(string term in AssistantQuestionLanguage.LiteralTerms(name)) Check(query.Parameters.Values.Contains("%"+term+"%"),"quoted name term remains parameterized: "+term);
            }
            foreach(string name in new[] {"ongoing","wip","previous month","done","urgent"})
            {
                var request=Request("show tickets named "+name);
                Check(request.Status==null && !request.FromLocal.HasValue && request.Search.Trim('"')==name,"explicit unquoted search names are protected: "+name);
            }
            var employee=Request("show tickets assigned to WIP");Check(employee.Employee=="wip" && employee.Status==null,"unquoted employee name is not a status alias");
            var company=Request("show tickets in company Ongoing");Check(company.Company=="ongoing" && company.Status==null,"unquoted organization name is not a status alias");
            var product=Request("how many TIX items in stock");Check(product.Capability=="items" && product.Search=="tix","a product name beside an explicit stock source is not changed into tickets");
            foreach(string phrase in new[] {"IT tickets","helpdesk tickets","IT support tickets","service desk tickets"})
                Check(AssistantOrganizationVocabulary.Candidates("count "+phrase,new[] {new AssistantRequest {Capability="tickets"}}).All(m=>m.Value!="IT" && m.Value!="helpdesk" && m.Value!="IT support" && m.Value!="service desk"),"ticket source wording does not introduce an organization filter: "+phrase);
            Check(AssistantPlanner.Plan("pls delete all tix",ExpandedAssistantTests.Context()).GuidanceQuestion!=null,"polite shorthand cannot turn a write request into a read");
            Check(AssistantPlanner.Plan("pls "+new string('x',1201),ExpandedAssistantTests.Context()).Message.Contains("shorten"),"original input size is checked before shortening its wording");
        }
        private static void FollowUps()
        {
            var stock=Request("how many LX310 items in stock");
            foreach(string q in new[] {"how about LX350?","filter to brand new","limit to this wk","keep it to this mo","same as before, but only this year","show more","go back"})
            {
                var next=Request(q,new[] {stock});
                Check(next.Capability=="items" && next.InStockOnly && (q.StartsWith("how about")?next.Search=="lx350":next.Search=="lx310"),"follow-up retains compatible item scope: "+q);
            }
            var ticket=Request("how many my solved tickets in company YMC this month");
            Check(Request("show more",new[] {ticket}).Page==0 && Request("show more",new[] {ticket}).Operation==AssistantOperation.List,"showing records after a count starts at the first page");
            var dated=Request("MTD",new[] {ticket});Check(dated.Company=="ymc" && dated.Personal && dated.Status=="Solved" && dated.FromLocal==new DateTime(2026,10,1) && dated.ToLocalExclusive==Today.AddDays(1),"to-date follow-up retains organization, status and ownership");
            var replaced=Request("in co YPI",new[] {ticket});Check(replaced.Company=="ypi" && replaced.Personal && replaced.Status=="Solved" && replaced.FromLocal==ticket.FromLocal,"bare organization refinement retains the other filters");
            Check(AssistantPlanner.Plan("how about YMC?",ExpandedAssistantTests.Context()).Message!=null,"follow-up without prior context clarifies");
            Check(AssistantPlanner.Plan("filter to this wk",ExpandedAssistantTests.Context(),new[] {ticket,stock}).Message!=null,"multiple prior results are not guessed");
        }
        private static async Task Conversation()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();var service=Service(reader,model);
            foreach(string q in new[] {"Pls count tix in co. YMC this wk","Ilan ang mga tix sa company YMC ngayong buwan","Count tickets in dept. IT","Count tix at br. MO","Count tickets in co YMC in dept IT at br MO","Count YMC tix","Count YMC IT support tickets"})
            {
                var result=await service.AskAsync(q,context,CancellationToken.None);
                Check(result.IsLive && result.Request.Organizations.Count>0 && string.IsNullOrEmpty(result.Request.Search) && model.Calls==0,"mapped organization roles resolve actual relationships locally: "+q+" / "+result.Text);
                var query=reader.Queries.Last();Check(query.CommandText.Contains("d.[CompanyId]=") || query.CommandText.Contains("d.[DepartmentId]=") || query.CommandText.Contains("d.[BranchId]="),"related record IDs, rather than text searches, constrain totals: "+q);
            }
            reader.Records=0;var zero=await service.AskAsync("count tix in co YMC",context,CancellationToken.None);
            Check(zero.Outcome==AssistantOutcome.NoMatches && zero.TotalRecords==0 && zero.Request.Organizations.Single().Id==17,"mapped questions retain true zero and exact company identity");reader.Records=2;
            await service.AskAsync("count tix in co YMC",context,CancellationToken.None);
            var other=await service.AskAsync("how about YPI?",context,CancellationToken.None);
            Check(other.IsLive && other.Request.Organizations.Single().Id==18 && other.Request.Company=="Yakult Philippines","organization replacement keeps the resolved role");
            foreach(string ambiguous in new[] {"done","fixed","finished","completed"})
            {
                int before=reader.Queries.Count;var result=await service.AskAsync("how many my "+ambiguous+" tix in co YMC this month?",context,CancellationToken.None);
                Check(result.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before && result.ClarificationChoices.Select(c=>c.Label).SequenceEqual(new[] {"Solved","Closed","Resolved (Temporary)"}),"ambiguous completion wording asks a focused status choice: "+ambiguous);
                var selected=await service.ResolveClarificationAsync(result.ClarificationChoices.First(),context,CancellationToken.None);
                Check(selected.IsLive && selected.Request.Status=="Solved" && selected.Request.Personal && selected.Request.FromLocal.HasValue && selected.Request.Organizations.Single().Id==17,"status choice retains mapped ownership, date and company: "+ambiguous);
            }
            var priority=await service.AskAsync("how many my urgent tix in co YMC this month?",context,CancellationToken.None);
            Check(priority.Outcome==AssistantOutcome.Clarification && priority.ClarificationChoices.Select(c=>c.Label).SequenceEqual(new[] {"High priority","Critical priority"}),"urgent does not guess High versus Critical");
            var critical=await service.ResolveClarificationAsync(priority.ClarificationChoices.Last(),context,CancellationToken.None);
            Check(critical.IsLive && critical.Request.Priority=="Critical" && critical.Request.Personal && critical.Request.FromLocal.HasValue && critical.Request.Organizations.Single().Id==17,"priority choice retains related filters");
            foreach(string q in new[] {"count mysteriously ready tix","count tix except closed","count requests not yet approved","show requests awaiting approval","show urgent requests"})
            {
                int before=reader.Queries.Count;var result=await service.AskAsync(q,context,CancellationToken.None);
                Check(result.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before,"unknown or unsupported meaning is not dropped: "+q);
            }
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new string[0];int calls=reader.Queries.Count;
            Check(!(await service.AskAsync("pls count tix in co YMC",denied,CancellationToken.None)).IsLive && reader.Queries.Count==calls,"mapped words preserve source permissions");
            using(var ct=new CancellationTokenSource())
            {
                ct.Cancel();bool stopped=false;try {await service.AskAsync("count tix",context,ct.Token);} catch(OperationCanceledException) {stopped=true;}
                Check(stopped && reader.Queries.Count==calls,"mapped lookup honors cancellation before querying");
            }
        }
        public static Dictionary<string,string> SqlCases()
        {
            var context=ExpandedAssistantTests.Context();var result=new Dictionary<string,string>();
            foreach(var pair in Pairs)
            {
                var plan=AssistantPlanner.Plan(pair[0],context,todayOverride:Today);
                result.Add("everyday wording "+pair[0],AssistantQueryEngine.BuildQuery(plan.Requests.Single(),context).CommandText);
            }
            return result;
        }
        public static string InspectMappings()
        {
            var messages=new List<string>();
            foreach(var pair in Pairs)
            {
                var actual=AssistantPlanner.Plan(pair[0],ExpandedAssistantTests.Context(),todayOverride:Today);
                var expected=AssistantPlanner.Plan(pair[1],ExpandedAssistantTests.Context(),todayOverride:Today);
                if(actual.Message!=null || actual.NeedsInterpretation || actual.Requests.Count!=1)
                    messages.Add(pair[0]+": "+actual.Message+" / "+actual.UnresolvedMeaning);
                else
                {
                    var a=AssistantQueryEngine.BuildQuery(actual.Requests[0],ExpandedAssistantTests.Context());var b=AssistantQueryEngine.BuildQuery(expected.Requests.Single(),ExpandedAssistantTests.Context());
                    if(a.CommandText!=b.CommandText || !a.Parameters.OrderBy(p=>p.Key).SequenceEqual(b.Parameters.OrderBy(p=>p.Key))) messages.Add(pair[0]+": SQL differs from canonical wording");
                }
            }
            return messages.Count==0?"All reviewed mapping pairs produce equivalent queries.":string.Join("\n",messages);
        }
    }
}
