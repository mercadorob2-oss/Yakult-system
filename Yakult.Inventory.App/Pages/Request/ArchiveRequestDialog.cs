using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Request
{
    /// <summary>
    /// Archive confirmation dialog for one or more requests — reason + optional
    /// "also archive associated item(s)" checkbox. Extracted from the inline Form built in
    /// ViewRequestsPage.ArchiveSelectedRequest() so the WPF Requests page can reuse it unchanged.
    /// </summary>
    public class ArchiveRequestDialog : Form
    {
        private TextBox _txtReason;
        private CheckBox _chkArchiveItem;

        public string ReasonText { get; private set; } = "No reason provided";
        public bool AlsoArchiveItem { get; private set; }

        public ArchiveRequestDialog(IReadOnlyList<RequestDto> checkedRequests)
        {
            Text = checkedRequests.Count == 1 ? "Archive Request" : "Archive Requests";
            Size = new Size(500, 340);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var first = checkedRequests[0];
            string messageText = checkedRequests.Count == 1
                ? "Are you sure you want to archive this request?\n\n" +
                  $"Employee: {first.EmployeeName}\n" +
                  $"Item: {first.ItemName}\n" +
                  $"Quantity: {first.Quantity}\n" +
                  $"Status: {first.Status}\n\n" +
                  "The request will be moved to the archive."
                : $"Are you sure you want to archive {checkedRequests.Count} requests?\n\n" +
                  string.Join("\n", checkedRequests.Take(6).Select(r => $"  • Req #{r.ReqId} — {r.EmployeeName} ({r.ItemName})")) +
                  (checkedRequests.Count > 6 ? $"\n  ... and {checkedRequests.Count - 6} more" : "") +
                  "\n\nAll selected requests will be moved to the archive.";

            var lblMessage = new Label
            {
                Text = messageText,
                AutoSize = false,
                Size = new Size(460, 110),
                Location = new Point(10, 10)
            };

            var lblReason = new Label
            {
                Text = "Reason for archiving:",
                AutoSize = true,
                Location = new Point(10, 125)
            };

            _txtReason = new TextBox
            {
                Size = new Size(460, 20),
                Location = new Point(10, 145)
            };

            _chkArchiveItem = new CheckBox
            {
                Text = checkedRequests.Count == 1 ? "Also archive the associated item" : "Also archive associated items",
                AutoSize = true,
                Location = new Point(10, 175),
                Checked = false
            };

            var lblNote = new Label
            {
                Text = "Note: Employees will NOT be archived",
                AutoSize = true,
                Location = new Point(10, 197),
                ForeColor = Color.Gray,
                Font = new Font(DefaultFont, FontStyle.Italic)
            };

            var btnArchive = new Button
            {
                Text = "Archive",
                DialogResult = DialogResult.OK,
                Location = new Point(290, 230),
                Size = new Size(90, 25)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(390, 230),
                Size = new Size(90, 25)
            };

            Controls.AddRange(new Control[] { lblMessage, lblReason, _txtReason, _chkArchiveItem, lblNote, btnArchive, btnCancel });
            AcceptButton = btnArchive;
            CancelButton = btnCancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                ReasonText = string.IsNullOrWhiteSpace(_txtReason.Text) ? "No reason provided" : _txtReason.Text;
                AlsoArchiveItem = _chkArchiveItem.Checked;
            };
        }
    }
}
