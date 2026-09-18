using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Services
{
    public class InventoryService
    {
        private readonly string _connectionString;

        public InventoryService()
        {
            _connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");
        }

        public async Task<string> DetermineEntryTypeAsync(int itemId, bool isAddingStock = true)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "SELECT AffectsInventory FROM dbo.Item WHERE ItemId = @ItemId";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);

                    var result = await cmd.ExecuteScalarAsync();

                    if (result == null || result == DBNull.Value)
                        throw new InventoryException($"Item {itemId} not found");

                    bool affectsInventory = Convert.ToBoolean(result);

                    // Use centralized helper with new logic
                    int quantity = isAddingStock ? 1 : -1;
                    return InventoryHelper.DetermineEntryType(affectsInventory, quantity);
                }
            }
        }

        public async Task<int?> FindSetIdForItemAsync(int itemId, int? reqId = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Strategy 1: If ReqId provided, get SetId from Request
                if (reqId.HasValue)
                {
                    string query = "SELECT SetId FROM dbo.Request WHERE ReqId = @ReqId";
                    
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ReqId", reqId);
                        var result = await cmd.ExecuteScalarAsync();
                        return result == DBNull.Value ? null : (int?)result;
                    }
                }

                // Strategy 2: For tracked assets (software/services), find most recent Set
                string itemQuery = "SELECT IsTrackedAsset FROM dbo.Item WHERE ItemId = @ItemId";
                bool isTrackedAsset;

                using (var cmd = new SqlCommand(itemQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    var result = await cmd.ExecuteScalarAsync();
                    isTrackedAsset = result != null && result != DBNull.Value && Convert.ToBoolean(result);
                }

                if (isTrackedAsset)
                {
                    string setQuery = @"
                        SELECT TOP 1 s.SetId
                        FROM dbo.[Set] s
                        INNER JOIN dbo.SetItem si ON s.SetId = si.SetId
                        WHERE si.ItemId = @ItemId
                        AND s.IsInvoice = 1
                        ORDER BY s.CreatedAt DESC";

                    using (var cmd = new SqlCommand(setQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        var result = await cmd.ExecuteScalarAsync();
                        return result == DBNull.Value ? null : (int?)result;
                    }
                }

                return null;
            }
        }

        public async Task ValidateInventoryCreationAsync(InventoryCreateDto dto)
        {
            // Validation rules
            if (dto.ItemId <= 0)
                throw new InventoryException("ItemId is required");

            if (dto.Quantity <= 0)
                throw new InventoryException("Quantity must be greater than 0");

            if (dto.PostedBy <= 0)
                throw new InventoryException("PostedBy is required");

            // If SetId is provided, validate it exists
            if (dto.SetId.HasValue)
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    
                    string query = "SELECT COUNT(*) FROM dbo.[Set] WHERE SetId = @SetId";
                    
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SetId", dto.SetId);
                        int count = (int)await cmd.ExecuteScalarAsync();

                        if (count == 0)
                            throw new InventoryException($"SetId {dto.SetId} does not exist");
                    }
                }
            }
        }
    }
}
