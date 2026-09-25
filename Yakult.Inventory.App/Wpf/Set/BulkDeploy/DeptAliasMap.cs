using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class DeptAliasMap
    {
        public static readonly Dictionary<string, string> aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Audit Department", "Audit" },
                { "Audit", "Audit" },
                { "Accounting", "Accounting" },
                { "Acctg", "Accounting" },
                { "Credit and Collection", "Credit and Collection" },
                { "Credit", "Credit and Collection" },
                { "Direct Sales", "Direct Sales" },
                { "PMD", "PMD" },
                { "PDD", "PDD" },
                { "Gen. Affairs", "Gen. Affairs" },
                { "GA", "Gen. Affairs" },
                { "PRSD", "PRSD" },
                { "Finance", "Finance" },
                { "Shipping", "Shipping" },
                { "Purchasing", "Purchasing" },
                { "Engineering", "Engineering" },
                { "Treasury", "Treasury" },
                { "MPD", "MPD" },
                { "Legal", "Legal" },
                { "Materials", "Materials" },
                { "MATERIALS1", "Materials" },
                { "MATERIALS2", "Materials" },
                { "MATERIALS3", "Materials" },
                { "MATERIALS6", "Materials" },
                { "MATERIALS7", "Materials" },
                { "MAT7", "Materials" },
                { "PERSONNEL", "Personnel" },
                { "ITD7", "IT" },
                { "Storage", "Storage" },
            };

        public static IReadOnlyList<string> CanonicalDepartments
        {
            get
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var ordered = new List<string>();
                foreach (var canonical in aliases.Values)
                {
                    if (seen.Add(canonical))
                        ordered.Add(canonical);
                }
                ordered.Sort(StringComparer.OrdinalIgnoreCase);
                return ordered;
            }
        }

        public static bool TryCanonicalize(string raw, out string canonical)
        {
            canonical = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string key = raw.Trim();
            if (aliases.TryGetValue(key, out string value))
            {
                canonical = value;
                return true;
            }
            return false;
        }
    }
}
