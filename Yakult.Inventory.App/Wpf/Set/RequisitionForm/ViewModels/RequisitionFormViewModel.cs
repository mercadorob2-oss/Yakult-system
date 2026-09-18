using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels
{
    public class RequisitionFormItem
    {
        public string Quantity    { get; set; }
        public string Description { get; set; }
        public string Remarks     { get; set; }
    }

    public class RequisitionFormViewModel
    {
        // ── Company header ────────────────────────────────────────────────────────
        public bool IsYakultPhilippines { get; set; }
        public bool IsYakultMarketing   { get; set; }
        public bool IsYakultElSalvador  { get; set; }

        // ── Header fields ─────────────────────────────────────────────────────────
        public string Department { get; set; }
        public string Date       { get; set; }

        // "cc: ITD/Requested by (Department)" footnote value — same resolved
        // department-or-branch value shown in the DEPARTMENT field above.
        public string CcRequestedBy { get; set; }

        // Employee this request/item was made for (the recipient), shown below
        // DEPARTMENT. Null/blank for department-level requests with no specific
        // employee attached.
        public string Employee { get; set; }

        // ── Line items ────────────────────────────────────────────────────────────
        public List<RequisitionFormItem> Items { get; set; } = new List<RequisitionFormItem>();

        // ── Signatories — editable in PrepareRequisitionDialog ────────────────────
        public string PreparedByName     { get; set; }
        public string PreparedByPosition { get; set; }
        public string NotedByName        { get; set; }
        public string NotedByPosition    { get; set; }
        public string ApprovedByName     { get; set; }
        public string ApprovedByPosition { get; set; }
        public string ReceivedByLabel    { get; set; } = "";
        public string ReceivedByPosition { get; set; } = "";

        // Matches "YAKULT PHILIPPINES INC.", "Yakult Philippines, Inc", the short
        // codes "YPI"/"YMC" stored directly on some employee/set records, etc.
        private static (bool isYPI, bool isYMC) DetectCompany(string companyName)
        {
            string n = Regex.Replace(companyName ?? "", @"[\s\-_.]", "").ToUpperInvariant();
            bool isYPI = n.Contains("PHILIPPINES") || n == "YPI";
            bool isYMC = n.Contains("MARKETING") || n == "YMC";
            return (isYPI, isYMC);
        }

        private static string BuildDescription(SetDetailRequestDto req)
        {
            string desc = !string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName : req.Description ?? "";
            if (!string.IsNullOrWhiteSpace(req.ModelNumber))
                desc += $" ({req.ModelNumber})";
            if (!string.IsNullOrWhiteSpace(req.SerialNumber))
                desc += $" - SN: {req.SerialNumber}";
            return desc;
        }

        // Cartridge requests used to store the "With Cartridge"/"Without Cartridge"
        // condition as a literal prefix inside the Remarks column (see
        // RequesterPortalService.StripCartridgeCondition for the original writer).
        // That storage convention is legacy and shouldn't surface on the printed
        // requisition form, so strip it back out here rather than showing it as if it
        // were an actual remark.
        private static string StripCartridgeCondition(string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return "";

            const string sep = " | ";
            foreach (var prefix in new[] { "With Cartridge", "Without Cartridge" })
            {
                if (remarks.StartsWith(prefix + sep, StringComparison.OrdinalIgnoreCase))
                    return remarks.Substring(prefix.Length + sep.Length);
                if (string.Equals(remarks.Trim(), prefix, StringComparison.OrdinalIgnoreCase))
                    return "";
            }

            return remarks;
        }

        public RequisitionFormViewModel Clone() => (RequisitionFormViewModel)MemberwiseClone();

        public static RequisitionFormViewModel FromSet(
            SetDto set, List<SetDetailRequestDto> requests, EmployeeDetailDto employeeDetail)
        {
            var (isYPI, isYMC) = DetectCompany(employeeDetail?.CompanyName ?? set.CurrentCompanyName);

            var items = (requests ?? new List<SetDetailRequestDto>())
                .Select(r => new RequisitionFormItem
                {
                    Quantity    = r.Quantity.ToString(),
                    Description = BuildDescription(r),
                    Remarks     = StripCartridgeCondition(r.Remarks)
                })
                .ToList();

            // Some requesters (department-level requests) have no Department set on their
            // record — only a Branch. When that happens, show the branch in place of the
            // department value (the DEPARTMENT: label itself stays the same).
            string departmentOrBranch =
                !string.IsNullOrWhiteSpace(employeeDetail?.DepartmentName) ? employeeDetail.DepartmentName :
                !string.IsNullOrWhiteSpace(set.CurrentDepartmentName)      ? set.CurrentDepartmentName :
                !string.IsNullOrWhiteSpace(employeeDetail?.BranchName)     ? employeeDetail.BranchName :
                set.CurrentBranchName ?? "";

            return new RequisitionFormViewModel
            {
                IsYakultPhilippines = isYPI,
                IsYakultMarketing   = isYMC,
                Department          = departmentOrBranch,
                Date                = (set.DateRequested ?? set.CreatedAt).ToString("MM/dd/yyyy"),
                Items               = items,
                PreparedByName      = AppSession.CurrentEmployeeName ?? "",
                CcRequestedBy       = departmentOrBranch,
                Employee            = employeeDetail?.EmployeeName ?? set.CurrentEmployeeName
            };
        }
    }
}
