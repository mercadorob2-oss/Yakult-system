using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public class RenewalRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public RenewalRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Gets all renewals from the vw_RenewalStatus view
        ///
        /// ✅ FILTER RULE: Only shows Sets that have at least one NON-ARCHIVED renewal record in the Renewals table
        /// This ensures the Renewals page only shows items that have been explicitly renewed,
        /// not all expired/expiring items (those appear in Warranty page first)
        ///
        /// Includes Active status: false if any items in the set are archived OR if any renewal records are archived
        ///
        /// Active = 0 means at least one Item in the Set has Item.Active = 0 (inactive/archived)
        ///              OR at least one Renewal record has IsArchived = 1
        /// Active = 1 means all Items in the Set have Item.Active = 1
        ///              AND all Renewal records have IsArchived = 0
        /// </summary>
        /// <summary>
        /// Advanced-filter overloads are additive/optional (all default to null) so every existing
        /// caller (Dashboard, Export, Grouped view) is unaffected. Only the WPF "View Renewals" page's
        /// Advanced Filters popup passes these. SetCode/DocumentNumber/CompanyName/CurrentBranchName/
        /// CurrentDepartmentName come straight from vw_RenewalStatus, so they're matched directly on
        /// the view alias. Employee and Req ID are Set-level (dbo.[Set], 1:1 with r.SetId via
        /// r.SetId = SetId) so they're reached with a single extra LEFT JOIN, not an EXISTS. Reference
        /// Code and Parent Tag live on dbo.SetItem (one Set can have many SetItem rows), so those two
        /// are matched via an EXISTS requiring both conditions on the same SetItem row.
        /// </summary>
        public List<RenewalDto> GetAllRenewals(
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var parameters = new DynamicParameters();
                var whereClauses = new List<string>();

                if (!string.IsNullOrWhiteSpace(setCodeFilter))
                {
                    whereClauses.Add("r.SetCode LIKE @SetCode");
                    parameters.Add("SetCode", $"%{setCodeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                {
                    whereClauses.Add("r.DocumentNumber LIKE @DocumentNumber");
                    parameters.Add("DocumentNumber", $"%{documentNumberFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(companyFilter))
                {
                    whereClauses.Add("r.CompanyName LIKE @Company");
                    parameters.Add("Company", $"%{companyFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(departmentFilter))
                {
                    whereClauses.Add("r.CurrentDepartmentName LIKE @Department");
                    parameters.Add("Department", $"%{departmentFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(branchFilter))
                {
                    whereClauses.Add("r.CurrentBranchName LIKE @Branch");
                    parameters.Add("Branch", $"%{branchFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(employeeFilter))
                {
                    whereClauses.Add("renewalEmp.Name LIKE @Employee");
                    parameters.Add("Employee", $"%{employeeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(reqIdFilter))
                {
                    whereClauses.Add("CAST(renewalSet.ReqId AS NVARCHAR(20)) LIKE @ReqId");
                    parameters.Add("ReqId", $"%{reqIdFilter}%");
                }

                var setItemConditions = new List<string>();
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                {
                    setItemConditions.Add("si2.ReferenceCode LIKE @ReferenceCode");
                    parameters.Add("ReferenceCode", $"%{referenceCodeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(parentTagFilter))
                {
                    setItemConditions.Add("ptg2.Label LIKE @ParentTag");
                    parameters.Add("ParentTag", $"%{parentTagFilter}%");
                }
                if (setItemConditions.Count > 0)
                {
                    whereClauses.Add($@"EXISTS (
                        SELECT 1 FROM dbo.SetItem si2
                        LEFT JOIN dbo.SetItemParentTagGroup ptg2 ON ptg2.ParentTagGroupId = si2.ParentTagGroupId
                        WHERE si2.SetId = r.SetId AND {string.Join(" AND ", setItemConditions)}
                    )");
                }

                string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                string query = $@"
                    SELECT
                        r.SetId, r.SetCode, r.SetType, r.DocumentNumber, r.ReferenceNumber,
                        r.DocumentDate, r.Status, r.Remarks,
                        r.StartDate, r.EndDate, r.DaysUntilExpiry, r.ExpiryStatus,
                        r.ComId, r.CompanyName,
                        r.SiteName,
                        r.CurrentBranchId, r.CurrentBranchName,
                        r.CurrentDepartmentId, r.CurrentDepartmentName,
                        r.Subtotal, r.VatAmount, r.WhtAmount, r.DiscountAmount, r.TotalAmountDue,
                        r.ItemCount,
                        r.TotalActiveItems, r.ExpiredItemsCount, r.RenewedItemsCount, r.ArchivedItemsCount,
                        r.SetLevelStatus,
                        r.PartNumber,
                        r.RenewedDate,
                        r.RenewalOfSetId,
                        r.CreatedBy, r.CreatedDate,
                        -- Active = 0 only when a renewal record for this set has been explicitly archived.
                        CAST(CASE WHEN arch_ren.SetId IS NULL THEN 1 ELSE 0 END AS BIT) AS Active,
                        -- Renewal Notes is entered once per renewal action and applies to every item
                        -- renewed together in that action, so any one item's current (non-archived,
                        -- highest RenewalCount) note represents the whole Set for search purposes.
                        (SELECT TOP 1 ren_notes.RenewalNotes
                         FROM dbo.SetItem si_notes
                         INNER JOIN dbo.Renewals ren_notes ON ren_notes.ItemId = si_notes.ItemId
                         WHERE si_notes.SetId = r.SetId
                           AND ren_notes.IsArchived = 0
                           AND ren_notes.RenewalNotes IS NOT NULL AND ren_notes.RenewalNotes <> ''
                           AND ren_notes.RenewalCount = (
                               SELECT MAX(ren_max.RenewalCount) FROM dbo.Renewals ren_max
                               WHERE ren_max.ItemId = ren_notes.ItemId AND ren_max.IsArchived = 0
                           )
                         ORDER BY ren_notes.RenewalId DESC) AS RenewalNotes
                    FROM dbo.vw_RenewalStatus r
                    -- Pre-aggregated: sets that have at least one archived renewal item
                    LEFT JOIN (
                        SELECT DISTINCT si.SetId
                        FROM dbo.SetItem si
                        INNER JOIN dbo.Renewals ren ON ren.ItemId = si.ItemId
                        WHERE ren.IsArchived = 1
                    ) arch_ren ON arch_ren.SetId = r.SetId
                    -- Employee/Req ID are Set-level (1:1 with r.SetId), only needed for the
                    -- Advanced Filters Employee/Req ID fields — cheap LEFT JOINs either way.
                    LEFT JOIN dbo.[Set] renewalSet ON renewalSet.SetId = r.SetId
                    LEFT JOIN dbo.Employee renewalEmp ON renewalEmp.EmpId = renewalSet.ReceivedById
                    -- Superseded sets (already renewed into a newer Set) are intentionally kept
                    -- visible here, not just in the Grouped view — they show SetLevelStatus
                    -- 'Fully Renewed'/'Partially Renewed' and their RenewedDate, side by side
                    -- with the newer Set that replaced them.
                    {whereSql}
                    ORDER BY
                        CASE r.SetLevelStatus
                            WHEN 'Expired' THEN 1
                            WHEN 'Expiring Soon' THEN 2
                            WHEN 'Warning' THEN 3
                            WHEN 'Partially Renewed' THEN 4
                            WHEN 'Fully Renewed' THEN 5
                            WHEN 'Active' THEN 6
                            ELSE 7
                        END,
                        r.DaysUntilExpiry ASC";

                return connection.Query<RenewalDto>(query, parameters, commandTimeout: 120).AsList();
            }
        }

        /// <summary>
        /// Returns ALL renewal sets including superseded ones (no WHERE NOT EXISTS filter).
        /// Used by ViewRenewalGroupPage to build full renewal chains grouped by root set.
        /// Advanced-filter parameters mirror GetAllRenewals above (all default to null so the
        /// unfiltered call sites stay unaffected) — see that method's doc comment for the
        /// join/EXISTS rationale per field.
        /// </summary>
        public List<RenewalDto> GetAllRenewalsAllChains(
            string setCodeFilter = null,
            string documentNumberFilter = null,
            string companyFilter = null,
            string departmentFilter = null,
            string branchFilter = null,
            string employeeFilter = null,
            string referenceCodeFilter = null,
            string parentTagFilter = null,
            string reqIdFilter = null)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var parameters = new DynamicParameters();
                var whereClauses = new List<string>();

                if (!string.IsNullOrWhiteSpace(setCodeFilter))
                {
                    whereClauses.Add("r.SetCode LIKE @SetCode");
                    parameters.Add("SetCode", $"%{setCodeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(documentNumberFilter))
                {
                    whereClauses.Add("r.DocumentNumber LIKE @DocumentNumber");
                    parameters.Add("DocumentNumber", $"%{documentNumberFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(companyFilter))
                {
                    whereClauses.Add("r.CompanyName LIKE @Company");
                    parameters.Add("Company", $"%{companyFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(departmentFilter))
                {
                    whereClauses.Add("r.CurrentDepartmentName LIKE @Department");
                    parameters.Add("Department", $"%{departmentFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(branchFilter))
                {
                    whereClauses.Add("r.CurrentBranchName LIKE @Branch");
                    parameters.Add("Branch", $"%{branchFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(employeeFilter))
                {
                    whereClauses.Add("renewalEmp.Name LIKE @Employee");
                    parameters.Add("Employee", $"%{employeeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(reqIdFilter))
                {
                    whereClauses.Add("CAST(renewalSet.ReqId AS NVARCHAR(20)) LIKE @ReqId");
                    parameters.Add("ReqId", $"%{reqIdFilter}%");
                }

                var setItemConditions = new List<string>();
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter))
                {
                    setItemConditions.Add("si2.ReferenceCode LIKE @ReferenceCode");
                    parameters.Add("ReferenceCode", $"%{referenceCodeFilter}%");
                }
                if (!string.IsNullOrWhiteSpace(parentTagFilter))
                {
                    setItemConditions.Add("ptg2.Label LIKE @ParentTag");
                    parameters.Add("ParentTag", $"%{parentTagFilter}%");
                }
                if (setItemConditions.Count > 0)
                {
                    whereClauses.Add($@"EXISTS (
                        SELECT 1 FROM dbo.SetItem si2
                        LEFT JOIN dbo.SetItemParentTagGroup ptg2 ON ptg2.ParentTagGroupId = si2.ParentTagGroupId
                        WHERE si2.SetId = r.SetId AND {string.Join(" AND ", setItemConditions)}
                    )");
                }

                string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                string query = $@"
                    SELECT
                        r.SetId, r.SetCode, r.SetType, r.DocumentNumber, r.ReferenceNumber,
                        r.DocumentDate, r.Status, r.Remarks,
                        r.StartDate, r.EndDate, r.DaysUntilExpiry, r.ExpiryStatus,
                        r.ComId, r.CompanyName,
                        r.SiteName,
                        r.CurrentBranchId, r.CurrentBranchName,
                        r.CurrentDepartmentId, r.CurrentDepartmentName,
                        r.Subtotal, r.VatAmount, r.WhtAmount, r.DiscountAmount, r.TotalAmountDue,
                        r.ItemCount,
                        r.TotalActiveItems, r.ExpiredItemsCount, r.RenewedItemsCount, r.ArchivedItemsCount,
                        r.SetLevelStatus,
                        r.PartNumber,
                        r.RenewalOfSetId,
                        r.CreatedBy, r.CreatedDate,
                        -- Pre-aggregated: sets that have at least one archived renewal item
                        CAST(CASE WHEN arch_ren.SetId IS NULL THEN 1 ELSE 0 END AS BIT) AS Active,
                        -- See GetAllRenewals for rationale — Renewal Notes is a Renewal-level field,
                        -- surfaced here via any one item's current (non-archived, highest
                        -- RenewalCount) note so the Grouped view's search can match on it too.
                        (SELECT TOP 1 ren_notes.RenewalNotes
                         FROM dbo.SetItem si_notes
                         INNER JOIN dbo.Renewals ren_notes ON ren_notes.ItemId = si_notes.ItemId
                         WHERE si_notes.SetId = r.SetId
                           AND ren_notes.IsArchived = 0
                           AND ren_notes.RenewalNotes IS NOT NULL AND ren_notes.RenewalNotes <> ''
                           AND ren_notes.RenewalCount = (
                               SELECT MAX(ren_max.RenewalCount) FROM dbo.Renewals ren_max
                               WHERE ren_max.ItemId = ren_notes.ItemId AND ren_max.IsArchived = 0
                           )
                         ORDER BY ren_notes.RenewalId DESC) AS RenewalNotes
                    FROM dbo.vw_RenewalStatus r
                    LEFT JOIN (
                        SELECT DISTINCT si.SetId
                        FROM dbo.SetItem si
                        INNER JOIN dbo.Renewals ren ON ren.ItemId = si.ItemId
                        WHERE ren.IsArchived = 1
                    ) arch_ren ON arch_ren.SetId = r.SetId
                    LEFT JOIN dbo.[Set] renewalSet ON renewalSet.SetId = r.SetId
                    LEFT JOIN dbo.Employee renewalEmp ON renewalEmp.EmpId = renewalSet.ReceivedById
                    {whereSql}
                    ORDER BY r.SetId";

                return connection.Query<RenewalDto>(query, parameters, commandTimeout: 120).AsList();
            }
        }

        /// <summary>
        /// Gets all SetItem rows for a given Set, including RenewalStatus and RenewalReferenceId.
        /// Used by SetItemRenewalDialog to display per-item renewal state.
        /// </summary>
        /// <summary>
        /// Fetches item descriptions for a batch of SetIds in one query.
        /// Returns a dictionary: SetId → ordered list of item descriptions.
        /// </summary>
        public Dictionary<int, List<string>> GetItemNamesBySetIds(IEnumerable<int> setIds)
        {
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0) return new Dictionary<int, List<string>>();

            const string sql = @"
                SELECT si.SetId, ISNULL(si.Description, '') AS Description
                FROM dbo.SetItem si
                WHERE si.SetId IN @SetIds
                  AND LTRIM(RTRIM(ISNULL(si.Description, ''))) <> ''
                ORDER BY si.SetId, si.SetItemId";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                return conn.Query<SetItemDesc>(sql, new { SetIds = ids })
                    .GroupBy(r => r.SetId)
                    .ToDictionary(g => g.Key, g => g.Select(r => r.Description).ToList());
            }
        }

        private class SetItemDesc
        {
            public int    SetId       { get; set; }
            public string Description { get; set; }
        }

        public List<SetItemRenewalDto> GetSetItemsForRenewal(int setId)
        {
            const string sql = @"
                SELECT
                    si.SetItemId,
                    si.SetId,
                    si.ItemId,
                    si.ItemCode,
                    si.Description,
                    i.Name AS ItemName,
                    si.Quantity,
                    si.UnitOfMeasure,
                    si.UnitPrice,
                    si.Amount,
                    si.LineStartDate,
                    si.LineEndDate,
                    si.RenewalStatus,
                    si.RenewalReferenceId,
                    si.GroupId,
                    si.SubType,
                    si.ReferenceCode,
                    si.BeginDate,
                    si.EndDate,
                    si.ParentTagGroupId,
                    ptg.Label AS ParentTag
                FROM dbo.SetItem si
                LEFT JOIN dbo.Item i ON i.ItemId = si.ItemId
                LEFT JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
                WHERE si.SetId = @SetId
                ORDER BY si.SetItemId ASC";

            using (var connection = new SqlConnection(GetConnectionString()))
                return connection.Query<SetItemRenewalDto>(sql, new { SetId = setId }).AsList();
        }

        /// <summary>
        /// Returns one row per Sub-Type Group attached to this Set, sourced from
        /// dbo.vw_SetItemSubTypeGroups (shared with ViewInvoiceDetailPage). Used to render the
        /// Sub-Type Group financial cards on RenewalDetailWindow's Invoice Items tab.
        /// </summary>
        public List<SetItemSubTypeGroupSummaryDto> GetSubTypeGroupSummaries(int setId)
        {
            const string sql = @"
                SELECT GroupId, SetId, SubType, ReferenceCode, BeginDate, EndDate, ItemCount, Subtotal,
                       SubtotalOverride, VatPercent, WhtPercent, DiscountPercent
                FROM dbo.vw_SetItemSubTypeGroups
                WHERE SetId = @SetId
                ORDER BY SubType, ReferenceCode";

            using (var connection = new SqlConnection(GetConnectionString()))
                return connection.Query<SetItemSubTypeGroupSummaryDto>(sql, new { SetId = setId }).AsList();
        }

        /// <summary>
        /// Persists a Sub-Type Group card's edited Subtotal/VAT%/WHT%/Discount% overrides,
        /// mirroring ViewInvoiceDetailPage's group-card save behavior.
        /// </summary>
        public void SaveSubTypeGroupFinancials(
            int groupId, decimal subtotalOverride, decimal vatPercent, decimal whtPercent, decimal discountPercent, int modifiedBy)
        {
            const string sql = @"
                UPDATE dbo.SetItemSubTypeGroup
                SET SubtotalOverride = @SubtotalOverride,
                    VatPercent       = @VatPercent,
                    WhtPercent       = @WhtPercent,
                    DiscountPercent  = @DiscountPercent,
                    ModifiedBy       = @ModifiedBy,
                    ModifiedAt       = sysutcdatetime()
                WHERE GroupId = @GroupId";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Execute(sql, new
                {
                    GroupId = groupId,
                    SubtotalOverride = subtotalOverride,
                    VatPercent = vatPercent,
                    WhtPercent = whtPercent,
                    DiscountPercent = discountPercent,
                    ModifiedBy = modifiedBy
                });
            }
        }

        /// <summary>
        /// Resolves (find-or-create) the Sub-Type Group on <paramref name="setId"/> matching this
        /// SubType/ReferenceCode/date key, then persists the given financial override onto it —
        /// used right after a renewal finishes inserting its SetItem rows (which already resolved/
        /// created these same GroupId rows per item) to save the Sub-Type Group Summary card's
        /// Subtotal/VAT/WHT/Discount that the user set during the renewal. Without this, those
        /// values only ever fed the renewal's own header totals and were never persisted at the
        /// group level, so View Details/Manage Items would always show the DB defaults afterward.
        /// No-op when subType is blank (nothing to resolve).
        /// </summary>
        public void SaveGroupCardFinancials(
            int setId, string subType, string referenceCode, DateTime? beginDate, DateTime? endDate,
            decimal subtotalOverride, decimal vatPercent, decimal whtPercent, decimal discountPercent, int modifiedBy)
        {
            if (string.IsNullOrWhiteSpace(subType)) return;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        int? groupId = SetItemSubTypeGroupHelper.FindOrCreateGroupId(
                            connection, tx, setId, subType, referenceCode, beginDate, endDate, modifiedBy);

                        if (groupId.HasValue)
                        {
                            const string sql = @"
                                UPDATE dbo.SetItemSubTypeGroup
                                SET SubtotalOverride = @SubtotalOverride,
                                    VatPercent       = @VatPercent,
                                    WhtPercent       = @WhtPercent,
                                    DiscountPercent  = @DiscountPercent,
                                    ModifiedBy       = @ModifiedBy,
                                    ModifiedAt       = sysutcdatetime()
                                WHERE GroupId = @GroupId";

                            connection.Execute(sql, new
                            {
                                GroupId = groupId.Value,
                                SubtotalOverride = subtotalOverride,
                                VatPercent = vatPercent,
                                WhtPercent = whtPercent,
                                DiscountPercent = discountPercent,
                                ModifiedBy = modifiedBy
                            }, tx);
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

        /// <summary>
        /// Renews a single SetItem by:
        ///   1. Inserting a new SetItem row with updated description, dates, and RenewalStatus = NULL (Active).
        ///   2. Marking the old SetItem as RenewalStatus = 'Renewed', RenewalReferenceId = new SetItemId.
        ///   3. Creating a Renewals record for the item.
        /// Returns the new SetItemId.
        /// </summary>
        public int RenewSingleItem(
            int oldSetItemId,
            string newDescription,
            DateTime newLineStartDate,
            DateTime newLineEndDate,
            int createdBy,
            int? newItemId = null,
            string newItemCode = null,
            decimal? newUnitPrice = null,
            decimal? newQuantity = null,
            string newUnitOfMeasure = null,
            int? renewalYears = null,
            decimal? renewalAmount = null,
            string renewalNotes = null,
            int? targetSetId = null,
            string subType = null,
            string referenceCode = null,
            DateTime? groupBeginDate = null,
            DateTime? groupEndDate = null,
            string parentTag = null)
        {
            const string getOldSql = @"
                SELECT SetItemId, SetId, ItemId, ItemCode, Description,
                       Quantity, UnitOfMeasure, UnitPrice, Amount
                FROM dbo.SetItem
                WHERE SetItemId = @SetItemId";

            const string insertNewSql = @"
                INSERT INTO dbo.SetItem
                    (SetId, ItemId, ItemCode, Description, Quantity, UnitOfMeasure,
                     UnitPrice, Amount, LineStartDate, LineEndDate, CreatedBy, CreatedAt,
                     RenewalStatus, RenewalReferenceId,
                     SubType, ReferenceCode, BeginDate, EndDate, GroupId, ParentTagGroupId)
                VALUES
                    (@SetId, @ItemId, @ItemCode, @Description, @Quantity, @UnitOfMeasure,
                     @UnitPrice, @Amount, @LineStartDate, @LineEndDate, @CreatedBy, GETDATE(),
                     NULL, NULL,
                     @SubType, @ReferenceCode, @GroupBeginDate, @GroupEndDate, @GroupId, @ParentTagGroupId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string markOldSql = @"
                UPDATE dbo.SetItem
                SET RenewalStatus      = 'Renewed',
                    RenewalReferenceId = @NewSetItemId
                WHERE SetItemId = @OldSetItemId";

            const string insertRenewalSql = @"
                INSERT INTO dbo.Renewals
                    (ItemId, RenewalStatus, RenewedDate, RenewalCount,
                     NewStartDate, NewEndDate, RenewalYears, RenewalAmount, RenewalNotes,
                     CreatedBy, CreatedAt, IsArchived)
                VALUES
                    (@ItemId, 'Renewed', GETDATE(),
                     ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = @ItemId), 0) + 1,
                     @NewStartDate, @NewEndDate, @RenewalYears, @RenewalAmount, @RenewalNotes,
                     @CreatedBy, GETDATE(), 0)";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var old = connection.QuerySingleOrDefault<SetItemRenewalDto>(
                            getOldSql, new { SetItemId = oldSetItemId }, tx);

                        if (old == null)
                            throw new InvalidOperationException($"SetItem {oldSetItemId} not found.");

                        int effectiveItemId       = newItemId       ?? old.ItemId;
                        string effectiveItemCode  = newItemCode      ?? old.ItemCode;
                        decimal effectiveQty      = newQuantity      ?? old.Quantity;
                        decimal effectiveUnitPrice = newUnitPrice    ?? old.UnitPrice;
                        string effectiveUoM       = newUnitOfMeasure ?? old.UnitOfMeasure;
                        int effectiveSetId        = targetSetId ?? old.SetId;

                        int? groupId = SetItemSubTypeGroupHelper.FindOrCreateGroupId(
                            connection, tx, effectiveSetId, subType, referenceCode, groupBeginDate, groupEndDate, createdBy);

                        int? parentTagGroupId = SetItemParentTagGroupHelper.FindOrCreateGroupId(
                            connection, tx, effectiveSetId, parentTag, createdBy);

                        int newSetItemId = connection.ExecuteScalar<int>(insertNewSql, new
                        {
                            SetId         = effectiveSetId,
                            ItemId        = effectiveItemId,
                            ItemCode      = effectiveItemCode,
                            Description   = newDescription,
                            Quantity      = effectiveQty,
                            UnitOfMeasure = effectiveUoM,
                            UnitPrice     = effectiveUnitPrice,
                            Amount        = effectiveUnitPrice * effectiveQty,
                            LineStartDate = newLineStartDate,
                            LineEndDate   = newLineEndDate,
                            CreatedBy     = createdBy,
                            SubType         = (object)subType ?? DBNull.Value,
                            ReferenceCode   = (object)referenceCode ?? DBNull.Value,
                            GroupBeginDate  = (object)groupBeginDate ?? DBNull.Value,
                            GroupEndDate    = (object)groupEndDate ?? DBNull.Value,
                            GroupId         = (object)groupId ?? DBNull.Value,
                            ParentTagGroupId = (object)parentTagGroupId ?? DBNull.Value
                        }, tx);

                        connection.Execute(markOldSql, new
                        {
                            NewSetItemId = newSetItemId,
                            OldSetItemId = oldSetItemId
                        }, tx);

                        // History record tracks the OLD item being renewed, not the replacement.
                        // effectiveItemId is the new/replacement item; old.ItemId is the original.
                        connection.Execute(insertRenewalSql, new
                        {
                            ItemId        = old.ItemId,
                            NewStartDate  = newLineStartDate,
                            NewEndDate    = newLineEndDate,
                            RenewalYears  = (object)renewalYears  ?? DBNull.Value,
                            RenewalAmount = (object)renewalAmount ?? DBNull.Value,
                            RenewalNotes  = (object)renewalNotes  ?? DBNull.Value,
                            CreatedBy     = createdBy
                        }, tx);

                        tx.Commit();
                        ActivityLogger.Log(ActivityLogger.Actions.Create, "Renewal", newSetItemId, $"Item renewed (old SetItem #{oldSetItemId} → new SetItem #{newSetItemId})");

                        try
                        {
                            Services.InventoryActivityNotifier.NotifyRenewalCreated(effectiveItemId, newLineEndDate, createdBy);
                        }
                        catch { /* best-effort */ }

                        return newSetItemId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Inserts a brand-new dbo.SetItem directly into <paramref name="setId"/> with no
        /// predecessor row to mark as renewed — used when the Renew Items screen adds an extra
        /// item that wasn't part of the original Set at all.
        /// </summary>
        public int AddNewSetItem(
            int setId,
            int itemId,
            string itemCode,
            string description,
            decimal quantity,
            string unitOfMeasure,
            decimal unitPrice,
            DateTime lineStartDate,
            DateTime lineEndDate,
            int createdBy,
            string subType = null,
            string referenceCode = null,
            DateTime? groupBeginDate = null,
            DateTime? groupEndDate = null,
            string parentTag = null)
        {
            const string insertSql = @"
                INSERT INTO dbo.SetItem
                    (SetId, ItemId, ItemCode, Description, Quantity, UnitOfMeasure,
                     UnitPrice, Amount, LineStartDate, LineEndDate, CreatedBy, CreatedAt,
                     RenewalStatus, RenewalReferenceId,
                     SubType, ReferenceCode, BeginDate, EndDate, GroupId, ParentTagGroupId)
                VALUES
                    (@SetId, @ItemId, @ItemCode, @Description, @Quantity, @UnitOfMeasure,
                     @UnitPrice, @Amount, @LineStartDate, @LineEndDate, @CreatedBy, GETDATE(),
                     NULL, NULL,
                     @SubType, @ReferenceCode, @GroupBeginDate, @GroupEndDate, @GroupId, @ParentTagGroupId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        int? groupId = SetItemSubTypeGroupHelper.FindOrCreateGroupId(
                            connection, tx, setId, subType, referenceCode, groupBeginDate, groupEndDate, createdBy);

                        int? parentTagGroupId = SetItemParentTagGroupHelper.FindOrCreateGroupId(
                            connection, tx, setId, parentTag, createdBy);

                        int newSetItemId = connection.ExecuteScalar<int>(insertSql, new
                        {
                            SetId = setId,
                            ItemId = itemId,
                            ItemCode = itemCode,
                            Description = description,
                            Quantity = quantity,
                            UnitOfMeasure = unitOfMeasure,
                            UnitPrice = unitPrice,
                            Amount = unitPrice * quantity,
                            LineStartDate = lineStartDate,
                            LineEndDate = lineEndDate,
                            CreatedBy = createdBy,
                            SubType         = (object)subType ?? DBNull.Value,
                            ReferenceCode   = (object)referenceCode ?? DBNull.Value,
                            GroupBeginDate  = (object)groupBeginDate ?? DBNull.Value,
                            GroupEndDate    = (object)groupEndDate ?? DBNull.Value,
                            GroupId         = (object)groupId ?? DBNull.Value,
                            ParentTagGroupId = (object)parentTagGroupId ?? DBNull.Value
                        }, tx);

                        tx.Commit();
                        ActivityLogger.Log(createdBy, ActivityLogger.Actions.Create, "Renewal", newSetItemId, $"New item added during renewal (Set #{setId})");
                        return newSetItemId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new dbo.[Set] row that is a renewal of <paramref name="originalSetId"/>.
        /// Copies metadata (SetType, ComId, VendorId, Site, CurrentBranchId, CurrentDepartmentId)
        /// from the original and sets RenewalOfSetId = originalSetId.
        /// Returns the new SetId.
        /// </summary>
        public int CreateRenewalSet(
            int originalSetId,
            int userId,
            string documentNumber,
            string referenceNumber,
            DateTime? documentDate,
            DateTime? startDate,
            DateTime? endDate,
            decimal subtotal,
            decimal vatAmount,
            decimal whtAmount,
            decimal discountAmount,
            decimal totalAmountDue)
        {
            const string copySql = @"
                SELECT SetType, ComId, VendorId, Site, CurrentBranchId, CurrentDepartmentId
                FROM dbo.[Set] WHERE SetId = @SetId";

            const string insertSql = @"
                INSERT INTO dbo.[Set]
                    (SetType, ComId, VendorId, Site, CurrentBranchId, CurrentDepartmentId,
                     DocumentNumber, ReferenceNumber, DispatchDate,
                     StartDate, EndDate,
                     Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                     RenewalOfSetId, IsInvoice, CreatedBy, CreatedAt)
                VALUES
                    (@SetType, @ComId, @VendorId, @Site, @CurrentBranchId, @CurrentDepartmentId,
                     @DocumentNumber, @ReferenceNumber, @DocumentDate,
                     @StartDate, @EndDate,
                     @Subtotal, @VatAmount, @WhtAmount, @DiscountAmount, @TotalAmountDue,
                     @RenewalOfSetId, 1, @CreatedBy, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                var orig = conn.QuerySingleOrDefault<dynamic>(copySql, new { SetId = originalSetId });
                if (orig == null)
                    throw new InvalidOperationException($"Original Set {originalSetId} not found.");

                int newSetId = conn.ExecuteScalar<int>(insertSql, new
                {
                    SetType              = (string)orig.SetType,
                    ComId                = (object)orig.ComId ?? DBNull.Value,
                    VendorId             = (object)orig.VendorId ?? DBNull.Value,
                    Site                 = (object)orig.Site ?? DBNull.Value,
                    CurrentBranchId      = (object)orig.CurrentBranchId ?? DBNull.Value,
                    CurrentDepartmentId  = (object)orig.CurrentDepartmentId ?? DBNull.Value,
                    DocumentNumber       = string.IsNullOrEmpty(documentNumber) ? (object)DBNull.Value : documentNumber,
                    ReferenceNumber      = string.IsNullOrEmpty(referenceNumber) ? (object)DBNull.Value : referenceNumber,
                    DocumentDate         = (object)documentDate ?? DBNull.Value,
                    StartDate            = (object)startDate ?? DBNull.Value,
                    EndDate              = (object)endDate ?? DBNull.Value,
                    Subtotal             = subtotal,
                    VatAmount            = vatAmount,
                    WhtAmount            = whtAmount,
                    DiscountAmount       = discountAmount,
                    TotalAmountDue       = totalAmountDue,
                    RenewalOfSetId       = originalSetId,
                    CreatedBy            = userId
                });

                ActivityLogger.Log(userId, ActivityLogger.Actions.Create, "Renewal", newSetId, $"Renewal set created (Set #{newSetId}, renewal of Set #{originalSetId})");
                return newSetId;
            }
        }

        /// <summary>
        /// Walks the RenewalOfSetId chain to find the root Set, then walks down via recursive CTE
        /// to return all Sets in the chain ordered from original to latest.
        /// RenewalNumber is assigned in C# (1 = original, 2 = first renewal, etc.).
        /// </summary>
        public List<RenewalChainDto> GetRenewalChain(int anySetId)
        {
            const string parentSql = "SELECT RenewalOfSetId FROM dbo.[Set] WHERE SetId = @SetId";

            const string chainSql = @"
                WITH ChainDown AS (
                    SELECT SetId, SetCode, DocumentNumber, RenewalOfSetId, StartDate, EndDate
                    FROM dbo.[Set] WHERE SetId = @RootId
                    UNION ALL
                    SELECT s.SetId, s.SetCode, s.DocumentNumber, s.RenewalOfSetId, s.StartDate, s.EndDate
                    FROM dbo.[Set] s
                    INNER JOIN ChainDown c ON s.RenewalOfSetId = c.SetId
                )
                SELECT * FROM ChainDown ORDER BY SetId";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                // Walk up to root
                int rootId = anySetId;
                while (true)
                {
                    int? parent = conn.QuerySingleOrDefault<int?>(parentSql, new { SetId = rootId });
                    if (!parent.HasValue) break;
                    rootId = parent.Value;
                }

                var chain = conn.Query<RenewalChainDto>(chainSql, new { RootId = rootId }).AsList();
                for (int i = 0; i < chain.Count; i++)
                    chain[i].RenewalNumber = i + 1;
                return chain;
            }
        }

        /// <summary>
        /// Returns active items for use in the Renew Item picker. Every ItemType (Hardware
        /// included) is eligible — a Hardware item can be invoiced under a Contract/Subscription/
        /// License/Services Sub-Type just like a Software/Service item, so it must be renewable
        /// too. ItemCode and UnitPrice are sourced from the most recent SetItem row for each item
        /// (dbo.Item has no ItemCode column; it lives in dbo.SetItem).
        /// </summary>
        public List<ItemCatalogDto> GetRenewableItemCatalog()
        {
            const string sql = @"
                SELECT
                    i.ItemId,
                    ISNULL(latest.ItemCode, '')        AS ItemCode,
                    i.Name,
                    ISNULL(i.UnitOfMeasure, '')        AS UnitOfMeasure,
                    ISNULL(latest.UnitPrice, i.Amount) AS UnitPrice
                FROM dbo.Item i
                OUTER APPLY (
                    SELECT TOP 1 si.ItemCode, si.UnitPrice
                    FROM dbo.SetItem si
                    WHERE si.ItemId = i.ItemId
                    ORDER BY si.SetItemId DESC
                ) latest
                WHERE i.Active = 1
                ORDER BY i.Name";

            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.Query<ItemCatalogDto>(sql).AsList();
        }

        /// <summary>
        /// Gets renewals filtered by expiry status
        /// ✅ FILTER RULE: Only shows Sets with existing non-archived renewal records
        /// </summary>
        public List<RenewalDto> GetRenewalsByExpiryStatus(string expiryStatus)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                string query = @"
                    SELECT
                        SetId, SetCode, SetType, DocumentNumber, ReferenceNumber,
                        DocumentDate, Status, Remarks,
                        StartDate, EndDate, DaysUntilExpiry, ExpiryStatus,
                        ComId, CompanyName,
                        SiteName,
                        Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                        ItemCount,
                        CreatedBy, CreatedDate
                    FROM dbo.vw_RenewalStatus
                    WHERE ExpiryStatus = @ExpiryStatus
                    ORDER BY DaysUntilExpiry ASC";

                return connection.Query<RenewalDto>(query, new { ExpiryStatus = expiryStatus }).AsList();
            }
        }

        /// <summary>
        /// Gets expired renewals
        /// </summary>
        public List<RenewalDto> GetExpiredRenewals()
        {
            return GetRenewalsByExpiryStatus("Expired");
        }

        /// <summary>
        /// Gets renewals expiring soon (within 30 days)
        /// </summary>
        public List<RenewalDto> GetExpiringSoonRenewals()
        {
            return GetRenewalsByExpiryStatus("Expiring Soon");
        }

        /// <summary>
        /// Gets renewals with warning status (30-90 days until expiry)
        /// </summary>
        public List<RenewalDto> GetWarningRenewals()
        {
            return GetRenewalsByExpiryStatus("Warning");
        }

        /// <summary>
        /// Gets renewals by company
        /// ✅ FILTER RULE: Only shows Sets with existing non-archived renewal records
        /// </summary>
        public List<RenewalDto> GetRenewalsByCompany(int comId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                string query = @"
                    SELECT
                        SetId, SetCode, SetType, DocumentNumber, ReferenceNumber,
                        DocumentDate, Status, Remarks,
                        StartDate, EndDate, DaysUntilExpiry, ExpiryStatus,
                        ComId, CompanyName,
                        SiteName,
                        Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                        ItemCount,
                        CreatedBy, CreatedDate
                    FROM dbo.vw_RenewalStatus
                    WHERE ComId = @ComId
                    ORDER BY
                        CASE ExpiryStatus
                            WHEN 'Expired' THEN 1
                            WHEN 'Expiring Soon' THEN 2
                            WHEN 'Warning' THEN 3
                            WHEN 'Active' THEN 4
                            ELSE 5
                        END,
                        DaysUntilExpiry ASC";

                return connection.Query<RenewalDto>(query, new { ComId = comId }).AsList();
            }
        }

        /// <summary>
        /// Gets renewals by SetType (Software/License or Service)
        /// ✅ FILTER RULE: Only shows Sets with existing non-archived renewal records
        /// </summary>
        public List<RenewalDto> GetRenewalsByType(string setType)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                string query = @"
                    SELECT
                        SetId, SetCode, SetType, DocumentNumber, ReferenceNumber,
                        DocumentDate, Status, Remarks,
                        StartDate, EndDate, DaysUntilExpiry, ExpiryStatus,
                        ComId, CompanyName,
                        SiteName,
                        Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                        ItemCount,
                        CreatedBy, CreatedDate
                    FROM dbo.vw_RenewalStatus
                    WHERE SetType = @SetType
                    ORDER BY
                        CASE ExpiryStatus
                            WHEN 'Expired' THEN 1
                            WHEN 'Expiring Soon' THEN 2
                            WHEN 'Warning' THEN 3
                            WHEN 'Active' THEN 4
                            ELSE 5
                        END,
                        DaysUntilExpiry ASC";

                return connection.Query<RenewalDto>(query, new { SetType = setType }).AsList();
            }
        }

        /// <summary>
        /// Gets a single renewal by SetId
        /// </summary>
        public RenewalDto GetRenewalById(int setId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                string query = @"
                    SELECT
                        SetId, SetCode, SetType, DocumentNumber, ReferenceNumber,
                        DocumentDate, Status, Remarks,
                        StartDate, EndDate, DaysUntilExpiry, ExpiryStatus,
                        ComId, CompanyName,
                        SiteName,
                        Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                        ItemCount,
                        CreatedBy, CreatedDate
                    FROM dbo.vw_RenewalStatus
                    WHERE SetId = @SetId";

                return connection.QuerySingleOrDefault<RenewalDto>(query, new { SetId = setId });
            }
        }

        /// <summary>
        /// Gets renewals expiring within a specific number of days
        /// ✅ FILTER RULE: Only shows Sets with existing non-archived renewal records
        /// </summary>
        public List<RenewalDto> GetRenewalsExpiringWithinDays(int days)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                string query = @"
                    SELECT
                        SetId, SetCode, SetType, DocumentNumber, ReferenceNumber,
                        DocumentDate, Status, Remarks,
                        StartDate, EndDate, DaysUntilExpiry, ExpiryStatus,
                        ComId, CompanyName,
                        SiteName,
                        Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                        ItemCount,
                        CreatedBy, CreatedDate
                    FROM dbo.vw_RenewalStatus
                    WHERE DaysUntilExpiry IS NOT NULL
                      AND DaysUntilExpiry <= @Days
                      AND DaysUntilExpiry >= 0
                    ORDER BY DaysUntilExpiry ASC";

                return connection.Query<RenewalDto>(query, new { Days = days }).AsList();
            }
        }

        /// <summary>
        /// Updates the End Date of a Set/Renewal using the stored procedure sp_UpdateSetEndDate
        /// </summary>
        /// <param name="setId">The SetId to update</param>
        /// <param name="newEndDate">The new End Date (must be at least 1 year from Start Date)</param>
        /// <param name="modifiedBy">The user ID making the change</param>
        /// <returns>Success message or throws exception on error</returns>
        public string UpdateSetEndDate(int setId, DateTime newEndDate, int modifiedBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    var result = connection.QuerySingleOrDefault<string>(
                        "sp_UpdateSetEndDate",
                        new
                        {
                            SetId = setId,
                            NewEndDate = newEndDate,
                            ModifiedBy = modifiedBy
                        },
                        commandType: System.Data.CommandType.StoredProcedure
                    );

                    return result ?? "End Date updated successfully";
                }
                catch (SqlException ex)
                {
                    // Re-throw with the SQL error message for display to user
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Updates both StartDate and EndDate of a Set for renewal purposes
        /// Also updates the Renewals table to record the RenewedDate for all items in the set
        /// </summary>
        /// <param name="setId">The SetId to update</param>
        /// <param name="newStartDate">The new Start Date</param>
        /// <param name="newEndDate">The new End Date</param>
        /// <param name="modifiedBy">The user ID making the change</param>
        public void UpdateSetDates(int setId, DateTime newStartDate, DateTime newEndDate, int modifiedBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update Set dates
                            string updateSetSql = @"
                                UPDATE dbo.[Set]
                                SET StartDate = @StartDate,
                                    EndDate = @EndDate
                                WHERE SetId = @SetId";

                            int rowsAffected = connection.Execute(updateSetSql, new
                            {
                                SetId = setId,
                                StartDate = newStartDate,
                                EndDate = newEndDate
                            }, transaction);

                            if (rowsAffected == 0)
                            {
                                throw new InvalidOperationException($"No set found with SetId: {setId}");
                            }

                            // Create NEW renewal records for all items in this set
                            // This maintains the renewal history by creating a new record each time
                            string insertRenewalsSql = @"
                                INSERT INTO dbo.Renewals (
                                    ItemId,
                                    RenewalStatus,
                                    RenewedDate,
                                    RenewalCount,
                                    NewStartDate,
                                    NewEndDate,
                                    CreatedBy,
                                    CreatedAt
                                )
                                SELECT
                                    si.ItemId,
                                    'Renewed',
                                    GETDATE(),
                                    -- Get the next renewal count for this item (or 1 if no previous renewals)
                                    ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = si.ItemId), 0) + 1,
                                    @NewStartDate,
                                    @NewEndDate,
                                    @ModifiedBy,
                                    GETDATE()
                                FROM dbo.SetItem si
                                WHERE si.SetId = @SetId";

                            connection.Execute(insertRenewalsSql, new
                            {
                                SetId = setId,
                                NewStartDate = newStartDate,
                                NewEndDate = newEndDate,
                                ModifiedBy = modifiedBy
                            }, transaction);

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    throw new InvalidOperationException($"Failed to update renewal dates: {ex.Message}\n\nSetId: {setId}\nStartDate: {newStartDate:yyyy-MM-dd}\nEndDate: {newEndDate:yyyy-MM-dd}", ex);
                }
            }
        }

        /// <summary>
        /// Gets renewal details by ItemId using stored procedure
        /// </summary>
        public RenewalDetailDto GetRenewalDetailsByItemId(int itemId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    return connection.QueryFirstOrDefault<RenewalDetailDto>(
                        "sp_GetRenewalDetailsByItemId",
                        new { ItemId = itemId },
                        commandType: System.Data.CommandType.StoredProcedure
                    );
                }
                catch (SqlException ex) when (ex.Number == 229 || (ex.Message != null && ex.Message.IndexOf("EXECUTE permission was denied", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return GetRenewalDetailsByItemId_FallbackQuery(connection, itemId);
                }
            }
        }

        /// <summary>
        /// Gets renewal history by ItemId using stored procedure
        /// </summary>
        public List<RenewalHistoryDto> GetRenewalHistoryByItemId(int itemId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    return connection.Query<RenewalHistoryDto>(
                        "sp_GetRenewalHistoryByItemId",
                        new { ItemId = itemId },
                        commandType: System.Data.CommandType.StoredProcedure
                    ).AsList();
                }
                catch (SqlException ex) when (ex.Number == 229 || (ex.Message != null && ex.Message.IndexOf("EXECUTE permission was denied", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return GetRenewalHistoryByItemId_FallbackQuery(connection, itemId);
                }
            }
        }

        private static RenewalDetailDto GetRenewalDetailsByItemId_FallbackQuery(SqlConnection connection, int itemId)
        {
            const string query = @"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    i.Description,
    i.ItemType,
    i.SerialNumber,
    i.ModelNumber,
    i.LicenseNumber,
    i.Amount,
    i.StartDate,
    i.EndDate,
    i.DateCreated,
    i.DateModified,
    i.Category,
    i.Remarks AS ItemRemarks,
    i.Active,

    ic.Name AS CategoryName,

    i.VendorId AS VendorId,
    v.VendorName AS VendorName,
    v.Address AS VendorAddress,
    v.TIN AS VendorTIN,

    cond.ConditionName AS ConditionName,

    CASE
        WHEN i.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
    END AS DaysUntilExpiry,

    CASE
        WHEN i.EndDate IS NULL THEN 'No Expiry Date'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) < 0 THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    u.Name AS CreatedByUsername,

    (SELECT TOP 1 RenewalStatus
     FROM dbo.Renewals
     WHERE ItemId = i.ItemId
     ORDER BY CreatedAt DESC) AS CurrentRenewalStatus,

    -- Asset linkage: latest non-archived renewal's AssetId (null for legacy rows)
    (SELECT TOP 1 r2.AssetId
     FROM dbo.Renewals r2
     WHERE r2.ItemId = i.ItemId AND r2.IsArchived = 0
     ORDER BY r2.CreatedAt DESC) AS AssetId,
    a_latest.ModelNumber AS PartNumber,
    a_latest.SerialNumber AS AssetSerialNumber,

    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,
    s.Site AS SiteName

FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.Condition cond ON i.ConditionID = cond.ConditionID
LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
-- OUTER APPLY (not a plain JOIN) so an item now belonging to more than one Set
-- (e.g. renewed into a new Set, per the Renew Items feature) still returns exactly
-- one row here instead of one row per Set.
OUTER APPLY (
    SELECT TOP 1 s2.SetId, s2.Subtotal, s2.VatAmount, s2.WhtAmount, s2.DiscountAmount,
           s2.TotalAmountDue, s2.CurrentBranchId, s2.CurrentDepartmentId, s2.Site
    FROM dbo.SetItem si2
    INNER JOIN dbo.[Set] s2 ON si2.SetId = s2.SetId AND s2.IsInvoice = 1
    WHERE si2.ItemId = i.ItemId
    ORDER BY s2.SetId DESC
) s
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
-- Resolve the asset for the most recent non-archived renewal (LEFT JOIN = safe when AssetId is NULL)
LEFT JOIN dbo.Asset a_latest ON a_latest.AssetId = (
    SELECT TOP 1 r3.AssetId
    FROM dbo.Renewals r3
    WHERE r3.ItemId = i.ItemId AND r3.IsArchived = 0
    ORDER BY r3.CreatedAt DESC)
WHERE i.ItemId = @ItemId";

            return connection.QueryFirstOrDefault<RenewalDetailDto>(query, new { ItemId = itemId });
        }

        private static List<RenewalHistoryDto> GetRenewalHistoryByItemId_FallbackQuery(SqlConnection connection, int itemId)
        {
            const string query = @"
SELECT
    r.RenewalId,
    r.ItemId,
    r.RenewalStatus,
    r.OnHoldDate,
    r.RenewedDate,
    r.RenewalCount,
    r.NewStartDate,
    r.NewEndDate,
    r.RenewalYears,
    r.IsArchived,
    r.ArchivedDate,
    r.ArchiveReason,
    r.CreatedBy,
    r.CreatedAt,
    r.ModifiedBy,
    r.ModifiedAt,
    r.RenewalNotes,
    r.RenewalAmount,
    u.Name AS CreatedByUsername,
    u2.Name AS ModifiedByUsername
FROM dbo.Renewals r
LEFT JOIN dbo.[User] u ON r.CreatedBy = u.UserId
LEFT JOIN dbo.[User] u2 ON r.ModifiedBy = u2.UserId
WHERE r.ItemId = @ItemId
ORDER BY r.CreatedAt DESC";

            return connection.Query<RenewalHistoryDto>(query, new { ItemId = itemId }).AsList();
        }

        /// <summary>
        /// Creates a new renewal record using stored procedure
        /// Also updates the Set table's StartDate and EndDate if renewalStatus is "Renewed"
        /// </summary>
        public int CreateRenewal(
            int itemId,
            string renewalStatus,
            DateTime newStartDate,
            DateTime newEndDate,
            int renewalYears,
            decimal? renewalAmount,
            string renewalNotes,
            int createdBy,
            int? setIdOverride = null)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Create the renewal record
                        var parameters = new DynamicParameters();
                        parameters.Add("@ItemId", itemId);
                        parameters.Add("@RenewalStatus", renewalStatus);
                        parameters.Add("@NewStartDate", newStartDate);
                        parameters.Add("@NewEndDate", newEndDate);
                        parameters.Add("@RenewalYears", renewalYears);
                        parameters.Add("@RenewalAmount", renewalAmount);
                        parameters.Add("@RenewalNotes", renewalNotes);
                        parameters.Add("@CreatedBy", createdBy);
                        parameters.Add("@RenewalId", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

                        connection.Execute(
                            "sp_CreateRenewal",
                            parameters,
                            transaction: transaction,
                            commandType: System.Data.CommandType.StoredProcedure
                        );

                        int renewalId = parameters.Get<int>("@RenewalId");

                        // If status is "Renewed", also update the Set table dates
                        if (renewalStatus == "Renewed")
                        {
                            int? setId = setIdOverride;

                            if (!setId.HasValue)
                            {
                                // Fallback: Get the SetId for this item (legacy behavior)
                                string getSetIdQuery = @"
                                    SELECT TOP 1 SetId
                                    FROM dbo.SetItem
                                    WHERE ItemId = @ItemId";

                                setId = connection.QueryFirstOrDefault<int?>(
                                    getSetIdQuery,
                                    new { ItemId = itemId },
                                    transaction
                                );
                            }

                            if (setId.HasValue)
                            {
                                // Update the Set table with new dates
                                string updateSetQuery = @"
                                    UPDATE dbo.[Set]
                                    SET StartDate = @StartDate,
                                        EndDate = @EndDate
                                    WHERE SetId = @SetId";

                                connection.Execute(
                                    updateSetQuery,
                                    new
                                    {
                                        SetId = setId.Value,
                                        StartDate = newStartDate,
                                        EndDate = newEndDate
                                    },
                                    transaction
                                );
                            }
                        }

                        transaction.Commit();

                        try
                        {
                            Services.InventoryActivityNotifier.NotifyRenewalCreated(itemId, newEndDate, createdBy);
                        }
                        catch { /* best-effort */ }

                        return renewalId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Archives a renewal and its associated item using stored procedure
        /// </summary>
        public void ArchiveRenewal(int renewalId, int itemId, string archiveReason, string archivedBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Execute(
                    "sp_ArchiveRenewal",
                    new
                    {
                        RenewalId = renewalId,
                        ItemId = itemId,
                        ArchiveReason = archiveReason,
                        ArchivedBy = archivedBy
                    },
                    commandType: System.Data.CommandType.StoredProcedure
                );
            }
        }

        /// <summary>
        /// Updates renewal status using stored procedure
        /// </summary>
        public void UpdateRenewalStatus(int renewalId, string renewalStatus, int modifiedBy)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Execute(
                    "sp_UpdateRenewalStatus",
                    new
                    {
                        RenewalId = renewalId,
                        RenewalStatus = renewalStatus,
                        ModifiedBy = modifiedBy
                    },
                    commandType: System.Data.CommandType.StoredProcedure
                );
            }
            ActivityLogger.Log(modifiedBy, ActivityLogger.Actions.Update,
                "Renewal", renewalId,
                $"Renewal #{renewalId} status set to: {renewalStatus}");
        }

        /// <summary>
        /// Creates a renewal record that includes an AssetId (physical device link).
        /// Uses a direct INSERT (bypasses sp_CreateRenewal which predates the Asset table).
        /// AssetId is nullable — pass null for legacy / non-asset renewals.
        /// Returns the new RenewalId.
        /// </summary>
        public int CreateRenewalForAsset(
            int itemId,
            int? assetId,
            string renewalStatus,
            DateTime newStartDate,
            DateTime newEndDate,
            int renewalYears,
            decimal? renewalAmount,
            string renewalNotes,
            int createdBy)
        {
            const string sql = @"
                INSERT INTO dbo.Renewals
                    (ItemId, AssetId, RenewalStatus, NewStartDate, NewEndDate,
                     RenewalYears, RenewalAmount, RenewalNotes, CreatedBy, CreatedAt,
                     RenewalCount, IsArchived)
                VALUES
                    (@ItemId, @AssetId, @RenewalStatus, @NewStartDate, @NewEndDate,
                     @RenewalYears, @RenewalAmount, @RenewalNotes, @CreatedBy, GETDATE(),
                     ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = @ItemId), 0) + 1,
                     0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int newRenewalId;
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                newRenewalId = connection.ExecuteScalar<int>(sql, new
                {
                    ItemId        = itemId,
                    AssetId       = (object)assetId ?? DBNull.Value,
                    RenewalStatus = renewalStatus,
                    NewStartDate  = newStartDate,
                    NewEndDate    = newEndDate,
                    RenewalYears  = renewalYears,
                    RenewalAmount = (object)renewalAmount ?? DBNull.Value,
                    RenewalNotes  = (object)renewalNotes  ?? DBNull.Value,
                    CreatedBy     = createdBy
                });
            }
            ActivityLogger.Log(createdBy, ActivityLogger.Actions.Create,
                "Renewal", newRenewalId,
                $"Renewal #{newRenewalId} created — {renewalStatus}");

            try
            {
                Services.InventoryActivityNotifier.NotifyRenewalCreated(itemId, newEndDate, createdBy);
            }
            catch { /* best-effort */ }

            return newRenewalId;
        }

        /// <summary>
        /// Returns true when an active (non-archived) renewal already exists for the given
        /// ItemId + AssetId combination whose date range overlaps [checkStart, checkEnd].
        /// Used by AddRenewalDialog to surface duplicate warnings before saving.
        /// AssetId null is treated as "no specific asset" and matched by IS NULL.
        /// </summary>
        public bool HasOverlappingRenewal(int itemId, int? assetId, DateTime checkStart, DateTime checkEnd)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM dbo.Renewals
                WHERE ItemId = @ItemId
                  AND ISNULL(AssetId, -1) = ISNULL(@AssetId, -1)
                  AND IsArchived = 0
                  AND NewStartDate <= @CheckEnd
                  AND NewEndDate   >= @CheckStart";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                int count = connection.ExecuteScalar<int>(sql, new
                {
                    ItemId     = itemId,
                    AssetId    = (object)assetId ?? DBNull.Value,
                    CheckStart = checkStart,
                    CheckEnd   = checkEnd
                });
                return count > 0;
            }
        }

        /// <summary>
        /// Returns a lightweight list of active Services items for use in AddRenewalDialog pickers.
        /// </summary>
        public List<Models.LookupItem> GetServiceItems()
        {
            const string sql = @"
                SELECT ItemId AS Id, Name
                FROM dbo.Item
                WHERE Active = 1
                ORDER BY Name";

            using (var connection = new SqlConnection(GetConnectionString()))
                return connection.Query<Models.LookupItem>(sql).AsList();
        }

        /// <summary>
        /// Archives a single SetItem and its associated Item.
        /// Sets SetItem.RenewalStatus = 'Archived', Item.Active = 0,
        /// and inserts an archived Renewals record for history visibility.
        /// </summary>
        public void ArchiveSetItem(int setItemId, int userId, string reason)
        {
            const string getItemSql = "SELECT ItemId FROM dbo.SetItem WHERE SetItemId = @SetItemId";
            const string archiveSetItemSql = @"
                UPDATE dbo.SetItem SET RenewalStatus = 'Archived' WHERE SetItemId = @SetItemId";
            const string archiveItemSql = @"
                UPDATE dbo.Item SET Active = 0, DateModified = GETDATE() WHERE ItemId = @ItemId";
            const string insertRenewalSql = @"
                INSERT INTO dbo.Renewals
                    (ItemId, RenewalStatus, IsArchived, ArchivedDate, ArchiveReason,
                     RenewalCount, CreatedBy, CreatedAt)
                VALUES
                    (@ItemId, 'Archived', 1, GETDATE(), @Reason,
                     ISNULL((SELECT MAX(RenewalCount) FROM dbo.Renewals WHERE ItemId = @ItemId), 0) + 1,
                     @UserId, GETDATE())";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        int itemId = connection.ExecuteScalar<int>(getItemSql, new { SetItemId = setItemId }, tx);
                        connection.Execute(archiveSetItemSql, new { SetItemId = setItemId }, tx);
                        connection.Execute(archiveItemSql, new { ItemId = itemId }, tx);
                        connection.Execute(insertRenewalSql, new { ItemId = itemId, Reason = reason, UserId = userId }, tx);

                        string serial = null;
                        using (var lookup = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", connection, tx))
                        {
                            lookup.Parameters.AddWithValue("@ItemId", itemId);
                            var result = lookup.ExecuteScalar();
                            serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                        }
                        ItemAuditTrailWriter.TryLog(connection, tx, new ItemAuditTrailDto
                        {
                            ItemId = itemId,
                            SerialNumber = serial,
                            Action = "Item Renewal Archived",
                            ActionTime = DateTime.Now,
                            Direction = "OUT",
                            Status = "Completed",
                            ReferenceType = "Item",
                            ReferenceId = itemId,
                            Notes = string.IsNullOrWhiteSpace(reason) ? "Item archived via renewal workflow." : $"Archived via renewal workflow: {reason}",
                            CreatedBy = AppSession.CurrentUserName ?? "System"
                        });

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

        /// <summary>
        /// Gets renewal history for all items belonging to a Set.
        /// Includes ItemName and ItemCode for identification in the set-level history view.
        /// </summary>
        public List<RenewalHistoryDto> GetRenewalHistoryBySetId(int setId)
        {
            const string sql = @"
SELECT
    r.RenewalId,
    r.ItemId,
    r.RenewalStatus,
    r.OnHoldDate,
    r.RenewedDate,
    r.RenewalCount,
    r.NewStartDate,
    r.NewEndDate,
    r.RenewalYears,
    r.IsArchived,
    r.ArchivedDate,
    r.ArchiveReason,
    r.CreatedBy,
    r.CreatedAt,
    r.ModifiedBy,
    r.ModifiedAt,
    r.RenewalNotes,
    r.RenewalAmount,
    u.Name  AS CreatedByUsername,
    u2.Name AS ModifiedByUsername,
    i.Name  AS ItemName,
    ISNULL(latest_si.ItemCode, '') AS ItemCode
FROM dbo.Renewals r
INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
LEFT JOIN dbo.[User] u  ON r.CreatedBy  = u.UserId
LEFT JOIN dbo.[User] u2 ON r.ModifiedBy = u2.UserId
OUTER APPLY (
    SELECT TOP 1 si2.ItemCode
    FROM dbo.SetItem si2
    WHERE si2.ItemId = r.ItemId
    ORDER BY si2.SetItemId DESC
) latest_si
WHERE r.ItemId IN (
    SELECT DISTINCT ItemId FROM dbo.SetItem WHERE SetId = @SetId
)
ORDER BY r.CreatedAt DESC";

            using (var connection = new SqlConnection(GetConnectionString()))
                return connection.Query<RenewalHistoryDto>(sql, new { SetId = setId }).AsList();
        }

        /// <summary>
        /// Updates dbo.Renewals.RenewalNotes on the "current" (non-archived, highest RenewalCount)
        /// renewal record for every item belonging to this Set. Renewal Notes is entered once per
        /// renewal action and applies to every item renewed together in that action, so editing it
        /// here updates all of them in lockstep, mirroring how it was originally set.
        /// </summary>
        public int UpdateCurrentRenewalNotesForSet(int setId, string notes, int modifiedBy)
        {
            const string sql = @"
UPDATE r
SET r.RenewalNotes = @Notes,
    r.ModifiedBy    = @ModifiedBy,
    r.ModifiedAt    = SYSUTCDATETIME()
FROM dbo.Renewals r
INNER JOIN (
    SELECT ItemId, MAX(RenewalCount) AS MaxCount
    FROM dbo.Renewals
    WHERE IsArchived = 0
      AND ItemId IN (SELECT DISTINCT ItemId FROM dbo.SetItem WHERE SetId = @SetId)
    GROUP BY ItemId
) cur ON cur.ItemId = r.ItemId AND cur.MaxCount = r.RenewalCount
WHERE r.IsArchived = 0";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return connection.Execute(sql, new
                {
                    SetId = setId,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    ModifiedBy = modifiedBy
                });
            }
        }

        /// <summary>
        /// Same as <see cref="UpdateCurrentRenewalNotesForSet"/> but for a standalone item renewal
        /// session with no backing Set (e.g. opened from the Warranty page).
        /// </summary>
        public int UpdateCurrentRenewalNotesForItem(int itemId, string notes, int modifiedBy)
        {
            const string sql = @"
UPDATE r
SET r.RenewalNotes = @Notes,
    r.ModifiedBy    = @ModifiedBy,
    r.ModifiedAt    = SYSUTCDATETIME()
FROM dbo.Renewals r
INNER JOIN (
    SELECT ItemId, MAX(RenewalCount) AS MaxCount
    FROM dbo.Renewals
    WHERE IsArchived = 0 AND ItemId = @ItemId
    GROUP BY ItemId
) cur ON cur.ItemId = r.ItemId AND cur.MaxCount = r.RenewalCount
WHERE r.IsArchived = 0";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return connection.Execute(sql, new
                {
                    ItemId = itemId,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    ModifiedBy = modifiedBy
                });
            }
        }

        /// <summary>
        /// Returns invoice sets eligible to be linked as the renewal of <paramref name="originalSetId"/>.
        /// Filters: same-category SetType, not already a child in another chain (RenewalOfSetId IS NULL),
        /// not the original set itself, and not already having the original as its ancestor.
        /// </summary>
        public List<InvoiceSetPickerDto> GetAvailableInvoiceSetsForLinking(int originalSetId)
        {
            const string sql = @"
                SELECT
                    s.SetId,
                    s.SetCode,
                    s.SetType,
                    s.DocumentNumber,
                    s.ReferenceNumber,
                    ISNULL(c.Name, 'N/A') AS CompanyName,
                    s.StartDate,
                    s.EndDate,
                    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue
                FROM dbo.[Set] s
                LEFT JOIN dbo.Company c ON s.ComId = c.ComId
                WHERE s.SetId <> @OriginalSetId
                  AND s.RenewalOfSetId IS NULL
                  AND s.SetType IN ('Software/License', 'Service', 'Services')
                ORDER BY s.SetId DESC";

            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.Query<InvoiceSetPickerDto>(sql, new { OriginalSetId = originalSetId }).AsList();
        }

        /// <summary>
        /// Returns lightweight item summaries for a single set — used by the WPF Link Renewal screen.
        /// Excludes Archived items.
        /// </summary>
        public List<SetItemSummaryDto> GetSetItemSummaries(int setId)
        {
            const string sql = @"
                SELECT
                    si.SetId, si.SetItemId, si.ItemId,
                    ISNULL(si.ItemCode, '') AS ItemCode,
                    ISNULL(si.Description, '') AS Description,
                    ISNULL(si.Amount, 0) AS Amount,
                    si.LineStartDate, si.LineEndDate
                FROM dbo.SetItem si
                WHERE si.SetId = @SetId
                  AND ISNULL(si.RenewalStatus, 'Active') <> 'Archived'";
            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.Query<SetItemSummaryDto>(sql, new { SetId = setId }).AsList();
        }

        /// <summary>
        /// Bulk-fetches item summaries for a list of candidate set IDs in a single query.
        /// Used to populate match metrics for all candidates at once in the WPF Link Renewal screen.
        /// </summary>
        public List<SetItemSummaryDto> GetCandidateItemSummaries(IEnumerable<int> setIds)
        {
            var ids = setIds?.ToList();
            if (ids == null || ids.Count == 0) return new List<SetItemSummaryDto>();

            const string sql = @"
                SELECT
                    si.SetId, si.SetItemId, si.ItemId,
                    ISNULL(si.ItemCode, '') AS ItemCode,
                    ISNULL(si.Description, '') AS Description,
                    ISNULL(si.Amount, 0) AS Amount,
                    si.LineStartDate, si.LineEndDate
                FROM dbo.SetItem si
                WHERE si.SetId IN @SetIds
                  AND ISNULL(si.RenewalStatus, 'Active') <> 'Archived'";
            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.Query<SetItemSummaryDto>(sql, new { SetIds = ids }).AsList();
        }

        /// <summary>
        /// Links <paramref name="renewalSetId"/> as the renewal of <paramref name="originalSetId"/> by:
        ///   1. Setting renewalSet.RenewalOfSetId = originalSetId.
        ///   2. Inserting Renewals history records for every currently-active item in the original set.
        ///   3. Marking those items as RenewalStatus = 'Renewed'.
        /// The original set's document data is never modified.
        /// </summary>
        public void LinkExistingInvoiceSet(int originalSetId, int renewalSetId)
        {
            const string linkSql = @"
                UPDATE dbo.[Set] SET RenewalOfSetId = @OriginalSetId WHERE SetId = @RenewalSetId";

            // Insert Renewals history BEFORE marking items so history captures the transition.
            // Use the renewal set's own CreatedBy so the FK always resolves regardless of who
            // is performing the link operation.
            const string insertHistorySql = @"
                INSERT INTO dbo.Renewals
                    (ItemId, RenewalStatus, RenewedDate, RenewalCount,
                     NewStartDate, NewEndDate, CreatedBy, CreatedAt, IsArchived)
                SELECT
                    si.ItemId,
                    'Renewed',
                    GETDATE(),
                    ISNULL((SELECT MAX(r2.RenewalCount) FROM dbo.Renewals r2 WHERE r2.ItemId = si.ItemId), 0) + 1,
                    ns.StartDate,
                    ns.EndDate,
                    ns.CreatedBy,
                    GETDATE(),
                    0
                FROM dbo.SetItem si
                CROSS APPLY (SELECT StartDate, EndDate, CreatedBy FROM dbo.[Set] WHERE SetId = @RenewalSetId) ns
                WHERE si.SetId = @OriginalSetId
                  AND ISNULL(si.RenewalStatus, 'Active') = 'Active'";

            const string markRenewedSql = @"
                UPDATE dbo.SetItem
                SET RenewalStatus = 'Renewed'
                WHERE SetId = @OriginalSetId
                  AND ISNULL(RenewalStatus, 'Active') = 'Active'";

            using (var conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        conn.Execute(linkSql, new { OriginalSetId = originalSetId, RenewalSetId = renewalSetId }, tx);
                        conn.Execute(insertHistorySql, new { OriginalSetId = originalSetId, RenewalSetId = renewalSetId }, tx);
                        conn.Execute(markRenewedSql, new { OriginalSetId = originalSetId }, tx);
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

        /// <summary>
        /// Returns the full RenewalDto for a single set (including VendorName from dbo.Vendor).
        /// Used by the WPF RenewalDetailWindow to populate its header and financial summary.
        /// </summary>
        public RenewalDto GetRenewalSetById(int setId)
        {
            const string sql = @"
                SELECT
                    r.SetId, r.SetCode, r.SetType, r.DocumentNumber, r.ReferenceNumber,
                    r.DocumentDate, r.Status, r.Remarks,
                    r.StartDate, r.EndDate, r.DaysUntilExpiry, r.ExpiryStatus,
                    r.ComId, r.CompanyName,
                    r.SiteName,
                    r.CurrentBranchId, r.CurrentBranchName,
                    r.CurrentDepartmentId, r.CurrentDepartmentName,
                    r.Subtotal, r.VatAmount, r.WhtAmount, r.DiscountAmount, r.TotalAmountDue,
                    r.ItemCount,
                    r.TotalActiveItems, r.ExpiredItemsCount, r.RenewedItemsCount, r.ArchivedItemsCount,
                    r.SetLevelStatus,
                    r.PartNumber,
                    r.RenewalOfSetId,
                    r.CreatedBy, r.CreatedDate,
                    CAST(1 AS BIT) AS Active,
                    s.VendorId,
                    v.VendorName
                FROM dbo.vw_RenewalStatus r
                INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
                LEFT JOIN dbo.Vendor v ON v.VendorId = s.VendorId
                WHERE r.SetId = @SetId";
            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.QuerySingleOrDefault<RenewalDto>(sql, new { SetId = setId });
        }

        /// <summary>
        /// Returns the editable document header fields (DocumentNumber, ReferenceNumber, DocumentDate)
        /// for a Set. DocumentDate maps to dbo.[Set].DispatchDate.
        /// </summary>
        public SetHeaderDto GetSetHeader(int setId)
        {
            const string sql = @"
                SELECT s.SetCode, s.DocumentNumber, s.ReferenceNumber,
                       s.DispatchDate AS DocumentDate,
                       ISNULL(u.Name, '') AS CreatedByUsername
                FROM dbo.[Set] s
                LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                WHERE s.SetId = @SetId";
            using (var connection = new SqlConnection(GetConnectionString()))
                return connection.QuerySingleOrDefault<SetHeaderDto>(sql, new { SetId = setId });
        }

        /// <summary>
        /// Walks the RenewalOfSetId chain forward from <paramref name="setId"/> and returns
        /// the leaf (latest) SetId. Returns <paramref name="setId"/> itself if it has no children.
        /// </summary>
        public int GetLatestSetIdInChain(int setId)
        {
            const string sql = @"
                WITH Chain AS (
                    SELECT SetId FROM dbo.[Set] WHERE SetId = @SetId
                    UNION ALL
                    SELECT s.SetId FROM dbo.[Set] s
                    INNER JOIN Chain c ON s.RenewalOfSetId = c.SetId
                )
                SELECT TOP 1 SetId FROM Chain ORDER BY SetId DESC";
            using (var conn = new SqlConnection(GetConnectionString()))
                return conn.QuerySingleOrDefault<int>(sql, new { SetId = setId });
        }

        /// <summary>
        /// Best-effort preview of the SetCode the next-created dbo.[Set] row would get (the
        /// computed column is 'SET-' + SetId zero-padded to 4 digits — see
        /// Migration script for dbo.[Set].SetCode). Purely informational: another insert
        /// happening between this call and the actual renewal could make it off by one.
        /// </summary>
        public string PreviewNextSetCode()
        {
            const string sql = "SELECT IDENT_CURRENT('Set') + 1";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                decimal nextId = connection.ExecuteScalar<decimal>(sql);
                return $"SET-{(long)nextId:D4}";
            }
        }

        /// <summary>
        /// Updates DocumentNumber, ReferenceNumber, and DispatchDate (= DocumentDate) on dbo.[Set].
        /// </summary>
        public void UpdateSetHeader(int setId, string documentNumber, string referenceNumber, DateTime? documentDate)
        {
            const string sql = @"
                UPDATE dbo.[Set]
                SET DocumentNumber  = @DocumentNumber,
                    ReferenceNumber = @ReferenceNumber,
                    DispatchDate    = @DocumentDate
                WHERE SetId = @SetId";
            using (var connection = new SqlConnection(GetConnectionString()))
                connection.Execute(sql, new
                {
                    SetId          = setId,
                    DocumentNumber  = (object)documentNumber  ?? DBNull.Value,
                    ReferenceNumber = (object)referenceNumber ?? DBNull.Value,
                    DocumentDate    = (object)documentDate    ?? DBNull.Value
                });
        }

        /// <summary>
        /// Runs auto-archive process for renewals On Hold > 1 year
        ///
        /// ⚠️ IMPORTANT: This method is currently DISABLED for automatic execution.
        /// The SQL Server Agent job that would call this daily has been commented out
        /// in RenewalsMigration_FIXED.sql (lines 484-518).
        ///
        /// This is TEMPORARY to allow expired items to remain visible for testing/validation.
        /// Auto-archiving should be an explicit user action, not a time-based side effect.
        ///
        /// Only call this manually from developer tools or by explicit user action.
        /// </summary>
        public int AutoArchiveExpiredRenewals()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var result = connection.QuerySingleOrDefault<dynamic>(
                    "sp_AutoArchiveExpiredRenewals",
                    commandType: System.Data.CommandType.StoredProcedure
                );

                return result?.ArchivedCount ?? 0;
            }
        }

        // ── Moved from ViewRenewalDetailPage.cs (WPF workspace conversion) ───────────
        // These were previously private ad-hoc-SQL helpers on the WinForms page.
        // Behavior and SQL are unchanged; only the home moved.

        public sealed class InvoiceFinancialSnapshot
        {
            public decimal Subtotal { get; set; }
            public decimal VatAmount { get; set; }
            public decimal WhtAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal TotalAmountDue { get; set; }
        }

        public sealed class SetDateRange
        {
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
        }

        /// <summary>
        /// Loads basic item data for first-time renewals (no renewal history exists yet).
        /// Mirrors GetRenewalDetailsByItemId_FallbackQuery but optimized for items from the
        /// Warranty page that may not be in a Set yet.
        /// </summary>
        public RenewalDetailDto LoadItemDataWithoutRenewal(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
            {
                connection.Open();

                const string query = @"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    i.Description,
    i.ItemType,
    i.SerialNumber,
    i.ModelNumber,
    i.LicenseNumber,
    i.Amount,
    i.StartDate,
    i.EndDate,
    i.DateCreated,
    i.DateModified,
    i.Category,
    i.Remarks AS ItemRemarks,
    i.Active,

    ic.Name AS CategoryName,

    i.VendorId AS VendorId,
    v.VendorName AS VendorName,
    v.Address AS VendorAddress,
    v.TIN AS VendorTIN,

    cond.ConditionName AS ConditionName,

    CASE
        WHEN i.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
    END AS DaysUntilExpiry,

    CASE
        WHEN i.EndDate IS NULL THEN 'No Expiry Date'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) < 0 THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    u.Name AS CreatedByUsername,

    NULL AS CurrentRenewalStatus,

    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,
    s.Site AS SiteName

FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.Condition cond ON i.ConditionID = cond.ConditionID
LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
LEFT JOIN dbo.SetItem si ON i.ItemId = si.ItemId
LEFT JOIN dbo.[Set] s ON si.SetId = s.SetId AND s.IsInvoice = 1
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
WHERE i.ItemId = @ItemId";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new RenewalDetailDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? null : reader.GetString(reader.GetOrdinal("ItemName")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType")) ? null : reader.GetString(reader.GetOrdinal("ItemType")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            LicenseNumber = reader.IsDBNull(reader.GetOrdinal("LicenseNumber")) ? null : reader.GetString(reader.GetOrdinal("LicenseNumber")),
                            Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Amount")),
                            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            DateCreated = reader.IsDBNull(reader.GetOrdinal("DateCreated")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            DateModified = reader.IsDBNull(reader.GetOrdinal("DateModified")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateModified")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            ItemRemarks = reader.IsDBNull(reader.GetOrdinal("ItemRemarks")) ? null : reader.GetString(reader.GetOrdinal("ItemRemarks")),
                            Active = reader.GetBoolean(reader.GetOrdinal("Active")),
                            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
                            VendorId = reader.IsDBNull(reader.GetOrdinal("VendorId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("VendorId")),
                            VendorName = reader.IsDBNull(reader.GetOrdinal("VendorName")) ? null : reader.GetString(reader.GetOrdinal("VendorName")),
                            VendorAddress = reader.IsDBNull(reader.GetOrdinal("VendorAddress")) ? null : reader.GetString(reader.GetOrdinal("VendorAddress")),
                            VendorTIN = reader.IsDBNull(reader.GetOrdinal("VendorTIN")) ? null : reader.GetString(reader.GetOrdinal("VendorTIN")),
                            ConditionName = reader.IsDBNull(reader.GetOrdinal("ConditionName")) ? null : reader.GetString(reader.GetOrdinal("ConditionName")),
                            DaysUntilExpiry = reader.IsDBNull(reader.GetOrdinal("DaysUntilExpiry")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DaysUntilExpiry")),
                            ExpiryStatus = reader.IsDBNull(reader.GetOrdinal("ExpiryStatus")) ? null : reader.GetString(reader.GetOrdinal("ExpiryStatus")),
                            CreatedByUsername = reader.IsDBNull(reader.GetOrdinal("CreatedByUsername")) ? null : reader.GetString(reader.GetOrdinal("CreatedByUsername")),
                            CurrentRenewalStatus = reader.IsDBNull(reader.GetOrdinal("CurrentRenewalStatus")) ? null : reader.GetString(reader.GetOrdinal("CurrentRenewalStatus")),
                            Subtotal = reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                            VatAmount = reader.GetDecimal(reader.GetOrdinal("VatAmount")),
                            WhtAmount = reader.GetDecimal(reader.GetOrdinal("WhtAmount")),
                            DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            TotalAmountDue = reader.GetDecimal(reader.GetOrdinal("TotalAmountDue")),
                            CurrentBranchId = reader.IsDBNull(reader.GetOrdinal("CurrentBranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentBranchId")),
                            CurrentBranchName = reader.IsDBNull(reader.GetOrdinal("CurrentBranchName")) ? null : reader.GetString(reader.GetOrdinal("CurrentBranchName")),
                            CurrentDepartmentId = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentDepartmentId")),
                            CurrentDepartmentName = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentName")) ? null : reader.GetString(reader.GetOrdinal("CurrentDepartmentName")),
                            SiteName = reader.IsDBNull(reader.GetOrdinal("SiteName")) ? null : reader.GetString(reader.GetOrdinal("SiteName"))
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Priority: use the same invoice-set financial fields shown in ViewInvoiceDetailPage.
        /// </summary>
        public InvoiceFinancialSnapshot GetLatestInvoiceFinancialsByItemId(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
            {
                connection.Open();

                const string sql = @"
SELECT TOP 1
    s.Subtotal, s.VatAmount, s.WhtAmount, s.DiscountAmount, s.TotalAmountDue
FROM dbo.SetItem si
INNER JOIN dbo.[Set] s ON si.SetId = s.SetId AND s.IsInvoice = 1
WHERE si.ItemId = @ItemId
ORDER BY s.SetId DESC";

                using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new InvoiceFinancialSnapshot
                        {
                            Subtotal = reader.IsDBNull(reader.GetOrdinal("Subtotal")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                            VatAmount = reader.IsDBNull(reader.GetOrdinal("VatAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("VatAmount")),
                            WhtAmount = reader.IsDBNull(reader.GetOrdinal("WhtAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("WhtAmount")),
                            DiscountAmount = reader.IsDBNull(reader.GetOrdinal("DiscountAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            TotalAmountDue = reader.IsDBNull(reader.GetOrdinal("TotalAmountDue")) ? 0m : reader.GetDecimal(reader.GetOrdinal("TotalAmountDue"))
                        };
                    }
                }
            }
        }

        public decimal? GetLatestRenewalAmountByItemId(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
            {
                connection.Open();

                const string sql = @"
SELECT TOP 1 r.RenewalAmount
FROM dbo.Renewals r
WHERE r.ItemId = @ItemId
  AND r.RenewalAmount IS NOT NULL
ORDER BY r.CreatedAt DESC";

                using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    var result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return null;

                    return Convert.ToDecimal(result);
                }
            }
        }

        public string LoadPartNumberFromRenewals(int itemId)
        {
            try
            {
                using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    const string sql = @"
                        SELECT TOP 1 PartNumber
                        FROM dbo.Renewals
                        WHERE ItemId = @ItemId
                          AND IsArchived = 0";
                    using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@ItemId", itemId);
                        var result = command.ExecuteScalar();
                        return (result == null || result == DBNull.Value) ? null : result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RenewalDetail] Failed to load PartNumber: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the first ItemId from a SetId (for renewal sets).
        /// </summary>
        public int GetItemIdFromSet(int setId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
            {
                connection.Open();
                var query = @"
                    SELECT TOP 1 ItemId
                    FROM dbo.SetItem
                    WHERE SetId = @SetId
                    ORDER BY ItemCode";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    var result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        public SetDateRange GetSetDateRangeIfSoftwareOrService(int setId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
            {
                connection.Open();

                var query = @"
                    SELECT StartDate, EndDate
                    FROM dbo.[Set]
                    WHERE SetId = @SetId
                      AND IsInvoice = 1";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new SetDateRange
                        {
                            StartDate = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0),
                            EndDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1)
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Gets the first valid UserId from the User table as a fallback.
        /// </summary>
        public int GetFirstValidUserId()
        {
            try
            {
                using (var connection = new System.Data.SqlClient.SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    var query = "SELECT TOP 1 UserId FROM dbo.[User] ORDER BY UserId";

                    using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// New capability (WPF workspace conversion): persists a manual correction to the
        /// Renewal Details tab's Start/End Date fields, which were previously display-only.
        /// </summary>
        public void UpdateItemDates(int itemId, DateTime startDate, DateTime endDate)
        {
            const string sql = @"
                UPDATE dbo.Item
                SET StartDate = @StartDate,
                    EndDate   = @EndDate
                WHERE ItemId = @ItemId";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        connection.Execute(sql, new { ItemId = itemId, StartDate = startDate, EndDate = endDate }, transaction);

                        string serial = null;
                        using (var lookup = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", connection, transaction))
                        {
                            lookup.Parameters.AddWithValue("@ItemId", itemId);
                            var result = lookup.ExecuteScalar();
                            serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                        }
                        ItemAuditTrailWriter.TryLog(connection, transaction, new ItemAuditTrailDto
                        {
                            ItemId = itemId,
                            SerialNumber = serial,
                            Action = "Item Renewal Dates Updated",
                            ActionTime = DateTime.Now,
                            Status = "Completed",
                            ReferenceType = "Item",
                            ReferenceId = itemId,
                            Notes = $"Renewal dates corrected: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.",
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
        }
    }
}
