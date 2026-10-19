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
        internal string Capability="items";
        internal string Category;
        internal string Phrase;
        internal bool Route=true;
        internal AssistantModelMatch Model;
        internal IReadOnlyList<AssistantProductTerm> Products=new AssistantProductTerm[0];
        internal IReadOnlyList<string> KnownTerms=new string[0];
        internal string Rewrite(string question)
        {
            string value=AssistantProductMatching.RewriteModel(question,Model);
            if(!Route) return value;
            value=AssistantPlanner.Normalize(value);
            if(!string.IsNullOrWhiteSpace(Phrase)) value=AssistantQuestionLanguage.ReplaceUnquoted(value,@"(?<![\p{L}\p{Nd}])"+Regex.Escape(Phrase)+@"(?![\p{L}\p{Nd}])",Capability);
            return AssistantPlanner.BestCapability(value)?.Id==Capability?value:Capability+" "+value;
        }
    }
    internal interface IAssistantStockVocabulary
    {
        Task<AssistantStockHint> ResolveAsync(string question,AssistantContext context,CancellationToken ct);
    }

    /// <summary>Reads permitted product/category names as routing vocabulary. Never sends catalog rows to a model.</summary>
    internal sealed class AssistantStockVocabulary : IAssistantStockVocabulary
    {
        private sealed class Index
        {
            internal IReadOnlyList<AssistantProductTerm> Products;
            internal string[] KnownTerms;
            internal HashSet<string> Tokens;
            internal KeyValuePair<string,string[]>[] Categories;
            internal Index(IReadOnlyList<AssistantProductTerm> products)
            {
                Products=products;
                KnownTerms=products.SelectMany(p=>AiIntentRouter.SearchTokens((p.Name ?? "")+" "+(p.Model ?? ""),"",true)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                Tokens=new HashSet<string>(products.SelectMany(p=>AiIntentRouter.SearchTokens((p.Name ?? "")+" "+(p.Model ?? ""))),StringComparer.OrdinalIgnoreCase);
                Categories=products.Select(p=>p.Category).Where(c=>!string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(c=>Canonical(c).Length)
                    .Select(c=>new KeyValuePair<string,string[]>(c,new[] {Canonical(c)}.Concat(Regex.Matches(c,@"\(([^)]+)\)").Cast<Match>().Select(m=>Canonical(m.Groups[1].Value))).ToArray())).ToArray();
            }
        }
        private readonly AssistantReferenceCache<Index> _cache;
        private readonly Func<string,AssistantContext,CancellationToken,Task<IReadOnlyList<AssistantProductTerm>>> _load;
        internal AssistantStockVocabulary(Func<string,AssistantContext,CancellationToken,Task<IReadOnlyList<AssistantProductTerm>>> load=null,Func<DateTime> clock=null)
        {_load=load ?? LoadAsync;_cache=new AssistantReferenceCache<Index>(3,"stock-cache",clock);}
        internal static bool ShouldResolve(string question)
        {
            if((question ?? "").Length>AiIntentRouter.MaxQuestionLength) return false;
            string q=AssistantPlanner.Normalize(question);
            if(AssistantPlanner.IsGuidance(q)) return false;
            if(!AiIntentRouter.Has(q,@"\b(how many|how much|ilan|ilang|bilang|stock|available|do (we|i) have|show|list|find|what about|meron|mayroon)\b")) return false;
            var source=AssistantPlanner.BestCapability(q);
            return source==null || AssistantProductMatching.IsStock(source.Id);
        }
        public async Task<AssistantStockHint> ResolveAsync(string question,AssistantContext context,CancellationToken ct)
        {
            string source=AssistantPlanner.BestCapability(question)?.Id ?? "items";
            if(!ShouldResolve(question) || !AssistantCatalog.Find(source).IsAllowed(context)) return null;
            var index=await _cache.GetAsync(context.SessionStamp,source,async token=>new Index(await _load(source,context,token).ConfigureAwait(false)),ct).ConfigureAwait(false);
            var hint=Match(question,index) ?? new AssistantStockHint {Route=false};
            hint.Capability=source;hint.Products=index.Products;
            // Category aliases are item routing vocabulary. Consumable/cartridge categories retain their own planner semantics.
            if(source!="items") {hint.Category=null;hint.Phrase=null;hint.Route=false;}
            return hint;
        }
        private static async Task<IReadOnlyList<AssistantProductTerm>> LoadAsync(string source,AssistantContext context,CancellationToken ct)
        {
                    var products=new List<AssistantProductTerm>();
                    using(var budget=CancellationTokenSource.CreateLinkedTokenSource(ct))
                    using(var connection=new SqlConnection(DatabaseConfig.ConnectionString))
                    using(var command=CreateVocabularyCommand(source,context,connection))
                    {
                        budget.CancelAfter(TimeSpan.FromSeconds(8));
                        await connection.OpenAsync(budget.Token).ConfigureAwait(false);
                        using(var reader=await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false))
                            while(await reader.ReadAsync(budget.Token).ConfigureAwait(false))
                            {
                                string category=reader.IsDBNull(2)?null:reader.GetString(2);
                                if(context.DeniedCategories!=null && context.DeniedCategories.Contains(category,StringComparer.OrdinalIgnoreCase)) continue;
                                products.Add(new AssistantProductTerm {Name=reader.IsDBNull(0)?null:reader.GetString(0),Model=reader.IsDBNull(1)?null:reader.GetString(1),Category=category});
                            }
                    }
                    return products.AsReadOnly();
        }
        internal static SqlCommand CreateVocabularyCommand(string source,AssistantContext context,SqlConnection connection=null)
        {
            if(!AssistantProductMatching.IsStock(source)) throw new ArgumentException("Please select a stock catalog.");
            if(!AssistantCatalog.Find(source).IsAllowed(context)) throw new UnauthorizedAccessException("That stock catalog is unavailable to your account.");
            var command=new SqlCommand {Connection=connection,CommandTimeout=6};
            command.CommandText=source=="consumables"?@"SELECT TOP (10000) i.ModelNumber Name,i.ModelNumber,i.Category FROM dbo.ConsumableModel i WHERE i.IsActive=1":
                source=="cartridges"?@"SELECT TOP (10000) i.Brand Name,i.ModelNumber,'Cartridge' Category FROM dbo.CartridgeModel i WHERE i.IsActive=1":
                @"SELECT TOP (10000) i.Name,i.ModelNumber,i.Category FROM dbo.Item i
                WHERE i.Active=1 AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus a WHERE a.EntityType='Item' AND a.EntityId=i.ItemId AND a.IsArchived=1)";
            foreach(string denied in context.DeniedCategories ?? new string[0])
            {
                string parameter="@Category"+command.Parameters.Count;
                command.CommandText+=" AND "+(source=="cartridges"?"N'Cartridge'":"ISNULL(i.Category,'')")+"<>"+parameter;
                command.Parameters.Add(parameter,System.Data.SqlDbType.NVarChar,200).Value=denied;
            }
            command.CommandText+=source=="cartridges"?" GROUP BY i.Brand,i.ModelNumber ORDER BY i.Brand,i.ModelNumber":
                source=="consumables"?" GROUP BY i.ModelNumber,i.Category ORDER BY i.Category,i.ModelNumber":
                " GROUP BY i.Name,i.ModelNumber,i.Category ORDER BY i.Category,i.Name,i.ModelNumber";
            return command;
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
        {return Match(question,new Index(products));}
        private static AssistantStockHint Match(string question,Index index)
        {
            var products=index.Products;
            string original=AssistantQuestionLanguage.WithoutQuotes(AssistantPlanner.Normalize(question));
            var model=AssistantProductMatching.MatchModel(question,products);
            var known=index.KnownTerms.Concat(model==null?new string[0]:new[] {model.Key}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var tokens=Regex.Matches(original,@"[\p{L}\p{Nd}]+").Cast<Match>().ToList();
            var phrases=new List<KeyValuePair<string,string>>();
            for(int size=Math.Min(6,tokens.Count);size>=1;size--)
                for(int start=0;start+size<=tokens.Count;start++)
                {
                    if(Canonical(tokens[start].Value).Length==0 || Canonical(tokens[start+size-1].Value).Length==0) continue;
                    string phrase=original.Substring(tokens[start].Index,tokens[start+size-1].Index+tokens[start+size-1].Length-tokens[start].Index);
                    string canonical=Canonical(phrase);
                    if(canonical.Length>=2) phrases.Add(new KeyValuePair<string,string>(canonical,phrase));
                }
            foreach(var category in index.Categories)
            {
                var phrase=phrases.FirstOrDefault(p=>category.Value.Contains(p.Key,StringComparer.OrdinalIgnoreCase));
                if(phrase.Key!=null) return new AssistantStockHint {Category=category.Key,Phrase=phrase.Value,KnownTerms=known,Model=model,Products=products};
            }
            var terms=AiIntentRouter.SearchTokens(original,"stock available items item records record catalog inventory",true);
            foreach(string term in terms.Where(t=>t.Length>=3))
            {
                // Exact catalog token evidence, not a substring or a model's guess.
                if(index.Tokens.Contains(term))
                    return new AssistantStockHint {KnownTerms=known,Model=model,Products=products};
            }
            return model==null?null:new AssistantStockHint {KnownTerms=known,Model=model,Products=products};
        }
    }
}
