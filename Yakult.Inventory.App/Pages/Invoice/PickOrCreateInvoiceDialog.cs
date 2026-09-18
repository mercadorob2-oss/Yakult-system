using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Invoice
{
    // Shown right after BulkAddItemsDialog collects Qty/Description for a batch of
    // items, so the user can pick which Invoice (Set) to attach them to, or create a
    // new one on the spot. Mirrors Pages\Set\AddRequestsToSetDialog.
    internal sealed class PickOrCreateInvoiceDialog : Form
    {
        private RadioButton _rbNewInvoice;
        private RadioButton _rbExistingInvoice;
        private ComboBox _cmbExistingInvoice;
        private Button _btnOk;
        private Button _btnCancel;
        private DataTable _invoices;

        public int? ResultSetId { get; private set; }

        public PickOrCreateInvoiceDialog(int itemCount)
        {
            const int margin = 20;

            Text = "Add to Invoice";
            ClientSize = new Size(440, 236);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5F);
            Padding = new Padding(margin);

            var lblInfo = new Label
            {
                Text = $"Add {itemCount} item(s) to an Invoice:",
                AutoSize = true,
                Location = new Point(margin, margin)
            };

            _rbNewInvoice = new RadioButton
            {
                Text = "Create a new Invoice",
                Checked = true,
                AutoSize = true,
                Location = new Point(margin, lblInfo.Bottom + 20)
            };
            _rbExistingInvoice = new RadioButton
            {
                Text = "Add to an existing Invoice",
                AutoSize = true,
                Location = new Point(margin, _rbNewInvoice.Bottom + 12)
            };
            _rbNewInvoice.CheckedChanged += (s, e) =>
            {
                _cmbExistingInvoice.Enabled = !_rbNewInvoice.Checked;
            };

            _cmbExistingInvoice = new ComboBox
            {
                Location = new Point(margin + 20, _rbExistingInvoice.Bottom + 8),
                Width = ClientSize.Width - (margin + 20) - margin,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };

            _btnOk = new Button
            {
                Text = "OK",
                Width = 90,
                Height = 30,
                Location = new Point(ClientSize.Width - margin - 90 - 12 - 90, ClientSize.Height - margin - 30)
            };
            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 30,
                Location = new Point(ClientSize.Width - margin - 90, ClientSize.Height - margin - 30)
            };
            _btnOk.Click += BtnOk_Click;

            Controls.AddRange(new Control[] { lblInfo, _rbNewInvoice, _rbExistingInvoice, _cmbExistingInvoice, _btnOk, _btnCancel });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Load += async (s, e) => await LoadInvoicesAsync();
        }

        private async System.Threading.Tasks.Task LoadInvoicesAsync()
        {
            try
            {
                var repo = new InvoiceRepository(DatabaseConfig.ConnectionString);
                _invoices = await repo.GetAllInvoicesAsync();

                _cmbExistingInvoice.DataSource = null;
                _cmbExistingInvoice.DataSource = _invoices;
                _cmbExistingInvoice.DisplayMember = "DocumentNumber";
                _cmbExistingInvoice.ValueMember = "SetId";

                if (_invoices == null || _invoices.Rows.Count == 0)
                {
                    _rbExistingInvoice.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load existing invoices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            _btnOk.Enabled = false;
            try
            {
                if (_rbExistingInvoice.Checked)
                {
                    if (!(_cmbExistingInvoice.SelectedItem is DataRowView row))
                    {
                        MessageBox.Show("Please select an Invoice.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    ResultSetId = Convert.ToInt32(row["SetId"]);
                }
                else
                {
                    using (var createDialog = new QuickCreateInvoicePage())
                    {
                        if (createDialog.ShowDialog(this) != DialogResult.OK || !(createDialog.Tag is int newSetId) || newSetId <= 0)
                            return;

                        ResultSetId = newSetId;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                _btnOk.Enabled = true;
            }
        }
    }
}
