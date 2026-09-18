﻿// =====================================================
// ALTERNATIVE: Show ALL Items (No Availability Check)
// =====================================================
// If you want to allow requesting the same item multiple times,
// replace the LoadLookups() method with this version:

private void LoadLookups()
{
    _categories.Clear();
    _employees.Clear();
    _allItems.Clear();

    var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

    if (string.IsNullOrWhiteSpace(cs))
    {
        MessageBox.Show("Connection string not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
    }

    try
    {
        using (var con = new SqlConnection(cs))
        {
            con.Open();

            // Employees
            using (var cmd = new SqlCommand("SELECT EmpId, Name FROM dbo.Employee WHERE Active = 1 ORDER BY Name", con))
            using (var r = cmd.ExecuteReader())
            {
                _employees.Add(new EmployeeItem { Id = null, Name = "-- Select Employee --" });
                while (r.Read())
                {
                    _employees.Add(new EmployeeItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                }
            }

            // Categories
            using (var cmd = new SqlCommand("SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    _categories.Add(new CategoryItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                }
            }

            // \u2705 SIMPLEST: Load ALL active items, mark based on stock only
            const string sqlItems = @"
                SELECT 
                    ItemId, 
                    Name, 
                    StockOnHand, 
                    CategoryId, 
                    Amount
                FROM dbo.Item 
                WHERE Active = 1 
                ORDER BY Name";

            using (var cmd = new SqlCommand(sqlItems, con))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    int stock = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                    
                    _allItems.Add(new ItemItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        StockOnHand = stock,
                        CategoryId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                        Amount = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                        IsAvailable = stock > 0  // \u2705 Only check stock, not requests
                    });
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Failed to load lookups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// NOTES:
// 1. This version shows ALL active items
// 2. Only blocks items that are out of stock (StockOnHand = 0)
// 3. Allows requesting the same item multiple times
// 4. Database validation will still catch errors at save time

