using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Set
{
    /// <summary>
    /// Archive confirmation dialog for one or more sets — reason + "also archive items"/"deactivate
    /// items" checkboxes. Extracted from the two near-identical inline Forms built in
    /// ViewSetPage.ArchiveSetAsync() (single) and BulkArchiveSets() (bulk) so the WPF Sets page can
    /// reuse a single dialog for both cases, exactly as the WinForms page's two dialogs behaved.
    /// </summary>
    public class ArchiveSetDialog : Form
    {
        private TextBox _txtReason;
        private CheckBox _chkArchiveItems;
        private CheckBox _chkDeactivateItems;

        public string ReasonText { get; private set; } = "No reason provided";
        public bool ArchiveItems { get; private set; } = true;
        public bool DeactivateItems { get; private set; } = true;

        public ArchiveSetDialog(IReadOnlyList<SetDto> sets)
        {
            bool single = sets.Count == 1;

            Text = single ? "Archive Set" : "Bulk Archive Sets";
            Size = single ? new Size(550, 340) : new Size(550, 390);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            string messageText;
            int messageHeight;
            if (single)
            {
                var s = sets[0];
                messageText = "Are you sure you want to archive this set?\n\n" +
                              $"Set Code: {s.SetCode}\n" +
                              $"Document #: {s.DocumentNumber}\n" +
                              $"Status: {s.Status}\n\n" +
                              "The set and its related items will be moved to the archive.";
                messageHeight = 100;
            }
            else
            {
                string setList = string.Join("\n", sets.Take(6).Select(s => $"  • {s.SetCode}"));
                if (sets.Count > 6) setList += $"\n  ... and {sets.Count - 6} more";
                messageText = $"Archive {sets.Count} set(s)?\n\n{setList}\n\n" +
                              "All selected sets and their related items will be moved to the archive.";
                messageHeight = 130;
            }

            var lblMessage = new Label
            {
                Text = messageText,
                AutoSize = false,
                Size = new Size(510, messageHeight),
                Location = new Point(10, 10)
            };

            int y = 10 + messageHeight + 8;

            var lblReason = new Label { Text = "Reason for archiving:", AutoSize = true, Location = new Point(10, y) };
            y += 20;

            _txtReason = new TextBox { Size = new Size(510, 20), Location = new Point(10, y) };
            y += 30;

            _chkArchiveItems = new CheckBox
            {
                Text = single ? "Also archive all items in this set" : "Also archive all items in each set",
                AutoSize = true,
                Location = new Point(10, y),
                Checked = true
            };
            y += 25;

            _chkDeactivateItems = new CheckBox
            {
                Text = "Mark archived items as inactive",
                AutoSize = true,
                Location = new Point(10, y),
                Checked = true
            };
            y += 40;

            var btnArchive = new Button
            {
                Text = single ? "Archive" : "Archive All",
                DialogResult = DialogResult.OK,
                Location = new Point(340, y),
                Size = new Size(90, 25)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(440, y),
                Size = new Size(90, 25)
            };

            Controls.AddRange(new Control[] { lblMessage, lblReason, _txtReason, _chkArchiveItems, _chkDeactivateItems, btnArchive, btnCancel });
            AcceptButton = btnArchive;
            CancelButton = btnCancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                ReasonText = string.IsNullOrWhiteSpace(_txtReason.Text) ? "No reason provided" : _txtReason.Text.Trim();
                ArchiveItems = _chkArchiveItems.Checked;
                DeactivateItems = _chkDeactivateItems.Checked;
            };
        }
    }
}
