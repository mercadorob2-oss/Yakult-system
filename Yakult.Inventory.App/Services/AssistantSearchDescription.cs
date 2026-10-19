using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Deterministic in-flight wording from validated request filters, with no database facts.</summary>
    internal static class AssistantSearchDescription
    {
        internal static string Describe(AssistantRequest request,AssistantContext context)
        {
            var parts=new List<string>();bool stock=AssistantProductMatching.IsStock(request.Capability);
            string search=AssistantEnglish.SearchLabel(request.Search);
            string subject=request.InStockOnly && stock && request.Operation!=AssistantOperation.Detail?"available "+(search.Length==0?"":"“"+search+"” ")+"stock":
                AssistantEnglish.Noun(request.Capability,request.Operation==AssistantOperation.Detail?1:2)+
                (search.Length==0?"":" matching “"+search+"”");
            if(request.RecordId.HasValue) subject+=" #"+request.RecordId.Value;
            if(stock && request.InStockOnly && request.Operation==AssistantOperation.Detail) parts.Add("available stock only");
            if(request.ParentRecordId.HasValue) parts.Add("linked record #"+request.ParentRecordId.Value);
            Add(parts,"status",request.Status ?? AssistantCatalog.Find(request.Capability)?.DefaultStatus);
            Add(parts,"condition",request.Condition);Add(parts,"category",request.Category);
            Add(parts,"priority",request.Priority);Add(parts,"employee",request.Employee);
            Add(parts,"department",request.Department);Add(parts,"branch",request.Branch);Add(parts,"company",request.Company);
            if(request.Personal) parts.Add(request.PersonalRelation=="Assigned"?"assigned to you":request.PersonalRelation=="Created"?"created by you":"for your account");
            if(request.OutstandingOnly) parts.Add("outstanding quantities");
            if(request.ArchivedOnly) parts.Add("archived only");else if(request.IncludeArchived) parts.Add("including archived");
            if(request.LowestStock) parts.Add("lowest stock first");
            Add(parts,"grouped by",request.GroupBy);
            if(request.Page>0) parts.Add("page "+(request.Page+1));
            string operation=request.Operation==AssistantOperation.Detail?"details for ":request.Operation==AssistantOperation.Summary?"a summary of ":"";
            string period=AssistantEnglish.Period(request);
            return "Checking "+operation+subject+(parts.Count==0?"":" ("+string.Join("; ",parts)+")")+"."+(period==null?"":"\n"+period);
        }
        private static void Add(List<string> parts,string label,string value)
        {if(!string.IsNullOrWhiteSpace(value)) parts.Add(label+": "+value);}
    }
}
