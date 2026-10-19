using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yakult.Inventory.App.Services
{
    internal sealed class AssistantInterpretationExample
    {
        internal string Question { get; }
        internal string Capability { get; }
        internal string MatchPattern { get; }
        internal string Json { get; }
        internal AssistantRequest Previous { get; }
        internal AssistantInterpretationExample(string question,string capability,string pattern,JObject filters,string operation="List",string source=null,AssistantRequest previous=null)
        {
            Question=question;Capability=capability;MatchPattern=pattern;Previous=previous;
            Json=new JObject { ["clarification"]="",["compare"]=false,["requests"]=new JArray(new JObject {
                ["capability"]=capability,["operation"]=operation,["sourceEvidence"]=source ?? capability,
                ["inherit"]=previous!=null,["filters"]=filters }) }.ToString(Formatting.None);
        }
    }

    /// <summary>Reviewed synthetic examples. Tests validate their plans with the production validator.</summary>
    internal static class AssistantInterpretationExamples
    {
        internal static readonly IReadOnlyList<AssistantInterpretationExample> All=Array.AsReadOnly(new[] {
            new AssistantInterpretationExample("How many support incidents?","tickets",@"\b(incident|incidents|support|helpdesk)\b",new JObject(),"Count","support incidents"),
            new AssistantInterpretationExample("Show requests awaiting fulfillment","requests",@"\b(fulfillment|fulfilled|unfulfilled)\b",new JObject {["status"]="Unfulfilled"}),
            new AssistantInterpretationExample("How many items named \"Still Open\" are in stock?","items","[\"']",new JObject {["search"]="\"Still Open\"",["stock"]=true},"Count","items"),
            new AssistantInterpretationExample("Show tickets assigned Example Person","tickets",@"\b(assigned|employee|assignee|handled)\b",new JObject {["employee"]="Example Person",["search"]=""}),
            new AssistantInterpretationExample("How many my pending requests this month?","requests",@"\b(my|pending|month)\b",new JObject {["status"]="Pending",["period"]="this month"},"Count"),
            new AssistantInterpretationExample("only this month","tickets",@"^(only|just|same)\b",new JObject {["period"]="this month"},"Count","",
                new AssistantRequest {Capability="tickets",Operation=AssistantOperation.Count,Status="Solved",Personal=true,PersonalRelation="Assigned"}),
            new AssistantInterpretationExample("Count IT incidents in company \"Example Company\"","tickets",@"\b(company|branch|department)\b",new JObject {["company"]="Example Company"},"Count","IT incidents"),
            new AssistantInterpretationExample("Show repairs priority Low this month","repairs",@"\brepairs?\b",new JObject {["priority"]="Low",["period"]="this month"})
        });
        internal static IEnumerable<AssistantInterpretationExample> Select(string question,IReadOnlyList<AssistantCapability> candidates,AssistantInterpretationContext context)
        {
            string q=AssistantPlanner.Normalize(question);var known=AssistantPlanner.BestCapability(question);
            bool singleSource=known!=null && !AiIntentRouter.Has(AssistantQuestionLanguage.WithoutQuotes(q),@"\b(and|plus|versus|vs|compare)\b");
            return All.Where(e=>candidates.Any(c=>c.Id==e.Capability) && (e.Previous==null ||
                (context?.Previous.Count==1 && context.Previous[0].Capability==e.Capability && AssistantQuestionLanguage.IsFollowUp(q))))
                .Where(e=>!singleSource || e.Capability==known.Id)
                .OrderByDescending(e=>AiIntentRouter.Has(q,e.MatchPattern)?10:0)
                .ThenByDescending(e=>e.Capability==known?.Id?1:0).Take(2);
        }
    }
}
