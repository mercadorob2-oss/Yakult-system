using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Immutable catalog evidence travels with copied requests, never with AI output.</summary>
    internal sealed class AssistantModelMatch
    {
        internal string Key { get; }
        internal string Phrase { get; }
        internal AssistantModelMatch(string key,string phrase) {Key=key;Phrase=phrase;}
    }
    internal sealed class AssistantProductSuggestion
    {
        internal string Original;
        internal IReadOnlyList<string> Alternatives;
    }
    internal static class AssistantProductMatching
    {
        internal static bool IsStock(string source) {return new[] {"items","consumables","cartridges"}.Contains(source);}
        // Only spaces and hyphens are equivalent. Suffixes, digits and other punctuation stay meaningful.
        internal static string ModelKey(string text)
        {
            string key=Regex.Replace(text ?? "",@"[ \t\r\n-]","").ToUpperInvariant();
            return Regex.IsMatch(key,@"^(?=.*[A-Z])(?=.*[0-9])[A-Z0-9]{2,80}$")?key:null;
        }
        private static string ModelPattern(string key)
        {return @"(?<![\p{L}\p{Nd}-])"+string.Join(@"[ \t\r\n-]*",key.Select(c=>Regex.Escape(c.ToString())))+@"(?![\p{L}\p{Nd}-])";}
        internal static AssistantModelMatch MatchModel(string question,IReadOnlyList<AssistantProductTerm> products)
        {
            string text=AssistantQuestionLanguage.WithoutQuotes(question);
            var matches=products.Select(p=>ModelKey(p.Model)).Where(k=>k!=null).Distinct()
                .Select(k=>new {Key=k,Match=Regex.Match(text,ModelPattern(k),RegexOptions.IgnoreCase)})
                .Where(p=>p.Match.Success).ToList();
            // A longer full model takes precedence over its prefix; separate models remain separate filters.
            matches=matches.Where(m=>!matches.Any(other=>other.Key!=m.Key && other.Match.Index<=m.Match.Index &&
                other.Match.Index+other.Match.Length>=m.Match.Index+m.Match.Length)).ToList();
            return matches.Count==1?new AssistantModelMatch(matches[0].Key,matches[0].Match.Value):null;
        }
        internal static string RewriteModel(string question,AssistantModelMatch model)
        {return model==null?question:AssistantQuestionLanguage.ReplaceUnquoted(question,ModelPattern(model.Key),model.Key);}
        internal static string UnresolvedSuffix(string question,AssistantModelMatch model)
        {
            if(model==null) return null;
            var suffix=Regex.Match(AssistantQuestionLanguage.WithoutQuotes(question),ModelPattern(model.Key)+@"[ \t]+[A-Za-z0-9](?![\p{L}\p{Nd}-])",RegexOptions.IgnoreCase);
            return suffix.Success?suffix.Value:null;
        }
        internal static void Apply(AssistantPlan plan,AssistantStockHint stock)
        {
            if(stock?.Model==null) return;
            foreach(var request in plan.Requests.Where(r=>r.Capability==stock.Capability))
                if(Regex.IsMatch(AssistantQuestionLanguage.WithoutQuotes(request.Search),ModelPattern(stock.Model.Key),RegexOptions.IgnoreCase))
                    request.ModelMatch=stock.Model;
        }
        internal static string RemainingSearch(AssistantRequest request)
        {
            if(request.ModelMatch==null) return request.Search;
            if(!IsStock(request.Capability) || string.IsNullOrEmpty(request.ModelMatch.Key) || ModelKey(request.ModelMatch.Key)!=request.ModelMatch.Key ||
                !Regex.IsMatch(AssistantQuestionLanguage.WithoutQuotes(request.Search),ModelPattern(request.ModelMatch.Key),RegexOptions.IgnoreCase))
                throw new ArgumentException("Please specify a supported model filter in the search.");
            return AssistantQuestionLanguage.ReplaceUnquoted(request.Search,ModelPattern(request.ModelMatch.Key)," ");
        }
        internal static AssistantProductSuggestion Suggest(string question,AssistantRequest request,IReadOnlyList<AssistantProductTerm> products)
        {
            if(!IsStock(request.Capability) || string.IsNullOrWhiteSpace(request.Search)) return null;
            var models=products.Select(p=>p.Model).Where(m=>ModelKey(m)!=null)
                .GroupBy(ModelKey).ToDictionary(g=>g.Key,g=>g.OrderBy(m=>m.Length).ThenBy(m=>m,StringComparer.OrdinalIgnoreCase).First());
            var names=products.SelectMany(p=>AssistantQuestionLanguage.LiteralTerms(p.Name))
                .Where(t=>t.Length>=5 && Regex.IsMatch(t,@"^[a-zA-Z]+$"))
                .GroupBy(t=>t,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            var proposals=new List<AssistantProductSuggestion>();
            string search=AssistantQuestionLanguage.WithoutQuotes(request.Search);
            var terms=AssistantQuestionLanguage.SearchTerms(search).Concat(Regex.Matches(search,@"\b[A-Za-z]{1,12}[ -]+[0-9][A-Za-z0-9-]{1,30}\b")
                .Cast<Match>().Select(m=>m.Value)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach(string term in terms)
            {
                if(!Regex.IsMatch(AssistantQuestionLanguage.WithoutQuotes(question),@"(?<![\p{L}\p{Nd}-])"+Regex.Escape(term)+@"(?![\p{L}\p{Nd}-])",RegexOptions.IgnoreCase)) continue;
                string key=ModelKey(term);
                if((key!=null && models.ContainsKey(key)) || names.ContainsKey(term)) continue;
                IEnumerable<string> alternatives;
                if(key!=null && key.Length>=4)
                    alternatives=models.Where(p=>OneEdit(key,p.Key)).Select(p=>p.Value);
                else if(term.Length>=5 && Regex.IsMatch(term,@"^[a-zA-Z]+$"))
                    alternatives=names.Where(p=>OneEdit(term.ToUpperInvariant(),p.Key.ToUpperInvariant())).Select(p=>p.Value);
                else continue;
                var selected=alternatives.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t=>t,StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
                // Too many plausible products is not a useful correction. Never guess between several typos.
                if(selected.Length>0 && selected.Length<=2) proposals.Add(new AssistantProductSuggestion {Original=term,Alternatives=selected});
            }
            return proposals.Count==1?proposals[0]:null;
        }
        private static bool OneEdit(string a,string b)
        {
            if(a==b || Math.Abs(a.Length-b.Length)>1) return false;
            int start=0;while(start<Math.Min(a.Length,b.Length) && a[start]==b[start]) start++;
            if(a.Length==b.Length)
                return a.Substring(start+1)==b.Substring(start+1) ||
                    (start+1<a.Length && a[start]==b[start+1] && a[start+1]==b[start] && a.Substring(start+2)==b.Substring(start+2));
            return a.Length>b.Length?a.Substring(start+1)==b.Substring(start):a.Substring(start)==b.Substring(start+1);
        }
    }
}
