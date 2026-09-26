using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class DeptAliasMap
    {
        // IMPORTANT: canonical values (right-hand side) MUST exactly match
        // dbo.Department.Name in the database - SetBulkDeployRepository.
        // GetDepartmentIdsAsync() keys its lookup dictionary by that exact
        // column, and CreateDeployedSetsAsync fails the whole bundle with
        // "Unknown department" if TryCanonicalize's output isn't found there.
        // Verified against Yakult_Inventory_System_DEV.dbo.Department on
        // 2026-09-26 (DeptId/Name/Acronym): 66/Accounting Department/ACC,
        // 68/Audit Department/AUD, 69/Credit & Collection Department/CCD,
        // 81/DIRECT SALES DEPT./DS, 56/General Affairs Department/GA,
        // 84/GEN. AFFAIRS SECTION/GAS, 65/Finance Section/FIN,
        // 73/Shipping Department/SHP, 89/SHIPPING SECTION/SHPS,
        // 57/Purchasing Section/PRC, 86/PURCH. & MATERIALS PROP./PRC2,
        // 59/Engineering/ENG, 67/Legal Department/LGL, 58/Materials Section/MTR,
        // 70/Human Resources Department/PMD, 29/Information Technology/ITD,
        // 74/Storage Section/STR, 54/Pr & Science Department/PRSD,
        // 72/Provincial Dealers Department (PDD)/PDD, 71/Special Audit Section (SAS)/SAS.
        // No department named "Treasury" or with acronym "MPD" exists in the DB -
        // those two aliases have been removed (previously always failed silently
        // until commit, when they'd hit "Unknown department").
        public static readonly Dictionary<string, string> aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Audit Department", "Audit Department" },
                { "Audit", "Audit Department" },
                { "AUD", "Audit Department" },

                { "Accounting Department", "Accounting Department" },
                { "Accounting", "Accounting Department" },
                { "Acctg", "Accounting Department" },
                { "ACC", "Accounting Department" },

                { "Credit & Collection Department", "Credit & Collection Department" },
                { "Credit and Collection", "Credit & Collection Department" },
                { "Credit", "Credit & Collection Department" },
                { "CCD", "Credit & Collection Department" },

                { "DIRECT SALES DEPT.", "DIRECT SALES DEPT." },
                { "Direct Sales", "DIRECT SALES DEPT." },
                { "DS", "DIRECT SALES DEPT." },

                { "Human Resources Department", "Human Resources Department" },
                { "PMD", "Human Resources Department" },
                { "Personnel", "Human Resources Department" },

                { "Provincial Dealers Department (PDD)", "Provincial Dealers Department (PDD)" },
                { "PDD", "Provincial Dealers Department (PDD)" },

                { "General Affairs Department", "General Affairs Department" },
                { "Gen. Affairs", "General Affairs Department" },
                { "GA", "General Affairs Department" },

                { "GEN. AFFAIRS SECTION", "GEN. AFFAIRS SECTION" },
                { "GAS", "GEN. AFFAIRS SECTION" },

                { "Pr & Science Department", "Pr & Science Department" },
                { "PRSD", "Pr & Science Department" },

                { "Finance Section", "Finance Section" },
                { "Finance", "Finance Section" },
                { "FIN", "Finance Section" },

                { "Shipping Department", "Shipping Department" },
                { "Shipping", "Shipping Department" },
                { "SHP", "Shipping Department" },

                { "SHIPPING SECTION", "SHIPPING SECTION" },
                { "SHPS", "SHIPPING SECTION" },

                { "Purchasing Section", "Purchasing Section" },
                { "Purchasing", "Purchasing Section" },
                { "PRC", "Purchasing Section" },

                { "PURCH. & MATERIALS PROP.", "PURCH. & MATERIALS PROP." },
                { "PRC2", "PURCH. & MATERIALS PROP." },

                { "Engineering", "Engineering" },
                { "ENG", "Engineering" },

                { "Legal Department", "Legal Department" },
                { "Legal", "Legal Department" },
                { "LGL", "Legal Department" },

                { "Materials Section", "Materials Section" },
                { "Materials", "Materials Section" },
                { "MATERIALS1", "Materials Section" },
                { "MATERIALS2", "Materials Section" },
                { "MATERIALS3", "Materials Section" },
                { "MATERIALS6", "Materials Section" },
                { "MATERIALS7", "Materials Section" },
                { "MAT7", "Materials Section" },
                { "MTR", "Materials Section" },

                { "Information Technology", "Information Technology" },
                { "IT", "Information Technology" },
                { "ITD7", "Information Technology" },
                { "ITD", "Information Technology" },

                { "Storage Section", "Storage Section" },
                { "Storage", "Storage Section" },
                { "STR", "Storage Section" },

                { "Special Audit Section (SAS)", "Special Audit Section (SAS)" },
                { "SAS", "Special Audit Section (SAS)" },

                { "Cashier", "Cashier" },
                { "CSH", "Cashier" },

                { "Merchandising DS", "Merchandising DS" },
                { "MRC", "Merchandising DS" },

                { "BOTTLEMAKING SECTION", "BOTTLEMAKING SECTION" },
                { "BMS", "BOTTLEMAKING SECTION" },

                { "BOTTLING SECTION", "BOTTLING SECTION" },
                { "BLS", "BOTTLING SECTION" },

                { "DISS. & PAST. SECTION", "DISS. & PAST. SECTION" },
                { "DPS", "DISS. & PAST. SECTION" },

                { "ENG'G. & MAINT. SECTION", "ENG'G. & MAINT. SECTION" },
                { "ENGMTS", "ENG'G. & MAINT. SECTION" },

                { "PRODUCTION SECTION", "PRODUCTION SECTION" },
                { "PRD", "PRODUCTION SECTION" },

                { "QUALITY CONTROL SECTION", "QUALITY CONTROL SECTION" },
                { "QCS", "QUALITY CONTROL SECTION" },

                { "SALES DIVISION", "SALES DIVISION" },
                { "SLS", "SALES DIVISION" },

                { "UTILITY CONTROL SECTION", "UTILITY CONTROL SECTION" },
                { "UTC", "UTILITY CONTROL SECTION" },

                { "Factory, Calamba", "Factory, Calamba" },
                { "FCT", "Factory, Calamba" },

                { "YMC - HO", "YMC - HO" },
                { "YMC", "YMC - HO" },

                { "YES - Yakult El Salvador Manufacturing Corp.", "YES - Yakult El Salvador Manufacturing Corp." },
                { "YES", "YES - Yakult El Salvador Manufacturing Corp." },

                { "NBI", "NBI" },
                { "PNP", "PNP" },
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
