using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    internal sealed class AssistantProductTerm
    {
        internal string Name;
        internal string Model;
        internal string Category;
    }
    internal sealed class AssistantStockHint
    {
        internal string Category;
        internal string Phrase;
        internal string Rewrite(string question)
        {
            string value=AssistantPlanner.Normalize(question);
            if(!string.IsNullOrWhiteSpace(Phrase)) value=Regex.Replace(value,@"(?<![\p{L}\p{Nd}])"+Regex.Escape(Phrase)+@"(?![\p{L}\p{Nd}])","items",RegexOptions.IgnoreCase);
            return "items "+value;
        }
    }
    internal interface IAssistantStockVocabulary
    {
        Task<AssistantStockHint> ResolveAsync(string question,AssistantContext context,CancellationToken ct);
    }

    /// <summary>Reads permitted product/category names as routing vocabulary. Never sends catalog rows to a model.</summary>
    internal sealed class AssistantStockVocabulary : IAssistantStockVocabulary
    {
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1,1);
        private string _stamp;
        private DateTime _expiresUtc;
        private List<AssistantProductTerm> _products;
        internal static bool ShouldResolve(string question)
        {
            string q=AssistantPlanner.Normalize(question);
            if(AssistantPlanner.IsGuidance(q)) return false;
            if(!AiIntentRouter.Has(q,@"\b(how many|how much|ilan|ilang|bilang|stock|available|do (we|i) have|show|list|find|meron|mayroon)\b")) return false;
            var source=AssistantPlanner.BestCapability(q);
            return source==null || source.Id=="items";
        }
        public async Task<AssistantStockHint> ResolveAsync(string question,AssistantContext context,CancellationToken ct)
        {
            if(!ShouldResolve(question) || !AssistantCatalog.Find("items").IsAllowed(context)) return null;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if(_products==null || _stamp!=context.SessionStamp || DateTime.UtcNow>=_expiresUtc)
                {
                    var products=new List<AssistantProductTerm>();
                    using(var budget=CancellationTokenSource.CreateLinkedTokenSource(ct))
                    using(var connection=new SqlConnection(DatabaseConfig.ConnectionString))
                    using(var command=connection.CreateCommand())
                    {
                        budget.CancelAfter(TimeSpan.FromSeconds(8));command.CommandTimeout=6;
                        command.CommandText=@"SELECT TOP (10000) i.Name,i.ModelNumber,i.Category FROM dbo.Item i
                            WHERE i.Active=1 AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus a WHERE a.EntityType='Item' AND a.EntityId=i.ItemId AND a.IsArchived=1)";
                        foreach(string denied in context.DeniedCategories ?? new string[0])
                        {
                            string parameter="@Category"+command.Parameters.Count;
                            command.CommandText+=" AND ISNULL(i.Category,'')<>"+parameter;
                            command.Parameters.Add(parameter,System.Data.SqlDbType.NVarChar,200).Value=denied;
                        }
                        command.CommandText+=" GROUP BY i.Name,i.ModelNumber,i.Category ORDER BY i.Category,i.Name,i.ModelNumber";
                        await connection.OpenAsync(budget.Token).ConfigureAwait(false);
                        using(var reader=await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false))
                            while(await reader.ReadAsync(budget.Token).ConfigureAwait(false))
                            {
                                string category=reader.IsDBNull(2)?null:reader.GetString(2);
                                if(context.DeniedCategories!=null && context.DeniedCategories.Contains(category,StringComparer.OrdinalIgnoreCase)) continue;
                                products.Add(new AssistantProductTerm {Name=reader.IsDBNull(0)?null:reader.GetString(0),Model=reader.IsDBNull(1)?null:reader.GetString(1),Category=category});
                            }
                    }
                    _products=products;_stamp=context.SessionStamp;_expiresUtc=DateTime.UtcNow.AddMinutes(5);
                }
                return Match(question,_products);
            }
            finally {_gate.Release();}
        }
        private static string Canonical(string text)
        {
            string value=AssistantPlanner.Normalize(text);
            value=Regex.Replace(value,@"\b(?:cell\s*phones?|smartphones?|mobile phones?)\b","cellphone");
            value=Regex.Replace(value,@"\bphones?\b","cellphone");
            value=Regex.Replace(value,@"\baccess\b","acces"); // Existing category is named "Acces Point(AP)".
            value=Regex.Replace(value,@"\([^)]*\)"," ");
            return string.Join(" ",AiIntentRouter.SearchTokens(value,"",true).Select(word=>
                word.Length>4 && word.EndsWith("ies",StringComparison.Ordinal)?word.Substring(0,word.Length-3)+"y":
                Regex.IsMatch(word,@"(ches|shes|xes|zes|sses)$")?word.Substring(0,word.Length-2):
                word.Length>2 && word.EndsWith("s",StringComparison.Ordinal) && !word.EndsWith("ss",StringComparison.Ordinal)?word.Substring(0,word.Length-1):word));
        }
        internal static AssistantStockHint Match(string question,IReadOnlyList<AssistantProductTerm> products)
        {
            string original=AssistantPlanner.Normalize(question);
            var tokens=Regex.Matches(original,@"[\p{L}\p{Nd}]+").Cast<Match>().ToList();
            foreach(var category in products.Select(p=>p.Category).Where(c=>!string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(c=>Canonical(c).Length))
            {
                var aliases=new List<string> {Canonical(category)};
                foreach(Match abbreviation in Regex.Matches(category,@"\(([^)]+)\)")) aliases.Add(Canonical(abbreviation.Groups[1].Value));
                for(int size=Math.Min(6,tokens.Count);size>=1;size--)
                for(int start=0;start+size<=tokens.Count;start++)
                {
                    if(Canonical(tokens[start].Value).Length==0 || Canonical(tokens[start+size-1].Value).Length==0) continue;
                    string phrase=original.Substring(tokens[start].Index,tokens[start+size-1].Index+tokens[start+size-1].Length-tokens[start].Index);
                    string canonical=Canonical(phrase);
                    if(canonical.Length>=2 && aliases.Contains(canonical,StringComparer.OrdinalIgnoreCase)) return new AssistantStockHint {Category=category,Phrase=phrase};
                }
            }
            var terms=AiIntentRouter.SearchTokens(original,"stock available items item records record catalog inventory",true);
            foreach(string term in terms.Where(t=>t.Length>=3))
            {
                // Exact catalog token evidence, not a substring or a model's guess.
                if(products.Any(p=>AiIntentRouter.SearchTokens((p.Name ?? "")+" "+(p.Model ?? "")).Contains(term,StringComparer.OrdinalIgnoreCase)))
                    return new AssistantStockHint();
            }
            return null;
        }
    }
}
