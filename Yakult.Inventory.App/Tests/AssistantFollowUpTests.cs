using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>References resolve saved request scope only; progress is observed before synthetic reads.</summary>
    public static class AssistantFollowUpTests
    {
        private static int _checks;
        private static void Check(bool value,string message)
        {if(!value) throw new InvalidOperationException("Yakumi follow-ups: "+message);_checks++;}
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            internal Action BeforeRead;
            internal bool Delay;
            internal TaskCompletionSource<DataTable> Pending;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();BeforeRead?.Invoke();Queries.Add(query);
                if(Delay) {Pending=new TaskCompletionSource<DataTable>();return Pending.Task;}
                return Task.FromResult(Data(query));
            }
            private static DataTable Data(AssistantSqlQuery query)
            {return ExpandedAssistantTests.Fixture(query,1,254,query.Request.Operation==AssistantOperation.Count?0:1);}
            internal void Complete() {Pending.SetResult(Data(Queries.Last()));}
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext previous,CancellationToken ct)
            {Calls++;throw new InvalidOperationException("Synthetic offline model");}
            public Task<string> GeneralAsync(List<ChatMessage> h,string q,CancellationToken ct) {return Task.FromResult("Hello.");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {throw new InvalidOperationException("Synthetic offline model");}
        }
        private sealed class Vocabulary : IAssistantStockVocabulary
        {
            public Task<AssistantStockHint> ResolveAsync(string question,AssistantContext context,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var products=new[] {new AssistantProductTerm {Name="Epson printer",Model="LX310",Category="Printer"},new AssistantProductTerm {Name="Epson printer",Model="LX350",Category="Printer"}};
                var hint=AssistantStockVocabulary.Match(question,products) ?? new AssistantStockHint {Route=false};
                hint.Products=products;return Task.FromResult(hint);
            }
        }
        private static YakumiAssistantService Service(Reader reader,Model model=null,Func<AssistantContext,bool> current=null)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),model ?? new Model(),current ?? (_=>true),new Vocabulary());}
        private static readonly DateTime Today=new DateTime(2026,10,16);
        private static AssistantRequest Stock()
        {return new AssistantRequest {Capability="items",Operation=AssistantOperation.Count,Search="epson lx310",ModelMatch=new AssistantModelMatch("LX310","LX310"),Category="Printer",ExactCategory=true,Condition="Used",InStockOnly=true,FromLocal=new DateTime(2026,10,1),ToLocalExclusive=new DateTime(2026,11,1)};}
        private static AssistantRequest Tickets()
        {return new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Status="Solved",Personal=true,PersonalRelation="Assigned",DateField="StatusChangedAt"};}
        private static AssistantPlan Plan(string question,params AssistantRequest[] previous)
        {return AssistantPlanner.Plan(question,ExpandedAssistantTests.Context(),previous,Today);}
        private static AssistantRequest Request(string question,params AssistantRequest[] previous)
        {var plan=Plan(question,previous);Check(plan.Message==null && !plan.NeedsInterpretation && plan.Requests.Count==1,"one resolved lookup: "+question);return plan.Requests.Single();}
        public static string RunAll()
        {
            _checks=0;References();ModelBoundary();Descriptions();Conversation().GetAwaiter().GetResult();Progress().GetAwaiter().GetResult();Interface();
            return _checks+" richer follow-up and pre-search description checks passed.";
        }
        private static void References()
        {
            var stock=Stock();var tickets=Tickets();
            foreach(string text in new[] {"the second result","second result!","2nd result","result 2","show me the second result","use result #2"})
            {
                var request=Request(text,stock,tickets);
                Check(request.Capability=="tickets" && request.Operation==AssistantOperation.Count && request.Status=="Solved" && request.PersonalRelation=="Assigned","ordinal selects scope, preserving ownership and operation");
                Check(string.IsNullOrEmpty(request.Search),"ordinal grammar never becomes a product filter");
            }
            var second=Request("the second result, only this month",stock,tickets);
            Check(second.Capability=="tickets" && second.FromLocal==new DateTime(2026,10,1) && second.Status=="Solved","ordinal refinement applies to selected result");
            var first=Request("the first result only brand-new",stock,tickets);
            Check(first.Search==stock.Search && first.ModelMatch==stock.ModelMatch && first.Condition=="Brand New" && first.FromLocal==stock.FromLocal,"selected item retains model and period while replacing condition");
            foreach(string text in new[] {"same item","the same product","same model","same one","same item, only brand new","same item but only brandnew","same filters but only brand new","same but only brand new"})
            {
                var request=Request(text,stock);
                Check(request.Search==stock.Search && request.ModelMatch==stock.ModelMatch && request.Category=="Printer" && request.FromLocal==stock.FromLocal,"same-product wording retains complete scope: "+text);
            }
            Check(stock.Condition=="Used" && !tickets.FromLocal.HasValue,"previous requests remain unchanged");
            foreach(string text in new[] {"the third result","result 0","result 999999999999999999999","the second item","show the first row","the second result requests","same item except repaired","same item delete it"})
                Check(Plan(text,stock,tickets).Message!=null,"unavailable, conflicting or unsupported reference is clarified: "+text);
            Check(Plan("same item",new AssistantRequest {Capability="items",Operation=AssistantOperation.List}).Message!=null,"no row identity is invented from a broad item list");
            Check(Plan("same item",tickets).Message!=null,"a ticket is not silently treated as an item");
            Check(Plan("second result").Message!=null && Plan("same item").Message!=null,"missing context requires a lookup first");
            var literal=Request("show items named \"the second result\"");
            Check(literal.Search=="\"the second result\"","quoted reference words are literal names");
            literal=Request("show items named \"brand-new\"");
            Check(literal.Search=="\"brand-new\"","quoted hyphenated names remain literal");
            var source=Request("what about requests?",stock);
            Check(source.Capability=="requests" && source.ModelMatch==null && source.Condition==null && !source.FromLocal.HasValue,"source changes clear incompatible inherited filters");
        }
        private static string Interpretation(string capability)
        {return new JObject {["clarification"]="",["compare"]=false,["requests"]=new JArray(new JObject {["capability"]=capability,["operation"]="Count",["sourceEvidence"]="",["inherit"]=true,["filters"]=new JObject {["period"]="this month"}})}.ToString();}
        private static void ModelBoundary()
        {
            var previous=new AssistantInterpretationContext(new[] {Stock(),Tickets()});
            var plan=AssistantInterpretation.Validate("the second result only this month",Interpretation("tickets"),ExpandedAssistantTests.Context(),AssistantCatalog.All,Today,previous);
            Check(plan.Requests.Single().PersonalRelation=="Assigned" && plan.Requests[0].FromLocal==new DateTime(2026,10,1),"AI validation uses the locally selected scope and original question");
            bool rejected=false;
            try {AssistantInterpretation.Validate("the second result only this month",Interpretation("items"),ExpandedAssistantTests.Context(),AssistantCatalog.All,Today,previous);} catch(ArgumentException) {rejected=true;}
            Check(rejected,"AI cannot reinterpret the second result as the first source");
        }
        private static void Descriptions()
        {
            var context=ExpandedAssistantTests.Context();var stock=Stock();string text=AssistantSearchDescription.Describe(stock,context);
            Check(text.StartsWith("Checking available") && text.Contains("LX310") && text.Contains("condition: Used") && text.Contains("category: Printer"),"description identifies stock and product filters");
            Check(text.Contains("creation dates") && text.Contains("October 1, 2026") && !text.Contains("254"),"date meaning is explicit and no database totals are guessed");
            var tickets=Tickets();tickets.FromLocal=Today;
            text=AssistantSearchDescription.Describe(tickets,context);
            Check(text.Contains("IT tickets") && text.Contains("status: Solved") && text.Contains("assigned to you") && text.Contains("status changes"),"ticket status, ownership and date semantics are described");
            text=AssistantSearchDescription.Describe(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Detail,RecordId=69},context);
            Check(text.StartsWith("Checking details for IT ticket #69"),"detail description identifies record");
            text=AssistantSearchDescription.Describe(new AssistantRequest {Capability="items",Operation=AssistantOperation.Detail,RecordId=12,InStockOnly=true},context);
            Check(text.StartsWith("Checking details for item #12") && text.Contains("available stock only"),"stock detail identifies the item record and quantity constraint separately");
            text=AssistantSearchDescription.Describe(new AssistantRequest {Capability="invoices",Operation=AssistantOperation.Summary,GroupBy="Month",Page=1,IncludeArchived=true},context);
            Check(text.Contains("summary of invoices") && text.Contains("grouped by: Month") && text.Contains("page 2") && text.Contains("including archived"),"summary, page and archive scope remain visible");
        }
        private static async Task Conversation()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();var service=Service(reader,model);
            await service.AskAsync("how many LX310 in stock and how many my solved tickets",context,CancellationToken.None);
            int before=reader.Queries.Count;
            var second=await service.AskAsync("the second result, only this month",context,CancellationToken.None);
            Check(second.IsLive && reader.Queries.Count==before+1 && second.Request.Capability=="tickets" && second.Request.Personal && second.Request.Status=="Solved" && second.Request.FromLocal.HasValue,"typed ordinal executes one fresh selected lookup");
            Check(model.Calls==0,"clear contextual references work with AI unavailable");
            await service.AskAsync("how many LX310 in stock and how many my solved tickets",context,CancellationToken.None);
            var choice=await service.AskAsync("only this month",context,CancellationToken.None);
            Check(choice.ClarificationChoices.Count==2,"ambiguous refinement still offers previous scopes");
            second=await service.AskAsync("the second result",context,CancellationToken.None);
            Check(second.IsLive && second.Request.Capability=="tickets" && second.Request.FromLocal.HasValue,"ordinal answer resolves saved clarification, retaining its pending period");
            await service.AskAsync("how many LX310 in stock and how many my solved tickets",context,CancellationToken.None);
            await service.AskAsync("only this month",context,CancellationToken.None);before=reader.Queries.Count;
            second=await service.AskAsync("the second result, only high priority",context,CancellationToken.None);
            Check(second.IsLive && second.Request.Capability=="tickets" && second.Request.FromLocal.HasValue && second.Request.Priority=="High" && second.Request.Status=="Solved" && second.Request.Personal,"ordinal reply may add a filter while retaining pending clarification scope");
            Check(reader.Queries.Count==before+1,"composed clarification performs one fresh lookup");
            await service.AskAsync("how many LX310 in stock and how many my solved tickets",context,CancellationToken.None);
            choice=await service.AskAsync("same item only brand new",context,CancellationToken.None);
            Check(choice.ClarificationChoices.Count==1 && choice.ClarificationChoices[0].Label=="Items and stock","same-item clarification omits incompatible ticket result");
            var selected=await service.ResolveClarificationAsync(choice.ClarificationChoices[0],context,CancellationToken.None);
            Check(selected.IsLive && selected.Request.Capability=="items" && selected.Request.Condition=="Brand New" && selected.Request.Search=="lx310","compatible choice retains product scope and applies the refinement");
            await service.AskAsync("how many LX310 in stock?",context,CancellationToken.None);
            var same=await service.AskAsync("same item only brand new",context,CancellationToken.None);
            Check(same.IsLive && same.Request.ModelMatch?.Key=="LX310" && same.Request.Condition=="Brand New" && same.Request.Search=="lx310","same item retains catalog model evidence");
            before=reader.Queries.Count;var ambiguous=await service.AskAsync("the second item",context,CancellationToken.None);
            Check(ambiguous.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before && ambiguous.ClarificationChoices.Count==0,"row reference never becomes an ordinal lookup or a broad query");
            service.Reset();before=reader.Queries.Count;
            Check((await service.AskAsync("second result",context,CancellationToken.None)).Outcome==AssistantOutcome.Clarification && reader.Queries.Count==before,"new chat discards ordinal context");
        }
        private static async Task Progress()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);var descriptions=new List<string>();
            reader.BeforeRead=()=>Check(descriptions.Count>0,"description arrives before reader starts");
            var result=await service.AskAsync("how many LX 310 are in stock?",context,CancellationToken.None,descriptions.Add);
            Check(result.IsLive && descriptions.Count==1 && descriptions[0].Contains("LX310"),"validated model description accompanies original question");
            descriptions.Clear();reader.BeforeRead=null;
            await service.AskAsync("how many records?",context,CancellationToken.None,descriptions.Add);
            Check(descriptions.Count==0,"uncertain questions do not announce an invented search");
            descriptions.Clear();await service.AskAsync("hello",context,CancellationToken.None,descriptions.Add);
            Check(descriptions.Count==0,"general chat has no lookup description");
            await service.AskAsync("how many LX310 in stock and how many my tickets",context,CancellationToken.None,descriptions.Add);
            Check(descriptions.Count==2 && descriptions[0].StartsWith("Result 1 of 2:") && descriptions[1].StartsWith("Result 2 of 2:"),"two lookups identify which result is being checked");
            descriptions.Clear();await service.ExecuteRequestAsync(Stock(),context,CancellationToken.None,descriptions.Add);
            Check(descriptions.Count==1 && descriptions[0].Contains("LX310"),"direct card actions receive the same description");
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new string[0];
            descriptions.Clear();int before=reader.Queries.Count;
            var refused=await service.ExecuteRequestAsync(Stock(),denied,CancellationToken.None,descriptions.Add);
            Check(refused.Outcome==AssistantOutcome.Denied && descriptions.Count==0 && reader.Queries.Count==before,"denied filters are never reported as an active search");
            before=reader.Queries.Count;service=Service(reader);
            result=await service.AskAsync("how many LX310 in stock?",context,CancellationToken.None,_=>service.Reset());
            Check(result.Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"session/reset change during progress prevents query");
            using(var cancellation=new CancellationTokenSource())
            {
                service=Service(reader);bool stopped=false;
                try {await service.AskAsync("how many LX310 in stock?",context,cancellation.Token,_=>cancellation.Cancel());} catch(OperationCanceledException) {stopped=true;}
                Check(stopped && reader.Queries.Count==before,"cancellation during progress prevents query");
            }
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {if(root is T) yield return (T)root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Descendants<T>(child)) yield return item;}
        private static void Pump(Task task,YakumiChatView view)
        {
            var until=DateTime.UtcNow.AddSeconds(5);
            while(!task.IsCompleted && DateTime.UtcNow<until) view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);
            Check(task.IsCompleted,"pending UI reply completes");task.GetAwaiter().GetResult();
        }
        private static void Interface()
        {
            Check(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"UI checks run on STA");
            var context=ExpandedAssistantTests.Context();var reader=new Reader {Delay=true};
            using(var view=new YakumiChatView(()=>context,Service(reader)))
            {
                var turn=view.SubmitAsync("how many LX310 in stock?");
                Check(!turn.IsCompleted && Descendants<TextBlock>(view).Any(t=>t.Text.StartsWith("Checking available") && t.Text.Contains("LX310")),"chat displays the description while read is pending");
                reader.Complete();Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text.StartsWith("Checking available")),"final answer replaces in-flight description");
                turn=view.SubmitAsync("how many LX350 in stock?");view.NewConversation();reader.Complete();Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text.Contains("LX350")),"old progress/result cannot return after new chat");
                turn=view.SubmitAsync("how many LX310 in stock?");view.Cancel();reader.Complete();Pump(turn,view);
                Check(Descendants<TextBlock>(view).Any(t=>t.Text=="Reply cancelled") && !Descendants<TextBlock>(view).Any(t=>t.Text.StartsWith("Checking available")),"Stop replaces progress with cancellation without facts: "+string.Join("|",Descendants<TextBlock>(view).Select(t=>t.Text)));
            }
        }
        public static Dictionary<string,string> SqlCases()
        {
            return new[] {"first result only brand new","second result only this month","same item only this month","same but only brand new"}.ToDictionary(q=>"follow-up "+q,q=>
                AssistantQueryEngine.BuildQuery(Request(q,q.StartsWith("same",StringComparison.Ordinal)?new[] {Stock()}:new[] {Stock(),Tickets()}),ExpandedAssistantTests.Context()).CommandText);
        }
    }
}
