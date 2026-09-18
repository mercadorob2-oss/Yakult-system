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

namespace Yakult.Inventory.App.Pages.Category
{
    public partial class QuickAddCategory : Form
    {
        private Label lblName;
        private TextBox txtName;
        private CheckBox chkActive;
        private Button btnSave;
        private Button btnCancel;

        public int? NewCategoryId { get; private set; }
        public string NewCategoryName { get; private set; }

        public QuickAddCategory()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Quick Add Category";

            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnCancel.Click += (s, e) => Close();

            btnSave = new Button { Text = "Save" };
            btnSave.Click += BtnSave_Click;

            // Auto-sizing shell instead of absolute Left/Top, which clipped at display scaling
            // above 100%.
            var fields = Shared.QuickAddDialogLayout.BuildShell(this, btnSave, btnCancel);

            lblName = new Label { Text = "Category Name *" };
            txtName = new TextBox { MaxLength = 50 };
            Shared.QuickAddDialogLayout.AddRow(fields, lblName.Text, txtName);

            chkActive = new CheckBox { Text = "Active", Checked = true };
            Shared.QuickAddDialogLayout.AddFullWidth(fields,
                Shared.QuickAddDialogLayout.CheckBoxRow(chkActive), topMargin: 8);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter category name.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
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
                        INSERT INTO dbo.ItemCategory (Name, Active, DateCreated, CreatedBy)
                        OUTPUT INSERTED.CategoryId
                        VALUES (@Name, @Active, @DateCreated, @CreatedBy)", con))
                    {
                        cmd.Parameters.AddWithValue("@Name", txtName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Active", chkActive.Checked);
                        cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                        // Reuse AppSession like your employee dialog:
                        cmd.Parameters.AddWithValue("@CreatedBy", AppSession.CurrentUserId);

                        NewCategoryId = (int)cmd.ExecuteScalar();
                        NewCategoryName = txtName.Text.Trim();
                    }
                }

                MessageBox.Show("Category added successfully!", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save category: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class ComboItem { /* not used here, but kept for parity with employee dialog if needed later */ }
    }
}

