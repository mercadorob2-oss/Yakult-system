using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class RepairTicketRepository
    {
        /// <summary>Looks up a single item for the New Repair Ticket picker by ID — used after the
        /// technician creates a new item via the full BatchAddItemDialog (the "+ Add Item" button)
        /// so it can be selected immediately without re-searching for it. A brand-new item has no
        /// Set yet, so CompanyName/BranchName/DeptName are simply null (same as any other
        /// not-yet-assigned item in SearchRepairableItemsAsync).</summary>
        public async Task<RepairableItemLookup> GetItemLookupByIdAsync(int itemId)
        {
            const string sql = "SELECT ItemId, Name, SerialNumber, ModelNumber FROM dbo.Item WHERE ItemId = @ItemId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new RepairableItemLookup
                        {
                            ItemId = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1),
                            SerialNumber = GetStringOrNull(reader, 2),
                            ModelNumber = GetStringOrNull(reader, 3)
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Item picker for the New Repair Ticket dialog. Reuses the exact "current active set for
        /// item" join used by RepairedItemsPageViewModel.Actions.cs / sp_RepairPortal_CreateTicket
        /// so the intake snapshot logic and picker preview agree.
        /// </summary>
        /// <summary>scope: "Set" (only items currently in an active Set — "In a Lot"), "Inventory"
        /// (only items with no active Set — "An Item"), or "Both" (default, no restriction).</summary>
        public async Task<List<RepairableItemLookup>> SearchRepairableItemsAsync(string query, string scope = "Both")
        {
            var list = new List<RepairableItemLookup>();
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return list;

            var sql = @"
SELECT TOP (25)
    i.ItemId, i.Name, i.SerialNumber, i.ModelNumber,
    aset.SetCode,
    -- Employee's own org (empCo/empBr/empDe) is preferred over the Set's own bare
    -- ComId/CurrentBranchId/CurrentDepartmentId columns (co/br/de) whenever an employee is
    -- actually assigned — mirrors SetRepository.GetEmployeeDetailsForSetAsync's
    -- COALESCE(ec.Name, sc.Name) priority. Without this, a Set that was previously assigned at
    -- Dept level and later reassigned to a specific Employee (UpdateSetLevelEmployeeAsync does
    -- NOT clear the old ComId/CurrentBranchId/CurrentDepartmentId) shows the stale Dept-level
    -- org instead of the actual recipient's own company/branch/department.
    COALESCE(reqCo.Name, empCo.Name, co.Name) AS CompanyName,
    COALESCE(reqBr.Name, empBr.Name, br.Name) AS BranchName,
    COALESCE(reqDe.Name, empDe.Name, de.Name) AS DeptName,
    COALESCE(reqRow.ComId, empAssigned.ComId, s.ComId) AS RequestedByComId,
    COALESCE(reqRow.BranchId, empAssigned.BranchId, s.CurrentBranchId) AS RequestedByBranchId,
    COALESCE(reqRow.DeptId, empAssigned.DeptId, s.CurrentDepartmentId) AS RequestedByDeptId,
    COALESCE(reqRow.EmpId, s.CurrentEmployeeId) AS RequestedByEmpId,
    empAssigned.Name AS RequestedByEmpName
FROM dbo.Item i
LEFT JOIN (
    SELECT
        x.ItemId, x.SetId, x.SetCode,
        ROW_NUMBER() OVER (PARTITION BY x.ItemId ORDER BY x.SetCreatedAt DESC, x.SetId DESC) AS rn
    FROM (
        SELECT si.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
        FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE s.Active = 1 AND archS.EntityId IS NULL

        UNION ALL

        SELECT r.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
        FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
    ) x
) aset ON aset.ItemId = i.ItemId AND aset.rn = 1
LEFT JOIN dbo.[Set] s ON s.SetId = aset.SetId
LEFT JOIN dbo.Company co ON co.ComId = s.ComId
LEFT JOIN dbo.Branch br ON br.BranchId = s.CurrentBranchId
LEFT JOIN dbo.Department de ON de.DeptId = s.CurrentDepartmentId
-- Company/Branch/Department/Employee are set on the Request row itself at intake (see
-- BatchAddRequestDialog) — NOT copied onto the Set's own ComId/CurrentBranchId/
-- CurrentDepartmentId columns, which the View Set Details page doesn't actually write to for
-- this flow. The Request row is the real source of truth; Set-level and the assigned Employee's
-- own org are just fallbacks for Sets that don't have a matching Request row.
OUTER APPLY (
    SELECT TOP (1) r2.EmpId, r2.ComId, r2.BranchId, r2.DeptId
    FROM dbo.Request r2
    WHERE r2.ItemId = i.ItemId AND r2.SetId = aset.SetId AND r2.Active = 1
    ORDER BY r2.DateRequested DESC, r2.ReqId DESC
) reqRow
LEFT JOIN dbo.Company reqCo ON reqCo.ComId = reqRow.ComId
LEFT JOIN dbo.Branch reqBr ON reqBr.BranchId = reqRow.BranchId
LEFT JOIN dbo.Department reqDe ON reqDe.DeptId = reqRow.DeptId
-- Falls back to dbo.[Set].CurrentEmployeeId when there's no dbo.Request row to read an EmpId
-- from (Renewal/Invoice-created Sets) — without this, an Employee-level assignment made via
-- ViewSetDetailPage's Save button on such a Set was structurally invisible here (see
-- UpdateSetLevelEmployeeAsync, the only writer of dbo.[Set].CurrentEmployeeId).
LEFT JOIN dbo.Employee empAssigned ON empAssigned.EmpId = COALESCE(reqRow.EmpId, s.CurrentEmployeeId)
LEFT JOIN dbo.Company empCo ON empCo.ComId = empAssigned.ComId
LEFT JOIN dbo.Branch empBr ON empBr.BranchId = empAssigned.BranchId
LEFT JOIN dbo.Department empDe ON empDe.DeptId = empAssigned.DeptId
WHERE i.Active = 1
  AND (i.ItemType IS NULL OR LTRIM(RTRIM(i.ItemType)) = '' OR LOWER(LTRIM(RTRIM(i.ItemType))) = 'hardware')
  -- Exclude items that already have a non-Completed repair ticket (Waiting/Diagnosing/Repairing/
  -- AwaitingParts/Testing, or an Unrepairable one whose disposition isn't resolved yet — resolved
  -- ones auto-flip to Completed, see RepairTicketRepository.Disposition.cs) — a second ticket
  -- shouldn't be openable for an item that's already being tracked in an active one.
  AND NOT EXISTS (
        SELECT 1 FROM dbo.RepairTicket rt
        WHERE rt.ItemId = i.ItemId AND rt.Status <> 'Completed'
      )"
                + (string.Equals(scope, "Set", StringComparison.OrdinalIgnoreCase) ? " AND aset.SetCode IS NOT NULL"
                   : string.Equals(scope, "Inventory", StringComparison.OrdinalIgnoreCase) ? " AND aset.SetCode IS NULL"
                   : string.Empty)
                + @"
  AND (i.Name LIKE @Search OR i.SerialNumber LIKE @Search OR i.ModelNumber LIKE @Search
       OR empAssigned.Name LIKE @Search OR COALESCE(reqDe.Name, de.Name) LIKE @Search)
ORDER BY i.Name ASC, i.ItemId ASC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Search", "%" + query.Trim() + "%");
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new RepairableItemLookup
                        {
                            ItemId = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1),
                            SerialNumber = GetStringOrNull(reader, 2),
                            ModelNumber = GetStringOrNull(reader, 3),
                            SetCode = GetStringOrNull(reader, 4),
                            CompanyName = GetStringOrNull(reader, 5),
                            BranchName = GetStringOrNull(reader, 6),
                            DeptName = GetStringOrNull(reader, 7),
                            RequestedByComId = GetIntOrNull(reader, 8),
                            RequestedByBranchId = GetIntOrNull(reader, 9),
                            RequestedByDeptId = GetIntOrNull(reader, 10),
                            RequestedByEmpId = GetIntOrNull(reader, 11),
                            RequestedByEmpName = GetStringOrNull(reader, 12)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Default candidate list shown in the New Repair Ticket item picker before the technician
        /// types a search: items that either currently have Condition = Damaged, or have prior
        /// repair history (dbo.ItemRepairHistory) — i.e. items likely to need another ticket.
        /// Reuses the same repair-history/current-condition join as RepairedItemsPageViewModel and
        /// the same "current active set for item" join as SearchRepairableItemsAsync above.
        /// </summary>
        /// <summary>scope: "Set" (only items currently in an active Set — "In a Lot"), "Inventory"
        /// (only items with no active Set — "An Item"), or "Both" (default, no restriction).</summary>
        public async Task<List<RepairableItemLookup>> GetCandidateRepairItemsAsync(string scope = "Both", int top = 50)
        {
            var list = new List<RepairableItemLookup>();

            var sql = @"
SELECT TOP (@Top)
    i.ItemId, i.Name, i.SerialNumber, i.ModelNumber,
    aset.SetCode,
    -- Employee's own org (empCo/empBr/empDe) is preferred over the Set's own bare
    -- ComId/CurrentBranchId/CurrentDepartmentId columns (co/br/de) whenever an employee is
    -- actually assigned — mirrors SetRepository.GetEmployeeDetailsForSetAsync's
    -- COALESCE(ec.Name, sc.Name) priority. Without this, a Set that was previously assigned at
    -- Dept level and later reassigned to a specific Employee (UpdateSetLevelEmployeeAsync does
    -- NOT clear the old ComId/CurrentBranchId/CurrentDepartmentId) shows the stale Dept-level
    -- org instead of the actual recipient's own company/branch/department.
    COALESCE(reqCo.Name, empCo.Name, co.Name) AS CompanyName,
    COALESCE(reqBr.Name, empBr.Name, br.Name) AS BranchName,
    COALESCE(reqDe.Name, empDe.Name, de.Name) AS DeptName,
    c.ConditionName, ISNULL(rh.RepairCount, 0) AS RepairCount,
    COALESCE(reqRow.ComId, empAssigned.ComId, s.ComId) AS RequestedByComId,
    COALESCE(reqRow.BranchId, empAssigned.BranchId, s.CurrentBranchId) AS RequestedByBranchId,
    COALESCE(reqRow.DeptId, empAssigned.DeptId, s.CurrentDepartmentId) AS RequestedByDeptId,
    COALESCE(reqRow.EmpId, s.CurrentEmployeeId) AS RequestedByEmpId,
    empAssigned.Name AS RequestedByEmpName
FROM dbo.Item i
LEFT JOIN dbo.Condition c ON c.ConditionId = i.ConditionId
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount
    FROM dbo.ItemRepairHistory
    WHERE SerialNumber IS NOT NULL
    GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN (
    SELECT
        x.ItemId, x.SetId, x.SetCode,
        ROW_NUMBER() OVER (PARTITION BY x.ItemId ORDER BY x.SetCreatedAt DESC, x.SetId DESC) AS rn
    FROM (
        SELECT si.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
        FROM dbo.SetItem si
        INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE s.Active = 1 AND archS.EntityId IS NULL

        UNION ALL

        SELECT r.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
        FROM dbo.Request r
        INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
        LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
        WHERE r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
    ) x
) aset ON aset.ItemId = i.ItemId AND aset.rn = 1
LEFT JOIN dbo.[Set] s ON s.SetId = aset.SetId
LEFT JOIN dbo.Company co ON co.ComId = s.ComId
LEFT JOIN dbo.Branch br ON br.BranchId = s.CurrentBranchId
LEFT JOIN dbo.Department de ON de.DeptId = s.CurrentDepartmentId
-- Company/Branch/Department/Employee are set on the Request row itself at intake (see
-- BatchAddRequestDialog) — NOT copied onto the Set's own ComId/CurrentBranchId/
-- CurrentDepartmentId columns, which the View Set Details page doesn't actually write to for
-- this flow. The Request row is the real source of truth; Set-level and the assigned Employee's
-- own org are just fallbacks for Sets that don't have a matching Request row.
OUTER APPLY (
    SELECT TOP (1) r2.EmpId, r2.ComId, r2.BranchId, r2.DeptId
    FROM dbo.Request r2
    WHERE r2.ItemId = i.ItemId AND r2.SetId = aset.SetId AND r2.Active = 1
    ORDER BY r2.DateRequested DESC, r2.ReqId DESC
) reqRow
LEFT JOIN dbo.Company reqCo ON reqCo.ComId = reqRow.ComId
LEFT JOIN dbo.Branch reqBr ON reqBr.BranchId = reqRow.BranchId
LEFT JOIN dbo.Department reqDe ON reqDe.DeptId = reqRow.DeptId
-- Falls back to dbo.[Set].CurrentEmployeeId when there's no dbo.Request row to read an EmpId
-- from (Renewal/Invoice-created Sets) — without this, an Employee-level assignment made via
-- ViewSetDetailPage's Save button on such a Set was structurally invisible here (see
-- UpdateSetLevelEmployeeAsync, the only writer of dbo.[Set].CurrentEmployeeId).
LEFT JOIN dbo.Employee empAssigned ON empAssigned.EmpId = COALESCE(reqRow.EmpId, s.CurrentEmployeeId)
LEFT JOIN dbo.Company empCo ON empCo.ComId = empAssigned.ComId
LEFT JOIN dbo.Branch empBr ON empBr.BranchId = empAssigned.BranchId
LEFT JOIN dbo.Department empDe ON empDe.DeptId = empAssigned.DeptId
WHERE i.Active = 1
  AND (i.ItemType IS NULL OR LTRIM(RTRIM(i.ItemType)) = '' OR LOWER(LTRIM(RTRIM(i.ItemType))) = 'hardware')
  -- Exclude items that already have a non-Completed repair ticket — see the identical guard/
  -- comment in SearchRepairableItemsAsync above.
  AND NOT EXISTS (
        SELECT 1 FROM dbo.RepairTicket rt
        WHERE rt.ItemId = i.ItemId AND rt.Status <> 'Completed'
      )"
                + (string.Equals(scope, "Set", StringComparison.OrdinalIgnoreCase) ? " AND aset.SetCode IS NOT NULL"
                   : string.Equals(scope, "Inventory", StringComparison.OrdinalIgnoreCase) ? " AND aset.SetCode IS NULL"
                   : string.Empty)
                + @"
  -- Currently Damaged only — having repair history alone (rh.RepairCount, still joined and
  -- shown/sorted below) no longer qualifies an otherwise-fine item for this list; an item that
  -- was repaired before and is Good now doesn't need another ticket just because of that history.
  AND LOWER(LTRIM(RTRIM(ISNULL(c.ConditionName, '')))) = 'damaged'
ORDER BY
    CASE WHEN LOWER(LTRIM(RTRIM(ISNULL(c.ConditionName, '')))) = 'damaged' THEN 0 ELSE 1 END,
    ISNULL(rh.RepairCount, 0) DESC,
    i.Name ASC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Top", top);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new RepairableItemLookup
                        {
                            ItemId = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1),
                            SerialNumber = GetStringOrNull(reader, 2),
                            ModelNumber = GetStringOrNull(reader, 3),
                            SetCode = GetStringOrNull(reader, 4),
                            CompanyName = GetStringOrNull(reader, 5),
                            BranchName = GetStringOrNull(reader, 6),
                            DeptName = GetStringOrNull(reader, 7),
                            RequestedByComId = GetIntOrNull(reader, 10),
                            RequestedByBranchId = GetIntOrNull(reader, 11),
                            RequestedByDeptId = GetIntOrNull(reader, 12),
                            RequestedByEmpId = GetIntOrNull(reader, 13),
                            RequestedByEmpName = GetStringOrNull(reader, 14)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Item picker for the "Replace" disposition on an Unrepairable ticket — hard-filters to the
        /// broken item's own CategoryId (looked up inline rather than passed in, so callers don't
        /// need to plumb it through) — a monitor shouldn't be offered as a replacement for a broken
        /// printer. With a blank query, shows the default browse list: same-category items not
        /// currently assigned to any active Set (i.e. free stock), same "current active set for
        /// item" join used by SearchRepairableItemsAsync/GetCandidateRepairItemsAsync above. This
        /// guard applies whether the query is blank or not — a replacement must never be an item
        /// that's currently issued to someone else via an active Set, even if it's found by typing
        /// its name/serial/model rather than browsing the default list. Paginated via
        /// pageNumber (1-based)/pageSize; TotalCount reflects the full filtered result set.
        /// </summary>
        public async Task<RepairableItemLookupPage> SearchReplacementCandidatesAsync(string query, int excludeItemId, int pageNumber = 1, int pageSize = 25)
        {
            var trimmedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 25 : pageSize;

            const string sql = @"
WITH Candidates AS (
    SELECT
        i.ItemId, i.Name, i.SerialNumber, i.ModelNumber, i.DateCreated, u.Name AS CreatedByName,
        aset.SetId AS CurrentSetId
    FROM dbo.Item i
    LEFT JOIN dbo.[User] u ON u.UserId = i.CreatedBy
    LEFT JOIN (
        SELECT
            x.ItemId, x.SetId,
            ROW_NUMBER() OVER (PARTITION BY x.ItemId ORDER BY x.SetCreatedAt DESC, x.SetId DESC) AS rn
        FROM (
            SELECT si.ItemId, s.SetId, s.CreatedAt AS SetCreatedAt
            FROM dbo.SetItem si
            INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE s.Active = 1 AND archS.EntityId IS NULL

            UNION ALL

            SELECT r.ItemId, s.SetId, s.CreatedAt AS SetCreatedAt
            FROM dbo.Request r
            INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
        ) x
    ) aset ON aset.ItemId = i.ItemId AND aset.rn = 1
    WHERE i.Active = 1
      AND i.ItemId <> @ExcludeItemId
      AND i.CategoryId = (SELECT CategoryId FROM dbo.Item WHERE ItemId = @ExcludeItemId)
      AND (i.ItemType IS NULL OR LTRIM(RTRIM(i.ItemType)) = '' OR LOWER(LTRIM(RTRIM(i.ItemType))) = 'hardware')
      AND (@Search IS NULL OR i.Name LIKE @Search OR i.SerialNumber LIKE @Search OR i.ModelNumber LIKE @Search)
)
SELECT ItemId, Name, SerialNumber, ModelNumber, DateCreated, CreatedByName, COUNT(*) OVER() AS TotalCount
FROM Candidates
WHERE CurrentSetId IS NULL
ORDER BY Name ASC, ItemId ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var page = new RepairableItemLookupPage();

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Search", trimmedQuery == null ? (object)DBNull.Value : "%" + trimmedQuery + "%");
                cmd.Parameters.AddWithValue("@ExcludeItemId", excludeItemId);
                cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        page.Items.Add(new RepairableItemLookup
                        {
                            ItemId = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1),
                            SerialNumber = GetStringOrNull(reader, 2),
                            ModelNumber = GetStringOrNull(reader, 3),
                            DateCreated = GetDateOrNull(reader, 4),
                            CreatedByName = GetStringOrNull(reader, 5)
                        });
                        page.TotalCount = reader.GetInt32(6);
                    }
                }
            }

            return page;
        }

        public async Task<List<OrgLookupOption>> GetCompanyOptionsAsync()
        {
            const string sql = "SELECT c.ComId AS Id, c.Name FROM dbo.Company c WHERE c.Active = 1 ORDER BY c.Name;";
            return await QueryOrgLookupAsync(sql);
        }

        public async Task<List<OrgLookupOption>> GetBranchOptionsAsync()
        {
            const string sql = "SELECT b.BranchId AS Id, b.Name FROM dbo.Branch b WHERE ISNULL(b.Active, 1) = 1 ORDER BY b.Name;";
            return await QueryOrgLookupAsync(sql);
        }

        public async Task<List<OrgLookupOption>> GetDepartmentOptionsAsync()
        {
            const string sql = "SELECT d.DeptId AS Id, d.Name FROM dbo.Department d WHERE d.Active = 1 ORDER BY d.Name;";
            return await QueryOrgLookupAsync(sql);
        }

        public async Task<List<OrgLookupOption>> GetTechnicianOptionsAsync()
        {
            const string sql = "SELECT e.EmpId AS Id, e.Name FROM dbo.Employee e WHERE e.Active = 1 ORDER BY e.Name;";
            return await QueryOrgLookupAsync(sql);
        }

        /// <summary>Candidate pool for the "Repaired By" multi-select — IT Dept. employees only
        /// (repairs are handled by IT), matched by partial department name the same way
        /// Helpers/RepairReportBuilder.FindDepartmentIdByNameAsync does, so both stay consistent if
        /// the department is ever renamed slightly (e.g. "...Department" vs "...Dept.").</summary>
        public async Task<List<OrgLookupOption>> GetItDepartmentEmployeesAsync()
        {
            const string sql = @"
SELECT e.EmpId AS Id, e.Name
FROM dbo.Employee e
INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
WHERE e.Active = 1 AND d.Active = 1 AND d.Name LIKE '%Information Technology%'
ORDER BY e.Name;";
            return await QueryOrgLookupAsync(sql);
        }

        /// <summary>Cascade step 1 (Company -> Branch) for the "Requested By: Department" picker —
        /// distinct Branches actually linked to the given Company via dbo.BranchDepartmentCompany,
        /// the authoritative valid-combination table (same one sp_Call_CreateTicket validates
        /// against). Mirrors the cascade concept in BatchAddRequestDialog, driven by real data
        /// instead of a flat unfiltered list.</summary>
        public async Task<List<OrgLookupOption>> GetBranchesForCompanyAsync(int comId)
        {
            const string sql = @"
SELECT DISTINCT b.BranchId AS Id, b.Name
FROM dbo.Branch b
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.BranchID = b.BranchId
WHERE bdc.CompanyID = @ComId
ORDER BY b.Name;";

            var list = new List<OrgLookupOption>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ComId", comId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new OrgLookupOption
                        {
                            Id = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>Cascade step 2 (Company [+ Branch] -> Department) for the "Requested By:
        /// Department" picker — distinct Departments actually linked to the given Company (and
        /// Branch, if chosen) via dbo.BranchDepartmentCompany. NULL DepartmentID rows (branch
        /// belongs to company but has no department, e.g. YMC-only rows) are excluded since this
        /// picker's whole purpose is choosing a Department.</summary>
        public async Task<List<OrgLookupOption>> GetDepartmentsForCompanyBranchAsync(int comId, int? branchId)
        {
            var sql = @"
SELECT DISTINCT d.DeptId AS Id, d.Name
FROM dbo.Department d
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.DepartmentID = d.DeptId
WHERE bdc.CompanyID = @ComId
  AND bdc.DepartmentID IS NOT NULL"
                + (branchId.HasValue ? " AND bdc.BranchID = @BranchId" : string.Empty)
                + @"
ORDER BY d.Name;";

            var list = new List<OrgLookupOption>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ComId", comId);
                if (branchId.HasValue)
                    cmd.Parameters.AddWithValue("@BranchId", branchId.Value);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new OrgLookupOption
                        {
                            Id = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>Employee options scoped to the Requested By Company/Branch/Department cascade —
        /// dbo.Employee carries ComId/BranchId/DeptId directly, so this narrows the same way
        /// GetDepartmentsForCompanyBranchAsync narrows Departments.</summary>
        public async Task<List<OrgLookupOption>> GetEmployeesForCompanyBranchDeptAsync(int comId, int? branchId, int? deptId)
        {
            var sql = "SELECT e.EmpId AS Id, e.Name FROM dbo.Employee e WHERE e.Active = 1 AND e.ComId = @ComId"
                + (branchId.HasValue ? " AND e.BranchId = @BranchId" : string.Empty)
                + (deptId.HasValue ? " AND e.DeptId = @DeptId" : string.Empty)
                + " ORDER BY e.Name;";

            var list = new List<OrgLookupOption>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ComId", comId);
                if (branchId.HasValue) cmd.Parameters.AddWithValue("@BranchId", branchId.Value);
                if (deptId.HasValue) cmd.Parameters.AddWithValue("@DeptId", deptId.Value);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new OrgLookupOption
                        {
                            Id = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1)
                        });
                    }
                }
            }

            return list;
        }

        private static async Task<List<OrgLookupOption>> QueryOrgLookupAsync(string sql)
        {
            var list = new List<OrgLookupOption>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new OrgLookupOption
                        {
                            Id = reader.GetInt32(0),
                            Name = GetStringOrNull(reader, 1)
                        });
                    }
                }
            }

            return list;
        }
    }
}
