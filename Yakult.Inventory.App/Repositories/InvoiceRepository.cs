using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    public class InvoiceRepository
    {
        private readonly string _connectionString;

        public InvoiceRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Get all invoices using the view. Optional Advanced Filters parameters (mirrors the
        // View Items page's Set-linked filters) — base entity is dbo.[Set] s, so SetCode/
        // DocumentNumber/Company/Department/Branch are matched directly against s/joined
        // lookup tables; Reference Code and Parent Tag live on dbo.SetItem (many rows per
        // Set) so they're matched via EXISTS; ReqId is a native nullable FK column on s.
        public async Task<DataTable> GetAllInvoicesAsync(
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
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var whereClauses = new List<string>
                {
                    // Was: WHERE v.Status IN ('Yes', 'Draft'), a whitelist that only meant to hide
                    // voided ('No') invoices but also silently dropped Sets whose Status was never
                    // set (NULL), e.g. Sets created outside the invoice-creation dialog, such as
                    // renewal-chain roots. Only exclude the explicit 'No' status instead.
                    "(v.Status IS NULL OR v.Status <> 'No')",
                    "arch.EntityId IS NULL"
                };

                if (!string.IsNullOrWhiteSpace(setCodeFilter)) whereClauses.Add("v.SetCode LIKE @SetCode");
                if (!string.IsNullOrWhiteSpace(documentNumberFilter)) whereClauses.Add("v.InvoiceNumber LIKE @DocumentNumber");
                if (!string.IsNullOrWhiteSpace(companyFilter)) whereClauses.Add("v.CompanyName LIKE @Company");
                if (!string.IsNullOrWhiteSpace(departmentFilter)) whereClauses.Add("d.Name LIKE @Department");
                if (!string.IsNullOrWhiteSpace(branchFilter)) whereClauses.Add("b.Name LIKE @Branch");
                if (!string.IsNullOrWhiteSpace(employeeFilter)) whereClauses.Add("emp.Name LIKE @Employee");
                if (!string.IsNullOrWhiteSpace(reqIdFilter)) whereClauses.Add("CAST(s.ReqId AS NVARCHAR(20)) LIKE @ReqId");

                // Reference Code / Parent Tag: a Set can have many dbo.SetItem rows, so match via
                // EXISTS (a Set qualifies if ANY of its SetItem rows satisfies both provided conditions).
                var setItemConditions = new List<string>();
                if (!string.IsNullOrWhiteSpace(referenceCodeFilter)) setItemConditions.Add("si.ReferenceCode LIKE @ReferenceCode");
                if (!string.IsNullOrWhiteSpace(parentTagFilter)) setItemConditions.Add("ptg.Label LIKE @ParentTag");
                if (setItemConditions.Count > 0)
                {
                    whereClauses.Add($@"EXISTS (
    SELECT 1 FROM dbo.SetItem si
    LEFT JOIN dbo.SetItemParentTagGroup ptg ON ptg.ParentTagGroupId = si.ParentTagGroupId
    WHERE si.SetId = s.SetId AND {string.Join(" AND ", setItemConditions)}
)");
                }

                // Use the vw_Invoices view we created
                var query = $@"
                    SELECT
                        v.SetId,
                        v.SetCode,
                        v.SetType,
                        v.Status,
                        v.InvoiceDate,
                        v.PreparedByName AS CreatedByName,
                        v.InvoiceNumber AS DocumentNumber,
                        v.PONumber AS ReferenceNumber,
                        v.Site,
                        v.StartDate,
                        v.EndDate,
                        v.Subtotal,
                        v.VatAmount,
                        v.WhtAmount,
                        v.DiscountAmount,
                        v.TotalAmountDue,
                        v.ComId,
                        v.CompanyName,
                        v.CompanyDescription,
                        s.DistributorId,
                        dist.Name AS DistributorName,
                        v.Remarks,
                        s.CreatedAt,
                        v.DaysUntilExpiry,
                        v.ExpiryStatus
                    FROM vw_Invoices v
                    INNER JOIN dbo.[Set] s ON v.SetId = s.SetId
                    LEFT JOIN dbo.Distributor dist ON dist.DistributorId = s.DistributorId
                    LEFT JOIN dbo.Branch b ON b.BranchId = s.CurrentBranchId
                    LEFT JOIN dbo.Department d ON d.DeptId = s.CurrentDepartmentId
                    LEFT JOIN dbo.Employee emp ON emp.EmpId = s.ReceivedById
                    LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Set' AND arch.EntityId = s.SetId AND arch.IsArchived = 1
                    WHERE {string.Join(" AND ", whereClauses)}
                    ORDER BY v.InvoiceDate DESC";

                using (var command = new SqlCommand(query, connection) { CommandTimeout = 120 })
                {
                    if (!string.IsNullOrWhiteSpace(setCodeFilter)) command.Parameters.AddWithValue("@SetCode", $"%{setCodeFilter}%");
                    if (!string.IsNullOrWhiteSpace(documentNumberFilter)) command.Parameters.AddWithValue("@DocumentNumber", $"%{documentNumberFilter}%");
                    if (!string.IsNullOrWhiteSpace(companyFilter)) command.Parameters.AddWithValue("@Company", $"%{companyFilter}%");
                    if (!string.IsNullOrWhiteSpace(departmentFilter)) command.Parameters.AddWithValue("@Department", $"%{departmentFilter}%");
                    if (!string.IsNullOrWhiteSpace(branchFilter)) command.Parameters.AddWithValue("@Branch", $"%{branchFilter}%");
                    if (!string.IsNullOrWhiteSpace(employeeFilter)) command.Parameters.AddWithValue("@Employee", $"%{employeeFilter}%");
                    if (!string.IsNullOrWhiteSpace(referenceCodeFilter)) command.Parameters.AddWithValue("@ReferenceCode", $"%{referenceCodeFilter}%");
                    if (!string.IsNullOrWhiteSpace(parentTagFilter)) command.Parameters.AddWithValue("@ParentTag", $"%{parentTagFilter}%");
                    if (!string.IsNullOrWhiteSpace(reqIdFilter)) command.Parameters.AddWithValue("@ReqId", $"%{reqIdFilter}%");

                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        // Get invoice by SetId
        public async Task<DataRow> GetInvoiceByIdAsync(int setId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Read directly from dbo.Set so software/service sets can be opened as invoices
                var query = @"
                    SELECT
                        s.SetId,
                        s.SetCode,
                        s.SetType,
                        s.Status,
                        s.DispatchDate AS InvoiceDate,
                        u.Name AS CreatedByName,
                        s.DocumentNumber,
                        s.ReferenceNumber,
                        s.Site,
                        s.CurrentBranchId,
                        b.Name AS CurrentBranchName,
                        s.CurrentDepartmentId,
                        d.Name AS CurrentDepartmentName,
                        siteCo.ComId AS SiteCompanyId,
                        siteCo.Name AS SiteCompanyName,
                        s.StartDate,
                        s.EndDate,
                        s.Subtotal,
                        s.VatAmount,
                        s.WhtAmount,
                        s.DiscountAmount,
                        s.TotalAmountDue,
                        s.ComId,
                        c.Name AS CompanyName,
                        c.Description AS CompanyDescription,
                        s.DistributorId,
                        dist.Name AS DistributorName,
                        s.Remarks,
                        -- Employee-level detail: the stored invoice requester (dbo.[Set].
                        -- InvoiceRequesterEmpId), falling back to the originating Request's
                        -- employee when it hasn't been set. NULL only when neither exists.
                        s.InvoiceRequesterEmpId,
                        COALESCE(s.InvoiceRequesterEmpId, rq.EmpId) AS RequestedByEmpId,
                        COALESCE(invEmp.Name, reqEmp.Name) AS RequestedByName,
                        COALESCE(invEmp.EmployeeNumber, reqEmp.EmployeeNumber) AS RequestedByNumber,
                        COALESCE(invEmp.Position, reqEmp.Position) AS RequestedByPosition,
                        CAST(NULL AS INT) AS DaysUntilExpiry,
                        CAST(NULL AS NVARCHAR(50)) AS ExpiryStatus,
                        s.CreatedBy
                    FROM dbo.[Set] s
                    LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                    LEFT JOIN dbo.Company c ON s.ComId = c.ComId
                    LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
                    LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
                    LEFT JOIN dbo.Distributor dist ON s.DistributorId = dist.DistributorId
                    LEFT JOIN dbo.Request  rq     ON rq.ReqId = s.ReqId
                    LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = rq.EmpId
                    LEFT JOIN dbo.Employee invEmp ON invEmp.EmpId = s.InvoiceRequesterEmpId
                    OUTER APPLY (
                        SELECT TOP 1 co.ComId, co.Name
                        FROM (
                            SELECT bdc.CompanyID, 1 AS Priority
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.BranchID = b.BranchId
                            UNION ALL
                            SELECT bdc.CompanyID, 2 AS Priority
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.DepartmentID = d.DeptId
                        ) src
                        JOIN dbo.Company co ON src.CompanyID = co.ComId
                        ORDER BY src.Priority, src.CompanyID
                    ) siteCo
                    WHERE s.SetId = @SetId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable.Rows.Count > 0 ? dataTable.Rows[0] : null;
                    }
                }
            }
        }

        // Get items in an invoice - only SetItem columns as requested
        public async Task<DataTable> GetInvoiceItemsAsync(int setId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Fetch only the requested columns from SetItem and Item
                var query = @"
                    SELECT
                        si.SetItemId,
                        si.ItemId,
                        si.ItemCode,
                        i.Name AS ItemName,
                        i.ModelNumber,
                        i.SerialNumber,
                        rn.PartNumber,
                        si.SetId,
                        s.SetCode,
                        si.UnitOfMeasure,
                        ISNULL(i.StockOnHand, si.Quantity) AS Quantity,
                        -- FIX: Some legacy Service/Software invoice lines were saved with UnitPrice/Amount = 0 even though Item.Amount is set.
                        -- Prefer the stored SetItem values, but (for Service/Software invoice sets) fall back to Item.Amount when missing/zero.
                        CASE
                            WHEN s.SetType IN ('Software/License', 'Service', 'Services')
                                 AND ISNULL(si.UnitPrice, 0) = 0
                                 AND ISNULL(i.Amount, 0) > 0 THEN i.Amount
                            ELSE ISNULL(si.UnitPrice, 0)
                        END AS UnitPrice,
                        CASE
                            WHEN s.SetType IN ('Software/License', 'Service', 'Services')
                                 AND ISNULL(si.Amount, 0) = 0
                                 AND (
                                        (ISNULL(si.UnitPrice, 0) = 0 AND ISNULL(i.Amount, 0) > 0)
                                        OR ISNULL(si.UnitPrice, 0) > 0
                                     )
                                 THEN ISNULL(si.Quantity, 0) * CASE
                                     WHEN ISNULL(si.UnitPrice, 0) = 0 AND ISNULL(i.Amount, 0) > 0 THEN i.Amount
                                     ELSE ISNULL(si.UnitPrice, 0)
                                 END
                            ELSE ISNULL(si.Amount, 0)
                        END AS Amount,
                        CASE
                            -- Back-compat: older inserts used GETDATE() for LineStartDate; treat that as a default
                            -- and prefer the invoice header StartDate unless the line was explicitly edited later.
                            WHEN si.LineStartDate IS NULL THEN s.StartDate
                            WHEN si.CreatedAt IS NOT NULL AND ABS(DATEDIFF(SECOND, si.LineStartDate, si.CreatedAt)) <= 5 THEN s.StartDate
                            ELSE si.LineStartDate
                        END AS LineStartDate,
                        COALESCE(si.LineEndDate, s.EndDate) AS LineEndDate,
                        si.CreatedAt,
                        si.SubType,
                        si.ReferenceCode,
                        si.ParentTagGroupId,
                        ptg.Label AS ParentTag,
                        ic.Name AS Category
                    FROM dbo.SetItem si
                    INNER JOIN dbo.[Set] s ON si.SetId = s.SetId
                    LEFT  JOIN dbo.Item i  ON si.ItemId = i.ItemId
                    LEFT  JOIN dbo.SetItemParentTagGroup ptg ON si.ParentTagGroupId = ptg.ParentTagGroupId
                    LEFT  JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                    OUTER APPLY (
                        SELECT TOP (1) rn2.PartNumber
                        FROM dbo.Renewals rn2
                        WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
                        ORDER BY rn2.RenewalId DESC
                    ) rn
                    WHERE si.SetId = @SetId
                    ORDER BY si.ItemCode";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        /// <summary>
        /// Fallback for a Request-based invoice Set (dispatched via dbo.Request, then
        /// flagged as an invoice in place by ServiceSetRepository.MarkRequestSetAsInvoiceAsync
        /// — no dbo.SetItem rows exist for it). Returns the exact same column shape as
        /// GetInvoiceItemsAsync so ViewInvoiceDetailPage's grid setup code works unchanged.
        /// SetItemId here is actually ReqId — safe because this DataTable only ever feeds a
        /// read-only display grid, never a Set/Request mutation.
        /// </summary>
        public async Task<DataTable> GetRequestItemsAsInvoiceItemsAsync(int setId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT
                        r.ReqId AS SetItemId,
                        r.ItemId,
                        ISNULL(i.ModelNumber, CAST(i.ItemId AS NVARCHAR(20))) AS ItemCode,
                        i.Name AS ItemName,
                        i.ModelNumber,
                        i.SerialNumber,
                        rn.PartNumber,
                        r.SetId,
                        s.SetCode,
                        i.UnitOfMeasure,
                        r.Quantity,
                        r.UnitPrice,
                        (r.Quantity * r.UnitPrice) AS Amount,
                        s.StartDate AS LineStartDate,
                        s.EndDate AS LineEndDate,
                        r.DateCreated AS CreatedAt,
                        CAST(NULL AS INT) AS ParentTagGroupId,
                        CAST(NULL AS NVARCHAR(200)) AS ParentTag,
                        ic.Name AS Category
                    FROM dbo.Request r
                    INNER JOIN dbo.[Set] s ON r.SetId = s.SetId
                    LEFT JOIN dbo.Item i   ON r.ItemId = i.ItemId
                    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
                    OUTER APPLY (
                        SELECT TOP (1) rn2.PartNumber
                        FROM dbo.Renewals rn2
                        WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
                        ORDER BY rn2.RenewalId DESC
                    ) rn
                    WHERE r.SetId = @SetId
                    ORDER BY r.DateCreated DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        public sealed class InvoiceItemLineUpdate
        {
            public int SetItemId { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }
            public string UnitOfMeasure { get; set; }
            public DateTime? LineStartDate { get; set; }
            public DateTime? LineEndDate { get; set; }
            public string SubType { get; set; }
        }

        public async Task<bool> UpdateInvoiceItemsAsync(int setId, List<InvoiceItemLineUpdate> updates)
        {
            if (updates == null)
                throw new ArgumentNullException(nameof(updates));

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        const string sql = @"
UPDATE dbo.SetItem
SET
    Quantity = @Quantity,
    UnitPrice = @UnitPrice,
    Amount = @Amount,
    UnitOfMeasure = @UnitOfMeasure,
    LineStartDate = @LineStartDate,
    LineEndDate = @LineEndDate,
    SubType = @SubType
WHERE SetItemId = @SetItemId
  AND SetId = @SetId;";

                        foreach (var u in updates.Where(x => x != null))
                        {
                            if (u.SetItemId <= 0)
                                continue;

                            using (var cmd = new SqlCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetItemId", u.SetItemId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@Quantity", u.Quantity);
                                cmd.Parameters.AddWithValue("@UnitPrice", u.UnitPrice);
                                cmd.Parameters.AddWithValue("@Amount", u.Amount);
                                cmd.Parameters.AddWithValue("@UnitOfMeasure", (object)u.UnitOfMeasure ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@LineStartDate", (object)u.LineStartDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@LineEndDate", (object)u.LineEndDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@SubType", (object)u.SubType ?? DBNull.Value);

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // Moves already-invoiced SetItem rows into a Sub-Type group after the fact — for invoices
        // created before subtype grouping existed, where the items logically belong to a Contract/
        // Subscription/License/Services group but were never tagged. Mirrors AddInvoiceItemsAsync's
        // group resolution (same SetItemSubTypeGroupHelper.FindOrCreateGroupId call), except it
        // updates existing rows instead of inserting new ones.
        public async Task<int> ConvertItemsToSubTypeGroupAsync(
            int setId,
            List<int> setItemIds,
            string subType,
            string referenceCode,
            DateTime? beginDate,
            DateTime? endDate,
            int postedBy)
        {
            ItemSubTypeCatalog.ValidateSubType(subType);
            if (setItemIds == null || setItemIds.Count == 0)
                return 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int? groupId = SetItemSubTypeGroupHelper.FindOrCreateGroupId(
                            connection, transaction, setId, subType, referenceCode, beginDate, endDate, postedBy);

                        const string sql = @"
UPDATE dbo.SetItem
SET
    SubType = @SubType,
    ReferenceCode = @ReferenceCode,
    BeginDate = @BeginDate,
    EndDate = @EndDate,
    GroupId = @GroupId
WHERE SetItemId = @SetItemId
  AND SetId = @SetId;";

                        int updated = 0;
                        foreach (var setItemId in setItemIds.Where(id => id > 0).Distinct())
                        {
                            using (var cmd = new SqlCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetItemId", setItemId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@SubType", subType);
                                cmd.Parameters.AddWithValue("@ReferenceCode", string.IsNullOrWhiteSpace(referenceCode) ? (object)DBNull.Value : referenceCode);
                                cmd.Parameters.AddWithValue("@BeginDate", (object)beginDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@EndDate", (object)endDate ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@GroupId", (object)groupId ?? DBNull.Value);

                                updated += await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return updated;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // Moves already-invoiced SetItem rows into a Parent Tag group after the fact — for
        // items that logically belong together (e.g. all "Cisco" line items) but weren't tagged
        // when the invoice was built. Independent of, and orthogonal to, Sub-Type grouping above
        // — an item can belong to both at once. Mirrors ConvertItemsToSubTypeGroupAsync, but
        // simpler: Parent Tag is a free-text label only, no date range or catalog validation.
        public async Task<int> ConvertItemsToParentTagGroupAsync(
            int setId,
            List<int> setItemIds,
            string label,
            int postedBy)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException("Parent Tag label is required.");
            if (setItemIds == null || setItemIds.Count == 0)
                return 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int? groupId = SetItemParentTagGroupHelper.FindOrCreateGroupId(
                            connection, transaction, setId, label, postedBy);

                        const string sql = @"
UPDATE dbo.SetItem
SET ParentTagGroupId = @ParentTagGroupId
WHERE SetItemId = @SetItemId
  AND SetId = @SetId;";

                        int updated = 0;
                        foreach (var setItemId in setItemIds.Where(id => id > 0).Distinct())
                        {
                            using (var cmd = new SqlCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetItemId", setItemId);
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@ParentTagGroupId", (object)groupId ?? DBNull.Value);

                                updated += await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return updated;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // Un-tags every SetItem in a Parent Tag group (items stay on the invoice, just
        // ungrouped) and removes the now-empty group row. Unlike Sub-Type's
        // InvoicePreparationRepository.RemoveGroupFromInvoice, this has no financial semantics
        // to unwind — Parent Tag is a read-only display/rollup grouping only.
        public async Task RemoveParentTagGroupFromInvoice(int parentTagGroupId, int removedBy)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        const string clearSql = @"
UPDATE dbo.SetItem SET ParentTagGroupId = NULL WHERE ParentTagGroupId = @ParentTagGroupId;";
                        using (var cmd = new SqlCommand(clearSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ParentTagGroupId", parentTagGroupId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        const string deleteSql = @"
DELETE FROM dbo.SetItemParentTagGroup WHERE ParentTagGroupId = @ParentTagGroupId;";
                        using (var cmd = new SqlCommand(deleteSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ParentTagGroupId", parentTagGroupId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        /// <summary>Distinct Parent Tag labels already used across invoices, for autocomplete
        /// suggestions in InvoiceBuilderDialog and ConvertToParentTagGroupDialog.</summary>
        public async Task<List<string>> GetDistinctParentTagLabelsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string sql = "SELECT DISTINCT Label FROM dbo.SetItemParentTagGroup ORDER BY Label";
                using (var cmd = new SqlCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var labels = new List<string>();
                    while (await reader.ReadAsync())
                        labels.Add(reader.GetString(0));
                    return labels;
                }
            }
        }

        public async Task<int> UpdateInvoiceItemDatesForHeaderChangeAsync(
            int setId,
            DateTime? oldStartDate,
            DateTime? oldEndDate,
            DateTime newStartDate,
            DateTime newEndDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
UPDATE dbo.SetItem
SET
    LineStartDate = @NewStartDate,
    LineEndDate = @NewEndDate
WHERE SetId = @SetId
  AND (
        @OldStartDate IS NULL
        OR LineStartDate IS NULL
        OR CONVERT(date, LineStartDate) = CONVERT(date, @OldStartDate)
      )
  AND (
        @OldEndDate IS NULL
        OR LineEndDate IS NULL
        OR CONVERT(date, LineEndDate) = CONVERT(date, @OldEndDate)
      );";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    cmd.Parameters.AddWithValue("@OldStartDate", (object)oldStartDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OldEndDate", (object)oldEndDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NewStartDate", newStartDate);
                    cmd.Parameters.AddWithValue("@NewEndDate", newEndDate);

                    return await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> OverrideInvoiceItemDatesFromHeaderAsync(int setId, DateTime newStartDate, DateTime newEndDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
UPDATE dbo.SetItem
SET
    LineStartDate = @NewStartDate,
    LineEndDate = @NewEndDate
WHERE SetId = @SetId;";

                using (var cmd = new SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    cmd.Parameters.AddWithValue("@NewStartDate", newStartDate);
                    cmd.Parameters.AddWithValue("@NewEndDate", newEndDate);

                    return await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // Create new invoice
        public async Task<int> CreateInvoiceAsync(
            string documentNumber,
            string referenceNumber,
            string site,
            DateTime startDate,
            DateTime endDate,
            decimal subtotal,
            decimal vatAmount,
            decimal whtAmount,
            decimal discountAmount,
            decimal totalAmountDue,
            int? companyId,
            string status,
            string remarks,
            int createdBy)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // CRITICAL: IsInvoice=1 marks this as an invoice (authoritative flag)
                        var query = @"
                            INSERT INTO dbo.[Set]
                            (DocumentNumber, ReferenceNumber, Site, StartDate, EndDate,
                             Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                             ComId, Status, Remarks, CreatedBy, CreatedAt, ReqId,
                             QRToken, QRImagePath, QRData, DispatchDate, IsInvoice)
                            VALUES
                            (@DocumentNumber, @ReferenceNumber, @Site, @StartDate, @EndDate,
                             @Subtotal, @VatAmount, @WhtAmount, @DiscountAmount, @TotalAmountDue,
                             @ComId, @Status, @Remarks, @CreatedBy, GETDATE(), NULL,
                             NULL, NULL, NULL, NULL, 1);

                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int setId;
                        using (var command = new SqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@DocumentNumber", (object)documentNumber ?? DBNull.Value);
                            command.Parameters.AddWithValue("@ReferenceNumber", (object)referenceNumber ?? DBNull.Value);
                            command.Parameters.AddWithValue("@Site", (object)site ?? DBNull.Value);
                            command.Parameters.AddWithValue("@StartDate", startDate);
                            command.Parameters.AddWithValue("@EndDate", endDate);
                            command.Parameters.AddWithValue("@Subtotal", subtotal);
                            command.Parameters.AddWithValue("@VatAmount", vatAmount);
                            command.Parameters.AddWithValue("@WhtAmount", whtAmount);
                            command.Parameters.AddWithValue("@DiscountAmount", discountAmount);
                            command.Parameters.AddWithValue("@TotalAmountDue", totalAmountDue);
                            command.Parameters.AddWithValue("@ComId", (object)companyId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@Status", (object)status ?? DBNull.Value);
                            command.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                            command.Parameters.AddWithValue("@CreatedBy", createdBy);

                            var result = await command.ExecuteScalarAsync();
                            setId = Convert.ToInt32(result);
                        }

                        transaction.Commit();
                        ActivityLogger.Log(ActivityLogger.Actions.Create, "Invoice", setId, $"Invoice created (Set #{setId})");
                        return setId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Add item to invoice (creates Inventory entry with SetId)
        public async Task<bool> AddInvoiceItemAsync(
            int setId,
            int itemId,
            int quantity,
            string description,
            int postedBy)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Default line contract dates to the invoice header dates (StartDate/EndDate)
                DateTime? headerStartDate = null;
                DateTime? headerEndDate = null;
                const string headerDatesSql = @"
SELECT StartDate, EndDate
FROM dbo.[Set]
WHERE SetId = @SetId;";

                using (var headerCmd = new SqlCommand(headerDatesSql, connection))
                {
                    headerCmd.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = await headerCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            headerStartDate = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                            headerEndDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                        }
                    }
                }

                // CRITICAL: Get item properties for Inventory and SetItem creation
                string itemType = null;
                string modelNumber = null;
                string itemName = null;
                string unitOfMeasure = null;
                decimal unitPrice = 0;
                bool affectsInventory = false;

                var getItemPropsQuery = @"
                    SELECT ItemType, AffectsInventory, ModelNumber, Name, UnitOfMeasure, Amount
                    FROM dbo.Item
                    WHERE ItemId = @ItemId";

                using (var getPropsCmd = new SqlCommand(getItemPropsQuery, connection))
                {
                    getPropsCmd.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = await getPropsCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            itemType = reader.IsDBNull(0) ? null : reader.GetString(0);
                            affectsInventory = !reader.IsDBNull(1) && reader.GetBoolean(1);
                            modelNumber = reader.IsDBNull(2) ? null : reader.GetString(2);
                            itemName = reader.IsDBNull(3) ? null : reader.GetString(3);
                            unitOfMeasure = reader.IsDBNull(4) ? null : reader.GetString(4);
                            unitPrice = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                        }
                    }
                }

                // =========================================================================
                // STEP 1: Insert into dbo.SetItem (ALWAYS, regardless of AffectsInventory)
                // =========================================================================
                // CRITICAL BUSINESS RULE: SetItem is the transactional source of truth
                // Must be populated for ALL items (Hardware, Software, Service)
                // This replicates the cartridge flow pattern

                var setItemQuery = @"
                    INSERT INTO dbo.SetItem (
                        SetId, ItemId, ItemCode, Description,
                        Quantity, UnitOfMeasure, UnitPrice, Amount,
                        LineStartDate, LineEndDate, CreatedBy
                    )
                    VALUES (
                        @SetId, @ItemId, @ItemCode, @Description,
                        @Quantity, @UnitOfMeasure, @UnitPrice, @Amount,
                        @LineStartDate, @LineEndDate, @CreatedBy
                    )";

                using (var setItemCmd = new SqlCommand(setItemQuery, connection))
                {
                    setItemCmd.Parameters.AddWithValue("@SetId", setId);
                    setItemCmd.Parameters.AddWithValue("@ItemId", itemId);
                    setItemCmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(modelNumber) ? (object)DBNull.Value : modelNumber);
                    setItemCmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(itemName) ? (string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description) : itemName);
                    setItemCmd.Parameters.AddWithValue("@Quantity", quantity);
                    setItemCmd.Parameters.AddWithValue("@UnitOfMeasure", string.IsNullOrWhiteSpace(unitOfMeasure) ? (object)DBNull.Value : unitOfMeasure);
                    setItemCmd.Parameters.AddWithValue("@UnitPrice", unitPrice);
                    setItemCmd.Parameters.AddWithValue("@Amount", quantity * unitPrice);
                    setItemCmd.Parameters.AddWithValue("@LineStartDate", (object)headerStartDate ?? DateTime.Now);
                    setItemCmd.Parameters.AddWithValue("@LineEndDate", (object)headerEndDate ?? DBNull.Value);
                    setItemCmd.Parameters.AddWithValue("@CreatedBy", postedBy);

                    await setItemCmd.ExecuteNonQueryAsync();
                }

                // =========================================================================
                // STEP 2: Insert into dbo.Inventory (TRANSACTIONAL INVENTORY)
                // =========================================================================
                // CRITICAL: This is TRANSACTIONAL inventory, not baseline
                // - SetId is REQUIRED (Invoice flow always creates Set first)
                // - Only created when AffectsInventory = true
                // - EntryType = "Positive" (receiving stock via invoice)
                // - Baseline inventory already exists from Item creation
                int rowsAffected = 0;
                if (affectsInventory)
                {
                    // Determine EntryType for TRANSACTIONAL inventory
                    string entryType = Yakult.Inventory.App.Helpers.InventoryHelper.DetermineEntryType(affectsInventory, quantity);

                    var inventoryQuery = @"
                        INSERT INTO dbo.Inventory
                        (ItemId, Quantity, EntryType, DatePosted, PostedBy, SetId, ReqId, Description, Active)
                        VALUES
                        (@ItemId, @Quantity, @EntryType, GETDATE(), @PostedBy, @SetId, NULL, @Description, 1)";

                    using (var command = new SqlCommand(inventoryQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ItemId", itemId);
                        command.Parameters.AddWithValue("@Quantity", quantity);
                        command.Parameters.AddWithValue("@EntryType", entryType);
                        command.Parameters.AddWithValue("@PostedBy", postedBy);
                        command.Parameters.AddWithValue("@SetId", setId);
                        command.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);

                        rowsAffected = await command.ExecuteNonQueryAsync();
                    }
                }

                // =========================================================================
                // STEP 3: Update Set.SetType to match item type
                // =========================================================================
                if (!string.IsNullOrWhiteSpace(itemType))
                {
                    var updateSetTypeQuery = @"
                        UPDATE dbo.[Set]
                        SET SetType = @SetType
                        WHERE SetId = @SetId
                          AND (SetType IS NULL OR SetType = '' OR SetType = 'Invoice')";

                    using (var updateCmd = new SqlCommand(updateSetTypeQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@SetId", setId);
                        updateCmd.Parameters.AddWithValue("@SetType", itemType);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }

                return true; // SetItem always created
            }
        }

        // Add multiple items to an invoice in one round-trip (mirrors AddInvoiceItemAsync, batched in a transaction).
        // subType/referenceCode/beginDate/endDate are optional — when subType is provided, every
        // inserted item is tagged with it and resolved/created into a dbo.SetItemSubTypeGroup row
        // (via the same SetItemSubTypeGroupHelper used by ServiceSetRepository), so items bulk-added
        // this way can be grouped without a separate trip through the Items Page's "Add to Group" flow.
        public async Task<int> AddInvoiceItemsAsync(
            int setId,
            List<(int ItemId, int Quantity, string Description)> items,
            int postedBy,
            string subType = null,
            string referenceCode = null,
            DateTime? groupBeginDate = null,
            DateTime? groupEndDate = null,
            string parentTagLabel = null)
        {
            if (items == null || items.Count == 0)
                return 0;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        DateTime? headerStartDate = null;
                        DateTime? headerEndDate = null;
                        const string headerDatesSql = @"
SELECT StartDate, EndDate
FROM dbo.[Set]
WHERE SetId = @SetId;";

                        using (var headerCmd = new SqlCommand(headerDatesSql, connection, transaction))
                        {
                            headerCmd.Parameters.AddWithValue("@SetId", setId);
                            using (var reader = await headerCmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    headerStartDate = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                                    headerEndDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                                }
                            }
                        }

                        int? groupId = SetItemSubTypeGroupHelper.FindOrCreateGroupId(
                            connection, transaction, setId, subType, referenceCode, groupBeginDate, groupEndDate, postedBy);

                        int? parentTagGroupId = SetItemParentTagGroupHelper.FindOrCreateGroupId(
                            connection, transaction, setId, parentTagLabel, postedBy);

                        int insertedCount = 0;

                        foreach (var item in items)
                        {
                            string itemType = null;
                            string modelNumber = null;
                            string itemName = null;
                            string unitOfMeasure = null;
                            decimal unitPrice = 0;
                            bool affectsInventory = false;

                            const string getItemPropsQuery = @"
                                SELECT ItemType, AffectsInventory, ModelNumber, Name, UnitOfMeasure, Amount
                                FROM dbo.Item
                                WHERE ItemId = @ItemId";

                            using (var getPropsCmd = new SqlCommand(getItemPropsQuery, connection, transaction))
                            {
                                getPropsCmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                                using (var reader = await getPropsCmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        itemType = reader.IsDBNull(0) ? null : reader.GetString(0);
                                        affectsInventory = !reader.IsDBNull(1) && reader.GetBoolean(1);
                                        modelNumber = reader.IsDBNull(2) ? null : reader.GetString(2);
                                        itemName = reader.IsDBNull(3) ? null : reader.GetString(3);
                                        unitOfMeasure = reader.IsDBNull(4) ? null : reader.GetString(4);
                                        unitPrice = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                                    }
                                }
                            }

                            const string setItemQuery = @"
                                INSERT INTO dbo.SetItem (
                                    SetId, ItemId, ItemCode, Description,
                                    Quantity, UnitOfMeasure, UnitPrice, Amount,
                                    LineStartDate, LineEndDate, SubType, ReferenceCode,
                                    BeginDate, EndDate, GroupId, ParentTagGroupId, CreatedBy
                                )
                                VALUES (
                                    @SetId, @ItemId, @ItemCode, @Description,
                                    @Quantity, @UnitOfMeasure, @UnitPrice, @Amount,
                                    @LineStartDate, @LineEndDate, @SubType, @ReferenceCode,
                                    @GroupBeginDate, @GroupEndDate, @GroupId, @ParentTagGroupId, @CreatedBy
                                )";

                            using (var setItemCmd = new SqlCommand(setItemQuery, connection, transaction))
                            {
                                setItemCmd.Parameters.AddWithValue("@SetId", setId);
                                setItemCmd.Parameters.AddWithValue("@ItemId", item.ItemId);
                                setItemCmd.Parameters.AddWithValue("@ItemCode", string.IsNullOrWhiteSpace(modelNumber) ? (object)DBNull.Value : modelNumber);
                                setItemCmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(itemName) ? (string.IsNullOrWhiteSpace(item.Description) ? (object)DBNull.Value : item.Description) : itemName);
                                setItemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                                setItemCmd.Parameters.AddWithValue("@UnitOfMeasure", string.IsNullOrWhiteSpace(unitOfMeasure) ? (object)DBNull.Value : unitOfMeasure);
                                setItemCmd.Parameters.AddWithValue("@UnitPrice", unitPrice);
                                setItemCmd.Parameters.AddWithValue("@Amount", item.Quantity * unitPrice);
                                setItemCmd.Parameters.AddWithValue("@LineStartDate", (object)headerStartDate ?? DateTime.Now);
                                setItemCmd.Parameters.AddWithValue("@LineEndDate", (object)headerEndDate ?? DBNull.Value);
                                setItemCmd.Parameters.AddWithValue("@SubType", string.IsNullOrWhiteSpace(subType) ? (object)DBNull.Value : subType);
                                setItemCmd.Parameters.AddWithValue("@ReferenceCode", string.IsNullOrWhiteSpace(referenceCode) ? (object)DBNull.Value : referenceCode);
                                setItemCmd.Parameters.AddWithValue("@GroupBeginDate", (object)groupBeginDate ?? DBNull.Value);
                                setItemCmd.Parameters.AddWithValue("@GroupEndDate", (object)groupEndDate ?? DBNull.Value);
                                setItemCmd.Parameters.AddWithValue("@GroupId", (object)groupId ?? DBNull.Value);
                                setItemCmd.Parameters.AddWithValue("@ParentTagGroupId", (object)parentTagGroupId ?? DBNull.Value);
                                setItemCmd.Parameters.AddWithValue("@CreatedBy", postedBy);

                                await setItemCmd.ExecuteNonQueryAsync();
                            }

                            if (affectsInventory)
                            {
                                string entryType = Yakult.Inventory.App.Helpers.InventoryHelper.DetermineEntryType(affectsInventory, item.Quantity);

                                const string inventoryQuery = @"
                                    INSERT INTO dbo.Inventory
                                    (ItemId, Quantity, EntryType, DatePosted, PostedBy, SetId, ReqId, Description, Active)
                                    VALUES
                                    (@ItemId, @Quantity, @EntryType, GETDATE(), @PostedBy, @SetId, NULL, @Description, 1)";

                                using (var command = new SqlCommand(inventoryQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@EntryType", entryType);
                                    command.Parameters.AddWithValue("@PostedBy", postedBy);
                                    command.Parameters.AddWithValue("@SetId", setId);
                                    command.Parameters.AddWithValue("@Description", (object)item.Description ?? DBNull.Value);

                                    await command.ExecuteNonQueryAsync();
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(itemType))
                            {
                                const string updateSetTypeQuery = @"
                                    UPDATE dbo.[Set]
                                    SET SetType = @SetType
                                    WHERE SetId = @SetId
                                      AND (SetType IS NULL OR SetType = '' OR SetType = 'Invoice')";

                                using (var updateCmd = new SqlCommand(updateSetTypeQuery, connection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@SetId", setId);
                                    updateCmd.Parameters.AddWithValue("@SetType", itemType);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                            }

                            insertedCount++;
                        }

                        transaction.Commit();
                        ActivityLogger.Log(ActivityLogger.Actions.Create, "Invoice", setId, $"{insertedCount} item(s) bulk-added to Invoice (Set #{setId})");
                        return insertedCount;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<bool> DeleteInvoiceSetItemAsync(int setId, int setItemId)
        {
            if (setId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setId));
            if (setItemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setItemId));

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int itemId = 0;
                        int quantity = 0;
                        bool affectsInventory = false;
                        string serialNumber = null;

                        const string selectSql = @"
SELECT TOP (1)
    si.ItemId,
    si.Quantity,
    ISNULL(i.AffectsInventory, 0) AS AffectsInventory,
    i.SerialNumber
FROM dbo.SetItem si
LEFT JOIN dbo.Item i ON si.ItemId = i.ItemId
WHERE si.SetId = @SetId
  AND si.SetItemId = @SetItemId;";

                        using (var cmd = new SqlCommand(selectSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.Parameters.AddWithValue("@SetItemId", setItemId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                    return false;

                                itemId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                if (!reader.IsDBNull(1))
                                {
                                    var qtyDec = Convert.ToDecimal(reader.GetValue(1));
                                    quantity = (int)Math.Round(qtyDec, MidpointRounding.AwayFromZero);
                                }
                                affectsInventory = !reader.IsDBNull(2) && reader.GetBoolean(2);
                                serialNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
                            }
                        }

                        if (affectsInventory && itemId > 0 && quantity != 0)
                        {
                            const string restoreStockSql = @"
UPDATE dbo.Item
SET StockOnHand = StockOnHand + @Quantity
WHERE ItemId = @ItemId;";

                            using (var cmd = new SqlCommand(restoreStockSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                cmd.Parameters.AddWithValue("@Quantity", quantity);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            const string deleteInventorySql = @"
DELETE FROM dbo.Inventory
WHERE SetId = @SetId
  AND ItemId = @ItemId;";

                            using (var cmd = new SqlCommand(deleteInventorySql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@ItemId", itemId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
                            {
                                ItemId = itemId,
                                SerialNumber = serialNumber,
                                Action = "Invoice Line Deleted - Stock Restored",
                                ActionTime = DateTime.Now,
                                Direction = "IN",
                                Status = "Completed",
                                ReferenceType = "Set",
                                ReferenceId = setId,
                                Notes = $"Stock restored for {quantity} unit(s) after invoice line deletion.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });
                        }

                        const string deleteSetItemSql = @"
DELETE FROM dbo.SetItem
WHERE SetId = @SetId
  AND SetItemId = @SetItemId;";

                        int rowsAffected;
                        using (var cmd = new SqlCommand(deleteSetItemSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@SetId", setId);
                            cmd.Parameters.AddWithValue("@SetItemId", setItemId);
                            rowsAffected = await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return rowsAffected > 0;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        // Update invoice
        public async Task<bool> UpdateInvoiceAsync(
            int setId,
            string documentNumber,
            string referenceNumber,
            string site,
            int? currentBranchId,
            int? currentDepartmentId,
            DateTime startDate,
            DateTime endDate,
            decimal subtotal,
            decimal vatAmount,
            decimal whtAmount,
            decimal discountAmount,
            decimal totalAmountDue,
            int? companyId,
            string status,
            string remarks,
            int? distributorId = null,
            int? invoiceRequesterEmpId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                // CRITICAL: IsInvoice=1 marks this as an invoice (authoritative flag)
                var query = @"
                    UPDATE dbo.[Set]
                    SET
                        DocumentNumber = @DocumentNumber,
                        ReferenceNumber = @ReferenceNumber,
                        Site = @Site,
                        CurrentBranchId = @CurrentBranchId,
                        CurrentDepartmentId = @CurrentDepartmentId,
                        StartDate = @StartDate,
                        EndDate = @EndDate,
                        Subtotal = @Subtotal,
                        VatAmount = @VatAmount,
                        WhtAmount = @WhtAmount,
                        DiscountAmount = @DiscountAmount,
                        TotalAmountDue = @TotalAmountDue,
                        ComId = @ComId,
                        DistributorId = @DistributorId,
                        InvoiceRequesterEmpId = @InvoiceRequesterEmpId,
                        Status = @Status,
                        Remarks = @Remarks,
                        IsInvoice = 1
                    WHERE SetId = @SetId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    command.Parameters.AddWithValue("@DocumentNumber", (object)documentNumber ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ReferenceNumber", (object)referenceNumber ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Site", (object)site ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CurrentBranchId", (object)currentBranchId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CurrentDepartmentId", (object)currentDepartmentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@Subtotal", subtotal);
                    command.Parameters.AddWithValue("@VatAmount", vatAmount);
                    command.Parameters.AddWithValue("@WhtAmount", whtAmount);
                    command.Parameters.AddWithValue("@DiscountAmount", discountAmount);
                    command.Parameters.AddWithValue("@TotalAmountDue", totalAmountDue);
                    command.Parameters.AddWithValue("@ComId", (object)companyId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DistributorId", (object)distributorId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@InvoiceRequesterEmpId", (object)invoiceRequesterEmpId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Status", (object)status ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                        ActivityLogger.Log(ActivityLogger.Actions.Update, "Invoice", setId, $"Invoice updated (Set #{setId})");
                    return rowsAffected > 0;
                }
            }
        }

        // Delete invoice
        /// <summary>
        /// Simple delete without restoring stock (deprecated - use DeleteInvoiceAndRestoreStock instead)
        /// </summary>
        public async Task<bool> DeleteInvoiceAsync(int setId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Delete inventory entries first
                        var deleteInventoryQuery = @"
                            DELETE FROM dbo.Inventory
                            WHERE SetId = @SetId";

                        using (var command = new SqlCommand(deleteInventoryQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@SetId", setId);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Delete the set
                        var deleteSetQuery = @"
                            DELETE FROM dbo.[Set]
                            WHERE SetId = @SetId";

                        using (var command = new SqlCommand(deleteSetQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@SetId", setId);
                            var rowsAffected = await command.ExecuteNonQueryAsync();

                            transaction.Commit();
                            if (rowsAffected > 0)
                                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Invoice", setId, $"Invoice deleted (Set #{setId})");
                            return rowsAffected > 0;
                        }
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
        /// Deletes an invoice (Set) and restores stock for all items in its SetItem table
        /// </summary>
        public async Task<bool> DeleteInvoiceAndRestoreStock(int setId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    int itemsRestored = 0;

                    // Step 1: Get all items in this invoice/set to restore stock (only for items that affect inventory)
                    string getSetItemsSql = @"
                        SELECT si.ItemId, si.Quantity, i.AffectsInventory, i.SerialNumber
                        FROM SetItem si
                        INNER JOIN Item i ON si.ItemId = i.ItemId
                        WHERE si.SetId = @SetId";

                    List<(int ItemId, int Quantity, bool AffectsInventory, string SerialNumber)> setItems = new List<(int, int, bool, string)>();

                    using (SqlCommand cmd = new SqlCommand(getSetItemsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int itemId = reader.GetInt32(0);
                                int quantity = reader.GetInt32(1);
                                bool affectsInventory = !reader.IsDBNull(2) && reader.GetBoolean(2);
                                string serialNumber = reader.IsDBNull(3) ? null : reader.GetString(3);
                                setItems.Add((itemId, quantity, affectsInventory, serialNumber));
                            }
                        }
                    }

                    // Step 2: Restore stock back to inventory (only for items that affect inventory)
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

                            ItemAuditTrailWriter.TryLog(conn, transaction, new ItemAuditTrailDto
                            {
                                ItemId = item.ItemId,
                                SerialNumber = item.SerialNumber,
                                Action = "Invoice Deleted - Stock Restored",
                                ActionTime = DateTime.Now,
                                Direction = "IN",
                                Status = "Completed",
                                ReferenceType = "Set",
                                ReferenceId = setId,
                                Notes = $"Stock restored for {item.Quantity} unit(s) after invoice deletion.",
                                CreatedBy = AppSession.CurrentUserName ?? "System"
                            });
                        }
                    }

                    // Step 3: Delete SetItem records (child records first to avoid FK constraint)
                    string deleteSetItemsSql = "DELETE FROM SetItem WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteSetItemsSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Step 4: Delete related Inventory entries if any
                    string deleteInventorySql = "DELETE FROM Inventory WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteInventorySql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Step 5: Finally delete the Set (invoice) itself
                    string deleteSetSql = "DELETE FROM [Set] WHERE SetId = @SetId";
                    using (SqlCommand cmd = new SqlCommand(deleteSetSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SetId", setId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            throw new Exception("Invoice not found or already deleted.");
                        }
                    }

                    transaction.Commit();
                    ActivityLogger.Log(ActivityLogger.Actions.Delete, "Invoice", setId, $"Invoice deleted and stock restored (Set #{setId})");
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // Get all companies for dropdown
        public async Task<DataTable> GetAllCompaniesAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT ComId, Name AS CompanyName FROM dbo.Company WHERE Active = 1 ORDER BY Name";

                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        public async Task<DataTable> GetAllBranchesAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT BranchId, Name AS BranchName FROM dbo.Branch ORDER BY Name";

                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        public async Task<DataTable> GetAllDistributorsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT DistributorId, Name AS DistributorName FROM dbo.Distributor WHERE IsActive = 1 ORDER BY SortOrder, Name";

                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        public async Task<DataTable> GetAllDepartmentsAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT DeptId, Name AS DepartmentName FROM dbo.Department ORDER BY Name";

                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        /// <summary>Active employees for the invoice "Requested By" picker. Name carries the
        /// employee number and position so the editable ComboBox is searchable by any of them.</summary>
        public async Task<DataTable> GetAllEmployeesAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string query = @"
                    SELECT EmpId,
                           LTRIM(RTRIM(
                               ISNULL(NULLIF(EmployeeNumber, '') + ' - ', '') + Name +
                               ISNULL(' (' + NULLIF(Position, '') + ')', '')
                           )) AS Name
                    FROM dbo.Employee
                    WHERE Active = 1
                    ORDER BY Name";

                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        /// <summary>
        /// Archives one or more invoices (their underlying dbo.Set rows) by inserting an
        /// ArchiveStatus row per SetId — the same EntityType='Set' mechanism the Manage Sets page
        /// uses, so archived invoices surface on the general Archive page and are excluded from
        /// GetAllInvoicesAsync (see its ArchiveStatus join) without needing a separate table.
        /// </summary>
        public void ArchiveInvoiceSets(IEnumerable<int> setIds, string reason)
        {
            var ids = setIds?.Where(id => id > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0) return;

            const string sql = @"
                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES ('Set', @SetId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                foreach (var setId in ids)
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@SetId", setId);
                        command.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                        command.Parameters.AddWithValue("@ArchiveReason", string.IsNullOrWhiteSpace(reason) ? "No reason provided" : reason.Trim());
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Fetches VendorName/ModelNumber/PartNumber/AssetSerialNumber from dbo.vw_InvoiceItems (one
        /// row per invoice line item) for a batch of SetIds in one query, mirroring the SetRepository
        /// GetXBySetIds pattern used for item names/serials/Parent Tag. Returns a dictionary: SetId →
        /// the distinct, non-blank values found across that invoice's line items — feeds the Invoice
        /// Reports quick-search field list (see InvoicePageViewModel.Filters.InvoiceSearchFields /
        /// InvoiceFuzzyCandidateFields) with columns that otherwise aren't reachable there at all.
        /// </summary>
        public Dictionary<int, InvoiceItemSearchFields> GetInvoiceItemSearchFieldsBySetIds(IEnumerable<int> setIds)
        {
            var result = new Dictionary<int, InvoiceItemSearchFields>();
            var ids = setIds?.Where(x => x > 0).Distinct().ToList();
            if (ids == null || ids.Count == 0 || ids.Count > 2000) return result;

            var paramNames = string.Join(",", ids.Select((id, i) => "@p" + i));
            var sql = $@"
                SELECT SetId, VendorName, ModelNumber, PartNumber, AssetSerialNumber
                FROM dbo.vw_InvoiceItems
                WHERE SetId IN ({paramNames})";

            using (var con = new SqlConnection(_connectionString))
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
                            int setId = reader.GetInt32(reader.GetOrdinal("SetId"));
                            if (!result.TryGetValue(setId, out var fields))
                                result[setId] = fields = new InvoiceItemSearchFields();

                            AddIfPresent(fields.VendorNames, reader, "VendorName");
                            AddIfPresent(fields.ModelNumbers, reader, "ModelNumber");
                            AddIfPresent(fields.PartNumbers, reader, "PartNumber");
                            AddIfPresent(fields.AssetSerialNumbers, reader, "AssetSerialNumber");
                        }
                    }
                }
            }
            return result;
        }

        private static void AddIfPresent(List<string> target, SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return;
            string value = reader.GetString(ordinal).Trim();
            if (value.Length > 0 && !target.Contains(value, StringComparer.OrdinalIgnoreCase))
                target.Add(value);
        }
    }

    /// <summary>Per-SetId bundle of dbo.vw_InvoiceItems columns folded into the Invoice Reports quick
    /// search — see InvoiceRepository.GetInvoiceItemSearchFieldsBySetIds.</summary>
    public class InvoiceItemSearchFields
    {
        public List<string> VendorNames { get; } = new List<string>();
        public List<string> ModelNumbers { get; } = new List<string>();
        public List<string> PartNumbers { get; } = new List<string>();
        public List<string> AssetSerialNumbers { get; } = new List<string>();
    }
}
