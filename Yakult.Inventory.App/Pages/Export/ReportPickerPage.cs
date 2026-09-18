using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Pages.Export
{
    /// <summary>
    /// Export page — lets the user check one or more report types,
    /// then click "Preview Selected" to open a combined preview dialog.
    /// </summary>
    public class ReportPickerPage : UserControl
    {
        private static readonly Color Teal       = Color.FromArgb(58, 134, 232);
        private static readonly Color PageBg     = Color.FromArgb(245, 247, 250);
        private static readonly Color CardBg     = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color TextDark   = Color.FromArgb(30, 41, 59);
        private static readonly Color TextMuted  = Color.FromArgb(100, 116, 139);

        // ── Checkboxes for each report type ───────────────────────────────
        private CheckBox _chkInvoice;
        private CheckBox _chkSets;
        private CheckBox _chkRenewal;
        private CheckBox _chkRenewalsGrouped;
        private CheckBox _chkCartridge;
        private Button   _btnPreview;
        private Button   _btnSelectAll;
        private Button   _btnClearAll;
        private Label    _lblStatus;

        public ReportPickerPage()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildPickerUi();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PICKER VIEW (report cards with checkboxes + Preview button)
        // ════════════════════════════════════════════════════════════════════

        internal void BuildPickerUi()
        {
            Controls.Clear();

            // ── Header ────────────────────────────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 80,
                BackColor = PageBg
            };
            header.Controls.Add(new Label
            {
                Text      = "Export Reports",
                Font      = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize  = true,
                Location  = new Point(24, 12)
            });
            Controls.Add(header);

            // ── Footer bar with Preview button ────────────────────────────────
            var footer = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 60,
                BackColor = Color.White
            };
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBorder))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _btnPreview = new Button
            {
                Text      = "Preview Selected  \u25B6",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size      = new Size(200, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Teal,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            _btnPreview.FlatAppearance.BorderSize = 0;
            _btnPreview.Click += async (s, e) => await OnPreviewSelectedAsync();

            _lblStatus = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                AutoSize  = true
            };

            _btnSelectAll = new Button
            {
                Text      = "Select All",
                Font      = new Font("Segoe UI", 9F, FontStyle.Regular),
                Size      = new Size(100, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextDark,
                Cursor    = Cursors.Hand
            };
            _btnSelectAll.FlatAppearance.BorderColor = CardBorder;
            _btnSelectAll.Click += (s, e) => ToggleAllCheckboxes(true);

            _btnClearAll = new Button
            {
                Text      = "Clear All",
                Font      = new Font("Segoe UI", 9F, FontStyle.Regular),
                Size      = new Size(100, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextDark,
                Cursor    = Cursors.Hand
            };
            _btnClearAll.FlatAppearance.BorderColor = CardBorder;
            _btnClearAll.Click += (s, e) => ToggleAllCheckboxes(false);

            footer.Layout += (s, e) =>
            {
                _btnPreview.Location = new Point(footer.Width - _btnPreview.Width - 40,
                    (footer.Height - _btnPreview.Height) / 2);
                    
                _btnClearAll.Location = new Point(_btnPreview.Left - _btnClearAll.Width - 10,
                    (footer.Height - _btnClearAll.Height) / 2);

                _btnSelectAll.Location = new Point(_btnClearAll.Left - _btnSelectAll.Width - 10,
                    (footer.Height - _btnSelectAll.Height) / 2);

                _lblStatus.Location = new Point(40, (footer.Height - _lblStatus.Height) / 2);
            };
            
            footer.Controls.Add(_btnSelectAll);
            footer.Controls.Add(_btnClearAll);
            footer.Controls.Add(_btnPreview);
            footer.Controls.Add(_lblStatus);
            Controls.Add(footer);

            // ── Card grid — 5 rows, each 20% height ────────────────────────
            var table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 5,
                BackColor   = PageBg,
                Padding     = new Padding(40, 16, 40, 16)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 5; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            Controls.Add(table);
            table.BringToFront();

            // ── Report cards (all use Teal accent) ──────────────────────────
            table.Controls.Add(MakeCard(
                "Invoice Report",
                "All invoices with financial details.",
                Teal, out _chkInvoice), 0, 0);

            table.Controls.Add(MakeCard(
                "Sets Report",
                "All sets with item and status details.",
                Teal, out _chkSets), 0, 1);

            table.Controls.Add(MakeCard(
                "Renewal Report",
                "Individual renewal records.",
                Teal, out _chkRenewal), 0, 2);

            table.Controls.Add(MakeCard(
                "Renewals (Grouped)",
                "Renewals grouped by company or period.",
                Teal, out _chkRenewalsGrouped), 0, 3);

            table.Controls.Add(MakeCard(
                "Cartridge Disposed / Sold",
                "Disposed and sold cartridge records.",
                Teal, out _chkCartridge), 0, 4);
        }

        private void ToggleAllCheckboxes(bool state)
        {
            if (_chkInvoice != null) _chkInvoice.Checked = state;
            if (_chkSets != null) _chkSets.Checked = state;
            if (_chkRenewal != null) _chkRenewal.Checked = state;
            if (_chkRenewalsGrouped != null) _chkRenewalsGrouped.Checked = state;
            if (_chkCartridge != null) _chkCartridge.Checked = state;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PREVIEW SELECTED — load data for every checked type, open dialog
        // ════════════════════════════════════════════════════════════════════

        private async Task OnPreviewSelectedAsync()
        {
            bool wantInvoice  = _chkInvoice.Checked;
            bool wantSets     = _chkSets.Checked;
            bool wantRenewal  = _chkRenewal.Checked;
            bool wantGrouped  = _chkRenewalsGrouped.Checked;
            bool wantCartridge = _chkCartridge.Checked;

            int checkedCount = (wantInvoice ? 1 : 0) + (wantSets ? 1 : 0) + 
                               (wantRenewal ? 1 : 0) + (wantGrouped ? 1 : 0) + 
                               (wantCartridge ? 1 : 0);

            if (checkedCount == 0)
            {
                MessageBox.Show("Please check at least one report type.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string renewalFilter = null;
            string groupedFilter = null;
            string cartridgeFilter = null;

            using (var dlg = new CombinedReportSettingsDialog(wantInvoice, wantSets, wantRenewal, wantGrouped, wantCartridge))
            {
                if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK)
                    return;

                renewalFilter = dlg.RenewalFilter;
                groupedFilter = dlg.GroupedFilter;
                cartridgeFilter = dlg.CartridgeFilter;
            }

            // ── Signatory picker — one per selected report, shown before preview opens ──
            var signatoryMap = new System.Collections.Generic.Dictionary<string, DataTable>();
            var selectedReports = new System.Collections.Generic.List<string>();
            if (wantInvoice)   selectedReports.Add("Invoice");
            if (wantRenewal)   selectedReports.Add("Renewals");
            if (wantGrouped)   selectedReports.Add("Renewals (Grouped)");
            if (wantCartridge) selectedReports.Add("Cartridge");

            foreach (var reportName in selectedReports)
            {
                using (var picker = new Dialogs.SignatoryPickerDialog(new[] { reportName }))
                {
                    if (picker.ShowDialog(this.FindForm()) != DialogResult.OK)
                        return;
                    signatoryMap[reportName] = picker.SignatoryData;
                }
            }

            // Sets report uses 4-column signatories — collect all 4 before opening
            if (wantSets)
            {
                var sigRow = ReportLauncher.ShowSetsSignatoryPickers();
                if (sigRow == null) return;
                signatoryMap["Sets"] = sigRow;
            }

            // ── Combined column selection — one tab per selected report ──────
            var colSelReports = new List<(string, IEnumerable<Dialogs.ReportColumnDef>)>();
            if (wantInvoice)   colSelReports.Add(("Invoice",             Dialogs.ReportColumnSelectionDialog.InvoiceReportColumns()));
            if (wantSets)      colSelReports.Add(("Sets",                Dialogs.ReportColumnSelectionDialog.SetsReportColumns()));
            if (wantRenewal)   colSelReports.Add(("Renewals",            Dialogs.ReportColumnSelectionDialog.RenewalsReportColumns()));
            if (wantGrouped)   colSelReports.Add(("Renewals (Grouped)",  Dialogs.ReportColumnSelectionDialog.RenewalsGroupedReportColumns()));
            if (wantCartridge) colSelReports.Add(("Cartridge",           Dialogs.ReportColumnSelectionDialog.CartridgeDisposeSoldReportColumns()));

            Dictionary<string, ReportParameter[]> colParams;
            using (var colDlg = new Dialogs.CombinedColumnSelectionDialog(colSelReports))
            {
                if (colDlg.ShowDialog(this.FindForm()) != DialogResult.OK)
                    return;
                colParams = colDlg.SelectedParameters;
            }

            _btnPreview.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                var forms = new System.Collections.Generic.List<(string Name, ReportViewerForm Form)>();

                // ── Invoice (RDL viewer) ──────────────────────────────────
                if (wantInvoice)
                {
                    _lblStatus.Text = "Opening invoice report…";
                    colParams.TryGetValue("Invoice", out var invCols);
                    var frm = await CreateInvoiceRdlReportFormAsync(signatoryMap["Invoice"], invCols);
                    if (frm != null) forms.Add(("Invoice", frm));
                }

                // ── Sets (RDLC viewer) ────────────────────────────────────
                if (wantSets)
                {
                    _lblStatus.Text = "Opening sets report…";
                    colParams.TryGetValue("Sets", out var setCols);
                    var frm = ReportLauncher.CreateSetsReportForm(signatoryMap["Sets"], setCols);
                    if (frm != null) forms.Add(("Sets", frm));
                }

                // ── Renewal (RDLC viewer) ─────────────────────────────────
                if (wantRenewal)
                {
                    _lblStatus.Text = "Opening renewals report…";
                    colParams.TryGetValue("Renewals", out var renCols);
                    var frm = ReportLauncher.CreateRenewalsReportForm(renewalFilter, signatoryMap["Renewals"], renCols);
                    if (frm != null) forms.Add(("Renewals", frm));
                }

                // ── Renewals Grouped (RDLC viewer) ────────────────────────
                if (wantGrouped)
                {
                    _lblStatus.Text = "Opening renewals grouped report…";
                    colParams.TryGetValue("Renewals (Grouped)", out var grpCols);
                    var frm = ReportLauncher.CreateRenewalGroupsReportForm(groupedFilter, signatoryMap["Renewals (Grouped)"], grpCols);
                    if (frm != null) forms.Add(("Renewals (Grouped)", frm));
                }

                // ── Cartridge Disposed/Sold (RDLC viewer) ─────────────────
                if (wantCartridge)
                {
                    _lblStatus.Text = "Opening cartridge report…";
                    colParams.TryGetValue("Cartridge", out var cartCols);
                    var frm = CreateCartridgeRdlcReportForm(cartridgeFilter, signatoryMap["Cartridge"], cartCols);
                    if (frm != null) forms.Add(("Cartridge", frm));
                }

                if (forms.Count > 0)
                {
                    using (var combinedWindow = new CombinedRdlcPreviewWindow())
                    {
                        foreach (var f in forms)
                            combinedWindow.AddReportTab(f.Name, f.Form);

                        combinedWindow.ShowDialog(this.FindForm());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reports:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _lblStatus.Text = "";
                Cursor = Cursors.Default;
                _btnPreview.Enabled = true;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Invoice RDL viewer
        // ════════════════════════════════════════════════════════════════════

        private async Task<ReportViewerForm> CreateInvoiceRdlReportFormAsync(DataTable signatoryData = null, ReportParameter[] columnParams = null)
        {
            string rdlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "InvoiceReport.rdl");
            if (!File.Exists(rdlPath))
            {
                MessageBox.Show($"Report file not found:\n{rdlPath}", "Report Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            // Query vw_InvoiceItems for all invoices (same query as ViewInvoiceDetailPage)
            DataTable dt = null;
            await Task.Run(() =>
            {
                DatabaseConfig.EnsureConfigured();
                const string sql = @"
;WITH lines AS (
    SELECT
        v.SetId,
        v.DocumentDate AS [Date],
        v.CompanyName AS Company,
        v.DocumentNumber AS SI,
        v.[Month],
        v.[Year],
        v.Status,
        '' AS Remarks,
        'NON-PO' AS POType,
        CAST(v.Quantity AS INT) AS Quantity,
        v.ItemDescription,
        v.ItemName,
        '' AS SerialNumber,
        '' AS ModelNumber,
        v.StartDate,
        v.EndDate,
        CASE
            WHEN v.StartDate IS NOT NULL AND v.EndDate IS NOT NULL
            THEN FORMAT(v.StartDate, 'dd-MMM-yy') + ' ' + FORMAT(v.EndDate, 'dd-MMM-yy')
            ELSE ''
        END AS Duration,
        v.VatAmount,
        v.DiscountAmount,
        v.WhtAmount AS WithholdingTax,
        v.TotalAmountDue,
        v.Site,
        v.SetType,
        '' AS ConditionName,
        '' AS VendorName,
        '' AS VendorAddress,
        '' AS TIN,
        v.ReferenceNumber,
        v.ExpiryStatus,
        v.ItemType,
        v.Subtotal AS HeaderSubtotal,
        CASE
            WHEN ISNULL(v.UnitPrice, 0) = 0 AND ISNULL(it.Amount, 0) > 0 THEN it.Amount
            ELSE ISNULL(v.UnitPrice, 0)
        END AS EffectiveUnitPrice,
        CASE
            WHEN ISNULL(v.LineTotal, 0) = 0 THEN CAST(ISNULL(v.Quantity, 0) AS DECIMAL(18, 4))
                * CASE
                    WHEN ISNULL(v.UnitPrice, 0) = 0 AND ISNULL(it.Amount, 0) > 0 THEN it.Amount
                    ELSE ISNULL(v.UnitPrice, 0)
                  END
            ELSE ISNULL(v.LineTotal, 0)
        END AS EffectiveLineTotal
    FROM vw_InvoiceItems v
    LEFT JOIN dbo.Item it ON it.ItemId = v.ItemId
    WHERE 1 = 1
        AND NOT EXISTS (
            SELECT 1 FROM dbo.ArchiveStatus
            WHERE EntityType = 'Set'
            AND EntityId = v.SetId
            AND IsArchived = 1
        )
        AND NOT EXISTS (
            SELECT 1 FROM dbo.ArchiveStatus
            WHERE EntityType = 'Item'
            AND EntityId = v.ItemId
            AND IsArchived = 1
        )
)
SELECT
    [Date] AS DocumentDate,
    [Month],
    [Year],
    Company AS CompanyName,
    SI AS DocumentNumber,
    Status,
    Remarks,
    POType,
    Quantity,
    ItemDescription,
    ItemName,
    SerialNumber,
    ModelNumber,
    Duration,
    StartDate,
    EndDate,
    HeaderSubtotal AS Subtotal,
    VatAmount,
    DiscountAmount,
    WithholdingTax AS WhtAmount,
    TotalAmountDue,
    SetType,
    ConditionName,
    VendorName,
    VendorAddress,
    TIN,
    ReferenceNumber,
    ExpiryStatus,
    ItemType,
    Site,
    '' AS CreatedBy
FROM lines
ORDER BY [Date], SI";

                dt = new DataTable("InvoiceData");
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new SqlCommand(sql, con))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    con.Open();
                    adapter.Fill(dt);
                }
            });

            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show("No invoice data found.",
                    "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            // Replace ##COLW_N## placeholders — same column defs as ReportLauncher._invoiceColDefs.
            // Visible columns are scaled to fill 190mm; hidden columns keep their base weight but are zero-width in the RDLC.
            var invColDefs = new (string ShowParam, int Weight)[]
            {
                ("ShowDate",        21), ("ShowCompany",     24), ("ShowDocNum",      17),
                ("ShowRefNum",      17), ("ShowStatus",      13), ("ShowQty",         10),
                ("ShowItemName",    32), ("ShowDescription", 35), ("ShowModel",       17),
                ("ShowStartDate",   17), ("ShowEndDate",     17), ("ShowAmount",      17),
                ("ShowAmount",      17), ("ShowSite",        25),
            };
            bool IsVisible(string paramName)
            {
                if (columnParams == null) return true;
                foreach (var p in columnParams)
                    if (string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase) &&
                        p.Values.Count > 0 &&
                        string.Equals(p.Values[0], "true", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
            int totalColWeight = 0;
            foreach (var cd in invColDefs)
                if (IsVisible(cd.ShowParam)) totalColWeight += cd.Weight;
            if (totalColWeight == 0) totalColWeight = invColDefs.Sum(cd => cd.Weight);
            double invScale = 190.0 / totalColWeight;
            string rdlXml = File.ReadAllText(rdlPath, System.Text.Encoding.UTF8);
            for (int i = 0; i < invColDefs.Length; i++)
            {
                bool vis = IsVisible(invColDefs[i].ShowParam);
                rdlXml = rdlXml.Replace("##COLW_" + i + "##",
                    string.Format("{0:F2}mm", vis ? invColDefs[i].Weight * invScale : (double)invColDefs[i].Weight));
            }
            byte[] rdlcBytes = System.Text.Encoding.UTF8.GetBytes(rdlXml);

            if (signatoryData == null) signatoryData = ReportLauncher.BuildEmptySignatoryTable();
            var invoiceSources = new System.Collections.Generic.List<(string, DataTable)>
            {
                ("InvoiceData",   dt),
                ("SignatoryData", signatoryData)
            };
            var form = new ReportViewerForm(rdlcBytes, "Invoice Report", invoiceSources);
            if (columnParams != null && columnParams.Length > 0)
                form.SetReportParameters(columnParams);
            return form;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Cartridge Disposed/Sold RDLC viewer
        // ════════════════════════════════════════════════════════════════════

        private ReportViewerForm CreateCartridgeRdlcReportForm(string decisionType = null, DataTable signatoryData = null, ReportParameter[] columnParams = null)
        {
            var repo = new Repositories.DisposedSoldReportRepository();
            var data = repo.GetReport(
                from:         new DateTime(DateTime.Today.Year, 1, 1),
                to:           DateTime.Today,
                decisionType: decisionType);

            if (data == null || data.Count == 0)
            {
                MessageBox.Show("No disposed/sold records found.",
                    "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            // Convert to DataTable matching RDLC DataSet field names
            var dt = new DataTable();
            dt.Columns.Add("DecisionId",           typeof(int));
            dt.Columns.Add("BatchId",              typeof(int));
            dt.Columns.Add("DecidedAt",            typeof(DateTime));
            dt.Columns.Add("DecisionTypeName",     typeof(string));
            dt.Columns.Add("Quantity",             typeof(int));
            dt.Columns.Add("RecipientName",        typeof(string));
            dt.Columns.Add("SaleAmount",           typeof(decimal));
            dt.Columns.Add("Remarks",              typeof(string));
            dt.Columns.Add("ItemName",             typeof(string));
            dt.Columns.Add("ItemModelNumber",      typeof(string));
            dt.Columns.Add("CategoryName",         typeof(string));
            dt.Columns.Add("ConditionName",        typeof(string));
            dt.Columns.Add("DecidedByName",        typeof(string));
            dt.Columns.Add("CartridgeModelNumber", typeof(string));
            dt.Columns.Add("CartridgeBrand",       typeof(string));
            dt.Columns.Add("DisposalCompanyName",  typeof(string));
            dt.Columns.Add("VendorName",           typeof(string));

            foreach (var r in data)
            {
                dt.Rows.Add(
                    r.DecisionId,
                    r.BatchId.HasValue ? (object)r.BatchId.Value : DBNull.Value,
                    r.DecidedAt,
                    r.DecisionTypeName   ?? (object)DBNull.Value,
                    r.Quantity,
                    r.RecipientName      ?? (object)DBNull.Value,
                    r.SaleAmount.HasValue ? (object)r.SaleAmount.Value : DBNull.Value,
                    r.Remarks            ?? (object)DBNull.Value,
                    r.ItemName           ?? (object)DBNull.Value,
                    r.ItemModelNumber    ?? (object)DBNull.Value,
                    r.CategoryName       ?? (object)DBNull.Value,
                    r.ConditionName      ?? (object)DBNull.Value,
                    r.DecidedByName      ?? (object)DBNull.Value,
                    r.CartridgeModelNumber  ?? (object)DBNull.Value,
                    r.CartridgeBrand        ?? (object)DBNull.Value,
                    r.DisposalCompanyName   ?? (object)DBNull.Value,
                    r.VendorName            ?? (object)DBNull.Value);
            }

            return ReportLauncher.CreateCartridgeDisposeSoldReportForm(dt, signatoryData, columnParams);
        }

        // ── Grouped-renewal builder (mirrors ExportRenewalsGrouped logic) ──
        private static List<RenewalGroupViewModel> LoadGroupedRenewals(RenewalRepository repo)
        {
            var all = repo.GetAllRenewalsAllChains();
            var parentOf = all.ToDictionary(r => r.SetId, r => r.RenewalOfSetId);

            int FindRoot(int id)
            {
                int cur = id;
                for (int guard = 0; guard < 50; guard++)
                {
                    if (!parentOf.TryGetValue(cur, out var p) || !p.HasValue) return cur;
                    cur = p.Value;
                }
                return cur;
            }

            return all
                .GroupBy(r => FindRoot(r.SetId))
                .Select(g =>
                {
                    var chain  = g.OrderByDescending(r => r.SetId).ToList();
                    var latest = chain.First();
                    var root   = chain.Last();
                    return new RenewalGroupViewModel
                    {
                        RootSetId       = g.Key,
                        RootSetCode     = root.SetCode,
                        CompanyName     = latest.CompanyName,
                        SetType         = latest.SetType,
                        OverallStatus   = latest.SetLevelStatus ?? latest.ExpiryStatus,
                        DaysUntilExpiry = latest.DaysUntilExpiry,
                        RootEndDate     = root.EndDate,
                        Active          = chain.Any(r => r.Active),
                        Chain           = chain,
                        ItemNamesBySetId = new Dictionary<int, List<string>>()
                    };
                })
                .OrderBy(g  => StatusOrder(g.OverallStatus))
                .ThenBy(g   => g.DaysUntilExpiry ?? int.MaxValue)
                .Where(g    => g.Active)
                .ToList();
        }

        private static int StatusOrder(string s)
        {
            switch (s)
            {
                case "Expired":       return 1;
                case "Expiring Soon": return 2;
                case "Warning":       return 3;
                case "Active":        return 4;
                default:              return 5;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARD BUILDER — horizontal strip with checkbox (select only)
        // ════════════════════════════════════════════════════════════════════

        private Panel MakeCard(string title, string description, Color accentColor, out CheckBox checkBox)
        {
            var card = new Panel
            {
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 6, 0, 6),
                BackColor = CardBg,
                Cursor    = Cursors.Hand
            };

            // Rounded border + accent left bar
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(CardBorder, 1.2f))
                {
                    var r = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                    using (var path = RoundedRect(r, 10))
                        g.DrawPath(pen, path);
                }
                using (var brush = new SolidBrush(accentColor))
                    g.FillRectangle(brush, 0, 10, 5, card.Height - 20);
            };

            var lblTitle = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };

            var lblDesc = new Label
            {
                Text      = description,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };

            var chk = new CheckBox
            {
                Text      = "  Select",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };

            // Highlight card border when checked
            chk.CheckedChanged += (s, e) =>
            {
                card.BackColor = chk.Checked ? Color.FromArgb(224, 242, 238) : CardBg;
                card.Invalidate();
            };

            // Vertically center content + position checkbox on right
            card.Layout += (s, e) =>
            {
                int midY = card.Height / 2;
                lblTitle.Location = new Point(24, midY - lblTitle.Height - 2);
                lblDesc.Location  = new Point(24, midY + 2);
                chk.Location      = new Point(card.Width - chk.Width - 24, midY - chk.Height / 2);
            };

            // Click anywhere on card toggles checkbox
            EventHandler toggleChk = (s, e) => chk.Checked = !chk.Checked;
            card.Click     += toggleChk;
            lblTitle.Click += toggleChk;
            lblDesc.Click  += toggleChk;

            // Hover effect (only when not checked — checked has its own tint)
            void OnEnter(object s, EventArgs e2) { if (!chk.Checked) card.BackColor = Color.FromArgb(235, 244, 255); }
            void OnLeave(object s, EventArgs e2) { if (!chk.Checked) card.BackColor = CardBg; }
            foreach (Control c in new Control[] { card, lblTitle, lblDesc, chk })
            {
                c.MouseEnter += OnEnter;
                c.MouseLeave += OnLeave;
            }

            card.Controls.Add(chk);
            card.Controls.Add(lblDesc);
            card.Controls.Add(lblTitle);

            checkBox = chk;
            return card;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
