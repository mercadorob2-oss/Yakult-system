using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Coordinates one read-only turn; keeps query context separate from model conversation.</summary>
    public sealed class YakumiAssistantService
    {
        private readonly AssistantQueryEngine _engine;
        private readonly IAssistantModel _model;
        private readonly Func<AssistantContext,bool> _isCurrent;
        private readonly IAssistantStockVocabulary _stockVocabulary;
        private readonly List<AssistantRequest> _previous=new List<AssistantRequest>();
        private readonly List<ChatMessage> _generalHistory=new List<ChatMessage>();
        private string _stamp;
        private int _generation;
        public YakumiAssistantService() : this(new AssistantQueryEngine(),new OllamaAssistantModel(),context=>
            AssistantContext.Capture(context.Module,context.Page,context.SelectedCapability,context.SelectedRecordId).SessionStamp==context.SessionStamp,new AssistantStockVocabulary()) { }
        internal YakumiAssistantService(AssistantQueryEngine engine,IAssistantModel model,Func<AssistantContext,bool> current,IAssistantStockVocabulary vocabulary=null)
        { _engine=engine;_model=model;_isCurrent=current;_stockVocabulary=vocabulary; }
        public void Reset() { _previous.Clear();_generalHistory.Clear();_stamp=null;Interlocked.Increment(ref _generation); }

        public async Task<AssistantResult> AskAsync(string question,AssistantContext context,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(!_isCurrent(context)) return ChangedContext();
            if(_stamp!=context.SessionStamp) { Reset();_stamp=context.SessionStamp; }
            int generation=_generation;
            AssistantPlan plan;
            try
            {
                AssistantStockHint stock=null;
                if(_stockVocabulary!=null && AssistantStockVocabulary.ShouldResolve(question))
                {
                    try {stock=await _stockVocabulary.ResolveAsync(question,context,ct);}
                    catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
                    catch { } // Known deterministic lookups still work if vocabulary refresh is unavailable.
                }
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                plan=AssistantPlanner.Plan(stock==null?question:stock.Rewrite(question),context,_previous.ToList());
                if(stock?.Category!=null && plan.Requests.Count==1 && plan.Requests[0].Capability=="items")
                {plan.Requests[0].Category=stock.Category;plan.Requests[0].ExactCategory=true;}
            }
            catch(ArgumentException ex) { return AssistantResult.Message(AssistantOutcome.Clarification,ex.Message); }
            if(plan.Message=="@session") return SessionProfile(context);
            if(plan.Message!=null)
            {
                _previous.Clear();
                var guides=plan.GuidanceQuestion==null?null:AssistantKnowledge.Search(plan.GuidanceQuestion,context);
                return AssistantResult.Message(AssistantOutcome.Clarification,plan.Message+(guides!=null && guides.Count>0?"\n\n"+AssistantKnowledge.FixedAnswer(guides):""));
            }
            if(plan.Requests.Count==1 && plan.Requests[0].Capability=="employees" && IsSelfSearch(plan.Requests[0].Search,context))
            { _previous.Clear();_generalHistory.Clear();return SessionProfile(context); }
            if(plan.Requests.Count==0 && plan.IsGeneralQuestion && AiIntentRouter.Has(question,@"\b(how many|how much|count|list|show|status|ilan|bilang)\b"))
            {
                var candidates=AssistantPlanner.InterpretationCandidates(question,context);
                try
                {
                    if(candidates.Count>0)
                    {
                        string json=await _model.InterpretAsync(question,candidates,ct);
                        var request=AssistantPlanner.InterpretJson(question,json,context,candidates,TimeZoneInfo.ConvertTime(DateTime.UtcNow,context.TimeZone).Date);
                        if(request!=null)
                        {
                            if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                            string title=AssistantCatalog.Find(request.Capability).Title;
                            return AssistantResult.Message(AssistantOutcome.Clarification,"Did you mean "+title+"? Please name that record type and its filters explicitly so I can run the correct read-only lookup.");
                        }
                    }
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
                catch { }
                if(plan.Requests.Count==0) return AssistantResult.Message(AssistantOutcome.Clarification,"Please name the records you want, such as tickets, items, requests, invoices or repair tickets. I could not safely interpret that lookup.");
            }
            if(plan.Requests.Count>0)
            {
                _generalHistory.Clear();var results=new List<AssistantResult>();
                foreach(var request in plan.Requests.Take(2))
                {
                    if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                    var result=await _engine.ExecuteAsync(request,context,ct);
                    ct.ThrowIfCancellationRequested();
                    if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                    results.Add(result);
                }
                _previous.Clear();
                if(results.Count==1 && results[0].IsLive) _previous.Add(results[0].Request.Copy());
                string explanation=plan.GuidanceQuestion==null?null:await ExplainAsync(plan.GuidanceQuestion,context,ct);
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                if(results.Count==1)
                {
                    if(explanation!=null) results[0].Text+="\n\n"+explanation;
                    return results[0];
                }
                var combined=AssistantResult.Message(results.All(r=>r.IsLive)?AssistantOutcome.Success:AssistantOutcome.Clarification,
                    string.Join("\n\n",results.Select((r,i)=>"Result "+(i+1)+"\n"+r.Text)));
                if(plan.Compare && results.Count==2 && results.All(r=>r.IsLive)) combined.Text+="\n\n"+Compare(results[0],results[1]);
                return combined;
            }
            _previous.Clear();
            if(plan.GuidanceQuestion!=null)
            {
                _generalHistory.Clear();
                string answer=await ExplainAsync(plan.GuidanceQuestion,context,ct);
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                return AssistantResult.Message(AssistantOutcome.Guidance,answer ?? "I do not have a verified guide answering that application question. Please specify the module or screen. I will not invent a workflow.");
            }
            try
            {
                string answer=await _model.GeneralAsync(_generalHistory,question,ct);
                ct.ThrowIfCancellationRequested();if(!_isCurrent(context) || generation!=_generation) return ChangedContext();
                if(string.IsNullOrWhiteSpace(answer)) return AssistantResult.Message(AssistantOutcome.Unavailable,"Ollama returned no answer. You can still use supported record lookups and application guides.");
                _generalHistory.Add(new ChatMessage("user",question));_generalHistory.Add(new ChatMessage("assistant",answer));
                if(_generalHistory.Count>12) _generalHistory.RemoveRange(0,_generalHistory.Count-12);
                return AssistantResult.Message(AssistantOutcome.Guidance,answer);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch { return AssistantResult.Message(AssistantOutcome.Unavailable,"Ollama is unavailable. Supported record lookups and authored application guides remain available."); }
        }

        private async Task<string> ExplainAsync(string question,AssistantContext context,CancellationToken ct)
        {
            var articles=AssistantKnowledge.Search(question,context);
            if(articles.Count==0) return null;
            try
            {
                var json=await _model.ExplainAsync(question,articles,ct);
                ct.ThrowIfCancellationRequested();
                string answer=OllamaAssistantModel.ValidateExplanation(json,articles);
                if(answer!=null) return answer;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch { }
            return AssistantKnowledge.FixedAnswer(articles);
        }
        private AssistantResult ChangedContext()
        { Reset();return AssistantResult.Message(AssistantOutcome.Cancelled,"Your account, permissions or database changed. The previous answer was discarded. Please ask again."); }
        internal static bool IsSelfSearch(string text,AssistantContext context)
        {
            if(context.IsDepartmentAccount || context.EmployeeId.GetValueOrDefault()<=0) return false;
            var search=AiIntentRouter.SearchTokens(text,AiContextProviders.EmployeeNoise);
            var own=AiIntentRouter.SearchTokens(context.EmployeeName);
            return search.Count>0 && search.All(s=>own.Contains(s,StringComparer.OrdinalIgnoreCase));
        }
        private static AssistantResult SessionProfile(AssistantContext context)
        {
            var text=new StringBuilder("Signed in as: "+(context.IsDepartmentAccount?context.UserName:context.EmployeeName ?? context.UserName)+".");
            if(context.IsDepartmentAccount) text.Append("\nThis is a department account, not an individual employee.");
            else
            {
                foreach(var item in new[] { Tuple.Create("Position",context.Position),Tuple.Create("Department",context.DepartmentName),Tuple.Create("Branch",context.BranchName),Tuple.Create("Company",context.CompanyName) })
                    if(!string.IsNullOrWhiteSpace(item.Item2)) text.Append("\n"+item.Item1+": "+AiContextProviders.Clean(item.Item2,160));
                if(context.EmployeeId.GetValueOrDefault()<=0) text.Append("\nNo linked employee record.");
            }
            text.Append("\nAccessible portals: "+string.Join(", ",context.Portals ?? new string[0])+".");
            text.Append("\nRoles: "+string.Join(", ",context.Roles ?? new string[0])+".");
            text.Append("\nPage and category restrictions still apply. Source: current signed-in session; not a fresh database lookup.");
            return AssistantResult.Message(AssistantOutcome.Guidance,text.ToString());
        }
        internal static string Compare(AssistantResult first,AssistantResult second)
        {
            if(first.Request.Capability!=second.Request.Capability) return "The sources measure different records; their results are shown separately without subtracting unlike quantities.";
            var text=new StringBuilder("Comparison (result 2 minus result 1): matching records "+(second.TotalRecords-first.TotalRecords).ToString("+0;-0;0")+".");
            foreach(string metric in first.Totals.Keys.Intersect(second.Totals.Keys))
                text.Append("\n"+AssistantQueryEngine.MetricLabel(metric,first.Request.Capability)+" difference: "+AssistantQueryEngine.FormatNumber(metric,second.Totals[metric]-first.Totals[metric])+".");
            return text.ToString();
        }
    }
}
