using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for ItemCategory data access operations
    /// </summary>
    public class CategoryRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public CategoryRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Gets all item categories with archive status check and item counts
        /// </summary>
        public async Task<List<ItemCategoryDto>> GetAllAsync()
        {
            var categories = new List<ItemCategoryDto>();

            string query = @"
                SELECT
                    c.CategoryId,
                    c.Name,
                    c.Active,
                    c.DateCreated,
                    c.CreatedBy,
                    u.Name AS CreatedByName,
                    ISNULL(a.IsArchived, 0) AS IsArchived,
                    ISNULL(ic.ItemCount, 0) AS ItemCount
                FROM dbo.ItemCategory c
                LEFT JOIN [User] u ON c.CreatedBy = u.UserId
                LEFT JOIN dbo.ArchiveStatus a ON a.EntityType = 'ItemCategory' AND a.EntityId = c.CategoryId AND a.IsArchived = 1
                LEFT JOIN (
                    SELECT i.CategoryId, COUNT(*) AS ItemCount
                    FROM dbo.Item i
                    LEFT JOIN dbo.ArchiveStatus arch
                        ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                    WHERE i.Active = 1 AND arch.EntityId IS NULL
                    GROUP BY i.CategoryId
                ) ic ON ic.CategoryId = c.CategoryId
                WHERE c.Active = 1
                ORDER BY c.DateCreated DESC, c.CategoryId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(query, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        categories.Add(new ItemCategoryDto
                        {
                            CategoryId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Active = reader.GetBoolean(2),
                            DateCreated = reader.GetDateTime(3),
                            CreatedBy = reader.GetInt32(4),
                            CreatedByName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsArchived = reader.GetBoolean(6),
                            ItemCount = reader.GetInt32(7)
                        });
                    }
                }
            }

            return categories;
        }

        /// <summary>
        /// Creates a new item category
        /// </summary>
        public async Task<int> CreateAsync(ItemCategoryDto category)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.ItemCategory (Name, Active, DateCreated, CreatedBy)
                    OUTPUT INSERTED.CategoryId
                    VALUES (@Name, @Active, @DateCreated, @CreatedBy);
                ", con))
                {
                    cmd.Parameters.AddWithValue("@Name", category.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Active", category.Active);
                    cmd.Parameters.AddWithValue("@DateCreated", category.DateCreated);
                    cmd.Parameters.AddWithValue("@CreatedBy", category.CreatedBy);

                    int categoryId = (int)await cmd.ExecuteScalarAsync();
                    Logger.LogInfo($"ItemCategory created successfully: {category.Name} (ID: {categoryId})");
                    ActivityLogger.Log(ActivityLogger.Actions.Create, "Category", categoryId, $"Category '{category.Name}' created");

                    return categoryId;
                }
            }
        }

        /// <summary>
        /// Updates an existing item category
        /// </summary>
        public async Task<bool> UpdateAsync(ItemCategoryDto category)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.ItemCategory
                    SET Name = @Name,
                        Active = @Active
                    WHERE CategoryId = @CategoryId
                ", con))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", category.CategoryId);
                    cmd.Parameters.AddWithValue("@Name", category.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Active", category.Active);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        Logger.LogInfo($"ItemCategory updated successfully: {category.Name} (ID: {category.CategoryId})");
                        ActivityLogger.Log(ActivityLogger.Actions.Update, "Category", category.CategoryId, $"Category '{category.Name}' updated");
                        return true;
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Permanently deletes an item category
        /// WARNING: This is a hard delete and cannot be undone. Use Archive instead for normal operations.
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteAsync(int categoryId)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    await con.OpenAsync();

                    // Check for dependencies first
                    int itemCount = 0;

                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Item WHERE CategoryId = @CategoryId", con))
                    {
                        cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                        itemCount = (int)await cmd.ExecuteScalarAsync();
                    }

                    // If there are items using this category, cannot delete
                    if (itemCount > 0)
                    {
                        return (false, $"Cannot delete category: {itemCount} item(s) are linked to this category. Please reassign or delete them first.");
                    }

                    // No dependencies, safe to delete
                    using (var cmd = new SqlCommand("DELETE FROM dbo.ItemCategory WHERE CategoryId = @CategoryId", con))
                    {
                        cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Logger.LogInfo($"ItemCategory deleted permanently: ID {categoryId}");
                            ActivityLogger.Log(ActivityLogger.Actions.Delete, "Category", categoryId, $"Category ID {categoryId} permanently deleted");
                            return (true, "Category deleted successfully.");
                        }
                        else
                        {
                            return (false, "Category not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete category ID {categoryId}", ex);
                return (false, $"Error deleting category: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a single category by ID
        /// </summary>
        public async Task<ItemCategoryDto> GetByIdAsync(int categoryId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();

                string query = @"
                    SELECT
                        c.CategoryId,
                        c.Name,
                        c.Active,
                        c.DateCreated,
                        c.CreatedBy,
                        u.Name AS CreatedByName,
                        ISNULL(a.IsArchived, 0) AS IsArchived,
                        (SELECT COUNT(*) FROM dbo.Item i WHERE i.CategoryId = c.CategoryId AND i.Active = 1 AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)) AS ItemCount
                    FROM dbo.ItemCategory c
                    LEFT JOIN [User] u ON c.CreatedBy = u.UserId
                    LEFT JOIN dbo.ArchiveStatus a ON a.EntityType = 'ItemCategory' AND a.EntityId = c.CategoryId AND a.IsArchived = 1
                    WHERE c.CategoryId = @CategoryId";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ItemCategoryDto
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Active = reader.GetBoolean(2),
                                DateCreated = reader.GetDateTime(3),
                                CreatedBy = reader.GetInt32(4),
                                CreatedByName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                IsArchived = reader.GetBoolean(6),
                                ItemCount = reader.GetInt32(7)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all items belonging to a category with their current location/process
        /// </summary>
        public async Task<List<CategoryItemLocationDto>> GetItemsByCategoryAsync(int categoryId)
        {
            var items = new List<CategoryItemLocationDto>();

            string query = @"
                SELECT
                    i.ItemId,
                    i.Name,
                    i.SerialNumber,
                    i.StockOnHand,
                    i.Active,
                    -- Check if item is archived or in other processes
                    CASE
                        WHEN EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1) THEN 'Archived'
                        WHEN EXISTS (SELECT 1 FROM dbo.SetItem si WHERE si.ItemId = i.ItemId) THEN 'In Set'
                        WHEN EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Status = 'Submitted') THEN 'In Request'
                        WHEN EXISTS (SELECT 1 FROM dbo.SetItemUpdate siu WHERE siu.ItemId = i.ItemId AND siu.Processed = 0) THEN 'Pending Update'
                        ELSE 'In Inventory'
                    END AS CurrentLocation
                FROM dbo.Item i
                WHERE i.CategoryId = @CategoryId
                    AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
                ORDER BY i.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            items.Add(new CategoryItemLocationDto
                            {
                                ItemId = reader.GetInt32(0),
                                ItemName = reader.GetString(1),
                                SerialNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                                StockOnHand = reader.GetInt32(3),
                                Active = reader.GetBoolean(4),
                                CurrentLocation = reader.GetString(5)
                            });
                        }
                    }
                }
            }

            return items;
        }
    }

    /// <summary>
    /// DTO for displaying items in a category with their current location
    /// </summary>
    public class CategoryItemLocationDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string SerialNumber { get; set; }
        public int StockOnHand { get; set; }
        public bool Active { get; set; }
        public string CurrentLocation { get; set; }
    }
}
