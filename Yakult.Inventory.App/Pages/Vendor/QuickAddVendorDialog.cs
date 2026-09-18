using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Session;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Vendor
{
    public partial class QuickAddVendorDialog : Form
    {
        private Label lblVendorName, lblAddress, lblTIN;
        private TextBox txtVendorName, txtAddress, txtTIN;
        private CheckBox chkActive, chkIsRefiller, chkIsDisposer, chkIsBuyer;
        private Button btnSave;
        private Button btnCancel;

        public int? NewVendorId { get; private set; }
        public string NewVendorName { get; private set; }

        public QuickAddVendorDialog()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Quick Add Vendor";

            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnCancel.Click += (s, e) => Close();

            btnSave = new Button { Text = "Save" };
            btnSave.Click += BtnSave_Click;

            // Auto-sizing shell instead of the previous absolute Left/Top layout, which only
            // lined up at 100% display scaling and clipped the role checkboxes and the buttons
            // at anything larger.
            var fields = Shared.QuickAddDialogLayout.BuildShell(this, btnSave, btnCancel, fieldWidth: 320);

            lblVendorName = new Label { Text = "Vendor Name *" };
            txtVendorName = new TextBox { MaxLength = 100 };
            Shared.QuickAddDialogLayout.AddRow(fields, lblVendorName.Text, txtVendorName);

            lblAddress = new Label { Text = "Address" };
            txtAddress = new TextBox
            {
                Height = 62,
                Multiline = true,
                MaxLength = 200,
                ScrollBars = ScrollBars.Vertical
            };
            Shared.QuickAddDialogLayout.AddRow(fields, lblAddress.Text, txtAddress);

            lblTIN = new Label { Text = "TIN (Tax ID)" };
            txtTIN = new TextBox { MaxLength = 50 };
            Shared.QuickAddDialogLayout.AddRow(fields, lblTIN.Text, txtTIN);

            chkActive = new CheckBox { Text = "Active", Checked = true };
            chkIsRefiller = new CheckBox { Text = "Is Refiller" };
            chkIsDisposer = new CheckBox { Text = "Is Disposer" };
            chkIsBuyer = new CheckBox { Text = "Is Buyer" };

            Shared.QuickAddDialogLayout.AddFullWidth(fields,
                Shared.QuickAddDialogLayout.CheckBoxRow(chkActive, chkIsRefiller, chkIsDisposer, chkIsBuyer),
                topMargin: 8);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtVendorName.Text))
            {
                MessageBox.Show("Please enter vendor name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtVendorName.Focus();
                return;
            }

            try
            {
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                {
                    MessageBox.Show("Connection string not found.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.Vendor (VendorName, Address, IsActive, IsRefiller, IsDisposer, IsBuyer, CreatedDate, TIN)
                        OUTPUT INSERTED.VendorID
                        VALUES (@VendorName, @Address, @IsActive, @IsRefiller, @IsDisposer, @IsBuyer, @CreatedDate, @TIN)", con))
                    {
                        cmd.Parameters.AddWithValue("@VendorName", txtVendorName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address",
                            string.IsNullOrWhiteSpace(txtAddress.Text) ? (object)DBNull.Value : txtAddress.Text.Trim());
                        cmd.Parameters.AddWithValue("@IsActive",   chkActive.Checked);
                        cmd.Parameters.AddWithValue("@IsRefiller", chkIsRefiller.Checked);
                        cmd.Parameters.AddWithValue("@IsDisposer", chkIsDisposer.Checked);
                        cmd.Parameters.AddWithValue("@IsBuyer",    chkIsBuyer.Checked);
                        cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@TIN",
                            string.IsNullOrWhiteSpace(txtTIN.Text) ? (object)DBNull.Value : txtTIN.Text.Trim());

                        NewVendorId = (int)cmd.ExecuteScalar();
                        NewVendorName = txtVendorName.Text.Trim();
                    }
                }

                MessageBox.Show("Vendor added successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save vendor: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

