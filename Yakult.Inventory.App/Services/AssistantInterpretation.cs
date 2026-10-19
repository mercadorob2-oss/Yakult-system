using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Yakult.Inventory.App.Services
{
    /// <summary>A model may propose typed scope only. C# validates evidence and builds every SQL parameter.</summary>
    internal static class AssistantInterpretation
    {
        private static readonly string[] FilterNames={"search","status","priority","employee","department","branch","company","condition","category","period","groupBy","personalRelation","stock"};
        private static readonly string[] StatusValues={"Resolved (Temporary)","Forwarded to Repair","In Progress","AwaitingParts","WaitingParts","WaitingDiagnosis","CannotRepair","Unrepairable","Diagnosing","Repairing","Completed","Reopened","Escalated","Rejected","Approved","Cancelled","Returned","Unfulfilled","Pending","Solved","Closed","Testing","Waiting","Inactive","Active","Open","Used","Repaired","Archived","Pending review","NonLicensed","Granted","Denied"};
        private static readonly Dictionary<string,string> SourceWords=new Dictionary<string,string>
        {
            {"tickets","helpdesk|support incident|support incidents|IT incident|IT incidents"},
            {"borrows","loan|loans|loaned equipment"}, {"requests","requisition|requisitions"},
            {"items","equipment|product|products|available|in stock"}
        };
        private static readonly Dictionary<string,string> StatusPhrases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            {"awaiting fulfillment","Unfulfilled"},{"waiting for fulfillment","Unfulfilled"}
        };
        internal static JObject Schema(IReadOnlyList<AssistantCapability> candidates,string question=null)
        {
            var filters=new JObject();
            foreach(string field in FilterNames) filters[field]=new JObject { ["type"]=field=="stock"?"boolean":"string" };
            filters["status"]["enum"]=new JArray(StatusValues);
            filters["status"]["description"]="Canonical status. Awaiting fulfillment means Unfulfilled. Omit status words found inside quoted names.";
            filters["search"]["description"]="Literal name/model terms. Keep double quotes around quoted names, even if they contain a status word.";
            filters["priority"]["enum"]=new JArray("Critical","High","Medium","Low");
            if(question!=null)
            {
                string semantic=AssistantQuestionLanguage.WithoutQuotes(AssistantPlanner.Normalize(question));
                var statuses=StatusValues.Where(s=>Grounded(s,semantic) ||
                    (AiIntentRouter.Has(semantic,@"\bstatus\b") && Grounded(s,question)))
                    .Concat(StatusPhrases.Where(p=>semantic.IndexOf(p.Key,StringComparison.OrdinalIgnoreCase)>=0).Select(p=>p.Value)).Distinct().ToArray();
                if(statuses.Length==0) filters.Remove("status");else filters["status"]["enum"]=new JArray(statuses);
                var priorities=new[] {"Critical","High","Medium","Low"}.Where(p=>Grounded(p,semantic) ||
                    (AiIntentRouter.Has(semantic,@"\bpriority\b") && Grounded(p,question))).ToArray();
                if(priorities.Length==0) filters.Remove("priority");else filters["priority"]["enum"]=new JArray(priorities);
                var known=AssistantPlanner.BestCapability(question);
                if(known!=null && !AiIntentRouter.Has(semantic,@"\b(and|plus|versus|vs|compare)\b"))
                    foreach(string field in new[] {"employee","department","branch","company","category","condition"})
                        if(!known.SupportedFilters.Contains(char.ToUpperInvariant(field[0])+field.Substring(1))) filters.Remove(field);
                if(!AiIntentRouter.Has(semantic,@"\b(condition|used|repaired|brand new|refilled)\b")) filters.Remove("condition");
                if(!AiIntentRouter.Has(semantic,@"\bcategory\b")) filters.Remove("category");
            }
            return new JObject { ["type"]="object",["additionalProperties"]=false,
                ["properties"]=new JObject {
                    ["clarification"]=new JObject { ["type"]="string",["description"]="Empty string for a clear lookup. A question only when requests is empty." },["compare"]=new JObject { ["type"]="boolean" },
                    ["requests"]=new JObject { ["type"]="array",["maxItems"]=2,["items"]=new JObject {
                        ["type"]="object",["additionalProperties"]=false,["required"]=new JArray("capability","operation","sourceEvidence","inherit","filters"),
                        ["properties"]=new JObject {
                            ["capability"]=new JObject { ["type"]="string",["enum"]=new JArray(candidates.Select(c=>c.Id)) },
                            ["operation"]=new JObject { ["type"]="string",["enum"]=new JArray(Enum.GetNames(typeof(AssistantOperation))) },
                            ["sourceEvidence"]=new JObject { ["type"]="string" },["inherit"]=new JObject { ["type"]="boolean" },
                            ["filters"]=new JObject { ["type"]="object",["additionalProperties"]=false,["properties"]=filters }
                        }
                    } }
                },["required"]=new JArray("clarification","compare","requests") };
        }
        internal static string ContextJson(AssistantInterpretationContext context)
        {
            // An explicit projection excludes labels, rows, totals, employee identity and session credentials.
            var previous=new JArray((context?.Previous ?? new AssistantRequest[0]).Select(r=>new JObject {
                ["capability"]=r.Capability,["operation"]=r.Operation.ToString(),["search"]=r.Search,
                ["status"]=r.Status,["priority"]=r.Priority,["employee"]=r.Employee,["department"]=r.Department,
                ["branch"]=r.Branch,["company"]=r.Company,["category"]=r.Category,["condition"]=r.Condition,
                ["personal"]=r.Personal,["relation"]=r.PersonalRelation,["stock"]=r.InStockOnly,["groupBy"]=r.GroupBy,
                ["from"]=r.FromLocal?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                ["toExclusive"]=r.ToLocalExclusive?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) }));
            string json=previous.ToString(Formatting.None);
            // Omit incomplete or oversized scope instead of truncating JSON or literal filter values.
            return System.Text.Encoding.UTF8.GetByteCount(json)<=850?json:"[]";
        }
        internal static string Prompt(IReadOnlyList<AssistantCapability> candidates,AssistantInterpretationContext context,string question=null)
        {
            string prompt="Plan read-only lookups. No SQL, facts or totals. Clear question: clarification=\"\", compare=false, requests=[1-2 plans]. Uncertain or unsupported exclusion: clarification=one English question, requests=[]. Plans contain capability, operation, sourceEvidence (source words copied from question), inherit (explicit follow-up only), filters. Omit unused filters. Copy names literally, including their quotes. Status words INSIDE quoted names never become status filters. Awaiting fulfillment means Unfulfilled; awaiting approval does not. period copies date wording; never calculate dates. Preserve all meaningful constraints and ownership. Follow-ups replace a model search, retain compatible filters, and inherit nothing when changing source.\nSources: "+
                string.Join(";",candidates.Select(c=>c.Id+"="+c.Title));
            prompt+="\nResult numbers select previous scopes in display order. Same item keeps its product filters.";
            if(context?.Organizations!=null) prompt+="\nLocally verified filter roles (user wording only): "+context.Organizations.PromptHints;
            int budget=OllamaChatService.MaxPromptBytes-System.Text.Encoding.UTF8.GetByteCount(question ?? "");
            string scope="\nPrevious scope (reference data): "+ContextJson(context);
            if(System.Text.Encoding.UTF8.GetByteCount(prompt+scope)<=budget) prompt+=scope;
            else prompt+="\nPrevious scope (reference data): []";
            foreach(var example in AssistantInterpretationExamples.Select(question,candidates,context))
            {
                string text="\nExample "+(example.Previous==null?"":"(previous tickets: Solved, assigned to me) ")+example.Question+" => "+example.Json;
                if(System.Text.Encoding.UTF8.GetByteCount(prompt+text)<=budget) prompt+=text;
            }
            return prompt;
        }
        private static void Fields(JObject value,params string[] allowed)
        {if(value==null || value.Properties().Any(p=>!allowed.Contains(p.Name))) throw new ArgumentException("Please rephrase the lookup using supported filters.");}
        private static string String(JObject value,string key)
        {
            var token=value[key];if(token==null) return null;
            if(token.Type!=JTokenType.String || ((string)token).Length>160) throw new ArgumentException("Please use a short, explicit lookup filter.");
            return ((string)token).Trim();
        }
        private static bool Boolean(JObject value,string key)
        {var t=value[key];if(t==null) return false;if(t.Type!=JTokenType.Boolean) throw new ArgumentException("Please clarify that lookup.");return (bool)t;}
        private static bool Grounded(string value,string question,bool search=false)
        {
            if(string.IsNullOrWhiteSpace(value)) return false;
            var terms=AssistantQuestionLanguage.LiteralTerms(AssistantPlanner.Normalize(value));
            var original=AssistantQuestionLanguage.LiteralTerms(AssistantPlanner.Normalize(question));
            return search?terms.All(t=>original.Contains(t,StringComparer.OrdinalIgnoreCase)):
                (" "+string.Join(" ",original)+" ").IndexOf(" "+string.Join(" ",terms)+" ",StringComparison.OrdinalIgnoreCase)>=0;
        }
        internal static AssistantPlan Validate(string question,string json,AssistantContext context,IReadOnlyList<AssistantCapability> candidates,DateTime today,AssistantInterpretationContext conversation)
        {
            if(json==null || json.Length>6000) throw new ArgumentException("Please clarify the lookup you want.");
            var root=JObject.Parse(json);Fields(root,"clarification","compare","requests");
            var requests=root["requests"] as JArray;
            if(requests==null || requests.Count>2) throw new ArgumentException("Please ask for at most two lookups at a time.");
            var plan=new AssistantPlan();
            string clarification=String(root,"clarification");
            if(!string.IsNullOrWhiteSpace(clarification) || requests.Count==0)
            {plan.Message="Which record type and filters would you like me to look up?";return plan;}
            if(AssistantQuestionLanguage.HasUnsupportedExclusion(question)) throw new ArgumentException("Which single status or item would you like to include? That exclusion isn’t supported.");
            var organizations=conversation?.Organizations;
            var reference=AssistantFollowUpLanguage.Resolve(organizations==null?question:organizations.Rewrite(question),conversation?.Previous);
            if(reference.Message!=null) throw new ArgumentException(reference.Message);
            string q=AssistantPlanner.Normalize(reference.Question);
            conversation=new AssistantInterpretationContext(reference.Previous);
            bool follow=AssistantQuestionLanguage.IsFollowUp(q);
            if(follow && (conversation==null || conversation.Previous.Count!=1)) throw new ArgumentException("Which result would you like to refine? Please name its record type.");
            plan.Compare=Boolean(root,"compare");
            if(plan.Compare && (requests.Count!=2 || !AiIntentRouter.Has(q,@"\b(compare|comparison|versus|vs)\b"))) throw new ArgumentException("Please name the two scopes you want to compare.");
            string[] scopes=requests.Count==2?Regex.Split(q,plan.Compare?@"\s+(?:with|vs\.?|versus|compared to|compared with)\s+":@"\s+(?:and|plus)\s+",RegexOptions.IgnoreCase):new[] {q};
            if(scopes.Length!=requests.Count) throw new ArgumentException("Please name each lookup and its filters separately.");
            for(int index=0;index<requests.Count;index++)
            {
                var entry=requests[index] as JObject;Fields(entry,"capability","operation","sourceEvidence","inherit","filters");
                string id=String(entry,"capability"),scope=scopes[index];
                var capability=candidates.FirstOrDefault(c=>c.Id==id && c.IsAllowed(context));
                if(capability==null) throw new ArgumentException("Please name a record type available to your account.");
                bool inherit=Boolean(entry,"inherit");
                if(inherit && (!follow || conversation==null || conversation.Previous.Count!=1 || conversation.Previous[0].Capability!=id))
                    throw new ArgumentException("Please name the lookup and its filters explicitly.");
                string evidence=String(entry,"sourceEvidence");
                var known=AssistantPlanner.BestCapability(scope);
                string synonyms;SourceWords.TryGetValue(id,out synonyms);
                var sourcePhrases=capability.Aliases.Concat((synonyms ?? "").Split('|')).Where(s=>s.Length>0).Select(AssistantPlanner.Normalize).ToArray();
                string semantic=AssistantQuestionLanguage.WithoutQuotes(scope);
                bool evidenceIsSource=Grounded(evidence,semantic) && sourcePhrases.Contains(AssistantPlanner.Normalize(evidence));
                if(!inherit)
                {
                    bool source=known?.Id==id || evidenceIsSource;
                    if(!source || (known!=null && known.Id!=id)) throw new ArgumentException("Which record type did you mean? Please name it explicitly.");
                }
                var baseline=AssistantPlanner.CreateRequest(scope,context,id,today);
                var request=inherit?conversation.Previous[0].Copy():baseline.Copy();
                if(!inherit && evidenceIsSource && !AssistantQuestionLanguage.Quotes.IsMatch(request.Search ?? ""))
                {
                    var sourceTerms=AssistantQuestionLanguage.SearchTerms(evidence);
                    request.Search=string.Join(" ",AssistantQuestionLanguage.SearchTerms(request.Search).Where(t=>!sourceTerms.Contains(t,StringComparer.OrdinalIgnoreCase)));
                }
                if(inherit)
                {
                    var local=AssistantPlanner.Plan(scope,context,conversation.Previous,today);
                    if(local.Message!=null) throw new ArgumentException(local.Message);
                    if(local.Requests.Count==1) request=local.Requests[0].Copy();
                }
                var filters=entry["filters"] as JObject;Fields(filters,FilterNames);
                foreach(var property in filters.Properties())
                {
                    if(property.Name=="stock")
                    {
                        bool stock=Boolean(filters,"stock");
                        if(stock!=baseline.InStockOnly && (!inherit || stock!=request.InStockOnly)) throw new ArgumentException("Please specify whether you mean available stock.");
                        request.InStockOnly=stock;continue;
                    }
                    string value=String(filters,property.Name);
                    bool statusPhrase=property.Name=="status" && StatusPhrases.Any(p=>p.Value.Equals(value,StringComparison.OrdinalIgnoreCase) &&
                        semantic.IndexOf(p.Key,StringComparison.OrdinalIgnoreCase)>=0);
                    if(!Grounded(value,scope,property.Name=="search") && !statusPhrase && !(property.Name=="search" && value=="")) throw new ArgumentException("Please state the "+property.Name+" filter explicitly.");
                    if(new[] {"status","priority","period","groupBy","personalRelation"}.Contains(property.Name) && !statusPhrase &&
                        !Grounded(value,semantic) && !AiIntentRouter.Has(semantic,@"\b"+property.Name+@"\b"))
                        throw new ArgumentException("Should I use that quoted text as a name or as a filter?");
                    string role=property.Name=="employee"?@"\b(assigned|handled|borrowed|employee|assignee|technician)\b":
                        new[] {"department","branch","company","category","condition"}.Contains(property.Name)?@"\b"+property.Name+@"\b":null;
                    string existing=property.Name=="employee"?baseline.Employee:property.Name=="department"?baseline.Department:property.Name=="branch"?baseline.Branch:
                        property.Name=="company"?baseline.Company:property.Name=="category"?baseline.Category:property.Name=="condition"?baseline.Condition:null;
                    if(role!=null && existing==null && !AiIntentRouter.Has(AssistantQuestionLanguage.WithoutQuotes(scope),role))
                        throw new ArgumentException("Did you mean “"+value+"” as a "+property.Name+" filter?");
                    switch(property.Name)
                    {
                        case "search":
                            // Keep quotes so names containing words such as Still, Open or This Month survive.
                            if(!string.Equals(request.Search,value,StringComparison.OrdinalIgnoreCase)) request.ModelMatch=null;
                            request.Search=value;break;
                        case "status":
                            string status=StatusValues.FirstOrDefault(s=>s.Equals(value,StringComparison.OrdinalIgnoreCase));
                            if(status==null) throw new ArgumentException("Which supported status would you like me to use?");
                            if(id=="requests" && new[] {"Pending","Unfulfilled","Open"}.Contains(status))
                            {request.OutstandingOnly=true;request.Status=null;}
                            else request.Status=Preserve(baseline.Status,status);break;
                        case "priority":
                            string priority=new[] {"Critical","High","Medium","Low"}.FirstOrDefault(s=>s.Equals(value,StringComparison.OrdinalIgnoreCase));
                            if(priority==null) throw new ArgumentException("Which priority would you like me to use?");
                            request.Priority=Preserve(baseline.Priority,priority);break;
                        case "condition": request.Condition=Preserve(baseline.Condition,value);break;
                        case "category": request.Category=Preserve(baseline.Category,value);break;
                        case "employee":
                            if(request.Personal) throw new ArgumentException("Would you like your own records or records for a named employee?");
                            request.Employee=Preserve(baseline.Employee,value);request.PersonalRelation=null;break;
                        case "department": request.Department=Preserve(baseline.Department,value);break;
                        case "branch": request.Branch=Preserve(baseline.Branch,value);break;
                        case "company": request.Company=Preserve(baseline.Company,value);break;
                        case "groupBy":
                            string field=capability.GroupColumns.Concat(new[] {"Month"}).FirstOrDefault(f=>f.Equals(value,StringComparison.OrdinalIgnoreCase));
                            if(field==null) throw new ArgumentException("Which supported field would you like to group by?");request.GroupBy=field;break;
                        case "period":
                            string dateText=AssistantPlanner.Normalize(value);
                            if(!AssistantPlanner.ApplyDates(ref dateText,request,today) || AssistantQuestionLanguage.SearchTerms(dateText).Count>0) throw new ArgumentException("Which date range would you like me to use?");break;
                        case "personalRelation":
                            // Ownership is parsed locally; a model cannot infer another person's identity or remove scope.
                            if(!baseline.Personal) throw new ArgumentException("Please specify whose records you want.");break;
                    }
                }
                AssistantOperation operation;
                if(!Enum.TryParse(String(entry,"operation"),out operation) || !Enum.IsDefined(typeof(AssistantOperation),operation)) throw new ArgumentException("Please specify a supported lookup operation.");
                if(operation!=request.Operation && !(operation==AssistantOperation.Count && AiIntentRouter.Has(scope,@"\b(number|quantity|total|count|how many|how much)\b")) &&
                    !(operation==AssistantOperation.Summary && request.GroupBy!=null)) throw new ArgumentException("Would you like a count, a list or a grouped summary?");
                request.Operation=operation;request.Page=0;
                // Every locally meaningful search term must remain in some filter. No retry may broaden a zero result.
                var covered=AssistantQuestionLanguage.SearchTerms(string.Join(" ",new[] {request.Search,request.Employee,request.Department,request.Branch,request.Company,request.Category,request.Condition,request.Status,request.Priority}));
                if(evidenceIsSource) covered.AddRange(AssistantQuestionLanguage.SearchTerms(evidence));
                foreach(var phrase in StatusPhrases.Where(p=>semantic.IndexOf(p.Key,StringComparison.OrdinalIgnoreCase)>=0 &&
                    (string.Equals(request.Status,p.Value,StringComparison.OrdinalIgnoreCase) || (id=="requests" && request.OutstandingOnly && p.Value=="Unfulfilled"))))
                {
                    covered.AddRange(AssistantQuestionLanguage.SearchTerms(phrase.Key));
                    if(filters["search"]==null && !AssistantQuestionLanguage.Quotes.IsMatch(request.Search ?? ""))
                    {
                        var phraseTerms=AssistantQuestionLanguage.SearchTerms(phrase.Key);
                        request.Search=string.Join(" ",AssistantQuestionLanguage.SearchTerms(request.Search).Where(t=>!phraseTerms.Contains(t,StringComparer.OrdinalIgnoreCase)));
                    }
                }
                var missing=AssistantQuestionLanguage.SearchTerms(baseline.Search).Where(t=>!covered.Contains(t,StringComparer.OrdinalIgnoreCase));
                if(missing.Any()) throw new ArgumentException("Should I search for “"+baseline.Search+"”, or did you mean a different filter?");
                AssistantOrganizationResolution.ClearStale(request);organizations?.Apply(request);
                // Validate source-specific fields and permissions before the service can execute anything.
                AssistantQueryEngine.BuildQuery(request,context);
                plan.Requests.Add(request);
            }
            return plan;
        }
        private static string Preserve(string local,string proposed)
        {if(local!=null && !local.Equals(proposed,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Please clarify the filter you want to replace.");return local ?? proposed;}
    }
}
