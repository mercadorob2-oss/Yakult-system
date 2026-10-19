using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>Synthetic checks for grounded examples, clarification decisions and native button wiring.</summary>
    public static class AssistantClarificationTests
    {
        private static int _checks;
        private static void Check(bool value,string message)
        {if(!value) throw new InvalidOperationException("Yakumi clarification: "+message);_checks++;}
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            internal long Total=1;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {ct.ThrowIfCancellationRequested();Queries.Add(query);return Task.FromResult(ExpandedAssistantTests.Fixture(query,Total,Total==0?0:254,query.Request.Operation==AssistantOperation.Count?0:(int)Total));}
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> candidates,AssistantInterpretationContext context,CancellationToken ct)
            {Calls++;throw new InvalidOperationException("Synthetic offline AI");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> articles,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline AI");}
            public Task<string> GeneralAsync(List<ChatMessage> history,string q,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline AI");}
        }
        private static YakumiAssistantService Service(Reader reader,Func<AssistantContext,bool> current=null)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),new Model(),current ?? (_=>true));}
        private static AssistantClarificationChoice Choice(AssistantResult result,string label)
        {return result.ClarificationChoices.Single(c=>c.Label==label);}
        public static string RunAll()
        {
            _checks=0;Examples();Decisions().GetAwaiter().GetResult();Boundaries().GetAwaiter().GetResult();Buttons();
            return _checks+" AI example and clickable clarification checks passed.";
        }
        private static void Examples()
        {
            var context=ExpandedAssistantTests.Context();
            foreach(var example in AssistantInterpretationExamples.All)
            {
                var conversation=new AssistantInterpretationContext(example.Previous==null?null:new[] {example.Previous});
                var plan=AssistantInterpretation.Validate(example.Question,example.Json,context,AssistantCatalog.All,new DateTime(2026,10,16),conversation);
                Check(plan.Message==null && plan.Requests.Count==1 && plan.Requests[0].Capability==example.Capability,"reviewed example validates: "+example.Question);
                AssistantQueryEngine.BuildQuery(plan.Requests[0],context);
                var candidates=AssistantPlanner.InterpretationCandidates(example.Question,context);
                string prompt=AssistantInterpretation.Prompt(candidates,conversation,example.Question);
                Check(Encoding.UTF8.GetByteCount(prompt)+Encoding.UTF8.GetByteCount(example.Question)<=OllamaChatService.MaxPromptBytes,"example fits existing input budget");
                Check(prompt.Contains(example.Question),"question-relevant example included");
            }
            var allowed=AssistantCatalog.All.Where(c=>c.Id=="items").ToList();
            var selected=AssistantInterpretationExamples.Select("support incidents",allowed,new AssistantInterpretationContext(null));
            Check(selected.All(e=>e.Capability=="items"),"examples only name candidate sources");
            foreach(string q in new[] {"how many items named \"Still Open\" in stock?","show tickets named 'High Priority'"})
            {
                var properties=(JObject)AssistantInterpretation.Schema(AssistantCatalog.All,q).SelectToken("properties.requests.items.properties.filters.properties");
                Check(properties["status"]==null && properties["priority"]==null,"quoted names cannot introduce status or priority in model schema");
            }
            var schema=AssistantInterpretation.Schema(AssistantCatalog.All,"show requests awaiting fulfillment");
            Check(schema.SelectToken("properties.requests.items.properties.filters.properties.status.enum").Values<string>().SequenceEqual(new[] {"Unfulfilled"}),"fulfillment schema permits only grounded Unfulfilled status");
            schema=AssistantInterpretation.Schema(AssistantCatalog.All,"how many sloved tickets?");
            Check(schema.SelectToken("properties.requests.items.properties.filters.properties.status.enum").Values<string>().SequenceEqual(new[] {"Solved"}),"normalized typo still grounds status enum");
            var named=AssistantPlanner.Plan("how many items named \"Still Open\" are in stock?",context).Requests.Single();
            var namedQuery=AssistantQueryEngine.BuildQuery(named,context);
            Check(named.Search=="\"Still Open\"" && !namedQuery.Parameters.Values.Contains("%named%"),"name introducer is grammar and never an extra SQL product filter");
            foreach(string q in new[] {new string('x',1200),string.Concat(Enumerable.Repeat("質問",180))})
            {
                string prompt=AssistantInterpretation.Prompt(AssistantCatalog.All.Take(6).ToList(),new AssistantInterpretationContext(new[] {new AssistantRequest {Capability="items",Search=new string('x',160)}}),q);
                Check(Encoding.UTF8.GetByteCount(prompt)+Encoding.UTF8.GetByteCount(q)<=OllamaChatService.MaxPromptBytes,"adaptive examples respect UTF-8 budget");
            }
        }
        private static async Task Decisions()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            var result=await service.AskAsync("how many records this month?",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==0,"unclear source does not query");
            Check(result.ClarificationChoices.Select(c=>c.Label).SequenceEqual(new[] {"Stock","Tickets","Requests"}),"three concise source choices");
            Check(result.Text.Contains("Choices:") && AssistantPresentationFormatter.Format(result).CopyText.Contains("Stock · Tickets · Requests"),"legacy and Copy include readable choices without opaque IDs");
            var choice=Choice(result,"Tickets");var answer=await service.ResolveClarificationAsync(choice,context,CancellationToken.None);
            Check(answer.IsLive && reader.Queries.Count==1 && answer.Request.Capability=="tickets" && answer.Request.Operation==AssistantOperation.Count,"source choice resolves original count");
            Check(answer.Request.FromLocal.HasValue && string.IsNullOrWhiteSpace(answer.Request.Search),"source choice retains date and no accidental name filter");
            await service.ResolveClarificationAsync(choice,context,CancellationToken.None);Check(reader.Queries.Count==1,"choice can execute only once");

            result=await service.AskAsync("show records this month",context,CancellationToken.None);
            answer=await service.ResolveClarificationAsync(Choice(result,"Stock"),context,CancellationToken.None);
            Check(answer.Request.InStockOnly && answer.Request.Operation==AssistantOperation.List && answer.Request.FromLocal.HasValue,"Stock choice explicitly selects available stock for a list too");

            result=await service.AskAsync("how many my resolved tickets this month?",context,CancellationToken.None);
            Check(result.ClarificationChoices.Select(c=>c.Label).SequenceEqual(new[] {"Solved","Closed","Resolved (Temporary)"}),"ambiguous resolved status offers exact statuses");
            answer=await service.ResolveClarificationAsync(Choice(result,"Solved"),context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.Status=="Solved" && answer.Request.Personal && answer.Request.FromLocal.HasValue,"status choice preserves ownership and dates");
            await service.AskAsync("how many my resolved tickets this month?",context,CancellationToken.None);
            answer=await service.AskAsync("Solved!",context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.Status=="Solved" && answer.Request.Personal && answer.Request.FromLocal.HasValue,"typing a displayed choice label resolves the same original scope");

            result=await service.AskAsync("show platinum tickets this month",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==1 && result.ClarificationChoices[0].Label=="Use as search text","unknown word requires explicit search confirmation");
            reader.Total=0;int before=reader.Queries.Count;
            answer=await service.ResolveClarificationAsync(result.ClarificationChoices[0],context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.NoMatches && answer.Request.Search=="platinum" && answer.Request.FromLocal.HasValue && reader.Queries.Count==before+1,"confirmed search keeps filters and true zero, without broadening");reader.Total=1;

            await service.AskAsync("how many my solved tickets and how many my requests",context,CancellationToken.None);
            result=await service.AskAsync("only this month",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==2,"ambiguous follow-up offers previous results");
            var requests=result.ClarificationChoices.Single(c=>c.Label=="Requests");
            answer=await service.ResolveClarificationAsync(requests,context,CancellationToken.None);
            Check(answer.Request.Capability=="requests" && answer.Request.Personal && answer.Request.FromLocal.HasValue && answer.Request.Status==null,"selected result receives refinement without ticket filters");

            result=await service.AskAsync("tickets except closed",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==0,"unsupported exclusion has no broadening button");
            result=await service.AskAsync("delete resolved tickets",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==0,"unsupported write request has no lookup status choices");
            result=await service.AskAsync("show tickets assigned to John who solved them",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==0,"ambiguous extracted name is not confirmed as an unrelated text filter");
        }
        private static async Task Boundaries()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            var result=await service.AskAsync("how many records?",context,CancellationToken.None);var stale=result.ClarificationChoices[0];
            await service.AskAsync("how many tickets?",context,CancellationToken.None);int before=reader.Queries.Count;
            var answer=await service.ResolveClarificationAsync(stale,context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"another question invalidates older choices");
            result=await service.AskAsync("how many records?",context,CancellationToken.None);stale=result.ClarificationChoices[0];service.Reset();
            answer=await service.ResolveClarificationAsync(stale,context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"new chat invalidates choices");
            result=await service.AskAsync("how many records?",context,CancellationToken.None);var active=result.ClarificationChoices[0];
            await service.ResolveClarificationAsync(stale,context,CancellationToken.None);
            Check(service.IsClarificationCurrent(active,context),"old prompt cannot discard newer choices");
            var forged=new AssistantClarificationChoice(active.PromptId,"invented",active.Label);
            answer=await service.ResolveClarificationAsync(forged,context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"invented choice ID cannot run a query");
            using(var cts=new CancellationTokenSource())
            {
                cts.Cancel();bool cancelled=false;try {await service.ResolveClarificationAsync(active,context,cts.Token);}catch(OperationCanceledException){cancelled=true;}
                Check(cancelled && reader.Queries.Count==before,"cancelled selection does not query");
                Check(service.IsClarificationCurrent(active,context),"cancellation before dispatch does not consume the choice");
            }
            var restricted=ExpandedAssistantTests.Context();restricted.IsDeveloper=false;restricted.IsSuperAdmin=false;restricted.Portals=new[] {"RequesterPortal"};restricted.SessionStamp="requester";
            result=await service.AskAsync("how many records?",restricted,CancellationToken.None);
            Check(result.ClarificationChoices.Count==1 && result.ClarificationChoices[0].Label=="Requests","source choices respect permitted portals");
            answer=await service.ResolveClarificationAsync(active,restricted,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"session change rejects old choice");

            service=Service(reader);result=await service.AskAsync("how many resolved tickets?",context,CancellationToken.None);
            answer=await service.ResolveClarificationAsync(Choice(result,"Solved"),restricted,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"status choices cannot cross accounts");
            service=Service(reader);result=await service.AskAsync("how many resolved tickets?",context,CancellationToken.None);
            restricted.SessionStamp=context.SessionStamp;
            answer=await service.ResolveClarificationAsync(Choice(result,"Solved"),restricted,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Denied && reader.Queries.Count==before,"selection rechecks effective permissions even with a reused stamp");
            bool current=true;service=Service(reader,_=>current);result=await service.AskAsync("how many records?",context,CancellationToken.None);current=false;
            answer=await service.ResolveClarificationAsync(result.ClarificationChoices[0],context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"stale-session guard precedes choice execution");
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {
            if(root is T) yield return (T)root;
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Descendants<T>(child)) yield return item;
        }
        private static void Buttons()
        {
            Check(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"native button tests run on STA");
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            using(var view=new YakumiChatView(()=>context,service))
            {
                view.SubmitAsync("how many records this month?").GetAwaiter().GetResult();
                var buttons=Descendants<Button>(view).Where(b=>b.Tag is AssistantClarificationChoice).ToList();
                Check(buttons.Select(b=>b.Content.ToString()).SequenceEqual(new[] {"Stock","Tickets","Requests"}),"clarification buttons reach the actual chat view");
                buttons.Single(b=>Equals(b.Content,"Tickets")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(reader.Queries.Count==1 && reader.Queries[0].Request.Capability=="tickets" && reader.Queries[0].Request.FromLocal.HasValue,"click dispatches original filtered lookup through service");
                Check(buttons.All(b=>!b.IsEnabled),"answered clarification choices disable together");
                view.SubmitAsync("how many records?").GetAwaiter().GetResult();
                var old=Descendants<Button>(view).Where(b=>b.Tag is AssistantClarificationChoice && b.IsEnabled).ToList();
                view.NewConversation();old[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(reader.Queries.Count==1,"detached old button cannot query after New chat");
            }
        }
    }
}
