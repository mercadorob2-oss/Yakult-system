using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Coordinates one read-only turn; keeps query context separate from model conversation.</summary>
    public sealed partial class YakumiAssistantService
    {
        private readonly AssistantQueryEngine _engine;
        private readonly IAssistantModel _model;
        private readonly Func<AssistantContext,bool> _isCurrent;
        private readonly IAssistantStockVocabulary _stockVocabulary;
        private readonly IAssistantOrganizationVocabulary _organizationVocabulary;
        private readonly List<AssistantRequest> _previous=new List<AssistantRequest>();
        private readonly List<ChatMessage> _generalHistory=new List<ChatMessage>();
        private string _stamp;
        private int _generation;
        public YakumiAssistantService() : this(new AssistantQueryEngine(),new OllamaAssistantModel(),context=>
            AssistantContext.Capture(context.Module,context.Page,context.SelectedCapability,context.SelectedRecordId).SessionStamp==context.SessionStamp,new AssistantStockVocabulary(),new AssistantOrganizationVocabulary()) { }
        internal YakumiAssistantService(AssistantQueryEngine engine,IAssistantModel model,Func<AssistantContext,bool> current,IAssistantStockVocabulary vocabulary=null,IAssistantOrganizationVocabulary organizations=null)
        { _engine=engine;_model=model;_isCurrent=current;_stockVocabulary=vocabulary;_organizationVocabulary=organizations; }
        public void Reset() { _previous.Clear();_generalHistory.Clear();_pendingClarification=null;_stamp=null;Interlocked.Increment(ref _generation); }

        /// <summary>Executes a copied follow-up through the same permission and session boundary as a question.</summary>
        public Task<AssistantResult> ExecuteRequestAsync(AssistantRequest request,AssistantContext context,CancellationToken ct)
        {return ExecuteRequestAsync(request,context,ct,null);}
        internal Task<AssistantResult> ExecuteRequestAsync(AssistantRequest request,AssistantContext context,CancellationToken ct,Action<string> searchProgress)
        {return AssistantPerformance.TrackAsync(()=>ExecuteCoreAsync(request,context,ct,searchProgress));}
        private async Task<AssistantResult> ExecuteCoreAsync(AssistantRequest request,AssistantContext context,CancellationToken ct,Action<string> searchProgress)
        {
            ct.ThrowIfCancellationRequested();
            _pendingClarification=null;
            if(!_isCurrent(context)) return ChangedContext();
            if(_stamp!=context.SessionStamp) {Reset();_stamp=context.SessionStamp;}
            int generation=_generation;
            var snapshot=request?.Copy();
            var result=await _engine.ExecuteAsync(snapshot,context,ct,r=>ReportSearch(r,context,generation,searchProgress));
            ct.ThrowIfCancellationRequested();
            if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
            _generalHistory.Clear();_previous.Clear();
            if(result.IsLive && result.Request!=null) _previous.Add(result.Request.Copy());
            return result;
        }

        public Task<AssistantResult> AskAsync(string question,AssistantContext context,CancellationToken ct)
        {return AskAsync(question,context,ct,null);}
        internal Task<AssistantResult> AskAsync(string question,AssistantContext context,CancellationToken ct,Action<string> searchProgress,Action<string> partial=null)
        {return AssistantPerformance.TrackAsync(()=>AskTurnAsync(question,context,ct,searchProgress,partial));}
        private Task<AssistantResult> AskTurnAsync(string question,AssistantContext context,CancellationToken ct,Action<string> searchProgress,Action<string> partial)
        {
            var choice=MatchClarificationReply(question,context);
            if(choice!=null) return ResolveClarificationAsync(choice,context,ct,searchProgress);
            string refinement;choice=MatchPendingResult(question,context,out refinement);
            return choice!=null && refinement.Length>0?RefinePendingResultAsync(choice,refinement,context,ct,searchProgress):AskCoreAsync(question,context,ct,null,searchProgress,partial:partial);
        }

        private bool ReportSearch(AssistantRequest request,AssistantContext context,int generation,Action<string> progress,string prefix="")
        {
            if(!_isCurrent(context) || generation!=_generation) return false;
            progress?.Invoke(prefix+AssistantSearchDescription.Describe(request,context));
            return _isCurrent(context) && generation==_generation;
        }
        private async Task<AssistantResult> AskCoreAsync(string question,AssistantContext context,CancellationToken ct,IReadOnlyList<AssistantRequest> previousOverride=null,Action<string> searchProgress=null,AssistantOrganizationResolution organizationOverride=null,Action<string> partial=null)
        {
            ct.ThrowIfCancellationRequested();
            _pendingClarification=null;
            if(!_isCurrent(context)) return ChangedContext();
            if(_stamp!=context.SessionStamp) { Reset();_stamp=context.SessionStamp; }
            int generation=_generation;
            string smallTalk=AssistantEnglish.SmallTalk(question);
            if(context.IsLoggedIn && smallTalk!=null) return AssistantResult.Message(AssistantOutcome.Guidance,smallTalk);
            AssistantPlan plan;
            var previous=(previousOverride ?? _previous).Select(r=>r.Copy()).Take(2).ToList().AsReadOnly();
            var follow=AssistantFollowUpLanguage.Resolve(question,previous);
            var planningPrevious=follow.Previous;
            string planningQuestion=follow.Message==null?follow.Question:question;
            bool stockFollowUp=planningPrevious.Count==1 && AssistantProductMatching.IsStock(planningPrevious[0].Capability) &&
                AssistantQuestionLanguage.IsFollowUp(planningQuestion) && AssistantPlanner.BestCapability(planningQuestion)==null;
            AssistantStockHint stock=null;
            try
            {
                string vocabularyQuestion=stockFollowUp?planningPrevious[0].Capability+" "+planningQuestion:planningQuestion;
                if(follow.Message==null && _stockVocabulary!=null && AssistantStockVocabulary.ShouldResolve(vocabularyQuestion) &&
                    !(previous.Count>0 && !stockFollowUp && AssistantQuestionLanguage.IsFollowUp(question) && AssistantPlanner.BestCapability(question)==null))
                {
                    try {stock=await AssistantPerformance.TimeAsync("stock-reference",()=>_stockVocabulary.ResolveAsync(vocabularyQuestion,context,ct));}
                    catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
                    catch { } // Known deterministic lookups still work if vocabulary refresh is unavailable.
                }
                ct.ThrowIfCancellationRequested();
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                string interpretedQuestion=stock==null?planningQuestion:stockFollowUp?AssistantProductMatching.RewriteModel(planningQuestion,stock.Model):stock.Rewrite(planningQuestion);
                using(AssistantPerformance.Measure("planning")) plan=follow.Message==null?AssistantPlanner.Plan(interpretedQuestion,context,planningPrevious):new AssistantPlan {Message=follow.Message};
                if(stock?.Category!=null && plan.Requests.Count==1 && plan.Requests[0].Capability=="items")
                {plan.Requests[0].Category=stock.Category;plan.Requests[0].ExactCategory=true;}
                if(stock!=null)
                {
                    plan.NeedsInterpretation=false;plan.UnresolvedMeaning=null;
                    var inherited=planningPrevious.Count==1 && plan.Requests.Count==1 && planningPrevious[0].Capability==plan.Requests[0].Capability &&
                        planningPrevious[0].Search==plan.Requests[0].Search?AssistantQuestionLanguage.SearchTerms(planningPrevious[0].Search):new List<string>();
                    AssistantPlanner.AssessMeaning(plan,interpretedQuestion,stock.KnownTerms.Concat(inherited));
                }
                AssistantProductMatching.Apply(plan,stock);
                string unresolvedModel=AssistantProductMatching.UnresolvedSuffix(question,stock?.Model);
                if(unresolvedModel!=null && plan.Message==null)
                {
                    plan.Message="Which model did you mean by “"+unresolvedModel+"”? Please put the full model in quotes.";
                    plan.UnresolvedFilter="model";
                }
            }
            catch(ArgumentException ex) { return AssistantResult.Message(AssistantOutcome.Clarification,ex.Message); }
            AssistantOrganizationResolution organizations=organizationOverride;
            if(plan.Message==null && plan.GuidanceQuestion==null && (_organizationVocabulary!=null || organizations!=null))
            {
                try
                {
                    var sources=plan.Requests.Count>0?plan.Requests.Select(r=>AssistantCatalog.Find(r.Capability)).Where(c=>c!=null).ToList().AsReadOnly():AssistantPlanner.InterpretationCandidates(question,context);
                    if(organizations==null) organizations=await AssistantPerformance.TimeAsync("organization-reference",()=>_organizationVocabulary.ResolveAsync(question,plan.Requests,sources,context,ct));
                    ct.ThrowIfCancellationRequested();
                    if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                    if(organizations?.Uncertain!=null) return ClarifyOrganization(organizations,context,previous);
                    if(organizations!=null && organizations.Mentions.Count>0)
                    {
                        using(AssistantPerformance.Measure("planning")) plan=AssistantPlanner.Plan(organizations.Rewrite(question),context,previous);
                        foreach(var request in plan.Requests) organizations.Apply(request);
                        AssistantProductMatching.Apply(plan,stock);
                    }
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
                catch(ArgumentException ex) {return AssistantResult.Message(AssistantOutcome.Clarification,ex.Message);}
                catch
                {return AssistantResult.Message(AssistantOutcome.Unavailable,"I couldn’t verify the company, branch or department filter just now. Please try again; I don’t have a verified count to show.");}
            }
            if(plan.Message=="@session") return SessionProfile(context);
            if(plan.Message!=null)
            {
                _previous.Clear();
                var guides=plan.GuidanceQuestion==null?null:AssistantKnowledge.Search(plan.GuidanceQuestion,context);
                var clarification=Clarify(question,plan,context,previous);
                if(guides!=null && guides.Count>0) AttachExplanation(clarification,FixedExplanation(guides));
                return clarification;
            }
            var productClarification=ClarifyProduct(question,plan,stock,context,previous);
            if(productClarification!=null) return productClarification;
            if(plan.Requests.Count==1 && plan.Requests[0].Capability=="employees" && IsSelfSearch(plan.Requests[0].Search,context))
            { _previous.Clear();_generalHistory.Clear();return SessionProfile(context); }
            if(plan.NeedsInterpretation)
            {
                var localPlan=plan;
                var candidates=previous.Select(r=>AssistantCatalog.Find(r.Capability)).Where(c=>c!=null && c.IsAllowed(context))
                    .Concat(AssistantPlanner.InterpretationCandidates(question,context)).GroupBy(c=>c.Id).Select(g=>g.First()).Take(6).ToList().AsReadOnly();
                try
                {
                    if(candidates.Count>0)
                    {
                        string json=await AssistantPerformance.TimeAsync("ai-interpretation",()=>_model.InterpretAsync(question,candidates,new AssistantInterpretationContext(previous,organizations),ct));
                        ct.ThrowIfCancellationRequested();
                        if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                        plan=AssistantInterpretation.Validate(question,json,context,candidates,
                            TimeZoneInfo.ConvertTime(DateTime.UtcNow,context.TimeZone).Date,new AssistantInterpretationContext(previous,organizations));
                        if(stock?.Category!=null && plan.Requests.Count==1 && plan.Requests[0].Capability=="items")
                        {plan.Requests[0].Category=stock.Category;plan.Requests[0].ExactCategory=true;}
                        AssistantProductMatching.Apply(plan,stock);
                    }
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
                catch { }
                ct.ThrowIfCancellationRequested();
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                if(plan.NeedsInterpretation || plan.Message!=null || plan.Requests.Count==0)
                    return Clarify(question,localPlan,context,previous);
            }
            if(plan.Requests.Count>0)
            {
                _generalHistory.Clear();var results=new List<AssistantResult>();
                foreach(var request in plan.Requests.Take(2))
                {
                    if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                    string prefix=plan.Requests.Count>1?"Result "+(results.Count+1)+" of "+plan.Requests.Count+": ":"";
                    var result=await _engine.ExecuteAsync(request,context,ct,r=>ReportSearch(r,context,generation,searchProgress,prefix));
                    ct.ThrowIfCancellationRequested();
                    if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                    results.Add(result);
                }
                _previous.Clear();
                if(results.All(r=>r.IsLive)) _previous.AddRange(results.Select(r=>r.Request.Copy()));
                var explanation=plan.GuidanceQuestion==null?null:await ExplainAsync(plan.GuidanceQuestion,context,ct);
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                if(results.Count==1)
                {
                    if(explanation!=null) AttachExplanation(results[0],explanation);
                    return results[0];
                }
                var combined=AssistantResult.Message(results.All(r=>r.IsLive)?AssistantOutcome.Success:AssistantOutcome.Clarification,
                    string.Join("\n\n",results.Select((r,i)=>"Result "+(i+1)+"\n"+r.Text)));
                combined.Children=results.AsReadOnly();combined.IsComparison=plan.Compare;
                if(plan.Compare && results.Count==2 && results.All(r=>r.IsLive)) combined.Text+="\n\n"+Compare(results[0],results[1]);
                if(explanation!=null) AttachExplanation(combined,explanation);
                return combined;
            }
            _previous.Clear();
            if(plan.GuidanceQuestion!=null)
            {
                _generalHistory.Clear();
                var answer=await ExplainAsync(plan.GuidanceQuestion,context,ct);
                if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                var guidance=AssistantResult.Message(AssistantOutcome.Guidance,answer==null?"I couldn’t find a verified guide for that question. Please name the module or screen you’d like help with.":null);
                if(answer!=null) AttachExplanation(guidance,answer);
                return guidance;
            }
            try
            {
                var streaming=_model as IAssistantStreamingModel;
                Action<string> publish=text=>
                {
                    if(ct.IsCancellationRequested || !_isCurrent(context) || generation!=_generation) return;
                    AssistantPerformance.FirstText();partial?.Invoke(text);
                };
                string answer=await AssistantPerformance.TimeAsync("ai-general",()=>streaming!=null && partial!=null?
                    streaming.GeneralStreamingAsync(_generalHistory.ToList(),question,ct,publish):_model.GeneralAsync(_generalHistory.ToList(),question,ct));
                ct.ThrowIfCancellationRequested();if(!_isCurrent(context) || generation!=_generation) return ChangedContext(generation);
                if(string.IsNullOrWhiteSpace(answer)) return AssistantResult.Message(AssistantOutcome.Unavailable,"I couldn’t get an AI response just now. You can still ask for supported record lookups and application guides.");
                _generalHistory.Add(new ChatMessage("user",question));_generalHistory.Add(new ChatMessage("assistant",answer));
                if(_generalHistory.Count>12) _generalHistory.RemoveRange(0,_generalHistory.Count-12);
                return AssistantResult.Message(AssistantOutcome.Guidance,answer);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch { return AssistantResult.Message(AssistantOutcome.Unavailable,"I can’t reach the AI service just now. You can still ask for supported record lookups and application guides."); }
        }

        private static void AttachExplanation(AssistantResult result,AssistantExplanation explanation)
        {
            result.ExplanationText=explanation.Text;result.GuideCitations=explanation.Citations;
            result.Text=(string.IsNullOrEmpty(result.Text)?"":result.Text+"\n\n")+explanation.ReadableText;
        }
        private static AssistantExplanation FixedExplanation(IReadOnlyList<AssistantArticle> articles)
        {
            return new AssistantExplanation {Text=string.Join("\n\n",articles.Select(a=>a.Title+"\n"+a.Text)),
                Citations=articles.Select(a=>new AssistantGuideCitation {Id=a.Id,Source=a.Source}).ToList().AsReadOnly(),LegacyText=AssistantKnowledge.FixedAnswer(articles)};
        }
        private async Task<AssistantExplanation> ExplainAsync(string question,AssistantContext context,CancellationToken ct)
        {
            var articles=AssistantKnowledge.Search(question,context);
            if(articles.Count==0) return null;
            try
            {
                var json=await AssistantPerformance.TimeAsync("ai-guide",()=>_model.ExplainAsync(question,articles,ct));
                ct.ThrowIfCancellationRequested();
                var answer=OllamaAssistantModel.ValidateStructuredExplanation(json,articles);
                if(answer!=null) return answer;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch { }
            return FixedExplanation(articles);
        }
        private AssistantResult ChangedContext(int? generation=null)
        { if(!generation.HasValue || generation.Value==_generation) Reset();return AssistantResult.Message(AssistantOutcome.Cancelled,"Your account, permissions or database changed. The previous answer was discarded. Please ask again."); }
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
