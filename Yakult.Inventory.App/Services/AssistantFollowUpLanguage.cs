using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    internal sealed class AssistantFollowUpScope
    {
        internal string Question;
        internal IReadOnlyList<AssistantRequest> Previous;
        internal string Message;
    }
    /// <summary>Resolves references to previous lookup scopes, never positions in database rows.</summary>
    internal static class AssistantFollowUpLanguage
    {
        private const string Intro=@"^(?:(?:please )?(?:show(?: me)?|use|refine|check|for|what about)\s+)?(?:the\s+)?";
        private static readonly Regex Result=new Regex(Intro+@"(?:(?<order>first|second|third|1st|2nd|3rd|[0-9]+)\s+result|result\s*#?\s*(?<number>[0-9]+))\b",RegexOptions.IgnoreCase);
        private static readonly Regex Same=new Regex(Intro+@"same\s+(?<kind>items?|products?|models?|one|records?|tickets?|requests?|search|filters)\b",RegexOptions.IgnoreCase);
        private static readonly Regex Row=new Regex(Intro+@"(?:first|second|third|[1-9][0-9]*(?:st|nd|rd|th)?)\s+(?:item|ticket|record|row)\b",RegexOptions.IgnoreCase);
        internal static bool IsRowReference(string question) {return Row.IsMatch(AssistantPlanner.Normalize(question));}
        internal static bool IsReference(string question)
        {string q=AssistantPlanner.Normalize(question);return Result.IsMatch(q) || Same.IsMatch(q);}
        internal static int? ResultNumber(string question,out string remainder)
        {
            string q=AssistantPlanner.Normalize(question);var match=Result.Match(q);remainder=q;
            if(!match.Success) return null;
            remainder=Tail(q,match.Length);
            string value=match.Groups["order"].Success?match.Groups["order"].Value:match.Groups["number"].Value;
            if(value=="first" || value=="1st") return 1;
            if(value=="second" || value=="2nd") return 2;
            if(value=="third" || value=="3rd") return 3;
            int number;return int.TryParse(value,out number)?number:int.MaxValue;
        }
        private static string Tail(string q,int length)
        {return Regex.Replace(q.Substring(length).Trim(' ', ',', ':', ';', '.', '?', '!'),@"^(?:but|and|with)\s+","").Trim();}
        internal static AssistantFollowUpScope Resolve(string question,IReadOnlyList<AssistantRequest> previous)
        {
            var result=new AssistantFollowUpScope {Question=question,Previous=previous ?? new AssistantRequest[0]};
            if((question ?? "").Length>AiIntentRouter.MaxQuestionLength) return result;
            string q=AssistantPlanner.Normalize(question),tail;
            if(Row.IsMatch(q))
            {result.Message="Which saved record do you mean? Please give its reference number, or say “first result” or “second result” to select a lookup.";return result;}
            int? number=ResultNumber(q,out tail);var same=Same.Match(q);
            if(!number.HasValue && !same.Success) return result;
            if(number.HasValue)
            {
                if(number<1 || number>result.Previous.Count)
                {result.Message=result.Previous.Count==0?"Which earlier result do you mean? Please run a lookup first.":"That result number isn’t available. Please choose one of the previous results.";return result;}
                result.Previous=new[] {result.Previous[number.Value-1].Copy()};
                var source=AssistantPlanner.BestCapability(tail);
                if(source!=null && source.Id!=result.Previous[0].Capability)
                {result.Message="That result contains "+(AssistantCatalog.Find(result.Previous[0].Capability)?.Title ?? "another record type").ToLowerInvariant()+". Would you like to refine it or start a new lookup?";return result;}
            }
            else
            {
                tail=Tail(q,same.Length);
                if(result.Previous.Count!=1)
                {result.Message=result.Previous.Count==0?"Which records would you like to look up first?":"Which result would you like to refine? Please choose its record type.";return result;}
                var request=result.Previous[0];string kind=same.Groups["kind"].Value;
                bool product=new[] {"item","items","product","products","model","models","one"}.Contains(kind);
                if((product && !AssistantProductMatching.IsStock(request.Capability)) ||
                    (kind.StartsWith("ticket",StringComparison.Ordinal) && request.Capability!="tickets") ||
                    (kind.StartsWith("request",StringComparison.Ordinal) && request.Capability!="requests"))
                {result.Message="Which item or record do you mean? Please name it or give its reference number.";return result;}
                if(product && !request.RecordId.HasValue && string.IsNullOrWhiteSpace(request.Search))
                {result.Message="Which item or model should I keep? Please name it or give its reference number.";return result;}
            }
            if(Regex.IsMatch(tail,@"^(?:please )?(create|delete|update|approve|reject|issue|archive|restore|send|grant|change|save|submit|remove|return)\b"))
            {result.Message="Yakumi is read-only. I can explain that workflow, but cannot perform it.";return result;}
            // Only the reference phrase is consumed. All remaining filters go through normal parsing.
            result.Question="only "+(tail.Length==0?"records":tail);
            return result;
        }
    }
}
