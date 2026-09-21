using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Result of rendering a deployment-notification email for preview (not sent).
    /// <see cref="Error"/> is non-null when the preview could not be built.
    /// </summary>
    public sealed class DeploymentEmailPreviewResult
    {
        public string Subject  { get; set; }
        public string BodyHtml { get; set; }
        public bool   IsHtml   { get; set; }
        public string Error    { get; set; }
    }

    public class SetRepository
    {
        public static event Action<int, string> SetDispatched;
        public static event Action<int, bool> SetDispatchStateChanged;

        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public SetRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
            // This allows pages to instantiate repositories without crashing.
        }

        /// <summary>
        /// Gets the connection string, throwing if not configured.
        /// Call this at the start of each method that needs database access.
        /// </summary>
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Creates a new Set and returns the generated SetId.
        ///
        /// Note: "Status" (e.g., Pending/Dispatched) is stored in the Status column.
        ///       SetType is intentionally NOT set here so it can be derived from item ItemType
        ///       (Hardware, Software/License, Service) elsewhere.
        /// </summary>
        public async Task<int> CreateSetAsync(int createdByUserId, string remarks = null,
            DateTime? dispatchDate = null, string status = "Pending", int? distributorId = null)
        {
            const string sql = @"
        INSERT INTO dbo.[Set] (CreatedBy, CreatedAt, QRToken, Remarks, DispatchDate, Status)
        VALUES (@CreatedBy, GETDATE(), NEWID(), @Remarks, @DispatchDate, @Status);
        SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DispatchDate", (object)dispatchDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", status);

                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                int newSetId = Convert.ToInt32(result);

                // Dept-level distributor: applied only when the column exists in this
                // database (older DBs predate Migration_Set_AddDistributorId).
                // Dynamic SQL: static column refs fail compile-time binding on old DBs.
                if (distributorId.HasValue && distributorId.Value > 0)
                {
                    const string sqlDistributor = @"
                        IF COL_LENGTH('dbo.[Set]', 'DistributorId') IS NOT NULL
                        BEGIN
                            EXEC sp_executesql
                                N'UPDATE dbo.[Set] SET DistributorId = @DistributorId WHERE SetId = @SetId;',
                                N'@SetId INT, @DistributorId INT',
                                @SetId = @SetId, @DistributorId = @DistributorId;
                        END";
                    using (var distCmd = new SqlCommand(sqlDistributor, con))
                    {
                        distCmd.Parameters.AddWithValue("@SetId", newSetId);
                        distCmd.Parameters.AddWithValue("@DistributorId", distributorId.Value);
                        await distCmd.ExecuteNonQueryAsync();
                    }
                }

                ActivityLogger.Log(createdByUserId, ActivityLogger.Actions.Create,
                    "Set", newSetId,
                    $"Set #{newSetId} created");
                return newSetId;
            }
        }

        public async Task ApplySetItemUpgradesAsync(int setId,
            System.Collections.Generic.List<SetItemUpgradeDto> upgrades,
            int processedByUserId)
        {
            if (upgrades == null || upgrades.Count == 0)
                return;

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var up in upgrades)
                        {
                            // CRITICAL: Get ItemType for both old and new items to determine correct EntryType
                            string oldItemType = null;
                            string newItemType = null;

                            bool oldItemAffectsInventory = false;
                            bool newItemAffectsInventory = false;

                            string oldSerial = null;
                            string newSerial = null;

                            using (var cmd = new SqlCommand(@"SELECT ItemType, AffectsInventory, SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", up.OldItemId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        oldItemType = reader[0] == DBNull.Value ? null : Convert.ToString(reader[0]);
                                        oldItemAffectsInventory = reader[1] != DBNull.Value && Convert.ToBoolean(reader[1]);
                                        oldSerial = reader[2] == DBNull.Value ? null : Convert.ToString(reader[2]);
                                    }
                                }
                            }

                            using (var cmd = new SqlCommand(@"SELECT ItemType, AffectsInventory, SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", up.NewItemId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        newItemType = reader[0] == DBNull.Value ? null : Convert.ToString(reader[0]);
                                        newItemAffectsInventory = reader[1] != DBNull.Value && Convert.ToBoolean(reader[1]);
                                        newSerial = reader[2] == DBNull.Value ? null : Convert.ToString(reader[2]);
                                    }
                                }
                            }

                            // CRITICAL: Only create TRANSACTIONAL Inventory records for items that affect inventory
                            // This is TRANSACTIONAL inventory (SetId is always available in this flow)
                            // Baseline inventory already exists from Item creation
                            if (oldItemAffectsInventory)
                            {
                                // Determine EntryType for returning old item (positive quantity)
                                string oldItemEntryType = Helpers.InventoryHelper.DetermineEntryType(oldItemAffectsInventory, up.Quantity);

                                // Return old item to inventory (TRANSACTIONAL)
                                using (var cmd = new SqlCommand(@"INSERT INTO dbo.Inventory
(Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active)
VALUES (@Description, @EntryType, @Quantity, SYSDATETIME(), @PostedBy, @ReqId, @ItemId, @SetId, 1);",
                                    conn, tx))
                                {
                                    var desc = $"Upgrade pullout (spare to inventory) for set {setId}, request #{up.ReqId}";
                                    cmd.Parameters.AddWithValue("@Description", desc);
                                    cmd.Parameters.AddWithValue("@EntryType", oldItemEntryType);
                                    cmd.Parameters.AddWithValue("@Quantity", up.Quantity);
                                    cmd.Parameters.AddWithValue("@PostedBy", processedByUserId);
                                    cmd.Parameters.AddWithValue("@ReqId", up.ReqId);
                                    cmd.Parameters.AddWithValue("@ItemId", up.OldItemId);
                                    cmd.Parameters.AddWithValue("@SetId", setId);

                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            // =====================================================
                            // Stock Restoration at Fulfillment (Item Upgrade/Return)
                            // =====================================================
                            // When an item is upgraded, the old item is returned to inventory
                            // This increases stock of the old item
                            // This is CORRECT - happens at fulfillment, not request creation
                            // =====================================================
                            using (var cmd = new SqlCommand(@"UPDATE dbo.Item
SET StockOnHand = ISNULL(StockOnHand, 0) + @Quantity,
    DateModified = SYSDATETIME(),
    ModifiedBy = @ModifiedBy
WHERE ItemId = @ItemId;",
                                conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@Quantity", up.Quantity);
                                cmd.Parameters.AddWithValue("@ModifiedBy", processedByUserId);
                                cmd.Parameters.AddWithValue("@ItemId", up.OldItemId);

                                await cmd.ExecuteNonQueryAsync();
                            }

                            ItemAuditTrailWriter.TryLog(conn, tx, new ItemAuditTrailDto
                            {
                                ItemId = up.OldItemId,
                                SerialNumber = oldSerial,
                                Action = "Item Upgraded - Old Item Returned",
                                ActionTime = DateTime.Now,
                                Direction = "IN",
                                Status = "Completed",
                                ReferenceType = "Request",
                                ReferenceId = up.ReqId,
                                Notes = $"Old item returned to stock after upgrade, qty {up.Quantity}.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            // CRITICAL: Only create TRANSACTIONAL Inventory records for items that affect inventory
                            // This is TRANSACTIONAL inventory (SetId is always available in this flow)
                            // Baseline inventory already exists from Item creation
                            if (newItemAffectsInventory)
                            {
                                // Determine EntryType for allocating new item (negative quantity)
                                string newItemEntryType = Helpers.InventoryHelper.DetermineEntryType(newItemAffectsInventory, -up.Quantity);

                                // Allocate new item from inventory (TRANSACTIONAL)
                                using (var cmd = new SqlCommand(@"INSERT INTO dbo.Inventory
(Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active)
VALUES (@Description, @EntryType, @Quantity, SYSDATETIME(), @PostedBy, @ReqId, @ItemId, @SetId, 1);",
                                    conn, tx))
                                {
                                    var desc = $"Upgrade replacement (allocated from inventory) for set {setId}, request #{up.ReqId}";
                                    cmd.Parameters.AddWithValue("@Description", desc);
                                    cmd.Parameters.AddWithValue("@EntryType", newItemEntryType);
                                    cmd.Parameters.AddWithValue("@Quantity", up.Quantity);
                                    cmd.Parameters.AddWithValue("@PostedBy", processedByUserId);
                                    cmd.Parameters.AddWithValue("@ReqId", up.ReqId);
                                    cmd.Parameters.AddWithValue("@ItemId", up.NewItemId);
                                    cmd.Parameters.AddWithValue("@SetId", setId);

                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            // =====================================================
                            // CRITICAL: Stock Deduction at Fulfillment (CORRECT LOCATION)
                            // =====================================================
                            // This is the ONLY place where stock should be deducted
                            // Request creation does NOT deduct stock (inventory read-only)
                            // Stock is deducted here when Set is created and deployed
                            //
                            // Guard: Only deduct if stock hasn't already been deducted for this item
                            // The ISNULL check prevents negative stock from double deduction
                            // =====================================================
                            using (var cmd = new SqlCommand(@"UPDATE dbo.Item
SET StockOnHand = CASE
    WHEN ISNULL(StockOnHand, 0) >= @Quantity THEN StockOnHand - @Quantity
    ELSE 0  -- Guard: prevent negative stock from double deduction
END,
    DateModified = SYSDATETIME(),
    ModifiedBy = @ModifiedBy
WHERE ItemId = @ItemId
  AND ISNULL(StockOnHand, 0) >= 0;  -- Guard: only update if stock is valid",
                                conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@Quantity", up.Quantity);
                                cmd.Parameters.AddWithValue("@ModifiedBy", processedByUserId);
                                cmd.Parameters.AddWithValue("@ItemId", up.NewItemId);

                                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                                // Guard: Log warning if stock deduction didn't happen (already deducted or invalid)
                                if (rowsAffected == 0)
                                {
                                    // Stock was already deducted or item doesn't exist
                                    // This prevents double deduction if called multiple times
                                    System.Diagnostics.Debug.WriteLine(
                                        $"WARNING: Stock deduction skipped for ItemId {up.NewItemId} - already deducted or insufficient stock");
                                }
                            }

                            ItemAuditTrailWriter.TryLog(conn, tx, new ItemAuditTrailDto
                            {
                                ItemId = up.NewItemId,
                                SerialNumber = newSerial,
                                Action = "Item Upgraded - New Item Allocated",
                                ActionTime = DateTime.Now,
                                Direction = "OUT",
                                Status = "Completed",
                                ReferenceType = "Request",
                                ReferenceId = up.ReqId,
                                Notes = $"New item allocated for upgrade, qty {up.Quantity}.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            // Update request to point to new item
                            using (var cmd = new SqlCommand(@"UPDATE dbo.Request
SET ItemId = @NewItemId
WHERE ReqId = @ReqId;",
                                conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@NewItemId", up.NewItemId);
                                cmd.Parameters.AddWithValue("@ReqId", up.ReqId);

                                await cmd.ExecuteNonQueryAsync();
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
        }

        /// <summary>Converts a raw Advanced Filters textbox value into a SQL LIKE parameter
        /// (wrapped in %...% wildcards), or DBNull when blank so the corresponding
        /// "@Param IS NULL OR column LIKE @Param" WHERE clause is a no-op.</summary>
        private static object ToLikeParam(string raw) =>
            string.IsNullOrWhiteSpace(raw) ? (object)DBNull.Value : $"%{raw.Trim()}%";

        /// <summary>
        /// Gets all sets with item count
        /// </summary>
        public async Task<List<SetDto>> GetAllSetsAsync(
            bool includeArchived = false, bool includeInvoices = false,
            string setCodeFilter = null, string documentNumberFilter = null, string companyFilter = null,
            string departmentFilter = null, string branchFilter = null, string employeeFilter = null,
            string referenceCodeFilter = null, string parentTagFilter = null, string reqIdFilter = null)
        {
            var sets = new List<SetDto>();

            string sql = @"
        SELECT
            s.SetId,
            s.SetCode,
            ISNULL(s.IsInvoice, 0) AS IsInvoice,
            CASE WHEN arch_set.EntityId IS NOT NULL OR arch_req.EntityId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived,
            s.CreatedBy,
            u.Name AS CreatedByName,
            s.CreatedAt,
            s.QRToken,
            s.QRImagePath,
            CASE WHEN s.QRImageData IS NOT NULL THEN 1 ELSE 0 END AS QRImageDataPresent,
            s.QRData,
            s.Remarks,
            s.DocumentNumber,
            s.ReferenceNumber,
            s.StartDate,
            s.EndDate,
            DATEDIFF(DAY, CAST(GETDATE() AS date), ISNULL(s.EndDate, DATEADD(YEAR, 5, s.CreatedAt))) AS DaysLeft,
            c.Name AS Company,
            s.DispatchDate,
            s.SetType,
            s.Status,
            s.UpgradeReason,
            s.ComputerName,
            s.IPAddress,
            (SELECT COUNT(*) FROM dbo.SetItem si WHERE si.SetId = s.SetId) AS ItemCount,
            (SELECT COUNT(*) FROM dbo.SetImages si2 WHERE si2.SetId = s.SetId) AS ImageCount,
            s.DateRequested,
            -- Employee info via OUTER APPLY (single lookup replacing 10 correlated subqueries)
            emp.EmpId                          AS CurrentEmployeeId,
            emp.Name                           AS CurrentEmployeeName,
            emp.EmployeeNumber                 AS CurrentEmployeeNumber,
            COALESCE(br.BranchId, rb.BranchId) AS CurrentBranchId,
            COALESCE(br.Name,     rb.Name)     AS CurrentBranchName,
            COALESCE(dp.DeptId,   rd.DeptId)   AS CurrentDepartmentId,
            COALESCE(dp.Name,     rd.Name)     AS CurrentDepartmentName,
            COALESCE(co.ComId,    rc.ComId)    AS CurrentCompanyId,
            COALESCE(co.Name,     rc.Name)     AS CurrentCompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
        LEFT JOIN dbo.Request r ON s.SetId = r.SetId
        LEFT JOIN dbo.Company c ON s.ComId = c.ComId
        LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId AND arch_set.IsArchived = 1
        LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId AND arch_req.IsArchived = 1
        OUTER APPLY (
            SELECT TOP 1 r2.EmpId, r2.ComId, r2.DeptId, r2.BranchId
            FROM   dbo.Request r2
            WHERE  r2.SetId = s.SetId
            ORDER  BY r2.ReqId
        ) top_req
        LEFT JOIN dbo.Employee   emp ON emp.EmpId   = top_req.EmpId
        LEFT JOIN dbo.Branch     br  ON br.BranchId = emp.BranchId
        LEFT JOIN dbo.Department dp  ON dp.DeptId   = emp.DeptId
        LEFT JOIN dbo.Company    co  ON co.ComId    = emp.ComId
        LEFT JOIN dbo.Company    rc  ON rc.ComId    = top_req.ComId
        LEFT JOIN dbo.Department rd  ON rd.DeptId   = top_req.DeptId
        LEFT JOIN dbo.Branch     rb  ON rb.BranchId = top_req.BranchId
        WHERE (@IncludeArchived = 1 OR (arch_set.EntityId IS NULL AND (r.ReqId IS NULL OR arch_req.EntityId IS NULL)))
          AND (@IncludeInvoices = 1 OR ISNULL(s.IsInvoice, 0) = 0)
          AND (@SetCodeFilter IS NULL OR s.SetCode LIKE @SetCodeFilter)
          AND (@DocumentNumberFilter IS NULL OR s.DocumentNumber LIKE @DocumentNumberFilter)
          AND (@CompanyFilter IS NULL OR c.Name LIKE @CompanyFilter)
          AND (@DepartmentFilter IS NULL OR COALESCE(dp.Name, rd.Name) LIKE @DepartmentFilter)
          AND (@BranchFilter IS NULL OR COALESCE(br.Name, rb.Name) LIKE @BranchFilter)
          AND (@EmployeeFilter IS NULL OR emp.Name LIKE @EmployeeFilter)
          AND (@ReqIdFilter IS NULL OR CAST(s.ReqId AS NVARCHAR(20)) LIKE @ReqIdFilter)
          AND (@ReferenceCodeFilter IS NULL OR EXISTS (
                SELECT 1 FROM dbo.SetItem si_ref
                WHERE si_ref.SetId = s.SetId AND si_ref.ReferenceCode LIKE @ReferenceCodeFilter))
          AND (@ParentTagFilter IS NULL OR EXISTS (
                SELECT 1 FROM dbo.SetItem si_tag
                LEFT JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si_tag.ParentTagGroupId
                WHERE si_tag.SetId = s.SetId AND ptg.Label LIKE @ParentTagFilter))
        GROUP BY s.SetId, s.SetCode, ISNULL(s.IsInvoice, 0), arch_set.EntityId, arch_req.EntityId, s.CreatedBy, u.Name, s.CreatedAt,
                 s.QRToken, s.QRImagePath, CASE WHEN s.QRImageData IS NOT NULL THEN 1 ELSE 0 END, s.QRData, s.Remarks,
                 s.DocumentNumber, s.ReferenceNumber, s.StartDate, s.EndDate,
                 c.Name,
                 s.DispatchDate, s.SetType, s.Status, s.UpgradeReason,
                 s.ComputerName, s.IPAddress, s.DateRequested,
                 emp.EmpId, emp.Name, emp.EmployeeNumber,
                 br.BranchId, br.Name,
                 dp.DeptId, dp.Name,
                 co.ComId, co.Name,
                 rc.ComId, rc.Name,
                 rd.DeptId, rd.Name,
                 rb.BranchId, rb.Name
        ORDER BY " + (includeInvoices ? "ISNULL(s.IsInvoice, 0) DESC, " : "") + @"s.SetId DESC";


            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
            {
                cmd.Parameters.AddWithValue("@IncludeArchived", includeArchived ? 1 : 0);
                cmd.Parameters.AddWithValue("@IncludeInvoices", includeInvoices ? 1 : 0);
                cmd.Parameters.AddWithValue("@SetCodeFilter", ToLikeParam(setCodeFilter));
                cmd.Parameters.AddWithValue("@DocumentNumberFilter", ToLikeParam(documentNumberFilter));
                cmd.Parameters.AddWithValue("@CompanyFilter", ToLikeParam(companyFilter));
                cmd.Parameters.AddWithValue("@DepartmentFilter", ToLikeParam(departmentFilter));
                cmd.Parameters.AddWithValue("@BranchFilter", ToLikeParam(branchFilter));
                cmd.Parameters.AddWithValue("@EmployeeFilter", ToLikeParam(employeeFilter));
                cmd.Parameters.AddWithValue("@ReqIdFilter", ToLikeParam(reqIdFilter));
                cmd.Parameters.AddWithValue("@ReferenceCodeFilter", ToLikeParam(referenceCodeFilter));
                cmd.Parameters.AddWithValue("@ParentTagFilter", ToLikeParam(parentTagFilter));

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        sets.Add(new SetDto
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                                ? $"SET-{reader.GetInt32(reader.GetOrdinal("SetId"))}"
                                : reader.GetString(reader.GetOrdinal("SetCode")),
                            IsInvoice = Convert.ToInt32(reader["IsInvoice"]) == 1,
                            IsArchived = Convert.ToInt32(reader["IsArchived"]) == 1,
                            CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                            CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName"))
                                ? "Unknown"
                                : reader.GetString(reader.GetOrdinal("CreatedByName")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            QRToken = reader.IsDBNull(reader.GetOrdinal("QRToken"))
                                ? Guid.Empty
                                : reader.GetGuid(reader.GetOrdinal("QRToken")),
                            QRImagePath = reader.IsDBNull(reader.GetOrdinal("QRImagePath"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRImagePath")),
                            QRImageDataPresent = Convert.ToInt32(reader["QRImageDataPresent"]) == 1,
                            QRData = reader.IsDBNull(reader.GetOrdinal("QRData"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRData")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Remarks")),
                            DocumentNumber = reader.IsDBNull(reader.GetOrdinal("DocumentNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DocumentNumber")),
                            ReferenceNumber = reader.IsDBNull(reader.GetOrdinal("ReferenceNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ReferenceNumber")),
                            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            DaysLeft = reader.IsDBNull(reader.GetOrdinal("DaysLeft"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("DaysLeft")),
                            Company = reader.IsDBNull(reader.GetOrdinal("Company"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Company")),
                            DispatchDate = reader.IsDBNull(reader.GetOrdinal("DispatchDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("DispatchDate")),
                            SetType = reader.IsDBNull(reader.GetOrdinal("SetType"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SetType")),
                            Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Status")),
                            UpgradeReason = reader.IsDBNull(reader.GetOrdinal("UpgradeReason"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpgradeReason")),
                            ComputerName = reader.IsDBNull(reader.GetOrdinal("ComputerName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ComputerName")),
                            IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("IPAddress")),
                            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
                            ImageCount = reader.IsDBNull(reader.GetOrdinal("ImageCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("ImageCount")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            // Employee and organizational information
                            CurrentEmployeeId = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentEmployeeId")),
                            CurrentEmployeeName = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentEmployeeName")),
                            CurrentEmployeeNumber = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentEmployeeNumber")),
                            CurrentBranchId = reader.IsDBNull(reader.GetOrdinal("CurrentBranchId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentBranchId")),
                            CurrentBranchName = reader.IsDBNull(reader.GetOrdinal("CurrentBranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentBranchName")),
                            CurrentDepartmentId = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentDepartmentId")),
                            CurrentDepartmentName = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentDepartmentName")),
                            CurrentCompanyId = reader.IsDBNull(reader.GetOrdinal("CurrentCompanyId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentCompanyId")),
                            CurrentCompanyName = reader.IsDBNull(reader.GetOrdinal("CurrentCompanyName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentCompanyName"))
                        });
                    }
                }
            }

            return sets;
        }

        /// <summary>
        /// Gets only cartridge-related sets (SetType='Cartridge' OR contains items with Category='Cartridge').
        /// </summary>
        public async Task<List<SetDto>> GetCartridgeSetsAsync()
        {
            var sets = new List<SetDto>();

            const string sql = @"
        SELECT
            s.SetId,
            s.SetCode,
            s.CreatedBy,
            u.Name AS CreatedByName,
            s.CreatedAt,
            s.QRToken,
            s.QRImagePath,
            CASE WHEN s.QRImageData IS NOT NULL THEN 1 ELSE 0 END AS QRImageDataPresent,
            s.QRData,
            s.Remarks,
            s.DocumentNumber,
            s.ReferenceNumber,
            s.StartDate,
            s.EndDate,
            DATEDIFF(DAY, CAST(GETDATE() AS date), ISNULL(s.EndDate, DATEADD(YEAR, 5, s.CreatedAt))) AS DaysLeft,
            c.Name AS Company,
            s.DispatchDate,
            s.SetType,
            s.Status,
            s.UpgradeReason,
            s.ComputerName,
            s.IPAddress,
            (SELECT COUNT(*) FROM dbo.SetItem si WHERE si.SetId = s.SetId) AS ItemCount,
            (SELECT COUNT(*) FROM dbo.SetImages si2 WHERE si2.SetId = s.SetId) AS ImageCount,
            -- Employee info via OUTER APPLY (single lookup replacing 10 correlated subqueries)
            emp.EmpId                          AS CurrentEmployeeId,
            emp.Name                           AS CurrentEmployeeName,
            emp.EmployeeNumber                 AS CurrentEmployeeNumber,
            COALESCE(br.BranchId, rb.BranchId) AS CurrentBranchId,
            COALESCE(br.Name,     rb.Name)     AS CurrentBranchName,
            COALESCE(dp.DeptId,   rd.DeptId)   AS CurrentDepartmentId,
            COALESCE(dp.Name,     rd.Name)     AS CurrentDepartmentName,
            COALESCE(co.ComId,    rc.ComId)    AS CurrentCompanyId,
            COALESCE(co.Name,     rc.Name)     AS CurrentCompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
        LEFT JOIN dbo.Request r ON s.SetId = r.SetId
        LEFT JOIN dbo.Company c ON s.ComId = c.ComId
        LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId AND arch_set.IsArchived = 1
        LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId AND arch_req.IsArchived = 1
        OUTER APPLY (
            SELECT TOP 1 r2.EmpId, r2.ComId, r2.DeptId, r2.BranchId
            FROM   dbo.Request r2
            WHERE  r2.SetId = s.SetId
            ORDER  BY r2.ReqId
        ) top_req
        LEFT JOIN dbo.Employee   emp ON emp.EmpId   = top_req.EmpId
        LEFT JOIN dbo.Branch     br  ON br.BranchId = emp.BranchId
        LEFT JOIN dbo.Department dp  ON dp.DeptId   = emp.DeptId
        LEFT JOIN dbo.Company    co  ON co.ComId    = emp.ComId
        LEFT JOIN dbo.Company    rc  ON rc.ComId    = top_req.ComId
        LEFT JOIN dbo.Department rd  ON rd.DeptId   = top_req.DeptId
        LEFT JOIN dbo.Branch     rb  ON rb.BranchId = top_req.BranchId
        WHERE arch_set.EntityId IS NULL
          AND (r.ReqId IS NULL OR arch_req.EntityId IS NULL)
          AND (
            s.SetType = 'Cartridge'
            OR EXISTS (
                SELECT 1
                FROM dbo.Request r_inner
                INNER JOIN dbo.Item i ON r_inner.ItemId = i.ItemId
                WHERE r_inner.SetId = s.SetId
                  AND (i.ItemType = 'Cartridge' OR i.Category = 'Cartridge')
            )
          )
        GROUP BY s.SetId, s.SetCode, s.CreatedBy, u.Name, s.CreatedAt,
                 s.QRToken, s.QRImagePath, CASE WHEN s.QRImageData IS NOT NULL THEN 1 ELSE 0 END, s.QRData, s.Remarks,
                 s.DocumentNumber, s.ReferenceNumber, s.StartDate, s.EndDate,
                 c.Name,
                 s.DispatchDate, s.SetType, s.Status, s.UpgradeReason,
                 s.ComputerName, s.IPAddress,
                 emp.EmpId, emp.Name, emp.EmployeeNumber,
                 br.BranchId, br.Name,
                 dp.DeptId, dp.Name,
                 co.ComId, co.Name,
                 rc.ComId, rc.Name,
                 rd.DeptId, rd.Name,
                 rb.BranchId, rb.Name
        ORDER BY s.SetId DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        sets.Add(new SetDto
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                                ? $"SET-{reader.GetInt32(reader.GetOrdinal("SetId"))}"
                                : reader.GetString(reader.GetOrdinal("SetCode")),
                            CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                            CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName"))
                                ? "Unknown"
                                : reader.GetString(reader.GetOrdinal("CreatedByName")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            QRToken = reader.IsDBNull(reader.GetOrdinal("QRToken"))
                                ? Guid.Empty
                                : reader.GetGuid(reader.GetOrdinal("QRToken")),
                            QRImagePath = reader.IsDBNull(reader.GetOrdinal("QRImagePath"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRImagePath")),
                            QRImageDataPresent = Convert.ToInt32(reader["QRImageDataPresent"]) == 1,
                            QRData = reader.IsDBNull(reader.GetOrdinal("QRData"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRData")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Remarks")),
                            DocumentNumber = reader.IsDBNull(reader.GetOrdinal("DocumentNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DocumentNumber")),
                            ReferenceNumber = reader.IsDBNull(reader.GetOrdinal("ReferenceNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ReferenceNumber")),
                            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            DaysLeft = reader.IsDBNull(reader.GetOrdinal("DaysLeft"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("DaysLeft")),
                            Company = reader.IsDBNull(reader.GetOrdinal("Company"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Company")),
                            DispatchDate = reader.IsDBNull(reader.GetOrdinal("DispatchDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("DispatchDate")),
                            SetType = reader.IsDBNull(reader.GetOrdinal("SetType"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SetType")),
                            Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Status")),
                            UpgradeReason = reader.IsDBNull(reader.GetOrdinal("UpgradeReason"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpgradeReason")),
                            ComputerName = reader.IsDBNull(reader.GetOrdinal("ComputerName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ComputerName")),
                            IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("IPAddress")),
                            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
                            ImageCount = reader.IsDBNull(reader.GetOrdinal("ImageCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("ImageCount")),
                            // Employee and organizational information
                            CurrentEmployeeId = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentEmployeeId")),
                            CurrentEmployeeName = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentEmployeeName")),
                            CurrentEmployeeNumber = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentEmployeeNumber")),
                            CurrentBranchId = reader.IsDBNull(reader.GetOrdinal("CurrentBranchId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentBranchId")),
                            CurrentBranchName = reader.IsDBNull(reader.GetOrdinal("CurrentBranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentBranchName")),
                            CurrentDepartmentId = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentDepartmentId")),
                            CurrentDepartmentName = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentDepartmentName")),
                            CurrentCompanyId = reader.IsDBNull(reader.GetOrdinal("CurrentCompanyId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("CurrentCompanyId")),
                            CurrentCompanyName = reader.IsDBNull(reader.GetOrdinal("CurrentCompanyName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CurrentCompanyName"))
                        });
                    }
                }
            }

            return sets;
        }

        /// <summary>Method to Generate QRData instead of Adding in on the Database to save space
        // Add this new method to SetRepository.cs
        public async Task<string> GenerateQRDataJsonAsync(int setId)
        {
            const string sql = @"
        SELECT 
            s.SetCode,
            s.DispatchDate,
            s.SetType,
            e.Name AS EmployeeName,
            (
                SELECT 
                    i.Name AS ItemName,
                    i.Category,
                    r.Quantity,
                    i.ModelNumber,
                    i.SerialNumber
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId = s.SetId
                FOR JSON PATH
            ) AS ItemsJson
        FROM dbo.[Set] s
        LEFT JOIN dbo.Request r ON s.SetId = r.SetId
        LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
        WHERE s.SetId = @SetId
        GROUP BY s.SetCode, s.DispatchDate, s.SetType, e.Name, s.SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var setCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                            ? "N/A"
                            : reader.GetString(reader.GetOrdinal("SetCode"));
                        var dispatchDate = reader.IsDBNull(reader.GetOrdinal("DispatchDate"))
                            ? "Not Set"
                            : reader.GetDateTime(reader.GetOrdinal("DispatchDate")).ToString("yyyy-MM-dd");
                        var setType = reader.IsDBNull(reader.GetOrdinal("SetType"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("SetType"));
                        var employeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName"))
                            ? "Unknown"
                            : reader.GetString(reader.GetOrdinal("EmployeeName"));
                        var itemsJson = reader.IsDBNull(reader.GetOrdinal("ItemsJson"))
                            ? "[]"
                            : reader.GetString(reader.GetOrdinal("ItemsJson"));

                        // Build complete JSON
                        return $@"{{
                    ""SetCode"": ""{setCode}"",
                    ""DispatchDate"": ""{dispatchDate}"",
                    ""SetType"": ""{setType}"",
                    ""EmployeeName"": ""{employeeName}"",
                    ""Items"": {itemsJson}
                }}";
                    }
                }
            }

            return null;
        }

        public async Task UpdateUpgradeReasonAsync(int setId, string upgradeReason)
        {
            const string sql = @"UPDATE dbo.[Set]
SET UpgradeReason = @UpgradeReason
WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@UpgradeReason", (object)upgradeReason ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Gets a single set by ID
        /// </summary>
        public async Task<SetDto> GetSetByIdAsync(int setId)
        {
            // Distributor columns may not exist on DBs predating their migrations.
            // Static SQL binds columns at compile time, so the fragments are dynamic.
            bool hasSetDistributor;
            bool hasRequestDistributorForSet;
            using (var guardCon = new SqlConnection(GetConnectionString()))
            {
                await guardCon.OpenAsync();
                using (var guardCmd = new SqlCommand(
                    "SELECT CASE WHEN COL_LENGTH('dbo.[Set]', 'DistributorId') IS NULL THEN 0 ELSE 1 END, " +
                    "CASE WHEN COL_LENGTH('dbo.Request', 'DistributorId') IS NULL THEN 0 ELSE 1 END",
                    guardCon))
                using (var guardReader = await guardCmd.ExecuteReaderAsync())
                {
                    await guardReader.ReadAsync();
                    hasSetDistributor = guardReader.GetInt32(0) == 1;
                    hasRequestDistributorForSet = guardReader.GetInt32(1) == 1;
                }
            }
            string setDistributorSelect = hasSetDistributor
                ? "s.DistributorId,\n                    dist.Name AS DistributorName,"
                : "CAST(NULL AS INT) AS DistributorId,\n                    CAST(NULL AS NVARCHAR(200)) AS DistributorName,";
            string setDistributorJoin = hasSetDistributor
                ? "LEFT JOIN dbo.Distributor dist ON dist.DistributorId = s.DistributorId"
                : string.Empty;
            string setDistributorGroupBy = hasSetDistributor ? ",\n                          s.DistributorId, dist.Name" : string.Empty;

            string sql = @"
                SELECT
                    s.SetId,
                    s.SetCode,
                    ISNULL(s.IsInvoice, 0) AS IsInvoice,
                    s.CreatedBy,
                    u.Name AS CreatedByName,
                    s.CreatedAt,
                    s.DateRequested,
                    s.QRToken,
                    s.QRImagePath,
                    s.QRImageData,
                    s.QRData,
                    s.Remarks,
                    s.DocumentNumber,
                    s.ReferenceNumber,
                    s.StartDate,
                    s.EndDate,
                    c.Name AS Company,
                    " + setDistributorSelect + @"
                    s.DispatchDate,
                    s.SetType,
                    s.Status,
                    s.Subtotal,
                    s.VatAmount,
                    s.DiscountAmount,
                    s.WhtAmount,
                    s.TotalAmountDue,
                    s.UpgradeReason,
                    s.ComputerName,
                    s.IPAddress,
                    s.IssuedBrandNewQty,
                    s.IssuedRefilledQty,
                    s.ReceivedById,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    (SELECT COUNT(*) FROM dbo.SetItem si WHERE si.SetId = s.SetId) AS ItemCount,
                    (SELECT COUNT(*) FROM dbo.SetImages si2 WHERE si2.SetId = s.SetId) AS ImageCount
                FROM dbo.[Set] s
                LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                LEFT JOIN dbo.Request r ON s.SetId = r.SetId
                LEFT JOIN dbo.Company c ON s.ComId = c.ComId
                " + setDistributorJoin + @"
                LEFT JOIN dbo.Employee recv_emp ON recv_emp.EmpId = s.ReceivedById
                WHERE s.SetId = @SetId
                GROUP BY s.SetId, s.SetCode, ISNULL(s.IsInvoice, 0), s.CreatedBy, u.Name, s.CreatedAt, s.DateRequested,
                         s.QRToken, s.QRImagePath, s.QRImageData, s.QRData, s.Remarks,
                         s.DocumentNumber, s.ReferenceNumber, s.StartDate, s.EndDate,
                         c.Name" + setDistributorGroupBy + @",
                         s.DispatchDate, s.SetType, s.Status,
                         s.Subtotal, s.VatAmount, s.DiscountAmount, s.WhtAmount, s.TotalAmountDue,
                         s.UpgradeReason, s.ComputerName, s.IPAddress, s.IssuedBrandNewQty, s.IssuedRefilledQty,
                         s.ReceivedById, recv_emp.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new SetDto
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                                ? $"SET-{reader.GetInt32(reader.GetOrdinal("SetId"))}"
                                : reader.GetString(reader.GetOrdinal("SetCode")),
                            IsInvoice = Convert.ToInt32(reader["IsInvoice"]) == 1,
                            CreatedBy = reader.GetInt32(reader.GetOrdinal("CreatedBy")),
                            CreatedByName = reader.IsDBNull(reader.GetOrdinal("CreatedByName"))
                                ? "Unknown"
                                : reader.GetString(reader.GetOrdinal("CreatedByName")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            QRToken = reader.IsDBNull(reader.GetOrdinal("QRToken"))
                                ? Guid.Empty
                                : reader.GetGuid(reader.GetOrdinal("QRToken")),
                            QRImagePath = reader.IsDBNull(reader.GetOrdinal("QRImagePath"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRImagePath")),
                            QRImageData = reader.IsDBNull(reader.GetOrdinal("QRImageData"))
                                ? null
                                : (byte[])reader["QRImageData"],
                            QRData = reader.IsDBNull(reader.GetOrdinal("QRData"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("QRData")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Remarks")),
                            DocumentNumber = reader.IsDBNull(reader.GetOrdinal("DocumentNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DocumentNumber")),
                            ReferenceNumber = reader.IsDBNull(reader.GetOrdinal("ReferenceNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ReferenceNumber")),
                            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            Company = reader.IsDBNull(reader.GetOrdinal("Company"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Company")),
                            DistributorId = reader.IsDBNull(reader.GetOrdinal("DistributorId"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("DistributorId")),
                            DistributorName = reader.IsDBNull(reader.GetOrdinal("DistributorName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DistributorName")),
                            DispatchDate = reader.IsDBNull(reader.GetOrdinal("DispatchDate"))
                                ? (DateTime?)null
                                : reader.GetDateTime(reader.GetOrdinal("DispatchDate")),
                            SetType = reader.IsDBNull(reader.GetOrdinal("SetType"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SetType")),
                            Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Status")),
                            Subtotal = reader.IsDBNull(reader.GetOrdinal("Subtotal"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                            VatAmount = reader.IsDBNull(reader.GetOrdinal("VatAmount"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("VatAmount")),
                            DiscountAmount = reader.IsDBNull(reader.GetOrdinal("DiscountAmount"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            WhtAmount = reader.IsDBNull(reader.GetOrdinal("WhtAmount"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("WhtAmount")),
                            TotalAmountDue = reader.IsDBNull(reader.GetOrdinal("TotalAmountDue"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("TotalAmountDue")),
                            UpgradeReason = reader.IsDBNull(reader.GetOrdinal("UpgradeReason"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("UpgradeReason")),
                            ComputerName = reader.IsDBNull(reader.GetOrdinal("ComputerName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ComputerName")),
                            IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("IPAddress")),
                            IssuedBrandNewQty = reader.IsDBNull(reader.GetOrdinal("IssuedBrandNewQty"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("IssuedBrandNewQty")),
                            IssuedRefilledQty = reader.IsDBNull(reader.GetOrdinal("IssuedRefilledQty"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("IssuedRefilledQty")),
                            ReceivedById = reader.IsDBNull(reader.GetOrdinal("ReceivedById"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("ReceivedById")),
                            ReceivedByName = reader.IsDBNull(reader.GetOrdinal("ReceivedByName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
                            ImageCount = reader.IsDBNull(reader.GetOrdinal("ImageCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("ImageCount"))
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all requests in a specific set with item details
        /// </summary>
        /// <summary>
        /// Gets Cartridge SetItems for email templates (Set-centric, NOT request-centric).
        /// Queries only dbo.SetItem and dbo.Item tables.
        /// Throws if no SetItems exist for the given SetId.
        /// </summary>
        public async Task<List<CartridgeSetItemDto>> GetCartridgeSetItemsAsync(int setId)
        {
            // CRITICAL: Abort if no SetItems exist
            const string checkSql = "SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = @SetId";
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(checkSql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();
                var count = (int)await cmd.ExecuteScalarAsync();
                if (count == 0)
                {
                    throw new InvalidOperationException($"Cartridge SMTP aborted: No SetItems found for SetId {setId}");
                }
            }

            var items = new List<CartridgeSetItemDto>();

            const string sql = @"
                SELECT
                    si.SetItemId,
                    si.ItemId,
                    i.Name AS ItemName,
                    CASE
                        WHEN i.Category = 'Cartridge'
                        THEN COALESCE(NULLIF(LTRIM(RTRIM(si.ItemCode)), ''), NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''))
                        ELSE i.ModelNumber
                    END AS ModelNumber,
                    si.Quantity,
                    si.UnitPrice,
                    si.Amount
                FROM dbo.SetItem si
                JOIN dbo.Item i ON i.ItemId = si.ItemId
                WHERE si.SetId = @SetId
                ORDER BY si.SetItemId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var setItemIdObj = reader["SetItemId"]; 
                        var itemIdObj = reader["ItemId"]; 
                        var quantityObj = reader["Quantity"]; 
                        var unitPriceObj = reader["UnitPrice"]; 
                        var amountObj = reader["Amount"]; 

                        items.Add(new CartridgeSetItemDto
                        {
                            SetItemId = setItemIdObj == DBNull.Value ? 0 : Convert.ToInt32(setItemIdObj),
                            ItemId = itemIdObj == DBNull.Value ? 0 : Convert.ToInt32(itemIdObj),
                            ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? "" : reader.GetString(reader.GetOrdinal("ItemName")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            Quantity = quantityObj == DBNull.Value ? 0 : Convert.ToInt32(quantityObj),
                            UnitPrice = unitPriceObj == DBNull.Value ? (decimal?)null : Convert.ToDecimal(unitPriceObj),
                            Amount = amountObj == DBNull.Value ? (decimal?)null : Convert.ToDecimal(amountObj)
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Fetches all SetItem rows (joined with Item) for a list of SetIds in a single query.
        /// Returns a dictionary keyed by SetId for O(1) lookup when building the PDF.
        /// </summary>
        public async Task<Dictionary<int, List<SetItemPdfRow>>> GetSetItemsForPdfAsync(IReadOnlyList<int> setIds)
        {
            var result = new Dictionary<int, List<SetItemPdfRow>>();
            if (setIds == null || setIds.Count == 0)
                return result;

            // Build a safe IN clause from integer ids (no string values, no injection risk)
            var idList = string.Join(",", setIds);

            var sql = $@"
                SELECT
                    si.SetId,
                    i.Name        AS ItemName,
                    i.ModelNumber,
                    i.SerialNumber,
                    i.Category,
                    si.Quantity,
                    si.UnitPrice,
                    si.Amount
                FROM dbo.SetItem si
                JOIN dbo.Item    i  ON i.ItemId = si.ItemId
                WHERE si.SetId IN ({idList})
                ORDER BY si.SetId, si.SetItemId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var setId = Convert.ToInt32(reader["SetId"]);
                        if (!result.ContainsKey(setId))
                            result[setId] = new List<SetItemPdfRow>();

                        var qtyObj       = reader["Quantity"];
                        var unitPriceObj = reader["UnitPrice"];
                        var amountObj    = reader["Amount"];

                        result[setId].Add(new SetItemPdfRow
                        {
                            SetId        = setId,
                            ItemName     = reader["ItemName"]     == DBNull.Value ? "" : reader["ItemName"].ToString(),
                            ModelNumber  = reader["ModelNumber"]  == DBNull.Value ? null : reader["ModelNumber"].ToString(),
                            SerialNumber = reader["SerialNumber"] == DBNull.Value ? null : reader["SerialNumber"].ToString(),
                            Category     = reader["Category"]     == DBNull.Value ? null : reader["Category"].ToString(),
                            Quantity     = qtyObj       == DBNull.Value ? 0 : Convert.ToInt32(qtyObj),
                            UnitPrice    = unitPriceObj == DBNull.Value ? (decimal?)null : Convert.ToDecimal(unitPriceObj),
                            Amount       = amountObj    == DBNull.Value ? (decimal?)null : Convert.ToDecimal(amountObj),
                        });
                    }
                }
            }

            return result;
        }

        public async Task<List<CartridgeExchangeModelSummaryDto>> GetCartridgeExchangeModelSummariesBySetAsync(int setId)
        {
            var result = new List<CartridgeExchangeModelSummaryDto>();

            Logger.LogInfo($"GetCartridgeExchangeModelSummariesBySetAsync: SetId={setId}");

            const string sql = @"
IF OBJECT_ID('dbo.CartridgeMovement', 'U') IS NULL
BEGIN
    SELECT
        CAST(NULL AS NVARCHAR(100)) AS CartridgeModel,
        CAST(0 AS INT) AS ReturnedEmptyQty,
        CAST(0 AS INT) AS IssuedFullQty,
        CAST(0 AS INT) AS UnfulfilledQty
    WHERE 1 = 0;
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.CartridgeMovement', 'ReferenceRequestId') IS NOT NULL
    BEGIN
        ;WITH SetReq AS
        (
            SELECT r.ReqId
            FROM dbo.Request r
            WHERE r.SetId = @SetId
        ),
        Movements AS
        (
            -- Primary (if populated): ReferenceRequestId
            SELECT
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CASE
                        WHEN r.Description LIKE '%[MODEL:%'
                             AND CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7) > 0
                        THEN SUBSTRING(
                            r.Description,
                            CHARINDEX('[MODEL:', r.Description) + 7,
                            CHARINDEX(']', r.Description, CHARINDEX('[MODEL:', r.Description) + 7)
                                - (CHARINDEX('[MODEL:', r.Description) + 7)
                        )
                        ELSE NULL
                    END)), ''),
                    NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''),
                    NULLIF(LTRIM(RTRIM(i.Name)), ''),
                    'N/A'
                ) AS CartridgeModel,
                cm.MovementType,
                ISNULL(cm.Quantity, 0) AS Quantity
            FROM dbo.CartridgeMovement cm
            INNER JOIN dbo.Item i ON i.ItemId = cm.ItemId
            INNER JOIN dbo.Request r ON r.ReqId = cm.ReferenceRequestId
            WHERE r.SetId = @SetId
              AND cm.ReferenceRequestId IS NOT NULL
              AND cm.MovementType IN ('Issued', 'Returned')

            UNION ALL

            -- Fallback: Remarks contains 'Request #<ReqId>'
            SELECT
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CASE
                        WHEN rq.Description LIKE '%[MODEL:%'
                             AND CHARINDEX(']', rq.Description, CHARINDEX('[MODEL:', rq.Description) + 7) > 0
                        THEN SUBSTRING(
                            rq.Description,
                            CHARINDEX('[MODEL:', rq.Description) + 7,
                            CHARINDEX(']', rq.Description, CHARINDEX('[MODEL:', rq.Description) + 7)
                                - (CHARINDEX('[MODEL:', rq.Description) + 7)
                        )
                        ELSE NULL
                    END)), ''),
                    NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''),
                    NULLIF(LTRIM(RTRIM(i.Name)), ''),
                    'N/A'
                ) AS CartridgeModel,
                cm.MovementType,
                ISNULL(cm.Quantity, 0) AS Quantity
            FROM dbo.CartridgeMovement cm
            INNER JOIN dbo.Item i ON i.ItemId = cm.ItemId
            INNER JOIN SetReq sr ON cm.Remarks LIKE '%Request #' + CAST(sr.ReqId AS VARCHAR(20)) + '%'
            INNER JOIN dbo.Request rq ON rq.ReqId = sr.ReqId
            WHERE cm.MovementType IN ('Issued', 'Returned')
        )
        SELECT
            m.CartridgeModel,
            SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END) AS ReturnedEmptyQty,
            SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END) AS IssuedFullQty,
            CASE
                WHEN SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END)
                   - SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END) > 0
                THEN SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END)
                   - SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END)
                ELSE 0
            END AS UnfulfilledQty
        FROM Movements m
        GROUP BY m.CartridgeModel
        ORDER BY m.CartridgeModel;
    END
    ELSE
    BEGIN
        ;WITH SetReq AS
        (
            SELECT r.ReqId
            FROM dbo.Request r
            WHERE r.SetId = @SetId
        ),
        Movements AS
        (
            SELECT
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CASE
                        WHEN rq.Description LIKE '%[MODEL:%'
                             AND CHARINDEX(']', rq.Description, CHARINDEX('[MODEL:', rq.Description) + 7) > 0
                        THEN SUBSTRING(
                            rq.Description,
                            CHARINDEX('[MODEL:', rq.Description) + 7,
                            CHARINDEX(']', rq.Description, CHARINDEX('[MODEL:', rq.Description) + 7)
                                - (CHARINDEX('[MODEL:', rq.Description) + 7)
                        )
                        ELSE NULL
                    END)), ''),
                    NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''),
                    NULLIF(LTRIM(RTRIM(i.Name)), ''),
                    'N/A'
                ) AS CartridgeModel,
                cm.MovementType,
                ISNULL(cm.Quantity, 0) AS Quantity
            FROM dbo.CartridgeMovement cm
            INNER JOIN dbo.Item i ON i.ItemId = cm.ItemId
            INNER JOIN SetReq sr ON cm.Remarks LIKE '%Request #' + CAST(sr.ReqId AS VARCHAR(20)) + '%'
            INNER JOIN dbo.Request rq ON rq.ReqId = sr.ReqId
            WHERE cm.MovementType IN ('Issued', 'Returned')
        )
        SELECT
            m.CartridgeModel,
            SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END) AS ReturnedEmptyQty,
            SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END) AS IssuedFullQty,
            CASE
                WHEN SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END)
                   - SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END) > 0
                THEN SUM(CASE WHEN m.MovementType = 'Returned' THEN m.Quantity ELSE 0 END)
                   - SUM(CASE WHEN m.MovementType = 'Issued' THEN m.Quantity ELSE 0 END)
                ELSE 0
            END AS UnfulfilledQty
        FROM Movements m
        GROUP BY m.CartridgeModel
        ORDER BY m.CartridgeModel;
    END
END";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new CartridgeExchangeModelSummaryDto
                        {
                            CartridgeModel = reader.IsDBNull(reader.GetOrdinal("CartridgeModel"))
                                ? "N/A"
                                : reader.GetString(reader.GetOrdinal("CartridgeModel")),
                            ReturnedEmptyQty = reader.IsDBNull(reader.GetOrdinal("ReturnedEmptyQty"))
                                ? 0
                                : Convert.ToInt32(reader["ReturnedEmptyQty"]),
                            IssuedFullQty = reader.IsDBNull(reader.GetOrdinal("IssuedFullQty"))
                                ? 0
                                : Convert.ToInt32(reader["IssuedFullQty"]),
                            UnfulfilledQty = reader.IsDBNull(reader.GetOrdinal("UnfulfilledQty"))
                                ? 0
                                : Convert.ToInt32(reader["UnfulfilledQty"])
                        });
                    }
                }
            }

            if (result.Count == 0)
            {
                Logger.LogWarning($"GetCartridgeExchangeModelSummariesBySetAsync: No movement summary rows returned for SetId={setId}." );
            }
            else
            {
                Logger.LogInfo($"GetCartridgeExchangeModelSummariesBySetAsync: SetId={setId} rows={result.Count}. " +
                               string.Join(" | ", result.Select(r => $"{r.CartridgeModel}: returned={r.ReturnedEmptyQty}, issued={r.IssuedFullQty}, pending={r.UnfulfilledQty}")));
            }

            return result;
        }

        /// <summary>
        /// Looks for an existing invoice (a DIFFERENT Set with IsInvoice = 1) that already
        /// covers one of this dispatch Set's items (matched by ItemId, via either that other
        /// Set's dbo.Request or dbo.SetItem rows). Used by "Record Invoice" on
        /// ViewSetDetailPage to stop the same physical item from getting a second, separate
        /// invoice record. Returns null when no such Set exists.
        /// </summary>
        public async Task<ExistingInvoiceMatch> FindExistingInvoiceForSetItemsAsync(int setId)
        {
            const string sql = @"
                SELECT TOP (1) s2.SetId, s2.SetCode, s2.DocumentNumber
                FROM dbo.[Set] s2
                WHERE s2.IsInvoice = 1
                  AND s2.SetId <> @SetId
                  AND (
                        EXISTS (
                            SELECT 1 FROM dbo.Request r2
                            WHERE r2.SetId = s2.SetId
                              AND r2.ItemId IN (SELECT ItemId FROM dbo.Request WHERE SetId = @SetId)
                        )
                        OR EXISTS (
                            SELECT 1 FROM dbo.SetItem si2
                            WHERE si2.SetId = s2.SetId
                              AND si2.ItemId IN (SELECT ItemId FROM dbo.Request WHERE SetId = @SetId)
                        )
                      )";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new ExistingInvoiceMatch
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode = reader.IsDBNull(reader.GetOrdinal("SetCode"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SetCode")),
                            DocumentNumber = reader.IsDBNull(reader.GetOrdinal("DocumentNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DocumentNumber"))
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Sum of Quantity * UnitPrice across this Set's dbo.Request rows — used as the
        /// default Subtotal when recording an invoice for a Request-based dispatch Set
        /// (RecordSetInvoiceDialog), since GetSetRequestsAsync's DTO doesn't carry UnitPrice.
        /// </summary>
        public async Task<decimal> GetSetRequestsSubtotalAsync(int setId)
        {
            const string sql = @"
                SELECT ISNULL(SUM(r.Quantity * r.UnitPrice), 0)
                FROM dbo.Request r
                WHERE r.SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
            }
        }

        public async Task<List<SetDetailRequestDto>> GetSetRequestsAsync(int setId)
        {
            var requests = new List<SetDetailRequestDto>();

            const string sql = @"
                SELECT
                    r.ReqId,
                    r.ItemId,
                    r.Description,
                    r.Quantity,
                    r.Status,
                    r.Remarks,
                    i.Name AS ItemName,
                    i.Category,
                    i.ModelNumber,
                    i.SerialNumber,
                    i.IsTrackedAsset
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId = @SetId
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var description = reader.IsDBNull(reader.GetOrdinal("Description"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("Description"));

                        requests.Add(new SetDetailRequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Description = description,
                            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            Status = reader.GetString(reader.GetOrdinal("Status")),
                            Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) 
                                ? null 
                                : reader.GetString(reader.GetOrdinal("Remarks")),
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) 
                                ? null 
                                : reader.GetString(reader.GetOrdinal("Category")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) 
                                ? null 
                                : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            IsTrackedAsset = reader.IsDBNull(reader.GetOrdinal("IsTrackedAsset"))
                                ? false
                                : reader.GetBoolean(reader.GetOrdinal("IsTrackedAsset"))
                        });

                        var last = requests[requests.Count - 1];
                        var typedModel = ExtractTypedModelFromDescription(description);
                        if (!string.IsNullOrWhiteSpace(typedModel)
                            && !string.IsNullOrWhiteSpace(description)
                            && description.TrimStart().StartsWith("[PORTAL]", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(last.Category?.Trim(), "Cartridge", StringComparison.OrdinalIgnoreCase))
                        {
                            last.ModelNumber = typedModel;
                            last.ItemName = typedModel + " Cartridge";
                        }
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Same shape as <see cref="GetSetRequestsAsync"/> (returns the same
        /// <see cref="SetDetailRequestDto"/>), but sourced from dbo.SetItem for Sets that have
        /// items but no dbo.Request rows at all (Renewal- or Invoice-created Sets). Used so
        /// the "Requests in this Set" grid and the QR/PDF/Requisition generators can display
        /// these items too, instead of showing/printing an empty item list.
        ///
        /// ReqId on the returned rows is actually SetItemId, repurposed only for
        /// numbering/display in QR/PDF/Requisition output — callers must NEVER pass it to a
        /// dbo.Request-based mutation (e.g. RemoveRequestFromSetAsync). See
        /// SetRequestDisplayRow.IsSetItemRow in ViewSetDetailPage.xaml.cs for the UI-side guard.
        /// </summary>
        public async Task<List<SetDetailRequestDto>> GetSetItemsAsRequestsAsync(int setId)
        {
            var items = new List<SetDetailRequestDto>();

            const string sql = @"
                SELECT
                    si.SetItemId,
                    si.ItemId,
                    si.Description,
                    si.Quantity,
                    s.Status,
                    i.Name AS ItemName,
                    i.Category,
                    i.ModelNumber,
                    i.SerialNumber,
                    i.IsTrackedAsset
                FROM dbo.SetItem si
                INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                INNER JOIN dbo.[Set] s ON si.SetId = s.SetId
                WHERE si.SetId = @SetId
                ORDER BY si.CreatedAt DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new SetDetailRequestDto
                        {
                            ReqId = reader.GetInt32(reader.GetOrdinal("SetItemId")),
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Description")),
                            Quantity = Convert.ToInt32(reader.GetDecimal(reader.GetOrdinal("Quantity"))),
                            Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Status")),
                            Remarks = null,
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("Category")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            IsTrackedAsset = reader.IsDBNull(reader.GetOrdinal("IsTrackedAsset"))
                                ? false
                                : reader.GetBoolean(reader.GetOrdinal("IsTrackedAsset"))
                        });
                    }
                }
            }

            return items;
        }

        private static string ExtractTypedModelFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            const string modelPrefix = "[MODEL:";
            int startIndex = description.IndexOf(modelPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                return null;

            startIndex += modelPrefix.Length;
            int endIndex = description.IndexOf(']', startIndex);
            if (endIndex < 0)
                return null;

            string model = description.Substring(startIndex, endIndex - startIndex).Trim();
            return string.IsNullOrWhiteSpace(model) ? null : model;
        }

        public async Task UpdateSetHardwareInfoAsync(int setId, string computerName, string ipAddress)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET ComputerName = @ComputerName,
                    IPAddress = @IPAddress
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@ComputerName", (object)computerName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IPAddress", (object)ipAddress ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Corrects Set.CreatedAt when staff mis-key the date on encoding. Preserves the existing
        /// time-of-day since the calendar picker only lets the user change the date portion.
        /// </summary>
        public async Task UpdateSetCreatedAtAsync(int setId, DateTime newDate)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET CreatedAt = DATEADD(
                    DAY,
                    DATEDIFF(DAY, CAST(CreatedAt AS date), @NewDate),
                    CreatedAt)
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@NewDate", newDate.Date);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Saves the Remarks field for a Set from the Set Details page.
        /// </summary>
        public async Task UpdateSetRemarksAsync(int setId, string remarks)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET Remarks = @Remarks
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Adds a request to a set and sets Set.ReqId and SetType if it's the first request
        /// </summary>
        public async Task AddRequestToSetAsync(int reqId, int setId)
        {
            var before = await GetRequestItemInfoAsync(reqId);
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        string originalStatus = null;
                        const string sqlGetStatus = @"
                            SELECT Status
                            FROM dbo.Request
                            WHERE ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlGetStatus, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            originalStatus = Convert.ToString(await cmd.ExecuteScalarAsync());
                        }

                        // Update Request to link it to the Set
                        const string sqlUpdateRequest = @"
                            UPDATE dbo.Request
                            SET SetId = @SetId
                            WHERE ReqId = @ReqId";

                        using (var cmd = new SqlCommand(sqlUpdateRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Preserve the original status (e.g. Under Review) even after linking to a Set.
                        // This guards against DB-side triggers/business rules that might change Status
                        // when SetId is updated.
                        if (!string.IsNullOrWhiteSpace(originalStatus))
                        {
                            const string sqlRestoreStatus = @"
                                UPDATE dbo.Request
                                SET Status = @Status
                                WHERE ReqId = @ReqId";

                            using (var cmd = new SqlCommand(sqlRestoreStatus, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@Status", originalStatus);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // If Set.ReqId is NULL, set it to this request and update SetType from the item's ItemType
                        const string sqlUpdateSet = @"
                            UPDATE s
                            SET s.ReqId = @ReqId,
                                s.SetType = ISNULL(i.ItemType, 'Hardware')
                            FROM dbo.[Set] s
                            INNER JOIN dbo.Request r ON r.ReqId = @ReqId
                            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                            WHERE s.SetId = @SetId AND s.ReqId IS NULL";

                        using (var cmd = new SqlCommand(sqlUpdateSet, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Dept-level distributor propagation: a dept-level request may
                        // carry dbo.Request.DistributorId (independent sales distributor,
                        // not under Company/Dept/Branch). Carry it onto the Set when the
                        // Set has none yet. Dynamic SQL is required: T-SQL binds column
                        // names at compile time, so a static UPDATE guarded by IF
                        // COL_LENGTH would still throw "Invalid column name" on DBs
                        // predating either migration.
                        const string sqlPropagateDistributor = @"
                            IF COL_LENGTH('dbo.Request', 'DistributorId') IS NOT NULL
                            AND COL_LENGTH('dbo.[Set]', 'DistributorId') IS NOT NULL
                            BEGIN
                                EXEC sp_executesql
                                    N'UPDATE s SET s.DistributorId = r.DistributorId FROM dbo.[Set] s INNER JOIN dbo.Request r ON r.ReqId = @ReqId WHERE s.SetId = @SetId AND s.DistributorId IS NULL AND r.DistributorId IS NOT NULL;',
                                    N'@ReqId INT, @SetId INT',
                                    @ReqId = @ReqId, @SetId = @SetId;
                            END";

                        using (var cmd = new SqlCommand(sqlPropagateDistributor, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // CRITICAL: Materialize SetItem from Request ONLY for non-Cartridge items.
                        //
                        // WHY CARTRIDGE SETS ARE EXCLUDED:
                        //   Portal cartridge requests use a placeholder ItemId (the first active
                        //   cartridge item by ItemId, e.g. ItemId=626 "HP 107A") because inventory
                        //   is not allocated at request time (INTENT-ONLY design).
                        //
                        //   If we materialise SetItem here we get wrong data:
                        //     ItemCode = placeholder.ModelNumber  ("123456")
                        //     Description = placeholder.Name      ("HP 107A Black Laser Toner Cartridge")
                        //
                        //   CartridgeManagementRepository.FulfillCartridgeExchange owns the
                        //   cartridge SetItem write. It uses:
                        //     1. [MODEL:XXX] tag extraction from Request.Description
                        //     2. CartridgeModel.ModelNumber via Item.CartridgeModelId FK
                        //     3. Item.ModelNumber as last resort
                        //   and has a RAISERROR guard that rolls back if no model can be resolved.
                        //
                        //   sqlMaterializeSetItem's NOT EXISTS guard is per Set+Item, so it won't
                        //   overwrite a SetItem row FulfillCartridgeExchange already wrote for this
                        //   item â€” but it also won't ever correct one. We must NOT write bad
                        //   placeholder data here in the first place.
                        const string sqlIsCartridgeRequest = @"
                            SELECT TOP 1 1
                            FROM dbo.Request r
                            INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
                            WHERE r.ReqId = @ReqId
                              AND i.Category = 'Cartridge'";

                        bool isCartridgeRequest;
                        using (var cmd = new SqlCommand(sqlIsCartridgeRequest, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ReqId", reqId);
                            var chk = await cmd.ExecuteScalarAsync();
                            isCartridgeRequest = chk != null;
                        }

                        if (!isCartridgeRequest)
                        {
                            await MaterializeSetItemsFromRequestsAsync(setId, con, transaction);
                        }
                        // else: FulfillCartridgeExchange will materialise with cartridge-aware SQL.

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            var after = await GetRequestItemInfoAsync(reqId);
            if (after != null && after.ItemId.HasValue)
            {
                await LogRequestSetAssignmentAsync(
                    after,
                    setId,
                    after.SetCode,
                    "Added to Set",
                    $"Request #{reqId} linked to Set {after.SetCode ?? setId.ToString()}"
                );
            }
        }

        /// <summary>
        /// Removes a request from a set (sets SetId to NULL)
        /// </summary>
        public async Task RemoveRequestFromSetAsync(int reqId)
        {
            var before = await GetRequestItemInfoAsync(reqId);
            const string sql = @"
                UPDATE dbo.Request
                SET SetId = NULL
                WHERE ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            if (before != null && before.ItemId.HasValue && before.SetId.HasValue)
            {
                await LogRequestSetAssignmentAsync(
                    before,
                    before.SetId,
                    before.SetCode,
                    "Removed from Set",
                    $"Request #{reqId} unlinked from Set {before.SetCode ?? before.SetId.Value.ToString()}"
                );
            }
        }

        /// <summary>
        /// Deletes a set (requires all requests to be removed first or cascade delete)
        /// </summary>
        public async Task DeleteSetAsync(int setId)
        {
            var requests = await GetRequestsInSetAsync(setId);
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // First, remove all requests from this set
                        using (var cmd = new SqlCommand(
                            "UPDATE dbo.Request SET SetId = NULL WHERE SetId = @SetId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Then delete the set
                        using (var cmd = new SqlCommand(
                            "DELETE FROM dbo.[Set] WHERE SetId = @SetId", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        ActivityLogger.Log(ActivityLogger.Actions.Delete, "Set", setId, $"Set #{setId} deleted");
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            foreach (var r in requests)
            {
                if (!r.ItemId.HasValue)
                    continue;

                await LogRequestSetAssignmentAsync(
                    r,
                    setId,
                    r.SetCode,
                    "Removed from Set",
                    $"Set {r.SetCode ?? setId.ToString()} deleted; request #{r.ReqId} unlinked"
                );
            }
        }

        /// <summary>
        /// Deletes a set and restores stock for all items in its requests
        /// </summary>
        public async Task<bool> DeleteSetAndRestoreStock(int setId)
        {
            var requests = await GetRequestsInSetAsync(setId);
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    int itemsRestored = 0;
                    int requestsUnlinked = 0;
                    int inventoryDeleted = 0;

                    // Step 1: Unlink any requests that reference this set
                    string unlinkRequestsSql = @"
                        UPDATE Request 
                        SET SetId = NULL 
                        WHERE SetId = @SetId";
                    
                    using (SqlCommand cmd = new SqlCommand(unlinkRequestsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        requestsUnlinked = await cmd.ExecuteNonQueryAsync();
                    }

                    // Step 2: Get all items in this set to restore stock (only for items that affect inventory)
                    string getSetItemsSql = @"
                        SELECT si.ItemId, si.Quantity, i.AffectsInventory
                        FROM SetItem si
                        INNER JOIN Item i ON si.ItemId = i.ItemId
                        WHERE si.SetId = @SetId";

                    List<(int ItemId, int Quantity, bool AffectsInventory)> setItems = new List<(int, int, bool)>();

                    using (SqlCommand cmd = new SqlCommand(getSetItemsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int itemId = Convert.ToInt32(reader[0]);
                                int quantity = Convert.ToInt32(reader[1]);
                                bool affectsInventory = reader[2] != DBNull.Value && Convert.ToBoolean(reader[2]);
                                setItems.Add((itemId, quantity, affectsInventory));
                            }
                        }
                    }

                    // Step 3: Restore stock back to inventory (only for items that affect inventory)
                    foreach (var item in setItems)
                    {
                        if (item.AffectsInventory)
                        {
                            string restoreStockSql = @"
                                UPDATE Item
                                SET StockOnHand = StockOnHand + @Quantity
                                WHERE ItemId = @ItemId";

                            using (SqlCommand cmd = new SqlCommand(restoreStockSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                                cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                await cmd.ExecuteNonQueryAsync();
                                itemsRestored++;
                            }
                        }
                    }

                    // Step 4: Delete SetItem records (child records first to avoid FK constraint)
                    string deleteSetItemsSql = "DELETE FROM SetItem WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteSetItemsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Step 5: Delete related Inventory entries if any
                    string deleteInventorySql = "DELETE FROM Inventory WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteInventorySql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        inventoryDeleted = await cmd.ExecuteNonQueryAsync();
                    }

                    // Step 6: Finally delete the Set itself
                    string deleteSetSql = "DELETE FROM [Set] WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteSetSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        
                        if (rowsAffected == 0)
                        {
                            throw new Exception("Set not found or already deleted.");
                        }
                    }

                    transaction.Commit();
                    ActivityLogger.Log(ActivityLogger.Actions.Delete, "Set", setId, $"Set #{setId} deleted and stock restored");

                    foreach (var r in requests)
                    {
                        if (!r.ItemId.HasValue)
                            continue;

                        await LogRequestSetAssignmentAsync(
                            r,
                            setId,
                            r.SetCode,
                            "Removed from Set",
                            $"Set {r.SetCode ?? setId.ToString()} deleted/restored stock; request #{r.ReqId} unlinked"
                        );
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.LogError($"DeleteSetAndRestoreStock failed: {ex.Message}");
                    throw;
                }
            }
        }

        // Update RequestRepository.cs - DeleteRequestAndRestoreStock method
        public async Task<bool> DeleteRequestAndRestoreStock(int reqId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("dbo.usp_Request_Delete", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ReqId", reqId);

                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                        await LogRequestDeleteStockRestoredAsync(reqId);
                        return true;
                    }
                    catch (SqlException ex)
                    {
                        throw new Exception($"Error deleting request and restoring stock: {ex.Message}", ex);
                    }
                }
            }
        }

        private async Task LogRequestDeleteStockRestoredAsync(int reqId)
        {
            try
            {
                var items = new List<(int ItemId, string Serial, int Quantity)>();
                using (var auditConn = new SqlConnection(GetConnectionString()))
                using (var lookup = new SqlCommand(
                    "SELECT i.ItemId, i.SerialNumber, r.Quantity " +
                    "FROM dbo.Request r " +
                    "INNER JOIN dbo.Item i ON i.ItemId = r.ItemId " +
                    "WHERE r.ReqId = @ReqId AND i.AffectsInventory = 1", auditConn))
                {
                    lookup.Parameters.AddWithValue("@ReqId", reqId);
                    await auditConn.OpenAsync();
                    using (var reader = await lookup.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            items.Add((
                                reader.GetInt32(0),
                                reader.IsDBNull(1) ? null : reader.GetString(1),
                                reader.GetInt32(2)
                            ));
                        }
                    }
                }

                var audit = new ItemAuditTrailRepository();
                foreach (var item in items)
                {
                    await audit.LogActionAsync(new ItemAuditTrailDto
                    {
                        ItemId = item.ItemId,
                        SerialNumber = item.Serial,
                        Action = "Request Deleted - Stock Restored",
                        ActionTime = DateTime.Now,
                        Direction = "IN",
                        Status = "Completed",
                        ReferenceType = "Request",
                        ReferenceId = reqId,
                        Notes = $"Stock restored for {item.Quantity} unit(s) after request deletion.",
                        CreatedBy = AppSession.CurrentUserName ?? "System"
                    });
                }
            }
            catch { /* best-effort: audit must not break request deletion */ }
        }

        /// <summary>
        /// Gets set details for confirmation dialog
        /// </summary>
        public async Task<(string SetCode, int RequestCount)> GetSetDetailsForDelete(int setId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                await conn.OpenAsync();
                
                string query = @"
                    SELECT s.SetCode, COUNT(r.ReqId) as RequestCount
                    FROM dbo.[Set] s
                    LEFT JOIN dbo.Request r ON s.SetId = r.SetId
                    WHERE s.SetId = @SetId
                    GROUP BY s.SetCode";
                
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return (Convert.ToString(reader[0]), Convert.ToInt32(reader[1]));
                        }
                    }
                }
            }
            
            return (null, 0);
        }

        /// <summary>
        /// Updates the QR image path for a set
        /// </summary>
        public async Task UpdateQRImagePathAsync(int setId, string qrImagePath)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET QRImagePath = @QRImagePath
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@QRImagePath", (object)qrImagePath ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Updates both the QR image (stored in DB) AND QR data JSON for a set (no automatic dispatch)
        /// </summary>
        public async Task UpdateQRDataAsync(int setId, byte[] qrImageData, string qrDataJson)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET QRImageData = @QRImageData,
                    QRData = @QRData
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@QRImageData", (object)qrImageData ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@QRData", (object)qrDataJson ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Manual dispatch: marks set as dispatched and logs per-item audit entries
        /// </summary>
        public async Task<string> DispatchSetAsync(int setId)
        {
            const string updateSql = @"
                UPDATE dbo.[Set]
                SET Status = 'Dispatched',
                    DispatchDate = ISNULL(DispatchDate, SYSDATETIME())
                WHERE SetId = @SetId
                  AND ISNULL(Status, '') <> 'Dispatched';";

            // Dispatching a Set means every Request in it now has its item physically
            // with the requester, regardless of what stock was on hand when the request
            // was encoded â€” keep IssuedQty in sync so it no longer shows as Unfulfilled
            // / Partially Fulfilled once the Set goes out.
            const string syncIssuedQtySql = @"
                UPDATE dbo.Request
                SET IssuedQty = Quantity
                WHERE SetId = @SetId
                  AND IssuedQty < Quantity;";

            const string getSetCodeSql = @"
                SELECT SetCode
                FROM dbo.[Set]
                WHERE SetId = @SetId;";

            const string getServerTimeSql = "SELECT SYSDATETIME();";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync().ConfigureAwait(false);
                using (var tx = con.BeginTransaction())
                {
                    string setCode = null;
                    List<RequestItemInfo> requests = null;
                    try
                    {
                        int rowsAffected;
                        using (var cmd = new SqlCommand(updateSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            rowsAffected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }

                        using (var cmd = new SqlCommand(syncIssuedQtySql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }

                        using (var cmd = new SqlCommand(getSetCodeSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            setCode = (await cmd.ExecuteScalarAsync().ConfigureAwait(false)) as string;
                        }

                        // Idempotency: if already dispatched (or not found), do not log duplicates.
                        if (rowsAffected == 0)
                        {
                            tx.Commit();
                            RaiseSetDispatched(setId, setCode);
                            return null;
                        }

                        requests = await GetRequestsInSetAsync(setId, con, tx).ConfigureAwait(false);

                        // Use Client time (DateTime.Now) to match Request creation time and avoid clock skew
                        DateTime actionTime = DateTime.Now;

                        var auditRepo = new ItemAuditTrailRepository();

                        if (requests != null)
                        {
                            foreach (var info in requests)
                            {
                                if (info == null || !info.ItemId.HasValue)
                                    continue;

                                await auditRepo.LogActionAsync(new ItemAuditTrailDto
                                {
                                    ItemId = info.ItemId,
                                    SerialNumber = info.SerialNumber,
                                    Action = "Set Dispatched",
                                    ActionTime = actionTime,
                                    Direction = "OUT",
                                    Status = "Completed",
                                    EmployeeName = info.EmployeeName,
                                    DepartmentName = info.DepartmentName,
                                    BranchName = info.BranchName,
                                    ReferenceType = "Set",
                                    ReferenceId = setId,
                                    SetCode = info.SetCode,
                                    Notes = "Manually deployed via Deploy button",
                                    CreatedBy = AppSession.CurrentUserName ?? "System"
                                }, con, tx).ConfigureAwait(false);
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }

                    // Raise after commit only.
                    RaiseSetDispatched(setId, setCode);

                    // Email is no longer sent automatically on dispatch.
                    // Use SetDispatchNotificationPage to send notifications manually.
                    return null;
                }
            }
        }

        private static void RaiseSetDispatched(int setId, string setCode)
        {
            try
            {
                SetDispatched?.Invoke(setId, setCode);
                SetDispatchStateChanged?.Invoke(setId, true); // deployed = true
            }
            catch
            {
            }
        }

        /// <summary>
        /// Gets employee details for a request including Company, Department, and Branch
        /// </summary>
        public async Task<EmployeeDetailDto> GetEmployeeDetailsForRequestAsync(int reqId)
        {
            // LEFT JOIN employee so dept-level requests (EmpId IS NULL) are still returned.
            // Org-unit columns (ComId/DeptId/BranchId) are read directly from Request and
            // joined to the lookup tables regardless of whether an employee exists.
            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name          AS EmployeeName,
                    e.Position,
                    e.EmployeeNumber,
                    t.Code          AS TitleDescription,
                    COALESCE(ec.Name, rc.Name) AS CompanyName,
                    COALESCE(ed.Name, rd.Name) AS DepartmentName,
                    COALESCE(eb.Name, rb.Name) AS BranchName
                FROM dbo.Request r
                LEFT JOIN dbo.Employee   e  ON r.EmpId    = e.EmpId
                LEFT JOIN dbo.Title      t  ON e.TitleId  = t.TitleId
                -- org units via employee
                LEFT JOIN dbo.Company    ec ON e.ComId     = ec.ComId
                LEFT JOIN dbo.Department ed ON e.DeptId    = ed.DeptId
                LEFT JOIN dbo.Branch     eb ON e.BranchId  = eb.BranchId
                -- org units directly on the request (dept-level)
                LEFT JOIN dbo.Company    rc ON r.ComId     = rc.ComId
                LEFT JOIN dbo.Department rd ON r.DeptId    = rd.DeptId
                LEFT JOIN dbo.Branch     rb ON r.BranchId  = rb.BranchId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new EmployeeDetailDto
                        {
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId"))
                                ? 0
                                : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            Position = reader.IsDBNull(reader.GetOrdinal("Position"))
                                ? ""
                                : reader.GetString(reader.GetOrdinal("Position")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CompanyName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("BranchName")),
                            EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                            TitleDescription = reader.IsDBNull(reader.GetOrdinal("TitleDescription"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("TitleDescription"))
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Same shape as <see cref="GetEmployeeDetailsForRequestAsync"/>, but sourced from
        /// dbo.[Set]'s own CurrentEmployeeId/CurrentDepartmentId/CurrentBranchId/ComId
        /// columns instead of dbo.Request. Used for Sets that have items (dbo.SetItem) but
        /// no dbo.Request rows at all — e.g. Renewal-created or Invoice-created Sets —
        /// where there is no Request to read employee/org-unit info from.
        /// </summary>
        public async Task<EmployeeDetailDto> GetEmployeeDetailsForSetAsync(int setId)
        {
            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name          AS EmployeeName,
                    e.Position,
                    e.EmployeeNumber,
                    t.Code          AS TitleDescription,
                    COALESCE(ec.Name, sc.Name) AS CompanyName,
                    COALESCE(ed.Name, sd.Name) AS DepartmentName,
                    COALESCE(eb.Name, sb.Name) AS BranchName
                FROM dbo.[Set] s
                LEFT JOIN dbo.Employee   e  ON s.CurrentEmployeeId = e.EmpId
                LEFT JOIN dbo.Title      t  ON e.TitleId  = t.TitleId
                -- org units via employee
                LEFT JOIN dbo.Company    ec ON e.ComId     = ec.ComId
                LEFT JOIN dbo.Department ed ON e.DeptId    = ed.DeptId
                LEFT JOIN dbo.Branch     eb ON e.BranchId  = eb.BranchId
                -- org units directly on the Set (dept-level)
                LEFT JOIN dbo.Company    sc ON s.ComId               = sc.ComId
                LEFT JOIN dbo.Department sd ON s.CurrentDepartmentId = sd.DeptId
                LEFT JOIN dbo.Branch     sb ON s.CurrentBranchId     = sb.BranchId
                WHERE s.SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new EmployeeDetailDto
                        {
                            EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId"))
                                ? 0
                                : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("EmployeeName")),
                            Position = reader.IsDBNull(reader.GetOrdinal("Position"))
                                ? ""
                                : reader.GetString(reader.GetOrdinal("Position")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("CompanyName")),
                            DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("DepartmentName")),
                            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("BranchName")),
                            EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                            TitleDescription = reader.IsDBNull(reader.GetOrdinal("TitleDescription"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("TitleDescription"))
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Snapshots a Set's current ownership (Company/Branch/Department/Employee, IDs + names)
        /// for use as the before/after payload of an ownership-transfer AuditTrail entry.
        /// </summary>
        public async Task<SetOwnershipSnapshot> GetSetOwnershipSnapshotAsync(int setId)
        {
            const string sql = @"
                SELECT
                    s.ComId, c.Name AS CompanyName,
                    s.CurrentBranchId, b.Name AS BranchName,
                    s.CurrentDepartmentId, d.Name AS DepartmentName,
                    s.CurrentEmployeeId, e.Name AS EmployeeName
                FROM dbo.[Set] s
                LEFT JOIN dbo.Company c ON s.ComId = c.ComId
                LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
                LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
                LEFT JOIN dbo.Employee e ON s.CurrentEmployeeId = e.EmpId
                WHERE s.SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);

                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return new SetOwnershipSnapshot();

                    return new SetOwnershipSnapshot
                    {
                        ComId = reader.IsDBNull(reader.GetOrdinal("ComId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ComId")),
                        CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                        CurrentBranchId = reader.IsDBNull(reader.GetOrdinal("CurrentBranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentBranchId")),
                        BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                        CurrentDepartmentId = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentDepartmentId")),
                        DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                        CurrentEmployeeId = reader.IsDBNull(reader.GetOrdinal("CurrentEmployeeId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentEmployeeId")),
                        EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName"))
                    };
                }
            }
        }

        /// <summary>
        /// Fire-and-forget style: an audit-log failure must never block the ownership update it describes.
        /// </summary>
        private static async Task LogSetOwnershipAuditAsync(int setId, int modifiedByUserId, string notes, SetOwnershipSnapshot before, SetOwnershipSnapshot after)
        {
            try
            {
                await new AuditRepository().LogAsync(new AuditEntry
                {
                    Action = "OwnershipTransfer",
                    EntityId = setId,
                    EntityType = "Set",
                    UserId = modifiedByUserId,
                    UserName = AppSession.CurrentUserName,
                    Timestamp = DateTime.Now,
                    Notes = notes,
                    OldValues = JsonConvert.SerializeObject(before),
                    NewValues = JsonConvert.SerializeObject(after)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SetRepository] Failed to write ownership audit log: {ex.Message}");
            }
        }

        /// <summary>
        /// Set-level equivalent of <see cref="TransferSetOwnershipAsync"/> for Sets with no
        /// dbo.Request rows — writes directly to dbo.[Set].CurrentEmployeeId instead of a
        /// (non-existent) dbo.Request row. Pass null to clear the assignment.
        /// </summary>
        public async Task UpdateSetLevelEmployeeAsync(int setId, int? empId, int modifiedByUserId)
        {
            var before = await GetSetOwnershipSnapshotAsync(setId);

            // Clears ComId/CurrentBranchId/CurrentDepartmentId — mirrors UpdateSetLevelOrgUnitAsync
            // clearing CurrentEmployeeId on the other direction. Without this, a Set previously
            // assigned at Dept level kept its old org columns after being reassigned to an
            // Employee, so anything reading them directly (bypassing the employee's own org) would
            // show the stale Dept-level company/branch/department instead of the new employee's.
            const string sql = @"
                UPDATE dbo.[Set]
                SET CurrentEmployeeId   = @EmpId,
                    ComId               = NULL,
                    CurrentDepartmentId = NULL,
                    CurrentBranchId     = NULL
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@EmpId", (object)empId ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            var after = await GetSetOwnershipSnapshotAsync(setId);
            await LogSetOwnershipAuditAsync(setId, modifiedByUserId,
                $"Set transferred to employee level: {after.EmployeeName ?? "Unassigned"}", before, after);
        }

        /// <summary>
        /// Set-level equivalent of <see cref="TransferSetOwnershipToDeptAsync"/> for Sets
        /// with no dbo.Request rows — writes directly to dbo.[Set]'s own ComId/
        /// CurrentDepartmentId/CurrentBranchId columns and clears CurrentEmployeeId
        /// (mutually exclusive with Employee-mode assignment).
        /// </summary>
        public async Task UpdateSetLevelOrgUnitAsync(int setId, int comId, int? deptId, int? branchId, int modifiedByUserId)
        {
            var before = await GetSetOwnershipSnapshotAsync(setId);

            const string sql = @"
                UPDATE dbo.[Set]
                SET CurrentEmployeeId   = NULL,
                    ComId               = @ComId,
                    CurrentDepartmentId = @DeptId,
                    CurrentBranchId     = @BranchId
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@ComId", comId);
                cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            var after = await GetSetOwnershipSnapshotAsync(setId);
            await LogSetOwnershipAuditAsync(setId, modifiedByUserId,
                $"Set transferred to dept level: {after.CompanyName ?? "Unassigned"}" +
                (after.DepartmentName != null ? $" / {after.DepartmentName}" : "") +
                (after.BranchName != null ? $" / {after.BranchName}" : ""), before, after);
        }

        /// <summary>
        /// Sets the independent sales distributor on a Set header and all of its
        /// Requests. Null clears it. No-op on databases predating either
        /// DistributorId migration. Dynamic SQL is required: static column refs
        /// fail compile-time binding on old DBs.
        /// </summary>
        public async Task UpdateSetDistributorAsync(int setId, int? distributorId, int modifiedByUserId)
        {
            const string sql = @"
                IF COL_LENGTH('dbo.[Set]', 'DistributorId') IS NOT NULL
                BEGIN
                    EXEC sp_executesql
                        N'UPDATE dbo.[Set] SET DistributorId = @DistributorId WHERE SetId = @SetId;',
                        N'@SetId INT, @DistributorId INT',
                        @SetId = @SetId, @DistributorId = @DistributorId;
                END
                IF COL_LENGTH('dbo.[Set]', 'DistributorId') IS NOT NULL
                AND COL_LENGTH('dbo.Request', 'DistributorId') IS NOT NULL
                BEGIN
                    EXEC sp_executesql
                        N'UPDATE dbo.Request SET DistributorId = @DistributorId WHERE SetId = @SetId;',
                        N'@SetId INT, @DistributorId INT',
                        @SetId = @SetId, @DistributorId = @DistributorId;
                END";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@DistributorId", (object)distributorId ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            string distributorLabel = null;
            if (distributorId.HasValue)
            {
                try
                {
                    using (var con = new SqlConnection(GetConnectionString()))
                    using (var cmd = new SqlCommand(
                        "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT Name FROM dbo.Distributor WHERE DistributorId = @DistributorId", con))
                    {
                        cmd.Parameters.AddWithValue("@DistributorId", distributorId.Value);
                        await con.OpenAsync();
                        var result = await cmd.ExecuteScalarAsync();
                        distributorLabel = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                    }
                }
                catch
                {
                    // Distributor catalog unavailable; audit still records the id path below.
                }
            }

            ActivityLogger.Log(modifiedByUserId, ActivityLogger.Actions.Update,
                "Set", setId,
                $"Set #{setId} distributor set to {distributorLabel ?? "(None)"}");
        }

        /// <summary>
        /// Gets all unassigned requests (SetId is NULL) for adding to sets
        /// </summary>
        public async Task<List<RequestDto>> GetUnassignedRequestsAsync()
        {
            var requests = new List<RequestDto>();

            bool hasUnassignedDistributor;
            using (var distCheckCon = new SqlConnection(GetConnectionString()))
            {
                await distCheckCon.OpenAsync();
                using (var distCheckCmd = new SqlCommand(
                    "SELECT CASE WHEN COL_LENGTH('dbo.Request', 'DistributorId') IS NULL THEN 0 ELSE 1 END",
                    distCheckCon))
                {
                    hasUnassignedDistributor = Convert.ToInt32(await distCheckCmd.ExecuteScalarAsync()) == 1;
                }
            }
            string unassignedDistributorSelect = hasUnassignedDistributor
                ? "r.DistributorId,\n                    dist.Name AS DistributorName,"
                : "CAST(NULL AS INT) AS DistributorId,\n                    CAST(NULL AS NVARCHAR(200)) AS DistributorName,";
            string unassignedDistributorJoin = hasUnassignedDistributor
                ? "LEFT  JOIN dbo.Distributor dist ON dist.DistributorId = r.DistributorId"
                : string.Empty;

            string sql = @"
                SELECT
                    r.ReqId,
                    r.Description,
                    r.Quantity,
                    r.Status,
                    r.ItemId,
                    i.Name AS ItemName,
                    i.Category,
                    r.EmpId,
                    r.ComId,
                    r.DeptId,
                    r.BranchId,
                    " + unassignedDistributorSelect + @"
                    r.DateCreated,
                    r.DateRequested,
                    e.Name   AS EmployeeName,
                    co.Name  AS CompanyName,
                    d.Name   AS DepartmentName,
                    b.Name   AS BranchName
                FROM dbo.Request r
                INNER JOIN dbo.Item     i  ON r.ItemId   = i.ItemId
                LEFT  JOIN dbo.Employee e  ON r.EmpId    = e.EmpId
                LEFT  JOIN dbo.Company  co ON r.ComId    = co.ComId
                LEFT  JOIN dbo.Department d ON r.DeptId  = d.DeptId
                LEFT  JOIN dbo.Branch   b  ON r.BranchId = b.BranchId
                " + unassignedDistributorJoin + @"
                WHERE r.SetId IS NULL
                  AND ISNULL(i.Category, '') <> 'Cartridge'
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string empName    = reader.IsDBNull(reader.GetOrdinal("EmployeeName"))  ? null : reader.GetString(reader.GetOrdinal("EmployeeName"));
                        string coName     = reader.IsDBNull(reader.GetOrdinal("CompanyName"))   ? null : reader.GetString(reader.GetOrdinal("CompanyName"));
                        string deptName   = reader.IsDBNull(reader.GetOrdinal("DepartmentName"))? null : reader.GetString(reader.GetOrdinal("DepartmentName"));
                        string branchName = reader.IsDBNull(reader.GetOrdinal("BranchName"))    ? null : reader.GetString(reader.GetOrdinal("BranchName"));
                        int? distId       = reader.IsDBNull(reader.GetOrdinal("DistributorId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DistributorId"));
                        string distName   = reader.IsDBNull(reader.GetOrdinal("DistributorName")) ? null : reader.GetString(reader.GetOrdinal("DistributorName"));

                        // For dept-level requests build a readable label e.g. "Company / Dept / Branch".
                        // An independent sales distributor (not under any Company/Dept/Branch)
                        // is appended when present, e.g. "Company / Dept / Branch / LACTO-B".
                        string displayName = empName;
                        if (displayName == null)
                        {
                            var parts = new System.Collections.Generic.List<string>();
                            if (coName     != null) parts.Add(coName);
                            if (deptName   != null) parts.Add(deptName);
                            if (branchName != null) parts.Add(branchName);
                            if (distName   != null) parts.Add(distName);
                            displayName = parts.Count > 0 ? string.Join(" / ", parts) : "(Dept. Level)";
                        }

                        requests.Add(new RequestDto
                        {
                            ReqId        = reader.GetInt32(reader.GetOrdinal("ReqId")),
                            Description  = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Quantity     = reader.GetInt32(reader.GetOrdinal("Quantity")),
                            Status       = reader.GetString(reader.GetOrdinal("Status")),
                            ItemId       = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemName     = reader.GetString(reader.GetOrdinal("ItemName")),
                            Category     = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            DateCreated  = reader.IsDBNull(reader.GetOrdinal("DateCreated")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            DateRequested = reader.IsDBNull(reader.GetOrdinal("DateRequested")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRequested")),
                            EmpId        = reader.IsDBNull(reader.GetOrdinal("EmpId"))    ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                            ComId        = reader.IsDBNull(reader.GetOrdinal("ComId"))    ? (int?)null : reader.GetInt32(reader.GetOrdinal("ComId")),
                            DeptId       = reader.IsDBNull(reader.GetOrdinal("DeptId"))   ? (int?)null : reader.GetInt32(reader.GetOrdinal("DeptId")),
                            BranchId     = reader.IsDBNull(reader.GetOrdinal("BranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("BranchId")),
                            DistributorId  = distId,
                            DistributorName = distName,
                            EmployeeName    = displayName,
                            CompanyName     = coName,
                            DepartmentName  = deptName,
                            BranchName      = branchName
                        });
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Updates the financial calculations for a set
        /// </summary>
        public async Task UpdateFinancialCalculationsAsync(
            int setId, 
            decimal subtotal, 
            decimal vatAmount, 
            decimal discountAmount,
            decimal whtAmount, 
            decimal totalAmountDue)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET 
                    Subtotal = @Subtotal,
                    VatAmount = @VatAmount,
                    DiscountAmount = @DiscountAmount,
                    WhtAmount = @WhtAmount,
                    TotalAmountDue = @TotalAmountDue
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@Subtotal", subtotal);
                cmd.Parameters.AddWithValue("@VatAmount", vatAmount);
                cmd.Parameters.AddWithValue("@DiscountAmount", discountAmount);
                cmd.Parameters.AddWithValue("@WhtAmount", whtAmount);
                cmd.Parameters.AddWithValue("@TotalAmountDue", totalAmountDue);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Transfers set ownership by updating all requests in the set to a new employee.
        /// This effectively changes who owns the entire set and logs the transfer in the audit trail.
        /// </summary>
        public async Task TransferSetOwnershipAsync(int setId, int newEmployeeId)
        {
            // Get current owner info BEFORE transfer
            var currentOwner = await GetCurrentSetOwnerAsync(setId);
            
            // Get new owner info
            var newOwner = await GetEmployeeByIdAsync(newEmployeeId);
            
            // Get all items in this set for audit logging
            var setItems = await GetSetItemsAsync(setId);
            
            // Log transfer initiation for all items
            await LogTransferAuditAsync(setItems, currentOwner, newOwner, "Transfer Initiated", setId);
            
            // Update database (existing logic)
            const string sql = @"
                UPDATE dbo.Request
                SET EmpId = @NewEmployeeId
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@NewEmployeeId", newEmployeeId);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            
            // Log transfer completion for all items
            await LogTransferAuditAsync(setItems, currentOwner, newOwner, "Transfer Completed", setId);

            // Header-level record of the transfer cycle for the ownership history page
            await LogSetOwnershipAuditAsync(setId, AppSession.CurrentUserId,
                $"Ownership transfer cycle: {currentOwner.Name} -> {newOwner.Name}",
                new SetOwnershipSnapshot { CurrentEmployeeId = currentOwner.EmpId == 0 ? (int?)null : currentOwner.EmpId, EmployeeName = currentOwner.EmpId == 0 ? null : currentOwner.Name, DepartmentName = currentOwner.Department, BranchName = currentOwner.Branch, CompanyName = currentOwner.Company },
                new SetOwnershipSnapshot { CurrentEmployeeId = newOwner.EmpId, EmployeeName = newOwner.Name, DepartmentName = newOwner.Department, BranchName = newOwner.Branch, CompanyName = newOwner.Company });
        }

        public async Task TransferSetOwnershipToDeptAsync(int setId, int comId, int? deptId, int? branchId)
        {
            var currentOwner = await GetCurrentSetOwnerAsync(setId);

            const string sql = @"
                UPDATE dbo.Request
                SET    EmpId    = NULL,
                       ComId    = @ComId,
                       DeptId   = @DeptId,
                       BranchId = @BranchId
                WHERE  SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId",    setId);
                cmd.Parameters.AddWithValue("@ComId",    comId);
                cmd.Parameters.AddWithValue("@DeptId",   (object)deptId   ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }

            var afterOrgUnit = await GetOrgUnitNamesAsync(comId, deptId, branchId);
            await LogSetOwnershipAuditAsync(setId, AppSession.CurrentUserId,
                $"Ownership transfer cycle (dept level): {currentOwner.Name} -> {afterOrgUnit.CompanyName ?? "Unassigned"}",
                new SetOwnershipSnapshot { CurrentEmployeeId = currentOwner.EmpId == 0 ? (int?)null : currentOwner.EmpId, EmployeeName = currentOwner.EmpId == 0 ? null : currentOwner.Name, DepartmentName = currentOwner.Department, BranchName = currentOwner.Branch, CompanyName = currentOwner.Company },
                afterOrgUnit);
        }

        private async Task<SetOwnershipSnapshot> GetOrgUnitNamesAsync(int? comId, int? deptId, int? branchId)
        {
            const string sql = @"
                SELECT
                    (SELECT Name FROM dbo.Company WHERE ComId = @ComId) AS CompanyName,
                    (SELECT Name FROM dbo.Department WHERE DeptId = @DeptId) AS DepartmentName,
                    (SELECT Name FROM dbo.Branch WHERE BranchId = @BranchId) AS BranchName";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ComId", (object)comId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);

                await con.OpenAsync();
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        return new SetOwnershipSnapshot
                        {
                            ComId = comId,
                            CompanyName = rdr.IsDBNull(rdr.GetOrdinal("CompanyName")) ? null : rdr.GetString(rdr.GetOrdinal("CompanyName")),
                            CurrentDepartmentId = deptId,
                            DepartmentName = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? null : rdr.GetString(rdr.GetOrdinal("DepartmentName")),
                            CurrentBranchId = branchId,
                            BranchName = rdr.IsDBNull(rdr.GetOrdinal("BranchName")) ? null : rdr.GetString(rdr.GetOrdinal("BranchName"))
                        };
                    }
                }
            }

            return new SetOwnershipSnapshot { ComId = comId, CurrentDepartmentId = deptId, CurrentBranchId = branchId };
        }

        /// <summary>
        /// Reflects the Set's current owner, whether it's Employee-level (dbo.Request.EmpId set)
        /// or Department-level (dbo.Request.EmpId is NULL, ComId/DeptId/BranchId set directly).
        /// </summary>
        private async Task<EmployeeInfo> GetCurrentSetOwnerAsync(int setId)
        {
            const string sql = @"
                SELECT TOP 1
                    r.EmpId,
                    e.Name AS EmployeeName,
                    COALESCE(ed.Name, rd.Name) AS DepartmentName,
                    COALESCE(eb.Name, rb.Name) AS BranchName,
                    COALESCE(ec.Name, rc.Name) AS CompanyName
                FROM dbo.Request r
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Department ed ON e.DeptId = ed.DeptId
                LEFT JOIN dbo.Branch eb ON e.BranchId = eb.BranchId
                LEFT JOIN dbo.Company ec ON e.ComId = ec.ComId
                LEFT JOIN dbo.Department rd ON r.DeptId = rd.DeptId
                LEFT JOIN dbo.Branch rb ON r.BranchId = rb.BranchId
                LEFT JOIN dbo.Company rc ON r.ComId = rc.ComId
                WHERE r.SetId = @SetId
                ORDER BY CASE WHEN r.EmpId IS NOT NULL THEN 0 ELSE 1 END";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        bool isEmployeeLevel = !rdr.IsDBNull(rdr.GetOrdinal("EmpId"));
                        string department = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? "Unassigned" : rdr.GetString(rdr.GetOrdinal("DepartmentName"));
                        string branch = rdr.IsDBNull(rdr.GetOrdinal("BranchName")) ? "Unassigned" : rdr.GetString(rdr.GetOrdinal("BranchName"));
                        string company = rdr.IsDBNull(rdr.GetOrdinal("CompanyName")) ? null : rdr.GetString(rdr.GetOrdinal("CompanyName"));

                        if (isEmployeeLevel)
                        {
                            return new EmployeeInfo
                            {
                                EmpId = rdr.GetInt32(rdr.GetOrdinal("EmpId")),
                                Name = rdr.IsDBNull(rdr.GetOrdinal("EmployeeName")) ? "Unknown" : rdr.GetString(rdr.GetOrdinal("EmployeeName")),
                                Department = department,
                                Branch = branch,
                                Company = company
                            };
                        }

                        return new EmployeeInfo
                        {
                            EmpId = 0,
                            Name = company != null ? $"Department Level ({company})" : "Department Level",
                            Department = department,
                            Branch = branch,
                            Company = company
                        };
                    }
                }
            }

            return new EmployeeInfo { EmpId = 0, Name = "Unassigned", Department = "Unassigned", Branch = "Unassigned" };
        }

        private async Task<EmployeeInfo> GetEmployeeByIdAsync(int employeeId)
        {
            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name AS EmployeeName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName,
                    c.Name AS CompanyName
                FROM dbo.Employee e
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                WHERE e.EmpId = @EmpId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EmpId", employeeId);
                await con.OpenAsync();

                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (await rdr.ReadAsync())
                    {
                        return new EmployeeInfo
                        {
                            EmpId = rdr.GetInt32(rdr.GetOrdinal("EmpId")),
                            Name = rdr.IsDBNull(rdr.GetOrdinal("EmployeeName")) ? "Unknown" : rdr.GetString(rdr.GetOrdinal("EmployeeName")),
                            Department = rdr.IsDBNull(rdr.GetOrdinal("DepartmentName")) ? "Unassigned" : rdr.GetString(rdr.GetOrdinal("DepartmentName")),
                            Branch = rdr.IsDBNull(rdr.GetOrdinal("BranchName")) ? "Unassigned" : rdr.GetString(rdr.GetOrdinal("BranchName")),
                            Company = rdr.IsDBNull(rdr.GetOrdinal("CompanyName")) ? null : rdr.GetString(rdr.GetOrdinal("CompanyName"))
                        };
                    }
                }
            }

            return new EmployeeInfo { EmpId = employeeId, Name = "Unknown", Department = "Unassigned", Branch = "Unassigned" };
        }

        private async Task<List<SetItemInfo>> GetSetItemsAsync(int setId)
        {
            const string sql = @"
                SELECT 
                    i.ItemId,
                    i.SerialNumber,
                    s.SetCode
                FROM dbo.Request r
                JOIN dbo.Item i ON r.ItemId = i.ItemId
                JOIN dbo.[Set] s ON r.SetId = s.SetId
                WHERE r.SetId = @SetId";

            var items = new List<SetItemInfo>();
            
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();
                
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        items.Add(new SetItemInfo
                        {
                            ItemId = rdr.GetInt32(rdr.GetOrdinal("ItemId")),
                            SerialNumber = rdr.IsDBNull(rdr.GetOrdinal("SerialNumber")) ? null : rdr.GetString(rdr.GetOrdinal("SerialNumber")),
                            SetCode = rdr.IsDBNull(rdr.GetOrdinal("SetCode")) ? null : rdr.GetString(rdr.GetOrdinal("SetCode"))
                        });
                    }
                }
            }
            
            return items;
        }

        private async Task LogTransferAuditAsync(List<SetItemInfo> setItems, EmployeeInfo fromOwner, EmployeeInfo toOwner, string status, int setId)
        {
            var auditRepo = new ItemAuditTrailRepository();
            
            foreach (var item in setItems)
            {
                var isInitiation = status == "Transfer Initiated";
                
                await auditRepo.LogActionAsync(new ItemAuditTrailDto
                {
                    ItemId = item.ItemId,
                    SerialNumber = item.SerialNumber,
                    Action = isInitiation ? "Ownership Transfer Initiated" : "Ownership Transfer Completed",
                    ActionTime = DateTime.Now,
                    Direction = isInitiation ? "OUT" : "IN",
                    Status = isInitiation ? "In Progress" : "Completed",
                    EmployeeName = isInitiation ? fromOwner.Name : toOwner.Name,
                    DepartmentName = isInitiation ? fromOwner.Department : toOwner.Department,
                    BranchName = isInitiation ? fromOwner.Branch : toOwner.Branch,
                    ReferenceType = "Set",
                    ReferenceId = setId,
                    SetCode = item.SetCode,
                    Notes = isInitiation 
                        ? $"Transfer initiated from {fromOwner.Name} to {toOwner.Name}"
                        : $"Transfer completed - now owned by {toOwner.Name}",
                    CreatedBy = AppSession.CurrentUserName ?? "System"
                });
            }
        }

        private async Task<RequestItemInfo> GetRequestItemInfoAsync(int reqId)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.ItemId,
                    i.SerialNumber,
                    r.SetId,
                    s.SetCode,
                    r.EmpId,
                    e.Name AS EmployeeName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName
                FROM dbo.Request r
                LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.[Set] s ON r.SetId = s.SetId
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                await con.OpenAsync();
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    if (!await rdr.ReadAsync())
                        return null;

                    return new RequestItemInfo
                    {
                        ReqId = reqId,
                        ItemId = rdr["ItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ItemId"]),
                        SerialNumber = rdr["SerialNumber"] == DBNull.Value ? null : Convert.ToString(rdr["SerialNumber"]),
                        SetId = rdr["SetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["SetId"]),
                        SetCode = rdr["SetCode"] == DBNull.Value ? null : Convert.ToString(rdr["SetCode"]),
                        EmpId = rdr["EmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["EmpId"]),
                        EmployeeName = rdr["EmployeeName"] == DBNull.Value ? null : Convert.ToString(rdr["EmployeeName"]),
                        DepartmentName = rdr["DepartmentName"] == DBNull.Value ? null : Convert.ToString(rdr["DepartmentName"]),
                        BranchName = rdr["BranchName"] == DBNull.Value ? null : Convert.ToString(rdr["BranchName"])
                    };
                }
            }
        }

        private async Task<List<RequestItemInfo>> GetRequestsInSetAsync(int setId)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.ItemId,
                    i.SerialNumber,
                    r.SetId,
                    s.SetCode,
                    r.EmpId,
                    e.Name AS EmployeeName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName
                FROM dbo.Request r
                INNER JOIN dbo.[Set] s ON r.SetId = s.SetId
                LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE r.SetId = @SetId";

            var results = new List<RequestItemInfo>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();
                using (var rdr = await cmd.ExecuteReaderAsync())
                {
                    while (await rdr.ReadAsync())
                    {
                        results.Add(new RequestItemInfo
                        {
                            ReqId = Convert.ToInt32(rdr["ReqId"]),
                            ItemId = rdr["ItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ItemId"]),
                            SerialNumber = rdr["SerialNumber"] == DBNull.Value ? null : Convert.ToString(rdr["SerialNumber"]),
                            SetId = rdr["SetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["SetId"]),
                            SetCode = rdr["SetCode"] == DBNull.Value ? null : Convert.ToString(rdr["SetCode"]),
                            EmpId = rdr["EmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["EmpId"]),
                            EmployeeName = rdr["EmployeeName"] == DBNull.Value ? null : Convert.ToString(rdr["EmployeeName"]),
                            DepartmentName = rdr["DepartmentName"] == DBNull.Value ? null : Convert.ToString(rdr["DepartmentName"]),
                            BranchName = rdr["BranchName"] == DBNull.Value ? null : Convert.ToString(rdr["BranchName"])
                        });
                    }
                }
            }
            return results;
        }

        private async Task<List<RequestItemInfo>> GetRequestsInSetAsync(int setId, SqlConnection connection, SqlTransaction transaction)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.ItemId,
                    i.SerialNumber,
                    r.SetId,
                    s.SetCode,
                    r.EmpId,
                    e.Name AS EmployeeName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName
                FROM dbo.Request r
                INNER JOIN dbo.[Set] s ON r.SetId = s.SetId
                LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE r.SetId = @SetId";

            var results = new List<RequestItemInfo>();
            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                using (var rdr = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rdr.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(new RequestItemInfo
                        {
                            ReqId = Convert.ToInt32(rdr["ReqId"]),
                            ItemId = rdr["ItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ItemId"]),
                            SerialNumber = rdr["SerialNumber"] == DBNull.Value ? null : Convert.ToString(rdr["SerialNumber"]),
                            SetId = rdr["SetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["SetId"]),
                            SetCode = rdr["SetCode"] == DBNull.Value ? null : Convert.ToString(rdr["SetCode"]),
                            EmpId = rdr["EmpId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["EmpId"]),
                            EmployeeName = rdr["EmployeeName"] == DBNull.Value ? null : Convert.ToString(rdr["EmployeeName"]),
                            DepartmentName = rdr["DepartmentName"] == DBNull.Value ? null : Convert.ToString(rdr["DepartmentName"]),
                            BranchName = rdr["BranchName"] == DBNull.Value ? null : Convert.ToString(rdr["BranchName"])
                        });
                    }
                }
            }

            return results;
        }

        private async Task LogRequestSetAssignmentAsync(RequestItemInfo info, int? setId, string setCode, string action, string notes)
        {
            if (info == null || !info.ItemId.HasValue)
                return;

            var auditRepo = new ItemAuditTrailRepository();
            await auditRepo.LogActionAsync(new ItemAuditTrailDto
            {
                ItemId = info.ItemId,
                SerialNumber = info.SerialNumber,
                Action = action,
                ActionTime = DateTime.Now,
                Status = "Completed",
                EmployeeName = info.EmployeeName,
                DepartmentName = info.DepartmentName,
                BranchName = info.BranchName,
                ReferenceType = "Set",
                ReferenceId = setId,
                SetCode = setCode,
                Notes = notes,
                CreatedBy = AppSession.CurrentUserName ?? "System"
            });
        }

        // Helper classes for transfer tracking
        private class EmployeeInfo
        {
            public int EmpId { get; set; }
            public string Name { get; set; }
            public string Department { get; set; }
            public string Branch { get; set; }
            public string Company { get; set; }
        }

        private class RequestItemInfo
        {
            public int ReqId { get; set; }
            public int? ItemId { get; set; }
            public string SerialNumber { get; set; }
            public int? SetId { get; set; }
            public string SetCode { get; set; }
            public int? EmpId { get; set; }
            public string EmployeeName { get; set; }
            public string DepartmentName { get; set; }
            public string BranchName { get; set; }
        }

        private class SetItemInfo
        {
            public int ItemId { get; set; }
            public string SerialNumber { get; set; }
            public string SetCode { get; set; }
        }

        /// <summary>
        /// Sends deployment email using VIEWSET_DEPLOYMENT_SUCCESS template
        /// </summary>
        private async Task<string> SendDeploymentEmailAsync(int setId, string setCode, List<RequestItemInfo> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                Logger.LogWarning($"SetRepository.SendDeploymentEmailAsync: No requests found for SetId={setId}");
                return null;
            }

            // Get requester data from first request
            var firstRequest = requests[0];
            if (!firstRequest.EmpId.HasValue)
            {
                Logger.LogWarning($"SetRepository.SendDeploymentEmailAsync: No employee found for SetId={setId}");
                return "No employee is linked to this set's request. Cannot send notification.";
            }

            var emailRepo = new EmailRepository();
            var emailService = new SystemEmailNotificationService(emailRepo);

            // Get active SMTP profile
            var profiles = await emailRepo.GetSmtpProfilesAsync(activeOnly: true);
            if (profiles == null || profiles.Count == 0)
            {
                Logger.LogWarning("SetRepository.SendDeploymentEmailAsync: No active SMTP profile found");
                return "No active SMTP profile is configured. Go to Email Settings â†’ SMTP Profiles to add one.";
            }

            var activeProfile = profiles[0];

            // Reuse the same data sources as ViewSetDetailPage.LoadSetDetailsAsync()
            // to ensure email content matches what user sees in UI
            var setDto = await GetSetByIdAsync(setId);
            var setRequests = await GetSetRequestsAsync(setId);

            if (setDto != null && !string.IsNullOrWhiteSpace(setDto.SetType)
                && string.Equals(setDto.SetType.Trim(), "Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo($"SetRepository.SendDeploymentEmailAsync: Skipping VIEWSET_DEPLOYMENT_SUCCESS for cartridge SetId={setId}");
                return null;
            }

            var emailData = await BuildViewSetDeploymentEmailDataAsync(setId, setCode, firstRequest, setDto, setRequests, emailRepo);
            var placeholders = emailData.Placeholders;
            var inlineImageFilePathsByContentId = emailData.InlineImages;

            // Send email using template
            var result = await emailService.SendEmailAsync(
                templateKey: "VIEWSET_DEPLOYMENT_SUCCESS",
                placeholders: placeholders,
                profileId: activeProfile.ProfileId,
                recipientEmails: null,
                empId: firstRequest.EmpId.Value,
                branchId: null,
                entityType: "Set",
                entityId: setId,
                sentByUserId: AppSession.CurrentUserId,
                attachmentFilePaths: null,
                inlineImageFilePathsByContentId: inlineImageFilePathsByContentId
            );

            if (result.SentSuccessfully)
            {
                Logger.LogInfo($"Deployment email sent successfully for SetId={setId} to employee {firstRequest.EmployeeName}");
                return null;
            }
            else if (result.WasSkipped)
            {
                Logger.LogWarning($"Deployment email skipped for SetId={setId}: {result.Message}");
                return result.Message; // Surface the skip reason so the user knows why nothing was sent
            }
            else
            {
                Logger.LogWarning($"Deployment email failed for SetId={setId}: {result.Message}");
                return result.Message;
            }
        }

        /// <summary>
        /// Gets all images for a specific set
        /// </summary>
        public async Task<List<SetImageDto>> GetSetImagesAsync(int setId)
        {
            const string sql = @"
                SELECT ImageId, SetId, ImagePath, ImageType, UploadedBy, UploadDate,
                       ImageData, MimeType, OriginalFileName, FileSizeBytes
                FROM SetImages
                WHERE SetId = @SetId
                ORDER BY UploadDate DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                await con.OpenAsync();

                var images = new List<SetImageDto>();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        images.Add(new SetImageDto
                        {
                            ImageId = Convert.ToInt32(reader[0]),
                            SetId = Convert.ToInt32(reader[1]),
                            ImagePath = reader[2] == DBNull.Value ? null : Convert.ToString(reader[2]),
                            ImageType = Convert.ToString(reader[3]),
                            UploadedBy = reader[4] == DBNull.Value ? "Unknown" : Convert.ToString(reader[4]),
                            UploadDate = Convert.ToDateTime(reader[5]),
                            ImageData = reader[6] == DBNull.Value ? null : (byte[])reader[6],
                            MimeType = reader[7] == DBNull.Value ? null : Convert.ToString(reader[7]),
                            OriginalFileName = reader[8] == DBNull.Value ? null : Convert.ToString(reader[8]),
                            FileSizeBytes = reader[9] == DBNull.Value ? (int?)null : Convert.ToInt32(reader[9])
                        });
                    }
                }
                return images;
            }
        }

        /// <summary>
        /// Adds a new image for a set, storing the bytes directly in the database
        /// </summary>
        public async Task<int> AddSetImageAsync(int setId, byte[] imageData, string mimeType, string originalFileName, int fileSizeBytes, string imageType = "Photo", string uploadedBy = "Mobile")
        {
            const string sql = @"
                INSERT INTO SetImages (SetId, ImageData, MimeType, OriginalFileName, FileSizeBytes, ImageType, UploadedBy)
                VALUES (@SetId, @ImageData, @MimeType, @OriginalFileName, @FileSizeBytes, @ImageType, @UploadedBy);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@ImageData", imageData);
                cmd.Parameters.AddWithValue("@MimeType", (object)mimeType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OriginalFileName", (object)originalFileName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FileSizeBytes", fileSizeBytes);
                cmd.Parameters.AddWithValue("@ImageType", imageType);
                cmd.Parameters.AddWithValue("@UploadedBy", uploadedBy);

                await con.OpenAsync();
                return (int)await cmd.ExecuteScalarAsync();
            }
        }

        public async Task<int> AddSetImageFromFileAsync(
            int setId,
            string sourceFilePath,
            string imageType = "Photo",
            string uploadedBy = "Desktop")
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Selected file does not exist.", sourceFilePath);

            string mimeType = Helpers.LocalImageFileHelper.GetMimeType(sourceFilePath);
            byte[] imageData = File.ReadAllBytes(sourceFilePath);
            string originalFileName = Path.GetFileName(sourceFilePath);

            return await AddSetImageAsync(setId, imageData, mimeType, originalFileName, imageData.Length, imageType, uploadedBy);
        }

        /// <summary>
        /// Sets still storing their QR code only as a local file path (pre-DB-storage migration).
        /// </summary>
        public async Task<List<QrBackfillCandidateDto>> GetSetsPendingQrBackfillAsync()
        {
            const string sql = @"
                SELECT SetId, QRImagePath
                FROM dbo.[Set]
                WHERE QRImageData IS NULL AND QRImagePath IS NOT NULL";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                var results = new List<QrBackfillCandidateDto>();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new QrBackfillCandidateDto
                        {
                            SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                            QRImagePath = reader.GetString(reader.GetOrdinal("QRImagePath"))
                        });
                    }
                }
                return results;
            }
        }

        /// <summary>
        /// Backfills a Set row's QRImageData from a locally-read QR PNG file. Guarded by
        /// "QRImageData IS NULL" so it's safe to re-run without clobbering an already-migrated row.
        /// </summary>
        public async Task<bool> BackfillSetQrImageDataAsync(int setId, byte[] qrImageData)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET QRImageData = @QRImageData
                WHERE SetId = @SetId AND QRImageData IS NULL";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                cmd.Parameters.AddWithValue("@QRImageData", qrImageData);

                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Deletes an image
        /// </summary>
        public async Task<bool> DeleteSetImageAsync(int imageId)
        {
            const string sql = "DELETE FROM SetImages WHERE ImageId = @ImageId";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ImageId", imageId);
                await con.OpenAsync();
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Materializes dbo.SetItem rows from dbo.Request for a given SetId.
        /// This is the CANONICAL SetItem creation pattern used by cartridge transactions.
        ///
        /// CRITICAL BUSINESS RULE:
        /// - SetItem is the transactional source of truth for Set contents
        /// - Must be populated for ALL items in a Set, regardless of:
        ///   * ItemType (Hardware/Software/Service)
        ///   * EntryType
        ///   * AffectsInventory flag
        ///
        /// Pattern (replicated from CartridgeManagementRepository):
        /// 1. Check IF NOT EXISTS to prevent duplicates
        /// 2. INSERT INTO SetItem by selecting from Request JOIN Item
        /// 3. Maps Request fields to SetItem columns
        ///
        /// Call this method:
        /// - After creating a Set from Requests
        /// - Before finalizing/dispatching a Set
        /// - When materializing Request data for SMTP emails or reports
        /// </summary>
        /// <param name="setId">The SetId to materialize SetItems for</param>
        /// <param name="connection">Active SqlConnection (optional, creates new if null)</param>
        /// <param name="transaction">Active SqlTransaction (optional, uses connection's transaction if provided)</param>
        /// <returns>Number of SetItem rows inserted (0 if already materialized)</returns>
        public async Task<int> MaterializeSetItemsFromRequestsAsync(int setId, SqlConnection connection = null, SqlTransaction transaction = null)
        {
            if (setId <= 0)
            {
                throw new ArgumentException("SetId must be greater than 0", nameof(setId));
            }

            bool ownsConnection = connection == null;
            SqlConnection conn = connection ?? new SqlConnection(GetConnectionString());

            try
            {
                if (ownsConnection)
                {
                    await conn.OpenAsync();
                }

                // REFERENCE IMPLEMENTATION: Copied from CartridgeManagementRepository
                // This is the canonical pattern for materializing SetItem from Request
                // NOT EXISTS is scoped per Request/Item (not a blanket "does this Set have
                // any SetItem yet" check) -- otherwise materializing the first request in a
                // Set would insert its row, and every subsequent request linked to the same
                // Set afterward would be silently skipped because the Set already "has" a
                // SetItem row. That collapsed ItemCount to 1 for every multi-request Set.
                const string sqlMaterializeSetItem = @"
                    INSERT dbo.SetItem (
                        SetId, ItemId, ItemCode, Description,
                        Quantity, UnitOfMeasure, UnitPrice, Amount,
                        LineStartDate, LineEndDate, CreatedBy
                    )
                    SELECT
                        r.SetId,
                        r.ItemId,
                        i.ModelNumber,
                        i.Name,
                        r.Quantity,
                        i.UnitOfMeasure,
                        r.UnitPrice,
                        r.Quantity * r.UnitPrice,
                        r.DateRequested,
                        NULL,
                        r.CreatedBy
                    FROM dbo.Request r
                    JOIN dbo.Item i ON i.ItemId = r.ItemId
                    WHERE r.SetId = @SetId
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.SetItem si
                          WHERE si.SetId = r.SetId AND si.ItemId = r.ItemId
                      );

                    SELECT @@ROWCOUNT";

                using (var cmd = new SqlCommand(sqlMaterializeSetItem, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    var result = await cmd.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }
            }
            finally
            {
                if (ownsConnection && conn != null)
                {
                    conn.Dispose();
                }
            }
        }

        // â”€â”€ Notification Page Support â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Returns dispatched inventory Sets from the last 90 days for the Send Notifications page.
        /// Unlike the cartridge version, these are NOT filtered by portal origin.
        /// </summary>
        public List<DispatchedSetNotificationDto> GetDispatchedSetsForNotification()
        {
            var list = new List<DispatchedSetNotificationDto>();

            const string sql = @"
                SELECT TOP 300
                    s.SetId,
                    ISNULL(s.SetCode, '') AS SetCode,
                    ISNULL(s.Status, 'Pending') AS SetStatus,
                    ISNULL(s.SetType, '') AS SetType,
                    s.CreatedAt,
                    s.ReceivedById,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    ISNULL(req_emp.Name, '') AS EmployeeName,
                    ISNULL(b.Name, '') AS BranchName,
                    ISNULL(d.Name, '') AS DepartmentName
                FROM dbo.[Set] s
                CROSS APPLY (
                    SELECT TOP 1 r.EmpId
                    FROM dbo.Request r
                    WHERE r.SetId = s.SetId
                    ORDER BY r.ReqId
                ) AS TOP_REQ
                LEFT JOIN dbo.Employee req_emp ON req_emp.EmpId = TOP_REQ.EmpId
                LEFT JOIN dbo.Branch    b       ON b.BranchId   = req_emp.BranchId
                LEFT JOIN dbo.Department d      ON d.DeptId     = req_emp.DeptId
                LEFT JOIN dbo.Employee recv_emp ON recv_emp.EmpId = s.ReceivedById
                WHERE s.SetType <> 'Cartridge'
                  AND s.CreatedAt >= DATEADD(day, -90, GETDATE())
                ORDER BY s.CreatedAt DESC";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new DispatchedSetNotificationDto
                        {
                            SetId          = reader.GetInt32(reader.GetOrdinal("SetId")),
                            SetCode        = reader.GetString(reader.GetOrdinal("SetCode")),
                            SetStatus      = reader.GetString(reader.GetOrdinal("SetStatus")),
                            SetType        = reader.GetString(reader.GetOrdinal("SetType")),
                            CreatedAt      = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            ReceivedById   = reader.IsDBNull(reader.GetOrdinal("ReceivedById")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ReceivedById")),
                            ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                            EmployeeName   = reader.GetString(reader.GetOrdinal("EmployeeName")),
                            BranchName     = reader.GetString(reader.GetOrdinal("BranchName")),
                            DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName"))
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Returns all active employees for the Received By lookup ComboBox.
        /// </summary>
        public List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> GetActiveEmployeesForReceiver()
        {
            var list = new List<(int, string, string, string, string, string)>();

            const string sql = @"
                SELECT e.EmpId, e.Name, ISNULL(e.EmployeeNumber, '') AS EmployeeNumber,
                       ISNULL(e.Position, '') AS Position,
                       ISNULL(b.Name, '') AS BranchName,
                       ISNULL(d.Name, '') AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetString(4),
                            reader.GetString(5)
                        ));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Saves or clears ReceivedById on a Set.
        /// </summary>
        public void UpdateReceivedByForSet(int setId, int? receivedById, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET ReceivedById  = @ReceivedById
                WHERE SetId = @SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReceivedById", (object)receivedById ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Public overload: sends the deployment email for a Set by loading all required data internally.
        /// Returns null on success; returns an error message string on failure or skip.
        /// </summary>
        public async Task<string> SendDeploymentEmailAsync(int setId)
        {
            var requests = await GetRequestsInSetAsync(setId);
            if (requests == null || requests.Count == 0)
                return "No items were found in this set. Cannot send notification.";

            var setDto = await GetSetByIdAsync(setId);
            return await SendDeploymentEmailAsync(setId, setDto?.SetCode, requests);
        }

        /// <summary>
        /// Holds the placeholder map + inline images for a VIEWSET_DEPLOYMENT_SUCCESS email.
        /// Built by <see cref="BuildViewSetDeploymentEmailDataAsync"/> and shared by the
        /// send path and the "preview before sending" path so both render identically.
        /// </summary>
        private sealed class ViewSetDeploymentEmailData
        {
            public Dictionary<string, string> Placeholders { get; set; }
            public Dictionary<string, string> InlineImages { get; set; }
        }

        private async Task<ViewSetDeploymentEmailData> BuildViewSetDeploymentEmailDataAsync(
            int setId, string setCode, RequestItemInfo firstRequest, SetDto setDto,
            List<SetDetailRequestDto> setRequests, EmailRepository emailRepo)
        {
            // setRequests is ordered by DateCreated DESC for display, but every request in a batch
            // shares the same timestamp, so setRequests[0] is not stable. Pick the lowest ReqId
            // (earliest request added to the Set) for a deterministic result.
            var firstReqId = setRequests != null && setRequests.Count > 0
                ? setRequests.OrderBy(r => r.ReqId).First().ReqId
                : firstRequest.ReqId;

            var employeeDetail = await GetEmployeeDetailsForRequestAsync(firstReqId);
            var requesterName        = employeeDetail?.EmployeeName    ?? firstRequest.EmployeeName ?? "N/A";
            var requesterBranch     = employeeDetail?.BranchName      ?? firstRequest.BranchName   ?? "N/A";
            var requesterDepartment = employeeDetail?.DepartmentName  ?? "N/A";
            var requesterCompany    = employeeDetail?.CompanyName     ?? "N/A";
            var requesterTitle      = employeeDetail?.TitleDescription ?? string.Empty;
            var requesterDisplayName = string.IsNullOrWhiteSpace(requesterTitle)
                ? requesterName
                : $"{requesterTitle} {requesterName}";

            // Get requester email
            string requesterEmail = null;
            try
            {
                if (employeeDetail != null)
                    requesterEmail = await emailRepo.GetEmployeePrimaryEmailAsync(employeeDetail.EmpId);
                else if (firstRequest.EmpId.HasValue)
                    requesterEmail = await emailRepo.GetEmployeePrimaryEmailAsync(firstRequest.EmpId.Value);
            }
            catch
            {
                requesterEmail = null;
            }

            // Use Remarks as SetName if available, otherwise N/A
            var setName = (setDto != null && !string.IsNullOrWhiteSpace(setDto.Remarks))
                ? setDto.Remarks
                : "N/A";

            var itemListTable = SetRepositoryEmailHelpers.BuildItemListTableHtml(setRequests);
            var qrCodeUrl = SetRepositoryEmailHelpers.NormalizeQrCodeUrl(setDto?.QRImagePath);

            Dictionary<string, string> inlineImageFilePathsByContentId = null;
            try
            {
                var qrImagePath = setDto?.QRImagePath;
                if (!string.IsNullOrWhiteSpace(qrImagePath) && File.Exists(qrImagePath))
                {
                    const string contentId = "SetQrCode";
                    qrCodeUrl = $"cid:{contentId}";
                    inlineImageFilePathsByContentId = new Dictionary<string, string> { { contentId, qrImagePath } };
                }
            }
            catch
            {
                inlineImageFilePathsByContentId = null;
            }

            // Build placeholders for template rendering
            // Keep legacy placeholders for backward compatibility
            var placeholders = new Dictionary<string, string>
            {
                // New template placeholders
                { "RequesterName",        requesterName },
                { "RequesterDisplayName", requesterDisplayName },
                { "RequesterTitle",       requesterTitle },
                { "RequesterBranch",      requesterBranch },
                { "RequesterDepartment", requesterDepartment },
                { "RequesterCompany",    requesterCompany },
                { "RequesterEmail",      requesterEmail ?? string.Empty },
                { "SetCode", setCode ?? setDto?.SetCode ?? "N/A" },
                { "SetName", setName },
                { "RequestId", firstReqId.ToString() },
                { "ItemListTable", itemListTable },
                { "QrCodeUrl", qrCodeUrl },
                { "Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm") },

                // Legacy placeholders (for backward compatibility)
                { "EmployeeName",    requesterName },
                { "BranchName",      requesterBranch },
                { "DepartmentName",  requesterDepartment },
                { "CompanyName",     requesterCompany }
            };

            return new ViewSetDeploymentEmailData
            {
                Placeholders = placeholders,
                InlineImages = inlineImageFilePathsByContentId
            };
        }

        /// <summary>
        /// Renders the VIEWSET_DEPLOYMENT_SUCCESS email for a Set WITHOUT sending it,
        /// so the user can preview exactly what the "Send Notification" button would dispatch.
        /// </summary>
        public async Task<DeploymentEmailPreviewResult> GetDeploymentEmailPreviewAsync(int setId)
        {
            try
            {
                var requests = await GetRequestsInSetAsync(setId);
                if (requests == null || requests.Count == 0)
                    return new DeploymentEmailPreviewResult { Error = "No items were found in this set." };

                var firstRequest = requests[0];
                if (!firstRequest.EmpId.HasValue)
                    return new DeploymentEmailPreviewResult { Error = "No employee is linked to this set's request." };

                var setDto = await GetSetByIdAsync(setId);
                if (setDto != null && !string.IsNullOrWhiteSpace(setDto.SetType)
                    && string.Equals(setDto.SetType.Trim(), "Cartridge", StringComparison.OrdinalIgnoreCase))
                    return new DeploymentEmailPreviewResult
                    {
                        Error = "This is a cartridge set — it uses the cartridge exchange notification, not the deployment email."
                    };

                var emailRepo = new EmailRepository();
                var template = await emailRepo.GetEmailTemplateByKeyAsync("VIEWSET_DEPLOYMENT_SUCCESS");
                if (template == null)
                    return new DeploymentEmailPreviewResult { Error = "Email template 'VIEWSET_DEPLOYMENT_SUCCESS' was not found." };

                var setRequests = await GetSetRequestsAsync(setId);
                var emailData = await BuildViewSetDeploymentEmailDataAsync(
                    setId, setDto?.SetCode, firstRequest, setDto, setRequests, emailRepo);

                // The live send embeds the QR as a "cid:" inline attachment, which a raw-HTML
                // preview (no MIME parts) cannot resolve. Swap in a self-contained data: URI so
                // the QR image actually renders in the preview window.
                var qrDataUri = TryBuildImageDataUri(setDto?.QRImagePath);
                if (!string.IsNullOrEmpty(qrDataUri))
                    emailData.Placeholders["QrCodeUrl"] = qrDataUri;

                return new DeploymentEmailPreviewResult
                {
                    Subject  = SystemEmailNotificationService.Render(template.SubjectTemplate ?? string.Empty, emailData.Placeholders),
                    BodyHtml = SystemEmailNotificationService.Render(template.BodyTemplate ?? string.Empty, emailData.Placeholders),
                    IsHtml   = template.IsHtml
                };
            }
            catch (Exception ex)
            {
                return new DeploymentEmailPreviewResult { Error = ex.Message };
            }
        }

        /// <summary>
        /// Reads a local image file and returns it as a base64 "data:" URI, or null if the
        /// path is missing / not a readable local file. Used to inline images into HTML previews.
        /// </summary>
        private static string TryBuildImageDataUri(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                string mime;
                switch (ext)
                {
                    case "png":          mime = "image/png";  break;
                    case "jpg":
                    case "jpeg":         mime = "image/jpeg"; break;
                    case "gif":          mime = "image/gif";  break;
                    case "bmp":          mime = "image/bmp";  break;
                    default:             mime = "image/png";  break;
                }

                var bytes = File.ReadAllBytes(path);
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches item descriptions (names) for a batch of SetIds in one query.
        /// Returns a dictionary: SetId â†’ list of item names. A Set's items can live in either
        /// dbo.SetItem (Invoice/Software/Service sets) or dbo.Request (regular Hardware sets built
        /// through the request workflow â€” see GetSetRequestsAsync, which the Set Details "Requests
        /// in this Set" panel uses) â€” so both sources are unioned here or a set built the second
        /// way would silently report only its first item.
        /// </summary>
        public Dictionary<int, List<string>> GetItemNamesBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, List<string>>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT si.SetId, ISNULL(si.Description, '') AS Description
                FROM dbo.SetItem si
                WHERE si.SetId IN ({paramNames})
                  AND LTRIM(RTRIM(ISNULL(si.Description, ''))) <> ''

                UNION ALL

                SELECT r.SetId, ISNULL(i.Name, '') AS Description
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId IN ({paramNames})
                  AND LTRIM(RTRIM(ISNULL(i.Name, ''))) <> ''

                ORDER BY SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string desc = reader.GetString(1);
                            if (!result.ContainsKey(setId))
                                result[setId] = new List<string>();
                            result[setId].Add(desc);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches item serial numbers for a batch of SetIds in one query, mirroring
        /// GetItemNamesBySetIds. Returns a dictionary: SetId â†’ list of serial numbers. Sourced from
        /// both dbo.SetItem (Invoice/Software/Service sets) and dbo.Request (Hardware sets), same
        /// union rationale as GetItemNamesBySetIds.
        /// </summary>
        public Dictionary<int, List<string>> GetSerialNumbersBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, List<string>>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT si.SetId, i.SerialNumber
                FROM dbo.SetItem si
                INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                WHERE si.SetId IN ({paramNames})
                  AND LTRIM(RTRIM(ISNULL(i.SerialNumber, ''))) <> ''

                UNION ALL

                SELECT r.SetId, i.SerialNumber
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                WHERE r.SetId IN ({paramNames})
                  AND LTRIM(RTRIM(ISNULL(i.SerialNumber, ''))) <> ''

                ORDER BY SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string serial = reader.GetString(1);
                            if (!result.ContainsKey(setId))
                                result[setId] = new List<string>();
                            result[setId].Add(serial);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches distinct Parent Tag labels (dbo.SetItemParentTagGroup, see the Parent Tag feature —
        /// a free-text grouping like "Samsung Services" independent of Sub-Type Group) for a batch of
        /// SetIds in one query, mirroring GetItemNamesBySetIds. Returns a dictionary: SetId → list of
        /// distinct labels. Used to fold Parent Tag into a page's quick-search field list without a
        /// per-row round trip — the Invoices grid search box otherwise has no way to find an invoice
        /// by the tag it was grouped under, since Parent Tag is not a column on dbo.[Set] itself.
        /// </summary>
        public Dictionary<int, List<string>> GetParentTagLabelsBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, List<string>>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT DISTINCT si.SetId, ptg.Label
                FROM dbo.SetItem si
                INNER JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
                WHERE si.SetId IN ({paramNames})
                ORDER BY si.SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string label = reader.GetString(1);
                            if (!result.ContainsKey(setId))
                                result[setId] = new List<string>();
                            result[setId].Add(label);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches the distinct item categories present in each set, for a batch of SetIds.
        /// Returns a dictionary: SetId â†’ list of distinct category names (e.g. "Cartridge", "Ink",
        /// "Toner", "Printhead"). A Set's items can live in either dbo.SetItem (Invoice/Software/
        /// Service sets) or dbo.Request (regular Hardware sets â€” see GetSetRequestsAsync), so both
        /// sources are unioned â€” otherwise a Hardware set with e.g. Ink + Cartridge items (stored
        /// via dbo.Request) would only ever surface its first category in the Category filter.
        /// A set that mixes Cartridge items with other categories will report ALL of those
        /// categories â€” there is no exclusion of Cartridge here, so mixed sets correctly surface
        /// under a "Cartridge" filter alongside their other categories.
        /// </summary>
        public Dictionary<int, List<string>> GetCategoriesBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, List<string>>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT DISTINCT x.SetId, x.Category
                FROM (
                    SELECT si.SetId, COALESCE(ic.Name, i.Category) AS Category
                    FROM dbo.SetItem si
                    INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                    LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId = i.CategoryId
                    WHERE si.SetId IN ({paramNames})

                    UNION ALL

                    SELECT r.SetId, COALESCE(ic.Name, i.Category) AS Category
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    LEFT JOIN dbo.ItemCategory ic ON ic.CategoryId = i.CategoryId
                    WHERE r.SetId IN ({paramNames})
                ) x
                WHERE LTRIM(RTRIM(ISNULL(x.Category, ''))) <> ''
                ORDER BY x.SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string category = reader.GetString(1).Trim();
                            if (!result.ContainsKey(setId))
                                result[setId] = new List<string>();
                            result[setId].Add(category);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches the earliest and latest dbo.Item.DatePurchased among each set's items, for a
        /// batch of SetIds. Returns a dictionary: SetId â†’ (MinDate, MaxDate). Unions dbo.SetItem
        /// and dbo.Request the same way GetCategoriesBySetIds does â€” see that method's comment for why.
        /// </summary>
        public Dictionary<int, (DateTime? Min, DateTime? Max)> GetPurchaseDateRangeBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, (DateTime? Min, DateTime? Max)>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT x.SetId, MIN(x.DatePurchased) AS MinDate, MAX(x.DatePurchased) AS MaxDate
                FROM (
                    SELECT si.SetId, i.DatePurchased
                    FROM dbo.SetItem si
                    INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                    WHERE si.SetId IN ({paramNames})

                    UNION ALL

                    SELECT r.SetId, i.DatePurchased
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE r.SetId IN ({paramNames})
                ) x
                WHERE x.DatePurchased IS NOT NULL
                GROUP BY x.SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            DateTime? min = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                            DateTime? max = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                            result[setId] = (min, max);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches the distinct item types (Hardware / Software License / Services â€” the
        /// dbo.Item.ItemType CHECK-constrained values) present in each set, for a batch of SetIds.
        /// Returns a dictionary: SetId â†’ list of distinct item type names. Unions dbo.SetItem and
        /// dbo.Request the same way GetCategoriesBySetIds does â€” see that method's comment for why.
        /// This is the coarse Hardware/Software/Services classification, distinct from
        /// GetCategoriesBySetIds' finer-grained category names (Ink, Toner, Cartridge, ...).
        /// </summary>
        public Dictionary<int, List<string>> GetItemTypesBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, List<string>>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT DISTINCT x.SetId, x.ItemType
                FROM (
                    SELECT si.SetId, i.ItemType
                    FROM dbo.SetItem si
                    INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
                    WHERE si.SetId IN ({paramNames})

                    UNION ALL

                    SELECT r.SetId, i.ItemType
                    FROM dbo.Request r
                    INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                    WHERE r.SetId IN ({paramNames})
                ) x
                WHERE LTRIM(RTRIM(ISNULL(x.ItemType, ''))) <> ''
                ORDER BY x.SetId";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string itemType = reader.GetString(1).Trim();
                            if (!result.ContainsKey(setId))
                                result[setId] = new List<string>();
                            result[setId].Add(itemType);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Fetches the distinct Sub-Types (Contract/Subscription/License/Services — see
        /// ItemSubTypeCatalog) present among each set's dbo.SetItem rows, for a batch of SetIds,
        /// plus whether that set has at least one item with NO Sub-Type ("Non-Subtype"), plus the
        /// distinct Sub-Type Group Reference Codes (Contract Code/License ID/etc. — also nullable)
        /// on that set, so callers can make them searchable alongside the Sub-Type names
        /// themselves. Unlike GetCategoriesBySetIds/GetItemTypesBySetIds this does not union
        /// dbo.Request — Sub-Type grouping only ever applies to invoiced dbo.SetItem rows, never
        /// to Hardware Requests. A SetId absent from the result (no SetItem rows at all, e.g. a
        /// pure Hardware/Request set) should be treated by the caller as "Non-Subtype" too — it
        /// has no Sub-Type items.
        /// </summary>
        public Dictionary<int, (List<string> SubTypes, bool HasNonSubType, List<string> ReferenceCodes)> GetSubTypesBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, (List<string> SubTypes, bool HasNonSubType, List<string> ReferenceCodes)>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT si.SetId, si.SubType, si.ReferenceCode
                FROM dbo.SetItem si
                WHERE si.SetId IN ({paramNames})";

            using (var con = new SqlConnection(GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 }) // Prevent default 30s timeout on large datasets
                {
                    for (int i = 0; i < ids.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int setId = reader.GetInt32(0);
                            string subType = reader.IsDBNull(1) ? null : reader.GetString(1).Trim();
                            string referenceCode = reader.IsDBNull(2) ? null : reader.GetString(2).Trim();

                            if (!result.ContainsKey(setId))
                                result[setId] = (new List<string>(), false, new List<string>());

                            var entry = result[setId];
                            if (string.IsNullOrEmpty(subType))
                                entry.HasNonSubType = true;
                            else if (!entry.SubTypes.Contains(subType, StringComparer.OrdinalIgnoreCase))
                                entry.SubTypes.Add(subType);

                            if (!string.IsNullOrEmpty(referenceCode) && !entry.ReferenceCodes.Contains(referenceCode, StringComparer.OrdinalIgnoreCase))
                                entry.ReferenceCodes.Add(referenceCode);

                            result[setId] = entry;
                        }
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// An existing invoice Set found to already cover one of the items on a different
    /// dispatch Set — see SetRepository.FindExistingInvoiceForSetItemsAsync.
    /// </summary>
    public class ExistingInvoiceMatch
    {
        public int    SetId          { get; set; }
        public string SetCode        { get; set; }
        public string DocumentNumber { get; set; }
    }

    /// <summary>
    /// Represents a dispatched inventory Set shown on the Set Notifications page.
    /// </summary>
    public class DispatchedSetNotificationDto
    {
        public int      SetId          { get; set; }
        public string   SetCode        { get; set; }
        public string   SetStatus      { get; set; }
        public string   SetType        { get; set; }
        public DateTime CreatedAt      { get; set; }
        public int?     ReceivedById   { get; set; }
        public string   ReceivedByName { get; set; }
        public string   EmployeeName   { get; set; }
        public string   BranchName     { get; set; }
        public string   DepartmentName { get; set; }
    }
}
