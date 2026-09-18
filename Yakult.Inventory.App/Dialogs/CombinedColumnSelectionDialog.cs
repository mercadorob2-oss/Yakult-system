using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Shows a tab per selected report, each with the same column-card grid as
    /// ReportColumnSelectionDialog. Returns ReportParameter[] for every report.
    /// </summary>
    public class CombinedColumnSelectionDialog : Form
    {
        // ── Palette (mirrors ReportColumnSelectionDialog) ────────────────────
        // The application's standard accent blue (#3498DB). This was previously called "Teal" while
        // actually holding a third blue (58,134,232); renamed and aligned so all report dialogs match.
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

        // ── Card layout constants ────────────────────────────────────────────
        private const int PadL     = 10;
        private const int PadT     = 10;
        private const int CardH    = 88;
        private const int CardGapX = 3;
        private const int RowGapY  = 6;
        private const int AccentH  = 3;
        private const int CbY      = 7;
        private const int NameY    = 27;
        private const int NameH    = 30;
        private const int DivY     = 57;
        private const int SampleY  = 61;
        private const int SampleH  = 24;

        // ── Per-report state ─────────────────────────────────────────────────
        private readonly List<(string ReportName, List<ReportColumnDef> Columns)> _reports;
        // checkboxes[reportName][paramName] = CheckBox
        private readonly Dictionary<string, Dictionary<string, CheckBox>> _checkboxes
            = new Dictionary<string, Dictionary<string, CheckBox>>();
        // cards[reportName] = list of card panels
        private readonly Dictionary<string, List<Panel>> _cards
            = new Dictionary<string, List<Panel>>();

        /// <summary>
        /// Result: maps each report name to its chosen ReportParameter[].
        /// Only populated after DialogResult.OK.
        /// </summary>
        public Dictionary<string, ReportParameter[]> SelectedParameters { get; private set; }

        // ── Layout panels ────────────────────────────────────────────────────
        private TabControl _tabs;
        private Panel      _header;
        private Panel      _footer;
        private Button     _btnAll;
        private Button     _btnNone;

        private const int HeaderH = 52;
        private const int FooterH = 48;

        // ════════════════════════════════════════════════════════════════════
        //  Constructor
        // ════════════════════════════════════════════════════════════════════

        public CombinedColumnSelectionDialog(
            IEnumerable<(string ReportName, IEnumerable<ReportColumnDef> Columns)> reports)
        {
            _reports = reports
                .Select(r => (r.ReportName, r.Columns.ToList()))
                .ToList();

            Text            = "Column Selection — All Reports";
            Size            = new Size(1080, 530);
            MinimumSize     = new Size(760, 460);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = PageBg;
            Font            = new Font("Segoe UI", 9F);

            BuildUi();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI construction
        // ════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            // ── Header ───────────────────────────────────────────────────────
            _header = new Panel { BackColor = AccentBlue };
            _header.Controls.Add(new Label
            {
                Text      = "Column Selection",
                Font      = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 6)
            });
            _header.Controls.Add(new Label
            {
                Text      = "Choose which columns to include for each report. Switch tabs to configure each one.",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = OnAccentMuted,
                AutoSize  = true,
                Location  = new Point(16, 30)
            });
            Controls.Add(_header);

            // ── Tab control ──────────────────────────────────────────────────
            _tabs = new TabControl
            {
                Font     = new Font("Segoe UI", 9.5F),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(160, 30),
                SizeMode = TabSizeMode.Fixed,
                Padding  = new Point(10, 6)
            };
            _tabs.DrawItem += DrawTabItem;
            Controls.Add(_tabs);

            foreach (var (reportName, columns) in _reports)
                AddReportTab(reportName, columns);

            // ── Footer ───────────────────────────────────────────────────────
            _footer = new Panel { BackColor = Color.White };
            _footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
            };

            _btnAll = MakeFooterButton("All", false);
            _btnNone = MakeFooterButton("None", false);
            _btnAll.Click  += (s, e) => SetAllInCurrentTab(true);
            _btnNone.Click += (s, e) => SetAllInCurrentTab(false);

            var btnCancel = MakeFooterButton("Cancel", false);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var btnOk = MakeFooterButton("Preview Reports", true);
            btnOk.BackColor = AccentBlue;
            btnOk.ForeColor = Color.White;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Width = 140;
            btnOk.Click += OnOk;

            _footer.Resize += (s, e) =>
            {
                int mid = (FooterH - btnOk.Height) / 2;
                btnOk.Location     = new Point(_footer.Width - btnOk.Width - 14, mid);
                btnCancel.Location = new Point(btnOk.Left - btnCancel.Width - 8, mid);
                _btnAll.Location   = new Point(14, mid);
                _btnNone.Location  = new Point(_btnAll.Right + 6, mid);
            };
            _footer.Controls.AddRange(new Control[] { btnOk, btnCancel, _btnAll, _btnNone });
            Controls.Add(_footer);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Resize += (s, e) => DoLayout();
            Load   += (s, e) => DoLayout();
        }

        private static Button MakeFooterButton(string text, bool primary)
        {
            var b = new Button
            {
                Text      = text,
                Size      = new Size(text == "All" || text == "None" ? 52 : 88, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = primary ? Color.White : TextDark,
                Font      = new Font("Segoe UI", 9F, primary ? FontStyle.Bold : FontStyle.Regular),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = CardBdr;
            return b;
        }

        private void AddReportTab(string reportName, List<ReportColumnDef> columns)
        {
            var cbMap   = new Dictionary<string, CheckBox>();
            var cardList = new List<Panel>();

            _checkboxes[reportName] = cbMap;
            _cards[reportName]      = cardList;

            var container = new Panel { BackColor = PageBg, AutoScroll = false };

            // ── Legend strip at top of each tab ──────────────────────────────
            var legend = new Panel { BackColor = Color.White, Height = 22 };
            legend.Paint += (s, e) =>
            {
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 0, legend.Height - 1, legend.Width, legend.Height - 1);
            };
            int lx = 14;
            foreach (var item in new (Color clr, string txt)[]
            {
                (AccentBlue,                         "Portrait ✓  always fits portrait"),
                (Color.FromArgb(220, 160, 40), "Overflow ⚠  may need landscape"),
            })
            {
                legend.Controls.Add(new Panel
                {
                    Location  = new Point(lx, 7),
                    Size      = new Size(9, 9),
                    BackColor = item.clr
                });
                legend.Controls.Add(new Label
                {
                    Text      = item.txt,
                    Font      = new Font("Segoe UI", 7F),
                    ForeColor = TextMuted,
                    AutoSize  = true,
                    Location  = new Point(lx + 13, 5)
                });
                lx += 220;
            }

            // ── Card grid ────────────────────────────────────────────────────
            var grid = new Panel { BackColor = PageBg, AutoScroll = false };

            foreach (var col in columns)
            {
                var card = BuildCard(col, cbMap);
                cardList.Add(card);
                grid.Controls.Add(card);
            }

            // Layout helper — called on resize
            void LayoutGrid()
            {
                int n = cardList.Count;
                if (n == 0) return;
                int rows       = n > 16 ? 3 : 2;
                int colsPerRow = (int)Math.Ceiling((double)n / rows);
                int available  = grid.ClientSize.Width - 2 * PadL;
                int cardW      = Math.Max(80, (available - (colsPerRow - 1) * CardGapX) / colsPerRow);

                for (int i = 0; i < n; i++)
                {
                    int row = i / colsPerRow;
                    int col = i % colsPerRow;
                    var c   = cardList[i];
                    c.SetBounds(
                        PadL + col * (cardW + CardGapX),
                        PadT + row * (CardH + RowGapY),
                        cardW, CardH);

                    foreach (Control ctrl in c.Controls)
                        switch (ctrl.Tag as string)
                        {
                            case "badge":  ctrl.Width = Math.Max(10, cardW - 27); break;
                            case "name":   ctrl.Width = Math.Max(10, cardW - 8);  break;
                            case "sample": ctrl.Width = Math.Max(10, cardW - 8);  break;
                        }
                    c.Invalidate();
                }
            }

            container.Controls.Add(legend);
            container.Controls.Add(grid);

            container.Resize += (s, e) =>
            {
                legend.SetBounds(0, 0, container.Width, 22);
                grid.SetBounds(0, 22, container.Width, container.Height - 22);
                LayoutGrid();
            };

            var tab = new TabPage(reportName) { BackColor = PageBg };
            tab.Controls.Add(container);
            container.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(tab);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Layout
        // ════════════════════════════════════════════════════════════════════

        private void DoLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            _header.SetBounds(0, 0, w, HeaderH);
            _tabs.SetBounds(0, HeaderH, w, Math.Max(0, h - HeaderH - FooterH));
            _footer.SetBounds(0, h - FooterH, w, FooterH);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Tab drawing
        // ════════════════════════════════════════════════════════════════════

        private void DrawTabItem(object sender, DrawItemEventArgs e)
        {
            var page = _tabs.TabPages[e.Index];
            bool sel = e.Index == _tabs.SelectedIndex;

            e.Graphics.FillRectangle(
                new SolidBrush(sel ? Color.White : Color.FromArgb(240, 242, 245)),
                e.Bounds);

            if (sel)
                e.Graphics.FillRectangle(new SolidBrush(AccentBlue),
                    e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 3);

            var sf = new System.Drawing.StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(page.Text,
                new Font("Segoe UI", 9F, sel ? FontStyle.Bold : FontStyle.Regular),
                sel ? new SolidBrush(TextDark) : new SolidBrush(TextMuted),
                e.Bounds, sf);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Card building (mirrors ReportColumnSelectionDialog)
        // ════════════════════════════════════════════════════════════════════

        private static Panel BuildCard(ReportColumnDef col, Dictionary<string, CheckBox> cbMap)
        {
            bool on = col.DefaultValue;
            Color accentColor = col.FitsPortrait ? AccentBlue : Color.FromArgb(220, 160, 40);

            var card = new Panel
            {
                BackColor = on ? Color.White : Color.FromArgb(250, 251, 253),
                Cursor    = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(accentColor))
                    e.Graphics.FillRectangle(b, 0, 0, card.Width, AccentH);
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                using (var pen = new Pen(CardBdr))
                    e.Graphics.DrawLine(pen, 1, DivY, card.Width - 2, DivY);
            };

            var cb = new CheckBox
            {
                Checked  = on,
                AutoSize = false,
                Location = new Point(5, CbY),
                Size     = new Size(16, 16),
                Cursor   = Cursors.Hand
            };
            cbMap[col.ParamName] = cb;

            var badge = new Label
            {
                // Summary rows span the full page width, so a portrait/overflow verdict does not
                // apply to them — same rule as ReportColumnSelectionDialog.
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

            cb.CheckedChanged += (s, e) => ApplyCardState(card, col, cb.Checked);

            Action toggle = () => cb.Checked = !cb.Checked;
            card.Click      += (s, e) => toggle();
            badge.Click     += (s, e) => toggle();
            nameLbl.Click   += (s, e) => toggle();
            sampleLbl.Click += (s, e) => toggle();

            card.Controls.Add(cb);
            card.Controls.Add(badge);
            card.Controls.Add(nameLbl);
            card.Controls.Add(sampleLbl);
            return card;
        }

        private static void ApplyCardState(Panel card, ReportColumnDef col, bool on)
        {
            foreach (Control ctrl in card.Controls)
                switch (ctrl.Tag as string)
                {
                    case "name":   ctrl.ForeColor = on ? TextDark  : DimFore; break;
                    case "sample": ctrl.ForeColor = on ? TextMuted : DimFore; break;
                    case "badge":  ctrl.ForeColor = on ? (col.IsSummaryRow ? TextMuted : col.FitsPortrait ? GreenOk : WarnAmber) : DimFore; break;
                }
            card.BackColor = on ? Color.White : Color.FromArgb(250, 251, 253);
            card.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        //  All / None for current tab
        // ════════════════════════════════════════════════════════════════════

        private void SetAllInCurrentTab(bool value)
        {
            if (_tabs.SelectedTab == null) return;
            string name = _tabs.SelectedTab.Text;
            if (!_checkboxes.TryGetValue(name, out var cbMap)) return;
            foreach (var cb in cbMap.Values) cb.Checked = value;
        }

        // ════════════════════════════════════════════════════════════════════
        //  OK handler
        // ════════════════════════════════════════════════════════════════════

        private void OnOk(object sender, EventArgs e)
        {
            SelectedParameters = new Dictionary<string, ReportParameter[]>();

            foreach (var (reportName, columns) in _reports)
            {
                var cbMap = _checkboxes[reportName];
                SelectedParameters[reportName] = columns
                    .Select(col => new ReportParameter(
                        col.ParamName,
                        cbMap.TryGetValue(col.ParamName, out var cb)
                            ? cb.Checked.ToString()
                            : col.DefaultValue.ToString()))
                    .ToArray();
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        // ════════════════════════════════════════════════════════════════════
        //  Static entry point
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows the combined column selection dialog.
        /// Returns a map of reportName → ReportParameter[], or null if cancelled.
        /// </summary>
        public static Dictionary<string, ReportParameter[]> Show(
            IWin32Window owner,
            IEnumerable<(string ReportName, IEnumerable<ReportColumnDef> Columns)> reports)
        {
            using (var dlg = new CombinedColumnSelectionDialog(reports))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;
                return dlg.SelectedParameters;
            }
        }
    }
}
