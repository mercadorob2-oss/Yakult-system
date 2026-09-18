using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Renewal
{
    /// <summary>
    /// Shown before generating the Renewals (Grouped) report.
    /// Lets the user opt-in to including the full renewal chain
    /// (original + intermediate sets) alongside the latest set.
    /// </summary>
    public class RenewalReportOptionsDialog : Form
    {
        public bool IncludeOriginalSets => _chkIncludeOriginal.Checked;

        private readonly CheckBox _chkIncludeOriginal;

        private const int FormW = 480;

        public RenewalReportOptionsDialog()
        {
            Text            = "Report Options";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new Font("Segoe UI", 9.5f);
            BackColor       = Color.FromArgb(245, 247, 250);

            // ── Header ──────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                BackColor = Color.FromArgb(0, 150, 136),
                Dock      = DockStyle.Top,
                Height    = 70
            };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Report Options",
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Size      = new Size(FormW - 20, 26),
                Location  = new Point(18, 13),
                TextAlign = ContentAlignment.MiddleLeft
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Choose what to include in the generated report",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(180, 228, 222),
                AutoSize  = false,
                Size      = new Size(FormW - 20, 18),
                Location  = new Point(18, 43),
                TextAlign = ContentAlignment.MiddleLeft
            });

            // ── Option card ──────────────────────────────────────────────────
            const int cardX = 16;
            const int cardW = FormW - cardX * 2;
            const int cardY = 86;
            const int cardH = 84;

            var pnlOuter = new Panel
            {
                Location  = new Point(cardX, cardY),
                Size      = new Size(cardW, cardH),
                BackColor = Color.FromArgb(214, 216, 220),
                Cursor    = Cursors.Hand
            };
            var pnlInner = new Panel
            {
                Location  = new Point(1, 1),
                Size      = new Size(cardW - 2, cardH - 2),
                BackColor = Color.White,
                Cursor    = Cursors.Hand
            };

            _chkIncludeOriginal = new CheckBox
            {
                AutoSize = false,
                Size     = new Size(18, 18),
                Location = new Point(14, (cardH - 18) / 2 - 1),
                Cursor   = Cursors.Hand
            };

            var lblTitle = new Label
            {
                Text        = "Include Original Sets",
                Font        = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location    = new Point(42, 10),
                AutoSize    = false,
                Size        = new Size(cardW - 58, 22),
                ForeColor   = Color.FromArgb(18, 18, 18),
                Cursor      = Cursors.Hand,
                UseMnemonic = false
            };
            var lblSub = new Label
            {
                Text        = "Shows the full renewal chain: original set, intermediate renewals, and the latest renewal. " +
                              "Each set is labelled (Original), (Renewed), or (Latest).",
                Font        = new Font("Segoe UI", 8.5f),
                Location    = new Point(42, 34),
                AutoSize    = true,
                MaximumSize = new Size(cardW - 58, 0),
                ForeColor   = Color.FromArgb(115, 115, 115),
                Cursor      = Cursors.Hand,
                UseMnemonic = false
            };

            pnlInner.Controls.AddRange(new Control[] { _chkIncludeOriginal, lblTitle, lblSub });
            pnlOuter.Controls.Add(pnlInner);

            Action toggle = () => _chkIncludeOriginal.Checked = !_chkIncludeOriginal.Checked;
            pnlOuter.Click  += (s, e) => toggle();
            pnlInner.Click  += (s, e) => toggle();
            lblTitle.Click  += (s, e) => toggle();
            lblSub.Click    += (s, e) => toggle();

            // ── Bottom bar ───────────────────────────────────────────────────
            int bottomBarY = cardY + cardH + 14;
            var pnlBottom  = new Panel
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
                Text         = "Generate",
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

            pnlBottom.Controls.AddRange(new Control[] { btnGenerate, btnCancel });

            pnlBottom.Layout += (s, e) =>
            {
                int rightEdge      = pnlBottom.ClientSize.Width - 16;
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

            Controls.AddRange(new Control[] { pnlHeader, pnlOuter, pnlBottom });
        }
    }
}
