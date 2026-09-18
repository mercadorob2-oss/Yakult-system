using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Dialog shown before generating the Disposed &amp; Sold Items Report.
    /// Lets the user choose a date range and whether to include All, Disposed-only, or Sold-only records.
    /// </summary>
    public class DisposedSoldReportFilterDialog : Form
    {
        /// <summary>"All", "DISPOSE", or "SELL"</summary>
        public string    SelectedFilter { get; private set; } = "All";
        public DateTime  FromDate       { get; private set; }
        public DateTime  ToDate         { get; private set; }

        private readonly RadioButton _rdoAll;
        private readonly RadioButton _rdoDisposed;
        private readonly RadioButton _rdoSold;

        private bool _handlingRadio;

        private Panel _pnlAllOuter,      _pnlAllInner;
        private Panel _pnlDisposedOuter, _pnlDisposedInner;
        private Panel _pnlSoldOuter,     _pnlSoldInner;

        private const int FormW   = 520;
        private const int CardH   = 84;
        private const int CardGap = 10;
        private const int CardX   = 16;
        private const int CardW   = FormW - CardX * 2; // 468

        public DisposedSoldReportFilterDialog()
        {
            Text            = "Generate Disposed & Sold Report";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new Font("Segoe UI", 9.5f);
            BackColor       = Color.FromArgb(245, 247, 250);

            // ── Header ──────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                BackColor = Color.FromArgb(58, 134, 232),
                Dock      = DockStyle.Top,
                Height    = 60
            };
            pnlHeader.Controls.Add(new Label
            {
                Text        = "Generate Disposed & Sold Report",
                UseMnemonic = false,
                Font        = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor   = Color.White,
                AutoSize    = false,
                Size        = new Size(FormW - 20, 34),
                Location    = new Point(18, 13),
                TextAlign   = ContentAlignment.MiddleLeft
            });

            // ── Date range row ───────────────────────────────────────────────
            var pnlDates = new Panel
            {
                BackColor = Color.White,
                Location  = new Point(0, 60),
                Size      = new Size(FormW, 52)
            };

            var dtpFrom = new DateTimePicker
            {
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Width        = 140,
                Location     = new Point(90, 14),
                Value        = new DateTime(DateTime.Today.Year, 1, 1)
            };
            var dtpTo = new DateTimePicker
            {
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Width        = 140,
                Location     = new Point(310, 14),
                Value        = DateTime.Today
            };

            pnlDates.Controls.Add(new Label
            {
                Text      = "From:",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize  = true,
                Location  = new Point(16, 17)
            });
            pnlDates.Controls.Add(dtpFrom);
            pnlDates.Controls.Add(new Label
            {
                Text      = "To:",
                Font      = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize  = true,
                Location  = new Point(246, 17)
            });
            pnlDates.Controls.Add(dtpTo);

            // Separator line under date row
            pnlDates.Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(218, 220, 224),
                Dock      = DockStyle.Bottom,
                Height    = 1
            });

            // ── Radio buttons ────────────────────────────────────────────────
            _rdoAll      = new RadioButton { Checked = true };
            _rdoDisposed = new RadioButton();
            _rdoSold     = new RadioButton();

            // ── Option cards ─────────────────────────────────────────────────
            int firstCardY = 128;

            BuildCard(firstCardY,
                      "All (Disposed & Sold)",
                      "Show all records regardless of type",
                      _rdoAll, out _pnlAllOuter, out _pnlAllInner);

            BuildCard(firstCardY + (CardH + CardGap),
                      "Disposed Only",
                      "Only cartridges that have been sent for disposal",
                      _rdoDisposed, out _pnlDisposedOuter, out _pnlDisposedInner);

            BuildCard(firstCardY + (CardH + CardGap) * 2,
                      "Sold Only",
                      "Only cartridges that have been sold",
                      _rdoSold, out _pnlSoldOuter, out _pnlSoldInner);

            _rdoAll.CheckedChanged      += (s, e) => SetFilter(_rdoAll);
            _rdoDisposed.CheckedChanged += (s, e) => SetFilter(_rdoDisposed);
            _rdoSold.CheckedChanged     += (s, e) => SetFilter(_rdoSold);
            UpdateHighlights();

            // ── Bottom bar ───────────────────────────────────────────────────
            int bottomBarY = firstCardY + (CardH + CardGap) * 3 - CardGap + 14;

            var pnlBottom = new Panel
            {
                BackColor = Color.White,
                Location  = new Point(0, bottomBarY),
                Size      = new Size(FormW, 54)
            };
            pnlBottom.Controls.Add(new Panel
            {
                BackColor = Color.FromArgb(218, 220, 224),
                Dock      = DockStyle.Top,
                Height    = 1
            });

            var btnCancel = new Button
            {
                Text         = "Cancel",
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(14, 0, 14, 0),
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Segoe UI", 9.5f),
                ForeColor    = Color.FromArgb(70, 70, 70),
                Cursor       = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 202, 206);

            var btnGenerate = new Button
            {
                Text         = "Generate Report",
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(16, 0, 16, 0),
                BackColor    = Color.FromArgb(34, 160, 84),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor       = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += (s, e) =>
            {
                if (_rdoDisposed.Checked)      SelectedFilter = "DISPOSE";
                else if (_rdoSold.Checked)     SelectedFilter = "SELL";
                else                           SelectedFilter = "All";

                FromDate = dtpFrom.Value.Date;
                ToDate   = dtpTo.Value.Date;
            };

            pnlBottom.Controls.Add(btnGenerate);
            pnlBottom.Controls.Add(btnCancel);

            pnlBottom.Layout += (s, e) =>
            {
                int rightEdge  = pnlBottom.ClientSize.Width - 16;
                btnCancel.Height   = 32;
                btnGenerate.Height = 32;
                btnCancel.Top      = 11;
                btnGenerate.Top    = 11;
                btnCancel.Left     = rightEdge - btnCancel.Width;
                btnGenerate.Left   = btnCancel.Left - 8 - btnGenerate.Width;
            };

            ClientSize   = new Size(FormW, bottomBarY + 54);
            AcceptButton = btnGenerate;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                pnlHeader,
                pnlDates,
                _pnlAllOuter, _pnlDisposedOuter, _pnlSoldOuter,
                pnlBottom
            });
        }

        private void BuildCard(int y, string title, string subtitle, RadioButton radio,
                               out Panel outerPanel, out Panel innerPanel)
        {
            outerPanel = new Panel
            {
                Location = new Point(CardX, y),
                Size     = new Size(CardW, CardH),
                Cursor   = Cursors.Hand
            };

            innerPanel = new Panel
            {
                Location  = new Point(1, 1),
                Size      = new Size(CardW - 2, CardH - 2),
                BackColor = Color.White,
                Cursor    = Cursors.Hand
            };

            radio.AutoSize = false;
            radio.Size     = new Size(18, 18);
            radio.Location = new Point(14, (CardH - 18) / 2 - 1);
            radio.Cursor   = Cursors.Hand;
            radio.TabStop  = true;

            var lblTitle = new Label
            {
                Text        = title,
                Font        = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location    = new Point(42, 10),
                AutoSize    = false,
                Size        = new Size(CardW - 58, 22),
                ForeColor   = Color.FromArgb(18, 18, 18),
                Cursor      = Cursors.Hand,
                UseMnemonic = false
            };

            var lblSub = new Label
            {
                Text        = subtitle,
                Font        = new Font("Segoe UI", 8.5f),
                Location    = new Point(42, 34),
                AutoSize    = true,
                MaximumSize = new Size(CardW - 58, 0),
                ForeColor   = Color.FromArgb(115, 115, 115),
                Cursor      = Cursors.Hand,
                UseMnemonic = false
            };

            innerPanel.Controls.AddRange(new Control[] { radio, lblTitle, lblSub });
            outerPanel.Controls.Add(innerPanel);

            Action select = () => radio.Checked = true;
            outerPanel.Click += (s, e) => select();
            innerPanel.Click += (s, e) => select();
            lblTitle.Click   += (s, e) => select();
            lblSub.Click     += (s, e) => select();
        }

        private void SetFilter(RadioButton selected)
        {
            if (_handlingRadio) return;
            _handlingRadio = true;
            try
            {
                _rdoAll.Checked      = (selected == _rdoAll);
                _rdoDisposed.Checked = (selected == _rdoDisposed);
                _rdoSold.Checked     = (selected == _rdoSold);
            }
            finally
            {
                _handlingRadio = false;
            }
            UpdateHighlights();
        }

        private void UpdateHighlights()
        {
            SetHighlight(_pnlAllOuter,      _pnlAllInner,      _rdoAll.Checked);
            SetHighlight(_pnlDisposedOuter, _pnlDisposedInner, _rdoDisposed.Checked);
            SetHighlight(_pnlSoldOuter,     _pnlSoldInner,     _rdoSold.Checked);
        }

        private static void SetHighlight(Panel outer, Panel inner, bool selected)
        {
            outer.BackColor = selected
                ? Color.FromArgb(59, 130, 246)
                : Color.FromArgb(214, 216, 220);

            inner.BackColor = selected
                ? Color.FromArgb(239, 246, 255)
                : Color.White;
        }
    }
}
