using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace Yakult.Inventory.App
{
    public partial class EditItemDialog : Form
    {
        private TextBox txtItemName;
        private TextBox txtDescription;
        private ComboBox cboCategory;
        private TextBox txtSerialNumber;
        private ComboBox cboUnit;
        private CheckBox chkActive;
        private CheckBox chkIsArchived;
        private NumericUpDown nudStockOnHand;
        private Button btnSave;
        private Button btnCancel;
        
        // Public properties to set/get item data
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }
        public string Unit { get; set; }
        public bool IsActive { get; set; }
        public bool IsArchived { get; set; }
        public int StockOnHand { get; set; }
        
        private string connectionString = "Data Source=YAKULT\\SQLEXPRESS;Database=InventoryDB;Integrated Security=True";
        
        public EditItemDialog()
        {
            InitializeComponent();
            LoadCategories();
            LoadUnits();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Edit Item";
            this.Size = new System.Drawing.Size(450, 430);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // Item Name
            Label lblItemName = new Label();
            lblItemName.Text = "Item Name:";
            lblItemName.Location = new System.Drawing.Point(20, 20);
            lblItemName.Size = new System.Drawing.Size(100, 23);
            
            txtItemName = new TextBox();
            txtItemName.Location = new System.Drawing.Point(130, 20);
            txtItemName.Size = new System.Drawing.Size(280, 23);
            
            // Description
            Label lblDescription = new Label();
            lblDescription.Text = "Description:";
            lblDescription.Location = new System.Drawing.Point(20, 55);
            lblDescription.Size = new System.Drawing.Size(100, 23);
            
            txtDescription = new TextBox();
            txtDescription.Location = new System.Drawing.Point(130, 55);
            txtDescription.Size = new System.Drawing.Size(280, 60);
            txtDescription.Multiline = true;
            
            // Category
            Label lblCategory = new Label();
            lblCategory.Text = "Category:";
            lblCategory.Location = new System.Drawing.Point(20, 125);
            lblCategory.Size = new System.Drawing.Size(100, 23);
            
            cboCategory = new ComboBox();
            cboCategory.Location = new System.Drawing.Point(130, 125);
            cboCategory.Size = new System.Drawing.Size(280, 23);
            cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            
            // Serial Number
            Label lblSerialNumber = new Label();
            lblSerialNumber.Text = "Serial Number:";
            lblSerialNumber.Location = new System.Drawing.Point(20, 160);
            lblSerialNumber.Size = new System.Drawing.Size(100, 23);
            
            txtSerialNumber = new TextBox();
            txtSerialNumber.Location = new System.Drawing.Point(130, 160);
            txtSerialNumber.Size = new System.Drawing.Size(280, 23);
            
            // Unit
            Label lblUnit = new Label();
            lblUnit.Text = "Unit:";
            lblUnit.Location = new System.Drawing.Point(20, 195);
            lblUnit.Size = new System.Drawing.Size(100, 23);
            
            cboUnit = new ComboBox();
            cboUnit.Location = new System.Drawing.Point(130, 195);
            cboUnit.Size = new System.Drawing.Size(280, 23);
            cboUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            
            // Stock On Hand
            Label lblStockOnHand = new Label();
            lblStockOnHand.Text = "Stock On Hand:";
            lblStockOnHand.Location = new System.Drawing.Point(20, 230);
            lblStockOnHand.Size = new System.Drawing.Size(100, 23);
            
            nudStockOnHand = new NumericUpDown();
            nudStockOnHand.Location = new System.Drawing.Point(130, 230);
            nudStockOnHand.Size = new System.Drawing.Size(100, 23);
            nudStockOnHand.Maximum = 9999;
            nudStockOnHand.Minimum = 0;
            
            // Active checkbox
            chkActive = new CheckBox();
            chkActive.Text = "Active";
            chkActive.Location = new System.Drawing.Point(130, 265);
            chkActive.Size = new System.Drawing.Size(100, 23);
            chkActive.Checked = true;
            
            // IsArchived checkbox
            chkIsArchived = new CheckBox();
            chkIsArchived.Text = "Archived";
            chkIsArchived.Location = new System.Drawing.Point(240, 265);
            chkIsArchived.Size = new System.Drawing.Size(100, 23);
            chkIsArchived.Checked = false;
            
            // Save button
            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Location = new System.Drawing.Point(230, 345);
            btnSave.Size = new System.Drawing.Size(85, 30);
            btnSave.BackColor = System.Drawing.Color.FromArgb(52, 152, 219);
            btnSave.ForeColor = System.Drawing.Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btnSave.Click += BtnSave_Click;
            
            // Cancel button
            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new System.Drawing.Point(325, 345);
            btnCancel.Size = new System.Drawing.Size(85, 30);
            btnCancel.BackColor = System.Drawing.Color.FromArgb(149, 165, 166);
            btnCancel.ForeColor = System.Drawing.Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            
            // Add controls to form
            this.Controls.Add(lblItemName);
            this.Controls.Add(txtItemName);
            this.Controls.Add(lblDescription);
            this.Controls.Add(txtDescription);
            this.Controls.Add(lblCategory);
            this.Controls.Add(cboCategory);
            this.Controls.Add(lblSerialNumber);
            this.Controls.Add(txtSerialNumber);
            this.Controls.Add(lblUnit);
            this.Controls.Add(cboUnit);
            this.Controls.Add(lblStockOnHand);
            this.Controls.Add(nudStockOnHand);
            this.Controls.Add(chkActive);
            this.Controls.Add(chkIsArchived);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
            
            // Load event to populate fields
            this.Load += EditItemDialog_Load;
        }
        
        private void EditItemDialog_Load(object sender, EventArgs e)
        {
            // Populate fields with the item data
            txtItemName.Text = ItemName;
            txtDescription.Text = Description;
            txtSerialNumber.Text = SerialNumber;
            
            // Select the appropriate category
            if (!string.IsNullOrEmpty(Category))
            {
                int index = cboCategory.FindStringExact(Category);
                if (index != -1)
                    cboCategory.SelectedIndex = index;
            }
            
            // Select the appropriate unit
            if (!string.IsNullOrEmpty(Unit))
            {
                int index = cboUnit.FindStringExact(Unit);
                if (index != -1)
                    cboUnit.SelectedIndex = index;
            }
            
            // Load the stock and active status
            LoadItemDetails();
        }
        
        private void LoadItemDetails()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT StockOnHand, IsActive, IsArchived 
                    FROM Items 
                    WHERE ItemID = @ItemID";
                
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ItemID", ItemId);
                
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                
                if (reader.Read())
                {
                    nudStockOnHand.Value = Convert.ToInt32(reader["StockOnHand"]);
                    chkActive.Checked = Convert.ToBoolean(reader["IsActive"]);
                    chkIsArchived.Checked = Convert.ToBoolean(reader["IsArchived"]);
                }
            }
        }
        
        private void LoadCategories()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                
                cboCategory.DisplayMember = "CategoryName";
                cboCategory.ValueMember = "CategoryID";
                cboCategory.DataSource = dt;
            }
        }
        
        private void LoadUnits()
        {
            cboUnit.Items.Clear();
            cboUnit.Items.AddRange(new string[] { "Unit", "Piece", "Box", "Pack", "Set", "Ream", "Bottle" });
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(txtItemName.Text))
            {
                MessageBox.Show("Please enter an item name.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtItemName.Focus();
                return;
            }
            
            if (cboCategory.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a category.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboCategory.Focus();
                return;
            }
            
            if (cboUnit.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a unit.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboUnit.Focus();
                return;
            }
            
            // Update the item in the database
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string updateQuery = @"
                        UPDATE Items 
                        SET ItemName = @ItemName,
                            Description = @Description,
                            CategoryID = @CategoryID,
                            SerialNumber = @SerialNumber,
                            Unit = @Unit,
                            StockOnHand = @StockOnHand,
                            IsActive = @IsActive,
                            IsArchived = @IsArchived,
                            DateModified = GETDATE(),
                            ModifiedBy = @ModifiedBy
                        WHERE ItemID = @ItemID";
                    
                    SqlCommand command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@ItemID", ItemId);
                    command.Parameters.AddWithValue("@ItemName", txtItemName.Text.Trim());
                    command.Parameters.AddWithValue("@Description", txtDescription.Text.Trim());
                    command.Parameters.AddWithValue("@CategoryID", cboCategory.SelectedValue);
                    command.Parameters.AddWithValue("@SerialNumber", 
                        string.IsNullOrWhiteSpace(txtSerialNumber.Text) ? DBNull.Value : (object)txtSerialNumber.Text.Trim());
                    command.Parameters.AddWithValue("@Unit", cboUnit.Text);
                    command.Parameters.AddWithValue("@StockOnHand", nudStockOnHand.Value);
                    command.Parameters.AddWithValue("@IsActive", chkActive.Checked);
                    command.Parameters.AddWithValue("@IsArchived", chkIsArchived.Checked);
                    command.Parameters.AddWithValue("@ModifiedBy", Environment.UserName);
                    
                    connection.Open();
                    int rowsAffected = command.ExecuteNonQuery();
                    
                    if (rowsAffected > 0)
                    {
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Failed to update item. Please try again.", "Update Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating item: {ex.Message}", "Database Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
