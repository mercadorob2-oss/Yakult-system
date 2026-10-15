using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Pure, bounded question planning. Produces typed requests, never SQL.</summary>
    public static class AssistantPlanner
    {
        private static bool Has(string question,string pattern) { return AiIntentRouter.Has(question,pattern); }
        internal static string Normalize(string question)
        {
            string text=Regex.Replace(question ?? "",@"\bhow\s+(may|mny|mnay)\b","how many",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"\bsloved\b","solved",RegexOptions.IgnoreCase);
            return AssistantKnowledge.NormalizeWords(text).Trim();
        }
        internal static bool IsGuidance(string q)
        {
            return Has(q,@"\b(how (do|can|should|to)|where (do|can|should)|paano|saan|explain|why|bakit|meaning|difference|workflow|procedure|guide|steps|what can you do)\b") ||
                Has(q,@"^what (is|are) (a |an |the )?(invoice|request|set|asset|renewal|cartridge|borrow|repair|ticket|parent tag|vat|wht|permission|role)\??$");
        }
        public static AssistantPlan Plan(string question,AssistantContext context,IReadOnlyList<AssistantRequest> previous=null,DateTime? todayOverride=null)
        {
            string q=Normalize(question);var plan=new AssistantPlan();
            if(q.Length==0) { plan.Message="Please enter a question.";return plan; }
            if(q.Length>AiIntentRouter.MaxQuestionLength) { plan.Message="Please shorten your question to "+AiIntentRouter.MaxQuestionLength+" characters.";return plan; }
            if(!context.IsLoggedIn) { plan.Message="Please sign in to use application lookups.";return plan; }
            if(Has(q,@"^(please )?(create|delete|update|approve|reject|issue|archive|restore|send|grant|change|save|submit|remove|return)\b"))
            { plan.Message="Yakumi is read-only. I can explain that workflow, but cannot perform it.";plan.GuidanceQuestion="How do I "+q;return plan; }
            if(Has(q,@"\b(who am i|do you know me|my (name|profile)|about me|sino ako|what can i access|my permission|my access)\b") ||
                Has(q,@"^(what is |show |tell me )?my (department|branch|company|position)\??$"))
            { plan.Message="@session";return plan; }
            if(Has(q,@"\b(help (with )?(this|current) (page|screen)|what (can|does) (this|the current) (page|screen)|how (do i )?use this (page|screen))\b"))
            {
                var screen=AssistantScreens.All.FirstOrDefault(s=>s.PageKey==context.Page);
                plan.GuidanceQuestion=screen==null?"Yakumi application capabilities":screen.Title+" workflow guide";return plan;
            }
            if(Has(q,@"\boverdue\b") && Has(q,@"\b(borrow|loan)\b"))
            { plan.Message="Borrow records have no due-date field, so I cannot determine overdue status. Ask for open loans or borrow aging.";return plan; }
            Tuple<string,long> reference;
            try { reference=Reference(q); } catch(ArgumentException ex) { plan.Message=ex.Message;return plan; }
            if(IsGuidance(q))
            {
                if(BestCapability(q)?.Id!="policies") {plan.GuidanceQuestion=question;if(reference==null) return plan;}
            }
            DateTime today=todayOverride ?? TimeZoneInfo.ConvertTime(DateTime.UtcNow,context.TimeZone).Date;
            try
            {
                if(TryFollowUp(q,context,previous,today,plan)) return plan;
                bool compare=Has(q,@"\b(compare|comparison|versus|vs)\b");
                if(compare)
                {
                    var halves=Regex.Split(q,@"\s+(?:vs\.?|versus|compared with|compared to|with)\s+",RegexOptions.IgnoreCase);
                    if(halves.Length!=2) { plan.Message="Please name two comparable scopes, for example 'compare invoices this month with last month'.";return plan; }
                    var first=CreateRequest(halves[0],context,null,today);
                    if(first==null) { plan.Message="Please specify the records to compare.";return plan; }
                    var secondSource=BestCapability(halves[1]);
                    AssistantRequest second;
                    if(secondSource==null) {second=first.Copy();ApplyFilters(halves[1],second,context,today,false);second.Page=0;}
                    else second=CreateRequest(halves[1],context,null,today);
                    if(second==null) { plan.Message="Please specify the second comparison scope.";return plan; }
                    first.Operation=AssistantOperation.Count;second.Operation=AssistantOperation.Count;
                    plan.Requests.Add(first);plan.Requests.Add(second);plan.Compare=true;return plan;
                }
                var parts=Regex.Split(q,@"\s+(?:and|plus|at)\s+",RegexOptions.IgnoreCase);
                // Split only when each side names its own source; names and filter conjunctions stay intact.
                if(parts.Length>1 && parts.All(p=>BestCapability(p)!=null) && !Has(q,@"\bbetween\b"))
                {
                    if(parts.Length>2) { plan.Message="Please ask for at most two lookups at a time.";return plan; }
                    foreach(string part in parts) plan.Requests.Add(CreateRequest(part,context,null,today));
                    return plan;
                }
                var request=CreateRequest(q,context,null,today);
                if(request!=null)
                {
                    if(request.LowestStock && request.Operation==AssistantOperation.Count)
                    { plan.Message="No low-stock threshold is defined for this lookup. Ask 'show lowest stock items' for a sorted list, or use the stock page's configured threshold.";return plan; }
                    plan.Requests.Add(request);return plan;
                }
                bool application=Has(q,@"\b(yakumi|yakult|application|system|portal|report|permission|account|stock|request|ticket|invoice|repair|borrow)\b");
                if(application) plan.GuidanceQuestion=question;
                else plan.IsGeneralQuestion=true;
            }
            catch(ArgumentException ex) { plan.Requests.Clear();plan.Message=ex.Message; }
            return plan;
        }

        private static bool TryFollowUp(string q,AssistantContext context,IReadOnlyList<AssistantRequest> previous,DateTime today,AssistantPlan plan)
        {
            bool page=Has(q,@"^\s*(show |the )?(next( page)?|previous( page)?|more|back)\s*[?.!]*$");
            bool total=Has(q,@"^(and )?(how many (in )?total|what is the total|total|ilan lahat)\??$");
            bool list=Has(q,@"^(show|list) (them|those|the records|the items)\??$");
            bool filter=Has(q,@"^(and |only |just |what about |same (but )?)") || Has(q,@"^(today|yesterday|this month|last month|this week|last week|this year|last year|assigned to me|by status|by month)( only)?\??$");
            if(!page && !total && !list && !filter) return false;
            if(previous==null || previous.Count!=1)
            {
                if(filter && BestCapability(q)!=null) return false;
                plan.Message="Please specify one lookup first, then apply the follow-up filter or page.";return true;
            }
            var current=previous[0].Copy();
            if(filter && BestCapability(q)!=null && BestCapability(q).Id!=current.Capability) return false;
            if(page)
            {
                if(current.Operation==AssistantOperation.Count) current.Operation=AssistantOperation.List;
                current.Page=Has(q,@"\b(previous|back)\b")?Math.Max(0,current.Page-1):current.Page+1;
            }
            else if(total) { current.Operation=AssistantOperation.Count;current.Page=0; }
            else if(list) { current.Operation=AssistantOperation.List;current.Page=0; }
            else { ApplyFilters(q,current,context,today,false);current.Page=0; }
            plan.Requests.Add(current);return true;
        }

        internal static AssistantCapability BestCapability(string q)
        {
            q=Normalize(q);
            if(Has(q,@"\b(policy|policies|guideline|handbook|memo|rules)\b")) return AssistantCatalog.Find("policies");
            q=Regex.Replace(q,@"\b(?:group(?:ed)? by|by|per)\s+(status|priority|employee|department|branch|company|category|condition|month|subtype|parent tag|vendor|role|action|entity type)\b"," ");
            q=Regex.Replace(q,@"\b(?:assigned to|handled by|borrowed by|in department|in branch|in company|for employee)\s+.+$"," ");
            var choices=AssistantCatalog.All.Select(c=>new { Capability=c,Score=c.Aliases.Select(a=>
            {
                if(a=="do you know" || a=="who is") return 0.0;
                var match=Regex.Match(q,@"\b"+Regex.Escape(Normalize(a))+@"\b");
                return match.Success?Normalize(a).Split(' ').Length*10+3.0/(1+match.Index):0;
            }).DefaultIfEmpty(0).Max() })
                .Where(c=>c.Score>0).OrderByDescending(c=>c.Score).ThenBy(c=>c.Capability.Id,StringComparer.Ordinal).ToList();
            return choices.Count==0?(Has(q,@"\b(who is|do you know|tell me about)\b")?AssistantCatalog.Find("employees"):null):choices[0].Capability;
        }

        internal static IReadOnlyList<AssistantCapability> InterpretationCandidates(string question,AssistantContext context)
        {
            var tokens=AssistantKnowledge.Tokens(question);
            return AssistantCatalog.All.Where(c=>c.IsAllowed(context)).Select(c=>new { C=c,Score=c.Aliases.Sum(a=>AssistantKnowledge.Tokens(a).Intersect(tokens).Count())+(c.Module==context.Module?1:0) })
                .OrderByDescending(p=>p.Score).ThenBy(p=>p.C.Id).Take(6).Select(p=>p.C).ToList().AsReadOnly();
        }

        internal static AssistantRequest InterpretJson(string question,string json,AssistantContext context,IReadOnlyList<AssistantCapability> candidates,DateTime today)
        {
            var value=JObject.Parse(json);
            if(value.Properties().Any(p=>p.Name!="capability")) throw new ArgumentException("The interpretation contained unsupported fields.");
            string id=(string)value["capability"];
            if(id==null || !candidates.Any(c=>c.Id==id && c.IsAllowed(context))) throw new ArgumentException("Please clarify the record type you want to look up.");
            var known=BestCapability(question);
            if(known!=null && known.Id!=id) throw new ArgumentException("The model interpretation disagreed with the recognized record type. Please name the lookup explicitly.");
            // The model chooses only an allowlisted source. All filters and parameters are parsed and checked in C#.
            return CreateRequest(Normalize(question),context,id,today);
        }

        private static AssistantRequest CreateRequest(string q,AssistantContext context,string forcedCapability,DateTime today)
        {
            var reference=Reference(q);
            var capability=forcedCapability==null?BestCapability(q):AssistantCatalog.Find(forcedCapability);
            if(capability!=null && capability.Id=="ticket-email" && context.Module=="Admin" && !Has(q,@"\bticket\b")) capability=AssistantCatalog.Find("system-email");
            if(capability==null && reference!=null) capability=AssistantCatalog.Find(reference.Item1);
            bool thisRecord=Has(q,@"\b(this (record|ticket|invoice|item|set|repair)|selected (record|item|ticket))\b");
            if(thisRecord)
            {
                if(!context.SelectedRecordId.HasValue || string.IsNullOrEmpty(context.SelectedCapability))
                    throw new ArgumentException("Please select a saved record or give its reference number.");
                var noun=Regex.Match(q,@"\b(?:this|selected) (ticket|invoice|item|set|repair)\b");
                string expected=!noun.Success?null:noun.Groups[1].Value=="ticket"?"tickets":noun.Groups[1].Value=="repair"?"repairs":noun.Groups[1].Value+"s";
                if(expected!=null && expected!=context.SelectedCapability)
                    throw new ArgumentException("The selected record is a different type. Select a saved "+noun.Groups[1].Value+" or give its reference number.");
                capability=AssistantCatalog.Find(context.SelectedCapability);
                reference=Tuple.Create(context.SelectedCapability,context.SelectedRecordId.Value);
            }
            if(capability==null) return null;
            var request=new AssistantRequest { Capability=capability.Id,Operation=Has(q,@"\b(how many|how much|count|total|sum|ilan|ilang|bilang)\b")?AssistantOperation.Count:AssistantOperation.List };
            bool history=Has(q,@"\b(history|changes|activity|contents|line items)\b");
            if(capability.Id=="sets" && Has(q,@"\b(history|changes|activity)\b")) capability=AssistantCatalog.Find("ownership-history");
            if(capability.Id=="invoices" && Has(q,@"\b(history|changes|activity)\b") && !Has(q,@"\b(contents|line items)\b"))
                throw new ArgumentException("Invoice change history is not exposed as a verified lookup. Ask for invoice details or invoice contents.");
            request.Capability=capability.Id;
            if(history && capability.HistoryCapability!=null)
            { request.Capability=capability.HistoryCapability;capability=AssistantCatalog.Find(request.Capability);if(request.Operation!=AssistantOperation.Count) request.Operation=AssistantOperation.History; }
            else if(history && capability.ParentColumn!=null && request.Operation!=AssistantOperation.Count) request.Operation=AssistantOperation.History;
            if(reference!=null)
            {
                if((capability.ParentColumn=="TicketId" && reference.Item1=="tickets") || (capability.ParentColumn=="RepairTicketId" && reference.Item1=="repairs") ||
                    (capability.ParentColumn=="SetId" && (reference.Item1=="sets" || reference.Item1=="invoices")) || (capability.ParentColumn=="ItemId" && reference.Item1=="items")) request.ParentRecordId=reference.Item2;
                else if(capability.Id==reference.Item1 || (reference.Item1=="sets" && capability.Id=="invoices"))
                { request.RecordId=reference.Item2;request.Operation=AssistantOperation.Detail; }
                else throw new ArgumentException("That reference belongs to another record type. Please name the intended lookup.");
            }
            ApplyFilters(q,request,context,today,true);
            if(history && request.Capability=="borrows" && !Has(q,@"\b(open|returned)\b")) request.Status=null;
            return request;
        }

        private static Tuple<string,long> Reference(string q)
        {
            var matches=Regex.Matches(q,@"\b(TCK|RPR|REQ(?:UEST)?|SET)[-\s#]*(\d+)\b|\b(item|invoice|borrow|employee)\s*#\s*(\d+)\b",RegexOptions.IgnoreCase);
            if(matches.Count>1) throw new ArgumentException("Please look up one reference per question, or compare two named date periods.");
            if(matches.Count==0) return null;
            bool numeric=matches[0].Groups[3].Success;
            long id;if(!long.TryParse(matches[0].Groups[numeric?4:2].Value,out id) || id<=0 || id>int.MaxValue) throw new ArgumentException("Please provide a valid record number.");
            if(numeric) return Tuple.Create(matches[0].Groups[3].Value.ToLowerInvariant()+"s",id);
            string prefix=matches[0].Groups[1].Value.ToUpperInvariant();
            return Tuple.Create(prefix=="TCK"?"tickets":prefix=="RPR"?"repairs":prefix.StartsWith("REQ",StringComparison.Ordinal)?"requests":"sets",id);
        }

        private static void ApplyFilters(string q,AssistantRequest r,AssistantContext context,DateTime today,bool extractSearch)
        {
            var c=AssistantCatalog.Find(r.Capability);
            if(c.Id=="tickets" && Has(q,@"\bresolved\b") && !Has(q,@"\btemporary\b"))
                throw new ArgumentException("Ticket statuses distinguish Solved, Closed and Resolved (Temporary). Please specify which status you want.");
            string remaining=Regex.Replace(q,@"\b(TCK|RPR|REQ(?:UEST)?|SET)[-\s#]*\d+\b|\b(item|invoice|borrow|employee)\s*#\s*\d+\b"," ",RegexOptions.IgnoreCase);
            bool personal=Has(q,@"\b(my|mine|ako|akin|ko|assigned to me|created by me|borrowed by me)\b");
            if(personal) { r.Personal=true;r.Employee=null; }
            if(Has(q,@"\bassigned to me\b")) r.PersonalRelation="Assigned";
            else if(Has(q,@"\bcreated by me\b")) r.PersonalRelation="Created";
            string employee=Extract(ref remaining,@"\b(?:assigned to|handled by|borrowed by|for employee)\s+",true);
            if(employee!=null && employee!="me") { r.Employee=employee;r.Personal=false;r.PersonalRelation=null; }
            r.Department=Extract(ref remaining,@"\b(?:in |for )?(?:the )?department\s+",true) ?? r.Department;
            r.Branch=Extract(ref remaining,@"\b(?:in |for )?(?:the )?branch\s+",true) ?? r.Branch;
            r.Company=Extract(ref remaining,@"\b(?:in |for )?(?:the )?company\s+",true) ?? r.Company;
            var group=Regex.Match(remaining,@"\b(?:group(?:ed)? by|by|per)\s+(status|priority|employee|department|branch|company|category|condition|month|subtype|parent tag|vendor|role|action|entity type)\b",RegexOptions.IgnoreCase);
            if(group.Success)
            {
                var groups=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { {"status","Status"},{"priority","Priority"},{"employee","Employee"},{"department","Department"},{"branch","Branch"},{"company","Company"},{"category","Category"},{"condition","Condition"},{"month","Month"},{"subtype","SubType"},{"parent tag","ParentTag"},{"vendor","Vendor"},{"role","Role"},{"action","Action"},{"entity type","EntityType"} };
                r.GroupBy=groups[group.Groups[1].Value];r.Operation=AssistantOperation.Summary;remaining=remaining.Remove(group.Index,group.Length);
            }
            if(c.Id=="item-repairs") remaining=Regex.Replace(remaining,@"\brepaired items\b"," ",RegexOptions.IgnoreCase);
            var statuses=new[] { "Resolved (Temporary)","Forwarded to Repair","In Progress","AwaitingParts","WaitingParts","WaitingDiagnosis","CannotRepair","Unrepairable","Diagnosing","Repairing","Completed","Reopened","Escalated","Rejected","Approved","Cancelled","Returned","Unfulfilled","Pending","Solved","Closed","Testing","Waiting","Inactive","Active","Open","Used","Repaired" };
            var matchingStatuses=statuses.Where(status=>Has(remaining,@"(?<![\p{L}\p{Nd}])"+Regex.Escape(status)+@"(?![\p{L}\p{Nd}])")).ToList();
            if(matchingStatuses.Count>1) throw new ArgumentException("Please ask for one status at a time, name two complete lookups, or request a summary by status.");
            if(matchingStatuses.Count==1)
            {
                r.Status=matchingStatuses[0];remaining=Regex.Replace(remaining,@"(?<![\p{L}\p{Nd}])"+Regex.Escape(r.Status)+@"(?![\p{L}\p{Nd}])"," ",RegexOptions.IgnoreCase);
            }
            if(c.Id=="license-review" && r.Status=="Pending") r.Status="Pending review";
            if(c.Id=="exchanges" && r.Status=="Unfulfilled") {r.Status=null;r.OutstandingOnly=true;}
            if(r.Capability=="tickets" && Has(remaining,@"\b(unsolved|unresolved)\b")) {r.Status="Open";remaining=Regex.Replace(remaining,@"\b(unsolved|unresolved)\b"," ");}
            if(r.Capability=="requests" && Has(q,@"\b(rejected|cancelled|closed)\b")) r.OutstandingOnly=false;
            if(r.Capability=="requests" && (r.Status=="Open" || r.Status=="Pending" || r.Status=="Unfulfilled" || Has(q,@"\b(outstanding|partially fulfilled)\b")))
            { r.OutstandingOnly=true;r.Status=null; }
            if(r.Capability=="borrows" && r.Status==null && !Has(q,@"\bhistory\b")) r.Status="Open";
            foreach(string priority in new[] { "Critical","High","Medium","Low" })
                if(Has(remaining,@"\b"+priority+@"(?: priority)?\b") && !(priority=="Low" && Has(remaining,@"\blow stock\b")))
                { r.Priority=priority;remaining=Regex.Replace(remaining,@"\b"+priority+@"(?: priority)?\b"," ",RegexOptions.IgnoreCase);break; }
            if(Has(remaining,@"\bbrand new\b")) {r.Condition="Brand New";remaining=Regex.Replace(remaining,@"\bbrand new\b"," ");}
            else if(Has(remaining,@"\brefilled\b") && r.Capability=="cartridges") {r.Condition="Refilled";remaining=Regex.Replace(remaining,@"\brefilled\b"," ");}
            if(r.Capability=="consumables")
            {
                if(Has(remaining,@"\btoner\b")) r.Category="Toner";
                else if(Has(remaining,@"\bink\b")) r.Category="Ink";
                else if(Has(remaining,@"\bprint\s*head\b")) r.Category="Print";
            }
            if(r.Capability=="items" && Has(remaining,@"\bip phones?\b"))
            {r.Category="IP Phone";remaining=Regex.Replace(remaining,@"\bip phones?\b"," ",RegexOptions.IgnoreCase);}
            else if(r.Capability=="items" && Has(remaining,@"\b(?:(?:cell|mobile)\s*phones?|smartphones?|phones?)\b"))
            {
                r.Category="Cellphone";
                remaining=Regex.Replace(remaining,@"\b(?:(?:cell|mobile)\s*phones?|smartphones?|phones?)\b"," ",RegexOptions.IgnoreCase);
            }
            if(new[] {"items","consumables","cartridges"}.Contains(r.Capability))
            {
                bool stock=Has(q,@"\b(in stock|stock|available|do (?:we|i) have)\b") || r.Operation==AssistantOperation.Count;
                if(stock && !Has(q,@"\b(low stock|lowest stock|running out)\b")) r.InStockOnly=true;
                if(stock && Has(q,@"\b(in stock|stock|available)\b")) {r.Personal=false;r.PersonalRelation=null;}
            }
            if(Has(remaining,@"\b(exclude|excluding|without|not) archived\b")) {r.IncludeArchived=false;r.ArchivedOnly=false;remaining=Regex.Replace(remaining,@"\b(exclude|excluding|without|not) archived\b"," ");}
            else if(Has(remaining,@"\barchived\b")) { r.IncludeArchived=true;r.ArchivedOnly=!Has(remaining,@"\b(including|include|with) archived\b"); }
            if(Has(q,@"\b(low stock|lowest stock|running out)\b")) r.LowestStock=true;
            if(Has(q,@"\b(created|submitted|posted)\b")) r.DateField="CreatedAt";
            else if(r.Capability=="tickets" && r.Status!=null && r.Status!="Open") r.DateField="StatusChangedAt";
            else if(Has(q,@"\b(returned|return date)\b") && r.Capability=="borrows") r.DateField="ReturnedAt";
            else if(Has(q,@"\bcompleted\b") && r.Capability=="repairs") r.DateField="CompletedAt";
            bool dates=ApplyDates(ref remaining,r,today);
            if(r.Capability=="tickets" && r.GroupBy=="Month" && r.Status=="Solved" && !Has(q,@"\b(created|submitted|posted)\b")) r.DateField="SolvedAt";
            if(Has(q,@"\b(expired|past expiry)\b") && (r.Capability=="renewals" || r.Capability=="warranties"))
            { r.DateField="EndDate";r.FromLocal=null;r.ToLocalExclusive=today;dates=true; }
            else if(!dates && (r.Capability=="renewals" || r.Capability=="warranties") && Has(q,@"\b(expir\w*|upcoming)\b"))
            {r.DateField="EndDate";r.FromLocal=today;r.ToLocalExclusive=today.AddDays(91);}
            if(!extractSearch) return;
            remaining=Regex.Replace(remaining,@"\b(?:how many|how much|do you know|who is|tell me about|what is|what are|status of|what about)\b"," ",RegexOptions.IgnoreCase);
            string noise="record records list show count sum total summary compare comparison versus vs only just all dates today yesterday this last next month week year currently created submitted posted assigned handled priority grouped per history changes activity contents line details lookup status of outstanding partially fulfilled archived including include expiry expiration expire expires expiring expired upcoming within past soon condition highest lowest low running out personal";
            string domainNoise=r.Capability=="items"?"stock stocks inventory item items equipment product products available quantity quantities unit units level levels":
                r.Capability=="employees"?AiContextProviders.EmployeeNoise:
                r.Capability=="policies"?AiContextProviders.PolicyNoise:
                string.Join(" ",c.Aliases.Where(a=>!new[] {"keyboard","laptop","mouse","monitor","memory","ram","printer","battery","cable"}.Contains(a)).SelectMany(a=>a.Split(' ')));
            var terms=AiIntentRouter.SearchTokens(remaining,noise+" "+domainNoise,true);
            r.Search=string.Join(" ",terms);
        }

        private static string Extract(ref string text,string prefix,bool stopAtFilter)
        {
            if(prefix.Contains("department") || prefix.Contains("branch") || prefix.Contains("company")) prefix=@"(?<!by )(?<!per )"+prefix;
            string ending=@"(?=\s+(?:in |for )?(?:department|branch|company|with |from |since |before |after |today\b|yesterday\b|this |last |next |by |per |high\b|low\b|critical\b|pending\b|solved\b|only\b)|[?.!]|$)";
            var match=Regex.Match(text,prefix+"(?:\"([^\"]+)\"|'([^']+)'|(.+?))"+ending,RegexOptions.IgnoreCase);
            if(!match.Success) return null;
            string value=Enumerable.Range(1,3).Select(i=>match.Groups[i].Value).FirstOrDefault(s=>s.Length>0);
            text=text.Remove(match.Index,match.Length);return value==null?null:value.Trim();
        }

        internal static bool ApplyDates(ref string q,AssistantRequest request,DateTime today)
        {
            DateTime from,to;
            const string months="january|february|march|april|may|june|july|august|september|october|november|december";
            var named=Regex.Match(q,@"\b(?:in|during|for)\s+("+months+@")(?:\s+(\d{4}))?\b|\b("+months+@")\s+(\d{4})\b",RegexOptions.IgnoreCase);
            if(named.Success)
            {
                string month=named.Groups[named.Groups[1].Success?1:3].Value;
                string year=named.Groups[named.Groups[1].Success?2:4].Value;
                int y=year.Length==0?today.Year:int.Parse(year,CultureInfo.InvariantCulture);
                if(y<1753 || y>9998) throw new ArgumentException("Please choose a supported calendar year.");
                from=new DateTime(y,Array.IndexOf(months.Split('|'),month.ToLowerInvariant())+1,1);
                request.FromLocal=from;request.ToLocalExclusive=from.AddMonths(1);q=q.Remove(named.Index,named.Length);return true;
            }
            var explicitRange=Regex.Match(q,@"\b(?:from|between)\s+(\d{4}-\d{2}-\d{2})\s+(?:to|and|through)\s+(\d{4}-\d{2}-\d{2})\b");
            if(explicitRange.Success)
            {
                if(!DateTime.TryParseExact(explicitRange.Groups[1].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out from) ||
                    !DateTime.TryParseExact(explicitRange.Groups[2].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out to) || from>to)
                    throw new ArgumentException("Please use a valid date range such as 'from 2026-10-01 to 2026-10-15'.");
                request.FromLocal=from;request.ToLocalExclusive=to.AddDays(1);q=q.Remove(explicitRange.Index,explicitRange.Length);return true;
            }
            var relative=Regex.Match(q,@"\b(today|yesterday|this month|last month|this week|last week|this year|last year)\b");
            if(relative.Success)
            {
                string period=relative.Value;
                if(period=="today" || period=="yesterday") {from=period=="today"?today:today.AddDays(-1);to=from.AddDays(1);}
                else if(period.EndsWith("month",StringComparison.Ordinal)) {from=new DateTime(today.Year,today.Month,1);if(period.StartsWith("last",StringComparison.Ordinal)) from=from.AddMonths(-1);to=from.AddMonths(1);}
                else if(period.EndsWith("year",StringComparison.Ordinal)) {from=new DateTime(today.Year-(period.StartsWith("last",StringComparison.Ordinal)?1:0),1,1);to=from.AddYears(1);}
                else {from=today.AddDays(-(((int)today.DayOfWeek+6)%7));if(period.StartsWith("last",StringComparison.Ordinal)) from=from.AddDays(-7);to=from.AddDays(7);}
                request.FromLocal=from;request.ToLocalExclusive=to;q=q.Remove(relative.Index,relative.Length);return true;
            }
            var days=Regex.Match(q,@"\b(within|next|last|past)\s+(\d+)\s+days?\b");
            if(days.Success)
            {
                int count;if(!int.TryParse(days.Groups[2].Value,out count) || count<1 || count>3650) throw new ArgumentException("Please choose a day window between 1 and 3650.");
                bool past=days.Groups[1].Value=="past" || days.Groups[1].Value=="last";
                request.FromLocal=past?today.AddDays(1-count):today;request.ToLocalExclusive=past?today.AddDays(1):today.AddDays(count+1);
                q=q.Remove(days.Index,days.Length);return true;
            }
            var bound=Regex.Match(q,@"\b(since|before|after|on)\s+(\d{4}-\d{2}-\d{2})\b");
            if(bound.Success)
            {
                if(!DateTime.TryParseExact(bound.Groups[2].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out from)) throw new ArgumentException("Please provide a valid ISO date.");
                if(bound.Groups[1].Value=="before") {request.FromLocal=null;request.ToLocalExclusive=from;}
                else {request.FromLocal=bound.Groups[1].Value=="after"?from.AddDays(1):from;request.ToLocalExclusive=bound.Groups[1].Value=="on"?from.AddDays(1):(DateTime?)null;}
                q=q.Remove(bound.Index,bound.Length);return true;
            }
            if(Regex.IsMatch(q,@"\b\d{4}-\d{2}-\d{2}\b") || Has(q,@"\b(?:this|last|next|past)\s+(?:\d+\s+)?(?:days?|weeks?|months?|years?)\b"))
                throw new ArgumentException("Please use a supported date range, such as 'this month', 'last 30 days', or 'from 2026-10-01 to 2026-10-15'.");
            return false;
        }
    }
}
