using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Yakult.Inventory.App.Services
{
    public sealed class AssistantArticle
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Module { get; set; }
        public string Capability { get; set; }
        public string Page { get; set; }
        public string[] Tags { get; set; }
        public string Text { get; set; }
        public string[] SourceFiles { get; set; }
        public bool IsNavigationOnly { get; internal set; }
        public string Source { get { return Module+" → "+Title; } }
        public bool IsAllowed(AssistantContext context)
        {
            if(!context.IsLoggedIn || !context.HasPage(Page)) return false;
            var capability=AssistantCatalog.Find(Capability);
            return capability==null ? Module=="Workspace" : capability.IsAllowed(context);
        }
    }

    public static class AssistantKnowledge
    {
        private static readonly IReadOnlyList<AssistantArticle> _articles=Load();
        public static IReadOnlyList<AssistantArticle> Articles { get { return _articles; } }
        public static IEnumerable<string> PageKeys { get { return _articles.Select(a=>a.Page).Where(p=>!string.IsNullOrEmpty(p)); } }
        private static readonly HashSet<string> Stop=new HashSet<string>(("the a an is are do does can could would should i you me my to of in on for and or please how what why where tell explain this that it " +
            "paano saan ang mga ba po ko ako mo ito ng sa na naman").Split(' '),StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string,string> Synonyms=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            {"pahiram","borrow"},{"hiram","borrow"},{"hiniram","borrow"},{"ibalik","return"},{"isauli","return"},{"balik","return"},
            {"sira","repair"},{"sirang","repair"},{"ayos","repair"},{"ayusin","repair"},{"kahilingan","request"},{"aprubahan","approval"},
            {"ngayon","today"},{"kahapon","yesterday"},{"nasolve","solved"},{"solb","solved"},{"nakabinbin","pending"},{"empleyado","employee"},{"tiket","ticket"},
            {"gamit","equipment"},{"tauhan","employee"},{"halaga","amount"},{"presyo","amount"},{"bayad","invoice"},
            {"keyboards","keyboard"},{"tickets","ticket"},{"requests","request"},{"policies","policy"},{"employees","employee"},
            {"cartridges","cartridge"},{"renewals","renewal"},{"repairs","repair"},{"invoices","invoice"},{"borrows","borrow"}
        };
        internal static string NormalizeWords(string question)
        {
            string text=Regex.Replace(question ?? "",@"\bngayong buwan\b","this month",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"\bnakaraang buwan\b","last month",RegexOptions.IgnoreCase);
            return Regex.Replace(text,@"[\p{L}\p{Nd}]+",m=> { string replacement;return Synonyms.TryGetValue(m.Value,out replacement)?replacement:m.Value.ToLowerInvariant(); });
        }
        internal static string[] Tokens(string text)
        {
            return Regex.Matches(NormalizeWords(text),@"[\p{L}\p{Nd}]+").Cast<Match>().Select(m=>m.Value).Where(t=>t.Length>1 && !Stop.Contains(t)).ToArray();
        }
        private static IReadOnlyList<AssistantArticle> Load()
        {
            const string resource="Yakult.Inventory.App.AI_assets.YakumiKnowledge.json";
            var list=new List<AssistantArticle>();
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if(stream!=null) using(var reader=new StreamReader(stream,Encoding.UTF8))
                    list=JsonConvert.DeserializeObject<List<AssistantArticle>>(reader.ReadToEnd()) ?? list;
            }
            foreach(var capability in AssistantCatalog.All)
                if(!list.Any(a=>a.Capability==capability.Id))
                    list.Add(new AssistantArticle { Id="lookup-"+capability.Id,Title=capability.Title,Module=capability.Module,Capability=capability.Id,
                        Tags=capability.Aliases,Text="Open "+capability.Source+" to inspect records. Yakumi supports counts, lists, record details and summaries. Available filters: "+
                        string.Join(", ",capability.SupportedFilters)+". Groupings: "+string.Join(", ",capability.GroupColumns)+". Existing account permissions apply.",
                        SourceFiles=new[] { "Yakult.Inventory.App/Services/AssistantCatalog.cs" },IsNavigationOnly=true });
            return list.AsReadOnly();
        }

        public static IReadOnlyList<AssistantArticle> Search(string question,AssistantContext context,int take=2)
        {
            var candidates=_articles.Where(a=>a.IsAllowed(context)).ToList();
            var query=Tokens(question).Distinct().ToArray();
            if(query.Length==0) return new List<AssistantArticle>().AsReadOnly();
            var documents=candidates.ToDictionary(a=>a.Id,a=>Tokens(a.Title+" "+string.Join(" ",a.Tags ?? new string[0])+" "+a.Text));
            double average=documents.Count==0?1:Math.Max(1,documents.Values.Average(d=>d.Length));
            var scored=new List<Tuple<AssistantArticle,double>>();
            foreach(var article in candidates)
            {
                var words=documents[article.Id];double score=0;
                foreach(string token in query)
                {
                    int frequency=words.Count(w=>w==token);if(frequency==0) continue;
                    int containing=documents.Values.Count(d=>d.Contains(token));
                    double idf=Math.Log(1+(documents.Count-containing+0.5)/(containing+0.5));
                    score+=idf*(frequency*2.2)/(frequency+1.2*(0.25+0.75*words.Length/average));
                    if(Tokens(article.Title+" "+string.Join(" ",article.Tags ?? new string[0])).Contains(token)) score+=2;
                }
                if(score>0 && article.Module==context.Module) score+=0.5;
                if(score>0 && !article.IsNavigationOnly) score+=0.5;
                if(score>0) scored.Add(Tuple.Create(article,score));
            }
            return scored.OrderByDescending(s=>s.Item2).ThenBy(s=>s.Item1.Id,StringComparer.Ordinal).Take(Math.Min(2,Math.Max(1,take))).Select(s=>s.Item1).ToList().AsReadOnly();
        }

        internal static string FixedAnswer(IEnumerable<AssistantArticle> articles)
        {
            return string.Join("\n\n",articles.Select(a=>a.Title+"\n"+a.Text+"\nGuide source: "+a.Source+" ["+a.Id+"]."));
        }
    }
}
