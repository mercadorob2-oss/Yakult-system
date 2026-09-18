using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class CartridgeTransmittalViewModel
    {
        // ── Known named-checkbox models ───────────────────────────────────────────
        // Normalize by stripping ALL non-alphanumeric characters before comparing,
        // so "LX-310", "lx 310", "LX310" etc. all match "LX310".
        // To add a new model: add it here AND add a matching checkbox row in the XAML.
        private static readonly HashSet<string> KnownNormalizedModels =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LX300", "LX310" };

        // ── Request data ──────────────────────────────────────────────────────────
        public string To             { get; set; }
        public string Date           { get; set; }
        public string From           { get; } = "INFORMATION TECHNOLOGY DEPARTMENT";
        public string RequesterName  { get; set; }
        public string BranchName     { get; set; }
        public string DepartmentName { get; set; }
        public int    Quantity       { get; set; }

        // ── Company selection (checkable in PrepareTransmittalDialog) ─────────────
        public bool IsYakultPhilippines { get; set; }
        public bool IsYakultMarketing   { get; set; }

        // ── Copy type — controls which box is checked in GATEPASS/TRANSMITTAL/FILE ─
        // Set by TransmittalPrintService before each page is rendered.
        public bool IsGatepassCopy    { get; set; }
        public bool IsTransmittalCopy { get; set; }
        public bool IsFileCopy        { get; set; }

        // ── Cartridge model flags ─────────────────────────────────────────────────
        public bool   IsCartridgeRibbon { get; } = true;
        public bool   IsLx300           { get; set; }
        public bool   IsLx310           { get; set; }
        public bool   IsOtherModel      { get; set; }
        public string OtherModelName    { get; set; }
        public Visibility OtherModelVisibility =>
            IsOtherModel ? Visibility.Visible : Visibility.Collapsed;

        // ── Fields editable in PrepareTransmittalDialog ───────────────────────────
        public string IssuedBy     { get; set; }
        public string NotedBy      { get; set; }
        public string ApprovedBy   { get; set; }
        public string ApprovalDate { get; set; }
        public string ReceivedBy   { get; set; }
        public string ReceivedDate { get; set; }
        public string Others       { get; set; }
        public string Remarks      { get; set; }

        // ── Model detection ───────────────────────────────────────────────────────
        // Strips spaces, hyphens, underscores, and dots before comparing so that
        // "LX-310", "lx 310", "Lx.310" all match the known model "LX310".
        private static string NormalizeModel(string raw) =>
            Regex.Replace(raw ?? "", @"[\s\-_.]", "").ToUpperInvariant();

        private static (bool isLx300, bool isLx310, bool isOther, string otherName)
            DetectModel(string rawModel)
        {
            string normalized = NormalizeModel(rawModel);
            bool isLx300 = normalized.Contains("LX300");
            bool isLx310 = normalized.Contains("LX310");
            bool isKnown = isLx300 || isLx310;
            bool isOther = !isKnown && !string.IsNullOrWhiteSpace(normalized);
            return (isLx300, isLx310, isOther, isOther ? rawModel?.Trim() ?? "" : "");
        }

        private static string BuildToLine(string dept, string branch)
        {
            var parts = new[] { dept?.Trim(), branch?.Trim() }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join(", ", parts);
        }

        // Matches "YAKULT PHILIPPINES INC.", "Yakult Philippines, Inc", the short
        // codes "YPI"/"YMC" stored directly on some request records, etc.
        private static (bool isYPI, bool isYMC) DetectCompany(string companyName)
        {
            string n = NormalizeModel(companyName ?? "");
            bool isYPI = n.Contains("PHILIPPINES") || n == "YPI";
            bool isYMC = n.Contains("MARKETING") || n == "YMC";
            return (isYPI, isYMC);
        }

        // ── Shallow clone for rendering multiple copy-type pages side by side ─────
        public CartridgeTransmittalViewModel Clone() =>
            (CartridgeTransmittalViewModel)MemberwiseClone();

        // ── Factory: single-model request ─────────────────────────────────────────
        public static CartridgeTransmittalViewModel FromRequest(CartridgeRequestDto req)
        {
            string rawModel = req.TypedModelNumber ?? req.ModelNumber ?? "";
            var (isLx300, isLx310, isOther, otherName) = DetectModel(rawModel);
            var (isYPI, isYMC) = DetectCompany(req.CompanyName);

            return new CartridgeTransmittalViewModel
            {
                To                   = BuildToLine(req.DepartmentName, req.BranchName),
                Date                 = DateTime.Now.ToString("MM/dd/yyyy"),
                RequesterName        = req.EmployeeName ?? "",
                BranchName           = req.BranchName ?? "",
                DepartmentName       = req.DepartmentName ?? "",
                Remarks              = req.AdditionalRemarks ?? "",
                IssuedBy             = AppSession.CurrentEmployeeName ?? "",
                ReceivedBy           = req.ReceivedByName ?? req.EmployeeName ?? "",
                IsYakultPhilippines  = isYPI,
                IsYakultMarketing    = isYMC,
                IsGatepassCopy       = true,
                IsTransmittalCopy    = true,
                IsFileCopy           = true,
                IsLx300              = isLx300,
                IsLx310              = isLx310,
                IsOtherModel         = isOther,
                OtherModelName       = otherName,
                Quantity             = req.Quantity
            };
        }

        // ── Factory: fulfilled history row (reprint) ──────────────────────────────
        // row.CartridgeModel may hold several comma-separated model names when the
        // original request spanned multiple cartridge models — detect each one so
        // every model's checkbox gets checked on the reprinted transmittal.
        public static CartridgeTransmittalViewModel FromHistory(FulfilledCartridgeRowDto row)
        {
            var modelNames = (row.CartridgeModel ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            bool anyLx300 = false, anyLx310 = false, anyOther = false;
            var otherNames = new List<string>();

            foreach (var modelName in modelNames)
            {
                var (isLx300, isLx310, isOther, otherName) = DetectModel(modelName);
                anyLx300 = anyLx300 || isLx300;
                anyLx310 = anyLx310 || isLx310;
                if (isOther && !otherNames.Contains(otherName, StringComparer.OrdinalIgnoreCase))
                    otherNames.Add(otherName);
                anyOther = anyOther || isOther;
            }

            var (isYPI, isYMC) = DetectCompany(row.CompanyName);

            return new CartridgeTransmittalViewModel
            {
                To                   = BuildToLine(row.DepartmentName, row.BranchName),
                Date                 = row.FulfilledAtDisplay,
                RequesterName        = row.RequesterName,
                BranchName           = row.BranchName,
                DepartmentName       = row.DepartmentName,
                IssuedBy             = AppSession.CurrentEmployeeName ?? "",
                ReceivedBy           = row.ReceivedByName,
                IsYakultPhilippines  = isYPI,
                IsYakultMarketing    = isYMC,
                IsGatepassCopy       = true,
                IsTransmittalCopy    = true,
                IsFileCopy           = true,
                IsLx300              = anyLx300,
                IsLx310              = anyLx310,
                IsOtherModel         = anyOther,
                OtherModelName       = string.Join(", ", otherNames),
                Quantity             = row.Quantity > 0 ? row.Quantity : row.TotalIssuedQty,
            };
        }

        // ── Factory: multi-model session request ──────────────────────────────────
        public static CartridgeTransmittalViewModel FromMultiModel(
            IEnumerable<CartridgeRequestDto> requests)
        {
            var list   = requests?.ToList() ?? new List<CartridgeRequestDto>();
            var anchor = list.FirstOrDefault();
            if (anchor == null) return new CartridgeTransmittalViewModel();

            bool anyLx300 = false, anyLx310 = false, anyOther = false;
            var otherNames = new List<string>();

            foreach (var req in list)
            {
                var (isLx300, isLx310, isOther, otherName) =
                    DetectModel(req.TypedModelNumber ?? req.ModelNumber ?? "");
                anyLx300 = anyLx300 || isLx300;
                anyLx310 = anyLx310 || isLx310;
                if (isOther && !otherNames.Contains(otherName, StringComparer.OrdinalIgnoreCase))
                    otherNames.Add(otherName);
                anyOther = anyOther || isOther;
            }

            var (isYPI, isYMC) = DetectCompany(anchor.CompanyName);

            return new CartridgeTransmittalViewModel
            {
                To                   = BuildToLine(anchor.DepartmentName, anchor.BranchName),
                Date                 = DateTime.Now.ToString("MM/dd/yyyy"),
                RequesterName        = anchor.EmployeeName ?? "",
                BranchName           = anchor.BranchName ?? "",
                DepartmentName       = anchor.DepartmentName ?? "",
                Remarks              = anchor.AdditionalRemarks ?? "",
                IssuedBy             = AppSession.CurrentEmployeeName ?? "",
                ReceivedBy           = anchor.ReceivedByName ?? anchor.EmployeeName ?? "",
                IsYakultPhilippines  = isYPI,
                IsYakultMarketing    = isYMC,
                IsGatepassCopy       = true,
                IsTransmittalCopy    = true,
                IsFileCopy           = true,
                IsLx300              = anyLx300,
                IsLx310              = anyLx310,
                IsOtherModel         = anyOther,
                OtherModelName       = string.Join(", ", otherNames),
                Quantity             = list.Sum(r => r.Quantity)
            };
        }
    }
}
