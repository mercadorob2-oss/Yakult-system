using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    internal static class AssistantEnglish
    {
        internal const string Welcome="Hi, I’m Yakumi. What would you like to check? You can ask about records or get help with this page.";
        internal const string Stopped="Stopped. You can ask another question whenever you’re ready.";
        internal const string Unavailable="I couldn’t load the latest data. Please try again in a moment.";
        internal static string SmallTalk(string question)
        {
            string text=(question ?? "").Trim();
            if(text.Length>AiIntentRouter.MaxQuestionLength) return null;
            if(Regex.IsMatch(text,@"^(hi|hey|hello|yo|good morning|good afternoon|good evening)( yakumi)?[!.?]*$",RegexOptions.IgnoreCase))
                return "Hi! What would you like to check today? You can ask about records or get help with a page.";
            if(Regex.IsMatch(text,@"^(thanks|thank you|thanks yakumi|thank you yakumi|ty|cheers)[!.?]*$",RegexOptions.IgnoreCase))
                return "You’re welcome! I’m here if you need anything else.";
            return null;
        }
        // Explicit nouns avoid guessing singular forms from page titles such as IT tickets or Warranty.
        private static readonly Dictionary<string,string[]> Nouns=new[] {
            "items|item|items", "inventory|inventory movement|inventory movements", "assets|asset|assets",
            "warranties|warranty|warranties", "sets|set|sets", "set-items|set item|set items",
            "requests|request|requests", "consumables|consumable model|consumable models", "cartridges|cartridge model|cartridge models",
            "refills|refill batch|refill batches", "exchanges|cartridge exchange|cartridge exchanges", "approvals|request authorization|request authorizations",
            "tickets|IT ticket|IT tickets", "ticket-history|ticket history entry|ticket history entries", "ticket-email|ticket email log entry|ticket email log entries",
            "borrows|borrow record|borrow records", "repairs|repair ticket|repair tickets", "repair-history|repair history entry|repair history entries",
            "repair-parts|repair part|repair parts", "invoices|invoice|invoices", "invoice-items|invoice line item|invoice line items",
            "renewals|renewal|renewals", "accounts|user account|user accounts", "roles|role membership|role memberships",
            "audit|activity entry|activity entries", "policies|published company policy|published company policies", "receipts|receipt|receipts",
            "employees|employee|employees", "companies|company|companies", "branches|branch|branches", "departments|department|departments",
            "vendors|vendor|vendors", "categories|item category|item categories", "conditions|item condition|item conditions",
            "archive|archived record|archived records", "invoice-preparation|invoice sub group|invoice sub groups",
            "item-history|item movement entry|item movement entries", "ownership-history|ownership history entry|ownership history entries",
            "set-updates|set item update|set item updates", "item-repairs|item repair entry|item repair entries",
            "nonlicensed|non-licensed invoice line item|non-licensed invoice line items", "license-review|invoice license review item|invoice license review items",
            "department-accounts|department account|department accounts", "holidays|holiday|holidays", "permissions|individual permission override|individual permission overrides",
            "system-email|system email log entry|system email log entries", "user-positions|user position entry|user position entries",
            "approver-titles|approver title|approver titles", "branch-acronyms|branch acronym|branch acronyms",
            "department-acronyms|department acronym|department acronyms", "role-portals|role portal grant|role portal grants",
            "user-portals|individual portal override|individual portal overrides", "role-permissions|role permission override|role permission overrides"
        }.Select(s=>s.Split('|')).ToDictionary(p=>p[0],p=>new[] {p[1],p[2]});
        internal static string Noun(string capability,decimal count)
        {string[] pair;return Nouns.TryGetValue(capability,out pair)?pair[count==1?0:1]:count==1?"record":"records";}
        internal static string Count(decimal count,string singular,string plural)
        {return count.ToString("0.####",CultureInfo.InvariantCulture)+" "+(count==1?singular:plural);}
        internal static string SearchLabel(string search)
        {return Regex.Replace((search ?? "").Trim('"'),@"\b(?=[a-zA-Z0-9-]*[a-zA-Z])(?=[a-zA-Z0-9-]*\d)[a-zA-Z0-9-]+\b",m=>m.Value.ToUpperInvariant());}
        internal static string Answer(AssistantResult result)
        {
            var r=result.Request;string noun=Noun(r.Capability,result.TotalRecords);
            if(r.Operation==AssistantOperation.Detail && result.TotalRecords>0)
                return "Here are the details for "+Noun(r.Capability,1)+(r.RecordId.HasValue?" #"+r.RecordId:"")+".";
            if(r.Operation==AssistantOperation.Detail && r.RecordId.HasValue && result.TotalRecords==0)
                return "I couldn’t find "+Noun(r.Capability,1)+" #"+r.RecordId+" with those filters.";
            if(r.InStockOnly && result.Totals.ContainsKey("Units"))
            {
                decimal units=result.Totals["Units"];
                string target=string.IsNullOrWhiteSpace(r.Search)?r.Category:SearchLabel(r.Search);
                return (units==1?"There is ":"There are ")+units.ToString("0.####",CultureInfo.InvariantCulture)+" "+
                    (string.IsNullOrWhiteSpace(r.Condition)?"":r.Condition.ToLowerInvariant()+" ")+
                    (string.IsNullOrWhiteSpace(target)?"":"“"+target+"” ")+(units==1?"unit":"units")+" in stock"+Ownership(r)+OrganizationScope(r)+".";
            }
            string status=r.Status ?? AssistantCatalog.Find(r.Capability)?.DefaultStatus;
            string subject=
                (string.IsNullOrWhiteSpace(status)?"":status.ToLowerInvariant()+" ")+noun+
                (r.OutstandingOnly?" with outstanding quantities":"")+
                (string.IsNullOrWhiteSpace(r.Search)?"":" matching “"+SearchLabel(r.Search)+"”");
            if(result.TotalRecords==0) return "I couldn’t find any "+subject+Ownership(r)+OrganizationScope(r)+".";
            bool companySubject=!string.IsNullOrWhiteSpace(r.Company) && !r.Personal && string.IsNullOrWhiteSpace(r.Employee);
            return (companySubject?r.Company+" has ":"I found ")+result.TotalRecords.ToString("N0",CultureInfo.InvariantCulture)+" "+subject+
                Ownership(r)+OrganizationScope(r,companySubject)+".";
        }
        private static string OrganizationScope(AssistantRequest request,bool omitCompany=false)
        {
            return (omitCompany || string.IsNullOrWhiteSpace(request.Company)?"":" in company “"+request.Company+"”")+
                (string.IsNullOrWhiteSpace(request.Branch)?"":" at branch “"+request.Branch+"”")+
                (string.IsNullOrWhiteSpace(request.Department)?"":" in department “"+request.Department+"”");
        }
        private static string Ownership(AssistantRequest r)
        {
            if(!string.IsNullOrWhiteSpace(r.Employee)) return " for "+r.Employee;
            return !r.Personal?"":r.PersonalRelation=="Assigned"?" assigned to you":r.PersonalRelation=="Created"?" created by you":" for your account";
        }
        internal static string Period(AssistantRequest r)
        {
            if(!r.FromLocal.HasValue && !r.ToLocalExclusive.HasValue) return null;
            string field=r.DateField ?? AssistantCatalog.Find(r.Capability)?.DefaultDateField ?? "Date";
            string measure=field=="StatusChangedAt"?"status changes":field=="SolvedAt"?"recorded solution dates":field=="ReturnedAt"?"return dates":
                field=="CompletedAt"?"completion dates":field=="EndDate"?"expiry dates":field=="StartDate"?"start dates":field=="CreatedAt"?"creation dates":AssistantPresentationFormatter.FieldLabel(field).ToLowerInvariant();
            if(field=="CreatedAt")
            {
                if(r.Capability=="invoices") measure="invoice dates";
                else if(r.Capability=="inventory") measure="posting dates";
                else if(r.Capability=="ticket-email" || r.Capability=="system-email") measure="sent dates";
                else if(r.Capability=="archive") measure="archive dates";
                else if(r.Capability=="item-history") measure="movement dates";
                else if(r.Capability=="ownership-history") measure="ownership change dates";
                else if(r.Capability=="ticket-history" || r.Capability=="repair-history") measure="recorded change dates";
            }
            return "This covers "+measure+(r.FromLocal.HasValue?" from "+r.FromLocal.Value.ToString("MMMM d, yyyy",CultureInfo.InvariantCulture):"")+
                (r.ToLocalExclusive.HasValue?" through "+r.ToLocalExclusive.Value.AddTicks(-1).ToString("MMMM d, yyyy",CultureInfo.InvariantCulture):" onward")+".";
        }
        internal static string EmptyHint(AssistantRequest request)
        {
            if(request.RecordId.HasValue) return "Please check the record number, or try a different search.";
            if(request.FromLocal.HasValue || request.ToLocalExclusive.HasValue)
                return request.Operation==AssistantOperation.Detail || request.Operation==AssistantOperation.History?
                    "You can try another period while keeping your other filters.":"Try another period, or check all dates with the same filters.";
            return "You can check the name, change a filter, or ask about another record type.";
        }
    }

    internal sealed class AssistantField
    {
        internal string Label;
        internal string Value;
        internal AssistantField(string label,string value) {Label=label;Value=value;}
    }
    internal sealed class AssistantRecordPreview
    {
        internal string Title;
        internal bool IsDetail;
        internal readonly List<AssistantField> Fields=new List<AssistantField>();
    }
    internal sealed class AssistantFollowUp
    {
        internal string Label {get;private set;}
        private readonly AssistantRequest _request;
        internal AssistantClarificationChoice Choice {get;}
        internal AssistantRequest Request {get {return _request?.Copy();}}
        internal AssistantFollowUp(string label,AssistantRequest request) {Label=label;_request=request.Copy();}
        internal AssistantFollowUp(AssistantClarificationChoice choice) {Label=choice.Label;Choice=choice;}
    }
    internal sealed class AssistantPresentation
    {
        internal AssistantOutcome Outcome;
        internal string Headline;
        internal string Body;
        internal string Explanation;
        internal string PageCaption;
        internal readonly List<string> Filters=new List<string>();
        internal readonly List<string> Qualifications=new List<string>();
        internal readonly List<AssistantField> Metrics=new List<AssistantField>();
        internal readonly List<AssistantField> Details=new List<AssistantField>();
        internal readonly List<AssistantRecordPreview> Records=new List<AssistantRecordPreview>();
        internal readonly List<AssistantPresentation> Children=new List<AssistantPresentation>();
        internal readonly List<AssistantFollowUp> Actions=new List<AssistantFollowUp>();
        internal string CopyText {get {return AssistantPresentationFormatter.CopyText(this);}}
    }

    /// <summary>Deterministic presentation of typed facts. Never reads facts from the legacy Text response.</summary>
    internal static class AssistantPresentationFormatter
    {
        internal const int PreviewCount=3;
        internal static AssistantPresentation Format(AssistantResult result)
        {
            var view=new AssistantPresentation {Outcome=result.Outcome,Explanation=result.ExplanationText};
            foreach(var citation in result.GuideCitations)
                view.Details.Add(new AssistantField("Guide",citation.Source+" ["+citation.Id+"]"));
            if(result.Children.Count>0)
            {
                view.Headline=result.IsComparison?"Here’s the comparison":"Here’s what I found";
                foreach(var child in result.Children) view.Children.Add(Format(child));
                if(result.IsComparison)
                    for(int i=0;i<view.Children.Count;i++) view.Children[i].Headline="Period "+(i+1)+": "+view.Children[i].Headline;
                if(result.IsComparison && result.Children.Count==2 && result.Children.All(c=>c.IsLive))
                {
                    var first=result.Children[0];var second=result.Children[1];
                    if(first.Request.Capability!=second.Request.Capability)
                        view.Qualifications.Add("These sources measure different records; their totals are shown separately.");
                    else
                    {
                        view.Qualifications.Add("The differences below are period 2 minus period 1.");
                        view.Metrics.Add(new AssistantField("Matching records difference",Signed(second.TotalRecords-first.TotalRecords)));
                        foreach(var metric in first.Totals.Keys.Intersect(second.Totals.Keys))
                            view.Metrics.Add(new AssistantField(MetricLabel(metric,first.Request.Capability)+" difference",Signed(second.Totals[metric]-first.Totals[metric],metric)));
                    }
                }
                return view;
            }
            var request=result.Request;
            if(!result.IsLive || request==null)
            {
                view.Headline=OutcomeTitle(result.Outcome);
                view.Body=result.MessageText ?? (result.ExplanationText==null?result.Text:null);
                foreach(var choice in result.ClarificationChoices) view.Actions.Add(new AssistantFollowUp(choice));
                return view;
            }
            var capability=AssistantCatalog.Find(request.Capability);
            bool stock=request.InStockOnly && result.Totals.ContainsKey("Units");
            view.Headline=AssistantEnglish.Answer(result);
            view.Body=AssistantEnglish.Period(request);
            if(result.TotalRecords==0) view.Body=(view.Body==null?"":view.Body+"\n\n")+AssistantEnglish.EmptyHint(request);

            AddFilter(view,"Type",capability?.Title ?? request.Capability);
            AddFilter(view,"Search",request.Search);AddFilter(view,"Category",request.Category);
            AddFilter(view,"Status",request.Status ?? capability?.DefaultStatus);AddFilter(view,"Priority",request.Priority);
            AddFilter(view,"Condition",request.Condition);AddFilter(view,"Employee",request.Employee);
            AddFilter(view,"Department",request.Department);AddFilter(view,"Branch",request.Branch);AddFilter(view,"Company",request.Company);
            if(request.Personal) view.Filters.Add(request.PersonalRelation=="Assigned"?"Assigned to me":request.PersonalRelation=="Created"?"Created by me":"My records");
            if(request.RecordId.HasValue) view.Filters.Add("Record #"+request.RecordId);
            if(request.ParentRecordId.HasValue) view.Filters.Add("Parent record #"+request.ParentRecordId);
            if(request.OutstandingOnly) view.Filters.Add("Outstanding quantities only");
            if(stock) view.Filters.Add("Positive available stock");
            if(capability?.ArchiveColumn!=null) view.Filters.Add(request.ArchivedOnly?"Archived only":request.IncludeArchived?"Includes archived":"Excludes archived");
            if(request.FromLocal.HasValue || request.ToLocalExclusive.HasValue)
                view.Details.Add(new AssistantField("Date filter",FieldLabel(request.DateField ?? capability?.DefaultDateField ?? "Date")+": "+
                    (request.FromLocal.HasValue?request.FromLocal.Value.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture):"Beginning")+" to "+
                    (request.ToLocalExclusive.HasValue?request.ToLocalExclusive.Value.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+" (exclusive)":"Latest")+" · "+result.TimeZone.Id));
            else view.Filters.Add("All dates");
            view.Qualifications.AddRange(result.Qualifications);
            if(request.Status=="Open" && request.Capability=="tickets")
                view.Qualifications.Add("Open includes tickets that aren’t solved, closed, temporarily resolved or cancelled.");
            if(request.DateField=="StatusChangedAt" && request.Capability=="tickets" && request.Status!=null && (request.FromLocal.HasValue || request.ToLocalExclusive.HasValue))
                view.Qualifications.Add("These tickets are currently "+request.Status.ToLowerInvariant()+" and changed to that status during this period.");
            if(stock || request.Operation==AssistantOperation.Detail)
                view.Metrics.Add(new AssistantField("Matching records",result.TotalRecords.ToString("N0",CultureInfo.InvariantCulture)));
            foreach(var metric in result.Totals)
                if(!stock || metric.Key!="Units") view.Metrics.Add(new AssistantField(MetricLabel(metric.Key,request.Capability),Number(metric.Key,metric.Value)));
            if(request.Operation==AssistantOperation.Summary) view.Metrics.Add(new AssistantField("Matching groups",result.TotalGroups.ToString("N0",CultureInfo.InvariantCulture)));
            foreach(var row in result.Rows) view.Records.Add(Record(row,request,capability,result.TimeZone));
            if(request.Operation!=AssistantOperation.Count)
                view.PageCaption="Page "+(request.Page+1)+" · "+AssistantEnglish.Count(view.Records.Count,request.Operation==AssistantOperation.Summary?"loaded group":"loaded record",request.Operation==AssistantOperation.Summary?"loaded groups":"loaded records")+
                    (view.Records.Count==0 && result.TotalRecords>0?". No records on this page.":"");
            if(request.Operation==AssistantOperation.Count && result.TotalRecords>0)
            {var next=request.Copy();next.Operation=AssistantOperation.List;next.Page=0;view.Actions.Add(new AssistantFollowUp("Show "+ActionNoun(request.Capability),next));}
            if(request.Page>0)
            {var previous=request.Copy();previous.Page--;view.Actions.Add(new AssistantFollowUp("Previous page",previous));}
            if(result.HasNextPage)
            {var next=request.Copy();next.Page++;view.Actions.Add(new AssistantFollowUp("Next page",next));}
            AddSuggestions(view,result,capability);
            view.Details.Add(new AssistantField("Source",result.Source ?? capability?.Source ?? "Read-only database lookup"));
            view.Details.Add(new AssistantField("Complete scope",(result.AppliedScope ?? "All accessible matching records")+"; "+
                (request.FromLocal.HasValue || request.ToLocalExclusive.HasValue?"dates as filtered":"all dates")+"; existing account, page and category permissions apply"));
            view.Details.Add(new AssistantField("Retrieved",TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(result.RetrievedAtUtc,DateTimeKind.Utc),result.TimeZone).ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+" · "+result.TimeZone.Id));
            return view;
        }
        private static string ActionNoun(string capability)
        {string noun=capability=="tickets"?"tickets":capability=="repairs"?"repairs":AssistantEnglish.Noun(capability,2);return noun.Length<=22?noun:"records";}
        private static void AddSuggestions(AssistantPresentation view,AssistantResult result,AssistantCapability capability)
        {
            var request=result.Request;
            if(capability==null || request.Operation==AssistantOperation.Detail || request.Operation==AssistantOperation.History || request.RecordId.HasValue) return;
            if(result.TotalRecords==0)
            {
                if(request.FromLocal.HasValue || request.ToLocalExclusive.HasValue)
                {
                    var allDates=request.Copy();allDates.FromLocal=null;allDates.ToLocalExclusive=null;allDates.Page=0;
                    view.Actions.Add(new AssistantFollowUp("Check all dates",allDates));
                }
                return;
            }
            // Offer only supported refinements. Each button owns the original scope; nothing runs until clicked.
            if(view.Actions.Count<3 && request.Capability=="tickets" && string.IsNullOrWhiteSpace(request.Status))
            {
                var open=request.Copy();open.Status="Open";open.Page=0;
                view.Actions.Add(new AssistantFollowUp("Only open",open));
            }
            if(view.Actions.Count<3 && request.Operation!=AssistantOperation.Summary && capability.GroupColumns.Contains("Department") && string.IsNullOrWhiteSpace(request.Department))
            {
                var departments=request.Copy();departments.Operation=AssistantOperation.Summary;departments.GroupBy="Department";departments.Page=0;
                view.Actions.Add(new AssistantFollowUp("By department",departments));
            }
        }
        private static AssistantRecordPreview Record(IReadOnlyDictionary<string,object> row,AssistantRequest request,AssistantCapability capability,TimeZoneInfo zone)
        {
            var record=new AssistantRecordPreview {IsDetail=request.Operation==AssistantOperation.Detail};
            string titleKey=request.Operation==AssistantOperation.Summary?"GroupValue":row.ContainsKey("Label")?"Label":"Id";
            object title;row.TryGetValue(titleKey,out title);
            record.Title=request.Operation==AssistantOperation.Summary?FieldLabel(request.GroupBy)+": "+Value(titleKey,title,capability,zone):
                titleKey=="Id"?"Record #"+Value(titleKey,title,capability,zone):Value(titleKey,title,capability,zone);
            string[] preferred=request.Operation==AssistantOperation.Summary?new[] {"Records","Group_Units","Group_AmountDue","Group_Amount"}:
                new[] {"items","consumables","cartridges"}.Contains(request.Capability)?new[] {"Units","Model","Category","Condition","Status"}:
                new[] {"Status","AmountDue","Amount","Priority","Employee","Department","Category","Units","CreatedAt"};
            var fields=row.Where(f=>f.Key!=titleKey && !new[] {"CreatedBy","AssignedUserId","EmployeeId","Active","Archived"}.Contains(f.Key));
            if(!record.IsDetail) fields=fields.OrderBy(f=>Array.IndexOf(preferred,f.Key)<0?preferred.Length:Array.IndexOf(preferred,f.Key));
            foreach(var field in fields)
                record.Fields.Add(new AssistantField(field.Key.StartsWith("Group_",StringComparison.Ordinal)?MetricLabel(field.Key.Substring(6),request.Capability):
                    field.Key=="Records"?"Matching records":field.Key=="Units"?MetricLabel("Units",request.Capability):FieldLabel(field.Key),Value(field.Key,field.Value,capability,zone)));
            return record;
        }
        private static string Value(string key,object value,AssistantCapability capability,TimeZoneInfo zone)
        {
            if(value==null || value==DBNull.Value) return "Not recorded";
            if(value is DateTime)
            {
                var date=(DateTime)value;
                if(capability!=null && capability.UtcDateColumns.Contains(key)) date=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(date,DateTimeKind.Utc),zone);
                return date.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);
            }
            if(value is decimal || value is double || value is float) return Number(key,Convert.ToDecimal(value,CultureInfo.InvariantCulture));
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }
        internal static string FieldLabel(string key) {return Regex.Replace(key ?? "Field","([a-z])([A-Z])","$1 $2").Replace('_',' ');}
        private static string MetricLabel(string key,string capability)
        {return key=="Units" && new[] {"items","consumables","cartridges"}.Contains(capability)?"Stock units":AssistantQueryEngine.MetricLabel(key,capability);}
        private static string Number(string key,decimal value) {return AssistantQueryEngine.FormatNumber(key,value);}
        private static string Signed(decimal value,string key="Records") {return (value>0?"+":"")+Number(key,value);}
        private static void AddFilter(AssistantPresentation view,string label,string value) {if(!string.IsNullOrWhiteSpace(value)) view.Filters.Add(label+": "+value);}
        private static string OutcomeTitle(AssistantOutcome outcome)
        {
            switch(outcome)
            {
                case AssistantOutcome.Denied:return "Let’s check your access";
                case AssistantOutcome.Unavailable:return "I couldn’t load that just now";
                case AssistantOutcome.Cancelled:return "Reply cancelled";
                case AssistantOutcome.Clarification:return "A little more detail, please";
                case AssistantOutcome.NoMatches:return "No matches";
                default:return "Here to help";
            }
        }
        internal static string CopyText(AssistantPresentation view)
        {
            var text=new StringBuilder(view.Headline);
            if(view.Filters.Count>0) text.Append("\n"+string.Join(" · ",view.Filters));
            foreach(var note in view.Qualifications) text.Append("\n"+note);
            if(!string.IsNullOrWhiteSpace(view.Body)) text.Append("\n\n"+view.Body);
            if(view.Actions.Any(a=>a.Choice!=null)) text.Append("\nChoices: "+string.Join(" · ",view.Actions.Where(a=>a.Choice!=null).Select(a=>a.Label)));
            foreach(var metric in view.Metrics) text.Append("\n"+metric.Label+": "+metric.Value);
            if(view.PageCaption!=null) text.Append("\n"+view.PageCaption);
            foreach(var record in view.Records)
            {text.Append("\n\n"+record.Title);foreach(var field in record.Fields) text.Append("\n"+field.Label+": "+field.Value);}
            for(int i=0;i<view.Children.Count;i++) text.Append("\n\nResult "+(i+1)+"\n"+CopyText(view.Children[i]));
            if(!string.IsNullOrWhiteSpace(view.Explanation)) text.Append("\n\n"+view.Explanation);
            foreach(var detail in view.Details) text.Append("\n"+detail.Label+": "+detail.Value);
            // Plain readable copy of the supported inline emphasis syntax.
            return text.ToString().Replace("**","");
        }
    }
}
