using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    public static class AssistantPerformanceTests
    {
        private static int _checks;
        private static void Check(bool condition,string message)
        {if(!condition) throw new InvalidOperationException("Yakumi performance: "+message);_checks++;}
        private sealed class Reader : IAssistantDataReader
        {
            internal int Calls;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {ct.ThrowIfCancellationRequested();Calls++;return Task.FromResult(ExpandedAssistantTests.Fixture(query,Calls,Calls,query.Request.Operation==AssistantOperation.Count?0:1));}
        }
        private sealed class Model : IAssistantModel,IAssistantStreamingModel
        {
            internal int Interpretations,Streams;
            internal Action<string> Publish;
            internal TaskCompletionSource<string> Pending;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> c,AssistantInterpretationContext previous,CancellationToken ct)
            {Interpretations++;throw new InvalidOperationException("Synthetic offline model");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> a,CancellationToken ct) {return Task.FromResult("{}");}
            public Task<string> GeneralAsync(List<ChatMessage> history,string question,CancellationToken ct) {return Task.FromResult("Buffered answer.");}
            public async Task<string> GeneralStreamingAsync(List<ChatMessage> history,string question,CancellationToken ct,Action<string> partial)
            {
                Streams++;Publish=partial;var pending=new TaskCompletionSource<string>();Pending=pending;
                using(ct.Register(()=>pending.TrySetCanceled())) {partial("A draft about DNS");return await pending.Task;}
            }
        }
        private static YakumiAssistantService Service(Reader reader,Model model)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),model,_=>true);}
        public static string RunAll()
        {
            _checks=0;Caches().GetAwaiter().GetResult();Local().GetAwaiter().GetResult();Transport().GetAwaiter().GetResult();Conversation().GetAwaiter().GetResult();Interface();
            return _checks+" latency, reference-cache, streaming and cancellation checks passed.";
        }
        private static async Task Caches()
        {
            DateTime now=new DateTime(2026,10,17);var context=ExpandedAssistantTests.Context();int reads=0;
            var stock=new AssistantStockVocabulary((source,c,ct)=>
            {
                reads++;return Task.FromResult<IReadOnlyList<AssistantProductTerm>>(new[] {new AssistantProductTerm {Name="Printer",Model="LX310",Category=source=="items"?"Printer":"Cartridge"}});
            },()=>now);
            var report=await AssistantPerformance.TrackAsync(async ()=>
            {
                await stock.ResolveAsync("how many LX310 items in stock?",context,CancellationToken.None);
                await stock.ResolveAsync("how many LX310 cartridges in stock?",context,CancellationToken.None);
                var hint=await stock.ResolveAsync("how many LX-310 items in stock?",context,CancellationToken.None);
                Check(reads==2 && hint.Model.Key=="LX310" && hint.KnownTerms.Count>0,"switching stock sources reuses indexed reference names and model formats");
                return AssistantResult.Message(AssistantOutcome.Guidance,"Synthetic");
            });
            Check(report.Performance.Counters["stock-cache-hit"]==1 && report.Performance.Counters["stock-cache-miss"]==2,"cache diagnostics distinguish hits from reference reads");
            now=now.AddMinutes(6);await stock.ResolveAsync("how many LX310 items in stock?",context,CancellationToken.None);
            Check(reads==3,"reference vocabulary expires after five minutes");
            context.SessionStamp="another-account";await stock.ResolveAsync("how many LX310 items in stock?",context,CancellationToken.None);
            Check(reads==4,"account or permission changes invalidate reference data");
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new string[0];
            await stock.ResolveAsync("how many items in stock?",denied,CancellationToken.None);Check(reads==4,"denied catalogs do not read cache or database");

            int directories=0;var organizations=new[] {new AssistantOrganization("Company",1,"YMC"),new AssistantOrganization("Company",2,"YPI")};
            var directory=new AssistantOrganizationVocabulary((values,fields,ct)=> {directories++;return Task.FromResult<IReadOnlyList<AssistantOrganization>>(organizations.Where(o=>values.Contains(o.Name,StringComparer.OrdinalIgnoreCase)).ToArray());},()=>now);
            foreach(string q in new[] {"how many tickets in YMC?","how many tickets in YPI?","how many solved tickets in ymc this month?"})
            {
                var plan=AssistantPlanner.Plan(q,context);var resolved=await directory.ResolveAsync(q,plan.Requests,new[] {AssistantCatalog.Find("tickets")},context,CancellationToken.None);
                Check(resolved.Uncertain==null && resolved.Mentions.Single().Choices.Single().Id.HasValue,"organization cache keeps actual identity: "+q);
            }
            Check(directories==2,"alternating organization filters reuse more than the last lookup");
            foreach(string q in new[] {"tickets in Atlantis?","tickets in Atlantis?"})
            {var plan=AssistantPlanner.Plan(q,context);await directory.ResolveAsync(q,plan.Requests,new[] {AssistantCatalog.Find("tickets")},context,CancellationToken.None);}
            Check(directories==3,"empty reference lookups are cached without inventing an organization");

            int loads=0;var cache=new AssistantReferenceCache<int>(2,"test-cache",()=>now);
            Func<CancellationToken,Task<int>> load=ct=>Task.FromResult(++loads);
            await cache.GetAsync("account","A",load,CancellationToken.None);now=now.AddSeconds(1);
            await cache.GetAsync("account","B",load,CancellationToken.None);now=now.AddSeconds(1);
            await cache.GetAsync("account","A",load,CancellationToken.None);now=now.AddSeconds(1);
            await cache.GetAsync("account","C",load,CancellationToken.None);
            await cache.GetAsync("account","B",load,CancellationToken.None);
            Check(loads==4,"bounded cache evicts the least recently used entry");
            int failures=0;
            for(int i=0;i<2;i++) try {await cache.GetAsync("account","failed",ct=> {failures++;throw new IOException("offline");},CancellationToken.None);} catch(IOException) { }
            Check(failures==2,"failed reference reads never become cached empty results");
            var gate=new TaskCompletionSource<int>();int joined=0;
            Func<CancellationToken,Task<int>> delayed=ct=> {joined++;return gate.Task;};
            var first=cache.GetAsync("account","shared",delayed,CancellationToken.None);var second=cache.GetAsync("account","shared",delayed,CancellationToken.None);
            gate.SetResult(7);Check(await first==7 && await second==7 && joined==1,"simultaneous reference reads share a successful fill");
            using(var cancellation=new CancellationTokenSource())
            {
                bool cancelled=false;
                try {await cache.GetAsync("account","cancelled",ct=> {cancellation.Cancel();return Task.FromResult(9);},cancellation.Token);} catch(OperationCanceledException) {cancelled=true;}
                Check(cancelled && await cache.GetAsync("account","cancelled",load,CancellationToken.None)!=9,"cancelled fill is not published");
            }
        }
        private static async Task Local()
        {
            var reader=new Reader();var model=new Model();var service=Service(reader,model);var context=ExpandedAssistantTests.Context();
            foreach(string q in new[] {"Give me the number of tickets","Tell me the total count of open tickets","What’s the number of my requests?","What is the total number of tickets in company \"YMC\" this month?","Could you please count tickets?","Can you give me the total number of tickets?"})
            {
                var result=await service.AskAsync(q,context,CancellationToken.None);
                Check(result.IsLive && result.Request.Operation==AssistantOperation.Count && model.Interpretations==0,"common count phrasing avoids AI: "+q);
            }
            int before=reader.Calls;
            var uncertain=await service.AskAsync("Could you count mysteriously ready tickets?",context,CancellationToken.None);
            Check(uncertain.Outcome==AssistantOutcome.Clarification && reader.Calls==before && model.Interpretations==1,"unknown wording is not dropped to make local parsing succeed");
            var exclusion=await service.AskAsync("Could you count tickets except closed?",context,CancellationToken.None);
            Check(exclusion.Outcome==AssistantOutcome.Clarification && reader.Calls==before,"faster count normalization retains unsupported exclusion");
            var quoted=await service.AskAsync("count tickets named \"Give me the number of\"",context,CancellationToken.None);
            Check(quoted.IsLive && quoted.Request.Search.Contains("Give me the number of"),"count phrases inside quoted names remain literal");
            Check(OllamaChatService.ResponseBudget("What is RAM?")==160 && OllamaChatService.ResponseBudget("How do I troubleshoot a printer?")==384,"simple AI replies get a smaller generation budget while procedures retain room");
            Check((bool)JObject.Parse(OllamaChatService.BuildPayload("model",null,"What is RAM?",true))["stream"],"streaming payload keeps existing prompt boundaries");
        }
        private sealed class ControlledStream : Stream
        {
            private readonly byte[] _first=Encoding.UTF8.GetBytes("{\"message\":{\"role\":\"assistant\",\"content\":\"RAM \"},\"done\":false}\n");
            private readonly byte[] _last=Encoding.UTF8.GetBytes("{\"message\":{\"role\":\"assistant\",\"content\":\"stores working data.\"},\"done\":true}\n");
            internal readonly TaskCompletionSource<bool> Continue=new TaskCompletionSource<bool>();
            private int _part,_offset;
            public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
            public override long Length=>throw new NotSupportedException();public override long Position {get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override async Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken ct)
            {
                if(_part==1) await Continue.Task.ConfigureAwait(false);
                if(_part>=2) return 0;
                byte[] bytes=_part==0?_first:_last;int size=Math.Min(count,bytes.Length-_offset);Array.Copy(bytes,_offset,buffer,offset,size);_offset+=size;
                if(_offset==bytes.Length) {_part++;_offset=0;}return size;
            }
            protected override void Dispose(bool disposing) {Continue.TrySetCanceled();base.Dispose(disposing);}
            public override int Read(byte[] b,int o,int c)=>ReadAsync(b,o,c,CancellationToken.None).GetAwaiter().GetResult();
            public override void Flush() { } public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
            public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        }
        private sealed class Handler : HttpMessageHandler
        {
            internal string Payload;internal readonly ControlledStream Stream=new ControlledStream();
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            {Payload=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StreamContent(Stream)};}
        }
        private static async Task Transport()
        {
            using(var handler=new Handler()) using(var client=new HttpClient(handler))
            {
                var early=new TaskCompletionSource<string>();
                var turn=OllamaChatService.AskStreamingAsync(client,"http://synthetic.test","test",null,"What is RAM?",CancellationToken.None,t=>early.TrySetResult(t));
                Check(await early.Task=="RAM " && !turn.IsCompleted,"HTTP response starts appearing before generation finishes");
                handler.Stream.Continue.SetResult(true);Check(await turn=="RAM stores working data.","completed stream concatenates chunks exactly");
                Check((bool)JObject.Parse(handler.Payload)["stream"] && (int)JObject.Parse(handler.Payload)["options"]["num_predict"]==160,"transport requests streaming and a short reply budget");
            }
            foreach(string invalid in new[] {"{\"message\":{\"content\":\"unfinished\"},\"done\":false}\n","{\"error\":\"server error\"}\n","not json\n",new string('x',32769)})
            {
                bool rejected=false;using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(invalid)))
                    try {await OllamaChatService.ReadChatStreamAsync(stream,_=>{},CancellationToken.None);} catch(Exception ex) when(ex is InvalidDataException || ex is IOException || ex is Newtonsoft.Json.JsonException) {rejected=true;}
                Check(rejected,"incomplete, failed or oversized stream is rejected");
            }
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"message\":{\"content\":\"café\"},\"done\":true}")))
                Check(await OllamaChatService.ReadChatStreamAsync(stream,null,CancellationToken.None)=="café","buffered completion without trailing newline retains Unicode and legacy text output");
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"message\":{\"content\":\"Partial\"},\"done\":true,\"done_reason\":\"length\"}\n")))
                Check((await OllamaChatService.ReadChatStreamAsync(stream,null,CancellationToken.None)).Contains("length limit"),"generation limits are disclosed instead of presenting a truncated answer as complete");
            using(var stream=new ControlledStream()) using(var cancellation=new CancellationTokenSource())
            {
                var early=new TaskCompletionSource<bool>();var turn=OllamaChatService.ReadChatStreamAsync(stream,_=>early.TrySetResult(true),cancellation.Token);
                await early.Task;cancellation.Cancel();bool stopped=false;
                try {await turn;} catch(OperationCanceledException) {stopped=true;}
                Check(stopped,"Stop interrupts a stalled stream after the first text");
            }
        }
        private static async Task Conversation()
        {
            var reader=new Reader();var model=new Model();var service=Service(reader,model);var context=ExpandedAssistantTests.Context();string draft=null;
            var turn=service.AskAsync("What is DNS?",context,CancellationToken.None,null,t=>draft=t);
            Check(draft!=null && !turn.IsCompleted && reader.Calls==0,"general conversation streams without database rows");
            model.Pending.SetResult("DNS translates domain names.");var result=await turn;
            Check(result.Text=="DNS translates domain names." && result.Performance.Stages.ContainsKey("ai-general") && result.Performance.Stages.ContainsKey("first-text"),"completed result includes general and first-text timings");
            string log=result.Performance.LogLine(result.Outcome);
            Check(log.Contains("ai-general_ms=") && !log.Contains("DNS") && !log.Contains("domain"),"diagnostic log contains fixed metrics and no question or response text");
            draft=null;var guide=await service.AskAsync("How do I borrow equipment?",context,CancellationToken.None,null,t=>draft=t);
            Check(draft==null && guide.GuideCitations.Count>0,"guide JSON is validated before display and retains authored fallback");
            var first=await service.AskAsync("how many tickets?",context,CancellationToken.None,null,t=>draft=t);
            var second=await service.AskAsync("how many tickets?",context,CancellationToken.None,null,t=>draft=t);
            Check(first.TotalRecords==1 && second.TotalRecords==2 && model.Streams==1 && draft==null && second.Performance.Stages.ContainsKey("sql"),"factual totals remain fresh SQL results without model streaming");
            var pending=service.AskAsync("What is DNS?",context,CancellationToken.None,null,t=>draft=t);var publish=model.Publish;service.Reset();draft=null;publish("stale draft");model.Pending.SetResult("stale final");
            Check((await pending).Outcome==AssistantOutcome.Cancelled && draft==null,"new conversation rejects late streaming and final text");
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {if(root is T) yield return (T)root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Descendants<T>(child)) yield return item;}
        private static string Text(TextBlock block)
        {return new System.Windows.Documents.TextRange(block.ContentStart,block.ContentEnd).Text;}
        private static void Layout(YakumiChatView view)
        {view.Measure(new Size(350,540));view.Arrange(new Rect(0,0,350,540));view.UpdateLayout();view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);}
        private static void Pump(Task turn,YakumiChatView view)
        {var until=DateTime.UtcNow.AddSeconds(5);while(!turn.IsCompleted && DateTime.UtcNow<until) view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);Check(turn.IsCompleted,"streaming UI turn completes");turn.GetAwaiter().GetResult();Layout(view);}
        private static void Interface()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();
            using(var view=new YakumiChatView(()=>context,Service(reader,model)))
            {
                view.RefreshContext(context);var turn=view.SubmitAsync("What is DNS?");Layout(view);
                Check(Descendants<TextBlock>(view).Any(t=>t.Text=="A draft about DNS") && Descendants<Button>(view).Any(b=>Equals(b.ToolTip,"Stop reply")),"partial reply is visible with Stop available");
                model.Pending.SetResult("DNS translates domain names.");Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>Text(t)=="A draft about DNS") && Descendants<TextBlock>(view).Any(t=>Text(t)=="DNS translates domain names."),"final formatted answer replaces the plain draft");
                turn=view.SubmitAsync("What is DNS?");Layout(view);model.Pending.SetException(new IOException("Synthetic broken stream"));Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>Text(t)=="A draft about DNS") && Descendants<TextBlock>(view).Any(t=>Text(t).Contains("AI service")),"a broken stream is replaced with an unavailable message");
                turn=view.SubmitAsync("What is DNS?");Layout(view);var publish=model.Publish;view.Cancel();publish("late text");Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text=="late text") && Descendants<TextBlock>(view).Any(t=>t.Text=="Reply cancelled"),"cancelled reply replaces drafts and rejects queued chunks");
                turn=view.SubmitAsync("What is DNS?");publish=model.Publish;view.NewConversation();publish("old conversation");Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text=="old conversation"),"new chat prevents old chunks from entering the UI");
                turn=view.SubmitAsync("What is DNS?");publish=model.Publish;context.SessionStamp="changed-session";view.RefreshContext(context);publish("other account text");Pump(turn,view);
                Check(!Descendants<TextBlock>(view).Any(t=>t.Text=="other account text") && reader.Calls==0,"changed account rejects streaming drafts without any database lookup");
            }
        }
        public static string RenderPreviews(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach(double scale in new[] {1.0,1.25,1.5})
            foreach(string scenario in new[] {"streaming","completed","stopped"})
            {
                var context=ExpandedAssistantTests.Context();var model=new Model();
                using(var view=new YakumiChatView(()=>context,Service(new Reader(),model)))
                {
                    view.RefreshContext(context);var turn=view.SubmitAsync("What is DNS?");Layout(view);
                    if(scenario=="completed") {model.Pending.SetResult("DNS translates domain names into the addresses computers use to find websites.");Pump(turn,view);}
                    if(scenario=="stopped") {view.Cancel();Pump(turn,view);}
                    Capture(view,directory,scenario,scale);
                    if(!turn.IsCompleted) {view.Cancel();Pump(turn,view);}
                }
            }
            return "9 streaming, completed and stopped WPF previews rendered at 100%, 125% and 150%: "+directory;
        }
        private static void Capture(YakumiChatView view,string directory,string scenario,double scale)
        {
            var grid=new Grid {Width=390,Height=580,Background=System.Windows.Media.Brushes.White};
            view.HorizontalAlignment=HorizontalAlignment.Right;view.VerticalAlignment=VerticalAlignment.Bottom;view.Margin=new Thickness(20);grid.Children.Add(view);
            grid.Measure(new Size(390,580));grid.Arrange(new Rect(0,0,390,580));grid.UpdateLayout();view.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(390*scale),(int)(580*scale),96*scale,96*scale,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(grid);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(directory,scenario+"-"+(int)(scale*100)+".png"))) encoder.Save(stream);
            grid.Children.Clear();
        }
        public static async Task<string> LiveSmokeAsync(CancellationToken ct)
        {
            var clock=Stopwatch.StartNew();long first=-1;int updates=0,firstCharacters=0;
            string answer=await OllamaChatService.AskStreamingAsync(null,"What is RAM? Answer in one sentence.",ct,text=> {if(first<0) {first=clock.ElapsedMilliseconds;firstCharacters=text.Length;}updates++;}).ConfigureAwait(false);
            if(updates==0 || string.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException("No streaming reply was received.");
            return "Generic live AI streaming check passed: first_text_ms="+first+", complete_ms="+clock.ElapsedMilliseconds+", updates="+updates+", first_characters="+firstCharacters+", final_characters="+answer.Length+". No database was opened or business data sent.";
        }
    }
}
