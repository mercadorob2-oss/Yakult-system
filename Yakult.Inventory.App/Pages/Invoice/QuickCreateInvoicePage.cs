using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Invoice
{
    // Minimal header-only Invoice creation form used when the user picks
    // "Create a new Invoice" from PickOrCreateInvoiceDialog. Financial totals
    // (Subtotal/Vat/Wht/Discount/Total) start at 0 -- same as every other invoice,
    // they get recalculated from the line items on ViewInvoiceDetailPage.
    internal sealed class QuickCreateInvoicePage : Form
    {
        private TextBox _txtDocumentNumber;
        private TextBox _txtReferenceNumber;
        private TextBox _txtSite;
        private DateTimePicker _dtpStartDate;
        private DateTimePicker _dtpEndDate;
        private ComboBox _cmbCompany;
        private TextBox _txtRemarks;
        private Button _btnSave;
        private Button _btnCancel;

        private readonly InvoiceRepository _repository;

        public QuickCreateInvoicePage()
        {
            _repository = new InvoiceRepository(DatabaseConfig.ConnectionString);
            BuildUi();
            Load += async (s, e) => await LoadCompaniesAsync();
        }

        private void BuildUi()
        {
            Text = "Create Invoice";
            Size = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(20)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            void AddRow(string label, Control field)
            {
                int row = root.RowCount;
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.Controls.Add(new Label { Text = label, AutoSize = false, Height = 30, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
                field.Dock = DockStyle.Fill;
                field.Margin = new Padding(0, 4, 0, 4);
                root.Controls.Add(field, 1, row);
                root.RowCount++;
            }

            _txtDocumentNumber = new TextBox();
            AddRow("Invoice/Doc Number *", _txtDocumentNumber);

            _txtReferenceNumber = new TextBox();
            AddRow("Reference (PO) Number", _txtReferenceNumber);

            _txtSite = new TextBox();
            AddRow("Site", _txtSite);

            _dtpStartDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy", Value = DateTime.Today };
            AddRow("Start Date", _dtpStartDate);

            _dtpEndDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy", Value = DateTime.Today.AddMonths(1) };
            AddRow("End Date", _dtpEndDate);

            _cmbCompany = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            AddRow("Company", _cmbCompany);

            _txtRemarks = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 80 };
            AddRow("Remarks", _txtRemarks);

            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 12, 0, 0)
            };

            _btnSave = new Button { Text = "Create Invoice", Width = 140, Height = 36 };
            _btnCancel = new Button { Text = "Cancel", Width = 100, Height = 36, DialogResult = DialogResult.Cancel };
            _btnSave.Click += BtnSave_Click;

            buttonBar.Controls.Add(_btnSave);
            buttonBar.Controls.Add(_btnCancel);
            root.Controls.Add(buttonBar, 1, root.RowCount);
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private async System.Threading.Tasks.Task LoadCompaniesAsync()
        {
            try
            {
                DataTable companies = await _repository.GetAllCompaniesAsync();
                _cmbCompany.DataSource = companies;
                _cmbCompany.DisplayMember = "CompanyName";
                _cmbCompany.ValueMember = "ComId";
                _cmbCompany.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load companies.\n\n{ex.Message}", "Create Invoice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            var documentNumber = _txtDocumentNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                MessageBox.Show("Invoice/Doc Number is required.", "Create Invoice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtDocumentNumber.Focus();
                return;
            }

            try
            {
                _btnSave.Enabled = false;
                Cursor = Cursors.WaitCursor;

                int? companyId = _cmbCompany.SelectedValue != null
                    ? (int?)Convert.ToInt32(_cmbCompany.SelectedValue)
                    : null;

                int newSetId = await _repository.CreateInvoiceAsync(
                    documentNumber,
                    string.IsNullOrWhiteSpace(_txtReferenceNumber.Text) ? null : _txtReferenceNumber.Text.Trim(),
                    string.IsNullOrWhiteSpace(_txtSite.Text) ? null : _txtSite.Text.Trim(),
                    _dtpStartDate.Value.Date,
                    _dtpEndDate.Value.Date,
                    0m, 0m, 0m, 0m, 0m,
                    companyId,
                    "Yes",
                    string.IsNullOrWhiteSpace(_txtRemarks.Text) ? null : _txtRemarks.Text.Trim(),
                    AppSession.CurrentUserId);

                Tag = newSetId;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create invoice.\n\n{ex.Message}", "Create Invoice",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnSave.Enabled = true;
            }
        }
    }
}
