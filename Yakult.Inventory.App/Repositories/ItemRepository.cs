using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    public class ItemRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ItemRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public int AddItem(Pages.ItemDto item)
        {
            // LOG: Add diagnostic logging
            var cs = GetConnectionString();
            System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] Starting - Connection String: {cs?.Substring(0, Math.Min(50, cs?.Length ?? 0))}...");

            using (var con = new SqlConnection(cs))
            {
                con.Open();
                System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] Connection opened - Database: {con.Database}, Server: {con.DataSource}");

                // WRITE TO FILE FOR DEBUGGING
                try
                {
                    var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DB_Connection_Log.txt");
                    var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SAVING ITEM '{item.Name}' TO:\n  Server: {con.DataSource}\n  Database: {con.Database}\n\n";
                    System.IO.File.AppendAllText(logPath, logMessage);
                }
                catch { /* ignore logging errors */ }

                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        int newItemId = InsertItemGraph(item, con, transaction);

                        transaction.Commit();
                        ActivityLogger.Log(ActivityLogger.Actions.Create, "Item", newItemId, $"Item '{item.Name}' created");

                        try
                        {
                            var auditRepo = new ItemAuditTrailRepository();
                            auditRepo.LogActionAsync(new ItemAuditTrailDto
                            {
                                ItemId = newItemId,
                                SerialNumber = item.SerialNumber,
                                Action = "Item Created/Added",
                                ActionTime = item.DateCreated,
                                Direction = "IN",
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = newItemId,
                                Notes = "Item added to inventory",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            }).GetAwaiter().GetResult();
                        }
                        catch
                        {
                        }

                        // Fan out an Inventory System "Activity" notification (best-effort — never
                        // let a notification failure affect the committed item add).
                        try
                        {
                            int actorId = item.CreatedByUserId > 0 ? item.CreatedByUserId : AppSession.CurrentUserId;
                            int qty     = item.StockOnHand > 1 ? item.StockOnHand : 1;
                            InventoryActivityNotifier.NotifyItemAdded(newItemId, item.Name, item.ItemType, actorId,
                                quantity: qty, serialNumber: item.SerialNumber);
                        }
                        catch
                        {
                        }

                        return newItemId;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] EXCEPTION - Type: {ex.GetType().Name}, Message: {ex.Message}");
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Overload that runs the Item inserts on a caller-supplied connection/transaction, so a
        /// larger unit of work (e.g. the atomic invoice import) can commit or roll back the whole
        /// batch together. The CALLER owns commit/rollback and any post-commit logging.
        /// </summary>
        public int AddItem(Pages.ItemDto item, SqlConnection con, SqlTransaction transaction)
            => InsertItemGraph(item, con, transaction);

        /// <summary>
        /// Core inserts shared by both AddItem overloads: the Item row, its baseline Inventory row,
        /// Cartridge bookkeeping (when Category = Cartridge), and the base Renewal record. Runs
        /// entirely on the supplied connection/transaction and returns the new ItemId. Does not
        /// commit, roll back, or log — that is the caller's responsibility.
        /// </summary>
        private int InsertItemGraph(Pages.ItemDto item, SqlConnection con, SqlTransaction transaction)
        {
                        // Step 1: Insert into Item table (now includes ItemType, Condition tracking, Warranty, DatePurchased, LicenseNumber, Duration, WarrantyStartDate, and new inventory control columns)
                        const string sqlItem = @"
                    INSERT INTO dbo.Item
                        (Name, Description, ModelNumber, CartridgeModelId, ConsumableModelId, Active, CategoryId, Category,
                         SerialNumber, UnitOfMeasure, StockOnHand, DateCreated, CreatedBy, DateModified, ModifiedBy,
                         ItemType, StartDate, EndDate, Amount, ConditionID, VendorId, Remarks, WarrantyYears, DatePurchased, LicenseNumber,
                         DurationYears, DurationStartDate, DurationEndDate, WarrantyStartDate, WarrantyEndDate, AffectsInventory, AcquisitionType, IsTrackedAsset,
                         RefillStatus, CellPhoneNumber, IMEI1, IMEI2, IsBorrowable, SubType)
                    VALUES
                        (@Name, @Description, @ModelNumber, @CartridgeModelId, @ConsumableModelId, @Active, @CategoryId, @Category,
                         @SerialNumber, @UnitOfMeasure, @StockOnHand, @DateCreated, @CreatedBy, @DateModified, @ModifiedBy,
                         @ItemType, @StartDate, @EndDate, @Amount, @ConditionID, @VendorId, @Remarks, @WarrantyYears, @DatePurchased, @LicenseNumber,
                         @DurationYears, @DurationStartDate, @DurationEndDate, @WarrantyStartDate, @WarrantyEndDate, @AffectsInventory, @AcquisitionType, @IsTrackedAsset,
                         @RefillStatus, @CellPhoneNumber, @IMEI1, @IMEI2, @IsBorrowable, @SubType);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newItemId;
                        using (var cmd = new SqlCommand(sqlItem, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Name", item.Name);
                            cmd.Parameters.AddWithValue("@Description", (object)item.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Active", item.Active);
                            // Allow CategoryId to be null when no category is specified (e.g., Excel import rows without Category)
                            cmd.Parameters.AddWithValue("@CategoryId", item.CategoryId > 0 ? (object)item.CategoryId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Category", (object)item.Category ?? DBNull.Value);
                            var serialNumber = string.IsNullOrWhiteSpace(item.SerialNumber) ? null : item.SerialNumber.Trim();
                            cmd.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ModelNumber", item.ModelNumber ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@CartridgeModelId", item.CartridgeModelId.HasValue ? (object)item.CartridgeModelId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ConsumableModelId", item.ConsumableModelId.HasValue ? (object)item.ConsumableModelId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@UnitOfMeasure", item.UnitOfMeasure);
                            cmd.Parameters.AddWithValue("@StockOnHand", item.StockOnHand);
                            cmd.Parameters.AddWithValue("@DateCreated", item.DateCreated);
                            cmd.Parameters.AddWithValue("@CreatedBy", item.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@DateModified", item.DateCreated);
                            cmd.Parameters.AddWithValue("@ModifiedBy", item.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@ItemType", (object)item.ItemType ?? "Hardware");
                            // StartDate = DateCreated (automatically set when item received into system)
                            // If not explicitly set, default to DateCreated
                            cmd.Parameters.AddWithValue("@StartDate", item.StartDate ?? item.DateCreated);
                            cmd.Parameters.AddWithValue("@EndDate", (object)item.EndDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Amount", item.Amount);
                            // Condition tracking parameters (default to Good = 1 if not set)
                            var conditionId = item.ConditionId <= 0 ? 1 : item.ConditionId;
                            cmd.Parameters.AddWithValue("@ConditionID", conditionId);
                            // Vendor reference (may be null)
                            cmd.Parameters.AddWithValue("@VendorId", item.VendorId.HasValue ? (object)item.VendorId.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remarks", (object)item.Remarks ?? DBNull.Value);
                            // Warranty years (default to 0 if not specified)
                            cmd.Parameters.AddWithValue("@WarrantyYears", item.WarrantyYears);
                            // Date Purchased (optional)
                            cmd.Parameters.AddWithValue("@DatePurchased", (object)item.DatePurchased ?? DBNull.Value);
                            // License Number (optional - for Software/License and Services)
                            cmd.Parameters.AddWithValue("@LicenseNumber", (object)item.LicenseNumber ?? DBNull.Value);
                            // Duration tracking (for contracts/services)
                            cmd.Parameters.AddWithValue("@DurationYears", item.DurationYears);
                            cmd.Parameters.AddWithValue("@DurationStartDate", (object)item.DurationStartDate ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DurationEndDate", (object)item.DurationEndDate ?? DBNull.Value);
                            // Warranty Start Date (optional)
                            cmd.Parameters.AddWithValue("@WarrantyStartDate", (object)item.WarrantyStartDate ?? DBNull.Value);
                            // Warranty End Date: use the caller's value if set, otherwise derive it the
                            // same way the old computed column did (Start + Years). WarrantyEndDate is a
                            // real column now, so it must be supplied explicitly on insert.
                            var warrantyEndDate = item.WarrantyEndDate
                                ?? (item.WarrantyStartDate.HasValue ? item.WarrantyStartDate.Value.AddYears(item.WarrantyYears) : (DateTime?)null);
                            cmd.Parameters.AddWithValue("@WarrantyEndDate", (object)warrantyEndDate ?? DBNull.Value);
                            // New inventory control columns
                            cmd.Parameters.AddWithValue("@AffectsInventory", item.AffectsInventory);
                            cmd.Parameters.AddWithValue("@AcquisitionType", (object)item.AcquisitionType ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IsTrackedAsset", item.IsTrackedAsset);
                            // RefillStatus: NULL = Brand New (default), 'Available' = Refilled (manually entered refilled stock)
                            cmd.Parameters.AddWithValue("@RefillStatus", (object)item.RefillStatus ?? DBNull.Value);
                            // Cell phone tracking (Category = CellPhone only; NULL for every other category)
                            cmd.Parameters.AddWithValue("@CellPhoneNumber", (object)item.CellPhoneNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IMEI1", (object)item.IMEI1 ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@IMEI2", (object)item.IMEI2 ?? DBNull.Value);
                            // dbo.Item.IsBorrowable is NOT NULL, even though ItemDto.IsBorrowable
                            // is bool? — a caller that never sets it (Import Invoice CSV / Build
                            // Invoice never did) leaves it null, and DBNull against a NOT NULL
                            // column fails the whole insert. Default to false, matching every
                            // caller that DOES set it explicitly (Batch Add Items always passes
                            // true or false, never null).
                            cmd.Parameters.AddWithValue("@IsBorrowable", item.IsBorrowable ?? false);
                            cmd.Parameters.AddWithValue("@SubType", (object)item.SubType ?? DBNull.Value);

                            System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] BEFORE ExecuteScalar for serial: {item.SerialNumber}");
                            newItemId = (int)cmd.ExecuteScalar();
                            System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] AFTER ExecuteScalar - New ItemId: {newItemId}, Serial: {item.SerialNumber}");
                        }

                        // ==================================================================================
                        // BASELINE INVENTORY CREATION
                        // ==================================================================================
                        // AUTHORITATIVE BUSINESS RULE:
                        // Inventory is tracked as soon as an Item is added to the system.
                        // This is BASELINE inventory - it records item existence, not transactional movement.
                        //
                        // BASELINE Inventory characteristics:
                        // - Created during Item creation (this method)
                        // - SetId = NULL (no Set association required)
                        // - ReqId = NULL (no Request association)
                        // - EntryType = "Fixed Assets" (for non-inventory items) or "Positive" (for inventory items)
                        // - Quantity = 0 for Fixed Assets, StockOnHand for inventory items
                        //
                        // TRANSACTIONAL Inventory (created later by Request/Invoice flows):
                        // - SetId is REQUIRED
                        // - ReqId may be present
                        // - EntryType = "Positive" or "Negative" based on movement
                        // ==================================================================================

                        const string sqlInventory = @"
                    INSERT INTO dbo.Inventory
                        (ItemId, EntryType, Quantity, DatePosted, PostedBy, ReqId, SetId, Description, ConditionID, Active)
                    VALUES
                        (@ItemId, @EntryType, @Quantity, @DatePosted, @PostedBy, NULL, NULL, @Description, @ConditionID, @Active)";

                        using (var cmd = new SqlCommand(sqlInventory, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", newItemId);

                            // Determine EntryType for BASELINE inventory
                            // For items that affect inventory: "Positive" (initial stock)
                            // For non-inventory items (Software/Services): "Fixed Assets" (tracked asset)
                            string baselineEntryType;
                            int baselineQuantity;

                            if (item.AffectsInventory)
                            {
                                // Inventory items: Positive EntryType with actual quantity
                                baselineEntryType = "Positive";
                                baselineQuantity = item.StockOnHand;
                            }
                            else
                            {
                                // Non-inventory items: Fixed Assets with 0 quantity
                                baselineEntryType = "Fixed Assets";
                                baselineQuantity = 0;
                            }

                            cmd.Parameters.AddWithValue("@EntryType", baselineEntryType);
                            cmd.Parameters.AddWithValue("@Quantity", baselineQuantity);
                            cmd.Parameters.AddWithValue("@DatePosted", item.DateCreated);
                            cmd.Parameters.AddWithValue("@PostedBy", item.CreatedByUserId);
                            cmd.Parameters.AddWithValue("@Description", $"Initial inventory for {item.Name}" +
                                (string.IsNullOrWhiteSpace(item.SerialNumber) ? "" : $" (S/N: {item.SerialNumber})"));

                            // Use the same corrected conditionId that was used for Item insert (defaults to 1 if not set)
                            var inventoryConditionId = item.ConditionId <= 0 ? 1 : item.ConditionId;
                            cmd.Parameters.AddWithValue("@ConditionID", inventoryConditionId);
                            cmd.Parameters.AddWithValue("@Active", 1);

                            cmd.ExecuteNonQuery();
                        }

                        System.Diagnostics.Debug.WriteLine($"[ItemRepository.AddItem] Created BASELINE inventory - ItemId: {newItemId}, EntryType: {(item.AffectsInventory ? "Positive" : "Fixed Assets")}, Quantity: {(item.AffectsInventory ? item.StockOnHand : 0)}");

                        // ==================================================================================
                        // BUSINESS RULE: Automatic Cartridge Registration
                        // When an item is created with Category == "Cartridge", automatically:
                        //   1) Create dbo.Cartridge row (default type: Brand New)
                        //   2) Create initial dbo.CartridgeMovement row (StockIn) when StockOnHand > 0
                        // This ensures Cartridge pages show newly created cartridge items without manual SQL.
                        // ==================================================================================
                        bool isCartridgeCategory = !string.IsNullOrWhiteSpace(item.Category)
                            && string.Equals(item.Category.Trim(), "Cartridge", StringComparison.OrdinalIgnoreCase);

                        if (isCartridgeCategory)
                        {
                            const string sqlEnsureCartridgeAndMovement = @"
-- Resolve CartridgeTypeId: 'Available' RefillStatus means this is a Refilled unit;
-- NULL (or any other value) means Brand New. dbo.Cartridge is a secondary record
-- (dbo.Item is the single source of truth), but we keep it consistent here.
DECLARE @ResolvedTypeId INT;
IF @RefillStatus = 'Available'
    SET @ResolvedTypeId = (SELECT TOP 1 CartridgeTypeId FROM dbo.CartridgeType WHERE CartridgeTypeName = 'Refill');
ELSE
    SET @ResolvedTypeId = (SELECT TOP 1 CartridgeTypeId FROM dbo.CartridgeType WHERE CartridgeTypeName = 'Brand New');

IF @ResolvedTypeId IS NULL
    THROW 50000, 'CartridgeType lookup failed in dbo.CartridgeType — ensure ''Brand New'' and ''Refill'' rows exist.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Cartridge WHERE ItemId = @ItemId)
BEGIN
    INSERT INTO dbo.Cartridge (ItemId, CartridgeTypeId, IsActive)
    VALUES (@ItemId, @ResolvedTypeId, 1);
END

IF (@StockOnHand IS NOT NULL AND @StockOnHand > 0)
BEGIN
    INSERT INTO dbo.CartridgeMovement
        (ItemId, EmployeeId, Quantity, MovementType, CartridgeTypeId, ReferenceRequestId, CreatedBy)
    VALUES
        (@ItemId, NULL, @StockOnHand, 'StockIn', @ResolvedTypeId, NULL, @CreatedBy);
END
";

                            using (var cmd = new SqlCommand(sqlEnsureCartridgeAndMovement, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", newItemId);
                                cmd.Parameters.AddWithValue("@StockOnHand", item.StockOnHand);
                                cmd.Parameters.AddWithValue("@CreatedBy", item.CreatedByUserId);
                                // Pass RefillStatus so the SQL can resolve the correct CartridgeTypeId
                                cmd.Parameters.AddWithValue("@RefillStatus", (object)item.RefillStatus ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // ==================================================================================
                        // RENEWAL RECORD CREATION
                        // Each new Item gets one active (IsArchived = 0) Renewal record that carries PartNumber.
                        // ==================================================================================
                        const string sqlRenewal = @"
                    INSERT INTO dbo.Renewals (ItemId, PartNumber, RenewalStatus, RenewalCount, IsArchived, CreatedBy, CreatedAt)
                    VALUES (@ItemId, @PartNumber, 'Active', 1, 0, @CreatedBy, GETDATE())";

                        using (var cmd = new SqlCommand(sqlRenewal, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", newItemId);
                            cmd.Parameters.AddWithValue("@PartNumber", (object)item.PartNumber ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@CreatedBy", item.CreatedByUserId);
                            cmd.ExecuteNonQuery();
                        }

            System.Diagnostics.Debug.WriteLine($"[ItemRepository.InsertItemGraph] Inserted ItemId: {newItemId}, Name: {item.Name}, Serial: {item.SerialNumber}");
            return newItemId;
        }

        /// <summary>
        /// Checks if a serial number already exists in the database.
        /// Used during import to prevent duplicate serial number errors.
        /// </summary>
        /// <param name="serialNumber">The serial number to check</param>
        /// <returns>True if the serial number already exists, false otherwise</returns>
        public bool SerialNumberExists(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return false;

            var cs = GetConnectionString();
            using (var con = new SqlConnection(cs))
            {
                con.Open();
                const string sql = "SELECT COUNT(1) FROM dbo.Item WHERE SerialNumber = @SerialNumber";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Returns true if any item with the given ModelNumber and CategoryId already exists.
        /// Used for idempotent CSV import of non-serialized items.
        /// </summary>
        public bool ItemExistsByModelAndCategory(string modelNumber, int categoryId)
        {
            if (string.IsNullOrWhiteSpace(modelNumber) || categoryId <= 0)
                return false;

            var cs = GetConnectionString();
            using (var con = new SqlConnection(cs))
            {
                con.Open();
                const string sql = @"
SELECT COUNT(1) FROM dbo.Item
WHERE ModelNumber = @ModelNumber AND CategoryId = @CategoryId";
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelNumber", modelNumber);
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Returns the CartridgeModelId of the built-in 'Unassigned' CartridgeModel, creating it if it does not exist.
        /// Used as a default when importing cartridge items that have no CartridgeModelId in the CSV.
        /// </summary>
        public int GetOrCreateDefaultCartridgeModelId(int createdByUserId)
        {
            var cs = GetConnectionString();
            using (var con = new SqlConnection(cs))
            {
                con.Open();

                // Try to find existing Unassigned model
                const string selectSql = @"
SELECT TOP 1 CartridgeModelId FROM dbo.CartridgeModel
WHERE LTRIM(RTRIM(ModelNumber)) = 'Unassigned'";
                using (var cmd = new SqlCommand(selectSql, con))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                // Create the Unassigned default model
                const string insertSql = @"
INSERT INTO dbo.CartridgeModel (ModelNumber, VendorId, IsRequestable, IsRefillable, IsActive, CreatedAt, CreatedBy)
VALUES ('Unassigned', NULL, 0, 0, 1, GETDATE(), @CreatedBy);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                using (var cmd = new SqlCommand(insertSql, con))
                {
                    cmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public async Task<System.Collections.Generic.List<ItemLookupDto>> GetActiveItemsLookupAsync()
        {
            var items = new System.Collections.Generic.List<ItemLookupDto>();

            const string sql = @"
SELECT
    i.ItemId,
    i.Name,
    i.ModelNumber,
    i.Category,
    i.SerialNumber
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE i.Active = 1
  AND arch.EntityId IS NULL
ORDER BY i.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ItemLookupDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber"))
                        });
                    }
                }
            }

            return items;
        }

        public async Task<System.Collections.Generic.List<ItemLookupDto>> GetStockItemsLookupAsync()
        {
            var items = new System.Collections.Generic.List<ItemLookupDto>();

            const string sql = @"
SELECT
    i.ItemId,
    i.Name,
    i.ModelNumber,
    i.Category,
    i.SerialNumber
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE i.Active = 1
  AND ISNULL(i.StockOnHand, 0) > 0
  AND arch.EntityId IS NULL
ORDER BY i.Name;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ItemLookupDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber"))
                        });
                    }
                }
            }

            return items;
        }

        public async Task<System.Collections.Generic.List<ItemLookupDto>> GetHardwareStockItemsLookupAsync()
        {
            var items = new System.Collections.Generic.List<ItemLookupDto>();

            const string sql = @"
SELECT
    i.ItemId,
    i.Name,
    i.ModelNumber,
    ISNULL(NULLIF(LTRIM(RTRIM(i.Category)), ''), c.Name) AS Category,
    i.SerialNumber,
    CAST(ISNULL(i.StockOnHand, 0) AS int) AS StockOnHand
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE i.Active = 1
  AND i.ItemType = 'Hardware'
  AND ISNULL(i.StockOnHand, 0) > 0
  -- Exclude consumable cartridges/ink/toner from hardware replacement stock — they are Hardware type in DB but belong to cartridge workflow, not device replacement.
  AND ISNULL(c.Name, i.Category) NOT IN ('Cartridge', 'Ink', 'Toner', 'Printhead')
  AND ISNULL(i.CategoryId, 0) NOT IN (1038, 1063, 1064, 1076)
  AND arch.EntityId IS NULL
ORDER BY i.Name;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ItemLookupDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            StockOnHand = reader.GetInt32(reader.GetOrdinal("StockOnHand"))
                        });
                    }
                }
            }

            return items;
        }

        public async Task<System.Collections.Generic.List<ItemLookupDto>> GetHardwareOutItemsLookupAsync()
        {
            var items = new System.Collections.Generic.List<ItemLookupDto>();

            const string sql = @"
SELECT
    i.ItemId,
    i.Name,
    i.ModelNumber,
    ISNULL(NULLIF(LTRIM(RTRIM(i.Category)), ''), c.Name) AS Category,
    i.SerialNumber
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
WHERE i.Active = 1
  AND i.ItemType = 'Hardware'
  AND (
        -- Legacy definition (stock depleted)
        ISNULL(i.StockOnHand, 0) = 0
        -- Deployed assets (e.g., Set deployed via usp_Item_Deploy)
     OR i.DurationStartDate IS NOT NULL
        -- Included in an active/non-archived Set (via SetItem snapshot or Request rows)
     OR EXISTS
        (
            SELECT 1
            FROM dbo.SetItem si
            INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
            LEFT JOIN dbo.ArchiveStatus asSet ON asSet.EntityType = 'Set' AND asSet.EntityId = s.SetId AND asSet.IsArchived = 1
            WHERE si.ItemId = i.ItemId
              AND ISNULL(s.Active, 1) = 1
              AND asSet.EntityId IS NULL
        )
     OR EXISTS
        (
            SELECT 1
            FROM dbo.Request r
            INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
            LEFT JOIN dbo.ArchiveStatus asSet ON asSet.EntityType = 'Set' AND asSet.EntityId = s.SetId AND asSet.IsArchived = 1
            WHERE r.ItemId = i.ItemId
              AND r.SetId IS NOT NULL
              AND ISNULL(s.Active, 1) = 1
              AND asSet.EntityId IS NULL
        )
  )
  AND arch.EntityId IS NULL
ORDER BY i.Name;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new ItemLookupDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber"))
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Attempts to delete an item, or sets it inactive if it has references
        /// </summary>
        public async Task<(bool Success, string Message, bool WasInactivated)> DeleteItemAsync(int itemId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                try
                {
                    string serialNumber = null;
                    using (var cmd = new SqlCommand("SELECT TOP (1) SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        var result = await cmd.ExecuteScalarAsync();
                        serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                    }

                    // Check if item is referenced anywhere
                    string checkReferencesSql = @"
                        SELECT
                            (SELECT COUNT(*) FROM SetItem WHERE ItemId = @ItemId) as SetItemCount,
                            (SELECT COUNT(*) FROM Request WHERE ItemId = @ItemId) as RequestCount,
                            (SELECT COUNT(*) FROM Inventory WHERE ItemId = @ItemId) as InventoryCount,
                            (SELECT COUNT(*) FROM SetItemUpdate WHERE ItemId = @ItemId AND Processed = 0) as MobileUpdateCount";

                    int setItemCount = 0;
                    int requestCount = 0;
                    int inventoryCount = 0;
                    int mobileUpdateCount = 0;

                    using (var cmd = new SqlCommand(checkReferencesSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                setItemCount = reader.GetInt32(0);
                                requestCount = reader.GetInt32(1);
                                inventoryCount = reader.GetInt32(2);
                                mobileUpdateCount = reader.GetInt32(3);
                            }
                        }
                    }

                    // If item is referenced, set it inactive instead
                    if (setItemCount > 0 || requestCount > 0 || inventoryCount > 0 || mobileUpdateCount > 0)
                    {
                        using (var transaction = conn.BeginTransaction())
                        {
                            string updateSql = "UPDATE Item SET Active = 0 WHERE ItemId = @ItemId";
                            using (var cmd = new SqlCommand(updateSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemId,
                                SerialNumber = serialNumber,
                                Action = "Item Deactivated",
                                ActionTime = DateTime.Now,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = itemId,
                                Notes =
                                    "Item could not be deleted because it is referenced in: " +
                                    $"{setItemCount} set(s)/invoice(s), {requestCount} request(s), {inventoryCount} inventory record(s), {mobileUpdateCount} pending mobile update(s). " +
                                    "Item set to inactive instead.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            transaction.Commit();
                            ActivityLogger.Log(ActivityLogger.Actions.Delete, "Item", itemId, $"Item ID {itemId} deactivated (has references, could not hard-delete)");
                        }

                        string message = $"Item cannot be deleted because it is referenced in:\n" +
                                       $"- {setItemCount} set(s)/invoice(s)\n" +
                                       $"- {requestCount} request(s)\n" +
                                       $"- {inventoryCount} inventory record(s)\n" +
                                       $"- {mobileUpdateCount} pending mobile update(s)\n\n" +
                                       $"Item has been set to INACTIVE instead.";

                        return (true, message, true);
                    }

                    // If no references, proceed with hard delete using a transaction
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemId,
                                SerialNumber = serialNumber,
                                Action = "Item Deleted",
                                ActionTime = DateTime.Now,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = itemId,
                                Notes = "Item deleted.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            // Step 1: Delete the Item
                            string deleteSql = "DELETE FROM Item WHERE ItemId = @ItemId";
                            using (var cmd = new SqlCommand(deleteSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    return (false, "Item not found.", false);
                                }
                            }

                            // Step 2: Clean up ArchiveStatus record (if exists)
                            string cleanupArchiveSql = @"
                                DELETE FROM ArchiveStatus 
                                WHERE EntityType = 'Item' AND EntityId = @ItemId";
                            
                            using (var cmd = new SqlCommand(cleanupArchiveSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();
                            ActivityLogger.Log(ActivityLogger.Actions.Delete, "Item", itemId, $"Item ID {itemId} permanently deleted");
                            return (true, "Item deleted successfully!", false);
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"Error deleting item: {ex.Message}", false);
                }
            }
        }

        /// <summary>
        /// Checks if an item has dependencies in related tables
        /// </summary>
        public async Task<(bool HasDependencies, int SetItemCount, int RequestCount, int InventoryCount)> CheckItemDependencies(int itemId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                try
                {
                    string checkReferencesSql = @"
                        SELECT
                            (SELECT COUNT(*) FROM SetItem WHERE ItemId = @ItemId) as SetItemCount,
                            (SELECT COUNT(*) FROM Request WHERE ItemId = @ItemId) as RequestCount,
                            (SELECT COUNT(*) FROM Inventory WHERE ItemId = @ItemId) as InventoryCount,
                            (SELECT COUNT(*) FROM SetItemUpdate WHERE ItemId = @ItemId AND Processed = 0) as MobileUpdateCount";

                    int setItemCount = 0;
                    int requestCount = 0;
                    int inventoryCount = 0;
                    int mobileUpdateCount = 0;

                    using (var cmd = new SqlCommand(checkReferencesSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                setItemCount = reader.GetInt32(0);
                                requestCount = reader.GetInt32(1);
                                inventoryCount = reader.GetInt32(2);
                                mobileUpdateCount = reader.GetInt32(3);
                            }
                        }
                    }

                    bool hasDependencies = (setItemCount > 0 || requestCount > 0 || inventoryCount > 0 || mobileUpdateCount > 0);
                    return (hasDependencies, setItemCount, requestCount, inventoryCount);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ItemRepository.CheckItemDependencies failed for ItemId={itemId}", ex);
                    return (false, 0, 0, 0);
                }
            }
        }

        /// <summary>
        /// Manually sets an item to inactive (non-async wrapper)
        /// </summary>
        public async Task SetItemInactive(int itemId)
        {
            await SetItemInactiveAsync(itemId);
        }

        /// <summary>
        /// Manually sets an item to inactive
        /// </summary>
        public async Task<bool> SetItemInactiveAsync(int itemId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                try
                {
                    string updateSql = "UPDATE Item SET Active = 0 WHERE ItemId = @ItemId";
                    using (var cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ItemRepository.SetItemInactiveAsync failed for ItemId={itemId}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Reactivates an inactive item
        /// </summary>
        public async Task<bool> SetItemActiveAsync(int itemId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                try
                {
                    string updateSql = "UPDATE Item SET Active = 1 WHERE ItemId = @ItemId";
                    using (var cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"ItemRepository.SetItemActiveAsync failed for ItemId={itemId}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Force deletes an item and ALL its relations (Inventory, Request, SetItem, ArchiveStatus)
        /// WARNING: This is a CASCADE DELETE. Use only for wrong data!
        /// </summary>
        public async Task<(bool Success, string Message)> ForceDeleteItemWithRelations(int itemId)
        {
            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string serialNumber = null;
                        using (var cmd = new SqlCommand("SELECT TOP (1) SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            var result = await cmd.ExecuteScalarAsync();
                            serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                        }

                        ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
                        {
                            ItemId = itemId,
                            SerialNumber = serialNumber,
                            Action = "Item Force Deleted",
                            ActionTime = DateTime.Now,
                            Status = "Completed",
                            ReferenceType = "Item",
                            ReferenceId = itemId,
                            Notes = "Force delete used to remove item and related records.",
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        });

                        // Step 0: Check if item affects inventory
                        bool affectsInventory = false;
                        string checkAffectsSql = "SELECT AffectsInventory FROM Item WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(checkAffectsSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            var result = await cmd.ExecuteScalarAsync();
                            affectsInventory = result != null && result != DBNull.Value && Convert.ToBoolean(result);
                        }

                        // Only restore stock if item affects inventory
                        if (affectsInventory)
                        {
                            // Step 1: Get all submitted requests that use this item to restore stock
                            string getSubmittedRequestsSql = @"
                                SELECT r.ReqId, r.Quantity
                                FROM Request r
                                WHERE r.ItemId = @ItemId AND r.Status = 'Submitted'";

                            var submittedRequests = new System.Collections.Generic.List<(int ReqId, int Quantity)>();

                            using (var cmd = new SqlCommand(getSubmittedRequestsSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        submittedRequests.Add((reader.GetInt32(0), reader.GetInt32(1)));
                                    }
                                }
                            }

                            // Step 2: Restore stock for submitted requests
                            foreach (var (reqId, quantity) in submittedRequests)
                            {
                                string restoreStockSql = @"
                                    UPDATE Item
                                    SET StockOnHand = StockOnHand + @Quantity
                                    WHERE ItemId = @ItemId";

                                using (var cmd = new SqlCommand(restoreStockSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            // Step 3: Get all SetItems that use this item to restore stock
                            string getSetItemsSql = @"
                                SELECT si.SetItemId, si.Quantity
                                FROM SetItem si
                                WHERE si.ItemId = @ItemId";

                            var setItems = new System.Collections.Generic.List<(int SetItemId, int Quantity)>();

                            using (var cmd = new SqlCommand(getSetItemsSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        // si.Quantity is DECIMAL(18,2); StockOnHand is INT, so round to whole units.
                                        setItems.Add((reader.GetInt32(0), Convert.ToInt32(reader.GetDecimal(1))));
                                    }
                                }
                            }

                            // Step 4: Restore stock for SetItems
                            foreach (var (setItemId, quantity) in setItems)
                            {
                                string restoreStockSql = @"
                                    UPDATE Item
                                    SET StockOnHand = StockOnHand + @Quantity
                                    WHERE ItemId = @ItemId";

                                using (var cmd = new SqlCommand(restoreStockSql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Step 5: Delete from Inventory table
                        string deleteInventorySql = "DELETE FROM Inventory WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteInventorySql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 5b: Delete from CartridgeRequestModel table (FK_CartridgeRequestModel_Request —
                        // references Request.ReqId with no cascade, so it must go before the Request delete).
                        string deleteCartridgeRequestModelSql = @"
                            DELETE FROM dbo.CartridgeRequestModel
                            WHERE ReqId IN (SELECT ReqId FROM Request WHERE ItemId = @ItemId)";
                        using (var cmd = new SqlCommand(deleteCartridgeRequestModelSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 5c: Delete from UnfulfilledCartridgeExchange table (FK_UnfulfilledCartridgeExchange_Request —
                        // references Request.ReqId with no cascade; rows can be Status='Fulfilled' and so invisible
                        // in the Cartridge Management Portal's default Pending filter, but still block the FK).
                        string deleteUnfulfilledExchangeSql = @"
                            DELETE FROM dbo.UnfulfilledCartridgeExchange
                            WHERE ReqId IN (SELECT ReqId FROM Request WHERE ItemId = @ItemId)";
                        using (var cmd = new SqlCommand(deleteUnfulfilledExchangeSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 5d: Null out EmptyCartridge/Set rows' ReqId (both nullable FKs to Request —
                        // preserve the history rows, same pattern as the SourceItemId clear in Step 8c below).
                        string clearEmptyCartridgeReqIdSql = @"
                            UPDATE dbo.EmptyCartridge SET ReqId = NULL
                            WHERE ReqId IN (SELECT ReqId FROM Request WHERE ItemId = @ItemId)";
                        using (var cmd = new SqlCommand(clearEmptyCartridgeReqIdSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string clearSetReqIdSql = @"
                            UPDATE dbo.[Set] SET ReqId = NULL
                            WHERE ReqId IN (SELECT ReqId FROM Request WHERE ItemId = @ItemId)";
                        using (var cmd = new SqlCommand(clearSetReqIdSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 5e: Delete any remaining Inventory rows tied to this item's requests via
                        // ReqId only (ItemId nullable per CK_Inventory_ItemOrRequest) — Step 5 above only
                        // caught rows keyed by ItemId directly.
                        string deleteInventoryByReqIdSql = @"
                            DELETE FROM Inventory
                            WHERE ReqId IN (SELECT ReqId FROM Request WHERE ItemId = @ItemId)";
                        using (var cmd = new SqlCommand(deleteInventoryByReqIdSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 6: Delete from Request table
                        string deleteRequestsSql = "DELETE FROM Request WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteRequestsSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 7: Delete from SetItem table
                        string deleteSetItemsSql = "DELETE FROM SetItem WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteSetItemsSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 8: Delete from ArchiveStatus table
                        string deleteArchiveSql = @"
                            DELETE FROM ArchiveStatus
                            WHERE EntityType = 'Item' AND EntityId = @ItemId";
                        using (var cmd = new SqlCommand(deleteArchiveSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 8b: Delete from Renewals table (includes archived rows — FK blocks
                        // Item delete even when IsArchived = 1, so all rows must go first)
                        string deleteRenewalsSql = "DELETE FROM dbo.Renewals WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteRenewalsSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 8c: Null out EmptyCartridge rows that reference this item as their
                        // refill source — FK is nullable, so we preserve the history row instead
                        // of deleting it.
                        string clearEmptyCartridgeSourceSql = "UPDATE dbo.EmptyCartridge SET SourceItemId = NULL WHERE SourceItemId = @ItemId";
                        using (var cmd = new SqlCommand(clearEmptyCartridgeSourceSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 8d: Delete from CartridgeMovement table (FK_CartridgeMovement_Item)
                        string deleteCartridgeMovementSql = "DELETE FROM dbo.CartridgeMovement WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteCartridgeMovementSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 8e: Delete from Cartridge table (FK_Cartridge_Item — Cartridge.ItemId
                        // is a 1:1 row keyed on the item itself, so it must go before the Item row).
                        string deleteCartridgeSql = "DELETE FROM dbo.Cartridge WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteCartridgeSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Step 9: Finally, delete the Item itself
                        string deleteItemSql = "DELETE FROM Item WHERE ItemId = @ItemId";
                        using (var cmd = new SqlCommand(deleteItemSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();

                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                return (false, "Item not found.");
                            }
                        }

                        transaction.Commit();
                        return (true, "Item and all relations deleted successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return (false, $"Error during force delete: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Searches active items by Name, Description, SerialNumber, or ModelNumber.
        /// Returns up to <paramref name="maxResults"/> results ordered by relevance (Name match first).
        /// </summary>


        /// <summary>

        /// Gets all cartridges marked as "For Refill" - inactive cartridges waiting to be refilled
        /// </summary>
        public System.Collections.Generic.List<Pages.ItemDto> GetCartridgesForRefill()
        {
            var cartridges = new System.Collections.Generic.List<Pages.ItemDto>();

            const string sql = @"
                SELECT 
                    i.ItemId,
                    i.Name,
                    i.Description,
                    i.Active,
                    i.Category,
                    i.SerialNumber,
                    i.ModelNumber,
                    i.CategoryId,
                    i.UnitOfMeasure,
                    i.StockOnHand,
                    i.ItemType,
                    i.Amount,
                    i.DateCreated,
                    i.CreatedBy,
                    i.DateModified,
                    i.ModifiedBy,
                    i.ConditionID,
                    c.ConditionName,
                    i.VendorId,
                    v.VendorName,
                    i.Remarks,
                    i.RefillStatus,
                    i.AffectsInventory,
                    i.AcquisitionType,
                    i.IsTrackedAsset,
                    u.Name AS CreatedByName,
                    um.Name AS ModifiedByName
                FROM dbo.Item i
                LEFT JOIN dbo.Condition c ON i.ConditionID = c.ConditionID
                LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorId
                LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
                LEFT JOIN dbo.[User] um ON i.ModifiedBy = um.UserId
                LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                WHERE i.Category = 'Cartridge'
                  AND i.RefillStatus = 'For Refill'
                  AND i.Active = 0
                  AND arch.EntityId IS NULL
                ORDER BY i.DateModified DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cartridges.Add(new Pages.ItemDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Active = reader.GetBoolean(reader.GetOrdinal("Active")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                            UnitOfMeasure = reader.GetString(reader.GetOrdinal("UnitOfMeasure")),
                            StockOnHand = reader.GetInt32(reader.GetOrdinal("StockOnHand")),
                            ItemType = reader.GetString(reader.GetOrdinal("ItemType")),
                            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                            DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                            DateModified = reader.IsDBNull(reader.GetOrdinal("DateModified")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateModified")),
                            ModifiedByUserId = reader.IsDBNull(reader.GetOrdinal("ModifiedBy")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ModifiedBy")),
                            ConditionId = reader.GetInt32(reader.GetOrdinal("ConditionID")),
                            ConditionName = reader.IsDBNull(reader.GetOrdinal("ConditionName")) ? null : reader.GetString(reader.GetOrdinal("ConditionName")),
                            VendorId = reader.IsDBNull(reader.GetOrdinal("VendorId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("VendorId")),
                            VendorName = reader.IsDBNull(reader.GetOrdinal("VendorName")) ? null : reader.GetString(reader.GetOrdinal("VendorName")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                            RefillStatus = reader.IsDBNull(reader.GetOrdinal("RefillStatus")) ? null : reader.GetString(reader.GetOrdinal("RefillStatus")),
                            AffectsInventory = reader.GetBoolean(reader.GetOrdinal("AffectsInventory")),
                            AcquisitionType = reader.IsDBNull(reader.GetOrdinal("AcquisitionType")) ? null : reader.GetString(reader.GetOrdinal("AcquisitionType")),
                            IsTrackedAsset = reader.GetBoolean(reader.GetOrdinal("IsTrackedAsset")),
                            CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName")) ? null : reader.GetString(reader.GetOrdinal("CreatedByName")),
                            ModifiedByName = reader.IsDBNull(reader.GetOrdinal("ModifiedByName")) ? null : reader.GetString(reader.GetOrdinal("ModifiedByName"))
                        });
                    }
                }
            }

            return cartridges;
        }

        /// <summary>
        /// Items in the CellPhone category, for the "Update Cellphone Details" editable grid.
        /// Category matching mirrors EditItemDialog.IsCellPhoneCategory (space-insensitive, case-insensitive).
        /// </summary>
        /// <summary>
        /// Loads the full editable field set for the given items, for the Bulk Edit Items grid.
        /// Mirrors the fields the single-item Edit Item dialog exposes, so the two stay in step.
        /// </summary>
        public System.Collections.Generic.List<Pages.ItemDto> GetItemsForBulkEdit(System.Collections.Generic.IEnumerable<int> itemIds)
        {
            var items = new System.Collections.Generic.List<Pages.ItemDto>();
            var ids = (itemIds ?? System.Linq.Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0) return items;

            // Ids come from the grid's own selection (never user text), so an inlined IN list is
            // safe here and keeps the round trip to one command.
            string idList = string.Join(",", ids);
            string sql = @"
                SELECT
                    i.ItemId, i.Name, i.Description, i.Category, i.CategoryId, i.ItemType,
                    i.SubType, i.SerialNumber, i.ModelNumber, i.UnitOfMeasure, i.StockOnHand,
                    i.Amount, i.ConditionId, i.VendorId, i.Remarks, i.LicenseNumber,
                    i.WarrantyYears, i.WarrantyStartDate, i.WarrantyEndDate, i.DatePurchased,
                    i.AcquisitionType, i.StartDate, i.EndDate,
                    i.CellPhoneNumber, i.IMEI1, i.IMEI2,
                    c.ConditionName, v.VendorName,
                    -- Part # is a dbo.Renewals column, not an Item column — the single-item
                    -- Edit Item dialog reads and writes it the same way.
                    (SELECT TOP 1 r.PartNumber
                       FROM dbo.Renewals r
                      WHERE r.ItemId = i.ItemId AND r.IsArchived = 0) AS PartNumber
                FROM dbo.Item i
                LEFT JOIN dbo.Condition c ON c.ConditionID = i.ConditionId
                LEFT JOIN dbo.Vendor    v ON v.VendorID    = i.VendorId
                WHERE i.ItemId IN (" + idList + @")
                ORDER BY i.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    string S(string c) { int o = reader.GetOrdinal(c); return reader.IsDBNull(o) ? null : reader.GetString(o); }
                    int I(string c) { int o = reader.GetOrdinal(c); return reader.IsDBNull(o) ? 0 : Convert.ToInt32(reader.GetValue(o)); }
                    int? NI(string c) { int o = reader.GetOrdinal(c); return reader.IsDBNull(o) ? (int?)null : Convert.ToInt32(reader.GetValue(o)); }
                    DateTime? D(string c) { int o = reader.GetOrdinal(c); return reader.IsDBNull(o) ? (DateTime?)null : reader.GetDateTime(o); }
                    decimal M(string c) { int o = reader.GetOrdinal(c); return reader.IsDBNull(o) ? 0m : Convert.ToDecimal(reader.GetValue(o)); }

                    while (reader.Read())
                    {
                        items.Add(new Pages.ItemDto
                        {
                            ItemId            = I("ItemId"),
                            Name              = S("Name"),
                            Description       = S("Description"),
                            Category          = S("Category"),
                            CategoryId        = I("CategoryId"),
                            ItemType          = S("ItemType"),
                            SubType           = S("SubType"),
                            SerialNumber      = S("SerialNumber"),
                            ModelNumber       = S("ModelNumber"),
                            UnitOfMeasure     = S("UnitOfMeasure"),
                            StockOnHand       = I("StockOnHand"),
                            Amount            = M("Amount"),
                            ConditionId       = I("ConditionId"),
                            ConditionName     = S("ConditionName"),
                            VendorId          = NI("VendorId"),
                            VendorName        = S("VendorName"),
                            Remarks           = S("Remarks"),
                            LicenseNumber     = S("LicenseNumber"),
                            WarrantyYears     = I("WarrantyYears"),
                            WarrantyStartDate = D("WarrantyStartDate"),
                            WarrantyEndDate   = D("WarrantyEndDate"),
                            DatePurchased     = D("DatePurchased"),
                            AcquisitionType   = S("AcquisitionType"),
                            StartDate         = D("StartDate"),
                            EndDate           = D("EndDate"),
                            CellPhoneNumber   = S("CellPhoneNumber"),
                            IMEI1             = S("IMEI1"),
                            IMEI2             = S("IMEI2"),
                            PartNumber        = S("PartNumber")
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Saves the Bulk Edit Items grid. Column set matches the single-item Edit Item dialog's
        /// UPDATE so both paths write the same fields. All rows go in one transaction — a failure
        /// on any row leaves every row unchanged.
        /// </summary>
        public int BulkUpdateItems(System.Collections.Generic.List<Pages.ItemDto> items, int modifiedByUserId)
        {
            if (items == null || items.Count == 0) return 0;

            const string sql = @"
                UPDATE dbo.Item
                SET Name              = @Name,
                    Description       = @Description,
                    ItemType          = @ItemType,
                    SubType           = @SubType,
                    SerialNumber      = @SerialNumber,
                    ModelNumber       = @ModelNumber,
                    UnitOfMeasure     = @UnitOfMeasure,
                    StockOnHand       = @StockOnHand,
                    Amount            = @Amount,
                    ConditionId       = @ConditionId,
                    VendorId          = @VendorId,
                    Remarks           = @Remarks,
                    LicenseNumber     = @LicenseNumber,
                    WarrantyYears     = @WarrantyYears,
                    WarrantyStartDate = @WarrantyStartDate,
                    WarrantyEndDate   = @WarrantyEndDate,
                    DatePurchased     = @DatePurchased,
                    AcquisitionType   = @AcquisitionType,
                    StartDate         = @StartDate,
                    EndDate           = @EndDate,
                    CellPhoneNumber   = @CellPhoneNumber,
                    IMEI1             = @IMEI1,
                    IMEI2             = @IMEI2,
                    DateModified      = @DateModified,
                    ModifiedBy        = @ModifiedBy
                WHERE ItemId = @ItemId";

            int affected = 0;
            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        foreach (var it in items)
                        {
                            using (var cmd = new SqlCommand(sql, con, tx))
                            {
                                object Txt(string v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v.Trim();

                                cmd.Parameters.AddWithValue("@ItemId", it.ItemId);
                                cmd.Parameters.AddWithValue("@Name", it.Name?.Trim() ?? string.Empty);
                                cmd.Parameters.AddWithValue("@Description", Txt(it.Description));
                                cmd.Parameters.AddWithValue("@ItemType", Txt(it.ItemType));
                                cmd.Parameters.AddWithValue("@SubType", Txt(it.SubType));
                                cmd.Parameters.AddWithValue("@SerialNumber", Txt(it.SerialNumber));
                                cmd.Parameters.AddWithValue("@ModelNumber", Txt(it.ModelNumber));
                                cmd.Parameters.AddWithValue("@UnitOfMeasure", Txt(it.UnitOfMeasure));
                                cmd.Parameters.AddWithValue("@StockOnHand", it.StockOnHand);
                                cmd.Parameters.AddWithValue("@Amount", it.Amount);
                                cmd.Parameters.AddWithValue("@ConditionId", it.ConditionId > 0 ? (object)it.ConditionId : DBNull.Value);
                                cmd.Parameters.AddWithValue("@VendorId", (object)it.VendorId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Remarks", Txt(it.Remarks));
                                cmd.Parameters.AddWithValue("@LicenseNumber", Txt(it.LicenseNumber));
                                cmd.Parameters.AddWithValue("@WarrantyYears", it.WarrantyYears);
                                cmd.Parameters.AddWithValue("@WarrantyStartDate", (object)it.WarrantyStartDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@WarrantyEndDate", (object)it.WarrantyEndDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@DatePurchased", (object)it.DatePurchased ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@AcquisitionType", Txt(it.AcquisitionType));
                                cmd.Parameters.AddWithValue("@StartDate", (object)it.StartDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@EndDate", (object)it.EndDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@CellPhoneNumber", Txt(it.CellPhoneNumber));
                                cmd.Parameters.AddWithValue("@IMEI1", Txt(it.IMEI1));
                                cmd.Parameters.AddWithValue("@IMEI2", Txt(it.IMEI2));
                                cmd.Parameters.AddWithValue("@DateModified", DateTime.UtcNow);
                                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                                affected += cmd.ExecuteNonQuery();
                            }

                            // Part # is stored on the item's live renewal row, not on dbo.Item —
                            // the same target the single-item Edit Item dialog writes to. An item
                            // with no live renewal updates 0 rows, which is not an error.
                            using (var partCmd = new SqlCommand(
                                @"UPDATE dbo.Renewals
                                     SET PartNumber = @PartNumber, ModifiedBy = @ModifiedBy, ModifiedAt = GETDATE()
                                   WHERE ItemId = @ItemId AND IsArchived = 0", con, tx))
                            {
                                partCmd.Parameters.AddWithValue("@PartNumber",
                                    string.IsNullOrWhiteSpace(it.PartNumber) ? (object)DBNull.Value : it.PartNumber.Trim());
                                partCmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                                partCmd.Parameters.AddWithValue("@ItemId", it.ItemId);
                                partCmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
            return affected;
        }

        public System.Collections.Generic.List<Pages.ItemDto> GetCellPhoneItems()
        {
            var items = new System.Collections.Generic.List<Pages.ItemDto>();

            const string sql = @"
                SELECT
                    i.ItemId,
                    i.Name,
                    i.Description,
                    i.ModelNumber,
                    i.SerialNumber,
                    i.CellPhoneNumber,
                    i.IMEI1,
                    i.IMEI2
                FROM dbo.Item i
                LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                WHERE REPLACE(i.Category, ' ', '') = 'CellPhone'
                  AND i.Active = 1
                  AND arch.EntityId IS NULL
                ORDER BY i.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new Pages.ItemDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            CellPhoneNumber = reader.IsDBNull(reader.GetOrdinal("CellPhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("CellPhoneNumber")),
                            IMEI1 = reader.IsDBNull(reader.GetOrdinal("IMEI1")) ? null : reader.GetString(reader.GetOrdinal("IMEI1")),
                            IMEI2 = reader.IsDBNull(reader.GetOrdinal("IMEI2")) ? null : reader.GetString(reader.GetOrdinal("IMEI2"))
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Persists edits made in the "Update Cellphone Details" grid. Only CellPhoneNumber/IMEI1/IMEI2
        /// are editable in that grid — everything else on dbo.Item is left untouched.
        /// </summary>
        /// <summary>
        /// Updates only CellPhoneNumber for existing active CellPhone items, keyed by IMEI1.
        /// Every supplied IMEI1 must resolve to exactly one item inside the same transaction;
        /// otherwise the transaction is rolled back and no row is changed.
        /// </summary>
        public int UpdateCellPhoneNumbers(IEnumerable<Yakult.Inventory.App.Pages.Item.CellPhoneNumberUpdate> updates, int modifiedByUserId)
        {
            var updateList = (updates ?? Enumerable.Empty<Yakult.Inventory.App.Pages.Item.CellPhoneNumberUpdate>())
                .Where(u => u != null)
                .Select(u => new Yakult.Inventory.App.Pages.Item.CellPhoneNumberUpdate
                {
                    IMEI1 = u.IMEI1 == null ? null : u.IMEI1.Trim(),
                    CellPhoneNumber = u.CellPhoneNumber == null ? null : u.CellPhoneNumber.Trim()
                })
                .ToList();

            if (updateList.Count == 0)
                return 0;

            var duplicate = updateList
                .Where(u => !string.IsNullOrWhiteSpace(u.IMEI1))
                .GroupBy(u => u.IMEI1, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException($"IMEI1 '{duplicate.Key}' appears more than once in the update list.");

            const string findSql = @"
                SELECT i.ItemId, i.SerialNumber, i.CellPhoneNumber
                FROM dbo.Item i WITH (UPDLOCK, HOLDLOCK)
                LEFT JOIN dbo.ArchiveStatus arch
                    ON arch.EntityType = 'Item'
                   AND arch.EntityId = i.ItemId
                   AND arch.IsArchived = 1
                WHERE REPLACE(i.Category, ' ', '') = 'CellPhone'
                  AND i.Active = 1
                  AND arch.EntityId IS NULL
                  AND LTRIM(RTRIM(i.IMEI1)) = @IMEI1";

            const string updateSql = @"
                UPDATE dbo.Item
                SET CellPhoneNumber = @CellPhoneNumber,
                    DateModified = @DateModified,
                    ModifiedBy = @ModifiedBy
                WHERE ItemId = @ItemId
                  AND LTRIM(RTRIM(IMEI1)) = @IMEI1
                  AND Active = 1
                  AND REPLACE(Category, ' ', '') = 'CellPhone'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.ArchiveStatus arch
                      WHERE arch.EntityType = 'Item'
                        AND arch.EntityId = @ItemId
                        AND arch.IsArchived = 1
                  )";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        int updatedCount = 0;
                        DateTime modifiedAt = DateTime.UtcNow;

                        foreach (var update in updateList)
                        {
                            if (string.IsNullOrWhiteSpace(update.IMEI1))
                                throw new InvalidOperationException("An IMEI1 value is blank.");
                            if (!Regex.IsMatch(update.IMEI1, @"^\d{15}$"))
                                throw new InvalidOperationException($"IMEI1 '{update.IMEI1}' must contain exactly 15 digits.");
                            if (string.IsNullOrWhiteSpace(update.CellPhoneNumber))
                                throw new InvalidOperationException($"Cellphone number for IMEI1 '{update.IMEI1}' is blank.");
                            if (update.CellPhoneNumber.Length > 50)
                                throw new InvalidOperationException($"Cellphone number must not exceed 50 characters (IMEI1 '{update.IMEI1}').");

                            int itemId;
                            string serialNumber;
                            string oldCellPhoneNumber;
                            using (var find = new SqlCommand(findSql, con, transaction))
                            {
                                find.Parameters.Add("@IMEI1", SqlDbType.VarChar, 50).Value = update.IMEI1;
                                using (var reader = find.ExecuteReader())
                                {
                                    if (!reader.Read())
                                        throw new InvalidOperationException($"IMEI1 '{update.IMEI1}' was not found in the active CellPhone items.");

                                    itemId = reader.GetInt32(0);
                                    serialNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                                    oldCellPhoneNumber = reader.IsDBNull(2) ? null : reader.GetString(2);

                                    if (reader.Read())
                                        throw new InvalidOperationException($"IMEI1 '{update.IMEI1}' matches more than one active CellPhone item.");
                                }
                            }

                            using (var command = new SqlCommand(updateSql, con, transaction))
                            {
                                command.Parameters.Add("@CellPhoneNumber", SqlDbType.VarChar, 50).Value = update.CellPhoneNumber;
                                command.Parameters.Add("@DateModified", SqlDbType.DateTime2).Value = modifiedAt;
                                command.Parameters.Add("@ModifiedBy", SqlDbType.Int).Value = modifiedByUserId;
                                command.Parameters.Add("@ItemId", SqlDbType.Int).Value = itemId;
                                command.Parameters.Add("@IMEI1", SqlDbType.VarChar, 50).Value = update.IMEI1;

                                if (command.ExecuteNonQuery() != 1)
                                    throw new InvalidOperationException($"The item for IMEI1 '{update.IMEI1}' changed before it could be updated.");
                            }

                            if (!ItemAuditTrailWriter.TryLogWithResult(con, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemId,
                                SerialNumber = serialNumber,
                                Action = "Cellphone Details Updated",
                                ActionTime = modifiedAt,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = itemId,
                                Notes = "CellPhoneNumber updated through bulk IMEI1 paste. " +
                                        $"Previous value: '{oldCellPhoneNumber ?? ""}', new value: '{update.CellPhoneNumber}'.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            }, out var auditError))
                            {
                                throw new InvalidOperationException($"Audit logging failed for IMEI1 '{update.IMEI1}'.", auditError);
                            }

                            updatedCount++;
                        }

                        transaction.Commit();
                        return updatedCount;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateCellPhoneDetails(System.Collections.Generic.List<Pages.ItemDto> items, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.Item
                SET Name = @Name,
                    Description = @Description,
                    ModelNumber = @ModelNumber,
                    SerialNumber = @SerialNumber,
                    CellPhoneNumber = @CellPhoneNumber,
                    IMEI1 = @IMEI1,
                    IMEI2 = @IMEI2,
                    DateModified = @DateModified,
                    ModifiedBy = @ModifiedBy
                WHERE ItemId = @ItemId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            using (var cmd = new SqlCommand(sql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                                cmd.Parameters.AddWithValue("@Name", item.Name?.Trim());
                                cmd.Parameters.AddWithValue("@Description", (object)item.Description?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ModelNumber", (object)item.ModelNumber?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@SerialNumber", (object)item.SerialNumber?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@CellPhoneNumber", (object)item.CellPhoneNumber?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@IMEI1", (object)item.IMEI1?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@IMEI2", (object)item.IMEI2?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@DateModified", DateTime.UtcNow);
                                cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
                                cmd.ExecuteNonQuery();
                            }

                            ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
                            {
                                ItemId = item.ItemId,
                                SerialNumber = (object)item.SerialNumber?.Trim() as string ?? null,
                                Action = "Cellphone Details Updated",
                                ActionTime = DateTime.Now,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = item.ItemId,
                                Notes = $"Cellphone details updated: {item.Name?.Trim() ?? string.Empty}.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
