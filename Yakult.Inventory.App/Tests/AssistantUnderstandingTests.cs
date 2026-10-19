using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>Meaning, query equivalence and English wording checks, using synthetic data only.</summary>
    public static class AssistantUnderstandingTests
    {
        private static int _checks;
        private static void Check(bool value,string message)
        {if(!value) throw new InvalidOperationException("Yakumi understanding: "+message);_checks++;}
        private static AssistantRequest Request(string q,IReadOnlyList<AssistantRequest> previous=null)
        {
            var p=AssistantPlanner.Plan(q,ExpandedAssistantTests.Context(),previous,new DateTime(2026,10,16));
            Check(p.Message==null && !p.NeedsInterpretation && p.Requests.Count==1,"clear local question: "+q+" / "+p.Message+" / "+p.UnresolvedMeaning);
            return p.Requests.Single();
        }
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            internal bool Fail;internal decimal Units=254;internal long Records=2;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {ct.ThrowIfCancellationRequested();Queries.Add(query);if(Fail) throw new InvalidOperationException("Synthetic disconnected database");return Task.FromResult(ExpandedAssistantTests.Fixture(query,Records,Units,query.Request.Operation==AssistantOperation.Count?0:(int)Math.Min(Records,1)));}
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;internal string Json;internal Action During;internal AssistantInterpretationContext Previous;internal string Question;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext context,CancellationToken ct)
            {Calls++;Previous=context;Question=q;During?.Invoke();ct.ThrowIfCancellationRequested();if(Json==null) throw new InvalidOperationException("Synthetic offline AI");return Task.FromResult(Json);}
            public Task<string> GeneralAsync(List<ChatMessage> h,string q,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline AI");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline AI");}
        }
        private static JObject Entry(string capability,string operation,string evidence,JObject filters=null,bool inherit=false)
        {return new JObject {["capability"]=capability,["operation"]=operation,["sourceEvidence"]=evidence,["inherit"]=inherit,["filters"]=filters ?? new JObject()};}
        private static string Json(params JObject[] entries)
        {return new JObject {["clarification"]="",["compare"]=false,["requests"]=new JArray(entries)}.ToString();}
        private static AssistantPlan Validate(string q,string json,AssistantInterpretationContext previous=null,AssistantContext context=null)
        {return AssistantInterpretation.Validate(q,json,context ?? ExpandedAssistantTests.Context(),AssistantCatalog.All,new DateTime(2026,10,16),previous ?? new AssistantInterpretationContext(null));}
        private static void Reject(string q,string json,AssistantInterpretationContext previous=null)
        {bool rejected=false;try {Validate(q,json,previous);}catch(ArgumentException){rejected=true;}Check(rejected,"reject unsupported or ungrounded model interpretation: "+q);}
        public static string RunAll()
        {
            _checks=0;LocalMeaning();ModelBoundary();Wording();Conversation().GetAwaiter().GetResult();
            return _checks+" assistant understanding and English checks passed.";
        }
        private static void LocalMeaning()
        {
            var baseline=Request("how many lx310 are in stock?");
            Check(Request("how many Redmi cellphones are in stock?").Search=="redmi","clear brand and product noun need no catalog or AI");
            Check(Request("how many nonexistentbrand items in stock?").Search=="nonexistentbrand","unknown brand remains a real product filter");
            var sql=AssistantQueryEngine.BuildQuery(baseline,ExpandedAssistantTests.Context());
            foreach(string q in new[] {"how many lx310 are still in stock","HOW MANY LX310 ARE IN STOCK?!","how mny lx310 are currently available?","how many lx310 do we have left?","how many lx310 do we have?","how many lx310 in stock?"})
            {
                var r=Request(q);var query=AssistantQueryEngine.BuildQuery(r,ExpandedAssistantTests.Context());
                Check(r.Search=="lx310" && r.InStockOnly,"only the model remains: "+q);
                Check(query.CommandText==sql.CommandText && query.Parameters.OrderBy(p=>p.Key).SequenceEqual(sql.Parameters.OrderBy(p=>p.Key)),"identical SQL and parameters: "+q);
                var result=AssistantQueryEngine.Render(query,ExpandedAssistantTests.Fixture(query,2,254,0));
                Check(result.Totals["Units"]==254 && result.TotalRecords==2,"254 is synthetic unit metadata, not a record count");
            }
            var next=Request("what about LX350?",new[] {baseline});Check(next.Search=="lx350" && next.InStockOnly,"model replacement");
            next=Request("only this month",new[] {next});Check(next.Search=="lx350" && next.FromLocal==new DateTime(2026,10,1),"date refinement retains model");
            var allDates=Request("all dates",new[] {next});Check(!allDates.FromLocal.HasValue && !allDates.ToLocalExclusive.HasValue && allDates.Search=="lx350","explicit all-dates refinement clears only the period");
            var mine=Request("how many solved tickets assigned to me this month?");
            next=Request("only last month",new[] {mine});Check(next.Status=="Solved" && next.PersonalRelation=="Assigned" && next.FromLocal==new DateTime(2026,9,1),"retain status and ownership");
            next=Request("what about requests?",new[] {mine});Check(next.Capability=="requests" && next.Status==null && !next.Personal && !next.FromLocal.HasValue,"source change clears incompatible scope");
            Check(AssistantPlanner.Plan("what about LX350?",ExpandedAssistantTests.Context(),new[] {baseline,mine}).Message!=null,"multiple results require focused clarification");
            foreach(string q in new[] {"not solved tickets","tickets except closed","stock without toner","no archived items except laptops"})
                Check(AssistantPlanner.Plan(q,ExpandedAssistantTests.Context()).Message!=null,"meaningful exclusion is clarified: "+q);
            Check(Request("show items not archived").IncludeArchived==false,"supported archive exclusion stays usable");
            Check(Request("how many used items in stock?").Condition=="Used","used is an item condition, not an item status");
            Check(Request("show items condition \"Slightly Used\"").Condition=="Slightly Used","explicit condition keeps quoted spelling");
            Check(AssistantPlanner.Plan("tickets this month last month",ExpandedAssistantTests.Context()).Message!=null,"multiple periods are not silently collapsed");
            foreach(string name in new[] {"Still Open","This Month","Not Available","The New 1","Solved Tickets","Assigned to John","SET-12"})
            {
                var r=Request("how many items named \""+name+"\" are still in stock?");
                Check(r.Status==null && r.Condition==null && !r.FromLocal.HasValue,"quoted name is not a filter: "+name);
                var query=AssistantQueryEngine.BuildQuery(r,ExpandedAssistantTests.Context());
                foreach(string word in AssistantQuestionLanguage.LiteralTerms(name)) Check(query.Parameters.Values.Contains("%"+word+"%"),"quoted token preserved in SQL: "+word);
            }
            Check(Request("show tickets assigned to 'Still Open' this month").Employee=="Still Open","quoted employee keeps spelling and status words");
            Check(AssistantPlanner.Plan("show tickets assigned to John who solved them",ExpandedAssistantTests.Context()).NeedsInterpretation,"unexplained wording inside an extracted employee filter is not accepted as a name");
            foreach(string q in new[] {"how many solved tickets are there?","HOW MNY SLOVED TICKETS?","how many sloved tickets currently?"})
                Check(Request(q).Status=="Solved","supported typo and informal ticket grammar");
            foreach(string q in new[] {"how many pending requests?","HOW MANY MY PENDING REQUESTS?","show my requests this month"})
                Check(Request(q).Capability=="requests","request paraphrase");
            Check(AssistantPlanner.Plan("how many LX310 magically ready stock?",ExpandedAssistantTests.Context()).NeedsInterpretation,"valid SQL shape alone is not understanding");
            Check(AssistantPlanner.Plan("show platinum tickets",ExpandedAssistantTests.Context()).NeedsInterpretation,"unknown modifier needs interpretation");
        }
        private static void ModelBoundary()
        {
            var p=Validate("how many helpdesk tickets?",Json(Entry("tickets","Count","helpdesk",new JObject {["search"]=""})));
            Check(p.Requests.Count==1 && string.IsNullOrWhiteSpace(p.Requests[0].Search),"AI can identify source wording rather than searching it");
            p=Validate("number of support incidents",Json(Entry("tickets","Count","support incidents")));
            Check(p.Requests.Single().Capability=="tickets","grounded source synonym");
            p=Validate("show requests awaiting fulfillment",Json(Entry("requests","List","requests",new JObject {["status"]="Unfulfilled",["search"]=""})));
            Check(p.Requests.Single().OutstandingOnly && p.Requests[0].Status==null,"pending request retains quantity semantics");
            p=Validate("show requests awaiting fulfillment",Json(Entry("requests","List","requests",new JObject {["status"]="Unfulfilled"})));
            Check(p.Requests[0].OutstandingOnly && string.IsNullOrEmpty(p.Requests[0].Search),"consumed status wording is not left as a product search");
            Reject("show requests awaiting approval",Json(Entry("requests","List","requests",new JObject {["status"]="Pending",["search"]=""})));
            Reject("how many records?",Json(Entry("tickets","Count","records")));
            Reject("how many tickets?",Json(Entry("tickets","Count","tickets",new JObject {["status"]="Solved"})));
            Reject("show platinum tickets",Json(Entry("tickets","List","tickets",new JObject {["status"]="platinum"})));
            Reject("how many LX310 items in stock?",Json(Entry("items","Count","items",new JObject {["search"]=""})));
            Reject("show tickets",Json(Entry("tickets","List","tickets",new JObject {["sql"]="SELECT 1"})));
            Reject("how many tickets?",Json(Entry("ticket-email","Count","tickets")));
            Reject("show tickets this month",Json(Entry("tickets","List","tickets",new JObject {["period"]="last month"})));
            Reject("show tickets",Json(Entry("tickets","List","tickets",new JObject {["employee"]="Nobody"})));
            Reject("show my tickets named John",Json(Entry("tickets","List","tickets",new JObject {["employee"]="John"})));
            Reject("show IT tickets",Json(Entry("tickets","List","tickets",new JObject {["department"]="IT"})));
            Reject("show tickets named \"Open\"",Json(Entry("tickets","List","tickets",new JObject {["search"]="\"Open\"",["status"]="Open"})));
            var assigned=Validate("show tickets assigned John",Json(Entry("tickets","List","tickets",new JObject {["employee"]="John",["search"]=""})));
            Check(assigned.Requests.Single().Employee=="John" && string.IsNullOrEmpty(assigned.Requests[0].Search),"AI handles explicit employee relationship in unfamiliar word order");
            Reject("not solved tickets",Json(Entry("tickets","List","tickets")));
            Reject("show tickets",Json(Entry("tickets","Delete","tickets")));
            Reject("show tickets and requests and items",Json(Entry("tickets","List","tickets"),Entry("requests","List","requests"),Entry("items","List","items")));
            var prev=new AssistantInterpretationContext(new[] {Request("how many LX310 items in stock?")});
            p=Validate("what about LX350?",Json(Entry("items","Count","",new JObject {["search"]="lx350"},true)),prev);
            Check(p.Requests[0].Search=="lx350" && p.Requests[0].InStockOnly,"AI follow-up replaces model, retains stock");
            Reject("what about requests?",Json(Entry("requests","List","requests",null,true)),prev);
            var contextual=new AssistantInterpretationContext(new[] {Request("how many solved tickets assigned to me this month?")});
            string prompt=AssistantInterpretation.Prompt(AssistantCatalog.All.Take(6).ToList(),contextual);
            Check(prompt.Contains("Previous scope") && !prompt.Contains("TotalRecords") && !prompt.Contains("ROB MECA") && !prompt.Contains("SessionStamp"),"only bounded typed request scope reaches model");
            Check(Encoding.UTF8.GetByteCount(prompt)+Encoding.UTF8.GetByteCount("only last month")<=OllamaChatService.MaxPromptBytes,"existing model prompt budget");
            var two=Validate("number of support incidents and how many requests",Json(Entry("tickets","Count","support incidents"),Entry("requests","Count","requests")));
            Check(two.Requests.Count==2 && string.IsNullOrEmpty(two.Requests[0].Search),"two bounded typed lookups use separate source evidence");
            var guides=AssistantKnowledge.Search("How do I borrow equipment?",ExpandedAssistantTests.Context());
            var numbered=new JObject {["answer"]="1. Open Borrow Items.\n2. Inspect existing loans.",["citations"]=new JArray(guides.Select(g=>g.Id))};
            Check(OllamaAssistantModel.ValidateStructuredExplanation(numbered.ToString(),guides)!=null,"numbered procedure markers are formatting, not invented quantities");
            numbered["answer"]="1. There are 9876543 available units.";
            Check(OllamaAssistantModel.ValidateStructuredExplanation(numbered.ToString(),guides)==null,"numbered procedures still reject invented quantities");
            var copied=contextual.Previous[0].Copy();copied.Search="changed";Check(contextual.Previous[0].Search!=copied.Search,"interpretation context snapshots are isolated");
        }
        private static void Wording()
        {
            foreach(var capability in AssistantCatalog.All)
            foreach(int count in new[] {0,1,2})
            {
                var query=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=capability.Id,Operation=AssistantOperation.Count},ExpandedAssistantTests.Context());
                var result=AssistantQueryEngine.Render(query,ExpandedAssistantTests.Fixture(query,count,1.5m,0));
                var view=AssistantPresentationFormatter.Format(result);
                Check(AssistantEnglish.Noun(capability.Id,1)!="record","explicit noun registered: "+capability.Id);
                Check(view.Headline.EndsWith(".") && view.Headline.Contains(AssistantEnglish.Noun(capability.Id,count)),"complete English answer for "+capability.Id+" / "+count);
                Check(view.CopyText.Contains(view.Headline),"Copy includes direct factual answer");
            }
            foreach(decimal units in new[] {0m,1m,2m,1.25m})
            {
                var query=AssistantQueryEngine.BuildQuery(Request("how many LX310 items are still in stock?"),ExpandedAssistantTests.Context());
                var result=AssistantQueryEngine.Render(query,ExpandedAssistantTests.Fixture(query,units==0?0:1,units,0));
                var view=AssistantPresentationFormatter.Format(result);
                Check(view.Headline.Contains("“LX310”") && view.Headline.Contains(units==1?"There is 1 “LX310” unit":"There are "+units.ToString(System.Globalization.CultureInfo.InvariantCulture)+" “LX310” units"),"quantity agreement, item identity and fractional stock: "+units);
            }
            var detail=AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Detail,RecordId=69},ExpandedAssistantTests.Context());
            var fact=AssistantQueryEngine.Render(detail,ExpandedAssistantTests.Fixture(detail,1,0,1));
            var v=AssistantPresentationFormatter.Format(fact);Check(v.Headline=="Here are the details for IT ticket #69." && v.PageCaption.Contains("1 loaded record") && !v.CopyText.Contains("1 loaded records"),"singular detail and paging grammar");
            var solved=Request("how many solved tickets this month?");
            var result2=AssistantQueryEngine.Render(AssistantQueryEngine.BuildQuery(solved,ExpandedAssistantTests.Context()),ExpandedAssistantTests.Fixture(AssistantQueryEngine.BuildQuery(solved,ExpandedAssistantTests.Context()),1,0,0));
            Check(AssistantPresentationFormatter.Format(result2).Body.Contains("status changes") && AssistantPresentationFormatter.Format(result2).Body.Contains("October 31, 2026"),"date context states the measured event");
        }
        private static async Task Conversation()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();
            var service=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true);
            var first=await service.AskAsync("how many lx310 are still in stock",context,CancellationToken.None);
            var second=await service.AskAsync("how many lx310 are in stock",context,CancellationToken.None);
            Check(first.Totals["Units"]==254 && second.Totals["Units"]==254 && reader.Queries.Count==2 && model.Calls==0,"screenshot questions both query fresh and work with offline AI");
            await service.AskAsync("what about LX350?",context,CancellationToken.None);
            Check(reader.Queries.Last().Request.Search=="lx350","model replacement executes fresh query");
            reader.Records=0;reader.Units=0;int before=reader.Queries.Count;
            var zero=await service.AskAsync("how many UNKNOWN999 items in stock?",context,CancellationToken.None);
            Check(zero.Outcome==AssistantOutcome.NoMatches && zero.Totals["Units"]==0 && reader.Queries.Count==before+1 && zero.Request.Search=="unknown999","true zero keeps product, no broadening or retry");
            before=reader.Queries.Count;var uncertain=await service.AskAsync("how many LX310 mysteriously ready stock?",context,CancellationToken.None);
            Check(uncertain.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before && model.Calls==1,"offline AI clarifies unexplained wording before SQL");
            model.Json=Json(Entry("tickets","Count","support incidents",new JObject {["search"]=""}));reader.Records=2;
            var interpreted=await service.AskAsync("how many support incidents?",context,CancellationToken.None);
            Check(interpreted.IsLive && model.Question=="how many support incidents?" && reader.Queries.Last().Request.Capability=="tickets","validated AI typed plan executes original question");
            model.Json=Json(Entry("tickets","Count","tickets",new JObject {["employee"]="invented"}));before=reader.Queries.Count;
            uncertain=await service.AskAsync("how many strange tickets?",context,CancellationToken.None);
            Check(uncertain.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before,"invented filter never queries");
            bool current=true;service=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>current);
            model.Json=Json(Entry("tickets","Count","support incidents",new JObject {["search"]=""}));model.During=()=>current=false;before=reader.Queries.Count;
            var changed=await service.AskAsync("how many support incidents?",context,CancellationToken.None);
            Check(changed.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"stale session after AI prevents querying");model.During=null;
            using(var cts=new CancellationTokenSource())
            {
                service=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true);model.During=()=>cts.Cancel();bool cancelled=false;
                try {await service.AskAsync("how many support incidents?",context,cts.Token);}catch(OperationCanceledException){cancelled=true;}
                Check(cancelled && reader.Queries.Count==before,"cancellation after AI prevents querying");model.During=null;
            }
            service=new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true);reader.Fail=true;
            var unavailable=await service.AskAsync("how many LX310 in stock?",context,CancellationToken.None);
            Check(unavailable.Outcome==AssistantOutcome.Unavailable && !unavailable.Text.Contains("0 units"),"disconnected database never reports zero");reader.Fail=false;
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new[] {"RequesterPortal"};before=reader.Queries.Count;
            var noAccess=await service.AskAsync("how many tickets?",denied,CancellationToken.None);
            Check(noAccess.Outcome==AssistantOutcome.Denied && reader.Queries.Count==before,"denied local source cannot query");
            await service.AskAsync("how many tickets and how many requests?",context,CancellationToken.None);before=reader.Queries.Count;
            var ambiguous=await service.AskAsync("only this month",context,CancellationToken.None);
            Check(ambiguous.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before,"ambiguous multi-result context does not choose a result");
        }
    }
}
