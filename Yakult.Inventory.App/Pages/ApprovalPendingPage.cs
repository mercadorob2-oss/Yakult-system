using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages
{
    /// <summary>
    /// Shown to an employee whose cartridge-request approval is still Pending or
    /// has been Rejected.  The page is read-only; the employee cannot proceed
    /// until the supervisor approves.
    ///
    /// Usage — wire into the login / navigation flow after CheckOrCreateAsync:
    ///   if (approval.Status == "Pending" || approval.Status == "Rejected")
    ///       mainPanel.Controls.Add(new ApprovalPendingPage(approval));
    /// </summary>
    public class ApprovalPendingPage : UserControl
    {
        private readonly CartridgeApprovalDto _approval;

        public ApprovalPendingPage(CartridgeApprovalDto approval)
        {
            _approval = approval ?? throw new ArgumentNullException(nameof(approval));
            Dock = DockStyle.Fill;
            BuildUi();
        }

        private void BuildUi()
        {
            BackColor = Color.FromArgb(248, 250, 252);

            bool isRejected = _approval.Status == "Rejected";

            // ── Card panel ────────────────────────────────────────────────────
            var card = new Panel
            {
                Size      = new Size(520, isRejected ? 390 : 348),
                BackColor = Color.White,
                Anchor    = AnchorStyles.None
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            int y = 28;

            // ── Icon / status banner ──────────────────────────────────────────
            var banner = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(520, 6),
                BackColor = isRejected
                    ? Color.FromArgb(185, 28, 28)
                    : Color.FromArgb(217, 119, 6)
            };
            card.Controls.Add(banner);

            // ── Title ─────────────────────────────────────────────────────────
            string titleText = isRejected
                ? "Cartridge Request Access Rejected"
                : "Awaiting Supervisor Approval";

            var lblTitle = new Label
            {
                Text      = titleText,
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = isRejected
                    ? Color.FromArgb(185, 28, 28)
                    : Color.FromArgb(146, 64, 14),
                AutoSize  = true,
                Location  = new Point(28, y)
            };
            card.Controls.Add(lblTitle);
            y += 46;

            // ── Status message ────────────────────────────────────────────────
            string message = isRejected
                ? "Your supervisor has declined your cartridge request access.\n" +
                  "Please contact your supervisor or department administrator."
                : "Your request is pending approval from your department supervisor.\n" +
                  "You will be notified once the supervisor has reviewed your request.";

            var lblMsg = new Label
            {
                Text      = message,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Size      = new Size(464, 52),
                Location  = new Point(28, y)
            };
            card.Controls.Add(lblMsg);
            y += 62;

            // ── Info grid ─────────────────────────────────────────────────────
            void AddRow(string label, string value)
            {
                card.Controls.Add(new Label
                {
                    Text      = label,
                    Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Size      = new Size(150, 20),
                    Location  = new Point(28, y)
                });
                card.Controls.Add(new Label
                {
                    Text      = value ?? "—",
                    Font      = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(30, 41, 59),
                    Size      = new Size(310, 20),
                    Location  = new Point(182, y)
                });
                y += 24;
            }

            AddRow("Supervisor:",     _approval.SupervisorName);
            AddRow("Company:",        _approval.CompanyName);
            AddRow("Branch:",         _approval.BranchName);
            AddRow("Department:",     _approval.DepartmentName);
            AddRow("Requested On:",   _approval.RequestedAt.ToString("MMMM dd, yyyy  h:mm tt"));

            if (isRejected)
            {
                y += 4;
                var sep = new Panel
                {
                    Location  = new Point(28, y),
                    Size      = new Size(464, 1),
                    BackColor = Color.FromArgb(226, 232, 240)
                };
                card.Controls.Add(sep);
                y += 10;
                AddRow("Reason:", string.IsNullOrWhiteSpace(_approval.Notes)
                    ? "No reason provided."
                    : _approval.Notes);
            }
            else
            {
                y += 12;
                var lblHint = new Label
                {
                    Text      = "A notification email has been sent to your supervisor.",
                    Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    AutoSize  = true,
                    Location  = new Point(28, y)
                };
                card.Controls.Add(lblHint);
            }

            // ── Centre the card in the UserControl ────────────────────────────
            this.Resize += (s, e) => CentreCard(card);
            Controls.Add(card);
            CentreCard(card);
        }

        private void CentreCard(Control card)
        {
            card.Location = new Point(
                Math.Max(0, (Width  - card.Width)  / 2),
                Math.Max(0, (Height - card.Height) / 2));
        }
    }
}
