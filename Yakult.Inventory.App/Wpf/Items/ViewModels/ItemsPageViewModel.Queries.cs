using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Items.ViewModels
{
    // Raw-SQL data access, ported verbatim from Pages.Item.ViewItemsPage (LoadItems /
    // GetCategorySummaries / ArchiveItem) — same queries, same column ordinals, just
    // reading VM filter state instead of WinForms controls and running off the UI thread.
    public partial class ItemsPageViewModel
    {
        public async Task ReloadAsync()
        {
            IsLoading = true;
            try
            {
                await LoadItemsAsync();
                await LoadCategorySummaryAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshAndResetFiltersAsync()
        {
            _columnFilters.Clear();
            await ReloadAsync();
        }

        /// <summary>Clears every toolbar filter control back to its default state, then reloads.</summary>
        public async Task ResetFiltersAsync()
        {
            _columnFilters.Clear();

            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _suppressCategoryFilterChange = true;
            try
            {
                foreach (var opt in CategoryFilterOptions)
                    opt.IsSelected = false;
            }
            finally
            {
                _suppressCategoryFilterChange = false;
            }
            UpdateCategoryFilterSummary();

            _serialFilter = string.Empty;
            OnPropertyChanged(nameof(SerialFilter));

            _setFilterDebounceTimer.Stop();
            _setCodeFilter = string.Empty;
            _documentNumberFilter = string.Empty;
            _companyFilter = string.Empty;
            _departmentFilter = string.Empty;
            _branchFilter = string.Empty;
            _employeeFilter = string.Empty;
            _referenceCodeFilter = string.Empty;
            _parentTagFilter = string.Empty;
            _reqIdFilter = string.Empty;
            OnPropertyChanged(nameof(SetCodeFilter));
            OnPropertyChanged(nameof(DocumentNumberFilter));
            OnPropertyChanged(nameof(CompanyFilter));
            OnPropertyChanged(nameof(DepartmentFilter));
            OnPropertyChanged(nameof(BranchFilter));
            OnPropertyChanged(nameof(EmployeeFilter));
            OnPropertyChanged(nameof(ReferenceCodeFilter));
            OnPropertyChanged(nameof(ParentTagFilter));
            OnPropertyChanged(nameof(ReqIdFilter));

            _subTypeFilterNonSubType = false;
            _subTypeFilterAnySubtype = false;
            _subTypeFilterContract = false;
            _subTypeFilterSubscription = false;
            _subTypeFilterLicense = false;
            _subTypeFilterServices = false;
            OnPropertyChanged(nameof(SubTypeFilterNonSubType));
            OnPropertyChanged(nameof(SubTypeFilterAnySubtype));
            OnPropertyChanged(nameof(SubTypeFilterContract));
            OnPropertyChanged(nameof(SubTypeFilterSubscription));
            OnPropertyChanged(nameof(SubTypeFilterLicense));
            OnPropertyChanged(nameof(SubTypeFilterServices));
            UpdateSubTypeFilterSummary();

            _selectedSortBy = "Default";
            _sortProperty = null;
            OnPropertyChanged(nameof(SelectedSortBy));

            _showInactive = false;
            OnPropertyChanged(nameof(ShowInactive));

            _itemsSummaryMode = ItemsSummaryMode.ActiveOnly;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsInactiveCardActive));

            await ReloadAsync();
        }

        public Task LoadItemsAsync()
        {
            // Category is filtered client-side in ApplyFilters() (multi-select checkboxes), same as Sub-Type.
            string serialFilter = _serialFilter;

            // Set-linked filters (see property declarations for why these are server-side only).
            string setCodeFilter = _setCodeFilter;
            string documentNumberFilter = _documentNumberFilter;
            string companyFilter = _companyFilter;
            string departmentFilter = _departmentFilter;
            string branchFilter = _branchFilter;
            string employeeFilter = _employeeFilter;
            string referenceCodeFilter = _referenceCodeFilter;
            string parentTagFilter = _parentTagFilter;
            string reqIdFilter = _reqIdFilter;

            return Task.Run(() =>
            {
                var items = new List<ItemDto>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    var whereClauses = new List<string> { "1 = 1" };

                    // CRITICAL: Exclude archived items - they should only appear in ViewArchivePage
                    whereClauses.Add("arch.EntityId IS NULL");

                    if (!_showInactive)
                        whereClauses.Add("i.Active = 1");

                    if (!string.IsNullOrWhiteSpace(serialFilter))
                        whereClauses.Add("i.SerialNumber LIKE @Serial");

                    // Set-linked filters: an Item can appear in many dbo.SetItem rows across many
                    // Sets, so these are matched via a single EXISTS against one qualifying Set row
                    // (all provided fields must match the same Set, not just any Set the item is in).
                    var setConditions = new List<string>();
                    if (!string.IsNullOrWhiteSpace(setCodeFilter)) setConditions.Add("s.SetCode LIKE @SetCode");
                    if (!string.IsNullOrWhiteSpace(documentNumberFilter)) setConditions.Add("s.DocumentNumber LIKE @DocumentNumber");
                    if (!string.IsNullOrWhiteSpace(companyFilter)) setConditions.Add("co.Name LIKE @Company");
                    if (!string.IsNullOrWhiteSpace(departmentFilter)) setConditions.Add("dept.Name LIKE @Department");
                    if (!string.IsNullOrWhiteSpace(branchFilter)) setConditions.Add("br.Name LIKE @Branch");
                    if (!string.IsNullOrWhiteSpace(employeeFilter)) setConditions.Add("emp.Name LIKE @Employee");
                    if (!string.IsNullOrWhiteSpace(referenceCodeFilter)) setConditions.Add("si.ReferenceCode LIKE @ReferenceCode");
                    if (!string.IsNullOrWhiteSpace(parentTagFilter)) setConditions.Add("ptg.Label LIKE @ParentTag");
                    if (!string.IsNullOrWhiteSpace(reqIdFilter)) setConditions.Add("CAST(s.ReqId AS NVARCHAR(20)) LIKE @ReqId");

                    if (setConditions.Count > 0)
                    {
                        whereClauses.Add($@"EXISTS (
    SELECT 1 FROM dbo.SetItem si
    INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
    LEFT JOIN dbo.Company co ON co.ComId = s.ComId
    LEFT JOIN dbo.Branch br ON br.BranchId = s.CurrentBranchId
    LEFT JOIN dbo.Department dept ON dept.DeptId = s.CurrentDepartmentId
    LEFT JOIN dbo.Employee emp ON emp.EmpId = s.ReceivedById
    LEFT JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
    WHERE si.ItemId = i.ItemId AND {string.Join(" AND ", setConditions)}
)");
                    }

                    // NOTE: This query uses UNION ALL to split non-serialized bulk items into two rows:
                    // 1. Active row showing remaining quantity after requests
                    // 2. Inactive row showing requested quantity
                    // Serialized items remain as single rows (active or inactive based on requests)
                    //
                    // PERF: req_agg pre-aggregates dbo.Request once so both UNION parts and all
                    // CASE expressions can reference it via a JOIN instead of firing correlated
                    // subqueries for every row.
                    string sqlQuery = $@"
WITH req_agg AS (
    SELECT ItemId, SUM(Quantity) AS TotalRequested
    FROM dbo.Request
    WHERE Active = 1
    GROUP BY ItemId
)

-- Query 1: Active items showing available quantities
SELECT
        i.ItemId,                       -- 0
        i.Name,                         -- 1
        i.Description,                  -- 2
        i.Category,                     -- 3
        i.ItemType,                     -- 4
        i.SerialNumber,                 -- 5
        i.ModelNumber,                  -- 6
        i.Active AS DbActive,           -- 7 (original DB Active)
        i.UnitOfMeasure,                -- 8
        CASE
            WHEN i.SerialNumber IS NULL THEN
                i.StockOnHand - ISNULL(req.TotalRequested, 0)
            ELSE
                i.StockOnHand
        END AS StockOnHand,             -- 9
        i.DateCreated,                  -- 10
        u1.Name AS CreatedByName,       -- 11
        i.DateModified,                 -- 12
        u2.Name AS ModifiedByName,      -- 13
        CASE
            WHEN i.Active = 0 THEN CAST(0 AS BIT)
            WHEN i.SerialNumber IS NOT NULL THEN
                CASE WHEN req.ItemId IS NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
            ELSE
                CASE
                    WHEN i.StockOnHand > ISNULL(req.TotalRequested, 0)
                    THEN CAST(1 AS BIT)
                    ELSE CAST(0 AS BIT)
                END
        END AS EffectiveActive,         -- 14
        i.Amount,                       -- 15
        c.ConditionName,                -- 16
        i.ConditionId,                  -- 17
        i.Remarks,                      -- 18
        i.WarrantyYears,                -- 19
        i.WarrantyStartDate,            -- 20
        i.WarrantyEndDate,              -- 21
        i.DatePurchased,                -- 22
        CASE
            WHEN i.ConditionId = 1 THEN 'Good'
            WHEN i.ConditionId = 2 THEN 'Damaged'
            ELSE NULL
        END AS LatestStatus,            -- 23
        su.Remark AS LatestRemark,      -- 24
        ISNULL(rh.RepairCount, 0) AS RepairCount,   -- 25
        lr.RepairAction AS LastRepairAction,        -- 26
        i.VendorId,                     -- 27
        v.VendorName,                   -- 28
        i.IsTrackedAsset,               -- 29
        i.AcquisitionType,              -- 30
        i.CartridgeModelId,             -- 31
        CASE WHEN i.Category = 'Cartridge' AND cm.ModelNumber = 'Unassigned' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS UsedDefaultCartridgeModel,  -- 32
        i.RefillStatus,                 -- 33
        i.StartDate,                    -- 34
        i.EndDate,                      -- 35
        i.CellPhoneNumber,               -- 36
        i.IMEI1,                         -- 37
        i.IMEI2,                         -- 38
        i.IsBorrowable,                  -- 39
        i.SubType                        -- 40
FROM dbo.Item i
LEFT JOIN req_agg req ON req.ItemId = i.ItemId
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.[User] u2 ON i.ModifiedBy = u2.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = i.CartridgeModelId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT
        SerialNumber,
        MAX(CreatedAt) AS LatestCreatedAt
    FROM dbo.SetItemUpdate
    WHERE Processed = 1
    GROUP BY SerialNumber
) lu ON lu.SerialNumber = i.SerialNumber
LEFT JOIN dbo.SetItemUpdate su
    ON su.SerialNumber = i.SerialNumber
   AND su.CreatedAt = lu.LatestCreatedAt
   AND su.Processed = 1
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount, MAX(CreatedAt) AS LastRepairAt
    FROM dbo.ItemRepairHistory
    GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN dbo.ItemRepairHistory lr
    ON lr.SerialNumber = i.SerialNumber
   AND lr.CreatedAt = rh.LastRepairAt
WHERE {string.Join(" AND ", whereClauses)}

UNION ALL

-- Query 2: Inactive rows for non-serialized items showing requested quantities
SELECT
        i.ItemId,                       -- 0
        i.Name,                         -- 1
        i.Description,                  -- 2
        i.Category,                     -- 3
        i.ItemType,                     -- 4
        i.SerialNumber,                 -- 5
        i.ModelNumber,                  -- 6
        i.Active AS DbActive,           -- 7
        i.UnitOfMeasure,                -- 8
        ISNULL(req.TotalRequested, 0) AS StockOnHand,  -- 9
        i.DateCreated,                  -- 10
        u1.Name AS CreatedByName,       -- 11
        i.DateModified,                 -- 12
        u2.Name AS ModifiedByName,      -- 13
        CAST(0 AS BIT) AS EffectiveActive,  -- 14 (always inactive for requested items)
        i.Amount,                       -- 15
        c.ConditionName,                -- 16
        i.ConditionId,                  -- 17
        i.Remarks,                      -- 18
        i.WarrantyYears,                -- 19
        i.WarrantyStartDate,            -- 20
        i.WarrantyEndDate,              -- 21
        i.DatePurchased,                -- 22
        CASE
            WHEN i.ConditionId = 1 THEN 'Good'
            WHEN i.ConditionId = 2 THEN 'Damaged'
            ELSE NULL
        END AS LatestStatus,            -- 23
        su.Remark AS LatestRemark,      -- 24
        ISNULL(rh.RepairCount, 0) AS RepairCount,   -- 25
        lr.RepairAction AS LastRepairAction,        -- 26
        i.VendorId,                     -- 27
        v.VendorName,                   -- 28
        i.IsTrackedAsset,               -- 29
        i.AcquisitionType,              -- 30
        i.CartridgeModelId,             -- 31
        CASE WHEN i.Category = 'Cartridge' AND cm.ModelNumber = 'Unassigned' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS UsedDefaultCartridgeModel,  -- 32
        i.RefillStatus,                 -- 33
        i.StartDate,                    -- 34
        i.EndDate,                      -- 35
        i.CellPhoneNumber,               -- 36
        i.IMEI1,                         -- 37
        i.IMEI2,                         -- 38
        i.IsBorrowable,                  -- 39
        i.SubType                        -- 40
FROM dbo.Item i
LEFT JOIN req_agg req ON req.ItemId = i.ItemId
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.[User] u2 ON i.ModifiedBy = u2.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = i.CartridgeModelId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT
        SerialNumber,
        MAX(CreatedAt) AS LatestCreatedAt
    FROM dbo.SetItemUpdate
    WHERE Processed = 1
    GROUP BY SerialNumber
) lu ON lu.SerialNumber = i.SerialNumber
LEFT JOIN dbo.SetItemUpdate su
    ON su.SerialNumber = i.SerialNumber
   AND su.CreatedAt = lu.LatestCreatedAt
   AND su.Processed = 1
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount, MAX(CreatedAt) AS LastRepairAt
    FROM dbo.ItemRepairHistory
    GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN dbo.ItemRepairHistory lr
    ON lr.SerialNumber = i.SerialNumber
   AND lr.CreatedAt = rh.LastRepairAt
WHERE {string.Join(" AND ", whereClauses)}
    AND i.SerialNumber IS NULL          -- Only non-serialized items
    AND req.TotalRequested IS NOT NULL  -- Only items that have active requests (req_agg matched)

ORDER BY DateCreated DESC, ItemId DESC";

                    using (var cmd = new SqlCommand(sqlQuery, con))
                    {
                        if (!string.IsNullOrWhiteSpace(serialFilter))
                            cmd.Parameters.AddWithValue("@Serial", $"%{serialFilter}%");
                        if (!string.IsNullOrWhiteSpace(setCodeFilter))
                            cmd.Parameters.AddWithValue("@SetCode", $"%{setCodeFilter}%");
                        if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                            cmd.Parameters.AddWithValue("@DocumentNumber", $"%{documentNumberFilter}%");
                        if (!string.IsNullOrWhiteSpace(companyFilter))
                            cmd.Parameters.AddWithValue("@Company", $"%{companyFilter}%");
                        if (!string.IsNullOrWhiteSpace(departmentFilter))
                            cmd.Parameters.AddWithValue("@Department", $"%{departmentFilter}%");
                        if (!string.IsNullOrWhiteSpace(branchFilter))
                            cmd.Parameters.AddWithValue("@Branch", $"%{branchFilter}%");
                        if (!string.IsNullOrWhiteSpace(employeeFilter))
                            cmd.Parameters.AddWithValue("@Employee", $"%{employeeFilter}%");
                        if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                            cmd.Parameters.AddWithValue("@ReferenceCode", $"%{referenceCodeFilter}%");
                        if (!string.IsNullOrWhiteSpace(parentTagFilter))
                            cmd.Parameters.AddWithValue("@ParentTag", $"%{parentTagFilter}%");
                        if (!string.IsNullOrWhiteSpace(reqIdFilter))
                            cmd.Parameters.AddWithValue("@ReqId", $"%{reqIdFilter}%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new ItemDto
                                {
                                    ItemId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ItemType = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    ModelNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    DbActive = reader.GetBoolean(7),
                                    Active = reader.GetBoolean(14),
                                    UnitOfMeasure = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    StockOnHand = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    DateCreated = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                    CreatedByName = reader.IsDBNull(11) ? "N/A" : reader.GetString(11),
                                    DateModified = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12),
                                    ModifiedByName = reader.IsDBNull(13) ? null : reader.GetString(13),
                                    Amount = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15),
                                    ConditionName = reader.IsDBNull(16) ? null : reader.GetString(16),
                                    ConditionId = reader.IsDBNull(17) ? 1 : reader.GetInt32(17),
                                    Remarks = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    WarrantyYears = reader.GetInt32(19),
                                    WarrantyStartDate = reader.IsDBNull(20) ? (DateTime?)null : reader.GetDateTime(20),
                                    WarrantyEndDate = reader.IsDBNull(21) ? (DateTime?)null : reader.GetDateTime(21),
                                    DatePurchased = reader.IsDBNull(22) ? (DateTime?)null : reader.GetDateTime(22),
                                    LatestStatus = reader.IsDBNull(23) ? null : reader.GetString(23),
                                    LatestRemark = reader.IsDBNull(24) ? null : reader.GetString(24),
                                    RepairCount = reader.IsDBNull(25) ? 0 : reader.GetInt32(25),
                                    LastRepairAction = reader.IsDBNull(26) ? null : reader.GetString(26),
                                    VendorId = reader.IsDBNull(27) ? (int?)null : reader.GetInt32(27),
                                    VendorName = reader.IsDBNull(28) ? null : reader.GetString(28),
                                    IsTrackedAsset = !reader.IsDBNull(29) && reader.GetBoolean(29),
                                    AcquisitionType = reader.IsDBNull(30) ? null : reader.GetString(30),
                                    CartridgeModelId = reader.IsDBNull(31) ? (int?)null : reader.GetInt32(31),
                                    UsedDefaultCartridgeModel = !reader.IsDBNull(32) && reader.GetBoolean(32),
                                    RefillStatus = reader.IsDBNull(33) ? null : reader.GetString(33),
                                    StartDate = reader.IsDBNull(34) ? (DateTime?)null : reader.GetDateTime(34),
                                    EndDate = reader.IsDBNull(35) ? (DateTime?)null : reader.GetDateTime(35),
                                    CellPhoneNumber = reader.IsDBNull(36) ? null : reader.GetString(36),
                                    IMEI1 = reader.IsDBNull(37) ? null : reader.GetString(37),
                                    IMEI2 = reader.IsDBNull(38) ? null : reader.GetString(38),
                                    IsBorrowable = reader.IsDBNull(39) ? (bool?)null : reader.GetBoolean(39),
                                    SubType = reader.IsDBNull(40) ? null : reader.GetString(40)
                                });
                            }
                        }
                    }
                }

                return items;
            }).ContinueWith(t =>
            {
                _allItems = t.Result;
                _selectionManager.ApplyAndReconcile(_allItems);

                TotalItemsCount = _allItems.Count;
                ActiveItemsCount = _allItems.Count(i => i.Active);
                InactiveItemsCount = TotalItemsCount - ActiveItemsCount;

                RebuildCategoryOptions();
                ApplyFilters();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Queries the vw_CategoryCombinedSummary view which includes stock and condition breakdown columns.
        /// </summary>
        public Task LoadCategorySummaryAsync()
        {
            return Task.Run(() =>
            {
                var result = new List<CategorySummaryDto>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    string sqlQuery = @"
                        SELECT
                            CategoryId,
                            CategoryName,
                            ActiveStock,
                            0 AS RequestedItems,
                            ISNULL(GoodCount, 0) AS GoodCount,
                            ISNULL(DamagedCount, 0) AS DamagedCount,
                            ISNULL(SerializedItems, 0) AS SerializedItems,
                            ISNULL(TotalItems_All, 0) AS TotalItems
                        FROM dbo.vw_CategoryCombinedSummary
                        WHERE TotalItems_All > 0
                        ORDER BY CategoryName";

                    using (var cmd = new SqlCommand(sqlQuery, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new CategorySummaryDto
                            {
                                CategoryId = reader.GetInt32(0),
                                CategoryName = reader.GetString(1),
                                ActiveStock = reader.GetInt32(2),
                                RequestedItems = reader.GetInt32(3),
                                GoodCount = reader.GetInt32(4),
                                DamagedCount = reader.GetInt32(5),
                                SerializedItems = reader.GetInt32(6),
                                TotalItems = reader.GetInt32(7)
                            });
                        }
                    }
                }

                return result;
            }).ContinueWith(t =>
            {
                _allCategorySummaries = t.Result;
                _categoryCurrentPage = 1;
                RebuildCategoryPage();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public async Task ArchiveItemsAsync(List<ItemDto> items, string reason, bool deactivate)
        {
            foreach (var item in items)
                await ArchiveItemAsync(item.ItemId, reason, deactivate);

            await ReloadAsync();
        }

        private Task ArchiveItemAsync(int itemId, string reason, bool deactivate)
        {
            return Task.Run(() =>
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            string serialNumber = null;
                            using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                var result = cmd.ExecuteScalar();
                                serialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                            }

                            string upsertArchiveSql = @"
                                IF EXISTS (SELECT 1 FROM dbo.ArchiveStatus WHERE EntityType = 'Item' AND EntityId = @ItemId)
                                    UPDATE dbo.ArchiveStatus
                                    SET IsArchived = 1, ArchivedAt = GETDATE(), ArchivedBy = @ArchivedBy, ArchiveReason = @ArchiveReason
                                    WHERE EntityType = 'Item' AND EntityId = @ItemId
                                ELSE
                                    INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                    VALUES ('Item', @ItemId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(upsertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                cmd.ExecuteNonQuery();
                            }

                            if (deactivate)
                            {
                                string deactivateSql = "UPDATE Item SET Active = 0 WHERE ItemId = @ItemId";
                                using (var cmd = new SqlCommand(deactivateSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemId,
                                SerialNumber = serialNumber,
                                Action = deactivate ? "Item Archived - Deactivated" : "Item Archived",
                                ActionTime = DateTime.Now,
                                Status = "Completed",
                                ReferenceType = "Item",
                                ReferenceId = itemId,
                                Notes = $"Archived{(string.IsNullOrWhiteSpace(reason) ? "" : $" • Reason: {reason}")}.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }
    }
}
