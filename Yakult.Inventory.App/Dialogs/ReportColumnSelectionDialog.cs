using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;

namespace Yakult.Inventory.App.Dialogs
{
    public class ReportColumnDef
    {
        public string ParamName    { get; }
        public string Label        { get; }
        public bool   DefaultValue { get; }
        public bool   FitsPortrait { get; }
        public string SampleValue  { get; }

        /// <summary>
        /// True for toggles that show/hide a whole SUMMARY ROW (Subtotal, VAT, Sub-Type Groups …)
        /// rather than a physical column. They occupy no page width, so they are grouped into
        /// their own section and excluded from the portrait-fit maths.
        /// </summary>
        public bool   IsSummaryRow { get; }

        public ReportColumnDef(string paramName, string label, bool defaultValue,
                               bool fitsPortrait = true, string sampleValue = "—",
                               bool isSummaryRow = false)
        {
            ParamName    = paramName;
            Label        = label;
            DefaultValue = defaultValue;
            FitsPortrait = fitsPortrait;
            SampleValue  = sampleValue;
            IsSummaryRow = isSummaryRow;
        }
    }

    public class ReportColumnSelectionDialog : Form
    {
        // The application's standard accent blue (#3498DB) — the most-used colour in the codebase.
        private static readonly Color AccentBlue = Color.FromArgb(52, 152, 219);
        // Light tint of AccentBlue, for subtitle text on the coloured header bar.
        private static readonly Color OnAccentMuted = Color.FromArgb(214, 234, 248);
        private static readonly Color GreenOk   = Color.FromArgb(6, 120, 90);
        private static readonly Color WarnAmber = Color.FromArgb(180, 120, 0);
        private static readonly Color PageBg    = Color.FromArgb(245, 247, 250);
        private static readonly Color CardBdr   = Color.FromArgb(226, 232, 240);
        private static readonly Color TextDark  = Color.FromArgb(30, 41, 59);
        private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color DimFore   = Color.FromArgb(185, 195, 210);

        // Layout constants (card-internal positions are fixed; width is dynamic)
        private const int PadL      = 10;   // left/right padding inside card container
        private const int PadT      = 10;   // top padding inside card container
        private const int CardH     = 88;   // fixed card height
        private const int CardGapX  = 3;    // horizontal gap between cards
        private const int RowGapY   = 6;    // vertical gap between card rows
        private const int AccentH   = 3;    // top accent bar
        private const int CbY       = 7;    // checkbox Y inside card
        private const int NameY     = 27;   // column-name label Y inside card
        private const int NameH     = 30;   // column-name label height (allows 2 lines)
        private const int DivY      = 57;   // divider Y (NameY + NameH)
        private const int SampleY   = 61;   // sample-value label Y (DivY + 4)
        private const int SampleH   = 24;   // sample-value label height

        private readonly List<ReportColumnDef> _columns;
        private readonly Dictionary<string, CheckBox> _checkboxes = new Dictionary<string, CheckBox>();
        private Panel _headerPanel;
        private Panel _legendPanel;
        private Panel _footerPanel;
        private Panel _cardContainer;
        private readonly List<Panel> _cards = new List<Panel>();
        // Cards are laid out in two labelled sections, so keep each card next to its definition.
        private readonly List<(Panel Card, ReportColumnDef Def)> _cardDefs = new List<(Panel, ReportColumnDef)>();
        private Label _colSectionHdr;
        private Label _rowSectionHdr;

        private readonly string _reportName;
        private readonly Func<int, string> _describeLayout;
        private Label _fitLabel;
        private ComboBox _presetCombo;
        private bool _suppressPresetEvent;

        // ── Live preview ─────────────────────────────────────────────────────
        // Renders page 1 of the real RDLC with the current column selection. Rendering costs a few
        // hundred ms, so it runs on a background thread behind a debounce timer; a generation
        // counter discards results that a newer toggle has already superseded.
        private readonly Func<ReportParameter[], int, Image> _renderPreview;
        private Panel _previewPanel;
        private PictureBox _previewBox;
        private Label _previewStatus;
        private Label _previewHdr;
        private System.Windows.Forms.Timer _previewTimer;
        private int _previewGeneration;
        private const int PreviewPaneW = 330;
        private const int PreviewDebounceMs = 350;
        private const int PreviewDpi = 96;        // inline pane — small, so keep renders cheap
        private const int PreviewFullDpi = 200;   // full-screen — rendered once, on demand

        // Base panel heights at 100% scaling. This Form is hand-built with no designer, so it
        // never got AutoScaleMode/AutoScaleDimensions — nothing scales itself. Every fixed size
        // below therefore has to go through S() or it clips on a scaled display.
        private const int HeaderH  = 44;
        private const int LegendH  = 22;
        private const int FooterH  = 46;
        private const int PresetH  = 40;   // preset bar, between legend and cards
        private const int SectionHdrH = 20;

        /// <summary>Display scale (1.0 at 96 DPI, 1.5 at 150%).</summary>
        private readonly float _ui = 1f;

        /// <summary>Scales a 96-DPI design pixel value to the current display.</summary>
        private int S(int v) => (int)Math.Round(v * _ui);

        public ReportParameter[] SelectedParameters { get; private set; }

        /// <param name="describeLayout">
        /// Optional. Given the number of checked NON-summary columns, returns a one-line
        /// description of the page layout that count will produce (template + fit). Supplied by
        /// ReportLauncher so the thresholds stay defined in exactly one place.
        /// </param>
        /// <param name="renderPreview">
        /// Optional. Given the current column parameters, renders page 1 of the real report to an
        /// image. Invoked on a background thread, so it must not touch UI. Null disables the
        /// preview pane entirely.
        /// </param>
        public ReportColumnSelectionDialog(string reportName, IEnumerable<ReportColumnDef> columns,
                                           Func<int, string> describeLayout = null,
                                           Func<ReportParameter[], int, Image> renderPreview = null)
        {
            _columns        = columns.ToList();
            _reportName     = reportName;
            _describeLayout = describeLayout;
            _renderPreview  = renderPreview;

            using (var g = CreateGraphics())
                _ui = Math.Max(1f, g.DpiX / 96f);

            Text            = $"Column Selection — {reportName}";
            Size            = new Size(S(_renderPreview != null ? 1380 : 1050), S(640));
            MinimumSize     = new Size(S(_renderPreview != null ? 1000 : 760), S(520));
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = PageBg;
            Font            = new Font("Segoe UI", 9F);

            BuildUi();
            ApplyRemembered();
        }

        /// <summary>
        /// Restores the selection used the last time this report was generated. Falls back to each
        /// column's DefaultValue when there is nothing remembered, so first-run behaviour is
        /// exactly what it was before presets existed.
        /// </summary>
        private void ApplyRemembered()
        {
            var last = Core.ReportPreferences.GetLastColumns(_reportName);
            if (last == null) return;
            foreach (var col in _columns)
                if (_checkboxes.TryGetValue(col.ParamName, out var cb))
                    cb.Checked = last.Contains(col.ParamName);
        }

        // ── Predefined column sets per report ────────────────────────────────

        public static IEnumerable<ReportColumnDef> SetsReportColumns() => new[]
        {
            new ReportColumnDef("ShowSetCode",        "Set Code",        true,  true,  "YAK-2024-001"),
            new ReportColumnDef("ShowEmployee",       "Employee",        true,  true,  "J. Santos"),
            new ReportColumnDef("ShowRowNum",         "#",               true,  true,  "1"),
            new ReportColumnDef("ShowItemName",       "Item Name",       true,  true,  "HP LaserJet"),
            new ReportColumnDef("ShowModel",          "Model",           true,  true,  "M15w"),
            new ReportColumnDef("ShowSerial",         "Serial #",        true,  true,  "SN12345"),
            new ReportColumnDef("ShowItemType",       "Item Type",       false, false, "Printer"),
            new ReportColumnDef("ShowCategory",       "Category",        false, false, "IT Equip"),
            new ReportColumnDef("ShowQty",            "Qty",             true,  true,  "1"),
            new ReportColumnDef("ShowDateDispatched", "Date Dispatched", false, false, "2024-01-15"),
            new ReportColumnDef("ShowAmount",         "Amount",          true,  true,  "₱5,200"),
            new ReportColumnDef("ShowCompany",        "Company",         false, false, "Yakult PH"),
            new ReportColumnDef("ShowBranch",         "Branch",          false, false, "Main"),
            new ReportColumnDef("ShowDepartment",     "Department",      false, false, "IT Dept"),
        };

        public static IEnumerable<ReportColumnDef> RenewalsReportColumns() => new[]
        {
            new ReportColumnDef("ShowRowNum",      "#",             false, true,  "1"),
            new ReportColumnDef("ShowSetCode",     "Set Code",      false, true,  "YAK-2024-001"),
            new ReportColumnDef("ShowType",        "Type",          false, true,  "Renewal"),
            new ReportColumnDef("ShowDocNum",      "Document #",    true,  true,  "DOC-001"),
            new ReportColumnDef("ShowCompany",     "Company",       false, true,  "Yakult PH"),
            new ReportColumnDef("ShowSite",        "Site",          true,  true,  "Manila"),
            new ReportColumnDef("ShowSetStatus",   "Set Status",    false, true,  "Active"),
            new ReportColumnDef("ShowItemName",    "Item Description", true,  true,  "HP LaserJet"),
            new ReportColumnDef("ShowDescription", "Description",      false, false, "Printer cartridge"),
            new ReportColumnDef("ShowModel",       "Model",            true,  true,  "M15w"),
            new ReportColumnDef("ShowSerial",      "Serial #",         false, true,  "SN12345"),
            new ReportColumnDef("ShowQty",         "Qty",              true,  true,  "1"),
            new ReportColumnDef("ShowStartDate",   "Start Date",       true,  true,  "2024-01-01"),
            new ReportColumnDef("ShowExpiryDate",  "End Date",         true,  true,  "2025-01-01"),
            new ReportColumnDef("ShowItemStatus",  "Item Status",      false, true,  "Active"),
            new ReportColumnDef("ShowAmount",      "Amount",           true,  true,  "₱5,200"),
            // Summary rows
            new ReportColumnDef("ShowSubTypeGroups", "Row: Sub-Type Groups", true, true, "CONTRACT # 100316593", true),
            new ReportColumnDef("ShowParentTagGroups", "Row: Parent Tag Groups", true, true, "Hero Movies", true),
            new ReportColumnDef("ShowSubtotal",    "Row: Subtotal",     true,  true,  "₱5,000.00", true),
            new ReportColumnDef("ShowVat",         "Row: VAT",          true,  true,  "₱600.00", true),
            new ReportColumnDef("ShowWht",         "Row: WHT",          true,  true,  "₱250.00", true),
            new ReportColumnDef("ShowDiscount",    "Row: Discount",     true,  true,  "₱0.00", true),
            new ReportColumnDef("ShowTotalAmount", "Row: Total Amount", true,  true,  "₱5,350.00", true),
        };

        public static IEnumerable<ReportColumnDef> InvoiceReportColumns() => new[]
        {
            new ReportColumnDef("ShowDate",      "Document Date",    true,  true,  "01/15/2024"),
            new ReportColumnDef("ShowDocNum",    "Document #",       true,  true,  "DOC-001"),
            new ReportColumnDef("ShowRefNum",    "Reference #",      true,  true,  "REF-001"),
            new ReportColumnDef("ShowCompany",   "Recipient",        true,  true,  "Shawn Quin - YPI - IT - Manila"),
            new ReportColumnDef("ShowSite",      "Site",             false, true,  "Manila"),
            new ReportColumnDef("ShowStatus",    "Status",           true,  true,  "Active"),
            new ReportColumnDef("ShowStartDate", "Start Date",       true,  true,  "2024-01-01"),
            new ReportColumnDef("ShowEndDate",   "End Date",         true,  true,  "2025-01-01"),
            new ReportColumnDef("ShowItemName",    "Item Description", true,  true,  "HP LaserJet"),
            new ReportColumnDef("ShowDescription", "Description",    false, false, "Printer cartridge"),
            new ReportColumnDef("ShowModel",       "Model",          true,  true,  "M15w"),
            new ReportColumnDef("ShowQty",         "Quantity",       true,  true,  "1"),
            new ReportColumnDef("ShowAmount",      "Amount",         true,  true,  "₱5,200"),
            // Summary rows (not physical columns — excluded from the width/variant maths)
            new ReportColumnDef("ShowSubTypeGroups", "Row: Sub-Type Groups", true, true, "CONTRACT # 100316593", true),
            new ReportColumnDef("ShowParentTagGroups", "Row: Parent Tag Groups", true, true, "Hero Movies", true),
        };

        public static IEnumerable<ReportColumnDef> UserActivityReportColumns() => new[]
        {
            new ReportColumnDef("ShowDateTime",    "Date & Time",  true,  true,  "2026-05-12 08:44"),
            new ReportColumnDef("ShowUser",        "User",         true,  true,  "Abi"),
            new ReportColumnDef("ShowEmail",       "Email",        true,  false, "itd@yakult.com"),
            new ReportColumnDef("ShowAction",      "Action",       true,  true,  "Create"),
            new ReportColumnDef("ShowEntityType",  "Entity Type",  true,  true,  "Invoice"),
            new ReportColumnDef("ShowEntityId",    "Entity ID",    true,  true,  "1252"),
            new ReportColumnDef("ShowDescription", "Description",  true,  false, "Invoice created (Set #1252)"),
        };

        public static IEnumerable<ReportColumnDef> CartridgeDisposeSoldReportColumns() => new[]
        {
            new ReportColumnDef("ShowBatchId",        "Batch Id",            true,  true,  "1035"),
            new ReportColumnDef("ShowDate",           "Date",                true,  true,  "03/05/2026"),
            new ReportColumnDef("ShowType",           "Type",                true,  true,  "DISPOSE"),
            new ReportColumnDef("ShowCartridgeModel", "Cartridge Model",     true,  true,  "HP 85A"),
            new ReportColumnDef("ShowQty",            "Qty",                 true,  true,  "3"),
            new ReportColumnDef("ShowCondition",      "Condition",           true,  true,  "Good"),
            new ReportColumnDef("ShowRecipient",      "Recipient / Company", true,  true,  "Disposal Co."),
        };

        public static IEnumerable<ReportColumnDef> RenewalsGroupedReportColumns() => new[]
        {
            new ReportColumnDef("ShowRowNum",      "#",            false, true,  "1"),
            new ReportColumnDef("ShowSetCode",     "Set Code",     true,  true,  "YAK-2024-001"),
            new ReportColumnDef("ShowRenewedFrom", "Renewed From", false, true,  "YAK-2023"),
            new ReportColumnDef("ShowType",        "Type",         false, true,  "Renewal"),
            new ReportColumnDef("ShowDocNum",      "Document #",   true,  true,  "DOC-001"),
            new ReportColumnDef("ShowCompany",     "Company",      true,  true,  "Yakult PH"),
            new ReportColumnDef("ShowSite",        "Site",         false, true,  "Manila"),
            new ReportColumnDef("ShowSetStatus",   "Set Status",   false, true,  "Active"),
            new ReportColumnDef("ShowItemName",    "Item Description", true,  true,  "HP LaserJet"),
            new ReportColumnDef("ShowDescription", "Description",  true,  false, "Printer cartridge"),
            new ReportColumnDef("ShowModel",       "Model",        false, true,  "M15w"),
            new ReportColumnDef("ShowSerial",      "Serial #",     false, true,  "SN12345"),
            new ReportColumnDef("ShowQty",         "Qty",          true,  true,  "1"),
            new ReportColumnDef("ShowStartDate",   "Start Date",   true,  true,  "2024-01-01"),
            new ReportColumnDef("ShowExpiryDate",  "End Date",     true,  true,  "2025-01-01"),
            new ReportColumnDef("ShowItemStatus",  "Item Status",  true,  true,  "Active"),
            new ReportColumnDef("ShowAmount",      "Amount",       true,  true,  "₱5,200"),
            // Summary rows
            new ReportColumnDef("ShowSubTypeGroups", "Row: Sub-Type Groups", true, true, "CONTRACT # 100316593", true),
            new ReportColumnDef("ShowParentTagGroups", "Row: Parent Tag Groups", true, true, "Hero Movies", true),
            new ReportColumnDef("ShowSubtotal",    "Row: Subtotal",     true,  true,  "₱5,000.00", true),
            new ReportColumnDef("ShowVat",         "Row: VAT",          true,  true,  "₱600.00", true),
            new ReportColumnDef("ShowWht",         "Row: WHT",          true,  true,  "₱250.00", true),
            new ReportColumnDef("ShowDiscount",    "Row: Discount",     true,  true,  "₱0.00", true),
            new ReportColumnDef("ShowTotalAmount", "Row: Total Amount", true,  true,  "₱5,350.00", true),
        };

        // ── UI construction ──────────────────────────────────────────────────

        private void BuildUi()
        {
            // ── Header panel (no DockStyle — bounds set by DoLayout)
            _headerPanel = new Panel { BackColor = AccentBlue };
            _headerPanel.Controls.Add(new Label
            {
                Text      = "Select Columns",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(S(16), S(5))
            });
            _headerPanel.Controls.Add(new Label
            {
                Text      = "Check the columns you want included in the report. Unchecked columns will be hidden.",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = OnAccentMuted,
                AutoSize  = true,
                Location  = new Point(S(16), S(26))
            });
            Controls.Add(_headerPanel);

            // ── Legend panel
            _legendPanel = new Panel { BackColor = Color.White };
            _legendPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, _legendPanel.Height - 1, _legendPanel.Width, _legendPanel.Height - 1);
            };
            int lx = 14;
            foreach (var item in new (Color clr, string txt)[]
            {
                (AccentBlue,                         "Portrait ✓  always fits portrait"),
                (Color.FromArgb(220, 160, 40), "Overflow ⚠  may need landscape"),
                (TextMuted,                    "Summary row  full-width, uses no column width"),
            })
            {
                _legendPanel.Controls.Add(new Panel
                {
                    Location  = new Point(lx, S(6)),
                    Size      = new Size(S(9), S(9)),
                    BackColor = item.clr
                });
                var legendFont = new Font("Segoe UI", 7F);
                var legendLbl = new Label
                {
                    Text      = item.txt,
                    Font      = legendFont,
                    ForeColor = TextMuted,
                    AutoSize  = true,
                    Location  = new Point(lx + S(13), S(5))
                };
                _legendPanel.Controls.Add(legendLbl);
                // Advance by the measured text, not a fixed stride — the longest entry decides
                // the spacing, so entries can never run into each other at any scale.
                lx += S(13) + TextRenderer.MeasureText(item.txt, legendFont).Width + S(24);
            }
            Controls.Add(_legendPanel);

            // ── Footer panel
            _footerPanel = new Panel { BackColor = Color.White };
            _footerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, 0, _footerPanel.Width, 0);
            };

            var btnOk = new Button
            {
                Text      = "Generate Report",
                Size      = new Size(S(150), S(32)),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += OnOk;

            var btnCancel = new Button
            {
                Text      = "Cancel",
                Size      = new Size(S(84), S(32)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextDark,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = CardBdr;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var btnAll = new Button
            {
                Text      = "All",
                Size      = new Size(S(52), S(32)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextMuted,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand
            };
            btnAll.FlatAppearance.BorderColor = CardBdr;
            btnAll.Click += (s, e) => { foreach (var cb in _checkboxes.Values) cb.Checked = true; };

            var btnNone = new Button
            {
                Text      = "None",
                Size      = new Size(S(58), S(32)),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextMuted,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand
            };
            btnNone.FlatAppearance.BorderColor = CardBdr;
            btnNone.Click += (s, e) => { foreach (var cb in _checkboxes.Values) cb.Checked = false; };

            // Live layout readout — tells you which template the current selection will use and
            // whether it still fits portrait, instead of finding out after generating.
            _fitLabel = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = TextMuted,
                AutoSize  = false,
                Size      = new Size(S(430), S(30)),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Reposition footer buttons whenever the footer is resized
            _footerPanel.Resize += (s, e) =>
            {
                int mid = (S(FooterH) - btnOk.Height) / 2;
                btnOk.Location     = new Point(_footerPanel.Width - btnOk.Width - S(14), mid);
                btnCancel.Location = new Point(btnOk.Left - btnCancel.Width - S(8), mid);
                btnAll.Location    = new Point(S(14), mid);
                btnNone.Location   = new Point(btnAll.Right + S(5), mid);
                _fitLabel.Location = new Point(btnNone.Right + S(14), mid);
                _fitLabel.Width    = Math.Max(S(60), btnCancel.Left - btnNone.Right - S(28));
            };
            _footerPanel.Controls.AddRange(new Control[] { btnOk, btnCancel, btnAll, btnNone, _fitLabel });
            Controls.Add(_footerPanel);

            BuildPresetBar();

            // ── Card container — scrolls, since two sections can exceed the window
            _cardContainer = new Panel { BackColor = PageBg, AutoScroll = true };

            _colSectionHdr = BuildSectionHeader("COLUMNS  —  shown as table columns, share the 190mm page width");
            _rowSectionHdr = BuildSectionHeader("SUMMARY ROWS  —  full-width rows under the items, use no column width");
            _cardContainer.Controls.Add(_colSectionHdr);
            _cardContainer.Controls.Add(_rowSectionHdr);

            foreach (var col in _columns)
            {
                var card = BuildCard(col);
                _cards.Add(card);
                _cardDefs.Add((card, col));
                _cardContainer.Controls.Add(card);
            }
            _rowSectionHdr.Visible = _columns.Any(c => c.IsSummaryRow);

            Controls.Add(_cardContainer);

            BuildPreviewPane();

            // Explicit layout on Load and every resize — guaranteed correct bounds
            Resize += (s, e) => DoLayout();
            Load   += (s, e) => { DoLayout(); UpdateFitLabel(); };
            Shown  += (s, e) => SchedulePreview();

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        // ── Live preview ─────────────────────────────────────────────────────

        private void BuildPreviewPane()
        {
            if (_renderPreview == null) return;

            _previewPanel = new Panel { BackColor = Color.White, Name = "_previewPane" };
            _previewPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, 0, 0, _previewPanel.Height);
            };

            _previewHdr = new Label
            {
                Text      = "LIVE PREVIEW",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _previewStatus = new Label
            {
                Text      = "Rendering…",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = TextMuted,
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var expandFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            const string expandText = "⛶  Full screen";
            var btnExpand = new Button
            {
                Text      = expandText,
                // Sized from the text and positioned by DoLayout. NO Anchor here: an anchor makes
                // WinForms re-offset the control from its original bounds, which fought the manual
                // placement and pushed the button out of the visible pane entirely.
                Size      = new Size(TextRenderer.MeasureText(expandText, expandFont).Width + S(22), S(26)),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White,
                Font      = expandFont,
                Cursor    = Cursors.Hand,
                Name      = "_btnExpand"
            };
            btnExpand.FlatAppearance.BorderSize = 0;
            btnExpand.Click += (s, e) => ShowFullScreenPreview();

            _previewBox = new PictureBox
            {
                SizeMode    = PictureBoxSizeMode.Zoom,
                BackColor   = Color.FromArgb(250, 251, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor      = Cursors.Hand
            };
            // Double-clicking the thumbnail is the obvious gesture; keep the button for discovery.
            _previewBox.DoubleClick += (s, e) => ShowFullScreenPreview();

            _previewPanel.Controls.AddRange(new Control[] { _previewHdr, _previewStatus, btnExpand, _previewBox });
            Controls.Add(_previewPanel);

            _previewTimer = new System.Windows.Forms.Timer { Interval = PreviewDebounceMs };
            _previewTimer.Tick += (s, e) => { _previewTimer.Stop(); StartPreviewRender(); };
        }

        /// <summary>Restarts the debounce window — called on every checkbox change.</summary>
        private void SchedulePreview()
        {
            if (_previewTimer == null) return;
            _previewTimer.Stop();
            _previewTimer.Start();
            if (_previewStatus != null) _previewStatus.Text = "Rendering…";
        }

        private void StartPreviewRender()
        {
            if (_renderPreview == null || !IsHandleCreated) return;

            int generation = ++_previewGeneration;
            var parameters = BuildParameters();

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                Image img = null;
                string error = null;
                try { img = _renderPreview(parameters, PreviewDpi); }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Core.Logger.LogError("Column preview render failed", ex);
                }

                // The form can be disposed at ANY point after this check — closing the dialog while
                // a render is in flight is normal. Catch everything: an escaping exception here is
                // on a ThreadPool thread, where it takes the whole process down rather than being
                // handled anywhere useful.
                if (IsDisposed || !IsHandleCreated) { img?.Dispose(); return; }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            // A newer toggle already queued a render — drop this stale one.
                            if (generation != _previewGeneration || IsDisposed) { img?.Dispose(); return; }
                            ApplyPreview(img, error);
                        }
                        catch (Exception ex2)
                        {
                            img?.Dispose();
                            Core.Logger.LogError("Column preview: applying render failed", ex2);
                        }
                    }));
                }
                catch (Exception)
                {
                    // Disposed between the guard above and BeginInvoke (ObjectDisposedException),
                    // or the handle went away (InvalidOperationException). Nothing to apply.
                    img?.Dispose();
                }
            });
        }

        private void ApplyPreview(Image img, string error)
        {
            // Teardown can have run between the render finishing and this callback being pumped.
            if (IsDisposed || _previewBox == null || _previewBox.IsDisposed || _previewStatus == null)
            {
                img?.Dispose();
                return;
            }

            if (img != null)
            {
                var old = _previewBox.Image;
                _previewBox.Image = img;
                old?.Dispose();
                _previewStatus.Text      = "Page 1 — sample rows, actual column widths";
                _previewStatus.ForeColor = TextMuted;
            }
            else
            {
                _previewStatus.Text      = "Preview unavailable: " + error;
                _previewStatus.ForeColor = WarnAmber;
            }
        }

        /// <summary>
        /// Opens the current selection full screen, re-rendered at a higher DPI than the pane
        /// thumbnail so text is actually legible. Blocks on the render behind a wait cursor —
        /// this is an explicit user action, unlike the debounced inline preview.
        /// </summary>
        private void ShowFullScreenPreview()
        {
            if (_renderPreview == null) return;

            Image img;
            var parameters = BuildParameters();
            try
            {
                Cursor = Cursors.WaitCursor;
                img = _renderPreview(parameters, PreviewFullDpi);
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("Full-screen preview render failed", ex);
                MessageBox.Show(this, "Could not render the preview.\n\n" + ex.Message,
                    "Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally { Cursor = Cursors.Default; }

            using (var win = new PreviewWindow(img, _reportName, _ui))
                win.ShowDialog(this);
        }

        /// <summary>
        /// Maximised viewer for the rendered preview page. Starts fitted to the window; toggling to
        /// 100% switches on scrollbars so a dense report can be inspected at full size.
        /// </summary>
        private sealed class PreviewWindow : Form
        {
            private readonly PictureBox _pic;
            private readonly Panel _scroll;
            private readonly Image _img;
            private bool _actualSize;

            public PreviewWindow(Image img, string reportName, float ui)
            {
                int S(int v) => (int)Math.Round(v * ui);

                _img            = img;
                Text            = "Preview — " + reportName;
                WindowState     = FormWindowState.Maximized;
                StartPosition   = FormStartPosition.CenterParent;
                BackColor       = Color.FromArgb(60, 63, 68);
                KeyPreview      = true;
                ShowInTaskbar   = false;

                var bar = new Panel { Dock = DockStyle.Bottom, Height = S(44), BackColor = Color.White };

                var btnClose = new Button
                {
                    Text = "Close", Size = new Size(S(90), S(28)), FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => Close();

                var btnZoom = new Button
                {
                    Text = "Actual size (100%)", Size = new Size(S(140), S(28)), FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White, ForeColor = Color.FromArgb(100, 116, 139),
                    Font = new Font("Segoe UI", 9F), Cursor = Cursors.Hand
                };
                btnZoom.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
                btnZoom.Click += (s, e) =>
                {
                    _actualSize = !_actualSize;
                    btnZoom.Text = _actualSize ? "Fit to window" : "Actual size (100%)";
                    ApplyZoom();
                };

                var hint = new Label
                {
                    Text = "Page 1 of the report as it will print.  Esc closes.",
                    Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(100, 116, 139),
                    AutoSize = true
                };

                bar.Controls.AddRange(new Control[] { btnZoom, btnClose, hint });
                bar.Resize += (s, e) =>
                {
                    int mid = (bar.Height - btnClose.Height) / 2;
                    btnClose.Location = new Point(bar.Width - btnClose.Width - S(14), mid);
                    btnZoom.Location  = new Point(S(14), mid);
                    hint.Location     = new Point(btnZoom.Right + S(14), mid + S(6));
                };

                _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor };
                _pic    = new PictureBox { BackColor = Color.White };
                _scroll.Controls.Add(_pic);

                Controls.Add(_scroll);
                Controls.Add(bar);

                _scroll.Resize += (s, e) => ApplyZoom();
                Load           += (s, e) => ApplyZoom();
                KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

                CancelButton = btnClose;
            }

            private void ApplyZoom()
            {
                if (_img == null) return;

                if (_actualSize)
                {
                    _pic.SizeMode = PictureBoxSizeMode.AutoSize;
                    _pic.Image    = _img;
                    _pic.Location = new Point(0, 0);
                    _scroll.AutoScroll = true;
                    return;
                }

                // Fit: preserve aspect ratio inside the viewport and centre the page.
                _scroll.AutoScroll = false;
                int vw = Math.Max(1, _scroll.ClientSize.Width  - 24);
                int vh = Math.Max(1, _scroll.ClientSize.Height - 24);
                double scale = Math.Min((double)vw / _img.Width, (double)vh / _img.Height);
                int w = Math.Max(1, (int)(_img.Width * scale));
                int h = Math.Max(1, (int)(_img.Height * scale));

                _pic.SizeMode = PictureBoxSizeMode.Zoom;
                _pic.Image    = _img;
                _pic.SetBounds((_scroll.ClientSize.Width - w) / 2, (_scroll.ClientSize.Height - h) / 2, w, h);
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                _pic.Image = null;      // the caller owns _img and disposes it
                _img?.Dispose();
                base.OnFormClosed(e);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Invalidate any in-flight render, then release the timer and the last bitmap.
            _previewGeneration++;
            _previewTimer?.Stop();
            _previewTimer?.Dispose();
            if (_previewBox != null) { _previewBox.Image?.Dispose(); _previewBox.Image = null; }
            base.OnFormClosed(e);
        }

        private Label BuildSectionHeader(string text) => new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = TextMuted,
            AutoSize  = false,
            Height    = S(SectionHdrH),
            TextAlign = ContentAlignment.BottomLeft
        };

        // ── Presets ──────────────────────────────────────────────────────────

        private void BuildPresetBar()
        {
            var bar = new Panel { BackColor = Color.White, Name = "_presetBar" };
            bar.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            var barFont = new Font("Segoe UI", 8.5F);

            var lbl = new Label
            {
                Text      = "Preset:",
                Font      = barFont,
                ForeColor = TextMuted,
                AutoSize  = true
            };

            _presetCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = S(220),
                Font          = barFont
            };
            _presetCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressPresetEvent) return;
                var name = _presetCombo.SelectedItem as string;
                if (string.IsNullOrEmpty(name) || name == "(none)") return;
                var preset = Core.ReportPreferences.GetPreset(_reportName, name);
                if (preset == null) return;
                foreach (var col in _columns)
                    if (_checkboxes.TryGetValue(col.ParamName, out var cb))
                        cb.Checked = preset.Contains(col.ParamName);
            };

            // Width comes from the rendered text, not a magic number — a fixed width clips as soon
            // as the display scale or font changes.
            Button Small(string text)
            {
                var b = new Button
                {
                    Text      = text,
                    Size      = new Size(TextRenderer.MeasureText(text, barFont).Width + S(22), S(26)),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    ForeColor = TextMuted,
                    Font      = barFont,
                    Cursor    = Cursors.Hand
                };
                b.FlatAppearance.BorderColor = CardBdr;
                return b;
            }

            var btnSave = Small("Save as…");
            btnSave.Click += (s, e) => SaveCurrentAsPreset();

            var btnDelete = Small("Delete");
            btnDelete.Click += (s, e) =>
            {
                var name = _presetCombo.SelectedItem as string;
                if (string.IsNullOrEmpty(name) || name == "(none)") return;
                if (MessageBox.Show($"Delete preset \"{name}\"?", "Delete preset",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Core.ReportPreferences.DeletePreset(_reportName, name);
                RefreshPresetList(null);
            };

            // Flow the row left to right off each control's measured size, re-run on every resize.
            // The previous fixed offsets overlapped as soon as the display scale changed.
            bar.Resize += (s, e) =>
            {
                int Mid(Control c) => Math.Max(0, (bar.Height - c.Height) / 2);

                int x = S(14);
                lbl.Location = new Point(x, Mid(lbl));
                x = lbl.Right + S(8);

                // Give the combo whatever is left after the two buttons, within sane bounds.
                int buttonsW  = btnSave.Width + S(6) + btnDelete.Width + S(14);
                int comboRoom = bar.Width - x - buttonsW;
                _presetCombo.Width    = Math.Max(S(120), Math.Min(S(260), comboRoom));
                _presetCombo.Location = new Point(x, Mid(_presetCombo));
                x = _presetCombo.Right + S(10);

                btnSave.Location   = new Point(x, Mid(btnSave));
                btnDelete.Location = new Point(btnSave.Right + S(6), Mid(btnDelete));
            };

            bar.Controls.AddRange(new Control[] { lbl, _presetCombo, btnSave, btnDelete });
            Controls.Add(bar);
            RefreshPresetList(null);
        }

        private void SaveCurrentAsPreset()
        {
            string name = PromptForText("Save the current column selection as:", "Save preset");
            if (string.IsNullOrWhiteSpace(name)) return;
            Core.ReportPreferences.SavePreset(_reportName, name.Trim(), CheckedParamNames());
            RefreshPresetList(name.Trim());
        }

        private void RefreshPresetList(string select)
        {
            _suppressPresetEvent = true;
            try
            {
                _presetCombo.Items.Clear();
                _presetCombo.Items.Add("(none)");
                foreach (var n in Core.ReportPreferences.GetPresetNames(_reportName))
                    _presetCombo.Items.Add(n);
                _presetCombo.SelectedItem = select != null && _presetCombo.Items.Contains(select)
                    ? select : "(none)";
            }
            finally { _suppressPresetEvent = false; }
        }

        /// <summary>Minimal single-line text prompt (WinForms has no built-in InputBox).</summary>
        private string PromptForText(string prompt, string title)
        {
            using (var dlg = new Form
            {
                Text = title, Size = new Size(400, 160), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.White, Font = new Font("Segoe UI", 9F)
            })
            {
                var lbl = new Label { Text = prompt, AutoSize = true, Location = new Point(14, 16) };
                var box = new TextBox { Location = new Point(16, 44), Width = 350 };
                var ok  = new Button { Text = "Save", DialogResult = DialogResult.OK,
                                       Location = new Point(206, 78), Size = new Size(78, 28),
                                       FlatStyle = FlatStyle.Flat, BackColor = AccentBlue, ForeColor = Color.White };
                ok.FlatAppearance.BorderSize = 0;
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel,
                                          Location = new Point(290, 78), Size = new Size(78, 28),
                                          FlatStyle = FlatStyle.Flat };
                dlg.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                return dlg.ShowDialog(this) == DialogResult.OK ? box.Text : null;
            }
        }

        private List<string> CheckedParamNames() =>
            _columns.Where(c => _checkboxes.TryGetValue(c.ParamName, out var cb) && cb.Checked)
                    .Select(c => c.ParamName).ToList();

        /// <summary>Refreshes the footer readout from the current checkbox state.</summary>
        private void UpdateFitLabel()
        {
            if (_fitLabel == null) return;

            int cols = _columns.Count(c => !c.IsSummaryRow
                                           && _checkboxes.TryGetValue(c.ParamName, out var cb) && cb.Checked);
            int rows = _columns.Count(c => c.IsSummaryRow
                                           && _checkboxes.TryGetValue(c.ParamName, out var cb) && cb.Checked);

            string text = cols + (cols == 1 ? " column" : " columns");
            if (rows > 0) text += " + " + rows + (rows == 1 ? " summary row" : " summary rows");
            if (_describeLayout != null)
            {
                string hint = _describeLayout(cols);
                if (!string.IsNullOrEmpty(hint)) text += "  —  " + hint;
            }

            _fitLabel.Text = text;
            // Amber only when nothing is selected or the layout describer flags a squeeze.
            _fitLabel.ForeColor = cols == 0 || text.IndexOf("cramped", StringComparison.OrdinalIgnoreCase) >= 0
                ? WarnAmber : TextMuted;
        }

        // Sets every panel to its exact pixel bounds and then reflows the cards.
        private void DoLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            _headerPanel.SetBounds(0, 0, w, S(HeaderH));
            _legendPanel.SetBounds(0, S(HeaderH), w, S(LegendH));

            var presetBar = Controls.Find("_presetBar", false).FirstOrDefault();
            if (presetBar != null) presetBar.SetBounds(0, S(HeaderH) + S(LegendH), w, S(PresetH));

            _footerPanel.SetBounds(0, h - S(FooterH), w, S(FooterH));

            int cTop = S(HeaderH) + S(LegendH) + (presetBar != null ? S(PresetH) : 0);
            int cH   = Math.Max(0, h - cTop - S(FooterH));

            // Preview pane takes a fixed strip on the right; the cards get everything left of it.
            int cardsW = w;
            if (_previewPanel != null)
            {
                int paneW = Math.Min(S(PreviewPaneW), Math.Max(S(160), w / 3));
                _previewPanel.SetBounds(w - paneW, cTop, paneW, cH);

                // Row 1: "LIVE PREVIEW" on the left, the Full screen button hard right.
                var btnExpand = _previewPanel.Controls.Find("_btnExpand", false).FirstOrDefault();
                int hdrRight = paneW - S(12);
                if (btnExpand != null)
                {
                    btnExpand.Location = new Point(paneW - btnExpand.Width - S(12), S(8));
                    hdrRight = btnExpand.Left - S(8);
                }
                _previewHdr.SetBounds(S(12), S(10), Math.Max(S(40), hdrRight - S(12)), S(18));
                // Row 2: status, full pane width. Row 3 onward: the page image.
                _previewStatus.SetBounds(S(12), S(38), Math.Max(S(40), paneW - S(24)), S(18));
                _previewBox.SetBounds(S(12), S(60), paneW - S(24), Math.Max(S(40), cH - S(72)));
                cardsW = w - paneW;
            }

            _cardContainer.SetBounds(0, cTop, cardsW, cH);

            LayoutCards();
        }

        // Lays the cards out as two labelled sections — real columns first, then the summary-row
        // toggles — each as its own grid filling the container width.
        private void LayoutCards()
        {
            if (_cardDefs.Count == 0) return;

            int available = _cardContainer.ClientSize.Width - 2 * S(PadL) - SystemInformation.VerticalScrollBarWidth;
            if (available < 120) return;

            int y = S(PadT);
            y = LayoutSection(_colSectionHdr, _cardDefs.Where(cd => !cd.Def.IsSummaryRow).ToList(), available, y);
            y = LayoutSection(_rowSectionHdr, _cardDefs.Where(cd =>  cd.Def.IsSummaryRow).ToList(), available, y);

            _cardContainer.AutoScrollMinSize = new Size(0, y + S(PadT));
        }

        private int LayoutSection(Label header, List<(Panel Card, ReportColumnDef Def)> items, int available, int y)
        {
            if (items.Count == 0)
            {
                if (header != null) header.Visible = false;
                return y;
            }

            if (header != null)
            {
                header.Visible = true;
                header.SetBounds(S(PadL), y, available, S(SectionHdrH));
                y += S(SectionHdrH) + S(4);
            }

            // Keep cards readable: aim for ~150px each, then balance so the last row isn't a stub.
            int perRow = Math.Max(1, available / S(150));
            perRow = Math.Min(perRow, items.Count);
            int rows = (int)Math.Ceiling((double)items.Count / perRow);
            perRow = (int)Math.Ceiling((double)items.Count / rows);

            int cardW = Math.Max(S(80), (available - (perRow - 1) * S(CardGapX)) / perRow);

            for (int i = 0; i < items.Count; i++)
            {
                int r = i / perRow, c = i % perRow;
                var card = items[i].Card;

                card.SetBounds(S(PadL) + c * (cardW + S(CardGapX)), y + r * (S(CardH) + S(RowGapY)), cardW, S(CardH));

                // Resize child labels to match the new card width
                foreach (Control ctrl in card.Controls)
                    switch (ctrl.Tag as string)
                    {
                        case "badge":  ctrl.Width = Math.Max(S(10), cardW - S(27)); break;
                        case "name":   ctrl.Width = Math.Max(S(10), cardW - S(8));  break;
                        case "sample": ctrl.Width = Math.Max(S(10), cardW - S(8));  break;
                    }

                card.Invalidate();
            }

            return y + rows * (S(CardH) + S(RowGapY)) + S(8);
        }

        private Panel BuildCard(ReportColumnDef col)
        {
            bool on = col.DefaultValue;
            Color accentColor = col.FitsPortrait ? AccentBlue : Color.FromArgb(220, 160, 40);

            // Card: size and position are set later by LayoutCards()
            var card = new Panel
            {
                BackColor = on ? Color.White : Color.FromArgb(250, 251, 253),
                Cursor    = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                // Top accent bar
                using (var b = new SolidBrush(accentColor))
                    g.FillRectangle(b, 0, 0, card.Width, AccentH);
                // Card border
                using (var pen = new Pen(CardBdr))
                    g.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                // Divider between name and sample rows
                using (var pen = new Pen(CardBdr))
                    g.DrawLine(pen, 1, DivY, card.Width - 2, DivY);
            };

            var cb = new CheckBox
            {
                Checked  = col.DefaultValue,
                AutoSize = false,
                Location = new Point(5, CbY),
                Size     = new Size(16, 16),
                Cursor   = Cursors.Hand
            };
            _checkboxes[col.ParamName] = cb;

            var badge = new Label
            {
                // Summary rows span the full page width, so a portrait/overflow verdict is
                // meaningless for them — label what they actually are instead.
                Text      = col.IsSummaryRow ? "Summary row" : col.FitsPortrait ? "Portrait ✓" : "Overflow ⚠",
                Font      = new Font("Segoe UI", 6F, FontStyle.Bold),
                ForeColor = on ? (col.IsSummaryRow ? TextMuted : col.FitsPortrait ? GreenOk : WarnAmber) : DimFore,
                AutoSize  = false,
                Location  = new Point(24, CbY + 2),
                Size      = new Size(100, 12),
                TextAlign = ContentAlignment.MiddleLeft,
                Tag       = "badge"
            };

            var nameLbl = new Label
            {
                Text      = col.Label,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = on ? TextDark : DimFore,
                AutoSize  = false,
                Location  = new Point(5, NameY),
                Size      = new Size(100, NameH),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor    = Cursors.Hand,
                Tag       = "name"
            };

            var sampleLbl = new Label
            {
                Text      = col.SampleValue,
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = on ? TextMuted : DimFore,
                AutoSize  = false,
                Location  = new Point(5, SampleY),
                Size      = new Size(100, SampleH),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor    = Cursors.Hand,
                Tag       = "sample"
            };

            cb.CheckedChanged += (s, e) =>
            {
                ApplyCardState(card, col, cb.Checked);
                UpdateFitLabel();
                SchedulePreview();
            };

            Action toggle = () => cb.Checked = !cb.Checked;
            card.Click    += (s, e) => toggle();
            badge.Click   += (s, e) => toggle();
            nameLbl.Click  += (s, e) => { _checkboxes[col.ParamName].Checked = !_checkboxes[col.ParamName].Checked; };
            sampleLbl.Click += (s, e) => { _checkboxes[col.ParamName].Checked = !_checkboxes[col.ParamName].Checked; };

            card.Controls.Add(cb);
            card.Controls.Add(badge);
            card.Controls.Add(nameLbl);
            card.Controls.Add(sampleLbl);

            return card;
        }

        private static void ApplyCardState(Panel card, ReportColumnDef col, bool on)
        {
            foreach (Control ctrl in card.Controls)
            {
                switch (ctrl.Tag as string)
                {
                    case "name":   ctrl.ForeColor = on ? TextDark  : DimFore; break;
                    case "sample": ctrl.ForeColor = on ? TextMuted : DimFore; break;
                    case "badge":  ctrl.ForeColor = on ? (col.IsSummaryRow ? TextMuted : col.FitsPortrait ? GreenOk : WarnAmber) : DimFore; break;
                }
            }
            card.BackColor = on ? Color.White : Color.FromArgb(250, 251, 253);
            card.Invalidate();
        }

        /// <summary>Current checkbox state as report parameters — used by both OK and the preview.</summary>
        private ReportParameter[] BuildParameters() =>
            _columns
                .Select(col => new ReportParameter(
                    col.ParamName,
                    _checkboxes.TryGetValue(col.ParamName, out var cb)
                        ? cb.Checked.ToString()
                        : col.DefaultValue.ToString()))
                .ToArray();

        private void OnOk(object sender, EventArgs e)
        {
            SelectedParameters = BuildParameters();

            // Remember this selection so the next run of the same report starts where you left off.
            Core.ReportPreferences.SetLastColumns(_reportName, CheckedParamNames());

            DialogResult = DialogResult.OK;
            Close();
        }

        public static ReportParameter[] Show(IWin32Window owner, string reportName,
                                             IEnumerable<ReportColumnDef> columns,
                                             Func<int, string> describeLayout = null,
                                             Func<ReportParameter[], int, Image> renderPreview = null)
        {
            using (var dlg = new ReportColumnSelectionDialog(reportName, columns, describeLayout, renderPreview))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;
                return dlg.SelectedParameters;
            }
        }

        /// <summary>
        /// Shows the dialog and returns the set of selected (checked) param names,
        /// or null if the user cancelled.
        /// </summary>
        public static HashSet<string> ShowAndGetSelected(IWin32Window owner, string reportName,
                                                          IEnumerable<ReportColumnDef> columns)
        {
            var result = Show(owner, reportName, columns);
            if (result == null) return null;
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in result)
            {
                if (p.Values != null && p.Values.Count > 0
                    && string.Equals(p.Values[0], "True", StringComparison.OrdinalIgnoreCase))
                    selected.Add(p.Name);
            }
            return selected;
        }
    }
}
