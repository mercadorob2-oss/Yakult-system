using System;
using System.Collections.Generic;
using System.Net;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public sealed class BulkDeployValidationResult
    {
        public bool IsError { get; set; }
        public bool IsWarning { get; set; }
        public string Message { get; set; } = "";
        public string CanonicalDept { get; set; }
        public bool ReusesExistingItem { get; set; }
        public DateTime? ParsedDate { get; set; }
        public int? MatchedEmployeeId { get; set; }
        public string MatchedEmployeeName { get; set; }
        public int? MatchedCompanyId { get; set; }
        public int? MatchedBranchId { get; set; }
    }

    public static class BulkDeployValidator
    {
        public static readonly HashSet<string> ValidRoles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CPU", "Monitor", "Laptop", "Printer", "Keyboard",
                "Mouse", "UPS", "Charger", "Dock", "Other"
            };

        public static readonly HashSet<string> ValidConditions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Good", "Damaged"
            };

        public static BulkDeployValidationResult ValidateRow(
            BulkDeployRow row,
            HashSet<string> serialsInBatch,
            HashSet<string> existingSerialsInDb,
            Dictionary<string, string> computerToBundle,
            Dictionary<string, BulkDeployEmployee> employeeLookup = null,
            Dictionary<string, int> companyLookup = null,
            Dictionary<string, int> branchLookup = null,
            HashSet<string> validCategories = null)
        {
            var result = new BulkDeployValidationResult();
            var errors = new List<string>();
            var warnings = new List<string>();

            if (row == null)
            {
                result.IsError = true;
                result.Message = "Row is null.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(row.BundleKey))
                errors.Add("BundleKey is required.");
            if (string.IsNullOrWhiteSpace(row.ItemName))
                errors.Add("ItemName is required.");
            if (string.IsNullOrWhiteSpace(row.Category))
                errors.Add("Category is required.");
            else if (validCategories != null && validCategories.Count > 0
                     && !validCategories.Contains(row.Category.Trim()))
                // dbo.Item.CategoryId is NOT NULL and is resolved at commit time
                // by an exact-name lookup against dbo.ItemCategory with no
                // fallback - an unmatched Category silently produces a NULL
                // insert that only fails at the database. Catching it here
                // gives a row-level error message instead of a whole-bundle
                // SQL failure during Create.
                errors.Add("Unknown Category '" + row.Category.Trim() + "' (not in dbo.ItemCategory).");

            if (string.IsNullOrWhiteSpace(row.ItemRole) || !ValidRoles.Contains(row.ItemRole.Trim()))
                errors.Add("Invalid ItemRole.");

            if (row.Quantity < 1 || row.Quantity > 3)
                errors.Add("Quantity must be 1-3.");

            if (!string.IsNullOrWhiteSpace(row.Condition) && !ValidConditions.Contains(row.Condition.Trim()))
                errors.Add("Invalid Condition.");

            string serial = row.SerialNumber != null ? row.SerialNumber.Trim() : "";
            if (string.IsNullOrEmpty(serial))
            {
                warnings.Add("Serial number blank, pending follow-up.");
            }
            else if (ContainsIgnoreCase(serialsInBatch, serial))
            {
                errors.Add("Duplicate serial in batch.");
            }
            else if (ContainsIgnoreCase(existingSerialsInDb, serial))
            {
                warnings.Add("Serial exists in DB; reuses existing item.");
                result.ReusesExistingItem = true;
            }

            if (DeptAliasMap.TryCanonicalize(row.Department, out string canonical))
            {
                result.CanonicalDept = canonical;
            }
            else
            {
                errors.Add("Unknown department.");
            }

            string computer = row.ComputerName != null ? row.ComputerName.Trim() : "";
            string bundle = row.BundleKey != null ? row.BundleKey.Trim() : "";
            if (!string.IsNullOrEmpty(computer) && computerToBundle != null)
            {
                string mapped = null;
                foreach (var kvp in computerToBundle)
                {
                    if (string.Equals(kvp.Key != null ? kvp.Key.Trim() : "", computer, StringComparison.OrdinalIgnoreCase))
                    {
                        mapped = kvp.Value;
                        break;
                    }
                }
                if (mapped != null && !string.Equals(
                        mapped != null ? mapped.Trim() : "",
                        bundle, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("ComputerName mapped to a different bundle.");
                }
            }

            string ip = row.IPAddress != null ? row.IPAddress.Trim() : "";
            if (!string.IsNullOrEmpty(ip) && !IPAddress.TryParse(ip, out _))
                errors.Add("Invalid IPAddress.");

            string companyRaw = row.Company != null ? row.Company.Trim() : "";
            if (!string.IsNullOrEmpty(companyRaw))
            {
                int comId;
                if (companyLookup != null && companyLookup.TryGetValue(companyRaw, out comId))
                    result.MatchedCompanyId = comId;
                else
                    errors.Add("Unknown company '" + companyRaw + "'.");
            }

            string branchRaw = row.Branch != null ? row.Branch.Trim() : "";
            if (!string.IsNullOrEmpty(branchRaw))
            {
                int branchId;
                if (branchLookup != null && branchLookup.TryGetValue(branchRaw, out branchId))
                    result.MatchedBranchId = branchId;
                else
                    errors.Add("Unknown branch '" + branchRaw + "'.");
            }

            string empRaw = row.Employee != null ? row.Employee.Trim() : "";
            if (!string.IsNullOrEmpty(empRaw))
            {
                BulkDeployEmployee match = null;
                if (employeeLookup != null)
                    employeeLookup.TryGetValue(empRaw, out match);
                if (match == null)
                    errors.Add("Unknown employee '" + empRaw + "' (use Name or Employee #).");
                else
                {
                    result.MatchedEmployeeId = match.EmpId;
                    result.MatchedEmployeeName = match.Name;
                }
            }

            string dateText = row.DateDeployedText != null ? row.DateDeployedText.Trim() : "";
            if (string.IsNullOrEmpty(dateText))
            {
                result.ParsedDate = null;
            }
            else if (DateTime.TryParse(dateText, out DateTime parsed))
            {
                result.ParsedDate = parsed;
            }
            else if (double.TryParse(dateText, out double oa))
            {
                try { result.ParsedDate = DateTime.FromOADate(oa); }
                catch { errors.Add("Invalid DateDeployed (use yyyy-mm-dd)."); }
            }
            else
            {
                errors.Add("Invalid DateDeployed (use yyyy-mm-dd).");
            }

            var notes = new List<string>(errors.Count + warnings.Count);
            notes.AddRange(errors);
            notes.AddRange(warnings);
            result.Message = string.Join(" ", notes);
            result.IsError = errors.Count > 0;
            result.IsWarning = !result.IsError && warnings.Count > 0;
            return result;
        }

        private static bool ContainsIgnoreCase(HashSet<string> set, string value)
        {
            if (set == null || value == null)
                return false;
            if (set.Contains(value))
                return true;
            foreach (var item in set)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
