using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yakult.Inventory.App.Services
{
    public enum AiIntent
    {
        None, ItemStock, Ticket, TicketCount, MyRequests, RequestStatus,
        ConsumableStock, BorrowStatus, RenewalsExpiry, PolicyLookup, SetStatus,
        EmployeeLookup, SelfProfile, CartridgeStock, SystemHelp, Clarification
    }

    public sealed class AiRoute
    {
        public AiIntent Intent { get; set; }
        public string Term { get; set; }
        public bool Personal { get; set; }
        public string Status { get; set; }
        public bool ExpiredOnly { get; set; }
        public int DaysAhead { get; set; } = 90;
        public bool SortLowest { get; set; }
        public string Condition { get; set; }
    }

    /// <summary>Deterministic routing and extraction. The model never selects SQL or executes actions.</summary>
    public static class AiIntentRouter
    {
        public const int MaxQuestionLength = 1200;
        private static readonly HashSet<string> CommonWords = Words(
            "how many much the is are there in available left a an of do we have please me my show give tell " +
            "what which any for on with by at to and or it its s you your know about can could would " +
            "does did i am this that these those all total number count list get find check see currently when " +
            "ang mga ilan ilang meron mayroon natitira po ba ng sa ako ko akin naman na");
        private static readonly HashSet<string> StockWords = Words(
            "stock stocks item items product products unit units quantity quantities level levels " +
            "low lowest running out remaining inventory brand new refilled");
        // Only known nouns are normalized. Brand names and model identifiers stay intact.
        private static readonly Dictionary<string, string> StockNouns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "keyboards", "keyboard" }, { "mice", "mouse" }, { "laptops", "laptop" },
            { "monitors", "monitor" }, { "printers", "printer" }, { "batteries", "battery" },
            { "computers", "computer" }, { "cables", "cable" }, { "toners", "toner" },
            { "cartridges", "cartridge" }, { "consumables", "consumable" }, { "inks", "ink" }, { "heads", "head" }
            ,{ "phones", "phone" }, { "cellphones", "cellphone" }, { "smartphones", "smartphone" }
        };

        private static HashSet<string> Words(string words)
        {
            return new HashSet<string>(words.Split(' '), StringComparer.OrdinalIgnoreCase);
        }

        internal static bool Has(string text, string pattern)
        {
            return Regex.IsMatch(text ?? "", pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        internal static List<string> SearchTokens(string text, string extraStopWords = "", bool normalizeStock = false)
        {
            var extra = Words(extraStopWords);
            var result = new List<string>();
            foreach (Match match in Regex.Matches(text ?? "", @"[\p{L}\p{Nd}]+(?:[-'][\p{L}\p{Nd}]+)*"))
            {
                string token = match.Value;
                if (token.Length < 2 || CommonWords.Contains(token) || extra.Contains(token)) continue;
                if (normalizeStock)
                {
                    if (StockWords.Contains(token)) continue;
                    string singular;
                    if (StockNouns.TryGetValue(token, out singular)) token = singular;
                }
                if (!result.Contains(token, StringComparer.OrdinalIgnoreCase)) result.Add(token);
            }
            return result;
        }

        private static string StockTerm(string question, string extra = "")
        {
            return string.Join(" ", SearchTokens(question, extra, true));
        }

        public static AiRoute Route(string question)
        {
            string q = (question ?? "").Trim();
            if (q.Length == 0) return new AiRoute { Intent = AiIntent.None };
            if (q.Length > MaxQuestionLength)
                return Clarify("Please shorten your question to " + MaxQuestionLength + " characters.");
            var ticket = Regex.Match(q, @"\b(?:TCK|ticket)[-\s#]*(\d+)\b", RegexOptions.IgnoreCase);
            if (ticket.Success)
            {
                int id;
                if (!int.TryParse(ticket.Groups[1].Value, out id) || id <= 0)
                    return Clarify("Please provide a valid ticket number.");
                return new AiRoute { Intent = AiIntent.Ticket, Term = id.ToString() };
            }
            var req = Regex.Match(q, @"\bREQ(?:UEST)?[-\s#]*(\d+)\b", RegexOptions.IgnoreCase);
            if (req.Success) return RecordRoute(AiIntent.RequestStatus, req.Groups[1].Value);
            var set = Regex.Match(q, @"\bSET[-\s#]*(\d+)\b", RegexOptions.IgnoreCase);
            if (set.Success) return RecordRoute(AiIntent.SetStatus, set.Groups[1].Value);
            if (Has(q, @"\b(policy|policies|guidelines?|handbook|memos?|rules)\b"))
                return new AiRoute { Intent = AiIntent.PolicyLookup, Term = q };
            if (Has(q, @"\b(who am i|do you know me|my (name|profile|employee details)|about me|sino ako)\b"))
                return new AiRoute { Intent = AiIntent.SelfProfile };
            if (Has(q, @"\bhow (do|can|should|to|would)\b|\bwhere (do|can|should)\b") ||
                Has(q, @"\b(contact IT|help me use|what can you do|system access|report (a |an )?(system )?issue)\b"))
                return new AiRoute { Intent = AiIntent.SystemHelp, Term = q };
            bool personal = Has(q, @"\b(my|mine|ako|akin|ko)\b|\b(assigned to|created by|borrowed by) me\b|\bdo i have\b");
            if (Has(q, @"\btickets?\b"))
            {
                if (Has(q, @"\b(today|yesterday|week|month|year|since|before|after|between|priority|critical|high|medium|low)\b"))
                    return Clarify("Ticket lookups currently support status and 'my tickets' across all dates. Please use IT Call Monitoring for date or priority filters.");
                if (SearchTokens(q, "ticket tickets solved resolved closed unsolved unresolved open pending in progress escalated cancelled assigned created status statuses").Count > 0)
                    return Clarify("I can filter tickets by status and your account. For another employee or other filters, use IT Call Monitoring or give a ticket number.");
                return new AiRoute { Intent = AiIntent.TicketCount, Personal = personal, Status = TicketStatus(q), Term = q };
            }
            if (Has(q, @"\brequests?\b"))
            {
                if (!personal) return Clarify("For requests, ask 'my requests' or give a request number, for example REQ-123.");
                if (Has(q, @"\b(today|yesterday|week|month|year|since|before|after|between)\b"))
                    return Clarify("Request lookups currently support 'my requests', 'my pending requests', or a request number. Use Request/s Portal for date filters.");
                return new AiRoute { Intent = AiIntent.MyRequests, Personal = true,
                    Status = Has(q, @"\b(open|pending|unfulfilled|outstanding)\b") ? "Open" :
                        Has(q, @"\brejected\b") ? "Rejected" : Has(q, @"\bcancelled\b") ? "Cancelled" : null };
            }
            if (Has(q, @"\b(borrow\w*|overdue)\b"))
            {
                if (Has(q, @"\boverdue\b")) return Clarify("Borrow records do not include a due date, so I cannot determine overdue items. Ask for open borrows instead.");
                return new AiRoute { Intent = AiIntent.BorrowStatus, Personal = personal,
                    Status = Has(q, @"\breturned\b") ? "Returned" : Has(q, @"\bhistory\b") ? "History" : "Open",
                    Term = StockTerm(q, "who borrow borrowed borrowing borrows equipment open returned returns history holder holders since status") };
            }
            if (Has(q, @"\b(renew\w*|expir\w*)\b"))
            {
                var days = Regex.Match(q, @"\b(\d+)\s*days?\b", RegexOptions.IgnoreCase);
                int horizon = 90;
                if (days.Success && (!int.TryParse(days.Groups[1].Value, out horizon) || horizon < 1 || horizon > 3650))
                    return Clarify("Please choose an expiry window between 1 and 3650 days.");
                if (Has(q, @"\b(week|month|year)\b") && !days.Success)
                    return Clarify("Please express the expiry window in days, for example 'renewals expiring within 30 days'.");
                return new AiRoute { Intent = AiIntent.RenewalsExpiry,
                    ExpiredOnly = Has(q, @"\b(expired|already|past)\b"), DaysAhead = horizon,
                    Term = string.Join(" ", SearchTokens(days.Success ? q.Remove(days.Index, days.Length) : q,
                        "when renewal renewals renew expire expires expiring expiry expired already past next within soon upcoming ends end date dates status")) };
            }
            if (Has(q, @"\b(who is|who's|employees?|staff member|do you know|tell me about)\b"))
                return new AiRoute { Intent = AiIntent.EmployeeLookup, Term = q };
            bool stock = Has(q, @"\b(stock|stocks|inventory|quantity|quantities)\b") ||
                (Has(q, @"\b(how many|how much|available|do we have|ilan|ilang|meron|mayroon)\b") &&
                 Has(q, @"\b(items?|keyboards?|mice|mouse|laptops?|computers?|monitors?|memory|ram|printers?|batteries|battery|cables?|toners?|inks?|cartridges?|consumables?)\b"));
            if (stock)
            {
                if (personal)
                    return Clarify("Stock lookups show shared available stock. For equipment assigned to you, use the inventory ownership pages; for loans, ask 'my borrowed items'.");
                bool cartridge = Has(q, @"\bcartridges?\b");
                bool consumable = Has(q, @"\b(consumables?|toners?|inks?|print\s*heads?)\b");
                if (!cartridge && Has(q, @"\b(brand new|refilled)\b"))
                    return Clarify("Condition-specific stock lookup currently supports cartridges. For other items, use the Items page's condition filter.");
                return new AiRoute
                {
                    Intent = cartridge ? AiIntent.CartridgeStock : consumable ? AiIntent.ConsumableStock : AiIntent.ItemStock,
                    Term = StockTerm(q, cartridge ? "cartridge cartridges" : "consumable consumables"),
                    SortLowest = Has(q, @"\b(low|lowest|running out)\b"),
                    Condition = Has(q, @"\brefilled\b") ? "Refilled" : Has(q, @"\bbrand new\b") ? "Brand New" : null
                };
            }
            if (Has(q, @"\b(consumables?|toners?|inks?|cartridges?)\b"))
                return Clarify("Ask for a model's stock, for example 'How many HP CF276X toners are in stock?', or ask for its policy.");
            return new AiRoute { Intent = AiIntent.None };
        }

        /// <summary>Re-run explicit total follow-ups against SQL; never ask the model to count earlier rows.</summary>
        public static AiRoute Route(string question, AiRoute previous)
        {
            if (Has(question, @"^\s*(and\s+)?(how many (in )?total\??|what is the total\??|total\??)\s*$"))
                return previous ?? Clarify("Please specify what to count, for example 'How many keyboards are in stock?'.");
            return Route(question);
        }

        private static string TicketStatus(string question)
        {
            if (Has(question, @"\b(unsolved|unresolved|open)\b")) return "Open";
            if (Has(question, @"\bclosed\b")) return "Closed";
            if (Has(question, @"\bsolved\b")) return "Solved";
            if (Has(question, @"\bresolved\b")) return "Resolved (Temporary)";
            if (Has(question, @"\bpending\b")) return "Pending";
            if (Has(question, @"\bin progress\b")) return "In Progress";
            if (Has(question, @"\bescalated\b")) return "Escalated";
            if (Has(question, @"\bcancelled\b")) return "Cancelled";
            return null;
        }

        private static AiRoute Clarify(string message)
        {
            return new AiRoute { Intent = AiIntent.Clarification, Term = message };
        }

        private static AiRoute RecordRoute(AiIntent intent, string digits)
        {
            int id;
            return int.TryParse(digits, out id) && id > 0
                ? new AiRoute { Intent = intent, Term = id.ToString() }
                : Clarify("Please provide a valid record number.");
        }
    }
}
