using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>
    /// Shared multi-term, case-insensitive search matching for list-page search boxes.
    /// Splits the query on whitespace and requires every term to appear in at least one of
    /// the row's searchable fields (AND across terms, OR across fields) — so "yakult 2024"
    /// matches a row where "Yakult" is in one field and "2024" is in a different field,
    /// which a single whole-string Contains check would miss. Uses
    /// StringComparison.OrdinalIgnoreCase (not ToLower()+Contains()) so matching never depends
    /// on the current thread culture (e.g. the Turkish-I problem, where "I".ToLower() != "i").
    /// </summary>
    public static class SearchTextHelper
    {
        public static string[] SplitTerms(string query) =>
            string.IsNullOrWhiteSpace(query)
                ? Array.Empty<string>()
                : query.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        public static bool MatchesAllTerms(string[] terms, IEnumerable<string> fields)
        {
            if (terms == null || terms.Length == 0) return true;
            var fieldList = fields as IList<string> ?? fields.ToList();
            return terms.All(term => fieldList.Any(f => f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        /// <summary>A term that could plausibly be a mistyped identifier — SetCode, Serial, Model,
        /// Document/Reference Number — rather than a descriptive word like a company or status name.
        /// Used by each list page's "Did you mean...?" fuzzy fallback to decide which terms are worth
        /// relaxing into an edit-distance comparison and which must still match exactly.</summary>
        public static bool LooksLikeCode(string term) =>
            !string.IsNullOrEmpty(term) && (term.IndexOf('-') >= 0 || term.Any(char.IsDigit));

        /// <summary>
        /// Classic Wagner–Fischer edit distance (insert/delete/substitute, all cost 1),
        /// case-insensitive. Only ever called against short identifier-shaped values (a handful of
        /// candidate rows on a fuzzy-fallback path, never the hot filtering path), so the O(a·b) cost
        /// is trivial in practice.
        /// </summary>
        public static int LevenshteinDistance(string a, string b)
        {
            a = (a ?? "").ToLowerInvariant();
            b = (b ?? "").ToLowerInvariant();
            int la = a.Length, lb = b.Length;
            if (la == 0) return lb;
            if (lb == 0) return la;

            var prev = new int[lb + 1];
            var curr = new int[lb + 1];
            for (int j = 0; j <= lb; j++) prev[j] = j;

            for (int i = 1; i <= la; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= lb; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var swap = prev; prev = curr; curr = swap;
            }
            return prev[lb];
        }

        /// <summary>
        /// Edit distance between <paramref name="term"/> and the CLOSEST word inside
        /// <paramref name="field"/>, not the whole field. Plain LevenshteinDistance(term, field) only
        /// works when field is itself a single short token (a SetCode, a serial number); against a
        /// multi-word field like an item Name ("XIAOMI REDMI 15 5G (8GB 256GB)"), the distance from a
        /// short mistyped brand ("xiaoomi") to the ENTIRE string is dominated by the length difference
        /// and can never land within a strict threshold — even though the one word that matters
        /// ("XIAOMI") is one character away. Splits on whitespace and also checks the field as a
        /// whole (covers single-word fields), returning the smallest distance found.
        /// </summary>
        public static int MinWordDistance(string term, string field)
        {
            if (string.IsNullOrWhiteSpace(field)) return int.MaxValue;

            int best = LevenshteinDistance(term, field);
            foreach (var word in field.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                int d = LevenshteinDistance(term, word);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
