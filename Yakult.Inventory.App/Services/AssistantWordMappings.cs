using System;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Reviewed conversational aliases. Literal names and unknown words are never discarded.</summary>
    internal static class AssistantWordMappings
    {
        internal const string AmbiguousTicketStatus=@"\b(resolved|done|finished|fixed|completed)\b";
        internal const string AmbiguousPriority=@"\b(urgent|asap)\b";
        private const RegexOptions Options=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Compiled;
        private static readonly Regex EarlyWords=new Regex(@"^\s*(pls\b|plz\b|please\b|kindly\b|(?:can|could|would) u\b|hw\b|(?:show|list|find|count) me\b|lemme\b|let me\b|pakita\b|ipakita\b|patingin\b|ilan\b|ilang\b|gaano karaming\b)",Options);
        private static readonly Regex LateWords=new Regex(@"\b(tix|tkts?|tckts?|ticktes|tikets|reqs?|requets|requestes|dept|co|br|prio|helpdesk|help desk|service desk|it support|technical support|trouble|inprogress|in-progress|ongoing|wip|awaiting|waiting|not yet fulfilled|on[- ]hand|right now|at the moment|myself|raised|filed|logged|submitted|handled|working on|current|previous|mo|wk|yr|mtd|wtd|ytd|ngayong|nakaraang|how about|filter to|limit to|keep it to|same as before|show more|go back|broken down by|breakdown by|split by|for each)\b",Options);
        private static readonly Regex ValuePrefix=new Regex(@"\b(?:named|called|matching|containing|assigned to|handled by|borrowed by|for employee|(?:in |for |at )?(?:the )?(?:company|branch|department|dept\.?|co\.?|br\.?|condition|category))\s+(?<value>(?:(?!\b(?:today|yesterday|this|last|since|from|before|after|during|by|per|mtd|wtd|ytd)\b).)*)$",Options);
        private sealed class Rule
        {
            internal readonly Regex Pattern;internal readonly string Replacement;
            internal Rule(string pattern,string replacement) {Pattern=new Regex(pattern,Options);Replacement=replacement;}
        }
        private static readonly Rule[] Early={
            new Rule(@"^\s*(?:(?:please|pls|plz|kindly)[,]?\s+)+",""),
            new Rule(@"^(can|could|would) u\b","$1 you"),
            new Rule(@"^hw\s+(?:many|mny|may|mnay)\b","how many"),
            new Rule(@"^(show|list|find|count) me\s+","$1 "),
            new Rule(@"^(?:lemme|let me) (?:see|check)\s+","show "),
            new Rule(@"^(?:pakita|ipakita|patingin)(?:\s+(?:mo|ng|ang|yung|mga|po))*\s+","show "),
            new Rule(@"^ilan lahat\b","how many in total"),
            new Rule(@"^(?:ilan|ilang|gaano karaming)(?:\s+(?:ang|yung|mga|po|ba))*\s+","how many ")
        };
        private static readonly Rule TicketNoun=new Rule(@"\b(tix|tkts?|tckts?|ticktes|tikets)\b(?![-#]\d)","ticket");
        private static readonly Rule RequestNoun=new Rule(@"\b(reqs?|requets|requestes)\b(?![-#]?\d)","request");
        private static readonly Rule TicketPhrase=new Rule(@"\b(?:help[- ]?desk|service desk|it support|technical support|trouble)\s+tickets?\b","ticket");
        private static readonly Rule TicketState=new Rule(@"\b(?:inprogress|in-progress|ongoing|wip)\b","in progress");
        private static readonly Rule RequestState=new Rule(@"\b(?:awaiting fulfillment|waiting for fulfillment|not yet fulfilled)\b","unfulfilled");
        private static readonly Rule StockState=new Rule(@"\bon[- ]hand\b","in stock");
        private static readonly Rule OrganizationFollowUp=new Rule(@"^(?:in|at|for)\s+(?=company\b|branch\b|department\b)","only in ");
        private static readonly Rule[] Common={
            new Rule(@"\bdept\.?(?=\s|[?.!,;]|$)","department"),
            new Rule(@"\b(?:in|for|at|under|within|sa)\s+co\.?\s+","in company "),
            new Rule(@"\b(?:in|for|at|under|within|sa)\s+br\.?\s+","in branch "),
            new Rule(@"\bsa\s+(?=company\b|branch\b|department\b)","in "),
            new Rule(@"\b(?:broken down by|breakdown by|split by|for each)\s+(?=status\b|priority\b|department\b|branch\b|company\b|category\b|condition\b|month\b)","by "),
            new Rule(@"\bprio\s+(?=high\b|medium\b|low\b|critical\b)","priority "),
            new Rule(@"\b(?:current) (month|week|year)\b","this $1"),
            new Rule(@"\bprevious (month|week|year)\b","last $1"),
            new Rule(@"\b(this|last) mo\b","$1 month"),
            new Rule(@"\b(this|last) wk\b","$1 week"),
            new Rule(@"\b(this|last) yr\b","$1 year"),
            new Rule(@"\bngayong araw\b","today"),new Rule(@"\bngayong linggo\b","this week"),
            new Rule(@"\bnakaraang linggo\b","last week"),new Rule(@"\bngayong taon\b","this year"),
            new Rule(@"\bnakaraang taon\b","last year"),
            new Rule(@"\bmtd\b","month to date"),new Rule(@"\bwtd\b","week to date"),new Rule(@"\bytd\b","year to date"),
            new Rule(@"\bassigned to myself\b","assigned to me"),
            new Rule(@"\b(?:raised|filed|logged|submitted) by me\b","created by me"),
            new Rule(@"\b(?:handled by me|i am working on|i'm working on)\b","assigned to me"),
            new Rule(@"\b(?:right now|at the moment)[?.!]*$",""),
            new Rule(@"^how about\s+","what about "),
            new Rule(@"^(?:filter to|limit to|keep it to)\s+","only "),
            new Rule(@"^same as before\b","same records"),
            new Rule(@"^(?:show )?more(?: please)?[?.!]*$","next page"),
            new Rule(@"^(?:go back(?: a page)?|previous page please)[?.!]*$","previous page")
        };
        internal static string Opening(string text)
        {
            if(!EarlyWords.IsMatch(text)) return text;
            foreach(var rule in Early) text=rule.Pattern.Replace(text,rule.Replacement);
            return text;
        }
        internal static string ProtectMappedNames(string text)
        {
            if(!Regex.IsMatch(text ?? "",@"\b(named|called|matching|containing|search for)\b",RegexOptions.IgnoreCase)) return text;
            var literals=AssistantQuestionLanguage.Quotes.Matches(text);
            return Regex.Replace(text,@"\b(?:named|called|matching|containing|search for)\s+(?<value>.+?)(?=\s+(?:(?:in|for|at) (?:company|co\.?|branch|br\.?|department|dept\.?)\b|assigned to\b|handled by\b|condition\b|priority\b|today\b|yesterday\b|this\b|last\b|current\b|previous\b|since\b|from\b|before\b|after\b|during\b|by\b|per\b)|[?!,;]|$)",m=>
            {
                foreach(Match literal in literals) if(m.Index>=literal.Index && m.Index<literal.Index+literal.Length) return m.Value;
                string value=m.Groups["value"].Value;
                if(value.IndexOf('"')>=0 || value.IndexOf('\'')>=0 || (!LateWords.IsMatch(value) && !Regex.IsMatch(value,AmbiguousTicketStatus+"|"+AmbiguousPriority,RegexOptions.IgnoreCase))) return m.Value;
                return m.Value.Substring(0,m.Groups["value"].Index-m.Index)+"\""+value+"\"";
            },RegexOptions.IgnoreCase);
        }
        private static string Replace(string text,Rule rule)
        {
            return rule.Pattern.Replace(text,m=>
            {
                var prefix=ValuePrefix.Match(text.Substring(0,m.Index));
                bool period=Regex.IsMatch(m.Value,@"^(this|last|current|previous|ngayong|nakaraang|mtd|wtd|ytd)\b",RegexOptions.IgnoreCase);
                bool name=prefix.Success && (!period || string.IsNullOrWhiteSpace(prefix.Groups["value"].Value));
                return name?m.Value:m.Result(rule.Replacement);
            });
        }
        internal static string Phrases(string text)
        {
            if(!LateWords.IsMatch(text) && !OrganizationFollowUp.Pattern.IsMatch(text)) return text;
            bool stock=Regex.IsMatch(text,@"\b(items?|stock|cartridges?|consumables?|units?|on[- ]hand)\b",RegexOptions.IgnoreCase);
            if(!stock)
            {
                text=Replace(text,TicketNoun);
                text=Replace(text,RequestNoun);
            }
            text=Replace(text,TicketPhrase);
            foreach(var rule in Common) text=Replace(text,rule);
            bool tickets=Regex.IsMatch(text,@"\btickets?\b",RegexOptions.IgnoreCase);
            bool requests=Regex.IsMatch(text,@"\b(?:requests?|requisitions?)\b",RegexOptions.IgnoreCase);
            if(tickets) text=Replace(text,TicketState);
            if(requests) text=Replace(text,RequestState);
            if(stock) text=Replace(text,StockState);
            text=Replace(text,OrganizationFollowUp);
            return text;
        }
        internal static string OrganizationField(string value)
        {
            string role=(value ?? "").TrimEnd('.').ToLowerInvariant();
            return role=="co" || role=="company"?"Company":role=="br" || role=="branch"?"Branch":role=="dept" || role=="department"?"Department":null;
        }
        internal static Match TicketSourcePhraseAtEnd(string value)
        {return Regex.Match(value,@"\b(?:help[- ]?desk|service desk|it support|technical support|trouble|it)\s+tickets?$",RegexOptions.IgnoreCase);}
        internal static bool HasAmbiguousPriority(string text)
        {return HasFilterWord(text,AmbiguousPriority) && !HasFilterWord(text,@"\b(high|critical|low|medium)\b");}
        internal static bool HasAmbiguousTicketStatus(string text)
        {return HasFilterWord(text,AmbiguousTicketStatus);}
        private static bool HasFilterWord(string text,string pattern)
        {
            foreach(Match match in Regex.Matches(text,pattern,RegexOptions.IgnoreCase))
                if(!ValuePrefix.IsMatch(text.Substring(0,match.Index))) return true;
            return false;
        }
    }
}
