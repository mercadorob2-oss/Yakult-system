using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// Fast-pass invoice importer. A flat CSV (one row per invoice line) is grouped by
    /// Document Number into Sales Invoice Sets. For every line a brand-new dbo.Item is
    /// created, then the whole group is handed to ServiceSetRepository.CreateSoftwareServiceSet,
    /// which writes the Set (IsInvoice=1) + SetItem rows AND seeds the Renewals row in one
    /// transaction — so the imported invoices show up on the Invoice, Set and Renewal pages
    /// exactly as if they had been entered by hand.
    /// </summary>
    public partial class InvoiceCsvImportDialog : Window
    {
        // Canonical column names, matched against the CSV header case/space-insensitively.
        // Grouped as: invoice-header fields, invoice financials (raw inputs — the importer
        // runs the ViewInvoiceDetailPage formula), then per-line item fields.
        private static readonly string[] TemplateColumns =
        {
            // ── Invoice (Set) header — read from the first row of each Document # group ──
            // Company + Distributor = the sender/giver. SiteCompany + SiteBranch + SiteDepartment
            // = the recipient, composed into the Set's "Site" text (all nullable).
            "DocumentNumber", "ReferenceNumber", "DocumentDate", "Company", "Distributor",
            "SiteCompany", "SiteBranch", "SiteDepartment",
            "StartDate", "EndDate",
            // ── Financial inputs (header-level). Percents are plain numbers, e.g. 12 for 12%. ──
            "Subtotal", "VatPercent", "DiscountPercent", "WhtPercent",
            // ── Line item fields ──
            "ItemName", "Description", "ModelNumber", "SerialNumber", "ItemType", "Category",
            "Vendor", "UnitOfMeasure", "UnitPrice", "Quantity", "LicenseNumber", "PartNumber",
            "WarrantyYears",
            "Remarks", "DatePurchased", "LineStartDate", "LineEndDate",
            // ── Sub-Type Group (per line) ──
            // SubType must be one of Contract / Subscription / License / Services (the
            // CK_SetItem_SubType values); leave blank for an untagged line. Lines sharing a
            // SubType + ReferenceCode within one invoice collapse into a single
            // dbo.SetItemSubTypeGroup row, which is what the printed reports group and subtotal by.
            // GroupBeginDate/GroupEndDate are the group's coverage period; blank falls back to
            // the line's own LineStartDate/LineEndDate.
            "SubType", "ReferenceCode", "GroupBeginDate", "GroupEndDate",
            // ── Parent Tag (per line) ──
            // Free-text label (e.g. "Cisco") — an independent, orthogonal grouping from
            // SubType with no financial semantics. Lines sharing a ParentTag within one
            // invoice collapse into a single dbo.SetItemParentTagGroup row.
            "ParentTag"
            // Note: Status is intentionally NOT a column — imported invoices are always saved
            // as dispatched ("YES"), per the agreed fast-pass behaviour.
        };

        /// <summary>Accepted SubType values, matching CK_SetItem_SubType.</summary>
        private static string[] ValidSubTypes => Repositories.ItemSubTypeCatalog.ValidSubTypes;

        /// <summary>
        /// Normalises a CSV SubType to its canonical casing, or null when blank.
        /// Returns false when the value is present but not one of the four allowed values.
        /// </summary>
        private static bool TryNormaliseSubType(string raw, out string normalised)
        {
            normalised = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;

            var match = ValidSubTypes.FirstOrDefault(
                v => string.Equals(v, raw.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == null) return false;

            normalised = match;
            return true;
        }

        // Rate applied when the VatPercent column is left blank. VAT is the standard 12% here,
        // so a blank means "standard 12% VAT"; the encoder types 0 explicitly for a VAT-exempt
        // invoice, or another number for a non-standard rate.
        private const decimal DefaultVatPercentWhenBlank = 12m;

        private List<InvoiceCsvRow> _rows;

        // Live map of active item categories, Name -> CategoryId, case-insensitive so "mouse",
        // "Mouse" and "MOUSE" all match. Reloaded on every preview/import so newly-added
        // categories are picked up automatically.
        private Dictionary<string, int> _categoryMap;

        public InvoiceCsvImportDialog()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parsed row model (one CSV line == one invoice line item)
        //
        // Internal rather than private because InvoiceBuilderDialog builds these rows on screen
        // instead of parsing them from a file, then hands them to RunImport — so the on-screen
        // builder and the CSV import save through one identical code path.
        // ─────────────────────────────────────────────────────────────────────
        internal sealed class InvoiceCsvRow
        {
            public int LineNumber { get; set; }   // 1-based CSV data line, for error messages

            // Invoice (Set) header fields — taken from the first row of each group.
            public string DocumentNumber { get; set; }
            public string ReferenceNumber { get; set; }
            public DateTime? DocumentDate { get; set; }
            public string Company { get; set; }       // sender/giver company
            public string Distributor { get; set; }   // sender/giver distributor
            public string SiteCompany { get; set; }     // recipient company (text only — no FK column)
            public string SiteBranch { get; set; }      // recipient branch  -> CurrentBranchId
            public string SiteDepartment { get; set; }  // recipient dept    -> CurrentDepartmentId
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }

            // Financial inputs (header-level). Subtotal is net-of-VAT; blank falls back to
            // the sum of the group's line amounts. Percents are plain numbers (12 == 12%).
            public bool HasSubtotal { get; set; }
            public decimal Subtotal { get; set; }
            public decimal VatPercent { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal WhtPercent { get; set; }

            // Line (Item) fields.
            public string ItemName { get; set; }
            public string Description { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public string ItemType { get; set; }
            public string Category { get; set; }
            public string Vendor { get; set; }
            public string UnitOfMeasure { get; set; }
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public string LicenseNumber { get; set; }
            public string PartNumber { get; set; }
            public string Remarks { get; set; }
            public DateTime? DatePurchased { get; set; }
            public DateTime? LineStartDate { get; set; }
            public DateTime? LineEndDate { get; set; }

            /// <summary>Matches Batch Add Items' "Warranty (Years)" field. 0 means no warranty —
            /// WarrantyStartDate is only set when this is greater than zero.</summary>
            public int WarrantyYears { get; set; }

            /// <summary>Manual override of the auto rule (Start = the moment the item is created,
            /// End = Start + WarrantyYears) — null means "use the auto rule", matching Edit Item's
            /// and Batch Add Items' checkbox-gated Warranty Start/End. Set by InvoiceBuilderDialog
            /// only; the CSV has no columns for these.</summary>
            public DateTime? WarrantyStartDate { get; set; }
            public DateTime? WarrantyEndDate { get; set; }

            /// <summary>FK to dbo.CartridgeModel, for Cartridge-category lines. The CSV has no
            /// column for this — it is set by InvoiceBuilderDialog, where the model is picked
            /// from a dropdown.</summary>
            public int? CartridgeModelId { get; set; }

            /// <summary>FK to dbo.ConsumableModel, for Ink / Toner / Print Head lines. Set by
            /// InvoiceBuilderDialog only, like CartridgeModelId.</summary>
            public int? ConsumableModelId { get; set; }

            // ── Sub-Type Group (per line) ──
            public string SubType { get; set; }
            public string ReferenceCode { get; set; }
            public DateTime? GroupBeginDate { get; set; }
            public DateTime? GroupEndDate { get; set; }

            // The group's OWN financials, independent of the invoice header's. Null means
            // "not set" — the group then falls back to the header percentages and to the sum
            // of its own line amounts, which is the pre-existing behaviour.
            public decimal? GroupVatPercent { get; set; }
            public decimal? GroupWhtPercent { get; set; }
            public decimal? GroupDiscountPercent { get; set; }
            public decimal? GroupSubtotalOverride { get; set; }

            /// <summary>Set by ParseRows when SubType is present but not one of the four allowed values.</summary>
            public bool SubTypeInvalid { get; set; }

            /// <summary>Shown in the preview grid so the grouping is visible before import.</summary>
            public string SubTypeGroupDisplay =>
                string.IsNullOrWhiteSpace(SubType)
                    ? "—"
                    : string.IsNullOrWhiteSpace(ReferenceCode) ? SubType : SubType + "  #  " + ReferenceCode;

            // ── Parent Tag (per line) — free-text, independent of Sub-Type Group above. ──
            public string ParentTagLabel { get; set; }

            /// <summary>Set only by InvoiceBuilderDialog's "Existing?" checkbox flow — when
            /// present, RunImport reuses this dbo.Item instead of creating a new one. CSV
            /// import itself never sets this; there is no CSV column for it.</summary>
            public int? ExistingItemId { get; set; }

            // Category validation state — set by ValidateCategories() against the live
            // ItemCategory list. CategoryId is the resolved FK (dbo.Item.CategoryId is NOT NULL).
            public bool CategoryValid { get; set; }
            public int CategoryId { get; set; }

            // Duplicate-detection state — set by ScanDuplicates().
            public bool DuplicateSerial { get; set; }    // SerialNumber already exists (DB or earlier in this file)
            public bool DuplicateInvoice { get; set; }   // an invoice Set with this Document # already exists
            public bool IsDuplicate => DuplicateSerial || DuplicateInvoice;
            public string DuplicateReason { get; set; }

            // ── Display helpers (bound by the preview grid) ──
            public string QuantityDisplay => Quantity.ToString();
            public string UnitPriceDisplay => UnitPrice.ToString("N2");
            public string AmountDisplay => (UnitPrice * Quantity).ToString("N2");
            public string CategoryDisplay => string.IsNullOrWhiteSpace(Category) ? "(missing)" : Category;
            public string ContractDisplay
            {
                get
                {
                    var start = LineStartDate ?? StartDate;
                    var end = LineEndDate ?? EndDate;
                    if (!start.HasValue && !end.HasValue) return "— (no renewal)";
                    return $"{(start.HasValue ? start.Value.ToString("MM/dd/yyyy") : "?")} → {(end.HasValue ? end.Value.ToString("MM/dd/yyyy") : "?")}";
                }
            }

            // Status priority: an unusable Category is the hardest blocker, then a duplicate.
            public string ActionStatus =>
                !CategoryValid ? "FIX CATEGORY" :
                IsDuplicate    ? "DUPLICATE"    : "CREATE";
            public Brush ActionBrush =>
                !CategoryValid ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)) :   // red
                IsDuplicate    ? new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00)) :   // orange
                                 new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));    // green
        }

        // ─────────────────────────────────────────────────────────────────────
        // Window chrome
        // ─────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        // ─────────────────────────────────────────────────────────────────────
        // Template download
        // ─────────────────────────────────────────────────────────────────────
        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.SaveFileDialog
            {
                Title = "Save Invoice Import Template",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "InvoiceImportTemplate.csv"
            };

            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            try
            {
                // Pull real Company / Distributor names (separate tables) so the sample rows
                // are actually importable and show the user exactly what values are valid.
                var repo = new ServiceSetRepository();
                var companies = SafeGetCompanies(repo);
                var distributors = SafeGetDistributors(repo);

                string company1 = companies.ElementAtOrDefault(0)?.CompanyName
                    ?? "Company Name (must match a row in the Company table)";
                string company2 = companies.ElementAtOrDefault(1)?.CompanyName ?? company1;
                string distributor1 = distributors.ElementAtOrDefault(0)?.Name
                    ?? "Distributor Name (must match a row in the Distributor table)";
                string distributor2 = distributors.ElementAtOrDefault(1)?.Name ?? distributor1;

                // Recipient parts for the sample Site. Branch/Department resolve to FK ids, so
                // pull real names when available; the recipient company is free text.
                string siteCompany = company2;
                string siteBranch = GetFirstName("SELECT TOP 1 Name FROM dbo.Branch WHERE Active = 1 ORDER BY Name")
                    ?? "Branch Name (optional — must match the Branch table)";
                string siteDept = GetFirstName("SELECT TOP 1 Name FROM dbo.Department WHERE Active = 1 ORDER BY Name")
                    ?? "Department Name (optional — must match the Department table)";

                // Category is required (dbo.Item.CategoryId is NOT NULL) and must match an active
                // item category. Seed the sample with real ones so it imports without a fix step.
                var categoryNames = LoadActiveCategories().Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
                string category1 = categoryNames.ElementAtOrDefault(0) ?? "Category (must match an item category)";
                string category2 = categoryNames.ElementAtOrDefault(1) ?? category1;

                // Sample rows. Each string[] follows TemplateColumns order exactly (35 fields).
                // The first two rows share one Document # -> one invoice with two line items;
                // the third is a separate invoice. Header + financial fields are repeated on
                // every row of a group (the importer reads them from the first row). Dates are
                // MM/dd/yyyy; VAT/WHT/Discount are plain percents (12 == 12%).
                var sampleRows = new List<string[]>
                {
                    // Subtotal left blank on purpose — the importer sums the line amounts
                    // (Qty × UnitPrice) into it, exactly like adding up the paper invoice.
                    // Start/End are set-level (same on every row of an invoice). LineStart/LineEnd
                    // are per-item — INV-2026-001's two lines below use DIFFERENT line periods to
                    // show they can differ; leave them blank to inherit the set-level Start/End.
                    // All dates are MM/dd/yyyy.
                    //
                    // SubType/ReferenceCode drive the Sub-Type Group banner and subtotal on the
                    // printed Invoice and Renewals reports. SubType must be one of
                    // Contract / Subscription / License / Services; leave it blank for an untagged
                    // line. Lines with the same SubType AND ReferenceCode inside one invoice land
                    // in the same group — INV-2026-001's two lines below are deliberately in two
                    // different groups so the sample prints two banners. GroupBegin/GroupEnd are
                    // the banner's BEGIN/END DATE; blank inherits the line's own LineStart/LineEnd.
                    //       Doc#            Ref#       DocDate       Company    Distributor   SiteCompany  SiteBranch  SiteDept   Start         End           Subt Vat% Disc% Wht% ItemName                        Description                    Model     Serial ItemType            Category    Vendor UOM         Price    Qty  License#       Part# WarYrs Remarks DatePurchased  LineStart     LineEnd       SubType         RefCode          GroupBegin    GroupEnd
                    new[] { "INV-2026-001", "PO-8891", "07/26/2026", company1,   distributor1, siteCompany, siteBranch, siteDept, "07/26/2026", "07/25/2027", "", "12", "0", "0", "Antivirus License (50 seats)", "Annual endpoint protection",  "AV-50",  "",    "Software/License", category1, "",    "Annually", "15000",  "1", "LIC-AV-001",  "",   "1",   "",     "07/20/2026",  "07/26/2026", "07/25/2027", "License",      "LIC-AV-001",    "07/26/2026", "07/25/2027" },
                    new[] { "INV-2026-001", "PO-8891", "07/26/2026", company1,   distributor1, siteCompany, siteBranch, siteDept, "07/26/2026", "07/25/2027", "", "12", "0", "0", "Firewall Support",             "Yearly support contract",     "FW-SUP", "",    "Services",         category2, "",    "Contract", "8000",   "1", "",            "",   "0",   "",     "07/20/2026",  "08/01/2026", "07/31/2027", "Contract",     "SMARTNET-100316593", "08/01/2026", "07/31/2027" },
                    new[] { "INV-2026-002", "PO-8892", "07/26/2026", company2,   distributor2, "",          "",         "",       "07/26/2026", "07/25/2028", "", "12", "0", "2", "ERP Subscription",             "Two-year cloud subscription", "ERP-CLD","",    "Software/License", category1, "",    "Annually", "120000", "1", "LIC-ERP-77",  "",   "2",   "",     "07/22/2026",  "",           "",           "Subscription", "SUB-ERP-77",    "",           "" }
                };

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(string.Join(",", TemplateColumns.Select(CsvEscape)));
                foreach (var row in sampleRows)
                    sb.AppendLine(string.Join(",", row.Select(CsvEscape)));

                File.WriteAllText(dlg.FileName, sb.ToString(), new System.Text.UTF8Encoding(false));

                bool hadCompany = companies.Count > 0;
                bool hadDistributor = distributors.Count > 0;

                MessageBox.Show(
                    "Template saved with sample data.\n\n" +
                    "• One row = one invoice line item. Rows sharing a Document # become one Sales Invoice Set.\n" +
                    "• Header + financial fields (dates, Company, Distributor, Subtotal, VAT%, Discount%, WHT%) are read from the FIRST row of each Document # group.\n" +
                    "• Company + Distributor = the sender (giver). SiteCompany + SiteBranch + SiteDepartment = the recipient, combined into the invoice's Site text (all optional). Branch/Department must match their tables to link; the recipient company is free text.\n" +
                    (hadCompany ? string.Empty : "  (No companies in the Company table — replace the Company placeholder before importing.)\n") +
                    (hadDistributor ? string.Empty : "  (No distributors in the Distributor table — replace the Distributor placeholder before importing.)\n") +
                    "• Put each item's real price in UnitPrice (no need to zero it). The importer adds the line amounts into the Subtotal — like the paper invoice — then computes VAT/WHT/Discount/Total Amount Due. No Excel needed.\n" +
                    "• Only fill the Subtotal column to OVERRIDE that sum (e.g. the printed subtotal differs).\n" +
                    "• VAT%: leave BLANK for the standard 12%, or type 0 for a VAT-exempt invoice. WHT% and Discount% default to 0 when blank. All are plain numbers (12 = 12%, a trailing % is OK).\n" +
                    "• Category is REQUIRED and must match one of your item categories (not case-sensitive; new categories are picked up automatically). If a value doesn't match, you'll get a popup on import to map it to a valid category — no dead-end error.\n" +
                    "• Status is not a column — every imported invoice is saved as dispatched (\"YES\").\n" +
                    "• All dates use MM/dd/yyyy (e.g. 07/26/2026).\n" +
                    "• Start Date / End Date are SET-level (the whole invoice's contract period — keep them the same on every row of an invoice). Leave both blank to skip renewal seeding.\n" +
                    "• LineStartDate / LineEndDate are PER-ITEM and may differ per line; leave them blank to inherit the set-level Start/End.\n" +
                    "• DatePurchased is per-item and optional (MM/dd/yyyy).",
                    "Template Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save template: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // File picker
        // ─────────────────────────────────────────────────────────────────────
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.OpenFileDialog
            {
                Title = "Select Invoice CSV File",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtCsvPath.Text = dlg.FileName;
                BtnLoadPreview.IsEnabled = true;
                BtnImport.IsEnabled = false;
                PreviewGrid.Visibility = Visibility.Collapsed;
                LblEmptyState.Visibility = Visibility.Visible;
                BannerWarn.Visibility = Visibility.Collapsed;
                LblStatus.Text = string.Empty;
                _rows = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Load preview
        // ─────────────────────────────────────────────────────────────────────
        private void BtnLoadPreview_Click(object sender, RoutedEventArgs e)
        {
            var path = TxtCsvPath.Text;
            if (!File.Exists(path))
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var warnings = new List<string>();
                var rows = ParseCsv(path, warnings);

                if (rows.Count == 0)
                {
                    LblStatus.Text = "No valid rows found.";
                    BannerWarn.Visibility = warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    LblWarnNotice.Text = string.Join("  •  ", warnings);
                    return;
                }

                _rows = rows;
                PreviewGrid.ItemsSource = rows;
                PreviewGrid.Visibility = Visibility.Visible;
                LblEmptyState.Visibility = Visibility.Collapsed;

                // Validate each row's Category against the live list (case-insensitive) so bad
                // ones show as "FIX CATEGORY" in red — they're fixed interactively on import.
                ValidateCategories();
                int invalidCategories = _rows.Count(r => !r.CategoryValid);
                if (invalidCategories > 0)
                    warnings.Insert(0, $"{invalidCategories} row(s) have a missing/unknown Category — you'll be asked to pick a valid one when you click Import All.");

                // SubType is constrained in the DB (CK_SetItem_SubType). A bad value isn't a
                // blocker — the line still imports, just untagged — so warn rather than stop.
                int badSubTypes = _rows.Count(r => r.SubTypeInvalid);
                if (badSubTypes > 0)
                    warnings.Insert(0, $"{badSubTypes} row(s) have an unrecognised SubType — it must be Contract, Subscription, License or Services. Those lines will import without a Sub-Type Group.");

                // Flag rows that already exist (serial or Document #) so re-imports don't duplicate.
                ScanDuplicates();
                int dupInvoices = _rows.Where(r => r.DuplicateInvoice)
                    .Select(r => r.DocumentNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int dupSerials = _rows.Count(r => r.DuplicateSerial);
                if (dupInvoices > 0 || dupSerials > 0)
                    warnings.Insert(0, $"Possible duplicates: {dupInvoices} invoice(s) with an existing Document # and {dupSerials} row(s) with an existing serial — you can skip them on import.");

                int invoiceCount = rows
                    .Select(r => r.DocumentNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                LblStatus.Text = $"{rows.Count} line item(s) across {invoiceCount} invoice(s) — all Items will be created.";

                if (warnings.Count > 0)
                {
                    LblWarnNotice.Text = string.Join("  •  ", warnings);
                    BannerWarn.Visibility = Visibility.Visible;
                }
                else
                {
                    BannerWarn.Visibility = Visibility.Collapsed;
                }

                BtnImport.IsEnabled = true;
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to read the CSV:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CSV parsing (header-mapped so column order is flexible)
        // ─────────────────────────────────────────────────────────────────────
        private static List<InvoiceCsvRow> ParseCsv(string path, List<string> warnings)
        {
            var result = new List<InvoiceCsvRow>();
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return result;

            // Map header names -> column index.
            var header = SplitCsvLine(lines[0]);
            var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Count; i++)
            {
                var key = Canonical(header[i]);
                if (!string.IsNullOrEmpty(key) && !colIndex.ContainsKey(key))
                    colIndex[key] = i;
            }

            if (!colIndex.ContainsKey("DocumentNumber"))
                throw new InvalidOperationException("The CSV must have a 'DocumentNumber' column. Use 'Download Template' for the expected layout.");
            if (!colIndex.ContainsKey("ItemName"))
                throw new InvalidOperationException("The CSV must have an 'ItemName' column. Use 'Download Template' for the expected layout.");

            string Get(List<string> cols, string name)
            {
                if (!colIndex.TryGetValue(name, out int idx)) return null;
                if (idx >= cols.Count) return null;
                var v = cols[idx]?.Trim();
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = SplitCsvLine(lines[i]);

                var doc = Get(cols, "DocumentNumber");
                var itemName = Get(cols, "ItemName");

                if (string.IsNullOrWhiteSpace(doc))
                {
                    warnings.Add($"Line {i + 1}: missing Document # (skipped)");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    warnings.Add($"Line {i + 1}: missing Item Name (skipped)");
                    continue;
                }

                // Subtotal: a bad value falls back to auto-summing the lines (never a silent 0).
                var subtotalRaw = Get(cols, "Subtotal");
                bool hasSubtotal = !string.IsNullOrWhiteSpace(subtotalRaw);
                decimal subtotalVal = 0m;
                if (hasSubtotal && !TryParseMoney(subtotalRaw, out subtotalVal))
                {
                    warnings.Add($"Line {i + 1}: Subtotal '{subtotalRaw}' isn't a number — the line amounts will be summed instead.");
                    hasSubtotal = false;
                }

                // Percentages: a non-numeric value is flagged (not silently treated as 0), and
                // anything outside 0–100 is flagged as a likely typo. Blank uses the default.
                decimal vatPct  = ParsePercent(Get(cols, "VatPercent"),      "VAT%",      i + 1, warnings, DefaultVatPercentWhenBlank);
                decimal discPct = ParsePercent(Get(cols, "DiscountPercent"), "Discount%", i + 1, warnings, 0m);
                decimal whtPct  = ParsePercent(Get(cols, "WhtPercent"),      "WHT%",      i + 1, warnings, 0m);

                var row = new InvoiceCsvRow
                {
                    LineNumber = i + 1,
                    DocumentNumber = doc,
                    ReferenceNumber = Get(cols, "ReferenceNumber"),
                    DocumentDate = ParseDate(Get(cols, "DocumentDate")),
                    Company = Get(cols, "Company"),
                    Distributor = Get(cols, "Distributor"),
                    SiteCompany = Get(cols, "SiteCompany"),
                    SiteBranch = Get(cols, "SiteBranch"),
                    SiteDepartment = Get(cols, "SiteDepartment"),
                    StartDate = ParseDate(Get(cols, "StartDate")),
                    EndDate = ParseDate(Get(cols, "EndDate")),
                    HasSubtotal = hasSubtotal,
                    Subtotal = subtotalVal,
                    VatPercent = vatPct,
                    DiscountPercent = discPct,
                    WhtPercent = whtPct,
                    ItemName = itemName,
                    Description = Get(cols, "Description"),
                    ModelNumber = Get(cols, "ModelNumber"),
                    SerialNumber = Get(cols, "SerialNumber"),
                    ItemType = NormalizeItemType(Get(cols, "ItemType")),
                    Category = Get(cols, "Category"),
                    Vendor = Get(cols, "Vendor"),
                    UnitOfMeasure = Get(cols, "UnitOfMeasure"),
                    UnitPrice = ParseDecimal(Get(cols, "UnitPrice")),
                    Quantity = Math.Max(1, ParseInt(Get(cols, "Quantity"), 1)),
                    LicenseNumber = Get(cols, "LicenseNumber"),
                    PartNumber = Get(cols, "PartNumber"),
                    WarrantyYears = Math.Max(0, ParseInt(Get(cols, "WarrantyYears"), 0)),
                    Remarks = Get(cols, "Remarks"),
                    DatePurchased = ParseDate(Get(cols, "DatePurchased")),
                    LineStartDate = ParseDate(Get(cols, "LineStartDate")),
                    LineEndDate = ParseDate(Get(cols, "LineEndDate")),
                    ReferenceCode = Get(cols, "ReferenceCode"),
                    GroupBeginDate = ParseDate(Get(cols, "GroupBeginDate")),
                    GroupEndDate = ParseDate(Get(cols, "GroupEndDate")),
                    ParentTagLabel = Get(cols, "ParentTag")
                };

                // SubType has a DB check constraint (CK_SetItem_SubType), so reject anything
                // outside the four allowed values here rather than failing at save time.
                string subType;
                if (TryNormaliseSubType(Get(cols, "SubType"), out subType))
                {
                    row.SubType = subType;
                }
                else
                {
                    row.SubTypeInvalid = true;
                }

                result.Add(row);
            }

            return result;
        }

        // Accepts "Software"/"License"/"Software/License" -> "Software/License";
        // "Service"/"Services" -> "Services"; "Hardware" -> "Hardware". Blank/unknown
        // defaults to Software/License so the set still has a software/service line to
        // anchor renewals on.
        private static string NormalizeItemType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Software/License";
            var v = raw.Replace(" ", "").ToLowerInvariant();
            if (v.Contains("service")) return "Services";
            if (v.Contains("hardware")) return "Hardware";
            return "Software/License";
        }

        private static string Canonical(string s)
            => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Replace(" ", "").Replace("_", "").Trim();

        // Quotes a field only when it contains a comma, quote, or newline (RFC-4180 style),
        // doubling any embedded quotes. Keeps clean values unquoted so the template stays readable.
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool needsQuoting = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuoting) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    // Doubled quote inside a quoted field == literal quote.
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            string[] formats = { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "dd/MM/yyyy" };
            if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var exact))
                return exact;
            if (DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
                return loose;
            return null;
        }

        private static decimal ParseDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            var cleaned = s.Replace(",", string.Empty).Replace("₱", string.Empty).Replace("$", string.Empty).Trim();
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }

        private static int ParseInt(string s, int fallback)
            => int.TryParse((s ?? string.Empty).Trim(), out var i) ? i : fallback;

        // Parses a money value, tolerating currency symbols and thousands separators.
        // Returns false (rather than a silent 0) when the non-empty text isn't a number.
        private static bool TryParseMoney(string s, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var cleaned = s.Replace(",", string.Empty).Replace("₱", string.Empty)
                           .Replace("$", string.Empty).Replace("%", string.Empty).Trim();
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        // Parses a percentage rate (12 == 12%). Tolerates a trailing "%". A blank uses
        // defaultWhenBlank; a non-numeric value is warned about and treated as 0; a value
        // outside 0–100 is warned about (likely a typo) but still used so the confirmation
        // preview surfaces the resulting total.
        private static decimal ParsePercent(string raw, string label, int lineNumber,
            List<string> warnings, decimal defaultWhenBlank)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultWhenBlank;

            var cleaned = raw.Replace("%", string.Empty).Replace(",", string.Empty).Trim();
            if (!decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct))
            {
                warnings.Add($"Line {lineNumber}: {label} '{raw}' isn't a number — treated as 0%.");
                return 0m;
            }
            if (pct < 0m || pct > 100m)
                warnings.Add($"Line {lineNumber}: {label} = {pct} is outside 0–100 — check for a typo (the total below reflects it).");
            return pct;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Import
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_rows == null || _rows.Count == 0) return;

            // Sub-Type is optional and applies to every invoice created from this CSV batch
            // (a plain Hardware batch has none). Read it on the UI thread now — RunImport()
            // runs via Task.Run.

            // Categories are required (dbo.Item.CategoryId is NOT NULL). If any row's Category
            // is missing/unknown, open the correction popup instead of failing mid-insert.
            if (!EnsureCategoriesValid()) return;

            var groups = _rows
                .GroupBy(r => r.DocumentNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Duplicate guard: refresh against the current DB, then skip whole invoices that
            // already exist (by Document #) or contain an already-existing serial. Skipping at
            // invoice granularity avoids creating a half-invoice.
            ScanDuplicates();
            var dupGroups = groups.Where(g => g.Any(r => r.IsDuplicate)).ToList();
            if (dupGroups.Count > 0)
            {
                var dupList = new System.Text.StringBuilder();
                foreach (var g in dupGroups.Take(15))
                {
                    var reason = g.Select(r => r.DuplicateReason).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                    dupList.AppendLine($"   {g.Key} — {reason}");
                }
                if (dupGroups.Count > 15) dupList.AppendLine($"   … and {dupGroups.Count - 15} more");

                var remaining = groups.Count - dupGroups.Count;
                var dupResp = MessageBox.Show(
                    $"{dupGroups.Count} invoice(s) look like duplicates and will be SKIPPED:\n\n" +
                    dupList +
                    $"\n{(remaining > 0 ? $"Skip them and import the remaining {remaining} invoice(s)?" : "There is nothing left to import once these are skipped.")}",
                    "Duplicates Found",
                    remaining > 0 ? MessageBoxButton.YesNo : MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (remaining == 0 || dupResp != MessageBoxResult.Yes) return;

                groups = groups.Where(g => !g.Any(r => r.IsDuplicate)).ToList();
            }

            // Show the computed Subtotal + Total Amount Due for each invoice so the figures
            // can be verified before anything is written.
            var summaries = SummarizeInvoices(groups);
            var breakdown = new System.Text.StringBuilder();
            foreach (var s in summaries.Take(15))
                breakdown.AppendLine($"   {s.DocumentNumber}: {s.LineCount} line(s), Subtotal {s.Subtotal:N2} → Total Due {s.TotalDue:N2}");
            if (summaries.Count > 15)
                breakdown.AppendLine($"   … and {summaries.Count - 15} more invoice(s)");
            decimal grandTotal = summaries.Sum(s => s.TotalDue);

            int lineCount = groups.Sum(g => g.Count());
            var confirm = MessageBox.Show(
                $"Import {lineCount} line item(s) into {groups.Count} Sales Invoice Set(s)?\n\n" +
                $"• {lineCount} new Item record(s) will be created\n" +
                $"• {groups.Count} invoice set(s) will be created (saved as dispatched)\n" +
                "• Subtotal is summed from the line amounts (or your Subtotal override); VAT / WHT / Discount / Total Amount Due are computed\n" +
                "• Renewals will be seeded for lines that have a contract period\n\n" +
                "Computed totals per invoice:\n" +
                breakdown +
                $"\nGrand Total Amount Due: {grandTotal:N2}\n\n" +
                "Continue?",
                "Confirm Invoice Import", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnImport.IsEnabled = false;
            BtnLoadPreview.IsEnabled = false;
            LblStatus.Text = "Importing…";

            try
            {
                var (invoicesCreated, itemsCreated, errors, _) =
                    await Task.Run(() => RunImport(groups));

                if (errors.Count > 0)
                {
                    MessageBox.Show(
                        "Import completed with errors.\n\n" +
                        $"Invoices created: {invoicesCreated}   Items created: {itemsCreated}\n\n" +
                        string.Join("\n", errors.Take(12)),
                        "Import Results", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Import complete!\n\n{invoicesCreated} invoice set(s) and {itemsCreated} item(s) created.",
                        "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = invoicesCreated > 0;
                Close();
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"Import failed: {ex.Message}";
                BtnImport.IsEnabled = true;
                BtnLoadPreview.IsEnabled = true;
                MessageBox.Show($"Import failed:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Creates every invoice in <paramref name="groups"/> (one group per Document #), each in
        /// its own transaction: the Items, the Set, its SetItems/Renewals/Sub-Type groups and the
        /// financial totals all commit together or not at all.
        ///
        /// Static and free of any dialog state, so InvoiceBuilderDialog can call it with rows it
        /// built on screen rather than parsed from a CSV.
        /// </summary>
        internal static (int invoicesCreated, int itemsCreated, List<string> errors, Dictionary<string, int> createdSetIds) RunImport(
            List<IGrouping<string, InvoiceCsvRow>> groups)
        {
            var itemRepo = new ItemRepository();
            var setRepo = new ServiceSetRepository();
            var errors = new List<string>();
            var createdSetIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int invoicesCreated = 0, itemsCreated = 0;

            int conditionId = GetDefaultConditionId();
            var companies = SafeGetCompanies(setRepo);
            var distributors = SafeGetDistributors(setRepo);
            var vendorCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
            var branchCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
            var departmentCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var lines = group.ToList();
                var header = lines[0];

                var setItems = new List<ServiceSetItemDto>();
                int createdInThisGroup = 0;
                // Items created in this group — logged AFTER the transaction commits.
                var createdItems = new List<(int ItemId, string Name, string Serial, DateTime DateCreated)>();

                // Whole-invoice atomicity: every Item, the Set, its SetItems/Renewals and the
                // financial update run in ONE transaction. Any failure rolls the entire invoice
                // back, so a failed import never leaves orphan items behind.
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            foreach (var line in lines)
                            {
                        // CategoryId was validated (and fixed if needed) before import, so it's
                        // always a real, non-null category here.
                        if (line.CategoryId <= 0)
                            throw new InvalidOperationException($"Line {line.LineNumber}: Category '{line.Category}' is not a valid item category.");
                        int categoryId = line.CategoryId;
                        int? vendorId = ResolveVendorId(line.Vendor, vendorCache);
                        var lineStart = line.LineStartDate ?? header.StartDate;
                        var lineEnd = line.LineEndDate ?? header.EndDate;
                        bool isHardware = string.Equals(line.ItemType, "Hardware", StringComparison.OrdinalIgnoreCase);

                        // Same rule Batch Add Items and Edit Item use: a manually entered Warranty
                        // Start/End wins outright; otherwise Start defaults to the moment the item
                        // is created (not Date Purchased — a warranty against defects starts when
                        // the item enters inventory, not when it was bought), and
                        // ItemRepository.AddItem computes WarrantyEndDate = Start + Years itself
                        // when no explicit End is supplied.
                        var dateCreated = DateTime.Now;
                        DateTime? warrantyStart = line.WarrantyStartDate
                            ?? (line.WarrantyYears > 0 ? dateCreated : (DateTime?)null);

                        // "Existing?" checkbox lines (InvoiceBuilderDialog only — CSV import
                        // never sets this) reuse an already-created dbo.Item outright: no new
                        // Item row, no audit-trail "Item Created" entry, since nothing was
                        // actually created.
                        int newItemId;
                        if (line.ExistingItemId.HasValue)
                        {
                            newItemId = line.ExistingItemId.Value;
                        }
                        else
                        {
                            var itemDto = new ItemDto
                            {
                                Name = line.ItemName,
                                Description = line.Description,
                                ModelNumber = line.ModelNumber,
                                CategoryId = categoryId,
                                Category = line.Category,
                                SerialNumber = line.SerialNumber,
                                UnitOfMeasure = DefaultUom(line),
                                StockOnHand = line.Quantity,
                                Amount = line.UnitPrice,
                                ItemType = line.ItemType,
                                StartDate = lineStart,
                                EndDate = lineEnd,
                                ConditionId = conditionId,
                                ConditionName = "Good",
                                VendorId = vendorId,
                                Remarks = line.Remarks,
                                WarrantyYears = line.WarrantyYears,
                                WarrantyStartDate = warrantyStart,
                                WarrantyEndDate = line.WarrantyEndDate,
                                DatePurchased = line.DatePurchased,
                                LicenseNumber = line.LicenseNumber,
                                PartNumber = line.PartNumber,
                                // Software/License & Services are non-inventory; hardware invoice
                                // lines do affect stock, matching the manual Add Item behaviour.
                                AffectsInventory = isHardware,
                                AcquisitionType = "Invoice",
                                IsTrackedAsset = true,
                                RefillStatus = null,
                                // Model FKs follow the item's category: Cartridge lines carry a
                                // CartridgeModelId, Ink / Toner / Print Head a ConsumableModelId.
                                CartridgeModelId = line.CartridgeModelId,
                                ConsumableModelId = line.ConsumableModelId,
                                DateCreated = dateCreated,
                                CreatedByUserId = AppSession.CurrentUserId,
                                CreatedByName = AppSession.CurrentUserName,
                                Active = true
                            };

                            newItemId = itemRepo.AddItem(itemDto, con, tx);
                            if (newItemId <= 0)
                                throw new InvalidOperationException($"Item '{line.ItemName}' (line {line.LineNumber}) could not be created.");

                            createdInThisGroup++;
                            createdItems.Add((newItemId, itemDto.Name, itemDto.SerialNumber, itemDto.DateCreated));
                        }

                        setItems.Add(new ServiceSetItemDto
                        {
                            ItemId = newItemId,
                            ItemCode = string.IsNullOrWhiteSpace(line.ModelNumber) ? newItemId.ToString() : line.ModelNumber,
                            Description = string.IsNullOrWhiteSpace(line.Description) ? line.ItemName : line.Description,
                            Quantity = line.Quantity,
                            UnitOfMeasure = DefaultUom(line),
                            UnitPrice = line.UnitPrice,
                            Amount = line.UnitPrice * line.Quantity,
                            LineStartDate = lineStart,
                            LineEndDate = lineEnd,
                            // Sub-Type group. ServiceSetRepository.FindOrCreateSubTypeGroup collapses
                            // lines sharing SubType + ReferenceCode into one dbo.SetItemSubTypeGroup
                            // row, which is what the Invoice/Renewals reports group and subtotal by.
                            // A blank group period falls back to the line's own coverage dates.
                            SubType = line.SubType,
                            ReferenceCode = line.ReferenceCode,
                            BeginDate = line.GroupBeginDate ?? lineStart,
                            EndDate = line.GroupEndDate ?? lineEnd,
                            // Each group is priced independently of the invoice header.
                            GroupVatPercent = line.GroupVatPercent,
                            GroupWhtPercent = line.GroupWhtPercent,
                            GroupDiscountPercent = line.GroupDiscountPercent,
                            GroupSubtotalOverride = line.GroupSubtotalOverride,
                            // Independent, orthogonal grouping — no financial semantics of its own.
                            ParentTagLabel = line.ParentTagLabel
                        });
                    }

                    var setDto = new ServiceSetDto
                    {
                        DocumentNumber = header.DocumentNumber,
                        ReferenceNumber = header.ReferenceNumber,
                        DocumentDate = header.DocumentDate ?? DateTime.Today,
                        StartDate = header.StartDate,
                        EndDate = header.EndDate,
                        Notes = string.Empty,
                        // Imported invoices are always dispatched, per the fast-pass agreement.
                        Status = "YES",
                        ComId = MatchCompanyId(companies, header.Company),
                        DistributorId = MatchDistributorId(distributors, header.Distributor),
                        CreatedByUserId = AppSession.CurrentUserId,
                        DateCreated = DateTime.Now,
                        Items = setItems
                    };

                    int newSetId = setRepo.CreateSoftwareServiceSet(setDto, con, tx);
                    if (newSetId <= 0)
                        throw new InvalidOperationException("The invoice set was not created (repository returned no id).");

                    // Financial details: run the same formula ViewInvoiceDetailPage uses, then
                    // persist the computed amounts onto the Set so the invoice opens fully priced.
                    decimal subtotal = GroupSubtotal(header, lines);

                    ComputeFinancials(subtotal, header.VatPercent, header.DiscountPercent, header.WhtPercent,
                        out decimal vatAmount, out decimal discountAmount, out decimal whtAmount, out decimal totalDue);

                    // Site (recipient) = "Company - Branch - Department" from whichever parts are
                    // supplied. Branch/Department also resolve to their FK ids; the recipient
                    // company is text-only (the Set table has no SiteCompanyId column).
                    int? branchId = ResolveBranchId(header.SiteBranch, branchCache);
                    int? departmentId = ResolveDepartmentId(header.SiteDepartment, departmentCache);
                    string site = ComposeSite(header.SiteCompany, header.SiteBranch, header.SiteDepartment);

                    UpdateSetInvoiceExtras(con, tx, newSetId, subtotal, vatAmount, whtAmount, discountAmount, totalDue,
                        site, branchId, departmentId);

                            tx.Commit();

                            // Post-commit logging (best-effort; never rolls the invoice back).
                            LogImportedInvoice(newSetId, header.DocumentNumber, createdItems);

                            // Inventory System "Activity" feed — one "Invoice created" row for the
                            // whole import (items were created on the transactional AddItem overload,
                            // which isn't hooked, so there are no per-item rows to suppress).
                            try
                            {
                                var activityItems = createdItems
                                    .Select(ci => new Yakult.Inventory.App.Services.InventoryActivityNotifier.ActivityItemDetail
                                    {
                                        id     = ci.Item1,
                                        name   = string.IsNullOrWhiteSpace(ci.Item2) ? $"Item #{ci.Item1}" : ci.Item2,
                                        type   = null,
                                        serial = string.IsNullOrWhiteSpace(ci.Item3) ? null : ci.Item3,
                                    })
                                    .ToList();
                                Yakult.Inventory.App.Services.InventoryActivityNotifier.NotifySetCreated(
                                    newSetId, setItems.Count, AppSession.CurrentUserId, "Invoice", activityItems);
                            }
                            catch { /* activity notification must never affect the import result */ }

                            // Backfill dbo.InvoicePreparation/InvoicePreparationItem for every
                            // Sub-Type Group this invoice just created, so they show up on the
                            // Invoice Sub Groups page's Completed tab like every other invoiced
                            // group does — this fast-build path bypasses that staging table
                            // entirely otherwise. Best-effort, same reasoning as the logging above.
                            try { new InvoicePreparationRepository().RegisterCompletedGroupsFromSet(newSetId, AppSession.CurrentUserId); }
                            catch { /* audit-visibility backfill must never affect the import result */ }

                            createdSetIds[header.DocumentNumber] = newSetId;
                            invoicesCreated++;
                            itemsCreated += createdInThisGroup;
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); } catch { }
                            // The whole invoice rolled back — nothing was saved, so no orphans.
                            errors.Add($"Invoice '{group.Key}': {ex.Message} (nothing was saved for this invoice)");
                        }
                    }
                }
            }

            return (invoicesCreated, itemsCreated, errors, createdSetIds);
        }

        // Records the same activity + item-audit-trail entries the normal Add-Item flow writes
        // (skipped by the transaction-participating AddItem overload), plus one per-invoice entry.
        // Runs after commit and is fully best-effort — a logging failure never affects the import.
        private static void LogImportedInvoice(
            int setId, string documentNumber,
            List<(int ItemId, string Name, string Serial, DateTime DateCreated)> items)
        {
            try
            {
                var auditRepo = new ItemAuditTrailRepository();
                foreach (var it in items)
                {
                    ActivityLogger.Log(ActivityLogger.Actions.Create, "Item", it.ItemId,
                        $"Item '{it.Name}' created via invoice CSV import");

                    try
                    {
                        auditRepo.LogActionAsync(new ItemAuditTrailDto
                        {
                            ItemId        = it.ItemId,
                            SerialNumber  = it.Serial,
                            Action        = "Item Created/Added",
                            ActionTime    = it.DateCreated,
                            Direction     = "IN",
                            Status        = "Completed",
                            ReferenceType = "Item",
                            ReferenceId   = it.ItemId,
                            Notes         = "Item added via invoice CSV import",
                            CreatedBy     = AppSession.CurrentUserName ?? "System"
                        }).GetAwaiter().GetResult();
                    }
                    catch { /* per-item audit is best-effort */ }
                }

                ActivityLogger.Log(ActivityLogger.Actions.Import, "Invoice", setId,
                    $"Invoice '{documentNumber}' imported from CSV ({items.Count} item(s))");
            }
            catch { /* logging must never affect the import result */ }
        }

        // The invoice Subtotal (net of VAT): the header override if the Subtotal column was
        // filled, otherwise the sum of the group's line amounts (Qty × UnitPrice) — the paper
        // behaviour where adding up the item amounts gives the invoice subtotal.
        private static decimal GroupSubtotal(InvoiceCsvRow header, IEnumerable<InvoiceCsvRow> lines)
            => header.HasSubtotal ? header.Subtotal : lines.Sum(l => l.UnitPrice * l.Quantity);

        private sealed class InvoiceSummary
        {
            public string DocumentNumber { get; set; }
            public int LineCount { get; set; }
            public decimal Subtotal { get; set; }
            public decimal TotalDue { get; set; }
        }

        // Computes the per-invoice Subtotal and Total Amount Due for the confirmation preview,
        // using the exact figures the import will persist.
        private static List<InvoiceSummary> SummarizeInvoices(List<IGrouping<string, InvoiceCsvRow>> groups)
        {
            var list = new List<InvoiceSummary>();
            foreach (var group in groups)
            {
                var lines = group.ToList();
                var header = lines[0];
                decimal subtotal = GroupSubtotal(header, lines);
                ComputeFinancials(subtotal, header.VatPercent, header.DiscountPercent, header.WhtPercent,
                    out _, out _, out _, out decimal totalDue);
                list.Add(new InvoiceSummary
                {
                    DocumentNumber = group.Key,
                    LineCount = lines.Count,
                    Subtotal = subtotal,
                    TotalDue = totalDue
                });
            }
            return list;
        }

        private static string DefaultUom(InvoiceCsvRow line)
        {
            if (!string.IsNullOrWhiteSpace(line.UnitOfMeasure)) return line.UnitOfMeasure;
            if (string.Equals(line.ItemType, "Services", StringComparison.OrdinalIgnoreCase)) return "Contract";
            if (string.Equals(line.ItemType, "Hardware", StringComparison.OrdinalIgnoreCase)) return "Unit";
            return "One Time";
        }

        private static List<Models.CompanyDto> SafeGetCompanies(ServiceSetRepository repo)
        {
            try { return repo.GetAllCompanies()?.ToList() ?? new List<Models.CompanyDto>(); }
            catch { return new List<Models.CompanyDto>(); }
        }

        private static List<Models.DistributorDto> SafeGetDistributors(ServiceSetRepository repo)
        {
            try { return repo.GetAllDistributors()?.ToList() ?? new List<Models.DistributorDto>(); }
            catch { return new List<Models.DistributorDto>(); }
        }

        private static int? MatchCompanyId(List<Models.CompanyDto> companies, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var match = companies.FirstOrDefault(c =>
                string.Equals(c.CompanyName?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            return match != null && match.ComId > 0 ? (int?)match.ComId : null;
        }

        private static int? MatchDistributorId(List<Models.DistributorDto> distributors, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var match = distributors.FirstOrDefault(d =>
                string.Equals(d.Name?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            return match != null && match.DistributorId > 0 ? (int?)match.DistributorId : null;
        }

        // Loads active item categories into a case-insensitive Name -> CategoryId map. Queried
        // live, so a category added in the app shows up on the next preview/import automatically.
        private static Dictionary<string, int> LoadActiveCategories()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            if (r.IsDBNull(1)) continue;
                            var name = r.GetString(1).Trim();
                            if (name.Length > 0 && !map.ContainsKey(name))
                                map[name] = r.GetInt32(0);
                        }
                    }
                }
            }
            catch { /* leave map empty — caller handles the "no categories" case */ }
            return map;
        }

        // Stamps each row's CategoryValid / CategoryId from the live category map (case-insensitive)
        // and refreshes the preview grid so invalid rows show as "FIX CATEGORY".
        private void ValidateCategories()
        {
            if (_rows == null) return;
            if (_categoryMap == null) _categoryMap = LoadActiveCategories();

            foreach (var r in _rows)
            {
                var key = r.Category?.Trim();
                if (!string.IsNullOrWhiteSpace(key) && _categoryMap.TryGetValue(key, out int id))
                {
                    r.CategoryValid = true;
                    r.CategoryId = id;
                }
                else
                {
                    r.CategoryValid = false;
                    r.CategoryId = 0;
                }
            }

            PreviewGrid.Items.Refresh();
        }

        // Guarantees every row has a valid Category before import. If some don't, opens the
        // CategoryFixDialog so the user maps each unknown/blank value onto a real category,
        // then applies the mapping. Returns false if the user cancels (import aborts cleanly).
        private bool EnsureCategoriesValid()
        {
            _categoryMap = LoadActiveCategories();

            if (_categoryMap.Count == 0)
            {
                MessageBox.Show(
                    "No active item categories were found. Add at least one category in the app before importing invoices.",
                    "No Categories", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            ValidateCategories();

            var invalidValues = _rows
                .Where(r => !r.CategoryValid)
                .Select(r => (r.Category ?? string.Empty).Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidValues.Count == 0) return true;

            var validNames = _categoryMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            var fix = new CategoryFixDialog(invalidValues, validNames) { Owner = this };
            if (fix.ShowDialog() != true) return false;   // user cancelled

            // Apply the chosen mapping to every affected row, then re-validate.
            foreach (var r in _rows)
            {
                if (r.CategoryValid) continue;
                var key = (r.Category ?? string.Empty).Trim();
                if (fix.Mapping.TryGetValue(key, out var chosen))
                    r.Category = chosen;
            }

            ValidateCategories();
            return _rows.All(r => r.CategoryValid);
        }

        // Matches a Vendor by name (dbo.Vendor: VendorID, VendorName, IsActive). Unknown
        // names resolve to null so the item is simply saved without a vendor.
        private static int? ResolveVendorId(string vendorName, Dictionary<string, int?> cache)
        {
            if (string.IsNullOrWhiteSpace(vendorName)) return null;
            var key = vendorName.Trim();
            if (cache.TryGetValue(key, out var cached)) return cached;

            int? id = null;
            try
            {
                const string sql = "SELECT TOP 1 VendorID FROM dbo.Vendor WHERE VendorName = @Name AND IsActive = 1";
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Name", key);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value) id = Convert.ToInt32(val);
                    }
                }
            }
            catch { id = null; }

            cache[key] = id;
            return id;
        }

        // The exact calculation ViewInvoiceDetailPage uses. Subtotal is net-of-VAT; VAT/WHT/
        // Discount are plain percentages (12 == 12%). Intermediate math stays at full decimal
        // precision — only the display/DB values are rounded by the caller as needed.
        private static void ComputeFinancials(
            decimal subtotal, decimal vatPercent, decimal discountPercent, decimal whtPercent,
            out decimal vatAmount, out decimal discountAmount, out decimal whtAmount, out decimal totalAmountDue)
        {
            decimal vatRate = vatPercent / 100m;
            decimal discountRate = discountPercent / 100m;
            decimal whtRate = whtPercent / 100m;

            discountAmount = subtotal * discountRate;
            decimal netAfterDiscount = subtotal - discountAmount;

            vatAmount = netAfterDiscount * vatRate;
            whtAmount = netAfterDiscount * whtRate;
            totalAmountDue = netAfterDiscount + vatAmount - whtAmount;
        }

        // Joins the supplied recipient parts into the Set's "Site" text, exactly like
        // ViewInvoiceDetailPage's UpdateSiteFromBuilder ("Company - Branch - Department",
        // skipping blanks). Returns null when nothing was supplied.
        private static string ComposeSite(string company, string branch, string department)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(company)) parts.Add(company.Trim());
            if (!string.IsNullOrWhiteSpace(branch)) parts.Add(branch.Trim());
            if (!string.IsNullOrWhiteSpace(department)) parts.Add(department.Trim());
            return parts.Count > 0 ? string.Join(" - ", parts) : null;
        }

        // Writes the computed financials and the recipient Site (text + Branch/Department FK ids)
        // onto the freshly-created invoice Set. Mirrors the columns InvoiceRepository.UpdateInvoiceAsync
        // sets, but touches nothing else (ComId/Distributor/dates from CreateSoftwareServiceSet stay put).
        private static void UpdateSetInvoiceExtras(
            SqlConnection con, SqlTransaction tx, int setId,
            decimal subtotal, decimal vatAmount, decimal whtAmount,
            decimal discountAmount, decimal totalAmountDue,
            string site, int? branchId, int? departmentId)
        {
            const string sql = @"
UPDATE dbo.[Set]
SET Subtotal            = @Subtotal,
    VatAmount           = @VatAmount,
    WhtAmount           = @WhtAmount,
    DiscountAmount      = @DiscountAmount,
    TotalAmountDue      = @TotalAmountDue,
    Site                = @Site,
    CurrentBranchId     = @CurrentBranchId,
    CurrentDepartmentId = @CurrentDepartmentId
WHERE SetId = @SetId";

            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@Subtotal", subtotal);
                cmd.Parameters.AddWithValue("@VatAmount", vatAmount);
                cmd.Parameters.AddWithValue("@WhtAmount", whtAmount);
                cmd.Parameters.AddWithValue("@DiscountAmount", discountAmount);
                cmd.Parameters.AddWithValue("@TotalAmountDue", totalAmountDue);
                cmd.Parameters.AddWithValue("@Site", (object)site ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CurrentBranchId", (object)branchId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CurrentDepartmentId", (object)departmentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.ExecuteNonQuery();
            }
        }

        private static int? ResolveBranchId(string branchName, Dictionary<string, int?> cache)
            => ResolveLookupId(branchName, cache, "SELECT TOP 1 BranchId FROM dbo.Branch WHERE Name = @Name AND Active = 1");

        private static int? ResolveDepartmentId(string departmentName, Dictionary<string, int?> cache)
            => ResolveLookupId(departmentName, cache, "SELECT TOP 1 DeptId FROM dbo.Department WHERE Name = @Name AND Active = 1");

        // Shared name -> id resolver for the by-name Branch/Department lookups.
        private static int? ResolveLookupId(string name, Dictionary<string, int?> cache, string sql)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = name.Trim();
            if (cache.TryGetValue(key, out var cached)) return cached;

            int? id = null;
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Name", key);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value) id = Convert.ToInt32(val);
                    }
                }
            }
            catch { id = null; }

            cache[key] = id;
            return id;
        }

        // Flags rows that would duplicate an existing entry, reusing the item-import backend:
        //   • Serial numbers (hardware): ItemRepository.SerialNumberExists + intra-file check —
        //     dbo.Item enforces UQ_Item_SerialNumber, so a repeat serial can't be created anyway.
        //   • Document number (licenses/invoices): an existing non-archived invoice Set with the
        //     same Document # (no DB constraint enforces this, so we guard it here).
        // Consumables/licenses without a serial are intentionally NOT serial-deduped.
        private void ScanDuplicates()
        {
            if (_rows == null) return;

            var existingDocs = LoadExistingInvoiceDocNumbers();
            var dupDocs = new HashSet<string>(
                _rows.Select(r => (r.DocumentNumber ?? string.Empty).Trim())
                     .Where(d => existingDocs.Contains(d)),
                StringComparer.OrdinalIgnoreCase);

            var itemRepo = new ItemRepository();
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in _rows)
            {
                r.DuplicateInvoice = dupDocs.Contains((r.DocumentNumber ?? string.Empty).Trim());

                r.DuplicateSerial = false;
                var serial = r.SerialNumber?.Trim();
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    bool repeatedInFile = !seenSerials.Add(serial);
                    bool existsInDb = false;
                    try { existsInDb = itemRepo.SerialNumberExists(serial); } catch { }
                    r.DuplicateSerial = repeatedInFile || existsInDb;
                }

                r.DuplicateReason =
                    r.DuplicateInvoice && r.DuplicateSerial ? $"Invoice '{r.DocumentNumber}' already exists; serial '{serial}' already exists" :
                    r.DuplicateInvoice ? $"Invoice '{r.DocumentNumber}' already exists" :
                    r.DuplicateSerial  ? $"Serial '{serial}' already exists" : null;
            }

            PreviewGrid.Items.Refresh();
        }

        // Existing (non-archived) invoice Document numbers, case-insensitive.
        private static HashSet<string> LoadExistingInvoiceDocNumbers()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                const string sql = @"
SELECT s.DocumentNumber
FROM dbo.[Set] s
LEFT JOIN dbo.ArchiveStatus a ON a.EntityType = 'Set' AND a.EntityId = s.SetId AND a.IsArchived = 1
WHERE s.IsInvoice = 1 AND a.EntityId IS NULL AND s.DocumentNumber IS NOT NULL";
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            if (r.IsDBNull(0)) continue;
                            var doc = r.GetString(0).Trim();
                            if (doc.Length > 0) set.Add(doc);
                        }
                    }
                }
            }
            catch { /* leave empty — treated as "no known duplicates" */ }
            return set;
        }

        // Returns the single string value of a TOP 1 query, or null on empty/failure.
        // Used to seed the template with a real Branch / Department name.
        private static string GetFirstName(string sql)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        var val = cmd.ExecuteScalar();
                        return val != null && val != DBNull.Value ? val.ToString() : null;
                    }
                }
            }
            catch { return null; }
        }

        private static int GetDefaultConditionId()
        {
            try
            {
                const string sql = "SELECT TOP 1 ConditionId FROM dbo.Condition WHERE ConditionName = 'Good'";
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        var val = cmd.ExecuteScalar();
                        return val != null && val != DBNull.Value ? Convert.ToInt32(val) : 1;
                    }
                }
            }
            catch { return 1; }
        }
    }
}
