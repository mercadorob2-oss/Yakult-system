using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>Permitted synthetic catalog, query parameters and confirmation lifecycle; no live database.</summary>
    public static class AssistantProductMatchingTests
    {
        private static int _checks;
        private static void Check(bool value,string message)
        {if(!value) throw new InvalidOperationException("Yakumi product matching: "+message);_checks++;}
        private static readonly AssistantProductTerm[] Products={
            new AssistantProductTerm {Name="Epson printer",Model="LX310",Category="Printer"},
            new AssistantProductTerm {Name="Epson printer",Model="LX-310",Category="Printer"},
            new AssistantProductTerm {Name="Epson printer",Model="LX 310",Category="Printer"},
            new AssistantProductTerm {Name="Epson printer",Model="LX350",Category="Printer"},
            new AssistantProductTerm {Name="Epson printer",Model="LX310A",Category="Printer"},
            new AssistantProductTerm {Name="Logitech keyboard",Model="K120",Category="Keyboard"},
            new AssistantProductTerm {Name="Epson cartridge",Model="CF230A",Category="Cartridge"}
        };
        private sealed class Vocabulary : IAssistantStockVocabulary
        {
            internal bool Unavailable;
            internal Action OnRead;
            public Task<AssistantStockHint> ResolveAsync(string q,AssistantContext context,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();OnRead?.Invoke();
                if(Unavailable) throw new InvalidOperationException("Synthetic catalog unavailable");
                string source=AssistantPlanner.BestCapability(q)?.Id ?? "items";
                if(!AssistantCatalog.Find(source).IsAllowed(context)) return Task.FromResult<AssistantStockHint>(null);
                var permitted=Products.Where(p=>!(context.DeniedCategories ?? new string[0]).Contains(p.Category,StringComparer.OrdinalIgnoreCase)).ToArray();
                var hint=AssistantStockVocabulary.Match(q,permitted) ?? new AssistantStockHint {Route=false};
                hint.Products=permitted;hint.Capability=source;
                if(source!="items") {hint.Route=false;hint.Category=null;hint.Phrase=null;}
                return Task.FromResult(hint);
            }
        }
        private sealed class Model : IAssistantModel
        {
            internal int Calls;
            public Task<string> InterpretAsync(string q,IReadOnlyList<AssistantCapability> candidates,AssistantInterpretationContext context,CancellationToken ct)
            {Calls++;throw new InvalidOperationException("Synthetic AI unavailable");}
            public Task<string> ExplainAsync(string q,IReadOnlyList<AssistantArticle> articles,CancellationToken ct) {throw new InvalidOperationException("Synthetic AI unavailable");}
            public Task<string> GeneralAsync(List<ChatMessage> history,string q,CancellationToken ct) {throw new InvalidOperationException("Synthetic AI unavailable");}
        }
        private sealed class Reader : IAssistantDataReader
        {
            internal readonly List<AssistantSqlQuery> Queries=new List<AssistantSqlQuery>();
            internal bool Unavailable;
            internal decimal Adjustment;
            public Task<DataTable> ReadAsync(AssistantSqlQuery query,CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();Queries.Add(query);
                if(Unavailable) throw new InvalidOperationException("Synthetic database unavailable");
                // Three stored spellings of one model, a different model and a suffix model.
                var rows=new[] {
                    new {Name="Epson printer",Model="LX310",Key="LX310",Units=100m},
                    new {Name="Epson printer",Model="LX-310",Key="LX310",Units=100m},
                    new {Name="Epson printer",Model="LX 310",Key="LX310",Units=54m+Adjustment},
                    new {Name="Epson printer",Model="LX350",Key="LX350",Units=9m},
                    new {Name="Epson printer",Model="LX310A",Key="LX310A",Units=7m}
                }.AsEnumerable();
                var keys=query.Parameters.Values.OfType<string>().Where(v=>new[] {"LX310","LX350","LX310A"}.Contains(v)).ToList();
                if(keys.Count>0) rows=rows.Where(r=>keys.Contains(r.Key));
                foreach(string parameter in query.Parameters.Values.OfType<string>().Where(v=>v.StartsWith("%") && v.EndsWith("%")))
                {
                    string term=parameter.Trim('%');
                    rows=rows.Where(r=>(r.Name+" "+r.Model+" Printer").IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0);
                }
                var matches=rows.ToArray();
                return Task.FromResult(ExpandedAssistantTests.Fixture(query,matches.LongLength,matches.Sum(r=>r.Units),query.Request.Operation==AssistantOperation.Count?0:matches.Length));
            }
        }
        private static YakumiAssistantService Service(Reader reader,Vocabulary vocabulary=null,Model model=null,Func<AssistantContext,bool> current=null)
        {return new YakumiAssistantService(new AssistantQueryEngine(reader),model ?? new Model(),current ?? (_=>true),vocabulary ?? new Vocabulary());}
        private static AssistantClarificationChoice Choice(AssistantResult result,string label)
        {return result.ClarificationChoices.Single(c=>c.Label==label);}
        public static string RunAll()
        {
            _checks=0;Matching();Queries().GetAwaiter().GetResult();Suggestions().GetAwaiter().GetResult();Boundaries().GetAwaiter().GetResult();CatalogCommands();Buttons();
            return _checks+" catalog model and typo confirmation checks passed.";
        }
        private static void Matching()
        {
            foreach(string variant in new[] {"LX310","lx-310","LX 310","LX - 310","Lx\t310"})
            {
                var match=AssistantProductMatching.MatchModel("how many "+variant+" in stock?",Products);
                Check(match?.Key=="LX310","full catalog model formatting: "+variant);
            }
            foreach(string name in new[] {"LX310A","LX3100"})
                Check(AssistantProductMatching.MatchModel(name,Products)?.Key!="LX310","suffix and digits never dropped: "+name);
            foreach(string literal in new[] {"\"LX-310\"","'Epsno LX310'","\"Still Open\""})
            {
                Check(AssistantProductMatching.MatchModel(literal,Products)==null,"quoted name remains literal");
                Check(AssistantProductMatching.Suggest(literal,new AssistantRequest {Capability="items",Search=literal},Products)==null,"quoted name has no typo rewrite");
            }
            Check(AssistantProductMatching.MatchModel("LX310 and LX350",Products)==null,"two different models are not collapsed");
            Check(AssistantProductMatching.MatchModel("AB-12/3",Products)==null,"unlisted punctuation has no equivalence");
            Check(AssistantProductMatching.Suggest("LX999",new AssistantRequest {Capability="items",Search="LX999"},Products)==null,"distant unknown product has no invented suggestion");
            Check(AssistantProductMatching.Suggest("LX350",new AssistantRequest {Capability="items",Search="LX350"},Products)==null,"exact catalog model is never corrected to its neighbor");
            Check(AssistantProductMatching.Suggest("LX310",new AssistantRequest {Capability="tickets",Search="LX310"},Products)==null,"ticket search keeps its own semantics");
        }
        private static async Task Queries()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var model=new Model();var service=Service(reader,model:model);
            AssistantSqlQuery baseline=null;
            foreach(string question in new[] {"how many lx310 are still in stock","how many lx310 in stock?","how many LX-310 in stock?","how many LX 310 are currently available?"})
            {
                var result=await service.AskAsync(question,context,CancellationToken.None);var query=reader.Queries.Last();
                Check(result.IsLive && result.Totals["Units"]==254m && result.TotalRecords==3,"all equivalent questions return fresh synthetic 254 across three stored spellings");
                Check(result.Request.ModelMatch?.Key=="LX310" && result.Request.Search=="lx310","canonical typed filter and search are identical");
                Check(query.CommandText.Contains("UPPER(REPLACE") && query.Parameters.Values.Contains("LX310") && !query.Parameters.Values.Contains("%lx310%"),"exact model uses parameters and no contradictory LIKE filter");
                if(baseline!=null) Check(query.CommandText==baseline.CommandText && query.Parameters.OrderBy(p=>p.Key).SequenceEqual(baseline.Parameters.OrderBy(p=>p.Key)),"identical SQL and parameters for equivalent questions");
                baseline=query;
            }
            Check(model.Calls==0,"clear catalog model questions need no AI");
            reader.Adjustment=1;var fresh=await service.AskAsync("how many LX 310 in stock?",context,CancellationToken.None);
            Check(fresh.Totals["Units"]==255m,"routing cache never caches quantities");reader.Adjustment=0;
            var list=fresh.Request.Copy();list.Operation=AssistantOperation.List;
            Check((await service.ExecuteRequestAsync(list,context,CancellationToken.None)).Request.ModelMatch?.Key=="LX310","Show records retains immutable model evidence");
            var result2=await service.AskAsync("how many Canon LX 310 printers condition Used this month?",context,CancellationToken.None);
            var filtered=reader.Queries.Last();
            Check(result2.Outcome==AssistantOutcome.NoMatches && filtered.Parameters.Values.Contains("%canon%") && string.Equals(result2.Request.Condition,"Used",StringComparison.OrdinalIgnoreCase) && result2.Request.FromLocal.HasValue,"brand, condition and date retained with a truthful zero");
            Check(!filtered.Parameters.Values.Contains("%310%") && !filtered.Parameters.Values.Contains("%lx%"),"model fragments do not leak into text filters");
            await service.AskAsync("how many Epson LX 310 printers condition Used this month?",context,CancellationToken.None);
            var follow=await service.AskAsync("only last month",context,CancellationToken.None);
            Check(follow.IsLive && follow.Request.ModelMatch?.Key=="LX310" && follow.Request.Search.Contains("epson") && string.Equals(follow.Request.Condition,"Used",StringComparison.OrdinalIgnoreCase),"period refinement preserves brand, model and condition");
            follow=await service.AskAsync("what about LX 350?",context,CancellationToken.None);
            Check(follow.IsLive && follow.Request.ModelMatch?.Key=="LX350" && !follow.Request.Search.Contains("310") && string.Equals(follow.Request.Condition,"Used",StringComparison.OrdinalIgnoreCase) && follow.Request.FromLocal.HasValue,"model replacement clears old model filter and keeps compatible scope");
            follow=await service.AskAsync("show tickets",context,CancellationToken.None);
            Check(follow.IsLive && follow.Request.ModelMatch==null && !follow.Request.InStockOnly,"source change clears model and stock filters");
            foreach(string source in new[] {"consumables","cartridges"})
            {
                var stock=await service.AskAsync("how many CF 230A "+source+" in stock?",context,CancellationToken.None);
                Check(stock.IsLive && stock.Request.Capability==source && stock.Request.ModelMatch?.Key=="CF230A","formatting supported in "+source);
            }
            var suffix=await service.AskAsync("how many LX310A in stock?",context,CancellationToken.None);
            Check(suffix.Totals["Units"]==7m && suffix.TotalRecords==1,"suffix model is queried independently");
            suffix=await service.AskAsync("how many LX 310 A in stock?",context,CancellationToken.None);
            Check(suffix.IsLive && suffix.Request.ModelMatch?.Key=="LX310A" && suffix.Totals["Units"]==7m,"spacing preserves a known suffix model");
            int queries=reader.Queries.Count;
            suffix=await service.AskAsync("how many LX 310 B in stock?",context,CancellationToken.None);
            Check(suffix.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==queries,"an unknown one-letter suffix cannot be discarded to match the base model");
            suffix=await service.AskAsync("how many items named \"LX 310 B\" in stock?",context,CancellationToken.None);
            Check(suffix.Outcome==AssistantOutcome.NoMatches && reader.Queries.Last().Parameters.Values.Contains("%B%"),"quoted unknown model retains the suffix in its truthful zero lookup");
        }
        private static async Task Suggestions()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var service=Service(reader);
            var result=await service.AskAsync("how many Epson LX301 printers condition Used this month?",context,CancellationToken.None);
            Check(result.Outcome==AssistantOutcome.Clarification && reader.Queries.Count==0,"transposed model needs confirmation before query");
            Check(result.ClarificationChoices.Select(c=>c.Label).SequenceEqual(new[] {"LX310","Keep original search"}),"catalog suggestion plus explicit original search");
            Check(result.Text.Contains("Did you mean") && AssistantPresentationFormatter.Format(result).CopyText.Contains("LX310 · Keep original search"),"legacy and Copy output explain typo choices");
            var answer=await service.ResolveClarificationAsync(Choice(result,"LX310"),context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.ModelMatch?.Key=="LX310" && string.Equals(answer.Request.Condition,"Used",StringComparison.OrdinalIgnoreCase) && answer.Request.FromLocal.HasValue && answer.Request.Category=="Printer" && answer.Request.Search.Contains("epson"),"confirmed correction preserves all meaningful filters");
            result=await service.AskAsync("how many LX 301 in stock?",context,CancellationToken.None);
            Check(result.ClarificationChoices.Any(c=>c.Label=="LX310"),"spaced model typo also asks for confirmation");
            result=await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None);
            answer=await service.ResolveClarificationAsync(Choice(result,"Keep original search"),context,CancellationToken.None);
            Check(answer.Outcome==AssistantOutcome.NoMatches && answer.Request.Search=="lx301" && answer.Request.ModelMatch==null,"declined correction returns true zero without retry or broadening");
            int before=reader.Queries.Count;await service.ResolveClarificationAsync(Choice(result,"Keep original search"),context,CancellationToken.None);
            Check(reader.Queries.Count==before,"product choice consumed once");
            await service.AskAsync("how many Epsno LX310 printers?",context,CancellationToken.None);
            answer=await service.AskAsync("Epson!",context,CancellationToken.None);
            Check(answer.IsLive && answer.Request.Search.Contains("epson") && answer.Request.ModelMatch?.Key=="LX310","typed catalog name choice resolves original question");
            result=await service.AskAsync("how many LX330 in stock?",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==3 && result.ClarificationChoices.Any(c=>c.Label=="LX310") && result.ClarificationChoices.Any(c=>c.Label=="LX350"),"two plausible models are offered without choosing one");
            result=await service.AskAsync("how many LX301 except used in stock?",context,CancellationToken.None);
            Check(result.ClarificationChoices.Count==0,"unsupported exclusion cannot be bypassed by correction");
        }
        private static async Task Boundaries()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();var vocabulary=new Vocabulary();var service=Service(reader,vocabulary);
            var result=await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None);var stale=Choice(result,"LX310");
            await service.AskAsync("how many LX350 in stock?",context,CancellationToken.None);int before=reader.Queries.Count;
            Check((await service.ResolveClarificationAsync(stale,context,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled && reader.Queries.Count==before,"new question expires product suggestions");
            result=await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None);stale=Choice(result,"LX310");service.Reset();
            Check((await service.ResolveClarificationAsync(stale,context,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled,"new chat expires product suggestions");
            result=await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None);stale=Choice(result,"LX310");
            var changed=ExpandedAssistantTests.Context();changed.SessionStamp="another-session";
            Check((await service.ResolveClarificationAsync(stale,changed,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled,"session change expires suggestions");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();bool stopped=false;
                try {await service.ResolveClarificationAsync(stale,context,cancelled.Token);} catch(OperationCanceledException) {stopped=true;}
                Check(stopped,"cancelled correction cannot query");
            }
            var denied=ExpandedAssistantTests.Context();denied.DeniedCategories=new[] {"Printer"};
            var hidden=await Service(new Reader()).AskAsync("how many LX301 in stock?",denied,CancellationToken.None);
            Check(hidden.ClarificationChoices.All(c=>c.Label!="LX310"),"denied catalog categories cannot leak suggestions");
            var permissionReader=new Reader();var permissionService=Service(permissionReader);
            result=await permissionService.AskAsync("how many LX301 in stock?",context,CancellationToken.None);
            var revoked=ExpandedAssistantTests.Context();revoked.IsDeveloper=false;revoked.IsSuperAdmin=false;revoked.Portals=new string[0];
            var deniedChoice=await permissionService.ResolveClarificationAsync(Choice(result,"LX310"),revoked,CancellationToken.None);
            Check(!deniedChoice.IsLive && permissionReader.Queries.Count==0,"correction rechecks access before querying even if a caller reuses a stamp");
            vocabulary.Unavailable=true;
            var exact=await service.AskAsync("how many LX310 in stock?",context,CancellationToken.None);
            Check(exact.IsLive && exact.Request.ModelMatch==null,"catalog outage leaves deterministic exact lookup available");
            var typo=await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None);
            Check(typo.Outcome==AssistantOutcome.NoMatches && typo.ClarificationChoices.Count==0,"catalog outage invents no correction");
            vocabulary.Unavailable=false;reader.Unavailable=true;
            Check((await service.AskAsync("how many LX 310 in stock?",context,CancellationToken.None)).Outcome==AssistantOutcome.Unavailable,"database outage never becomes zero stock");
            service=Service(new Reader(),vocabulary);vocabulary.OnRead=service.Reset;
            Check((await service.AskAsync("how many LX301 in stock?",context,CancellationToken.None)).Outcome==AssistantOutcome.Cancelled,"reset during vocabulary read discards corrections");
            using(var cancellation=new CancellationTokenSource())
            {
                vocabulary.OnRead=cancellation.Cancel;service=Service(new Reader(),vocabulary);bool stopped=false;
                try {await service.AskAsync("how many LX301 in stock?",context,cancellation.Token);} catch(OperationCanceledException) {stopped=true;}
                Check(stopped,"cancellation during catalog matching discards correction choices");
            }
        }
        private static void CatalogCommands()
        {
            var context=ExpandedAssistantTests.Context();context.DeniedCategories=new[] {"Printer'_%"};
            foreach(string source in new[] {"items","consumables","cartridges"})
            using(var command=AssistantStockVocabulary.CreateVocabularyCommand(source,context))
            {
                Check(command.CommandText.Contains("TOP (10000)") && !command.CommandText.Contains("StockOnHand"),"bounded catalog query excludes quantities: "+source);
                Check(command.Parameters.Count==1 && command.Parameters[0].Value.Equals("Printer'_%") && !command.CommandText.Contains("Printer'_%"),"category permissions parameterized: "+source);
            }
            var denied=ExpandedAssistantTests.Context();denied.IsDeveloper=false;denied.IsSuperAdmin=false;denied.Portals=new string[0];
            bool rejected=false;
            try {using(var command=AssistantStockVocabulary.CreateVocabularyCommand("items",denied)) { }} catch(UnauthorizedAccessException) {rejected=true;}
            Check(rejected,"catalog names require source access before SQL");
            foreach(var request in new[] {
                new AssistantRequest {Capability="tickets",Search="LX310",ModelMatch=new AssistantModelMatch("LX310","LX310")},
                new AssistantRequest {Capability="items",Search="LX310",ModelMatch=new AssistantModelMatch("LX350","LX350")},
                new AssistantRequest {Capability="items",Search="LX310",ModelMatch=new AssistantModelMatch(null,"LX310")},
                new AssistantRequest {Capability="items",Search="LX310",ModelMatch=new AssistantModelMatch("LX310' OR 1=1","LX310")}
            })
            {
                rejected=false;try {AssistantQueryEngine.BuildQuery(request,context);} catch(ArgumentException) {rejected=true;}
                Check(rejected,"unsupported or ungrounded typed model is rejected");
            }
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {
            if(root is T) yield return (T)root;
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Descendants<T>(child)) yield return item;
        }
        private static void Buttons()
        {
            var context=ExpandedAssistantTests.Context();var reader=new Reader();
            using(var view=new YakumiChatView(()=>context,Service(reader)))
            {
                view.SubmitAsync("how many LX330 in stock?").GetAwaiter().GetResult();
                var buttons=Descendants<Button>(view).Where(b=>b.Tag is AssistantClarificationChoice).ToList();
                Check(buttons.Count==3 && reader.Queries.Count==0,"native product choices wait for a decision");
                buttons.Single(b=>Equals(b.Content,"LX310")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(reader.Queries.Count==1 && reader.Queries[0].Request.ModelMatch?.Key=="LX310" && buttons.All(b=>!b.IsEnabled),"native product button applies one fresh filtered lookup and disables choices");
            }
        }
        internal static YakumiAssistantService PreviewService() {return Service(new Reader());}
        public static Dictionary<string,string> SqlCases()
        {
            var queries=new[] {"items","consumables","cartridges"}.ToDictionary(s=>"catalog model "+s,s=>
                AssistantQueryEngine.BuildQuery(new AssistantRequest {Capability=s,Operation=AssistantOperation.Count,Search="Epson LX310",InStockOnly=true,
                    ModelMatch=new AssistantModelMatch("LX310","LX 310")},ExpandedAssistantTests.Context()).CommandText);
            var context=ExpandedAssistantTests.Context();context.DeniedCategories=new[] {"Printer","Cartridge"};
            foreach(string source in new[] {"items","consumables","cartridges"})
                using(var command=AssistantStockVocabulary.CreateVocabularyCommand(source,context)) queries.Add("catalog vocabulary "+source,command.CommandText);
            return queries;
        }
    }
}
