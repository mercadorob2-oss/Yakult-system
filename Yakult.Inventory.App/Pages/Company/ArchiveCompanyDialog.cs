using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Company
{
    /// <summary>
    /// Archive confirmation dialog for one or more companies — message + reason textbox.
    /// Extracted from the inline Form built in ViewCompanyPage.BtnArchive_Click() so the WPF
    /// Companies page can reuse it unchanged.
    /// </summary>
    public class ArchiveCompanyDialog : Form
    {
        private TextBox _txtReason;

        public string ReasonText => string.IsNullOrWhiteSpace(_txtReason.Text) ? "No reason provided" : _txtReason.Text;

        public ArchiveCompanyDialog(string message)
        {
            Text = "Archive Company";
            Size = new Size(500, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblMessage = new Label
            {
                Text = message,
                AutoSize = false,
                Size = new Size(460, 90),
                Location = new Point(10, 10)
            };

            var lblReason = new Label
            {
                Text = "Reason for archiving:",
                AutoSize = true,
                Location = new Point(10, 110)
            };

            _txtReason = new TextBox
            {
                Size = new Size(460, 20),
                Location = new Point(10, 130)
            };

            var btnArchive = new Button
            {
                Text = "Archive",
                DialogResult = DialogResult.OK,
                Location = new Point(290, 170),
                Size = new Size(90, 25)
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(390, 170),
                Size = new Size(90, 25)
            };

            Controls.AddRange(new Control[] { lblMessage, lblReason, _txtReason, btnArchive, btnCancel });
            AcceptButton = btnArchive;
            CancelButton = btnCancel;
        }
    }
}
