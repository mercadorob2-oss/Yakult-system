using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    internal sealed class AssistantOrganization
    {
        internal string Field { get; }
        internal int? Id { get; }
        internal string Name { get; }
        internal string Acronym { get; }
        internal AssistantOrganization(string field,int? id,string name,string acronym=null)
        {Field=field;Id=id;Name=name;Acronym=acronym;}
    }
    internal sealed class AssistantOrganizationMention
    {
        internal int Start,Length;
        internal string Value,Field,SourceWord;
        internal IReadOnlyList<AssistantOrganization> Choices;
    }
    internal sealed class AssistantOrganizationResolution
    {
        internal string Question { get; }
        internal IReadOnlyList<AssistantOrganizationMention> Mentions { get; }
        internal AssistantOrganizationResolution(string question,IReadOnlyList<AssistantOrganizationMention> mentions)
        {Question=question;Mentions=mentions;}
        internal AssistantOrganizationMention Uncertain {get {return Mentions.FirstOrDefault(m=>m.Choices.Count!=1);}}
        internal string Rewrite(string question)
        {
            if(question!=Question) throw new ArgumentException("Please ask the organization-filter question again.");
            string text=question;
            foreach(var mention in Mentions.Where(m=>m.Choices.Count==1).OrderByDescending(m=>m.Start))
            {
                // Only original user words enter interpretation. Canonical database names and IDs stay local.
                string prefix=mention.SourceWord!=null?mention.SourceWord+" in ":mention.Start==0 && AssistantQuestionLanguage.IsFollowUp(question)?"only in ":"in ";
                string replacement=prefix+mention.Choices[0].Field.ToLowerInvariant()+" \""+mention.Value.Replace("\"","")+"\"; ";
                text=text.Remove(mention.Start,mention.Length).Insert(mention.Start,replacement);
            }
            return text;
        }
        internal string PromptHints
        {get {string value=string.Join("; ",Mentions.Where(m=>m.Choices.Count==1).Select(m=>m.Choices[0].Field+"="+m.Value));return System.Text.Encoding.UTF8.GetByteCount(value)<=800?value:"";}}
        internal AssistantOrganizationResolution Choose(AssistantOrganization choice)
        {
            var uncertain=Uncertain;
            if(uncertain==null || !uncertain.Choices.Contains(choice)) throw new ArgumentException("Please choose a current organization filter.");
            return new AssistantOrganizationResolution(Question,Mentions.Select(m=>ReferenceEquals(m,uncertain)?new AssistantOrganizationMention {
                Start=m.Start,Length=m.Length,Value=m.Value,Field=choice.Field,SourceWord=m.SourceWord,Choices=new[] {choice}}:m).ToList().AsReadOnly());
        }
        internal void Apply(AssistantRequest request)
        {
            var matches=(request.Organizations ?? new AssistantOrganization[0]).Where(o=>o.Name==Value(request,o.Field)).ToList();
            foreach(var mention in Mentions.Where(m=>m.Choices.Count==1))
            {
                var organization=mention.Choices[0];
                if(!string.Equals(Value(request,organization.Field),mention.Value,StringComparison.OrdinalIgnoreCase)) continue;
                Set(request,organization.Field,organization.Name);
                matches.RemoveAll(o=>o.Field==organization.Field);matches.Add(organization);
            }
            request.Organizations=matches.AsReadOnly();
        }
        internal static string Value(AssistantRequest request,string field)
        {return field=="Company"?request.Company:field=="Branch"?request.Branch:field=="Department"?request.Department:null;}
        internal static void Set(AssistantRequest request,string field,string value)
        {if(field=="Company") request.Company=value;else if(field=="Branch") request.Branch=value;else if(field=="Department") request.Department=value;}
        internal static void ClearStale(AssistantRequest request)
        {request.Organizations=(request.Organizations ?? new AssistantOrganization[0]).Where(o=>o.Name==Value(request,o.Field)).ToList().AsReadOnly();}
    }
    internal interface IAssistantOrganizationVocabulary
    {
        Task<AssistantOrganizationResolution> ResolveAsync(string question,IReadOnlyList<AssistantRequest> requests,IReadOnlyList<AssistantCapability> sources,AssistantContext context,CancellationToken ct);
    }
    /// <summary>Bounded reference-name lookup; no business rows, quantities or catalog records enter AI prompts.</summary>
    internal sealed class AssistantOrganizationVocabulary : IAssistantOrganizationVocabulary
    {
        private readonly AssistantReferenceCache<IReadOnlyList<AssistantOrganization>> _cache;
        private readonly Func<IReadOnlyList<string>,IReadOnlyList<string>,CancellationToken,Task<IReadOnlyList<AssistantOrganization>>> _load;
        internal AssistantOrganizationVocabulary(Func<IReadOnlyList<string>,IReadOnlyList<string>,CancellationToken,Task<IReadOnlyList<AssistantOrganization>>> load=null,Func<DateTime> clock=null)
        {_load=load ?? LoadAsync;_cache=new AssistantReferenceCache<IReadOnlyList<AssistantOrganization>>(32,"organization-cache",clock);}
        internal static string AcronymKey(string text) {return Regex.Replace(text ?? "",@"[.\s-]","").ToUpperInvariant();}
        private static readonly string[] Fields={"Company","Branch","Department"};
        private static readonly Regex Words=new Regex(@"[\p{L}\p{Nd}]+(?:[-.'][\p{L}\p{Nd}]+)*");
        private static readonly Regex Stop=new Regex(@"^(in|at|from|for|under|within|sa|with|and|plus|by|per|only|today|yesterday|this|last|next|current|previous|mtd|wtd|ytd|ngayong|nakaraang|since|before|after|during|status|priority|assigned|handled|created|submitted|open|solved|closed|pending|high|low|critical|company|branch|department|dept|co|br)$",RegexOptions.IgnoreCase);
        internal static IReadOnlyList<AssistantOrganizationMention> Candidates(string question,IReadOnlyList<AssistantRequest> requests)
        {
            if((question ?? "").Length>AiIntentRouter.MaxQuestionLength) return new AssistantOrganizationMention[0];
            string text=question ?? "";var quotes=AssistantQuestionLanguage.Quotes.Matches(text).Cast<Match>().ToArray();
            string masked=AssistantQuestionLanguage.Quotes.Replace(text,m=>new string('~',m.Length));
            var result=new List<AssistantOrganizationMention>();
            foreach(Match prefix in Regex.Matches(masked,@"\b(?:(?:in|at|from|for|under|within|sa)\s+(?:the\s+)?)?(?<field>company|branch|department|dept\.?|co\.?|br\.?)\s+|\b(?:in|at|from|for|under|within|sa)\s+(?:the\s+)?",RegexOptions.IgnoreCase))
            {
                if(Regex.IsMatch(masked.Substring(0,prefix.Index),@"\b(?:by|per|for each|broken down by|breakdown by|split by)\s*$",RegexOptions.IgnoreCase)) continue;
                string field=prefix.Groups["field"].Success?AssistantWordMappings.OrganizationField(prefix.Groups["field"].Value):null;
                int start=prefix.Index+prefix.Length;
                var literal=quotes.FirstOrDefault(m=>m.Index==start);
                if(literal!=null)
                    result.Add(new AssistantOrganizationMention {Start=prefix.Index,Length=start+literal.Length-prefix.Index,Value=literal.Value.Substring(1,literal.Length-2),Field=field});
                else
                {
                    var words=Words.Matches(masked.Substring(start)).Cast<Match>().Take(8).ToList();
                    for(int i=0;i<words.Count;i++)
                    {
                        var word=words[i];
                        if(Regex.IsMatch(masked.Substring(start+word.Index),@"^(?:month|week|year)\s+to\s+date\b",RegexOptions.IgnoreCase)) break;
                        if(Stop.IsMatch(word.Value) || (i==0 && Regex.IsMatch(word.Value,@"^(stock|progress|me|employee|technician|approval|fulfillment|january|february|march|april|may|june|july|august|september|october|november|december)$",RegexOptions.IgnoreCase))) break;
                        if((i==0?word.Index:word.Index-(words[i-1].Index+words[i-1].Length))>1) break;
                        int length=word.Index+word.Length;
                        result.Add(new AssistantOrganizationMention {Start=prefix.Index,Length=start+length-prefix.Index,Value=text.Substring(start,length),Field=field});
                    }
                }
            }
            // Names before a source, and explicit suffixes such as "YMC company".
            foreach(Match noun in Regex.Matches(masked,@"\b(?<noun>tickets?|tix|tkts?|tckts?|ticktes|tikets|requests?|reqs?|requets|requestes|repairs?|employees?|invoices?|company|branch|department)\b",RegexOptions.IgnoreCase))
            {
                string nounText=noun.Value.ToLowerInvariant();bool fieldNoun=new[] {"company","branch","department"}.Contains(nounText);
                if(fieldNoun && Regex.IsMatch(masked.Substring(0,noun.Index),@"\b(?:by|per|for each|broken down by|breakdown by|split by)\s*$",RegexOptions.IgnoreCase)) continue;
                var sourcePhrase=AssistantWordMappings.TicketSourcePhraseAtEnd(masked.Substring(0,noun.Index+noun.Length));
                int sourceStart=sourcePhrase.Success?sourcePhrase.Index:noun.Index;
                string sourceWord=sourcePhrase.Success?text.Substring(sourceStart,noun.Index+noun.Length-sourceStart):noun.Value;
                var words=Words.Matches(masked.Substring(0,sourceStart)).Cast<Match>().Reverse().Take(8).ToList();
                for(int i=0;i<words.Count;i++)
                {
                    var word=words[i];if(Stop.IsMatch(word.Value) || Regex.IsMatch(word.Value,@"^(how|many|much|show|list|count|my|there|are|the|number|of)$",RegexOptions.IgnoreCase)) break;
                    int end=i==0?sourceStart:words[i-1].Index;
                    if(!Regex.IsMatch(masked.Substring(word.Index+word.Length,end-word.Index-word.Length),@"^\s+$")) break;
                    result.Add(new AssistantOrganizationMention {Start=word.Index,Length=noun.Index+noun.Length-word.Index,Value=text.Substring(word.Index,sourceStart-word.Index).Trim(),
                        Field=fieldNoun?char.ToUpperInvariant(nounText[0])+nounText.Substring(1):null,SourceWord=fieldNoun?null:sourceWord});
                }
            }
            if(requests.Count==1 && AssistantQuestionLanguage.IsFollowUp(text))
            {
                var follow=Regex.Match(masked,@"^(?:what about|how about|only|just|filter to|limit to|keep it to)\s+(?<value>[\p{L}\p{Nd}][\p{L}\p{Nd}. '\-]*?)[?.!]*$",RegexOptions.IgnoreCase);
                var existing=Fields.Where(f=>!string.IsNullOrWhiteSpace(AssistantOrganizationResolution.Value(requests[0],f))).ToList();
                if(follow.Success && existing.Count==1 && !Stop.IsMatch(follow.Groups["value"].Value))
                    result.Add(new AssistantOrganizationMention {Start=follow.Index,Length=follow.Length,Value=follow.Groups["value"].Value.Trim(),Field=existing[0]});
            }
            return result.Where(m=>m.Value.Length>0 && m.Value.Length<=150 && !Regex.IsMatch(m.Value,@"^\d"))
                .GroupBy(m=>m.Start+"|"+m.Length+"|"+m.Field).Select(g=>g.First()).Take(64).ToList().AsReadOnly();
        }
        internal static AssistantOrganizationResolution Match(string question,IReadOnlyList<AssistantRequest> requests,IReadOnlyList<string> fields,IReadOnlyList<AssistantOrganization> organizations)
        {
            var candidates=Candidates(question,requests);var matches=new List<AssistantOrganizationMention>();
            foreach(var group in candidates.GroupBy(m=>m.Start).OrderBy(g=>g.Key))
            {
                var found=group.Select(m=>new {Mention=m,Choices=organizations.Where(o=>fields.Contains(o.Field) && (m.Field==null || m.Field==o.Field) &&
                    (string.Equals(o.Name.Trim(),m.Value.Trim(),StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(o.Acronym) && AcronymKey(o.Acronym)==AcronymKey(m.Value)))).ToList()})
                    .Where(p=>p.Choices.Count>0).OrderByDescending(p=>p.Mention.Length).FirstOrDefault();
                if(found!=null)
                {
                    var m=found.Mention;
                    // A complete stored name is stronger evidence than an acronym for a different organization.
                    // Duplicate full names and shared acronyms still require the user's choice.
                    var names=found.Choices.Where(o=>string.Equals(o.Name.Trim(),m.Value.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();
                    var choices=names.Count>0?names:found.Choices;
                    matches.Add(new AssistantOrganizationMention {Start=m.Start,Length=m.Length,Value=m.Value,Field=m.Field,SourceWord=m.SourceWord,Choices=choices.AsReadOnly()});
                }
                else
                {
                    // Explicit unknown names remain exact organization filters; implicit unknown locations need a role choice.
                    var m=group.OrderByDescending(c=>c.Length).First();
                    if(m.SourceWord!=null) continue;
                    var search=requests.SelectMany(r=>AssistantQuestionLanguage.SearchTerms(r.Search)).ToList();
                    bool explicitField=m.Field!=null;
                    if(!explicitField && !AssistantQuestionLanguage.SearchTerms(m.Value).Any(t=>search.Contains(t,StringComparer.OrdinalIgnoreCase))) continue;
                    var choices=fields.Where(f=>m.Field==null || m.Field==f).Select(f=>new AssistantOrganization(f,null,m.Value)).ToArray();
                    if(choices.Length>0) matches.Add(new AssistantOrganizationMention {Start=m.Start,Length=m.Length,Value=m.Value,Field=m.Field,Choices=choices});
                }
            }
            var selected=new List<AssistantOrganizationMention>();
            foreach(var mention in matches.OrderByDescending(m=>m.Length))
                if(!selected.Any(m=>mention.Start<m.Start+m.Length && m.Start<mention.Start+mention.Length)) selected.Add(mention);
            return new AssistantOrganizationResolution(question,selected.OrderBy(m=>m.Start).ToList().AsReadOnly());
        }
        internal static SqlCommand CreateCommand(IReadOnlyList<string> values,IReadOnlyList<string> fields,SqlConnection connection=null)
        {
            if(values.Count==0 || values.Count>64 || fields.Count==0 || fields.Any(f=>!Fields.Contains(f))) throw new ArgumentException("Please use supported organization filters.");
            var command=new SqlCommand {Connection=connection,CommandTimeout=6};var names=new List<string>();var aliases=new List<string>();
            foreach(string value in values.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string name="@N"+names.Count,alias="@A"+aliases.Count;names.Add(name);aliases.Add(alias);
                command.Parameters.Add(name,SqlDbType.NVarChar,160).Value=value.ToUpperInvariant();
                command.Parameters.Add(alias,SqlDbType.NVarChar,160).Value=AcronymKey(value);
            }
            command.CommandText=string.Join(" UNION ALL ",fields.Select(field=>"SELECT TOP (64) '"+field+"' Field,"+(field=="Company"?"ComId":field=="Branch"?"BranchId":"DeptId")+" Id,Name,Acronym FROM dbo.["+field+"] WHERE "+
                "UPPER(LTRIM(RTRIM(Name))) IN ("+string.Join(",",names)+") OR UPPER(REPLACE(REPLACE(REPLACE(Acronym,'.',''),' ',''),'-','')) IN ("+string.Join(",",aliases)+")"));
            return command;
        }
        public async Task<AssistantOrganizationResolution> ResolveAsync(string question,IReadOnlyList<AssistantRequest> requests,IReadOnlyList<AssistantCapability> sources,AssistantContext context,CancellationToken ct)
        {
            var fields=sources.Where(c=>c.IsAllowed(context)).SelectMany(c=>c.SupportedFilters).Where(Fields.Contains).Distinct().OrderBy(f=>f).ToArray();
            var candidates=Candidates(question,requests);
            if(fields.Length==0 || candidates.Count==0) return null;
            var values=candidates.Select(c=>c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            // Length prefixes keep quoted names containing separators from colliding with other keys.
            string key=string.Join("|",fields)+"|"+string.Concat(values.Select(v=>v.ToUpperInvariant()).OrderBy(v=>v,StringComparer.Ordinal).Select(v=>v.Length+":"+v));
            var records=await _cache.GetAsync(context.SessionStamp,key,token=>_load(values,fields,token),ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();return Match(question,requests,fields,records);
        }
        private static async Task<IReadOnlyList<AssistantOrganization>> LoadAsync(IReadOnlyList<string> values,IReadOnlyList<string> fields,CancellationToken ct)
        {
                    var records=new List<AssistantOrganization>();
                    using(var budget=CancellationTokenSource.CreateLinkedTokenSource(ct))
                    using(var connection=new SqlConnection(DatabaseConfig.ConnectionString))
                    using(var command=CreateCommand(values,fields,connection))
                    {
                        budget.CancelAfter(TimeSpan.FromSeconds(8));await connection.OpenAsync(budget.Token).ConfigureAwait(false);
                        using(var reader=await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false))
                            while(await reader.ReadAsync(budget.Token).ConfigureAwait(false)) records.Add(new AssistantOrganization(reader.GetString(0),reader.GetInt32(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3)));
                    }
                    return records.AsReadOnly();
        }
    }
}
