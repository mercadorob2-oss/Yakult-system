using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// Delete confirmation dialog for one or more invoices — lists the affected invoices and lets
    /// the user choose Archive (reversible, via the Archive page) or Permanently Delete. Accepts
    /// pre-formatted display labels rather than InvoiceRow directly, so this WinForms dialog (under
    /// Pages\) doesn't need to reference the WPF ViewModels namespace.
    /// </summary>
    public class DeleteInvoicesDialog : Form
    {
        private RadioButton _rbArchive;
        private RadioButton _rbPermanent;
        private TextBox _txtReason;

        public bool PermanentlyDelete { get; private set; }
        public string Reason { get; private set; } = "No reason provided";

        public DeleteInvoicesDialog(IReadOnlyList<string> invoiceLabels)
        {
            bool single = invoiceLabels.Count == 1;

            Text = single ? "Delete Invoice" : "Delete Invoices";
            Size = new Size(550, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblHeading = new Label
            {
                Text = single
                    ? "The following invoice will be affected:"
                    : $"The following {invoiceLabels.Count} invoice(s) will be affected:",
                AutoSize = true,
                Location = new Point(10, 10)
            };

            var lstInvoices = new ListBox
            {
                Location = new Point(10, 32),
                Size = new Size(510, 110),
                IntegralHeight = false
            };
            lstInvoices.Items.AddRange(invoiceLabels.ToArray());

            int y = 32 + 110 + 12;

            var lblChoice = new Label { Text = "What should happen to the selected invoice(s)?", AutoSize = true, Location = new Point(10, y) };
            y += 22;

            _rbArchive = new RadioButton
            {
                Text = "Archive (reversible — can be restored from the Archive page)",
                AutoSize = true,
                Location = new Point(10, y),
                Checked = true
            };
            y += 24;

            _rbPermanent = new RadioButton
            {
                Text = "Permanently delete (cannot be undone)",
                AutoSize = true,
                Location = new Point(10, y),
                ForeColor = Color.DarkRed
            };
            y += 32;

            var lblReason = new Label { Text = "Reason:", AutoSize = true, Location = new Point(10, y) };
            y += 20;

            _txtReason = new TextBox { Size = new Size(510, 20), Location = new Point(10, y) };
            y += 36;

            var btnConfirm = new Button
            {
                Text = "Confirm",
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

            Controls.AddRange(new Control[]
            {
                lblHeading, lstInvoices, lblChoice, _rbArchive, _rbPermanent,
                lblReason, _txtReason, btnConfirm, btnCancel
            });
            AcceptButton = btnConfirm;
            CancelButton = btnCancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                PermanentlyDelete = _rbPermanent.Checked;
                Reason = string.IsNullOrWhiteSpace(_txtReason.Text) ? "No reason provided" : _txtReason.Text.Trim();
            };
        }
    }
}
