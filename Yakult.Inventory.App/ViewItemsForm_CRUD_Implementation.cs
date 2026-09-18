// Add these controls and code to your existing ViewItems form or MainForm

using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

public partial class MainForm : Form 
{
    // Add these button declarations to your form designer or code
    private Button btnAddItem;
    private Button btnEditItem;
    private Button btnDeleteItem;
    private Panel buttonPanel;
    
    // Add this method to initialize the CRUD buttons
    private void InitializeCRUDButtons()
    {
        // Create a panel to hold the buttons
        buttonPanel = new Panel();
        buttonPanel.Height = 40;
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.BackColor = Color.WhiteSmoke;
        
        // Create Delete button
        btnDeleteItem = new Button();
        btnDeleteItem.Text = "Delete";
        btnDeleteItem.Size = new Size(100, 30);
        btnDeleteItem.Location = new Point(230, 5);
        btnDeleteItem.BackColor = Color.FromArgb(231, 76, 60); // Red color
        btnDeleteItem.ForeColor = Color.White;
        btnDeleteItem.FlatStyle = FlatStyle.Flat;
        btnDeleteItem.FlatAppearance.BorderSize = 0;
        btnDeleteItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnDeleteItem.Cursor = Cursors.Hand;
        btnDeleteItem.Click += BtnDeleteItem_Click;
        
        // Create Edit button
        btnEditItem = new Button();
        btnEditItem.Text = "Edit";
        btnEditItem.Size = new Size(100, 30);
        btnEditItem.Location = new Point(120, 5);
        btnEditItem.BackColor = Color.FromArgb(241, 196, 15); // Yellow/Orange color
        btnEditItem.ForeColor = Color.White;
        btnEditItem.FlatStyle = FlatStyle.Flat;
        btnEditItem.FlatAppearance.BorderSize = 0;
        btnEditItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnEditItem.Cursor = Cursors.Hand;
        btnEditItem.Click += BtnEditItem_Click;
        
        // Create Add button
        btnAddItem = new Button();
        btnAddItem.Text = "Add";
        btnAddItem.Size = new Size(100, 30);
        btnAddItem.Location = new Point(10, 5);
        btnAddItem.BackColor = Color.FromArgb(46, 204, 113); // Green color
        btnAddItem.ForeColor = Color.White;
        btnAddItem.FlatStyle = FlatStyle.Flat;
        btnAddItem.FlatAppearance.BorderSize = 0;
        btnAddItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnAddItem.Cursor = Cursors.Hand;
        btnAddItem.Click += BtnAddItem_Click;
        
        // Add buttons to panel
        buttonPanel.Controls.Add(btnAddItem);
        buttonPanel.Controls.Add(btnEditItem);
        buttonPanel.Controls.Add(btnDeleteItem);
        
        // Add panel to your View Items tab or container
        // Assuming you have a tabPage or panel for View Items
        // Adjust this based on your actual control names
        this.tabPageViewItems.Controls.Add(buttonPanel); // Replace 'tabPageViewItems' with your actual tab page name
        
        // Alternative: If you want to add it directly below the DataGridView
        // dataGridViewItems.Parent.Controls.Add(buttonPanel);
    }
    
    // Add button click event - Opens BatchAddItemDialog
    private void BtnAddItem_Click(object sender, EventArgs e)
    {
        try
        {
            // Open the BatchAddItemDialog
            using (var addDialog = new BatchAddItemDialog())
            {
                if (addDialog.ShowDialog() == DialogResult.OK)
                {
                    // Refresh the DataGridView after adding items
                    LoadItems(); // Call your existing method to reload the items
                    MessageBox.Show("Items added successfully!", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening Add dialog: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    // Edit button click event - Opens EditItemDialog
    private void BtnEditItem_Click(object sender, EventArgs e)
    {
        try
        {
            // Check if a row is selected in the DataGridView
            if (dataGridViewItems.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Get the selected item's ID and details
            DataGridViewRow selectedRow = dataGridViewItems.SelectedRows[0];
            int itemId = Convert.ToInt32(selectedRow.Cells["ID"].Value);
            
            // Create an EditItemDialog and pass the item data
            using (var editDialog = new EditItemDialog())
            {
                // Set the item data in the dialog
                editDialog.ItemId = itemId;
                editDialog.ItemName = selectedRow.Cells["Name"].Value?.ToString();
                editDialog.Description = selectedRow.Cells["Description"].Value?.ToString();
                editDialog.Category = selectedRow.Cells["Category"].Value?.ToString();
                editDialog.SerialNumber = selectedRow.Cells["Serial Number"].Value?.ToString();
                editDialog.Unit = selectedRow.Cells["Unit"].Value?.ToString();
                
                if (editDialog.ShowDialog() == DialogResult.OK)
                {
                    // Refresh the DataGridView after editing
                    LoadItems(); // Call your existing method to reload the items
                    MessageBox.Show("Item updated successfully!", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening Edit dialog: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    // Delete button click event - Deletes selected item(s) with soft delete for referenced items
    private async void BtnDeleteItem_Click(object sender, EventArgs e)
    {
        try
        {
            // Check if any rows are selected
            if (dataGridViewItems.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select one or more items to delete.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Confirm deletion
            int selectedCount = dataGridViewItems.SelectedRows.Count;
            string message = selectedCount == 1 
                ? "Are you sure you want to delete the selected item?" 
                : $"Are you sure you want to delete {selectedCount} selected items?";
            
            DialogResult result = MessageBox.Show(message, "Confirm Deletion", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                await DeleteSelectedItemsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error deleting items: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    // Method to delete selected items using repository
    private async Task DeleteSelectedItemsAsync()
    {
        var itemRepository = new Repositories.ItemRepository();
        int deletedCount = 0;
        int inactivatedCount = 0;
        List<string> messages = new List<string>();
        
        foreach (DataGridViewRow row in dataGridViewItems.SelectedRows)
        {
            if (!row.IsNewRow)
            {
                int itemId = Convert.ToInt32(row.Cells["ItemId"].Value);
                string itemName = row.Cells["Name"].Value?.ToString() ?? "Unknown";
                
                try
                {
                    var (success, message, wasInactivated) = await itemRepository.DeleteItemAsync(itemId);
                    
                    if (success)
                    {
                        if (wasInactivated)
                        {
                            inactivatedCount++;
                            messages.Add($"• {itemName}: Set to INACTIVE (has references)");
                        }
                        else
                        {
                            deletedCount++;
                        }
                    }
                    else
                    {
                        messages.Add($"• {itemName}: {message}");
                    }
                }
                catch (Exception ex)
                {
                    messages.Add($"• {itemName}: Error - {ex.Message}");
                }
            }
        }
        
        // Show results summary
        string resultMessage = "";
        if (deletedCount > 0)
            resultMessage += $"✓ {deletedCount} item(s) deleted successfully\n";
        if (inactivatedCount > 0)
            resultMessage += $"⚠ {inactivatedCount} item(s) set to INACTIVE (had references)\n";
        
        if (messages.Count > 0)
        {
            resultMessage += "\n" + string.Join("\n", messages);
        }
        
        if (!string.IsNullOrEmpty(resultMessage))
        {
            MessageBox.Show(
                resultMessage,
                "Delete Results",
                MessageBoxButtons.OK,
                inactivatedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        
        // Refresh the DataGridView
        LoadItems();
    }
    
    // Call this method in your Form_Load or constructor after InitializeComponent()
    private void MainForm_Load(object sender, EventArgs e)
    {
        // Your existing code...
        
        // Initialize CRUD buttons
        InitializeCRUDButtons();
        
        // Load items in the DataGridView
        LoadItems();
    }
    
    // Make sure you have this method or similar to refresh the DataGridView
    private void LoadItems()
    {
        // Your existing code to load items into the DataGridView
        // This should be the same code you use to populate the grid initially
        
        string connectionString = "Data Source=YAKULT\\SQLEXPRESS;Database=InventoryDB;Integrated Security=True";
        
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            string query = @"
                SELECT 
                    i.ItemID AS ID,
                    i.ItemName AS Name,
                    i.Description,
                    c.CategoryName AS Category,
                    i.SerialNumber AS 'Serial Number',
                    i.IsActive AS Active,
                    i.Unit,
                    i.StockOnHand AS 'Stock On Hand',
                    i.DateCreated AS 'Date Created',
                    i.CreatedBy AS 'Created By',
                    i.DateModified AS 'Date Modified',
                    i.ModifiedBy AS 'Modified By'
                FROM Items i
                LEFT JOIN Categories c ON i.CategoryID = c.CategoryID
                ORDER BY i.ItemID";
            
            SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
            DataTable dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            dataGridViewItems.DataSource = dataTable;
            
            // Format the Active column as checkbox if needed
            if (dataGridViewItems.Columns["Active"] != null)
            {
                // The Active column should already be a checkbox column
                dataGridViewItems.Columns["Active"].Width = 50;
            }
        }
    }
}
