using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace Inventory.RequestPortal.Services
{
    /// <summary>
    /// DEV-ONLY service for database reset and diagnostic operations.
    /// This class should NEVER be instantiated in production builds.
    /// </summary>
    public interface IDevToolsService
    {
        Task<DevToolsResetResult> ResetDatabaseAsync();
    }

    public class DevToolsResetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RequestsDeleted { get; set; }
        public int InventoryRowsDeleted { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Implementation of dev tools service.
    /// Resets request and inventory tables to clean state for testing.
    ///
    /// SAFETY MEASURES:
    /// - Uses transactions with rollback on error
    /// - Only affects Request and Inventory tables
    /// - Does NOT touch master data (User, Employee, Company, Branch, Department, Item)
    /// - Extensive logging of all operations
    /// </summary>
    public class DevToolsService : IDevToolsService
    {
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly ILogger<DevToolsService> _logger;

        public DevToolsService(
            IConnectionStringProvider connectionStringProvider,
            ILogger<DevToolsService> logger)
        {
            _connectionStringProvider = connectionStringProvider;
            _logger = logger;
        }

        /// <summary>
        /// Resets the database by:
        /// 1. Truncating dbo.Request table (all cartridge requests)
        /// 2. Truncating dbo.Inventory table (inventory movement history)
        ///
        /// Master data (Items, Users, Employees, Organizations) is NOT affected.
        /// </summary>
        public async Task<DevToolsResetResult> ResetDatabaseAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new DevToolsResetResult();

            _logger.LogWarning("========================================");
            _logger.LogWarning("DEV TOOLS: DATABASE RESET INITIATED");
            _logger.LogWarning("========================================");

            try
            {
                using var connection = new SqlConnection(_connectionStringProvider.GetConnectionString());
                await connection.OpenAsync();

                _logger.LogWarning("Target database: {Database} on {Server}", connection.Database, connection.DataSource);
                _logger.LogInformation("Database connection opened: {Database}", connection.Database);

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Step 1: Get count of requests before deletion
                    int requestCount = await GetTableCountAsync(connection, transaction, "dbo.Request");
                    int inventoryCount = await GetTableCountAsync(connection, transaction, "dbo.Inventory");

                    _logger.LogInformation("Current counts - Requests: {RequestCount}, Inventory: {InventoryCount}",
                        requestCount, inventoryCount);

                    // Step 2: Truncate Request table
                    // NOTE: Cannot use TRUNCATE if there are foreign key constraints
                    // Using DELETE instead for safety
                    var cmdDeleteRequests = new SqlCommand("DELETE FROM dbo.Request", connection, transaction);
                    cmdDeleteRequests.CommandTimeout = 60;
                    int requestsDeleted = await cmdDeleteRequests.ExecuteNonQueryAsync();

                    _logger.LogInformation("Deleted {Count} rows from dbo.Request", requestsDeleted);

                    // Step 3: Truncate Inventory table
                    var cmdDeleteInventory = new SqlCommand("DELETE FROM dbo.Inventory", connection, transaction);
                    cmdDeleteInventory.CommandTimeout = 60;
                    int inventoryDeleted = await cmdDeleteInventory.ExecuteNonQueryAsync();

                    _logger.LogInformation("Deleted {Count} rows from dbo.Inventory", inventoryDeleted);

                    // Step 4: Reset Item stock counts to zero (optional - uncomment if needed)
                    // var cmdResetStock = new SqlCommand(@"
                    //     UPDATE dbo.Item
                    //     SET StockOnHand = 0,
                    //         Available = 0
                    //     WHERE Category = 'Cartridge'", connection, transaction);
                    // cmdResetStock.CommandTimeout = 60;
                    // int itemsReset = await cmdResetStock.ExecuteNonQueryAsync();
                    // _logger.LogInformation("Reset stock for {Count} cartridge items", itemsReset);

                    // Commit transaction
                    await transaction.CommitAsync();

                    stopwatch.Stop();

                    result.Success = true;
                    result.Message = $"Database reset successful. Deleted {requestsDeleted} requests and {inventoryDeleted} inventory rows.";
                    result.RequestsDeleted = requestsDeleted;
                    result.InventoryRowsDeleted = inventoryDeleted;
                    result.Duration = stopwatch.Elapsed;

                    _logger.LogWarning("========================================");
                    _logger.LogWarning("DATABASE RESET SUCCESSFUL");
                    _logger.LogWarning("Requests deleted: {RequestsDeleted}", requestsDeleted);
                    _logger.LogWarning("Inventory rows deleted: {InventoryDeleted}", inventoryDeleted);
                    _logger.LogWarning("Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);
                    _logger.LogWarning("========================================");
                }
                catch (Exception ex)
                {
                    // Rollback on any error
                    await transaction.RollbackAsync();

                    stopwatch.Stop();

                    _logger.LogError(ex, "Database reset FAILED. Transaction rolled back.");

                    result.Success = false;
                    result.Message = $"Database reset failed: {ex.Message}";
                    result.Duration = stopwatch.Elapsed;

                    _logger.LogWarning("========================================");
                    _logger.LogWarning("DATABASE RESET FAILED - ROLLED BACK");
                    _logger.LogWarning("========================================");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "Failed to connect to database for reset operation");

                result.Success = false;
                result.Message = $"Connection failed: {ex.Message}";
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// Helper method to get row count from a table.
        /// </summary>
        private async Task<int> GetTableCountAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string tableName)
        {
            var cmd = new SqlCommand($"SELECT COUNT(*) FROM {tableName}", connection, transaction);
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }
}
