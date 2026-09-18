using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.RequestPortal
{
    public class AuthorizationDetailDialog : Form
    {
        private readonly int _authorizationId;
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
        public Control TourTarget_StatusBanner    { get; private set; }
        public Control TourTarget_AuthDetails     { get; private set; }
        public Control TourTarget_CartridgeModels { get; private set; }
        public Control TourTarget_Employee        { get; private set; }
        public Button  TourTarget_CloseButton    { get; private set; }

        /// <summary>Standard constructor — loads data from the database.</summary>
        public AuthorizationDetailDialog(int authorizationId)
        {
            _authorizationId = authorizationId;
            BuildShell();
            Load += async (s, e) => await LoadAndRenderAsync();
        }

        /// <summary>Tour constructor — renders immediately with a pre-built model (no DB call).</summary>
        public AuthorizationDetailDialog(CartridgeAuthorizationModel model)
        {
            _authorizationId = model.AuthorizationId;
            BuildShell();
            Load += (s, e) => Render(model);
        }

        // ── Shell ─────────────────────────────────────────────────────────────

        private void BuildShell()
        {
            Text            = $"Authorization #{_authorizationId}";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Width           = 900;
            Height          = 700;
            BackColor       = CPage;
            Font            = new Font("Segoe UI", 9.5F);

            _scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = CPage
            };
            _scroll.Controls.Add(new Label
            {
                Text      = "Loading details…",
                Font      = new Font("Segoe UI", 10F),
                ForeColor = CLabel,
                Dock      = DockStyle.Top,
                Height    = 60,
                TextAlign = ContentAlignment.MiddleCenter
            });

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
            footer.Resize  += (s, e) => TourTarget_CloseButton.Location = new Point(footer.Width - 120, 10);
            footer.Controls.Add(TourTarget_CloseButton);

            Controls.Add(_scroll);
            Controls.Add(footer);
        }

        // ── Data ──────────────────────────────────────────────────────────────

        private async Task LoadAndRenderAsync()
        {
            CartridgeAuthorizationModel rec = null;
            try
            {
                rec = await new CartridgeAuthorizationRepository().GetDetailByIdAsync(_authorizationId);
            }
            catch (Exception ex)
            {
                ShowMsg($"Could not load record: {ex.Message}", error: true);
                return;
            }

            if (rec == null) { ShowMsg($"Authorization #{_authorizationId} not found.", error: true); return; }

            if (InvokeRequired) Invoke(new Action(() => Render(rec)));
            else Render(rec);
        }

        private void ShowMsg(string text, bool error)
        {
            if (InvokeRequired) { Invoke(new Action(() => ShowMsg(text, error))); return; }
            _scroll.Controls.Clear();
            _scroll.Controls.Add(new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = error ? Color.FromArgb(180, 40, 40) : CLabel,
                Dock      = DockStyle.Top,
                Height    = 60,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        // ── Renderer ──────────────────────────────────────────────────────────

        private void Render(CartridgeAuthorizationModel r)
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
            TourTarget_StatusBanner = BuildStatusBannerPanel(r, contentWidth);
            TourTarget_StatusBanner.Margin = new Padding(0, 0, 0, 12);
            flow.Controls.Add(TourTarget_StatusBanner);

            // ── Authorization Details section ─────────────────────────────────
            TourTarget_AuthDetails = BuildSectionTbl(contentWidth, (tbl, row) =>
            {
                AddSection(tbl, "Authorization Details", ref row, first: true);
                AddRow(tbl, "Status",    r.Status ?? "—", ref row, statusColor: StatusFg(r.Status));
                AddRow(tbl, "Signed By", r.SignedByName ?? "—", ref row);
                AddRow(tbl, "Signed Date", r.SignedDate.HasValue
                    ? r.SignedDate.Value.ToString("MMMM d, yyyy   h:mm tt") : "—", ref row);
                AddRow(tbl, "Submitted", r.CreatedDate.ToString("MMMM d, yyyy   h:mm tt"), ref row);
                if (!string.IsNullOrWhiteSpace(r.FulfillmentMethod))
                {
                    AddRow(tbl, "Distribution Method", r.FulfillmentMethod, ref row);
                    if (r.FulfillmentMethod.Equals("Pickup", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(r.ReceivedByName))
                        AddRow(tbl, "To Be Received By", r.ReceivedByName, ref row);
                }
            });
            TourTarget_AuthDetails.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(TourTarget_AuthDetails);

            // ── Cartridge Models section ──────────────────────────────────────
            var models = ParseModels(r.RequestedModels);
            TourTarget_CartridgeModels = BuildSectionTbl(contentWidth, (tbl, row) =>
            {
                AddSection(tbl, "Cartridge Models Requested", ref row);
                if (models.Count == 0)
                {
                    AddRow(tbl, "Models", "—", ref row, bold: false);
                }
                else
                {
                    foreach (var m in models)
                    {
                        string detail = m.Good > 0 || m.Damaged > 0
                            ? $"Qty ×{m.Qty}    Returns: {m.Good} good, {m.Damaged} damaged"
                            : $"Qty ×{m.Qty}";
                        AddRow(tbl, m.Model, detail, ref row, bold: false);
                    }
                }
            });
            TourTarget_CartridgeModels.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(TourTarget_CartridgeModels);

            // ── Employee section ──────────────────────────────────────────────
            TourTarget_Employee = BuildSectionTbl(contentWidth, (tbl, row) =>
            {
                AddSection(tbl, "Employee", ref row);
                AddRow(tbl, "Name",       r.EmployeeName     ?? "—", ref row);
                AddRow(tbl, "Position",   r.EmployeePosition ?? "—", ref row);
                AddRow(tbl, "Department", r.DepartmentName   ?? "—", ref row);
                AddRow(tbl, "Branch",     r.BranchName       ?? "—", ref row);
                AddRow(tbl, "Company",    r.CompanyName      ?? "—", ref row);
            });
            flow.Controls.Add(TourTarget_Employee);

            _scroll.Controls.Add(flow);
            _scroll.ResumeLayout(true);
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
            if (TourTarget_CloseButton != null)
                TourTarget_CloseButton.Cursor = Cursors.No;
        }

        public void ExitTourMode()
        {
            if (TourTarget_CloseButton != null)
                TourTarget_CloseButton.Cursor = Cursors.Hand;
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

        private static Panel BuildStatusBannerPanel(CartridgeAuthorizationModel r, int contentWidth)
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

            var lblId = new Label
            {
                Text      = $"Authorization  #{r.AuthorizationId}",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = CValue,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(20, 0, 0, 0)
            };

            banner.Controls.Add(lblId);
            return banner;
        }

        // ── TableLayoutPanel helpers ──────────────────────────────────────────

        // Adds a control that spans both columns at a fixed row height.
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
                // Gap row between sections
                var gap = new Panel { BackColor = CPage };
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, GapH));
                tbl.Controls.Add(gap, 0, row);
                tbl.SetColumnSpan(gap, 2);
                row++;
            }

            // Section heading — text sits at the bottom of its row
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

            // Divider line
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Color StatusBg(string status)
        {
            switch ((status ?? "").ToUpper())
            {
                case "APPROVED":                   return Color.FromArgb(220, 248, 228);
                case "PENDING": case "UNDER REVIEW": return Color.FromArgb(255, 243, 215);
                case "REJECTED":                   return Color.FromArgb(255, 228, 228);
                case "CANCELLED":                  return Color.FromArgb(240, 240, 240);
                default:                           return Color.FromArgb(232, 238, 255);
            }
        }

        private static Color StatusFg(string status)
        {
            switch ((status ?? "").ToUpper())
            {
                case "APPROVED":                   return Color.FromArgb(25, 118, 50);
                case "PENDING": case "UNDER REVIEW": return Color.FromArgb(148, 96, 0);
                case "REJECTED":                   return Color.FromArgb(180, 32, 32);
                case "CANCELLED":                  return Color.FromArgb(108, 108, 108);
                default:                           return Color.FromArgb(55, 78, 160);
            }
        }

        private static List<(string Model, int Qty, int Good, int Damaged)> ParseModels(string json)
        {
            var result = new List<(string, int, int, int)>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        string model   = el.TryGetProperty("model",   out var m) ? m.GetString() ?? "—" : "—";
                        int    qty     = el.TryGetProperty("qty",     out var q) && q.TryGetInt32(out int qi) ? qi : 0;
                        int    good    = el.TryGetProperty("good",    out var g) && g.TryGetInt32(out int gi) ? gi : 0;
                        int    damaged = el.TryGetProperty("damaged", out var d) && d.TryGetInt32(out int di) ? di : 0;
                        result.Add((model, qty, good, damaged));
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
