using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public sealed class BulkDeployViewModel
    {
        public ObservableCollection<BulkDeployRow> Rows { get; } = new ObservableCollection<BulkDeployRow>();

        public ObservableCollection<string> DeptOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> CategoryOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> RoleOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> ConditionOptions { get; } = new ObservableCollection<string>();

        public BulkDeployViewModel()
        {
            EnsureSeeded(300);
            foreach (var dept in DeptAliasMap.CanonicalDepartments)
                DeptOptions.Add(dept);
            foreach (var role in BulkDeployValidator.ValidRoles)
                RoleOptions.Add(role);
            foreach (var condition in BulkDeployValidator.ValidConditions)
                ConditionOptions.Add(condition);
        }

        public async Task LoadCategoryOptionsAsync()
        {
            List<string> names = null;
            try
            {
                names = await new SetBulkDeployRepository().GetCategoryNamesAsync();
            }
            catch
            {
                names = null;
            }
            if (names == null || names.Count == 0)
                names = new List<string> { "Desktop", "Monitor", "Laptop", "Printer", "Accessories", "Other" };
            CategoryOptions.Clear();
            foreach (var name in names)
                CategoryOptions.Add(name);
        }

        public void EnsureSeeded(int count)
        {
            for (int i = Rows.Count; i < count; i++)
                Rows.Add(new BulkDeployRow());
        }

        public async Task ValidateAllAsync()
        {
            var serials = Rows
                .Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber))
                .Select(r => r.SerialNumber.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            HashSet<string> existing = await LoadExistingSerialsAsync(serials);
            Dictionary<string, BulkDeployEmployee> employees = await LoadEmployeeLookupAsync();
            Dictionary<string, int> companies = await LoadCompanyLookupAsync();
            Dictionary<string, int> branches = await LoadBranchLookupAsync();

            var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var computerToBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in Rows)
            {
                if (IsBlankRow(row))
                {
                    row.RowStatus = "Pending";
                    row.RowMessage = "";
                    row.MatchedEmployeeId = null;
                    row.MatchedEmployeeName = null;
                    row.MatchedCompanyId = null;
                    row.MatchedBranchId = null;
                    continue;
                }
                BulkDeployValidationResult result =
                    BulkDeployValidator.ValidateRow(row, batch, existing, computerToBundle, employees, companies, branches);

                row.RowStatus = result.IsError ? "Error" : result.IsWarning ? "Warning" : "Valid";
                row.RowMessage = result.Message;
                row.MatchedEmployeeId = result.MatchedEmployeeId;
                row.MatchedEmployeeName = result.MatchedEmployeeName;
                row.MatchedCompanyId = result.MatchedCompanyId;
                row.MatchedBranchId = result.MatchedBranchId;

                string serial = row.SerialNumber != null ? row.SerialNumber.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(serial)) batch.Add(serial);

                string computer = row.ComputerName != null ? row.ComputerName.Trim() : string.Empty;
                string bundle = row.BundleKey != null ? row.BundleKey.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(computer) && !string.IsNullOrEmpty(bundle)
                    && !computerToBundle.ContainsKey(computer))
                    computerToBundle[computer] = bundle;
            }

            EnforceSingleEmployeePerBundle();
        }

        private void EnforceSingleEmployeePerBundle()
        {
            foreach (var group in Rows
                .Where(r => r.RowStatus != "Error" && !IsBlankRow(r) && !string.IsNullOrWhiteSpace(r.BundleKey))
                .GroupBy(r => r.BundleKey.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var ids = group
                    .Where(r => r.MatchedEmployeeId.HasValue)
                    .Select(r => r.MatchedEmployeeId.Value)
                    .Distinct()
                    .ToList();
                if (ids.Count > 1)
                {
                    foreach (var row in group)
                    {
                        row.RowStatus = "Error";
                        row.RowMessage = ((row.RowMessage ?? "") + " Mixed employees in bundle '" + group.Key + "'.").Trim();
                    }
                }
            }
        }

        private async Task<HashSet<string>> LoadExistingSerialsAsync(List<string> serials)
        {
            return await new SetBulkDeployRepository().GetExistingSerialsAsync(serials);
        }

        private async Task<Dictionary<string, BulkDeployEmployee>> LoadEmployeeLookupAsync()
        {
            try
            {
                return await new SetBulkDeployRepository().GetEmployeeLookupAsync();
            }
            catch
            {
                return new Dictionary<string, BulkDeployEmployee>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task<Dictionary<string, int>> LoadCompanyLookupAsync()
        {
            try
            {
                return await new SetBulkDeployRepository().GetCompanyIdsAsync();
            }
            catch
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task<Dictionary<string, int>> LoadBranchLookupAsync()
        {
            try
            {
                return await new SetBulkDeployRepository().GetBranchIdsAsync();
            }
            catch
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static bool IsBlankRow(BulkDeployRow row)
        {
            return row != null
                && string.IsNullOrWhiteSpace(row.BundleKey)
                && string.IsNullOrWhiteSpace(row.ItemName)
                && string.IsNullOrWhiteSpace(row.SerialNumber)
                && string.IsNullOrWhiteSpace(row.ComputerName)
                && string.IsNullOrWhiteSpace(row.Employee)
                && string.IsNullOrWhiteSpace(row.Company)
                && string.IsNullOrWhiteSpace(row.Branch);
        }

        public async Task<SetBulkDeployRepository.BulkDeployResult> CommitBundlesAsync(
            List<BulkDeployRow> rows,
            Dictionary<string, DateTime?> bundleDates,
            int createdByUserId)
        {
            return await new SetBulkDeployRepository()
                .CreateDeployedSetsAsync(rows, bundleDates, createdByUserId);
        }
    }
}
