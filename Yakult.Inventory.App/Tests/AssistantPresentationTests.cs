using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>Synthetic presentation and interaction checks; no production startup, model or database.</summary>
    public static class AssistantPresentationTests
    {
        private static int _checks;
        private static void Check(bool value,string message)
        {if(!value) throw new InvalidOperationException("Yakumi presentation: "+message);_checks++;}
        private sealed class Reader : IAssistantDataReader
        {
            internal int Calls;
            internal bool Delay;
            internal bool Empty,Unavailable;
            internal AssistantSqlQuery Last;
            internal TaskCompletionSource<DataTable> Pending;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {
                Calls++;Last=query;
                if(Unavailable) throw new InvalidOperationException("Synthetic database unavailable");
                if(Empty) return Task.FromResult(ExpandedAssistantTests.Fixture(query,0,0,0));
                if(Delay) {Pending=new TaskCompletionSource<DataTable>();return Pending.Task;}
                return Task.FromResult(Data(query));
            }
            private static DataTable Data(AssistantSqlQuery query)
            {return ExpandedAssistantTests.Fixture(query,query.Request.Operation==AssistantOperation.Detail?1:9,367,
                query.Request.Operation==AssistantOperation.Count?0:query.Request.Operation==AssistantOperation.Detail?1:query.Request.Operation==AssistantOperation.Summary?3:8);}
            internal void Complete() {Pending.SetResult(Data(Last));}
        }
        private sealed class Model : IAssistantModel
        {
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext context,CancellationToken ct) {throw new InvalidOperationException("offline");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {throw new InvalidOperationException("offline");}
            public Task<string> GeneralAsync(List<ChatMessage> h,string q,CancellationToken ct) {return Task.FromResult("Use **clear wording**.\n\n- First point\n- Second point\n\n1. Open the page\n2. Check its filters");}
        }
        private static YakumiAssistantService Service(Reader reader,Func<AssistantContext,bool> current=null)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),new Model(),current ?? (_=>true));}
        private static AssistantResult Fact(AssistantRequest request,long count=9,decimal total=367,int rows=0)
        {
            var query=AssistantQueryEngine.BuildQuery(request,ExpandedAssistantTests.Context());
            return AssistantQueryEngine.Render(query,ExpandedAssistantTests.Fixture(query,count,total,rows));
        }
        public static string RunAll()
        {
            _checks=0;Formatting();FollowUps().GetAwaiter().GetResult();Friendly().GetAwaiter().GetResult();Interaction();
            return _checks+" presentation and interaction checks passed.";
        }
        private static void Formatting()
        {
            var request=new AssistantRequest {Capability="items",Operation=AssistantOperation.Count,Search="Redmi",Category="Cellphone",InStockOnly=true};
            var result=Fact(request,9,367);result.Text="WRONG legacy response: 999999";
            var view=AssistantPresentationFormatter.Format(result);
            Check(view.Headline.StartsWith("There are 367 ") && view.Headline.Contains("in stock"),"SQL unit total leads stock headline");
            Check(view.Metrics.Single(m=>m.Label=="Matching records").Value=="9","record count remains separate");
            Check(!view.CopyText.Contains("999999") && !view.CopyText.Contains("Stock units: 367"),"legacy text ignored and headline total not duplicated");
            Check(view.Filters.Contains("Search: Redmi") && view.Filters.Contains("Category: Cellphone"),"visible item and category");
            Check(view.Details.Any(d=>d.Label=="Source") && view.Details.Any(d=>d.Label=="Complete scope") && view.Details.Any(d=>d.Label=="Retrieved" && d.Value.Contains("Singapore")),"complete provenance with original timezone");
            view=AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="tickets",Status="Solved",Operation=AssistantOperation.Count,Personal=true,FromLocal=new DateTime(2026,10,1),ToLocalExclusive=new DateTime(2026,11,1)}));
            Check(view.Headline.Contains("9 solved IT tickets"),"concise status headline");
            Check(AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="tickets",Status="Solved",Operation=AssistantOperation.Count},1)).Headline=="I found 1 solved IT ticket.","singular status headline");
            Check(view.Filters.Contains("My records") && view.Details.Any(f=>f.Label=="Date filter" && f.Value.Contains("2026-11-01") && f.Value.Contains("exclusive")) && view.CopyText.Contains("2026-11-01"),"personal scope and precise date boundary retained in Details and Copy");
            Check(AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="tickets",Status="Open"})).Qualifications.Any(q=>q.Contains("aren’t solved, closed, temporarily resolved or cancelled")),"open status meaning remains visible in everyday language");
            view=AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="items",InStockOnly=true},0,0));
            Check(view.Outcome==AssistantOutcome.NoMatches && view.Headline=="There are 0 units in stock." && view.Actions.Count==0,"real zero is distinct and has no records action");
            foreach(var outcome in new[] {AssistantOutcome.Denied,AssistantOutcome.Unavailable,AssistantOutcome.Cancelled})
            {
                var message=AssistantPresentationFormatter.Format(AssistantResult.Message(outcome,"Synthetic reason"));
                Check(message.Headline!=view.Headline && message.Outcome==outcome && !message.CopyText.Contains("0 matching"),"failure outcome distinct: "+outcome);
            }
            view=AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="items",InStockOnly=true},2,12.3456m));
            Check(view.Headline=="There are 12.3456 units in stock.","fractional quantities retained");
            view=AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="invoices",Operation=AssistantOperation.Count},2,12345.67m));
            Check(view.Metrics.Single(m=>m.Label=="Stored amount due").Value=="12,345.67","stored financial amounts retain decimals without currency invention");
            result=Fact(new AssistantRequest {Capability="items"},9,367,8);
            var row=result.Rows[0].ToDictionary(f=>f.Key,f=>f.Value);row["Label"]=new string('W',240);row["Model"]=null;
            result.Rows=new[] {(IReadOnlyDictionary<string,object>)row}.Concat(result.Rows.Skip(1)).ToList();
            view=AssistantPresentationFormatter.Format(result);
            Check(view.Records.Count==8 && view.Records[0].Title.Length==240,"all eight records and long names preserved");
            Check(view.Records[0].Fields[0].Label=="Stock units","record preview leads with relevant quantity");
            Check(view.Records[0].Fields.Any(f=>f.Label=="Model" && f.Value=="Not recorded"),"null field remains explicit");
            Check(view.CopyText.Contains("Synthetic item 7") && view.CopyText.Contains("Source:") && view.CopyText.Contains("Complete scope:") && view.CopyText.Contains("Retrieved:"),"copy includes hidden loaded records and provenance");
            result=Fact(new AssistantRequest {Capability="items",Operation=AssistantOperation.Detail,RecordId=1},1,4,1);
            Check(AssistantPresentationFormatter.Format(result).Records.Single().IsDetail,"detail renders all labelled fields");
            view=AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="items",Page=2},9,367,0));
            Check(view.Outcome==AssistantOutcome.Success && view.PageCaption.Contains("No records on this page") && view.Actions.Any(a=>a.Label=="Previous page"),"empty page does not become zero matches");
            result=Fact(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Summary,GroupBy="Status"},50,0,3);
            view=AssistantPresentationFormatter.Format(result);
            Check(view.Metrics.Any(m=>m.Label=="Matching groups" && m.Value=="3") && view.Records.All(r=>r.Title.StartsWith("Status:")),"group counts and labelled groups");
            var first=Fact(request,9,10);var second=Fact(request,6,13.25m);
            result=AssistantResult.Message(AssistantOutcome.Success,"Do not parse this text");result.IsComparison=true;result.Children=new[] {first,second};
            view=AssistantPresentationFormatter.Format(result);
            Check(view.Children.Count==2 && view.Metrics.Any(m=>m.Label=="Stock units difference" && m.Value=="+3.25") && view.Metrics.Any(m=>m.Label=="Matching records difference" && m.Value=="-3"),"comparison uses typed children and independent differences");
            result.Children=new[] {first,Fact(new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count})};
            view=AssistantPresentationFormatter.Format(result);Check(view.Metrics.Count==0 && view.Qualifications.Single().Contains("different records"),"unlike sources are not subtracted");
            string copied=null;var controls=AssistantAnswerRenderer.Render(AssistantPresentationFormatter.Format(Fact(new AssistantRequest {Capability="items"},9,367,8)),_=>{},null,text=>copied=text);
            Check(Descendants<Expander>(controls).Where(e=>Equals(e.Header,"Details")).All(e=>!e.IsExpanded),"provenance starts collapsed");
            Click(Descendants<Button>(controls).First(b=>Equals(b.Content,"Copy")));
            Check(copied!=null && copied.Contains("Synthetic item 7") && copied.Contains("Retrieved:"),"Copy button carries the complete readable payload");
            var prose=new StackPanel();AssistantAnswerRenderer.AddProse(prose,"A **bold** point.\n\n- One\n- Two\n\n1. Step one\n2. Step two");
            Check(Descendants<TextBlock>(prose).Any(b=>b.Inlines.OfType<System.Windows.Documents.Bold>().Any()) && Descendants<Grid>(prose).Count()==4,"native bold, bullets and numbered steps");
            prose=new StackPanel();AssistantAnswerRenderer.AddProse(prose,string.Join(" ",Enumerable.Repeat("This is a long sentence kept intact so that its meaning is preserved.",8)));
            Check(prose.Children.Count==4,"long prose is divided into short paragraphs without rewriting");
            var service=Service(new Reader());
            result=service.AskAsync("How do I borrow equipment?",ExpandedAssistantTests.Context(),CancellationToken.None).GetAwaiter().GetResult();
            view=AssistantPresentationFormatter.Format(result);
            Check(result.GuideCitations.Count>0 && result.ExplanationText!=null && !result.ExplanationText.Contains("Guide source:"),"explanation and citations separated");
            Check(view.CopyText.Contains("[borrow") && view.CopyText.Contains(result.GuideCitations[0].Source),"guide citations included in copy");
            result=service.AskAsync("keyboard stock and my requests",ExpandedAssistantTests.Context(),CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Children.Count==2 && result.Children.All(r=>r.Request!=null),"combined service answers retain typed children");
            result=service.AskAsync("compare solved tickets this month with last month",ExpandedAssistantTests.Context(),CancellationToken.None).GetAwaiter().GetResult();
            Check(result.IsComparison && result.Children.Count==2,"comparison service retains comparison metadata");
        }
        private static async Task FollowUps()
        {
            var reader=new Reader();var service=Service(reader);var context=ExpandedAssistantTests.Context();
            var result=await service.AskAsync("show my solved tickets this month",context,CancellationToken.None);
            var original=result.Request.Copy();var action=AssistantPresentationFormatter.Format(result).Actions.Single(a=>a.Label=="Next page");
            result.Request.Status="Closed";action.Request.Status="Closed";
            await service.AskAsync("show invoices",context,CancellationToken.None);
            await service.ExecuteRequestAsync(action.Request,context,CancellationToken.None);
            Check(reader.Last.Request.Capability==original.Capability && reader.Last.Request.Status=="Solved" && reader.Last.Request.Personal && reader.Last.Request.FromLocal==original.FromLocal && reader.Last.Request.Page==1,"paging uses a private snapshot after intervening questions");
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new[] {"RequesterPortal"};
            int before=reader.Calls;result=await service.ExecuteRequestAsync(action.Request,denied,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Denied && reader.Calls==before,"typed actions recheck permissions");
            service=Service(reader,_=>false);result=await service.ExecuteRequestAsync(action.Request,context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Cancelled && reader.Calls==before,"typed actions recheck session");
            using(var cts=new CancellationTokenSource())
            {
                cts.Cancel();bool stopped=false;
                try {await Service(reader).ExecuteRequestAsync(action.Request,context,cts.Token);}catch(OperationCanceledException){stopped=true;}
                Check(stopped && reader.Calls==before,"typed action honors cancellation before querying");
            }
        }
        private static async Task Friendly()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            var request=new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Company="YMC",
                Organizations=new[] {new AssistantOrganization("Company",17,"YMC")},Search="printer",Priority="High",
                Personal=true,PersonalRelation="Created",DateField="CreatedAt",FromLocal=new DateTime(2026,10,1),ToLocalExclusive=new DateTime(2026,11,1)};
            var answer=Fact(request);var view=AssistantPresentationFormatter.Format(answer);
            Check(view.Actions.Select(a=>a.Label).SequenceEqual(new[] {"Show tickets","Only open","By department"}),"helpful ticket actions have natural labels");
            Check(reader.Calls==0,"formatting suggestions never executes a search");
            var actions=view.Actions.ToArray();
            answer.Request.Company="Changed";request.Priority="Low";
            await service.AskAsync("show invoices",context,CancellationToken.None);
            foreach(var action in actions)
            {
                var snapshot=action.Request;snapshot.Company="Tampered";
                var result=await service.ExecuteRequestAsync(action.Request,context,CancellationToken.None);
                var current=reader.Last.Request;
                Check(result.IsLive && current.Company=="YMC" && current.Organizations.Single().Id==17 && current.Priority=="High" && current.Search=="printer" && current.Personal && current.PersonalRelation=="Created" && current.FromLocal==new DateTime(2026,10,1) && current.ToLocalExclusive==new DateTime(2026,11,1) && current.DateField=="CreatedAt" && current.Page==0,"suggestion keeps the originating filters after another question: "+action.Label);
                if(action.Label=="Only open") Check(current.Status=="Open" && reader.Last.CommandText.Contains("NOT IN"),"open suggestion uses existing nonterminal-ticket semantics");
                if(action.Label=="By department") Check(current.Operation==AssistantOperation.Summary && current.GroupBy=="Department","department suggestion uses a supported typed grouping");
                if(action.Label=="Show tickets") Check(current.Operation==AssistantOperation.List,"show suggestion loads a fresh list");
            }
            var company=new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Company="YMC"};
            foreach(int count in new[] {0,1,2})
            {
                view=AssistantPresentationFormatter.Format(Fact(company,count));
                Check(view.Headline==(count==0?"I couldn’t find any IT tickets in company “YMC”.":"YMC has "+count+" IT ticket"+(count==1?"":"s")+"."),"friendly company answer preserves count and grammar: "+count);
                Check(view.CopyText.Contains(view.Headline),"Copy retains friendly factual answer: "+count);
            }
            var dated=actions[0].Request;reader.Empty=true;
            var empty=await service.ExecuteRequestAsync(dated,context,CancellationToken.None);int calls=reader.Calls;
            view=AssistantPresentationFormatter.Format(empty);
            Check(empty.Outcome==AssistantOutcome.NoMatches && view.Body.Contains("Try another period") && view.Body.Contains("creation dates") && view.Actions.Single().Label=="Check all dates" && reader.Calls==calls,"true zero offers an explicit date change without retrying or hiding period semantics");
            reader.Empty=false;await service.ExecuteRequestAsync(view.Actions.Single().Request,context,CancellationToken.None);
            Check(reader.Last.Request.FromLocal==null && reader.Last.Request.ToLocalExclusive==null && reader.Last.Request.Company=="YMC" && reader.Last.Request.Personal && reader.Last.Request.Priority=="High" && reader.Last.Request.Search=="printer" && reader.Last.Request.Organizations.Single().Id==17 && dated.FromLocal.HasValue,"all-dates suggestion changes only date limits and page");
            foreach(var operation in new[] {AssistantOperation.Detail,AssistantOperation.History})
            {
                var detail=dated.Copy();detail.Operation=operation;detail.RecordId=69;
                view=AssistantPresentationFormatter.Format(Fact(detail,0,0));
                Check(view.Actions.Count==0 && !view.Body.Contains("Check all dates") && view.Body.Contains("record number"),"record lookups do not offer broader suggestions: "+operation);
            }
            var filtered=company.Copy();filtered.Status="Solved";filtered.Department="IT";
            view=AssistantPresentationFormatter.Format(Fact(filtered));
            Check(!view.Actions.Any(a=>a.Label=="Only open" || a.Label=="By department"),"suggestions respect explicit status and department filters");
            var stock=new AssistantRequest {Capability="cartridges",Operation=AssistantOperation.Count,Search="lx310",InStockOnly=true,Condition="Brand New",ModelMatch=new AssistantModelMatch("LX310","lx310")};
            var stockView=AssistantPresentationFormatter.Format(Fact(stock));
            Check(stockView.Actions.Single().Request.ModelMatch.Key=="LX310" && stockView.Actions.Single().Request.Condition=="Brand New" && !stockView.Actions.Any(a=>a.Label=="By department"),"stock suggestion retains model and condition without unsupported grouping");
            await service.ExecuteRequestAsync(actions[0].Request,context,CancellationToken.None);calls=reader.Calls;
            foreach(string greeting in new[] {"Hi Yakumi!","yo","Good morning","thanks!","thank you yakumi"})
            {
                var result=await service.AskAsync(greeting,context,CancellationToken.None);
                Check(result.Outcome==AssistantOutcome.Guidance && !result.Text.Contains("Use **clear wording**") && reader.Calls==calls,"greetings and thanks are friendly without model or database: "+greeting);
            }
            var follow=await service.AskAsync("only this month",context,CancellationToken.None);
            Check(follow.IsLive && follow.Request.Capability=="tickets" && follow.Request.Company=="YMC" && follow.Request.Search=="printer" && follow.Request.Personal,"brief thanks keeps the previous lookup context");
            reader.Unavailable=true;var unavailable=await service.ExecuteRequestAsync(actions[0].Request,context,CancellationToken.None);
            view=AssistantPresentationFormatter.Format(unavailable);
            Check(unavailable.Outcome==AssistantOutcome.Unavailable && unavailable.Text==AssistantEnglish.Unavailable && view.Actions.Count==0 && !view.CopyText.Contains("0 IT tickets"),"friendly database error never becomes a count or a broader suggestion");
            reader.Unavailable=false;
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new[] {"RequesterPortal"};calls=reader.Calls;
            var access=await service.ExecuteRequestAsync(actions[1].Request,denied,CancellationToken.None);
            Check(access.Outcome==AssistantOutcome.Denied && access.Text.Contains("You’ll need access to IT tickets") && reader.Calls==calls && AssistantPresentationFormatter.Format(access).Actions.Count==0,"suggestion access failure is specific and does not query");
            using(var chat=new YakumiChatView(()=>context,Service(reader)))
            {
                chat.RefreshContext(context);chat.SubmitAsync("how many tickets?").GetAwaiter().GetResult();Layout(chat);
                var open=Descendants<Button>(Field<Border>(chat,"_latestReply")).Single(b=>Equals(b.Content,"Only open"));
                calls=reader.Calls;Click(open);Layout(chat);
                Check(reader.Calls==calls+1 && reader.Last.Request.Status=="Open","clicking the rendered open button submits one fresh filtered lookup");
            }
        }
        private static T Field<T>(YakumiChatView view,string name)
        {return (T)typeof(YakumiChatView).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);}
        private static void Call(YakumiChatView view,string name)
        {typeof(YakumiChatView).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,null);}
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {
            if(root is T) yield return (T)root;
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Descendants<T>(child)) yield return item;
        }
        private static void Click(Button button) {button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
        private static void Layout(YakumiChatView view)
        {
            view.Measure(new Size(350,view.Height));view.Arrange(new Rect(0,0,350,view.Height));view.UpdateLayout();
            view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);view.UpdateLayout();
        }
        private static void Pump(Task task,YakumiChatView view)
        {
            var until=DateTime.UtcNow.AddSeconds(5);
            while(!task.IsCompleted && DateTime.UtcNow<until) {view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);Thread.Sleep(1);}
            Check(task.IsCompleted,"pending UI task completes");task.GetAwaiter().GetResult();Layout(view);
        }
        private static void Interaction()
        {
            Check(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"UI test thread is STA");
            var context=ExpandedAssistantTests.Context();var reader=new Reader();
            using(var view=new YakumiChatView(()=>context,Service(reader)))
            {
                view.RefreshContext(context);Layout(view);
                Check(view.Width==350 && view.Height==540,"familiar widget size");
                Check(Field<WrapPanel>(view,"_starters").Children.Count==4,"four permitted starters");
                var scroll=Field<ScrollViewer>(view,"_yakumiScroll");Check(double.IsNaN(scroll.Height) && scroll.ViewportHeight>200,"conversation uses flexible available height");
                var input=Field<TextBox>(view,"_yakumiInput");
                input.Text="first";input.CaretIndex=5;view.HandleEnter(true);input.AppendText("second");
                Check(input.AcceptsReturn && input.Text=="first"+Environment.NewLine+"second","Shift+Enter inserts a newline");
                input.Text="show items";view.HandleEnter(false);Layout(view);
                Check(reader.Calls==1 && input.Text=="" && Field<WrapPanel>(view,"_starters").Visibility==Visibility.Collapsed,"Enter sends and hides starters");
                Check(Math.Abs(input.FontSize-13.0*96/72)<0.01 && Descendants<TextBlock>(view).Any(b=>b.Text=="Ask Yakumi…" && b.Visibility==Visibility.Visible),"13 pt input and visible empty-input placeholder");
                Check(scroll.VerticalOffset<=Field<Border>(view,"_latestReply").TranslatePoint(new Point(0,0),Field<StackPanel>(view,"_yakumiMessages")).Y+1,"long replies open at their headline");
                var more=Descendants<Button>(view).Single(b=>(b.Content as string ?? "").StartsWith("Show more"));
                int calls=reader.Calls;Click(more);Layout(view);
                Check(reader.Calls==calls && Equals(more.Content,"Show fewer") && Descendants<TextBlock>(view).Any(t=>t.Text=="Synthetic item 7"),"Show more reveals loaded records without query");
                Click(more);Check(!Descendants<TextBlock>(view).Any(t=>t.Text=="Synthetic item 7"),"Show fewer restores compact previews");
                Call(view,"ToggleYakumiMinimized");Layout(view);
                Check(view.Height==56 && Field<Grid>(view,"_yakumiBody").Visibility==Visibility.Collapsed && Field<StackPanel>(view,"_yakumiSettings").Visibility==Visibility.Collapsed,"minimize is header only");
                Call(view,"ToggleYakumiMinimized");Layout(view);
                Check(view.Height==540 && Field<Grid>(view,"_yakumiBody").Visibility==Visibility.Visible,"restore returns to chat");
                // Set the view flag directly to avoid a real connection check during offline UI tests.
                typeof(YakumiChatView).GetField("_showingSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(view,true);
                Call(view,"ApplyViewState");Call(view,"ToggleYakumiMinimized");Call(view,"ToggleYakumiMinimized");Layout(view);
                Check(Field<StackPanel>(view,"_yakumiSettings").Visibility==Visibility.Visible && Field<Grid>(view,"_yakumiBody").Visibility==Visibility.Collapsed,"restore returns to settings");
                typeof(YakumiChatView).GetField("_showingSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(view,false);Call(view,"ApplyViewState");
                scroll.ScrollToTop();Layout(view);double offset=scroll.VerticalOffset;
                reader.Delay=true;var turn=view.SubmitAsync("show my solved tickets");Layout(view);
                Check(Field<Button>(view,"_sendButton").ToolTip.ToString()=="Stop reply","Stop remains available during request");
                reader.Complete();Pump(turn,view);
                Check(Math.Abs(scroll.VerticalOffset-offset)<1 && Field<Button>(view,"_newReplyButton").Visibility==Visibility.Visible,"incoming reply preserves reading position and shows shortcut");
                Click(Field<Button>(view,"_newReplyButton"));Layout(view);
                var latest=Field<Border>(view,"_latestReply");double replyOffset=Math.Min(scroll.ScrollableHeight,latest.TranslatePoint(new Point(0,0),Field<StackPanel>(view,"_yakumiMessages")).Y);
                Check(Math.Abs(scroll.VerticalOffset-replyOffset)<2 && Field<Button>(view,"_newReplyButton").Visibility==Visibility.Collapsed,"New reply jumps to the start of the latest answer");
                turn=view.SubmitAsync("show requests");view.Cancel();reader.Complete();Pump(turn,view);
                Check(Descendants<TextBlock>(view).Any(t=>t.Text=="Reply cancelled"),"Stop renders a distinct cancellation");
                turn=view.SubmitAsync("show items");var old=reader.Pending;var oldQuery=reader.Last;
                view.NewConversation();reader.Delay=false;view.SubmitAsync("how many solved tickets?").GetAwaiter().GetResult();
                old.SetResult(ExpandedAssistantTests.Fixture(oldQuery,999,999,1));Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text.Contains("999")) && Descendants<TextBlock>(view).Any(t=>t.Text=="I found 9 solved IT tickets."),"New chat rejects old replies and preserves newer turn controls");
                view.NewConversation();view.SubmitAsync(new string('x',AiIntentRouter.MaxQuestionLength+1)).GetAwaiter().GetResult();
                Check(Field<WrapPanel>(view,"_starters").Visibility==Visibility.Visible,"oversized input retains the existing limit without starting a turn");
                context=ExpandedAssistantTests.Context();context.IsDeveloper=false;context.IsSuperAdmin=false;context.Portals=new[] {"RequesterPortal"};context.SessionStamp="restricted";view.RefreshContext(context);
                var labels=Field<WrapPanel>(view,"_starters").Children.OfType<Button>().Select(b=>b.Content.ToString()).ToArray();
                Check(!labels.Contains("Stock") && !labels.Contains("Tickets") && labels.Contains("My requests"),"starters respect permissions");
                calls=reader.Calls;view.SubmitFollowUpAsync(new AssistantFollowUp("Next page",new AssistantRequest {Capability="items"}),"offline-test").GetAwaiter().GetResult();
                Check(reader.Calls==calls,"old session action cannot query");
            }
        }

        public static string RenderPreviews(string directory)
        {
            System.IO.Directory.CreateDirectory(directory);
            foreach(double scale in new[] {1.0,1.25,1.5})
            foreach(string scenario in new[] {"welcome","stock","list","details","summary","comparison","guide","unavailable","settings","minimized","clarification","status-choice","result-choice","product-choice","search-progress","company-count","organization-choice","ticket-actions","empty-period","mapped-question","priority-choice","done-choice"})
            {
                var context=ExpandedAssistantTests.Context();var reader=new Reader();
                using(var view=new YakumiChatView(()=>context,scenario=="product-choice"?AssistantProductMatchingTests.PreviewService():scenario=="company-count" || scenario=="organization-choice" || scenario=="mapped-question"?AssistantOrganizationTests.PreviewService():Service(reader)))
                {
                    Task pendingTurn=null;
                    view.RefreshContext(context);
                    if(scenario=="company-count") view.SubmitAsync("How many tickets are there in YMC?").GetAwaiter().GetResult();
                    if(scenario=="organization-choice") view.SubmitAsync("How many tickets in HQ?").GetAwaiter().GetResult();
                    if(scenario=="mapped-question") view.SubmitAsync("Ilan ang mga tix sa company YMC ngayong buwan?").GetAwaiter().GetResult();
                    if(scenario=="priority-choice") view.SubmitAsync("How many my urgent tix this month?").GetAwaiter().GetResult();
                    if(scenario=="done-choice") view.SubmitAsync("How many my done tix this month?").GetAwaiter().GetResult();
                    if(scenario=="ticket-actions") view.SubmitAsync("How many tickets?").GetAwaiter().GetResult();
                    if(scenario=="empty-period") {reader.Empty=true;view.SubmitAsync("How many my solved tickets this month?").GetAwaiter().GetResult();}
                    if(scenario=="search-progress") {reader.Delay=true;pendingTurn=view.SubmitAsync("How many LX310 are in stock?");}
                    if(scenario=="stock") view.SubmitAsync("How many Redmi cellphones are in stock?").GetAwaiter().GetResult();
                    if(scenario=="list") view.SubmitAsync("show my solved tickets this month").GetAwaiter().GetResult();
                    if(scenario=="details") view.SubmitAsync("TCK-69").GetAwaiter().GetResult();
                    if(scenario=="summary") view.SubmitAsync("tickets by status").GetAwaiter().GetResult();
                    if(scenario=="comparison") view.SubmitAsync("compare solved tickets this month with last month").GetAwaiter().GetResult();
                    if(scenario=="guide") view.SubmitAsync("How do I borrow equipment?").GetAwaiter().GetResult();
                    if(scenario=="clarification") view.SubmitAsync("How many records this month?").GetAwaiter().GetResult();
                    if(scenario=="status-choice") view.SubmitAsync("How many my resolved tickets this month?").GetAwaiter().GetResult();
                    if(scenario=="product-choice") view.SubmitAsync("How many LX330 in stock?").GetAwaiter().GetResult();
                    if(scenario=="result-choice")
                    {
                        view.SubmitAsync("how many my solved tickets and how many my requests").GetAwaiter().GetResult();
                        view.SubmitAsync("only this month").GetAwaiter().GetResult();
                    }
                    if(scenario=="unavailable")
                    {
                        var bubble=typeof(YakumiChatView).GetMethod("AddYakumiBubble",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,new object[] {false,"",false});
                        typeof(YakumiChatView).GetMethod("RenderAnswer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(view,new[] {bubble,AssistantResult.Message(AssistantOutcome.Unavailable,AssistantEnglish.Unavailable),context.SessionStamp});
                    }
                    if(scenario=="settings")
                    {
                        typeof(YakumiChatView).GetField("_showingSettings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(view,true);Call(view,"ApplyViewState");
                        Field<TextBox>(view,"_setUrlBox").Text="http://AI-PC:11434";Field<ComboBox>(view,"_setModelBox").Text="qwen2.5:3b";
                    }
                    if(scenario=="minimized") Call(view,"ToggleYakumiMinimized");
                    // A real WPF embedding surface, with the view aligned to the bottom-right.
                    var grid=new Grid {Width=390,Height=580,Background=Brushes.White};
                    view.HorizontalAlignment=HorizontalAlignment.Right;view.VerticalAlignment=VerticalAlignment.Bottom;view.Margin=new Thickness(20);grid.Children.Add(view);
                    grid.Measure(new Size(390,580));grid.Arrange(new Rect(0,0,390,580));grid.UpdateLayout();
                    view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);grid.UpdateLayout();
                    grid.UpdateLayout();
                    var bitmap=new RenderTargetBitmap((int)(390*scale),(int)(580*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(grid);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
                    using(var stream=System.IO.File.Create(System.IO.Path.Combine(directory,scenario+"-"+(int)(scale*100)+".png"))) png.Save(stream);
                    grid.Children.Clear();
                    if(pendingTurn!=null) {reader.Complete();Pump(pendingTurn,view);}
                }
            }
            return "66 synthetic WPF previews rendered at 100%, 125% and 150%: "+directory;
        }
    }
}
