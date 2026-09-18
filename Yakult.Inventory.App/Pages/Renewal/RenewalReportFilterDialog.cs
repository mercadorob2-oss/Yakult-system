using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Renewal
{
    /// <summary>
    /// Dialog shown before generating the Renewals Report.
    /// Lets the user choose whether to include Active, Expired, or both, and (when
    /// <paramref name="showChainOptions"/> is enabled) how the per-set "Original" /
    /// "Renewal #N" chain labels are shown.
    /// </summary>
    public class RenewalReportFilterDialog : Form
    {
        /// <summary>"Both", "Active", or "Expired"</summary>
        public string SelectedFilter { get; private set; } = "Both";

        /// <summary>Whether the "Original" / "Renewal #N" banner is shown above each set's table.</summary>
        public bool ShowChainLabels { get; private set; } = true;

        /// <summary>False = Original first, ascending to the latest renewal (default). True = reversed.</summary>
        public bool ChainOrderDescending { get; private set; } = false;

        /// <summary>
        /// True = report only the rows the user ticked, WITHOUT pulling in the rest of each row's
        /// renewal chain. Only offered when the caller passes a non-zero selection count.
        /// </summary>
        public bool PrintSelectedOnly { get; private set; } = false;

        private readonly bool _showExpiryFilter;
        private readonly bool _showChainOptions;
        private readonly int  _selectedCount;

        private CheckBox _chkSelectedOnly;

        private readonly RadioButton _rdoBoth;
        private readonly RadioButton _rdoActive;
        private readonly RadioButton _rdoExpired;

        private CheckBox _chkShowChainLabels;
        private RadioButton _rdoOriginalFirst;
        private RadioButton _rdoRenewalFirst;

        private Panel _pnlBothOuter,    _pnlBothInner;
        private Panel _pnlActiveOuter,  _pnlActiveInner;
        private Panel _pnlExpiredOuter, _pnlExpiredInner;

        private bool _handlingRadio;

        // The application's standard accent blue (#3498DB) — matches the signatory picker and the
        // column-selection dialogs.
        private static readonly Color AccentBlue = Color.FromArgb(52, 152, 219);
        // Light tint of AccentBlue, for the subtitle on the coloured header bar.
        private static readonly Color OnAccentMuted = Color.FromArgb(214, 234, 248);

        // This dialog is hand-built with no designer, so it never got AutoScaleMode and nothing
        // scales itself: at 150% display scaling the header clipped its title and the checkbox text
        // ran off the form. Every fixed size below therefore goes through S().
        private readonly float _ui = 1f;
        private int S(int v) => (int)Math.Round(v * _ui);

        // Base sizes at 100% scaling. Card width = form client width minus left+right margins.
        private const int BaseFormW   = 500;
        private const int BaseCardH   = 84;
        private const int BaseCardGap = 10;
        private const int BaseCardX   = 16;

        private readonly int FormW;
        private readonly int CardH;
        private readonly int CardGap;
        private readonly int CardX;
        private readonly int CardW;

        /// <param name="selectedCount">
        /// How many rows the user ticked. When greater than zero (and chain options are shown) a
        /// "Print selected only" choice is offered, letting the report skip chain expansion.
        /// </param>
        public RenewalReportFilterDialog(bool showExpiryFilter = true, bool showChainOptions = false,
                                         int selectedCount = 0)
        {
            _showExpiryFilter = showExpiryFilter;
            _showChainOptions = showChainOptions;
            _selectedCount    = selectedCount;

            using (var g = CreateGraphics())
                _ui = Math.Max(1f, g.DpiX / 96f);

            FormW   = S(BaseFormW);
            CardH   = S(BaseCardH);
            CardGap = S(BaseCardGap);
            CardX   = S(BaseCardX);
            CardW   = FormW - CardX * 2;

            Text            = "Generate Renewals Report";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new Font("Segoe UI", 9.5f);
            BackColor       = Color.FromArgb(245, 247, 250);

            // ── Header ──────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                BackColor = AccentBlue,
                Dock      = DockStyle.Top,
                Height    = S(78)
            };

            var lblHeaderTitle = new Label
            {
                Text      = _showExpiryFilter ? "Generate Renewals Report" : "Chain Display Options",
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Size      = new Size(FormW - S(20), S(30)),
                Location  = new Point(S(18), S(12)),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblHeaderSub = new Label
            {
                Text      = _showExpiryFilter
                    ? "Select which records to include in the report"
                    : "Choose how the Original / Renewal chain is labeled and ordered",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = OnAccentMuted,
                AutoSize  = false,
                Size      = new Size(FormW - S(20), S(22)),
                Location  = new Point(S(18), S(46)),
                TextAlign = ContentAlignment.MiddleLeft
            };

            pnlHeader.Controls.AddRange(new Control[] { lblHeaderTitle, lblHeaderSub });

            // ── Radio buttons ────────────────────────────────────────────────
            _rdoBoth    = new RadioButton { Checked = true };
            _rdoActive  = new RadioButton();
            _rdoExpired = new RadioButton();

            int nextY = S(94);

            if (_showExpiryFilter)
            {
                // ── Option cards ─────────────────────────────────────────────
                BuildCard(nextY,
                          "Active & Expired",
                          "Show all records regardless of expiry status",
                          _rdoBoth, out _pnlBothOuter, out _pnlBothInner);

                BuildCard(nextY + (CardH + CardGap),
                          "Active Only",
                          "Excludes expired records — includes Expiring Soon & Warning sets",
                          _rdoActive, out _pnlActiveOuter, out _pnlActiveInner);

                BuildCard(nextY + (CardH + CardGap) * 2,
                          "Expired Only",
                          "Only records that have passed their end date",
                          _rdoExpired, out _pnlExpiredOuter, out _pnlExpiredInner);

                _rdoBoth.CheckedChanged    += (s, e) => SetFilter(_rdoBoth);
                _rdoActive.CheckedChanged  += (s, e) => SetFilter(_rdoActive);
                _rdoExpired.CheckedChanged += (s, e) => SetFilter(_rdoExpired);
                UpdateHighlights();

                nextY += (CardH + CardGap) * 3 - CardGap + S(14);
            }

            var chainControls = new System.Collections.Generic.List<Control>();
            if (_showChainOptions)
            {
                var lblChainSection = new Label
                {
                    Text      = "Chain Labels:",
                    Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(18, 18, 18),
                    AutoSize  = true,
                    Location  = new Point(CardX, nextY)
                };
                chainControls.Add(lblChainSection);
                nextY += S(24);

                _chkShowChainLabels = new CheckBox
                {
                    Text     = "Show \"Original\" / \"Renewal #N\" label above each set's table",
                    Checked  = true,
                    AutoSize = true,
                    // Wrap rather than run off the form if the text still outgrows the width.
                    MaximumSize = new Size(CardW, 0),
                    Location = new Point(CardX, nextY)
                };
                chainControls.Add(_chkShowChainLabels);
                // Advance past the control's ACTUAL height, so a wrapped second line does not
                // collide with the "Chain Order:" heading below it.
                nextY += Math.Max(S(30), _chkShowChainLabels.PreferredSize.Height + S(8));

                var lblOrderSection = new Label
                {
                    Text      = "Chain Order:",
                    Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(18, 18, 18),
                    AutoSize  = true,
                    Location  = new Point(CardX, nextY)
                };
                chainControls.Add(lblOrderSection);
                nextY += S(24);

                _rdoOriginalFirst = new RadioButton
                {
                    Text     = "Original → Renewal N (default)",
                    Checked  = true,
                    AutoSize = true,
                    Location = new Point(CardX, nextY)
                };
                chainControls.Add(_rdoOriginalFirst);
                nextY += S(24);

                _rdoRenewalFirst = new RadioButton
                {
                    Text     = "Renewal N → Original",
                    AutoSize = true,
                    Location = new Point(CardX, nextY)
                };
                chainControls.Add(_rdoRenewalFirst);
                nextY += S(28);

                // ── Scope: selected rows only ────────────────────────────────
                // Only meaningful when rows are actually ticked; with no selection the report
                // already covers everything, so the option would be a no-op.
                if (_selectedCount > 0)
                {
                    _chkSelectedOnly = new CheckBox
                    {
                        Text = string.Format(
                            "Print selected only ({0} row{1}) — skip the rest of each renewal chain",
                            _selectedCount, _selectedCount == 1 ? "" : "s"),
                        Checked     = false,
                        AutoSize    = true,
                        MaximumSize = new Size(CardW, 0),
                        Location    = new Point(CardX, nextY)
                    };

                    // Chain labels/order describe how a chain is presented; with the chain
                    // excluded they have nothing to act on, so grey them out to say so.
                    _chkSelectedOnly.CheckedChanged += (s, e) =>
                    {
                        bool chainShown = !_chkSelectedOnly.Checked;
                        _chkShowChainLabels.Enabled = chainShown;
                        _rdoOriginalFirst.Enabled   = chainShown;
                        _rdoRenewalFirst.Enabled    = chainShown;
                        lblOrderSection.Enabled     = chainShown;
                    };

                    chainControls.Add(_chkSelectedOnly);
                    nextY += Math.Max(S(30), _chkSelectedOnly.PreferredSize.Height + S(8));
                }
                else
                {
                    nextY += S(2);
                }
            }

            // ── Bottom bar ───────────────────────────────────────────────────
            int bottomBarY = nextY + S(4);

            var pnlBottom = new Panel
            {
                BackColor = Color.White,
                Location  = new Point(0, bottomBarY),
                Size      = new Size(FormW, S(54))
            };

            var pnlBottomBorder = new Panel
            {
                BackColor = Color.FromArgb(218, 220, 224),
                Dock      = DockStyle.Top,
                Height    = 1
            };

            // AutoSize so the button always fits its text regardless of DPI
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
                BackColor    = AccentBlue,
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor       = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += (s, e) =>
            {
                if (_showExpiryFilter)
                {
                    if (_rdoActive.Checked)       SelectedFilter = "Active";
                    else if (_rdoExpired.Checked) SelectedFilter = "Expired";
                    else                          SelectedFilter = "Both";
                }

                if (_showChainOptions)
                {
                    ShowChainLabels       = _chkShowChainLabels.Checked;
                    ChainOrderDescending  = _rdoRenewalFirst.Checked;
                    PrintSelectedOnly     = _chkSelectedOnly != null && _chkSelectedOnly.Checked;
                }
            };

            pnlBottom.Controls.AddRange(new Control[] { pnlBottomBorder, btnGenerate, btnCancel });

            // Position buttons after adding (AutoSize needs layout to calc width)
            pnlBottom.Layout += (s, e) =>
            {
                int rightEdge = pnlBottom.ClientSize.Width - S(16);
                btnCancel.Height   = S(32);
                btnGenerate.Height = S(32);
                btnCancel.Top      = S(11);
                btnGenerate.Top    = S(11);
                btnCancel.Left     = rightEdge - btnCancel.Width;
                btnGenerate.Left   = btnCancel.Left - S(8) - btnGenerate.Width;
            };

            ClientSize   = new Size(FormW, bottomBarY + S(54));
            AcceptButton = btnGenerate;
            CancelButton = btnCancel;

            Controls.Add(pnlHeader);
            if (_showExpiryFilter)
                Controls.AddRange(new Control[] { _pnlBothOuter, _pnlActiveOuter, _pnlExpiredOuter });
            Controls.AddRange(chainControls.ToArray());
            Controls.Add(pnlBottom);
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
            radio.Size     = new Size(S(18), S(18));
            radio.Location = new Point(S(14), (CardH - S(18)) / 2 - 1);
            radio.Cursor   = Cursors.Hand;
            radio.TabStop  = true;

            var lblTitle = new Label
            {
                Text        = title,
                Font        = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location    = new Point(S(42), S(10)),
                AutoSize    = false,
                Size        = new Size(CardW - S(58), S(24)),
                ForeColor   = Color.FromArgb(18, 18, 18),
                Cursor      = Cursors.Hand,
                UseMnemonic = false
            };

            // AutoSize + MaximumSize = wraps text at any DPI, grows vertically
            var lblSub = new Label
            {
                Text        = subtitle,
                Font        = new Font("Segoe UI", 8.5f),
                Location    = new Point(S(42), S(36)),
                AutoSize    = true,
                MaximumSize = new Size(CardW - S(58), 0),
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
                _rdoBoth.Checked    = (selected == _rdoBoth);
                _rdoActive.Checked  = (selected == _rdoActive);
                _rdoExpired.Checked = (selected == _rdoExpired);
            }
            finally
            {
                _handlingRadio = false;
            }
            UpdateHighlights();
        }

        private void UpdateHighlights()
        {
            SetHighlight(_pnlBothOuter,    _pnlBothInner,    _rdoBoth.Checked);
            SetHighlight(_pnlActiveOuter,  _pnlActiveInner,  _rdoActive.Checked);
            SetHighlight(_pnlExpiredOuter, _pnlExpiredInner, _rdoExpired.Checked);
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
