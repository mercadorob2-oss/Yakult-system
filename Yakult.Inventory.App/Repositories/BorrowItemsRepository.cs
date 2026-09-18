using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Repositories
{
    public sealed class BorrowItemsRepository
    {
        private static string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task<bool> BorrowSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.BorrowLog");
            }
        }

        public async Task<BorrowLogRow> GetBorrowByIdAsync(int borrowId)
        {
            if (borrowId <= 0)
                return null;

            const string sql = @"
SELECT TOP 1
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.BorrowId = @BorrowId;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.BorrowLog"))
                    return null;

                return await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(sql, new { BorrowId = borrowId });
            }
        }

        public async Task<BorrowItemLookup> FindItemBySerialAsync(string serialNumber)
        {
            var serial = (serialNumber ?? string.Empty).Trim();
            if (serial.Length == 0)
                return null;

            const string sql = @"
SELECT TOP 1
    i.ItemId,
    i.SerialNumber AS SerialNumber,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber AS ModelNumber,
    i.Category AS Category
FROM dbo.Item i
WHERE i.SerialNumber = @Serial;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var row = await connection.QuerySingleOrDefaultAsync<BorrowItemLookup>(sql, new { Serial = serial });
                if (row != null)
                    row.SerialNumber = serial;
                return row;
            }
        }

        /// <summary>
        /// Broader "Listed in Inventory" search for Add Item/Borrow — the serial number is no
        /// longer the only way to find an item; any combination of name, model, category, and
        /// serial (all optional, ANDed together when provided) narrows the match. Returns every
        /// serialized, active item that matches so the caller can present a pick list.
        /// </summary>
        public async Task<List<BorrowItemLookup>> SearchListedItemsAsync(string name, string serialNumber, string modelText, string category, int maxRows = 100)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 500) maxRows = 500;

            var nameFilter = (name ?? string.Empty).Trim();
            var serialFilter = (serialNumber ?? string.Empty).Trim();
            var modelFilter = (modelText ?? string.Empty).Trim();
            var categoryFilter = (category ?? string.Empty).Trim();

            const string sql = @"
SELECT TOP (@MaxRows)
    i.ItemId,
    i.SerialNumber AS SerialNumber,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber AS ModelNumber,
    i.Category AS Category
FROM dbo.Item i
WHERE i.Active = 1
  AND i.SerialNumber IS NOT NULL
  AND (@Name = '' OR i.Name LIKE '%' + @Name + '%')
  AND (@Serial = '' OR i.SerialNumber LIKE '%' + @Serial + '%')
  AND (@Model = '' OR i.ModelNumber LIKE '%' + @Model + '%')
  AND (@Category = '' OR i.Category = @Category)
ORDER BY i.Name, i.SerialNumber;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<BorrowItemLookup>(sql, new
                {
                    Name = nameFilter,
                    Serial = serialFilter,
                    Model = modelFilter,
                    Category = categoryFilter,
                    MaxRows = maxRows
                });
                return rows.AsList();
            }
        }

        /// <summary>
        /// Helper lookup for the optional "Model number" search box on the Borrow Items scan
        /// panel. Not required for borrowing — it just helps the user find the serial number of
        /// an item when they only know (or partially know) its model number.
        /// </summary>
        public async Task<List<BorrowItemLookup>> SearchItemsByModelAsync(string modelText, int maxRows = 25)
        {
            var model = (modelText ?? string.Empty).Trim();
            if (model.Length == 0)
                return new List<BorrowItemLookup>();

            if (maxRows < 1) maxRows = 1;
            if (maxRows > 100) maxRows = 100;

            const string sql = @"
SELECT TOP (@MaxRows)
    i.ItemId,
    i.SerialNumber AS SerialNumber,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber AS ModelNumber
FROM dbo.Item i
WHERE i.Active = 1
  AND i.SerialNumber IS NOT NULL
  AND i.ModelNumber LIKE '%' + @Model + '%'
ORDER BY i.ModelNumber, i.SerialNumber;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<BorrowItemLookup>(sql, new { Model = model, MaxRows = maxRows });
                return rows.AsList();
            }
        }

        /// <summary>
        /// Backs the "Select from list" picker on the Borrow Items scan panel — a third way to
        /// pick an item to borrow alongside scanning a QR code or typing its serial number.
        /// Only items flagged IsBorrowable, active, and not already out on an open BorrowLog
        /// row are eligible.
        /// </summary>
        public async Task<List<BorrowItemLookup>> GetBorrowableItemsAsync(string searchText, int maxRows = 200)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 500) maxRows = 500;

            var search = (searchText ?? string.Empty).Trim();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasIsBorrowable = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Item", "IsBorrowable");
                if (!hasIsBorrowable)
                    return new List<BorrowItemLookup>();

                const string sql = @"
SELECT TOP (@MaxRows)
    i.ItemId,
    i.SerialNumber AS SerialNumber,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber AS ModelNumber
FROM dbo.Item i
WHERE i.IsBorrowable = 1
  AND i.Active = 1
  AND i.SerialNumber IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM dbo.BorrowLog bl
        WHERE bl.ItemId = i.ItemId AND bl.ReturnedAtUtc IS NULL
      )
  -- A spare must never be an item already issued to someone else via an active Set (either
  -- directly on a live SetItem row, or via a Request still linked to a live Set) — same
  -- ""current active set for item"" pattern used by the Repair Portal's own item pickers
  -- (RepairTicketRepository.Lookups.cs).
  AND NOT EXISTS (
        SELECT 1 FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE si.ItemId = i.ItemId AND s.Active = 1 AND archS.EntityId IS NULL

        UNION ALL

        SELECT 1 FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE r.ItemId = i.ItemId AND r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
      )
  AND (@Search = '' OR i.Name LIKE '%' + @Search + '%'
       OR i.SerialNumber LIKE '%' + @Search + '%' OR i.ModelNumber LIKE '%' + @Search + '%')
ORDER BY i.Name;";

                var rows = await connection.QueryAsync<BorrowItemLookup>(sql, new { Search = search, MaxRows = maxRows });
                return rows.AsList();
            }
        }

        public async Task<List<BorrowEmployeeLookup>> GetActiveEmployeesAsync(int maxRows = 5000)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 20000) maxRows = 20000;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasBdc = await CallSchemaGate.TableExistsAsync(connection, "dbo.BranchDepartmentCompany");
                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "BranchId");
                var hasComId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "ComId");

                var branchIdExpr = hasBranchId ? "e.BranchId" : "CAST(NULL AS int)";
                var branchNameExpr = hasBranchId ? "b.Name" : "CAST(NULL AS nvarchar(200))";
                var branchJoin = hasBranchId
                    ? "LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId"
                    : string.Empty;

                // An employee may be department-only, branch-only, or assigned to both.
                // Resolve company using the most specific available BDC mapping, then
                // fall back to the legacy Employee.ComId column when BDC is unavailable.
                string derivedComIdExpr;
                if (hasBdc)
                {
                    var exactMapping = hasBranchId
                        ? @"SELECT TOP 1 bdc.CompanyID
                            FROM dbo.BranchDepartmentCompany bdc
                            WHERE bdc.BranchID = e.BranchId
                              AND bdc.DepartmentID = e.DeptId
                              AND e.BranchId IS NOT NULL
                              AND e.DeptId IS NOT NULL
                            ORDER BY bdc.CompanyID ASC"
                        : "SELECT TOP 1 bdc.CompanyID FROM dbo.BranchDepartmentCompany bdc WHERE 1 = 0";
                    var branchMapping = hasBranchId
                        ? @"SELECT TOP 1 bdc.CompanyID
                            FROM dbo.BranchDepartmentCompany bdc
                            WHERE bdc.BranchID = e.BranchId
                              AND e.BranchId IS NOT NULL
                            ORDER BY bdc.CompanyID ASC"
                        : "SELECT TOP 1 bdc.CompanyID FROM dbo.BranchDepartmentCompany bdc WHERE 1 = 0";
                    var departmentMapping = @"SELECT TOP 1 bdc.CompanyID
                            FROM dbo.BranchDepartmentCompany bdc
                            WHERE bdc.DepartmentID = e.DeptId
                              AND e.DeptId IS NOT NULL
                            ORDER BY bdc.CompanyID ASC";

                    derivedComIdExpr = $@"COALESCE(
                        ({exactMapping}),
                        ({branchMapping}),
                        ({departmentMapping}))";
                }
                else
                {
                    derivedComIdExpr = hasComId ? "e.ComId" : "CAST(NULL AS int)";
                }

                var sql = @"
 SELECT TOP (@MaxRows)
     e.EmpId,
     e.Name AS EmployeeName,
     " + derivedComIdExpr + @" AS ComId,
     c.Name AS CompanyName,
     e.DeptId AS DeptId,
     d.Name AS DepartmentName,
     " + branchIdExpr + @" AS BranchId,
     " + branchNameExpr + @" AS BranchName
 FROM dbo.Employee e
 LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
 " + branchJoin + @"
 LEFT JOIN dbo.Company c ON c.ComId = (" + derivedComIdExpr + @")
 WHERE e.Active = 1
   AND (e.DeptId IS NOT NULL OR " + (hasBranchId ? "e.BranchId IS NOT NULL" : "1 = 0") + @")
 ORDER BY c.Name ASC, d.Name ASC, " + branchNameExpr + @" ASC, e.Name ASC, e.EmpId ASC;";

                var rows = await connection.QueryAsync<BorrowEmployeeLookup>(sql, new { MaxRows = maxRows });
                return rows?.ToList() ?? new List<BorrowEmployeeLookup>();
            }
        }

        public async Task<List<CompanyDto>> GetActiveCompaniesAsync(int maxRows = 5000)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 20000) maxRows = 20000;

            const string sql = @"
IF OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NULL
BEGIN
    SELECT TOP (@MaxRows)
        c.ComId,
        c.Name AS CompanyName
    FROM dbo.Company c
    LEFT JOIN dbo.ArchiveStatus arc
        ON arc.EntityType = 'Company'
       AND arc.EntityId = c.ComId
       AND arc.IsArchived = 1
    WHERE arc.ArchiveId IS NULL
      AND ISNULL(c.Active, 1) = 1
    ORDER BY c.Name ASC, c.ComId ASC;
END
ELSE
BEGIN
    SELECT TOP (@MaxRows)
        c.ComId,
        c.Name AS CompanyName
    FROM dbo.Company c
    WHERE ISNULL(c.Active, 1) = 1
      AND EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany bdc WHERE bdc.CompanyID = c.ComId)
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.ArchiveStatus arc
          WHERE arc.EntityType = 'Company'
            AND arc.EntityId = c.ComId
            AND arc.IsArchived = 1
      )
    ORDER BY c.Name ASC, c.ComId ASC;
END";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    var rows = await connection.QueryAsync<CompanyDto>(sql, new { MaxRows = maxRows });
                    return rows?.ToList() ?? new List<CompanyDto>();
                }
                catch (SqlException)
                {
                    // Some older DBs may not have ArchiveStatus — fallback to a simpler query.
                    const string fallbackSql = @"
IF OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NULL
BEGIN
    SELECT TOP (@MaxRows)
        c.ComId,
        c.Name AS CompanyName
    FROM dbo.Company c
    WHERE ISNULL(c.Active, 1) = 1
    ORDER BY c.Name ASC, c.ComId ASC;
END
ELSE
BEGIN
    SELECT TOP (@MaxRows)
        c.ComId,
        c.Name AS CompanyName
    FROM dbo.Company c
    WHERE ISNULL(c.Active, 1) = 1
      AND EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany bdc WHERE bdc.CompanyID = c.ComId)
    ORDER BY c.Name ASC, c.ComId ASC;
END";

                    var rows = await connection.QueryAsync<CompanyDto>(fallbackSql, new { MaxRows = maxRows });
                    return rows?.ToList() ?? new List<CompanyDto>();
                }
            }
        }

        public async Task<BorrowLogRow> GetOpenBorrowBySerialAsync(string serialNumber)
        {
            var serial = (serialNumber ?? string.Empty).Trim();
            if (serial.Length == 0)
                return null;

            const string sql = @"
SELECT TOP 1
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.ReturnedAtUtc IS NULL
  AND b.SerialNumber = @Serial
ORDER BY b.BorrowedAtUtc DESC, b.BorrowId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(sql, new { Serial = serial });
            }
        }

        public async Task<List<BorrowLogRow>> GetOpenBorrowsAsync(string serialContains = null, int maxRows = 500)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 5000) maxRows = 5000;

            // Backwards-compatible: serialContains is treated as a general search query (serial/name/model/person/department).
            var queryText = (serialContains ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(queryText);

            var sql = @"
 SELECT TOP (@MaxRows)
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
 FROM dbo.BorrowLog b
 WHERE b.ReturnedAtUtc IS NULL";

            if (hasQuery)
            {
                sql += @"
  AND (
        b.SerialNumber LIKE @Query
     OR b.ItemName LIKE @Query
     OR b.ModelNumber LIKE @Query
     OR b.BorrowedByEmpName LIKE @Query
     OR b.BorrowedByDeptName LIKE @Query
  )";
            }

            sql += "\nORDER BY b.BorrowedAtUtc DESC, b.BorrowId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<BorrowLogRow>(
                    sql,
                    new
                    {
                        MaxRows = maxRows,
                        Query = "%" + queryText + "%"
                    });
                return rows?.ToList() ?? new List<BorrowLogRow>();
            }
        }

        private sealed class BorrowLogPageMeta
        {
            public int TotalCount { get; set; }
            public DateTime? OldestBorrowedAtUtc { get; set; }
        }

        public async Task<BorrowLogPage> GetOpenBorrowsPageAsync(string serialContains, int pageIndex, int pageSize)
        {
            if (pageIndex < 0) pageIndex = 0;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 2000) pageSize = 2000;

            var queryText = (serialContains ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(queryText);
            var offset = pageIndex * pageSize;

            var where = @"
 WHERE b.ReturnedAtUtc IS NULL";

            if (hasQuery)
            {
                where += @"
  AND (
        b.SerialNumber LIKE @Query
     OR b.ItemName LIKE @Query
     OR b.ModelNumber LIKE @Query
     OR b.BorrowedByEmpName LIKE @Query
     OR b.BorrowedByDeptName LIKE @Query
  )";
            }

            var sql = @"
SELECT
    COUNT(1) AS TotalCount,
    MIN(b.BorrowedAtUtc) AS OldestBorrowedAtUtc
FROM dbo.BorrowLog b" + where + @";

SELECT
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b" + where + @"
ORDER BY b.BorrowedAtUtc DESC, b.BorrowId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var multi = await connection.QueryMultipleAsync(
                    sql,
                    new
                    {
                        Query = "%" + queryText + "%",
                        Offset = offset,
                        PageSize = pageSize
                    }))
                {
                    var meta = await multi.ReadSingleAsync<BorrowLogPageMeta>();
                    var rows = (await multi.ReadAsync<BorrowLogRow>())?.ToList() ?? new List<BorrowLogRow>();

                    return new BorrowLogPage
                    {
                        TotalCount = meta?.TotalCount ?? 0,
                        OldestBorrowedAtUtc = meta?.OldestBorrowedAtUtc,
                        Rows = rows
                    };
                }
            }
        }

        public async Task<List<BorrowLogRow>> GetHistoryAsync(DateTime fromUtc, DateTime toUtcExclusive, string serialContains = null, int maxRows = 2000)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 20000) maxRows = 20000;

            var queryText = (serialContains ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(queryText);

            var sql = @"
SELECT TOP (@MaxRows)
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.ReturnedAtUtc IS NOT NULL
  AND b.BorrowedAtUtc >= @FromUtc
  AND b.BorrowedAtUtc < @ToUtc";

            if (hasQuery)
            {
                sql += @"
  AND (
        b.SerialNumber LIKE @Query
     OR b.ItemName LIKE @Query
     OR b.ModelNumber LIKE @Query
     OR b.BorrowedByEmpName LIKE @Query
     OR b.BorrowedByDeptName LIKE @Query
     OR b.ReturnedByEmpName LIKE @Query
     OR b.ReturnedByDeptName LIKE @Query
  )";
            }

            sql += "\nORDER BY b.BorrowedAtUtc DESC, b.BorrowId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<BorrowLogRow>(
                    sql,
                    new
                    {
                        MaxRows = maxRows,
                        FromUtc = fromUtc,
                        ToUtc = toUtcExclusive,
                        Query = "%" + queryText + "%"
                    });
                return rows?.ToList() ?? new List<BorrowLogRow>();
            }
        }

        public async Task<BorrowLogPage> GetHistoryPageAsync(DateTime fromUtc, DateTime toUtcExclusive, string serialContains, int pageIndex, int pageSize)
        {
            if (pageIndex < 0) pageIndex = 0;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 5000) pageSize = 5000;

            var queryText = (serialContains ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(queryText);
            var offset = pageIndex * pageSize;

            var where = @"
WHERE b.ReturnedAtUtc IS NOT NULL
  AND b.BorrowedAtUtc >= @FromUtc
  AND b.BorrowedAtUtc < @ToUtc";

            if (hasQuery)
            {
                where += @"
  AND (
        b.SerialNumber LIKE @Query
     OR b.ItemName LIKE @Query
     OR b.ModelNumber LIKE @Query
     OR b.BorrowedByEmpName LIKE @Query
     OR b.BorrowedByDeptName LIKE @Query
     OR b.ReturnedByEmpName LIKE @Query
     OR b.ReturnedByDeptName LIKE @Query
  )";
            }

            var sql = @"
SELECT
    COUNT(1) AS TotalCount,
    CAST(NULL AS DATETIME2) AS OldestBorrowedAtUtc
FROM dbo.BorrowLog b" + where + @";

SELECT
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b" + where + @"
ORDER BY b.BorrowedAtUtc DESC, b.BorrowId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                using (var multi = await connection.QueryMultipleAsync(
                    sql,
                    new
                    {
                        FromUtc = fromUtc,
                        ToUtc = toUtcExclusive,
                        Query = "%" + queryText + "%",
                        Offset = offset,
                        PageSize = pageSize
                    }))
                {
                    var meta = await multi.ReadSingleAsync<BorrowLogPageMeta>();
                    var rows = (await multi.ReadAsync<BorrowLogRow>())?.ToList() ?? new List<BorrowLogRow>();

                    return new BorrowLogPage
                    {
                        TotalCount = meta?.TotalCount ?? 0,
                        Rows = rows
                    };
                }
            }
        }

        public async Task<BorrowLogRow> BorrowAsync(string serialNumber, int? borrowedByEmpId, int borrowEncodedByUserId, DateTime? borrowedAtUtc = null, int? borrowedByDeptId = null, string borrowedByDeptName = null, int? repairTicketId = null)
        {
            var serial = (serialNumber ?? string.Empty).Trim();
            if (serial.Length == 0)
                throw new InvalidOperationException("Serial number is required.");

            var deptOnlyName = (borrowedByDeptName ?? string.Empty).Trim();
            var isDepartmentOnly = !(borrowedByEmpId.HasValue && borrowedByEmpId.Value > 0);

            if (isDepartmentOnly && (!borrowedByDeptId.HasValue || borrowedByDeptId.Value <= 0 || deptOnlyName.Length == 0))
                throw new InvalidOperationException("Either a borrowing employee or a department is required.");

            if (borrowEncodedByUserId <= 0)
                throw new InvalidOperationException("Encoded-by user is required.");

            var actualBorrowedAtUtc = borrowedAtUtc.HasValue
                ? AppTime.AssumeUtc(borrowedAtUtc.Value)
                : AppTime.UtcNow;

            if (actualBorrowedAtUtc > AppTime.UtcNow.AddMinutes(1))
                throw new InvalidOperationException("Borrowed date/time cannot be in the future.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.BorrowLog", tx))
                            throw new InvalidOperationException("Borrow Items schema is not installed in this database.");

                        var item = await connection.QuerySingleOrDefaultAsync<BorrowItemLookup>(
                            new CommandDefinition(
                                @"
SELECT TOP 1
    i.ItemId,
    i.SerialNumber AS SerialNumber,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber AS ModelNumber
FROM dbo.Item i
WHERE i.SerialNumber = @Serial;",
                                new { Serial = serial },
                                transaction: tx));

                        if (item == null || item.ItemId <= 0)
                            throw new InvalidOperationException("Item not found for the given serial number.");

                        var existingOpen = await connection.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                "SELECT TOP 1 BorrowId FROM dbo.BorrowLog WHERE ItemId = @ItemId AND ReturnedAtUtc IS NULL;",
                                new { ItemId = item.ItemId },
                                transaction: tx));

                        if (existingOpen > 0)
                        {
                            // Idempotent fast-retry: if the same operator submits the same borrow again, treat it as "already logged".
                            // Otherwise, keep the strict behavior (don't allow re-borrowing while open).
                            const string existingOpenRowSql = @"
SELECT TOP 1
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.BorrowId = @BorrowId;";

                            var existingRow = await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(
                                new CommandDefinition(existingOpenRowSql, new { BorrowId = existingOpen }, transaction: tx));

                            if (existingRow != null && existingRow.BorrowId > 0 && existingRow.IsOpen)
                            {
                                var sameBorrower = isDepartmentOnly
                                    ? !existingRow.BorrowedByEmpId.HasValue && existingRow.BorrowedByDeptId == borrowedByDeptId
                                    : existingRow.BorrowedByEmpId == borrowedByEmpId;
                                var sameEncoder = existingRow.BorrowEncodedByUserId == borrowEncodedByUserId;
                                var closeInTime = Math.Abs((AppTime.AssumeUtc(existingRow.BorrowedAtUtc) - actualBorrowedAtUtc).TotalMinutes) <= 2;

                                if (sameBorrower && sameEncoder && closeInTime)
                                {
                                    tx.Commit();
                                    return existingRow;
                                }
                            }

                            throw new InvalidOperationException("This item is already borrowed (open record exists).");
                        }

                        int? insertEmpId;
                        string insertEmpName;
                        int? insertDeptId;
                        string insertDeptName;

                        if (isDepartmentOnly)
                        {
                            insertEmpId = null;
                            insertEmpName = $"{deptOnlyName} (no specific employee)";
                            insertDeptId = borrowedByDeptId;
                            insertDeptName = deptOnlyName;
                        }
                        else
                        {
                            var borrower = await GetEmployeeWithDeptAsync(connection, tx, borrowedByEmpId.Value);
                            if (borrower == null || borrower.EmpId <= 0)
                                throw new InvalidOperationException("Selected employee not found.");

                            var hasDept = borrower.DeptId > 0;
                            insertEmpId = borrower.EmpId;
                            insertEmpName = borrower.EmployeeName;
                            insertDeptId = hasDept ? (int?)borrower.DeptId : null;
                            insertDeptName = hasDept ? borrower.DepartmentName : null;
                        }

                        var encoderName = await GetUserNameAsync(connection, tx, borrowEncodedByUserId);
                        if (string.IsNullOrWhiteSpace(encoderName))
                            encoderName = borrowEncodedByUserId.ToString();

                        const string insertSql = @"
INSERT dbo.BorrowLog
(
    ItemId, SerialNumber, ItemName, ItemDescription, ModelNumber,
    BorrowedByEmpId, BorrowedByEmpName, BorrowedByDeptId, BorrowedByDeptName,
    BorrowEncodedByUserId, BorrowEncodedByUserName,
    BorrowedAtUtc, RepairTicketId
)
OUTPUT
    inserted.BorrowId,
    inserted.ItemId,
    inserted.SerialNumber,
    inserted.ItemName,
    inserted.ItemDescription,
    inserted.ModelNumber,
    inserted.BorrowedByEmpId,
    inserted.BorrowedByEmpName,
    inserted.BorrowedByDeptId,
    inserted.BorrowedByDeptName,
    inserted.BorrowEncodedByUserId,
    inserted.BorrowEncodedByUserName,
    inserted.BorrowedAtUtc,
    inserted.ReturnedByEmpId,
    inserted.ReturnedByEmpName,
    inserted.ReturnedByDeptId,
    inserted.ReturnedByDeptName,
    inserted.ReturnEncodedByUserId,
    inserted.ReturnEncodedByUserName,
    inserted.ReturnedAtUtc,
    inserted.RepairTicketId
VALUES
(
    @ItemId, @SerialNumber, @ItemName, @ItemDescription, @ModelNumber,
    @BorrowedByEmpId, @BorrowedByEmpName, @BorrowedByDeptId, @BorrowedByDeptName,
    @BorrowEncodedByUserId, @BorrowEncodedByUserName,
    @BorrowedAtUtc, @RepairTicketId
);";

                        var created = await connection.QuerySingleAsync<BorrowLogRow>(
                            new CommandDefinition(
                                insertSql,
                                new
                                {
                                    ItemId = item.ItemId,
                                    SerialNumber = serial,
                                    ItemName = (item.ItemName ?? string.Empty).Trim().Length == 0 ? serial : item.ItemName,
                                    ItemDescription = string.IsNullOrWhiteSpace(item.ItemDescription) ? (object)DBNull.Value : item.ItemDescription.Trim(),
                                    ModelNumber = string.IsNullOrWhiteSpace(item.ModelNumber) ? (object)DBNull.Value : item.ModelNumber.Trim(),
                                    BorrowedByEmpId = insertEmpId,
                                    BorrowedByEmpName = insertEmpName,
                                    BorrowedByDeptId = insertDeptId,
                                    BorrowedByDeptName = insertDeptName,
                                    BorrowEncodedByUserId = borrowEncodedByUserId,
                                    BorrowEncodedByUserName = encoderName.Trim(),
                                    BorrowedAtUtc = actualBorrowedAtUtc,
                                    RepairTicketId = repairTicketId
                                },
                                transaction: tx));

                        tx.Commit();
                        return created;
                    }
                    catch (SqlException ex) when (IsUniqueViolation(ex))
                    {
                        tx.Rollback();

                        // Last-chance idempotency: if a concurrent insert happened, try to load the open row and return it
                        // when it matches the same borrower/encoder within the retry window.
                        try
                        {
                            var existing = await GetOpenBorrowBySerialAsync(serial);
                            if (existing != null && existing.BorrowId > 0 && existing.IsOpen)
                            {
                                var sameBorrower = isDepartmentOnly
                                    ? !existing.BorrowedByEmpId.HasValue && existing.BorrowedByDeptId == borrowedByDeptId
                                    : existing.BorrowedByEmpId == borrowedByEmpId;
                                var sameEncoder = existing.BorrowEncodedByUserId == borrowEncodedByUserId;
                                var closeInTime = Math.Abs((AppTime.AssumeUtc(existing.BorrowedAtUtc) - actualBorrowedAtUtc).TotalMinutes) <= 2;
                                if (sameBorrower && sameEncoder && closeInTime)
                                    return existing;
                            }
                        }
                        catch
                        {
                            // If lookup fails, fall back to the original error.
                        }

                        throw new InvalidOperationException("This item is already borrowed (open record exists).");
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<BorrowLogRow> ReturnAsync(int borrowId, int? returnedByEmpId, int returnEncodedByUserId, int? returnedByDeptId = null, string returnedByDeptName = null)
        {
            if (borrowId <= 0)
                throw new InvalidOperationException("Borrow record is required.");

            var deptOnlyName = (returnedByDeptName ?? string.Empty).Trim();
            var isDepartmentOnly = !(returnedByEmpId.HasValue && returnedByEmpId.Value > 0);

            if (isDepartmentOnly && (!returnedByDeptId.HasValue || returnedByDeptId.Value <= 0 || deptOnlyName.Length == 0))
                throw new InvalidOperationException("Either a returning employee or a department is required.");

            if (returnEncodedByUserId <= 0)
                throw new InvalidOperationException("Encoded-by user is required.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.BorrowLog", tx))
                            throw new InvalidOperationException("Borrow Items schema is not installed in this database.");

                        var open = await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(
                            new CommandDefinition(
                                @"
SELECT TOP 1
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.BorrowId = @BorrowId;",
                                new { BorrowId = borrowId },
                                transaction: tx));

                        if (open == null || open.BorrowId <= 0)
                            throw new InvalidOperationException("Borrow record not found.");

                        if (!open.IsOpen)
                            throw new InvalidOperationException("This borrow record is already returned.");

                        int? updateEmpId;
                        string updateEmpName;
                        int? updateDeptId;
                        string updateDeptName;

                        if (isDepartmentOnly)
                        {
                            updateEmpId = null;
                            updateEmpName = $"{deptOnlyName} (no specific employee)";
                            updateDeptId = returnedByDeptId;
                            updateDeptName = deptOnlyName;
                        }
                        else
                        {
                            var returner = await GetEmployeeWithDeptAsync(connection, tx, returnedByEmpId.Value);
                            if (returner == null || returner.EmpId <= 0)
                                throw new InvalidOperationException("Selected employee not found.");

                            var hasDept = returner.DeptId > 0;
                            updateEmpId = returner.EmpId;
                            updateEmpName = returner.EmployeeName;
                            updateDeptId = hasDept ? (int?)returner.DeptId : null;
                            updateDeptName = hasDept ? returner.DepartmentName : null;
                        }

                        var encoderName = await GetUserNameAsync(connection, tx, returnEncodedByUserId);
                        if (string.IsNullOrWhiteSpace(encoderName))
                            encoderName = returnEncodedByUserId.ToString();

                        const string updateSql = @"
 UPDATE dbo.BorrowLog
 SET
     ReturnedByEmpId = @ReturnedByEmpId,
     ReturnedByEmpName = @ReturnedByEmpName,
     ReturnedByDeptId = @ReturnedByDeptId,
     ReturnedByDeptName = @ReturnedByDeptName,
     ReturnEncodedByUserId = @ReturnEncodedByUserId,
     ReturnEncodedByUserName = @ReturnEncodedByUserName,
     ReturnedAtUtc = SYSUTCDATETIME()
 OUTPUT
     inserted.BorrowId,
     inserted.ItemId,
     inserted.SerialNumber,
     inserted.ItemName,
     inserted.ItemDescription,
     inserted.ModelNumber,
     inserted.BorrowedByEmpId,
     inserted.BorrowedByEmpName,
     inserted.BorrowedByDeptId,
     inserted.BorrowedByDeptName,
     inserted.BorrowEncodedByUserId,
     inserted.BorrowEncodedByUserName,
     inserted.BorrowedAtUtc,
     inserted.ReturnedByEmpId,
     inserted.ReturnedByEmpName,
     inserted.ReturnedByDeptId,
     inserted.ReturnedByDeptName,
     inserted.ReturnEncodedByUserId,
     inserted.ReturnEncodedByUserName,
     inserted.ReturnedAtUtc
 WHERE BorrowId = @BorrowId
   AND ReturnedAtUtc IS NULL;";

                        var updated = await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(
                            new CommandDefinition(
                                updateSql,
                                new
                                {
                                    BorrowId = borrowId,
                                    ReturnedByEmpId = updateEmpId,
                                    ReturnedByEmpName = updateEmpName,
                                    ReturnedByDeptId = updateDeptId,
                                    ReturnedByDeptName = updateDeptName,
                                    ReturnEncodedByUserId = returnEncodedByUserId,
                                    ReturnEncodedByUserName = encoderName.Trim()
                                },
                                transaction: tx));

                        if (updated == null)
                            throw new InvalidOperationException("This borrow was already returned by someone else. Please refresh.");

                        tx.Commit();
                        return updated;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<BorrowLogRow> DeleteOpenBorrowAsync(int borrowId)
        {
            if (borrowId <= 0)
                throw new InvalidOperationException("Borrow record is required.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                // Serializable reduces chance of double-insert on fast retries without requiring DB changes.
                using (var tx = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.BorrowLog", tx))
                            throw new InvalidOperationException("Borrow Items schema is not installed in this database.");

                        var open = await connection.QuerySingleOrDefaultAsync<BorrowLogRow>(
                            new CommandDefinition(
                                @"
SELECT TOP 1
    b.BorrowId,
    b.ItemId,
    b.SerialNumber,
    b.ItemName,
    b.ItemDescription,
    b.ModelNumber,
    b.BorrowedByEmpId,
    b.BorrowedByEmpName,
    b.BorrowedByDeptId,
    b.BorrowedByDeptName,
    b.BorrowEncodedByUserId,
    b.BorrowEncodedByUserName,
    b.BorrowedAtUtc,
    b.ReturnedByEmpId,
    b.ReturnedByEmpName,
    b.ReturnedByDeptId,
    b.ReturnedByDeptName,
    b.ReturnEncodedByUserId,
    b.ReturnEncodedByUserName,
    b.ReturnedAtUtc
FROM dbo.BorrowLog b
WHERE b.BorrowId = @BorrowId;",
                                new { BorrowId = borrowId },
                                transaction: tx));

                        if (open == null || open.BorrowId <= 0)
                            throw new InvalidOperationException("Borrow record not found.");

                        if (!open.IsOpen)
                            throw new InvalidOperationException("Only open borrow records can be deleted.");

                        var affected = await connection.ExecuteAsync(
                            new CommandDefinition(
                                "DELETE FROM dbo.BorrowLog WHERE BorrowId = @BorrowId AND ReturnedAtUtc IS NULL;",
                                new { BorrowId = borrowId },
                                transaction: tx));

                        if (affected <= 0)
                            throw new InvalidOperationException("This borrow was already returned or deleted by someone else. Please refresh.");

                        tx.Commit();
                        return open;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static Task<string> GetUserNameAsync(SqlConnection connection, SqlTransaction tx, int userId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (userId <= 0) return Task.FromResult<string>(null);

            return connection.ExecuteScalarAsync<string>(
                new CommandDefinition(
                    "SELECT TOP 1 Name FROM dbo.[User] WHERE UserId = @UserId;",
                    new { UserId = userId },
                    transaction: tx));
        }

        private static async Task<BorrowEmployeeLookup> GetEmployeeWithDeptAsync(SqlConnection connection, SqlTransaction tx, int empId)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (empId <= 0) return null;

            var hasBdc = await CallSchemaGate.TableExistsAsync(connection, "dbo.BranchDepartmentCompany", tx);
            var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "BranchId", tx);
            var hasComId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.Employee", "ComId", tx);

            var derivedComIdExpr =
                (hasBdc && hasBranchId)
                    ? @"
(
    SELECT TOP 1 bdc.CompanyID
    FROM dbo.BranchDepartmentCompany bdc
    WHERE bdc.BranchID = e.BranchId
      AND bdc.DepartmentID = e.DeptId
    ORDER BY bdc.CompanyID ASC
)"
                    : (hasComId ? "e.ComId" : "NULL");

            var sql = @"
 SELECT TOP 1
     e.EmpId,
     e.Name AS EmployeeName,
     " + derivedComIdExpr + @" AS ComId,
     c.Name AS CompanyName,
     e.DeptId AS DeptId,
     d.Name AS DepartmentName
 FROM dbo.Employee e
 LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
 LEFT JOIN dbo.Company c ON c.ComId = (" + derivedComIdExpr + @")
 WHERE e.EmpId = @EmpId;";

            return await connection.QuerySingleOrDefaultAsync<BorrowEmployeeLookup>(
                new CommandDefinition(sql, new { EmpId = empId }, transaction: tx));
        }

        private static bool IsUniqueViolation(SqlException ex)
        {
            if (ex == null) return false;
            return ex.Number == 2601 || ex.Number == 2627;
        }
    }
}
