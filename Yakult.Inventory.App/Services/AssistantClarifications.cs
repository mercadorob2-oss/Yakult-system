using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Services
{
    public sealed partial class YakumiAssistantService
    {
        private sealed class ClarificationAction
        {
            internal string Label;
            internal string Question;
            internal AssistantRequest Request;
            internal IReadOnlyList<AssistantRequest> Previous;
            internal int? ResultNumber;
            internal AssistantOrganizationResolution Organizations;
            internal string OrganizationField;
        }
        private sealed class PendingClarification
        {
            internal string Id=Guid.NewGuid().ToString("N");
            internal string Stamp;
            internal int Generation;
            internal readonly Dictionary<string,ClarificationAction> Actions=new Dictionary<string,ClarificationAction>();
        }
        private PendingClarification _pendingClarification;

        private AssistantClarificationChoice MatchClarificationReply(string question,AssistantContext context)
        {
            var pending=_pendingClarification;
            if(pending==null || pending.Stamp!=context.SessionStamp || pending.Generation!=_generation) return null;
            string reply=AssistantPlanner.Normalize(question).Trim().TrimEnd('?', '.', '!');
            var matches=pending.Actions.Where(p=>AssistantPlanner.Normalize(p.Value.Label).Equals(reply,StringComparison.OrdinalIgnoreCase)).ToList();
            if(matches.Count==0) matches=pending.Actions.Where(p=>string.Equals(p.Value.OrganizationField,reply,StringComparison.OrdinalIgnoreCase)).ToList();
            if(matches.Count==1) return new AssistantClarificationChoice(pending.Id,matches[0].Key,matches[0].Value.Label);
            string remainder;var result=MatchPendingResult(question,context,out remainder);
            return remainder.Length==0?result:null;
        }
        private AssistantClarificationChoice MatchPendingResult(string question,AssistantContext context,out string remainder)
        {
            int? number=AssistantFollowUpLanguage.ResultNumber(question,out remainder);var pending=_pendingClarification;
            if(!number.HasValue || pending==null || pending.Stamp!=context.SessionStamp || pending.Generation!=_generation) return null;
            var matches=pending.Actions.Where(p=>p.Value.ResultNumber==number).ToList();
            return matches.Count==1?new AssistantClarificationChoice(pending.Id,matches[0].Key,matches[0].Value.Label):null;
        }
        private Task<AssistantResult> RefinePendingResultAsync(AssistantClarificationChoice choice,string refinement,AssistantContext context,CancellationToken ct,Action<string> progress)
        {
            ct.ThrowIfCancellationRequested();
            if(!_isCurrent(context)) return Task.FromResult(ChangedContext());
            if(!IsClarificationCurrent(choice,context)) return Task.FromResult(AssistantResult.Message(AssistantOutcome.Cancelled,"Those choices are no longer current. Please ask your question again."));
            var action=_pendingClarification.Actions[choice.Id];
            var plan=AssistantPlanner.Plan(action.Question,context,action.Previous);
            if(plan.Message!=null || plan.NeedsInterpretation || plan.Requests.Count!=1)
                return Task.FromResult(AssistantResult.Message(AssistantOutcome.Clarification,"Please choose the result first, then tell me which filter to change."));
            _pendingClarification=null;
            return AskCoreAsync("same records "+refinement,context,ct,plan.Requests,progress);
        }

        internal bool IsClarificationCurrent(AssistantClarificationChoice choice,AssistantContext context)
        {
            var pending=_pendingClarification;
            return choice!=null && pending!=null && pending.Id==choice.PromptId && pending.Stamp==context.SessionStamp &&
                pending.Generation==_generation && pending.Actions.ContainsKey(choice.Id);
        }
        internal Task<AssistantResult> ResolveClarificationAsync(AssistantClarificationChoice choice,AssistantContext context,CancellationToken ct,Action<string> searchProgress=null)
        {return AssistantPerformance.TrackAsync(()=>ResolveClarificationCoreAsync(choice,context,ct,searchProgress));}
        private async Task<AssistantResult> ResolveClarificationCoreAsync(AssistantClarificationChoice choice,AssistantContext context,CancellationToken ct,Action<string> searchProgress)
        {
            ct.ThrowIfCancellationRequested();
            if(!_isCurrent(context)) return ChangedContext();
            if(!IsClarificationCurrent(choice,context))
                return AssistantResult.Message(AssistantOutcome.Cancelled,"Those choices are no longer current. Please ask your question again.");
            var action=_pendingClarification.Actions[choice.Id];
            _pendingClarification=null; // Consume before awaiting, so a second click cannot repeat the lookup.
            if(action.Request!=null) return await ExecuteRequestAsync(action.Request.Copy(),context,ct,searchProgress);
            return await AskCoreAsync(action.Question,context,ct,action.Previous,searchProgress,action.Organizations);
        }
        private AssistantResult ClarifyOrganization(AssistantOrganizationResolution resolution,AssistantContext context,IReadOnlyList<AssistantRequest> previous)
        {
            var mention=resolution.Uncertain;
            var roles=mention.Choices.Select(c=>c.Field.ToLowerInvariant()).Distinct().ToArray();
            string roleList=roles.Length<2?roles.FirstOrDefault():string.Join(", ",roles.Take(roles.Length-1))+" or "+roles.Last();
            var result=AssistantResult.Message(AssistantOutcome.Clarification,roles.Length==1?
                "Which "+roleList+" did you mean by “"+mention.Value+"”?":
                "By “"+mention.Value+"”, did you mean the "+roleList+"?");
            if(mention.Choices.Count>3) {result.MessageText="Several organizations match “"+mention.Value+"”. Please give the full name and say company, branch or department.";result.Text=result.MessageText;return result;}
            var actions=mention.Choices.Select(o=>new ClarificationAction {Label=o.Field+": "+o.Name+(mention.Choices.Count(c=>c.Field==o.Field && c.Name==o.Name)>1?" #"+o.Id:""),
                Question=resolution.Question,Previous=previous,Organizations=resolution.Choose(o),OrganizationField=o.Field}).ToList();
            return RegisterChoices(result,actions,context);
        }
        private AssistantResult Clarify(string question,AssistantPlan plan,AssistantContext context,IReadOnlyList<AssistantRequest> previous)
        {
            string message=plan.Message ?? plan.UnresolvedMeaning ?? "Which records and filters would you like me to look up?";
            var result=AssistantResult.Message(AssistantOutcome.Clarification,message);
            var actions=new List<ClarificationAction>();
            string semantic=AssistantQuestionLanguage.WithoutQuotes(AssistantPlanner.Normalize(question));
            if(!context.IsLoggedIn || plan.GuidanceQuestion!=null || (question ?? "").Length>AiIntentRouter.MaxQuestionLength ||
                AssistantQuestionLanguage.HasUnsupportedExclusion(question) || AssistantFollowUpLanguage.IsRowReference(question)) return result;

            string remainder;int? number=AssistantFollowUpLanguage.ResultNumber(question,out remainder);
            if(previous.Count==2 && !number.HasValue && AssistantQuestionLanguage.IsFollowUp(question) &&
                (AssistantPlanner.BestCapability(question)==null || AssistantFollowUpLanguage.IsReference(question)))
            {
                result.MessageText="Which result would you like to refine?";
                for(int i=0;i<previous.Count;i++)
                {
                    var request=previous[i];var capability=AssistantCatalog.Find(request.Capability);
                    if(capability==null || !capability.IsAllowed(context)) continue;
                    if(AssistantFollowUpLanguage.IsReference(question) && AssistantFollowUpLanguage.Resolve(question,new[] {request}).Message!=null) continue;
                    string label=AssistantCatalog.Find(previous[0].Capability)?.Id!=previous[1].Capability?capability.Title:"Result "+(i+1)+": "+capability.Title;
                    actions.Add(new ClarificationAction {Label=label,Question=question,Previous=new[] {request.Copy()},ResultNumber=i+1});
                }
            }
            else if(AssistantPlanner.BestCapability(question)?.Id=="tickets" &&
                AssistantWordMappings.HasAmbiguousTicketStatus(semantic) && !AiIntentRouter.Has(semantic,@"\b(temporary|solved|closed)\b"))
            {
                if(AssistantCatalog.Find("tickets").IsAllowed(context))
                {
                    result.MessageText="Which ticket status would you like to check?";
                    foreach(string status in new[] {"Solved","Closed","Resolved (Temporary)"})
                        actions.Add(new ClarificationAction {Label=status,Question=AssistantQuestionLanguage.ReplaceUnquoted(question,AssistantWordMappings.AmbiguousTicketStatus,status),Previous=previous});
                }
            }
            else if(AssistantPlanner.BestCapability(question)?.SupportedFilters.Contains("Priority")==true && AssistantWordMappings.HasAmbiguousPriority(semantic))
            {
                var capability=AssistantPlanner.BestCapability(question);
                if(capability.IsAllowed(context))
                {
                    result.MessageText="Would you like High or Critical priority?";
                    foreach(string priority in new[] {"High","Critical"})
                        actions.Add(new ClarificationAction {Label=priority+" priority",Question=AssistantQuestionLanguage.ReplaceUnquoted(question,AssistantWordMappings.AmbiguousPriority,priority+" priority"),Previous=previous});
                }
            }
            else if(plan.NeedsInterpretation && plan.Requests.Count==0 && plan.GuidanceQuestion==null)
            {
                foreach(var source in new[] {Tuple.Create("items","Stock"),Tuple.Create("tickets","Tickets"),Tuple.Create("requests","Requests")})
                {
                    var capability=AssistantCatalog.Find(source.Item1);
                    if(!capability.IsAllowed(context)) continue;
                    // The user's explicit source selection is added to the original wording. The complete
                    // question goes back through planning; unknown filters cannot become a broad count.
                    actions.Add(new ClarificationAction {Label=source.Item2,Question=(source.Item1=="items"?"items in stock":source.Item1)+" "+question,Previous=new AssistantRequest[0]});
                }
            }
            else if(plan.NeedsInterpretation && plan.Requests.Count==1 && plan.UnresolvedFilter==null)
            {
                var request=plan.Requests[0];
                if(!string.IsNullOrWhiteSpace(request.Search) && AssistantCatalog.Find(request.Capability).IsAllowed(context))
                {
                    try
                    {
                        AssistantQueryEngine.BuildQuery(request,context);
                        actions.Add(new ClarificationAction {Label="Use as search text",Request=request.Copy()});
                    }
                    catch(ArgumentException) { }
                    catch(UnauthorizedAccessException) { }
                }
            }
            return RegisterChoices(result,actions,context);
        }
        private AssistantResult ClarifyProduct(string question,AssistantPlan plan,AssistantStockHint stock,AssistantContext context,IReadOnlyList<AssistantRequest> previous)
        {
            if(stock==null || plan.Requests.Count!=1 || plan.UnresolvedFilter!=null || plan.GuidanceQuestion!=null ||
                AssistantQuestionLanguage.HasUnsupportedExclusion(question)) return null;
            var request=plan.Requests[0];
            if(request.Capability!=stock.Capability || !AssistantCatalog.Find(request.Capability).IsAllowed(context)) return null;
            var suggestion=AssistantProductMatching.Suggest(question,request,stock.Products);
            if(suggestion==null) return null;
            try {AssistantQueryEngine.BuildQuery(request,context);}
            catch(ArgumentException) {return null;}
            catch(UnauthorizedAccessException) {return null;}
            string pattern=@"(?<![\p{L}\p{Nd}-])"+Regex.Escape(suggestion.Original)+@"(?![\p{L}\p{Nd}-])";
            var actions=suggestion.Alternatives.Select(name=>new ClarificationAction {Label=name,
                Question=AssistantQuestionLanguage.ReplaceUnquoted(question,pattern,name),Previous=previous}).ToList();
            actions.Add(new ClarificationAction {Label="Keep original search",Request=request.Copy()});
            string message=suggestion.Alternatives.Count==1?"Did you mean “"+suggestion.Alternatives[0]+"” instead of “"+suggestion.Original+"”?":
                "Which product did you mean by “"+suggestion.Original+"”?";
            return RegisterChoices(AssistantResult.Message(AssistantOutcome.Clarification,message),actions,context);
        }
        private AssistantResult RegisterChoices(AssistantResult result,IReadOnlyList<ClarificationAction> actions,AssistantContext context)
        {
            if(actions.Count==0) return result;
            var pending=new PendingClarification {Stamp=context.SessionStamp,Generation=_generation};
            var choices=new List<AssistantClarificationChoice>();
            foreach(var action in actions.Take(3))
            {
                string id=Guid.NewGuid().ToString("N");
                // Own every scope snapshot; neither mutable results nor later questions can alter it.
                action.Previous=action.Previous?.Select(r=>r.Copy()).ToList().AsReadOnly();
                action.Request=action.Request?.Copy();
                pending.Actions.Add(id,action);choices.Add(new AssistantClarificationChoice(pending.Id,id,action.Label));
            }
            _pendingClarification=pending;result.ClarificationChoices=choices.AsReadOnly();
            result.Text=result.MessageText+"\nChoices: "+string.Join(" · ",choices.Select(c=>c.Label));
            return result;
        }
    }
}
