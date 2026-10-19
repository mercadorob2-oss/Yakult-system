using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Conversational phrases are normalized outside quoted names only.</summary>
    internal static class AssistantQuestionLanguage
    {
        internal static readonly Regex Quotes=new Regex("\"[^\"\\r\\n]+\"|(?<![\\p{L}\\p{Nd}])'[^'\\r\\n]+'(?![\\p{L}\\p{Nd}])");
        internal static string MaskQuotes(string text,out Dictionary<string,string> literals)
        {
            var values=new Dictionary<string,string>();
            string prefix="yakumiliteral";
            while((text ?? "").IndexOf(prefix,StringComparison.OrdinalIgnoreCase)>=0) prefix+="x";
            string masked=Quotes.Replace(text ?? "",m=> {string key=prefix+values.Count+"token";values.Add(key,m.Value.Substring(1,m.Length-2));return key;});
            literals=values;return masked;
        }
        internal static string WithoutQuotes(string text) {return Quotes.Replace(text ?? ""," ");}
        internal static string ReplaceUnquoted(string text,string pattern,string replacement)
        {
            Dictionary<string,string> literals;string value=MaskQuotes(text,out literals);
            value=Regex.Replace(value,pattern,replacement,RegexOptions.IgnoreCase);
            foreach(var literal in literals) value=value.Replace(literal.Key,"\""+literal.Value+"\"");
            return value;
        }
        internal static string Normalize(string question)
        {
            Dictionary<string,string> literals;string text=MaskQuotes(AssistantWordMappings.ProtectMappedNames(question),out literals);
            text=AssistantWordMappings.Opening(text);
            text=Regex.Replace(text,@"\bhow\s+(may|mny|mnay)\b","how many",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"\bsloved\b","solved",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"\bbrand[- ]?new\b","brand new",RegexOptions.IgnoreCase);
            // Anchored count phrases add local coverage without discarding unknown filters or quoted names.
            text=Regex.Replace(text,@"^(?:please\s+)?(?:(?:can|could|would) you\s+)?(?:give|tell) me (?:the )?(?:total )?(?:number|count) of\b","how many",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"^(?:please\s+)?(?:what(?:'s| is)|what’s) (?:the )?(?:total )?(?:number|count) of\b","how many",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"^(?:please\s+)?(?:can|could|would) you (?:please )?count\b","how many",RegexOptions.IgnoreCase);
            text=AssistantKnowledge.NormalizeWords(text);
            text=AssistantWordMappings.Phrases(text);
            text=Regex.Replace(text,@"^(and|only|just|what about|same(?: but)?)\s+(company|branch|department)\s+","$1 in $2 ");
            text=Regex.Replace(text,@"\b(?:are |is )?(?:still |currently )?(?:in stock|available)\b","in stock");
            text=Regex.Replace(text,@"\b(?:do (?:we|i) have|have (?:we|i) got)(?: left| remaining)?\b","in stock");
            text=Regex.Replace(text,@"\bhow many\s+(?:are )?(?:left|remaining)\b","how many in stock");
            text=Regex.Replace(text,@"\b(?:can|could|would) you (?:please )?(?:tell me|check|show me)\b","show");
            foreach(var literal in literals) text=text.Replace(literal.Key,"\""+literal.Value+"\"");
            return text.Trim();
        }
        // Quoted words retain stop words, casing and numbers; SQL still uses parameters for every term.
        internal static List<string> SearchTerms(string text)
        {
            Dictionary<string,string> literals;string masked=MaskQuotes(text,out literals);
            return AiIntentRouter.SearchTokens(masked).SelectMany(t=>literals.ContainsKey(t)?LiteralTerms(literals[t]):new[] {t})
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        internal static string[] LiteralTerms(string text)
        {return Regex.Matches(text ?? "",@"[\p{L}\p{Nd}]+(?:[-'][\p{L}\p{Nd}]+)*").Cast<Match>().Select(m=>m.Value).ToArray();}
        internal static bool HasUnsupportedExclusion(string question)
        {
            string text=Regex.Replace(WithoutQuotes(Normalize(question)),@"\b(?:exclude|excluding|without|not) archived\b"," ");
            return AiIntentRouter.Has(text,@"\b(not|no|never|except|excluding|exclude|without|other than|out of stock|unavailable|isn't|aren't|don't|doesn't|isn’t|aren’t|don’t|doesn’t)\b");
        }
        internal static bool IsFollowUp(string q)
        {return AssistantFollowUpLanguage.IsReference(q) || AiIntentRouter.Has(Normalize(q),@"^(and |only |just |what about |same |those\b|them\b|next\b|previous\b|more\b|today\b|yesterday\b|all dates\b|this (month|week|year)\b|last (month|week|year)\b|(?:month|week|year) to date\b)");}
        internal static IEnumerable<string> ProductNameTerms(string question,AssistantRequest request)
        {
            if(!new[] {"items","consumables","cartridges"}.Contains(request.Capability)) return new string[0];
            string q=WithoutQuotes(Normalize(question));
            var nouns=AssistantCatalog.Find(request.Capability).Aliases.Concat(new[] {"item","items","product","products"})
                .Where(a=>!AiIntentRouter.Has(a,@"\b(stock|available|refilled|brand new)\b")).Select(Normalize);
            var terms=new List<string>();
            foreach(string noun in nouns)
            {
                var match=Regex.Match(q,@"^(?:(?:please )?(?:how many|how much|show|list|find|count)(?: me)?\s+)?(?<name>(?:[\p{L}\p{Nd}-]+\s+){1,3})"+Regex.Escape(noun)+@"\b");
                if(!match.Success) continue;
                var prefix=AiIntentRouter.SearchTokens(match.Groups["name"].Value,"",true);
                // One unknown word before an explicit product noun can be a brand/name. Longer unknown
                // phrases still need catalog evidence, quotes or interpretation. Suffix wording is not ignored.
                if(prefix.Count(t=>!Regex.IsMatch(t,@"\d"))<=1) terms.AddRange(prefix);
            }
            return terms;
        }
    }
}
