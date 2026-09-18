using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Pages.RequestPortal
{
    internal class RequestDetailDialog : Form
    {
        private Panel _scroll;

        // ── Palette ───────────────────────────────────────────────────────────
        private static readonly Color CBlue    = Color.FromArgb(78, 154, 252);
        private static readonly Color CPage    = Color.FromArgb(247, 249, 252);
        private static readonly Color CLabel   = Color.FromArgb(110, 118, 140);
        private static readonly Color CValue   = Color.FromArgb(20, 26, 46);
        private static readonly Color CDivider = Color.FromArgb(224, 228, 238);
        private static readonly Color CCard    = Color.White;

        // ── Layout ────────────────────────────────────────────────────────────
        private const int LabelColW = 190;
        private const int BannerH   = 54;
        private const int SectionH  = 32;
        private const int RowH      = 36;
        private const int DivH      = 1;
        private const int GapH      = 10;

        // ── Tour targets ──────────────────────────────────────────────────────
        public Control TourTarget_StatusBanner   { get; private set; }
        public Control TourTarget_RequestDetails { get; private set; }
        public Control TourTarget_RequesterInfo  { get; private set; }
        public Control TourTarget_Destination    { get; private set; }
        public Button  TourTarget_CloseButton   { get; private set; }

        public RequestDetailDialog(List<PortalRequestStatusDto> items)
        {
            var first = items[0];
            bool isMulti = items.Count > 1;

            string setCode = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.SetCode))?.SetCode
                             ?? "No Set Assigned";
            string aggregatedStatus = AggregateStatus(items);

            Text            = $"Request Details — {setCode}";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Width           = 980;
            Height          = 780;
            BackColor       = CPage;
            Font            = new Font("Segoe UI", 9.5F);

            _scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = CPage
            };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = CCard };
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = CDivider });

            TourTarget_CloseButton = new Button
            {
                Text      = "Close",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = CBlue,
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(100, 34),
                Cursor    = Cursors.Hand
            };
            TourTarget_CloseButton.FlatAppearance.BorderSize         = 0;
            TourTarget_CloseButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 130, 220);
            TourTarget_CloseButton.Click += (s, e) => { DialogResult = DialogResult.OK; };
            footer.Resize += (s, e) => TourTarget_CloseButton.Location = new Point(footer.Width - 120, 10);
            footer.Controls.Add(TourTarget_CloseButton);

            Controls.Add(_scroll);
            Controls.Add(footer);

            Render(items, first, isMulti, setCode, aggregatedStatus);
        }

        // ── Renderer ──────────────────────────────────────────────────────────

        private void Render(List<PortalRequestStatusDto> items, PortalRequestStatusDto first,
            bool isMulti, string setCode, string aggregatedStatus)
        {
            _scroll.SuspendLayout();
            _scroll.Controls.Clear();

            int contentWidth = Width - 48 - SystemInformation.VerticalScrollBarWidth - 2;

            // Each logical section is an independent Control so the tour can spotlight them.
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                Dock          = DockStyle.Top,
                BackColor     = CPage,
                Padding       = new Padding(24, 20, 24, 28)
            };

            // ── Status Banner ─────────────────────────────────────────────────
            TourTarget_StatusBanner = BuildStatusBannerPanel(setCode, contentWidth);
            TourTarget_StatusBanner.Margin = new Padding(0, 0, 0, 12);
            flow.Controls.Add(TourTarget_StatusBanner);

            // ── Request Details section ───────────────────────────────────────
            string cartridgeName = !string.IsNullOrWhiteSpace(first.CartridgeName)
                ? first.CartridgeName : first.ItemName;

            TourTarget_RequestDetails = BuildSectionTbl(contentWidth, (tbl, r) =>
            {
                AddSection(tbl, "Request Details", ref r, first: true);
                AddRow(tbl, "Status", aggregatedStatus ?? "—", ref r,
                       statusColor: GetStatusColor(aggregatedStatus));
                if (isMulti)
                {
                    int tableH = 28 + (items.Count * 26) + 46;
                    AddSpanning(tbl, BuildModelsTable(items, contentWidth), ref r, tableH);
                }
                else
                {
                    AddRow(tbl, "Cartridge Model",               cartridgeName ?? "—",             ref r, bold: true);
                    AddRow(tbl, "Quantity",                      first.Quantity.ToString(),         ref r, bold: false);
                    AddRow(tbl, "Returned Cartridges (Good)",    first.GoodEmptyQty.ToString(),    ref r, bold: false);
                    AddRow(tbl, "Returned Cartridges (Damaged)", first.DamagedEmptyQty.ToString(), ref r, bold: false);
                }
            });
            TourTarget_RequestDetails.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(TourTarget_RequestDetails);

            // ── Requester Information section ─────────────────────────────────
            TourTarget_RequesterInfo = BuildSectionTbl(contentWidth, (tbl, r) =>
            {
                AddSection(tbl, "Requester Information", ref r);
                AddRow(tbl, "Name",     first.DestinationEmployeeName     ?? "—", ref r);
                AddRow(tbl, "Position", first.DestinationEmployeePosition ?? "—", ref r);
            });
            TourTarget_RequesterInfo.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(TourTarget_RequesterInfo);

            // ── Destination & Fulfillment section ─────────────────────────────
            string receivedBy = items.Select(x => x.ReceivedByName)
                                     .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

            TourTarget_Destination = BuildSectionTbl(contentWidth, (tbl, r) =>
            {
                AddSection(tbl, "Destination & Fulfillment", ref r);
                AddRow(tbl, "Company",            first.DestinationCompany    ?? "—", ref r);
                AddRow(tbl, "Branch",             first.DestinationBranch     ?? "—", ref r);
                AddRow(tbl, "Department",         first.DestinationDepartment ?? "—", ref r);
                AddRow(tbl, "Fulfillment Method", first.FulfillmentMethod     ?? "—", ref r,
                       statusColor: Color.FromArgb(41, 128, 185));
                if (!string.IsNullOrWhiteSpace(receivedBy))
                    AddRow(tbl, "Received By", receivedBy, ref r);
            });
            TourTarget_Destination.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(TourTarget_Destination);

            // ── Additional Remarks (conditional) ─────────────────────────────
            string remarks = string.Join("\n\n", items
                .Where(x => !string.IsNullOrWhiteSpace(x.FullRemarks))
                .Select(x => x.FullRemarks)
                .Distinct());

            if (!string.IsNullOrWhiteSpace(remarks))
            {
                var remarksTbl = BuildSectionTbl(contentWidth, (tbl, r) =>
                {
                    AddSection(tbl, "Additional Remarks", ref r);
                    var lbl = new Label
                    {
                        Text        = remarks,
                        Font        = new Font("Segoe UI", 9.5F),
                        ForeColor   = CValue,
                        AutoSize    = true,
                        MaximumSize = new Size(contentWidth - LabelColW, 0),
                        Padding     = new Padding(0, 4, 0, 8)
                    };
                    tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tbl.Controls.Add(lbl, 0, r);
                    tbl.SetColumnSpan(lbl, 2);
                    r++;
                });
                flow.Controls.Add(remarksTbl);
            }

            _scroll.Controls.Add(flow);
            _scroll.ResumeLayout(true);
        }

        // ── Section table builder ─────────────────────────────────────────────

        private static TableLayoutPanel BuildSectionTbl(int contentWidth,
            Action<TableLayoutPanel, int> populate)
        {
            var tbl = new TableLayoutPanel
            {
                ColumnCount  = 2,
                RowCount     = 0,
                GrowStyle    = TableLayoutPanelGrowStyle.AddRows,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width        = contentWidth,
                // MinimumSize prevents AutoSize from shrinking the table below contentWidth.
                // Without this, GrowAndShrink can auto-calculate a narrower width, causing
                // the SizeType.Percent value column to get less space than intended and
                // AutoEllipsis to kick in even though there is room on screen.
                MinimumSize  = new Size(contentWidth, 0),
                BackColor    = CPage
            };
            tbl.ColumnStyles.Clear();
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelColW));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            populate(tbl, 0);
            return tbl;
        }

        // ── Status banner panel ───────────────────────────────────────────────

        private static Panel BuildStatusBannerPanel(string setCode, int contentWidth)
        {
            var banner = new Panel { Width = contentWidth, Height = BannerH, BackColor = CCard };
            banner.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.FillRectangle(new SolidBrush(CCard), banner.ClientRectangle);
                using (var pen = new Pen(CDivider, 1))
                    g.DrawRectangle(pen, 0, 0, banner.Width - 1, banner.Height - 1);
                g.FillRectangle(new SolidBrush(CBlue), 0, 0, 5, banner.Height);
            };

            var lblTitle = new Label
            {
                Text      = $"Request  {setCode}",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = CValue,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(20, 0, 0, 0)
            };

            banner.Controls.Add(lblTitle);
            return banner;
        }

        // ── Multi-model table ──────────────────────────────────────────────────

        private static Panel BuildModelsTable(List<PortalRequestStatusDto> items, int contentWidth)
        {
            var panel = new Panel { BackColor = CPage, Dock = DockStyle.Fill };

            int tableWidth = Math.Min(620, contentWidth - LabelColW);
            int col0 = (int)(tableWidth * 0.50);
            int col1 = (int)(tableWidth * 0.17);
            int col2 = (int)(tableWidth * 0.17);
            int col3 = tableWidth - col0 - col1 - col2;

            int y  = 4;
            int hx = 0;
            foreach (var (header, w) in new[]
            {
                ("Cartridge Model", col0), ("Qty", col1),
                ("Returned (Good)", col2), ("Returned (Damaged)", col3)
            })
            {
                panel.Controls.Add(new Label
                {
                    Text      = header,
                    Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = CLabel,
                    Location  = new Point(hx, y),
                    Width     = w,
                    AutoSize  = false
                });
                hx += w;
            }
            y += 22;

            panel.Controls.Add(new Panel { Location = new Point(0, y), Width = tableWidth, Height = 1, BackColor = CDivider });
            y += 6;

            foreach (var item in items)
            {
                string name = !string.IsNullOrWhiteSpace(item.CartridgeName) ? item.CartridgeName : item.ItemName;
                int rx = 0;
                foreach (var (text, w, isBold) in new (string, int, bool)[]
                {
                    (name,                           col0, true),
                    (item.Quantity.ToString(),        col1, false),
                    (item.GoodEmptyQty.ToString(),    col2, false),
                    (item.DamagedEmptyQty.ToString(), col3, false)
                })
                {
                    panel.Controls.Add(new Label
                    {
                        Text      = text,
                        Font      = new Font("Segoe UI", 9.5F, isBold ? FontStyle.Bold : FontStyle.Regular),
                        ForeColor = CValue,
                        Location  = new Point(rx, y + 3),
                        Width     = w,
                        AutoSize  = false
                    });
                    rx += w;
                }
                y += 26;
            }

            panel.Controls.Add(new Panel { Location = new Point(0, y), Width = tableWidth, Height = 1, BackColor = CDivider });
            y += 10;

            int tx = col0;
            panel.Controls.Add(new Label
            {
                Text      = "Total",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = CLabel,
                Location  = new Point(0, y + 3),
                AutoSize  = true
            });
            foreach (var (total, w) in new[]
            {
                (items.Sum(x => x.Quantity).ToString(),        col1),
                (items.Sum(x => x.GoodEmptyQty).ToString(),    col2),
                (items.Sum(x => x.DamagedEmptyQty).ToString(), col3)
            })
            {
                panel.Controls.Add(new Label
                {
                    Text      = total,
                    Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = CValue,
                    Location  = new Point(tx, y + 3),
                    Width     = w,
                    AutoSize  = false
                });
                tx += w;
            }

            return panel;
        }

        // ── TableLayoutPanel helpers ──────────────────────────────────────────

        private static void AddSpanning(TableLayoutPanel tbl, Control ctrl, ref int row, int height)
        {
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            tbl.Controls.Add(ctrl, 0, row);
            tbl.SetColumnSpan(ctrl, 2);
            row++;
        }

        private static void AddSection(TableLayoutPanel tbl, string title, ref int row, bool first = false)
        {
            if (!first)
            {
                var gap = new Panel { BackColor = CPage };
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, GapH));
                tbl.Controls.Add(gap, 0, row);
                tbl.SetColumnSpan(gap, 2);
                row++;
            }

            var heading = new Label
            {
                Text      = title.ToUpper(),
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = CBlue,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Margin    = new Padding(0, first ? 14 : 10, 0, 0)
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, SectionH));
            tbl.Controls.Add(heading, 0, row);
            tbl.SetColumnSpan(heading, 2);
            row++;

            var sep = new Panel { Dock = DockStyle.Fill, BackColor = CDivider };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, DivH));
            tbl.Controls.Add(sep, 0, row);
            tbl.SetColumnSpan(sep, 2);
            row++;
        }

        private static void AddRow(TableLayoutPanel tbl, string label, string value, ref int row,
            bool bold = true, Color? statusColor = null)
        {
            var lblCtrl = new Label
            {
                Text      = label,
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = CLabel,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin    = new Padding(0, 0, 12, 0)
            };
            var valCtrl = new Label
            {
                Text         = value ?? "—",
                Font         = new Font("Segoe UI", 9.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor    = statusColor ?? CValue,
                Dock         = DockStyle.Fill,
                TextAlign    = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, RowH));
            tbl.Controls.Add(lblCtrl, 0, row);
            tbl.Controls.Add(valCtrl, 1, row);
            row++;
        }

        public void ScrollIntoView(Control ctrl) => _scroll?.ScrollControlIntoView(ctrl);

        /// <summary>
        /// Called by the tour service when this dialog enters guided-tour mode.
        ///
        /// We intentionally do NOT disable controls, set Enabled=false, or touch ControlBox.
        /// Setting _scroll.Enabled=false cascades the disabled (gray) visual state through
        /// every child control, making the dialog unreadable and breaking ScrollControlIntoView
        /// (which each tour step's OnEnter relies on to position controls into view before
        /// the spotlight coordinates are sampled).
        ///
        /// Interaction blocking is handled entirely by the tour service's FormClosing guard
        /// (OnTourDialogFormClosing), which cancels e.Cancel on every close attempt — the
        /// title-bar X, Alt+F4, and the in-dialog Close button's Close() call — while
        /// DialogStepsActive is true.  No visual change to the dialog is needed.
        /// </summary>
        public void EnterTourMode()
        {
            // Change the Close button cursor to "not-allowed" so users get a clear visual
            // signal that the dialog cannot be dismissed during the tour.  The button is
            // NOT disabled (no gray cascade) — the FormClosing guard in WpfPortalTourService
            // is the actual blocker; this is purely a UX hint.
            if (TourTarget_CloseButton != null)
                TourTarget_CloseButton.Cursor = Cursors.No;
        }

        public void ExitTourMode()
        {
            if (TourTarget_CloseButton != null)
                TourTarget_CloseButton.Cursor = Cursors.Hand;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string AggregateStatus(List<PortalRequestStatusDto> items)
        {
            var statuses = items.Select(x => x.Status?.ToUpper() ?? "").Distinct().ToList();
            if (statuses.Count == 1) return items[0].Status;
            string[] priority = { "SUBMITTED", "UNDER REVIEW", "RECEIVED", "REPLACED", "FULFILLED", "COMPLETED", "CANCELLED", "REJECTED" };
            foreach (var p in priority)
                if (statuses.Contains(p)) return p;
            return items[0].Status;
        }

        private static Color GetStatusColor(string status)
        {
            switch (status?.ToUpper())
            {
                case "SUBMITTED":
                case "UNDER REVIEW": return Color.FromArgb(0, 123, 255);
                case "RECEIVED":     return Color.FromArgb(230, 126, 34);
                case "FULFILLED":
                case "REPLACED":
                case "COMPLETED":    return Color.FromArgb(39, 174, 96);
                case "CANCELLED":
                case "REJECTED":     return Color.FromArgb(192, 57, 43);
                default:             return Color.FromArgb(100, 100, 100);
            }
        }
    }
}
