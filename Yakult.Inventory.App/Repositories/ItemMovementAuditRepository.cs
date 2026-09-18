using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    public class ItemMovementAuditRepository
    {
        private static string NormalizeAuditNotes(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return notes;

            // Older request audit rows were written with the UTF-8 bullet decoded as
            // Windows-1252/Latin-1 text. Normalize those known forms at display time;
            // this avoids rewriting historical audit data while keeping new rows clean.
            return notes
                .Replace("Ã¢â‚¬Â¢", "\u2022")
                .Replace("â€¢", "\u2022");
        }

        private static string InjectBorrowEventsIntoTimelineSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            sql = sql.Replace(@"),
AuditEvents AS (", @"),
BorrowEvents AS (
    SELECT
        b.BorrowId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('BorrowLog' AS nvarchar(30)) AS ReferenceType,
        evt.EventTime AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        evt.ActionLabel AS AuditAction,
        CASE
            WHEN b.ReturnedAtUtc IS NULL THEN CAST('Open' AS nvarchar(100))
            ELSE CAST('Closed' AS nvarchar(100))
        END AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        evt.Direction AS Direction,
        b.SerialNumber,
        b.ItemId,
        COALESCE(it.Name, b.ItemName) AS ItemName,
        COALESCE(NULLIF(LTRIM(RTRIM(b.ModelNumber)), ''), it.ModelNumber) AS ModelNumber,
        CAST(NULL AS int) AS SetId,
        CAST(NULL AS nvarchar(100)) AS SetCode,
        1 AS Quantity,
        evt.EmployeeName AS EmployeeName,
        CAST(NULL AS nvarchar(200)) AS BranchName,
        evt.DepartmentName AS DepartmentName,
        bc.Name AS CompanyName,
        evt.UserName AS UserName,
        CAST('Borrow' AS nvarchar(30)) AS Source,
        evt.Notes AS Notes,
        CAST(CASE WHEN arcBor.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.BorrowLog b
    LEFT JOIN dbo.Item it ON it.ItemId = b.ItemId
    LEFT JOIN dbo.Employee be ON be.EmpId = b.BorrowedByEmpId
    LEFT JOIN dbo.Company bc ON bc.ComId = be.ComId
    LEFT JOIN dbo.ArchiveStatus arcBor ON arcBor.EntityType = 'Item' AND arcBor.EntityId = b.ItemId AND arcBor.IsArchived = 1
    CROSS APPLY (
        VALUES
        (
            b.BorrowedAtUtc,
            CAST('OUT' AS nvarchar(10)),
            CAST('Borrowed' AS nvarchar(200)),
            b.BorrowedByEmpName,
            b.BorrowedByDeptName,
            b.BorrowEncodedByUserName,
            CAST(
                'Borrowed by ' + ISNULL(NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), ''), 'Unknown')
                + CASE
                    WHEN b.BorrowedByDeptName IS NULL OR LTRIM(RTRIM(b.BorrowedByDeptName)) = '' THEN ''
                    ELSE ' | Department: ' + LTRIM(RTRIM(b.BorrowedByDeptName))
                  END
                + CASE
                    WHEN b.BorrowEncodedByUserName IS NULL OR LTRIM(RTRIM(b.BorrowEncodedByUserName)) = '' THEN ''
                    ELSE ' | Encoded by: ' + LTRIM(RTRIM(b.BorrowEncodedByUserName))
                  END
                AS nvarchar(max)
            )
        ),
        (
            b.ReturnedAtUtc,
            CAST('IN' AS nvarchar(10)),
            CAST('Returned' AS nvarchar(200)),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnedByEmpName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), '')),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnedByDeptName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowedByDeptName)), '')),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnEncodedByUserName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowEncodedByUserName)), '')),
            CAST(
                'Returned by ' + ISNULL(
                    NULLIF(LTRIM(RTRIM(b.ReturnedByEmpName)), ''),
                    ISNULL(NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), ''), 'Unknown'))
                + CASE
                    WHEN b.ReturnedByDeptName IS NULL OR LTRIM(RTRIM(b.ReturnedByDeptName)) = '' THEN ''
                    ELSE ' | Department: ' + LTRIM(RTRIM(b.ReturnedByDeptName))
                  END
                + CASE
                    WHEN b.ReturnEncodedByUserName IS NULL OR LTRIM(RTRIM(b.ReturnEncodedByUserName)) = '' THEN ''
                    ELSE ' | Encoded by: ' + LTRIM(RTRIM(b.ReturnEncodedByUserName))
                  END
                + CASE
                    WHEN b.BorrowedByEmpName IS NULL OR LTRIM(RTRIM(b.BorrowedByEmpName)) = '' THEN ''
                    ELSE ' | Original borrower: ' + LTRIM(RTRIM(b.BorrowedByEmpName))
                  END
                AS nvarchar(max)
            )
        )
    ) evt(EventTime, Direction, ActionLabel, EmployeeName, DepartmentName, UserName, Notes)
    WHERE evt.EventTime IS NOT NULL
),
AuditEvents AS (");

            return sql.Replace(@"    UNION ALL
    SELECT * FROM AuditEvents", @"    UNION ALL
    SELECT * FROM BorrowEvents
    UNION ALL
    SELECT * FROM AuditEvents");
        }

        private static string InjectBorrowEventsIntoLatestSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            sql = sql.Replace(@"),
AllEvents AS (", @"),
BorrowEvents AS (
    SELECT
        b.BorrowId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('BorrowLog' AS nvarchar(30)) AS ReferenceType,
        evt.EventTime AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        evt.ActionLabel AS AuditAction,
        CASE
            WHEN b.ReturnedAtUtc IS NULL THEN CAST('Open' AS nvarchar(100))
            ELSE CAST('Closed' AS nvarchar(100))
        END AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        evt.Direction AS Direction,
        b.SerialNumber,
        b.ItemId,
        COALESCE(it.Name, b.ItemName) AS ItemName,
        COALESCE(NULLIF(LTRIM(RTRIM(b.ModelNumber)), ''), it.ModelNumber) AS ModelNumber,
        CAST(NULL AS int) AS SetId,
        CAST(NULL AS nvarchar(100)) AS SetCode,
        1 AS Quantity,
        evt.EmployeeName AS EmployeeName,
        CAST(NULL AS nvarchar(200)) AS BranchName,
        evt.DepartmentName AS DepartmentName,
        bc.Name AS CompanyName,
        evt.UserName AS UserName,
        CAST('Borrow' AS nvarchar(30)) AS Source,
        evt.Notes AS Notes,
        CAST(CASE WHEN arcBor.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.BorrowLog b
    LEFT JOIN dbo.Item it ON it.ItemId = b.ItemId
    LEFT JOIN dbo.Employee be ON be.EmpId = b.BorrowedByEmpId
    LEFT JOIN dbo.Company bc ON bc.ComId = be.ComId
    LEFT JOIN dbo.ArchiveStatus arcBor ON arcBor.EntityType = 'Item' AND arcBor.EntityId = b.ItemId AND arcBor.IsArchived = 1
    CROSS APPLY (
        VALUES
        (
            b.BorrowedAtUtc,
            CAST('OUT' AS nvarchar(10)),
            CAST('Borrowed' AS nvarchar(200)),
            b.BorrowedByEmpName,
            b.BorrowedByDeptName,
            b.BorrowEncodedByUserName,
            CAST(
                'Borrowed by ' + ISNULL(NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), ''), 'Unknown')
                + CASE
                    WHEN b.BorrowedByDeptName IS NULL OR LTRIM(RTRIM(b.BorrowedByDeptName)) = '' THEN ''
                    ELSE ' | Department: ' + LTRIM(RTRIM(b.BorrowedByDeptName))
                  END
                + CASE
                    WHEN b.BorrowEncodedByUserName IS NULL OR LTRIM(RTRIM(b.BorrowEncodedByUserName)) = '' THEN ''
                    ELSE ' | Encoded by: ' + LTRIM(RTRIM(b.BorrowEncodedByUserName))
                  END
                AS nvarchar(max)
            )
        ),
        (
            b.ReturnedAtUtc,
            CAST('IN' AS nvarchar(10)),
            CAST('Returned' AS nvarchar(200)),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnedByEmpName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), '')),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnedByDeptName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowedByDeptName)), '')),
            COALESCE(NULLIF(LTRIM(RTRIM(b.ReturnEncodedByUserName)), ''), NULLIF(LTRIM(RTRIM(b.BorrowEncodedByUserName)), '')),
            CAST(
                'Returned by ' + ISNULL(
                    NULLIF(LTRIM(RTRIM(b.ReturnedByEmpName)), ''),
                    ISNULL(NULLIF(LTRIM(RTRIM(b.BorrowedByEmpName)), ''), 'Unknown'))
                + CASE
                    WHEN b.ReturnedByDeptName IS NULL OR LTRIM(RTRIM(b.ReturnedByDeptName)) = '' THEN ''
                    ELSE ' | Department: ' + LTRIM(RTRIM(b.ReturnedByDeptName))
                  END
                + CASE
                    WHEN b.ReturnEncodedByUserName IS NULL OR LTRIM(RTRIM(b.ReturnEncodedByUserName)) = '' THEN ''
                    ELSE ' | Encoded by: ' + LTRIM(RTRIM(b.ReturnEncodedByUserName))
                  END
                + CASE
                    WHEN b.BorrowedByEmpName IS NULL OR LTRIM(RTRIM(b.BorrowedByEmpName)) = '' THEN ''
                    ELSE ' | Original borrower: ' + LTRIM(RTRIM(b.BorrowedByEmpName))
                  END
                AS nvarchar(max)
            )
        )
    ) evt(EventTime, Direction, ActionLabel, EmployeeName, DepartmentName, UserName, Notes)
    WHERE evt.EventTime IS NOT NULL
),
AllEvents AS (");

            return sql.Replace(@"    UNION ALL
    SELECT * FROM AuditEvents", @"    UNION ALL
    SELECT * FROM BorrowEvents
    UNION ALL
    SELECT * FROM AuditEvents");
        }

        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public ItemMovementAuditRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public async Task<List<ItemMovementAuditDto>> GetTimelineAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string direction = null,
            string serial = null,
            int? itemId = null,
            string setCode = null,
            string source = null,
            string userName = null,
            int top = 500)
        {
            const string baseSql = @"
WITH InvEvents AS (
    SELECT
        inv.InvId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('Inventory' AS nvarchar(30)) AS ReferenceType,
        inv.DatePosted AS EventTime,
        inv.EntryType AS InventoryEntryType,
        r.Status AS RequestStatus,
        r.EntryType AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CASE
            WHEN inv.EntryType = 'Positive' THEN 'IN'
            WHEN inv.EntryType = 'Negative' THEN 'OUT'
            ELSE inv.EntryType
        END AS Direction,
        it.SerialNumber,
        inv.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        inv.SetId,
        s.SetCode,
        inv.Quantity,
        emp.Name AS EmployeeName,
        b.Name AS BranchName,
        d.Name AS DepartmentName,
        co.Name AS CompanyName,
        u.Name AS UserName,
        CASE
            WHEN inv.ReqId IS NOT NULL THEN 'Request'
            ELSE 'Inventory'
        END AS Source,
        inv.Description AS Notes,
        CAST(CASE WHEN arcInv.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.Inventory inv
    LEFT JOIN dbo.Item it ON inv.ItemId = it.ItemId
    LEFT JOIN dbo.[Set] s ON inv.SetId = s.SetId
    LEFT JOIN dbo.[User] u ON inv.PostedBy = u.UserId
    LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
    LEFT JOIN dbo.Employee emp ON r.EmpId = emp.EmpId
    LEFT JOIN dbo.Branch b ON emp.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON emp.DeptId = d.DeptId
    LEFT JOIN dbo.Company co ON co.ComId = COALESCE(r.ComId, emp.ComId, s.ComId)
    LEFT JOIN dbo.ArchiveStatus arcInv ON arcInv.EntityType = 'Item' AND arcInv.EntityId = inv.ItemId AND arcInv.IsArchived = 1
),
ReqOnly AS (
    SELECT
        r.ReqId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('Request' AS nvarchar(30)) AS ReferenceType,
        r.DateCreated AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        r.Status AS RequestStatus,
        r.EntryType AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CASE
            WHEN r.EntryType = 'Positive' THEN 'IN'
            WHEN r.EntryType = 'Negative' THEN 'OUT'
            ELSE 'OUT'
        END AS Direction,
        it.SerialNumber,
        r.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        r.SetId,
        s.SetCode,
        r.Quantity,
        emp.Name AS EmployeeName,
        b.Name AS BranchName,
        d.Name AS DepartmentName,
        co.Name AS CompanyName,
        u.Name AS UserName,
        CAST('Request' AS nvarchar(30)) AS Source,
        CAST(ISNULL(r.Description, '') + CASE WHEN r.Remarks IS NULL OR LTRIM(RTRIM(r.Remarks)) = '' THEN '' ELSE ' - ' + r.Remarks END AS nvarchar(max)) AS Notes,
        CAST(CASE WHEN arcReq.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.Request r
    LEFT JOIN dbo.Item it ON r.ItemId = it.ItemId
    LEFT JOIN dbo.[Set] s ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee emp ON r.EmpId = emp.EmpId
    LEFT JOIN dbo.Branch b ON emp.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON emp.DeptId = d.DeptId
    LEFT JOIN dbo.Company co ON co.ComId = COALESCE(r.ComId, emp.ComId, s.ComId)
    LEFT JOIN dbo.[User] u ON r.Createdby = u.UserId
    LEFT JOIN dbo.ArchiveStatus arcReq ON arcReq.EntityType = 'Item' AND arcReq.EntityId = r.ItemId AND arcReq.IsArchived = 1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Inventory inv WHERE inv.ReqId = r.ReqId)
),
UpdateEvents AS (
    SELECT
        u.UpdateId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('SetItemUpdate' AS nvarchar(30)) AS ReferenceType,
        u.CreatedAt AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CAST('IN' AS nvarchar(10)) AS Direction,
        u.SerialNumber,
        COALESCE(NULLIF(u.ItemId, 0), itemLookup.ItemId) AS ItemId,
        itemLookup.ItemName AS ItemName,
        COALESCE(NULLIF(LTRIM(RTRIM(u.ModelNumber)), ''), itemLookup.ModelNumber) AS ModelNumber,
        COALESCE(u.SetId, setLookup.SetId) AS SetId,
        COALESCE(NULLIF(LTRIM(RTRIM(u.SetCode)), ''), setLookup.SetCode) AS SetCode,
        1 AS Quantity,
        loc.EmployeeName AS EmployeeName,
        loc.BranchName,
        loc.DepartmentName,
        loc.CompanyName,
        u.UpdatedByName AS UserName,
        CASE
            WHEN u.Source IS NULL OR LTRIM(RTRIM(u.Source)) = '' THEN 'MobileUpdate'
            ELSE u.Source
        END AS Source,
        CAST(
            'Action: ' + ISNULL(NULLIF(LTRIM(RTRIM(u.NewStatus)), ''), 'Update')
            + ' • From: ' + ISNULL(NULLIF(LTRIM(RTRIM(u.PreviousStatus)), ''), '—')
            + CASE
                WHEN u.Remark IS NULL OR LTRIM(RTRIM(u.Remark)) = '' THEN ''
                ELSE ' • ' + u.Remark
              END
            + CASE WHEN u.Processed = 1 THEN ' (Processed)' ELSE ' (Unprocessed)' END
            AS nvarchar(max)
        ) AS Notes,
        CAST(CASE WHEN arcUpd.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.SetItemUpdate u
    OUTER APPLY (
        SELECT TOP (1)
            i.ItemId,
            i.Name AS ItemName,
            i.ModelNumber
        FROM dbo.Item i
        WHERE (NULLIF(u.ItemId, 0) IS NOT NULL AND i.ItemId = NULLIF(u.ItemId, 0))
           OR (u.ItemId IS NULL AND u.SerialNumber IS NOT NULL AND i.SerialNumber = u.SerialNumber)
        ORDER BY CASE WHEN NULLIF(u.ItemId, 0) IS NOT NULL AND i.ItemId = NULLIF(u.ItemId, 0) THEN 0 ELSE 1 END
    ) itemLookup
    LEFT JOIN dbo.ArchiveStatus arcUpd ON arcUpd.EntityType = 'Item' AND arcUpd.EntityId = COALESCE(NULLIF(u.ItemId, 0), itemLookup.ItemId) AND arcUpd.IsArchived = 1
    OUTER APPLY (
        SELECT TOP (1)
            s.SetId,
            s.SetCode
        FROM dbo.[Set] s
        WHERE (u.SetId IS NOT NULL AND s.SetId = u.SetId)
           OR (u.SetId IS NULL AND u.SetCode IS NOT NULL AND LTRIM(RTRIM(u.SetCode)) <> '' AND s.SetCode = u.SetCode)
        ORDER BY s.SetId DESC
    ) setLookup
    OUTER APPLY (
        SELECT TOP (1)
            e.Name AS EmployeeName,
            b.Name AS BranchName,
            d.Name AS DepartmentName,
            co.Name AS CompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.Request r ON r.SetId = s.SetId
        LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
        LEFT JOIN dbo.Company co ON co.ComId = COALESCE(s.ComId, r.ComId, e.ComId)
        WHERE (COALESCE(u.SetId, setLookup.SetId) IS NOT NULL AND s.SetId = COALESCE(u.SetId, setLookup.SetId))
           OR (COALESCE(u.SetId, setLookup.SetId) IS NULL AND u.SetCode IS NOT NULL AND LTRIM(RTRIM(u.SetCode)) <> '' AND s.SetCode = u.SetCode)
    ) loc
),
RepairEvents AS (
    SELECT
        h.RepairId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('ItemRepairHistory' AS nvarchar(30)) AS ReferenceType,
        h.CreatedAt AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        h.RepairAction AS AuditAction,
        h.ConditionName AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CAST(NULL AS nvarchar(10)) AS Direction,
        h.SerialNumber,
        h.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        h.SetId,
        h.SetCode,
        1 AS Quantity,
        loc.EmployeeName AS EmployeeName,
        loc.BranchName,
        loc.DepartmentName,
        loc.CompanyName,
        h.ProcessedByName AS UserName,
        CAST('RepairHistory' AS nvarchar(30)) AS Source,
        CAST(
            'Repair: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.RepairAction)), ''), 'Update')
            + ' • Condition: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.ConditionName)), ''), '—')
            + ' • From: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.PreviousStatus)), ''), '—')
            + ' • To: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.NewStatus)), ''), '—')
            + CASE
                WHEN h.SetCode IS NULL OR LTRIM(RTRIM(h.SetCode)) = '' THEN ''
                ELSE ' • Ref: ' + LTRIM(RTRIM(h.SetCode))
              END
            + CASE
                WHEN h.Remark IS NULL OR LTRIM(RTRIM(h.Remark)) = '' THEN ''
                ELSE ' • ' + h.Remark
              END
            AS nvarchar(max)
        ) AS Notes,
        CAST(CASE WHEN arcRep.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.ItemRepairHistory h
    LEFT JOIN dbo.Item it ON it.ItemId = h.ItemId
    LEFT JOIN dbo.ArchiveStatus arcRep ON arcRep.EntityType = 'Item' AND arcRep.EntityId = h.ItemId AND arcRep.IsArchived = 1
    OUTER APPLY (
        SELECT TOP (1)
            e.Name AS EmployeeName,
            b.Name AS BranchName,
            d.Name AS DepartmentName,
            co.Name AS CompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.Request r ON r.SetId = s.SetId
        LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
        LEFT JOIN dbo.Company co ON co.ComId = COALESCE(s.ComId, r.ComId, e.ComId)
        WHERE (h.SetId IS NOT NULL AND s.SetId = h.SetId)
           OR (h.SetId IS NULL AND h.SetCode IS NOT NULL AND s.SetCode = h.SetCode)
    ) loc
),
AuditEvents AS (
    SELECT
        a.Id AS ReferenceId,
        a.ReferenceType AS AuditReferenceType,
        a.ReferenceId AS AuditReferenceId,
        CAST('ItemAuditTrail' AS nvarchar(30)) AS ReferenceType,
        a.ActionTime AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        a.Action AS AuditAction,
        a.Status AS AuditStatus,
        a.Location AS AuditLocation,
        a.Direction AS Direction,
        a.SerialNumber,
        a.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        s.SetId,
        a.SetCode,
        1 AS Quantity,
        COALESCE(NULLIF(LTRIM(RTRIM(a.EmployeeName)), ''), setReceived.Name, reqEmp.Name) AS EmployeeName,
        COALESCE(NULLIF(LTRIM(RTRIM(a.BranchName)), ''), setBranch.Name, reqBranch.Name, empBranch.Name) AS BranchName,
        COALESCE(NULLIF(LTRIM(RTRIM(a.DepartmentName)), ''), setDept.Name, reqDept.Name, empDept.Name) AS DepartmentName,
        COALESCE(setCompany.Name, reqCompany.Name, empCompany.Name) AS CompanyName,
        a.CreatedBy AS UserName,
        CAST('AuditTrail' AS nvarchar(30)) AS Source,
        a.Notes AS Notes,
        CAST(CASE WHEN arcAud.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.ItemAuditTrail a
    LEFT JOIN dbo.Item it ON a.ItemId = it.ItemId
    LEFT JOIN dbo.ArchiveStatus arcAud ON arcAud.EntityType = 'Item' AND arcAud.EntityId = a.ItemId AND arcAud.IsArchived = 1
    LEFT JOIN dbo.[Set] s ON s.SetCode = a.SetCode
    OUTER APPLY (
        SELECT TOP (1) r.EmpId, r.ComId, r.BranchId, r.DeptId
        FROM dbo.Request r
        WHERE (s.SetId IS NOT NULL AND r.SetId = s.SetId)
           OR (a.ReferenceType = 'Request' AND a.ReferenceId = r.ReqId)
           OR (a.ItemId IS NOT NULL AND r.ItemId = a.ItemId AND (a.SetCode IS NULL OR r.SetId = s.SetId))
        ORDER BY
            CASE WHEN a.ItemId IS NOT NULL AND r.ItemId = a.ItemId THEN 0 ELSE 1 END,
            r.ReqId DESC
    ) req
    LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = req.EmpId
    LEFT JOIN dbo.Employee setReceived ON setReceived.EmpId = s.ReceivedById
    LEFT JOIN dbo.Branch setBranch ON setBranch.BranchId = s.CurrentBranchId
    LEFT JOIN dbo.Department setDept ON setDept.DeptId = s.CurrentDepartmentId
    LEFT JOIN dbo.Company setCompany ON setCompany.ComId = s.ComId
    LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId = req.BranchId
    LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = req.DeptId
    LEFT JOIN dbo.Company reqCompany ON reqCompany.ComId = req.ComId
    LEFT JOIN dbo.Branch empBranch ON empBranch.BranchId = reqEmp.BranchId
    LEFT JOIN dbo.Department empDept ON empDept.DeptId = reqEmp.DeptId
    LEFT JOIN dbo.Company empCompany ON empCompany.ComId = reqEmp.ComId
)
SELECT TOP (@Top)
    EventTime,
    InventoryEntryType,
    RequestStatus,
    RequestEntryType,
    AuditAction,
    AuditStatus,
    AuditLocation,
    Direction,
    SerialNumber,
    ItemId,
    ItemName,
    ModelNumber,
    SetId,
    SetCode,
    Quantity,
    EmployeeName,
    BranchName,
    DepartmentName,
    CompanyName,
    UserName,
    Source,
    ReferenceType,
    ReferenceId,
    AuditReferenceType,
    AuditReferenceId,
    Notes,
    IsArchived
FROM (
    SELECT * FROM InvEvents
    UNION ALL
    SELECT * FROM ReqOnly
    UNION ALL
    SELECT * FROM UpdateEvents
    UNION ALL
    SELECT * FROM RepairEvents
    UNION ALL
    SELECT * FROM AuditEvents
) x
WHERE 1=1
  AND (@FromDate IS NULL OR x.EventTime >= @FromDate)
  AND (@ToDate IS NULL OR x.EventTime < DATEADD(DAY, 1, @ToDate))
  AND (@Direction IS NULL OR @Direction = '' OR @Direction = 'All' OR x.Direction = @Direction)
  AND (@Serial IS NULL OR @Serial = '' OR x.SerialNumber LIKE @Serial)
  AND (@ItemId IS NULL OR x.ItemId = @ItemId)
  AND (@SetCode IS NULL OR @SetCode = '' OR x.SetCode LIKE @SetCode)
  AND (
        @Source IS NULL OR @Source = '' OR @Source = 'All'
        OR x.Source = @Source
        OR (
            @Source = 'ITCM'
            AND (
                x.Source = 'CallMonitoring'
                OR (x.ReferenceType = 'Inventory' AND (x.Notes LIKE '%Call ticket%' OR x.Notes LIKE '%Call Ticket%'))
                OR (x.ReferenceType = 'ItemAuditTrail' AND (
                        x.AuditAction LIKE 'Call Ticket %'
                        OR x.Notes LIKE '%Call ticket%'
                        OR x.Notes LIKE '%Call Ticket%'
                    ))
            )
        )
        OR (
            @Source = 'RepairHistory'
            AND (x.Source = 'RepairHistory' OR x.ReferenceType = 'ItemRepairHistory')
        )
      )
  AND (@UserName IS NULL OR @UserName = '' OR x.UserName LIKE @UserName)
ORDER BY x.EventTime DESC, x.ReferenceType DESC, x.ReferenceId DESC;";

            var results = new List<ItemMovementAuditDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync().ConfigureAwait(false);
                var includeBorrow = await CallSchemaGate.TableExistsAsync(con, "dbo.BorrowLog").ConfigureAwait(false);
                var sql = includeBorrow ? InjectBorrowEventsIntoTimelineSql(baseSql) : baseSql;

                using (var cmd = new SqlCommand(sql, con))
                {
                cmd.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = top });
                cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = (object)fromDate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = (object)toDate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@Direction", SqlDbType.NVarChar, 10) { Value = (object)direction ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@Serial", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(serial) ? (object)DBNull.Value : ("%" + serial.Trim() + "%")
                });
                cmd.Parameters.Add(new SqlParameter("@ItemId", SqlDbType.Int) { Value = (object)itemId ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@SetCode", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(setCode) ? (object)DBNull.Value : ("%" + setCode.Trim() + "%")
                });
                cmd.Parameters.Add(new SqlParameter("@Source", SqlDbType.NVarChar, 30) { Value = (object)source ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@UserName", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(userName) ? (object)DBNull.Value : ("%" + userName.Trim() + "%")
                });
                using (var rdr = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rdr.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(new ItemMovementAuditDto
                        {
                            EventTime = rdr["EventTime"] == DBNull.Value ? DateTime.MinValue : (DateTime)rdr["EventTime"],
                            InventoryEntryType = rdr["InventoryEntryType"] as string,
                            RequestStatus = rdr["RequestStatus"] as string,
                            RequestEntryType = rdr["RequestEntryType"] as string,
                            AuditAction = rdr["AuditAction"] as string,
                            AuditStatus = rdr["AuditStatus"] as string,
                            AuditLocation = rdr["AuditLocation"] as string,
                            Direction = rdr["Direction"] as string,
                            SerialNumber = rdr["SerialNumber"] as string,
                            ItemId = rdr["ItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ItemId"]),
                            ItemName = rdr["ItemName"] as string,
                            ModelNumber = rdr["ModelNumber"] as string,
                            SetId = rdr["SetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["SetId"]),
                            SetCode = rdr["SetCode"] as string,
                            Quantity = rdr["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["Quantity"]),
                            EmployeeName = rdr["EmployeeName"] as string,
                            BranchName = rdr["BranchName"] as string,
                            DepartmentName = rdr["DepartmentName"] as string,
                            CompanyName = rdr["CompanyName"] as string,
                            UserName = rdr["UserName"] as string,
                            Source = rdr["Source"] as string,
                            ReferenceType = rdr["ReferenceType"] as string,
                            ReferenceId = rdr["ReferenceId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ReferenceId"]),
                            AuditReferenceType = rdr["AuditReferenceType"] as string,
                            AuditReferenceId = rdr["AuditReferenceId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["AuditReferenceId"]),
                            Notes = NormalizeAuditNotes(rdr["Notes"] as string),
                            IsArchived = rdr["IsArchived"] != DBNull.Value && Convert.ToBoolean(rdr["IsArchived"])
                        });
                    }
                }
                }
            }

            return results;
        }

        public async Task<List<ItemMovementAuditDto>> GetLatestPerSerialAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string direction = null,
            string serial = null,
            int? itemId = null,
            string setCode = null,
            string source = null,
            string userName = null,
            int top = 500)
        {
            const string baseSql = @"
WITH InvEvents AS (
    SELECT
        inv.InvId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('Inventory' AS nvarchar(30)) AS ReferenceType,
        inv.DatePosted AS EventTime,
        inv.EntryType AS InventoryEntryType,
        r.Status AS RequestStatus,
        r.EntryType AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CASE
            WHEN inv.EntryType = 'Positive' THEN 'IN'
            WHEN inv.EntryType = 'Negative' THEN 'OUT'
            ELSE inv.EntryType
        END AS Direction,
        it.SerialNumber,
        inv.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        inv.SetId,
        s.SetCode,
        inv.Quantity,
        emp.Name AS EmployeeName,
        b.Name AS BranchName,
        d.Name AS DepartmentName,
        co.Name AS CompanyName,
        u.Name AS UserName,
        CASE
            WHEN inv.ReqId IS NOT NULL THEN 'Request'
            ELSE 'Inventory'
        END AS Source,
        inv.Description AS Notes,
        CAST(CASE WHEN arcInv.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.Inventory inv
    LEFT JOIN dbo.Item it ON inv.ItemId = it.ItemId
    LEFT JOIN dbo.[Set] s ON inv.SetId = s.SetId
    LEFT JOIN dbo.[User] u ON inv.PostedBy = u.UserId
    LEFT JOIN dbo.Request r ON inv.ReqId = r.ReqId
    LEFT JOIN dbo.Employee emp ON r.EmpId = emp.EmpId
    LEFT JOIN dbo.Branch b ON emp.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON emp.DeptId = d.DeptId
    LEFT JOIN dbo.Company co ON co.ComId = COALESCE(r.ComId, emp.ComId, s.ComId)
    LEFT JOIN dbo.ArchiveStatus arcInv ON arcInv.EntityType = 'Item' AND arcInv.EntityId = inv.ItemId AND arcInv.IsArchived = 1
),
ReqOnly AS (
    SELECT
        r.ReqId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('Request' AS nvarchar(30)) AS ReferenceType,
        r.DateCreated AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        r.Status AS RequestStatus,
        r.EntryType AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CASE
            WHEN r.EntryType = 'Positive' THEN 'IN'
            WHEN r.EntryType = 'Negative' THEN 'OUT'
            ELSE 'OUT'
        END AS Direction,
        it.SerialNumber,
        r.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        r.SetId,
        s.SetCode,
        r.Quantity,
        emp.Name AS EmployeeName,
        b.Name AS BranchName,
        d.Name AS DepartmentName,
        co.Name AS CompanyName,
        u.Name AS UserName,
        CAST('Request' AS nvarchar(30)) AS Source,
        CAST(ISNULL(r.Description, '') + CASE WHEN r.Remarks IS NULL OR LTRIM(RTRIM(r.Remarks)) = '' THEN '' ELSE ' - ' + r.Remarks END AS nvarchar(max)) AS Notes,
        CAST(CASE WHEN arcReq.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.Request r
    LEFT JOIN dbo.Item it ON r.ItemId = it.ItemId
    LEFT JOIN dbo.[Set] s ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee emp ON r.EmpId = emp.EmpId
    LEFT JOIN dbo.Branch b ON emp.BranchId = b.BranchId
    LEFT JOIN dbo.Department d ON emp.DeptId = d.DeptId
    LEFT JOIN dbo.Company co ON co.ComId = COALESCE(r.ComId, emp.ComId, s.ComId)
    LEFT JOIN dbo.[User] u ON r.Createdby = u.UserId
    LEFT JOIN dbo.ArchiveStatus arcReq ON arcReq.EntityType = 'Item' AND arcReq.EntityId = r.ItemId AND arcReq.IsArchived = 1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Inventory inv WHERE inv.ReqId = r.ReqId)
),
UpdateEvents AS (
    SELECT
        u.UpdateId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('SetItemUpdate' AS nvarchar(30)) AS ReferenceType,
        u.CreatedAt AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        CAST(NULL AS nvarchar(200)) AS AuditAction,
        CAST(NULL AS nvarchar(100)) AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CAST('IN' AS nvarchar(10)) AS Direction,
        u.SerialNumber,
        COALESCE(NULLIF(u.ItemId, 0), itemLookup.ItemId) AS ItemId,
        itemLookup.ItemName AS ItemName,
        COALESCE(NULLIF(LTRIM(RTRIM(u.ModelNumber)), ''), itemLookup.ModelNumber) AS ModelNumber,
        u.SetId,
        u.SetCode,
        1 AS Quantity,
        loc.EmployeeName AS EmployeeName,
        loc.BranchName,
        loc.DepartmentName,
        loc.CompanyName,
        u.UpdatedByName AS UserName,
        CASE
            WHEN u.Source IS NULL OR LTRIM(RTRIM(u.Source)) = '' THEN 'MobileUpdate'
            ELSE u.Source
        END AS Source,
        CAST(
            'Action: ' + ISNULL(NULLIF(LTRIM(RTRIM(u.NewStatus)), ''), 'Update')
            + ' • From: ' + ISNULL(NULLIF(LTRIM(RTRIM(u.PreviousStatus)), ''), '—')
            + CASE
                WHEN u.Remark IS NULL OR LTRIM(RTRIM(u.Remark)) = '' THEN ''
                ELSE ' • ' + u.Remark
              END
            + CASE WHEN u.Processed = 1 THEN ' (Processed)' ELSE ' (Unprocessed)' END
            AS nvarchar(max)
        ) AS Notes,
        CAST(CASE WHEN arcUpd.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.SetItemUpdate u
    OUTER APPLY (
        SELECT TOP (1)
            i.ItemId,
            i.Name AS ItemName,
            i.ModelNumber
        FROM dbo.Item i
        WHERE (NULLIF(u.ItemId, 0) IS NOT NULL AND i.ItemId = NULLIF(u.ItemId, 0))
           OR (u.ItemId IS NULL AND u.SerialNumber IS NOT NULL AND i.SerialNumber = u.SerialNumber)
        ORDER BY CASE WHEN NULLIF(u.ItemId, 0) IS NOT NULL AND i.ItemId = NULLIF(u.ItemId, 0) THEN 0 ELSE 1 END
    ) itemLookup
    LEFT JOIN dbo.ArchiveStatus arcUpd ON arcUpd.EntityType = 'Item' AND arcUpd.EntityId = COALESCE(NULLIF(u.ItemId, 0), itemLookup.ItemId) AND arcUpd.IsArchived = 1
    OUTER APPLY (
        SELECT TOP (1)
            e.Name AS EmployeeName,
            b.Name AS BranchName,
            d.Name AS DepartmentName,
            co.Name AS CompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.Request r ON r.SetId = s.SetId
        LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
        LEFT JOIN dbo.Company co ON co.ComId = COALESCE(s.ComId, r.ComId, e.ComId)
        WHERE s.SetId = u.SetId
    ) loc
),
RepairEvents AS (
    SELECT
        h.RepairId AS ReferenceId,
        CAST(NULL AS nvarchar(30)) AS AuditReferenceType,
        CAST(NULL AS int) AS AuditReferenceId,
        CAST('ItemRepairHistory' AS nvarchar(30)) AS ReferenceType,
        h.CreatedAt AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        h.RepairAction AS AuditAction,
        h.ConditionName AS AuditStatus,
        CAST(NULL AS nvarchar(200)) AS AuditLocation,
        CAST(NULL AS nvarchar(10)) AS Direction,
        h.SerialNumber,
        h.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        h.SetId,
        h.SetCode,
        1 AS Quantity,
        loc.EmployeeName AS EmployeeName,
        loc.BranchName,
        loc.DepartmentName,
        loc.CompanyName,
        h.ProcessedByName AS UserName,
        CAST('RepairHistory' AS nvarchar(30)) AS Source,
        CAST(
            'Repair: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.RepairAction)), ''), 'Update')
            + ' • Condition: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.ConditionName)), ''), '—')
            + ' • From: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.PreviousStatus)), ''), '—')
            + ' • To: ' + ISNULL(NULLIF(LTRIM(RTRIM(h.NewStatus)), ''), '—')
            + CASE
                WHEN h.SetCode IS NULL OR LTRIM(RTRIM(h.SetCode)) = '' THEN ''
                ELSE ' • Ref: ' + LTRIM(RTRIM(h.SetCode))
              END
            + CASE
                WHEN h.Remark IS NULL OR LTRIM(RTRIM(h.Remark)) = '' THEN ''
                ELSE ' • ' + h.Remark
              END
            AS nvarchar(max)
        ) AS Notes,
        CAST(CASE WHEN arcRep.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.ItemRepairHistory h
    LEFT JOIN dbo.Item it ON it.ItemId = h.ItemId
    LEFT JOIN dbo.ArchiveStatus arcRep ON arcRep.EntityType = 'Item' AND arcRep.EntityId = h.ItemId AND arcRep.IsArchived = 1
    OUTER APPLY (
        SELECT TOP (1)
            e.Name AS EmployeeName,
            b.Name AS BranchName,
            d.Name AS DepartmentName,
            co.Name AS CompanyName
        FROM dbo.[Set] s
        LEFT JOIN dbo.Request r ON r.SetId = s.SetId
        LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
        LEFT JOIN dbo.Company co ON co.ComId = COALESCE(s.ComId, r.ComId, e.ComId)
        WHERE (h.SetId IS NOT NULL AND s.SetId = h.SetId)
           OR (h.SetId IS NULL AND h.SetCode IS NOT NULL AND s.SetCode = h.SetCode)
    ) loc
),
AuditEvents AS (
    SELECT
        a.Id AS ReferenceId,
        a.ReferenceType AS AuditReferenceType,
        a.ReferenceId AS AuditReferenceId,
        CAST('ItemAuditTrail' AS nvarchar(30)) AS ReferenceType,
        a.ActionTime AS EventTime,
        CAST(NULL AS nvarchar(50)) AS InventoryEntryType,
        CAST(NULL AS nvarchar(50)) AS RequestStatus,
        CAST(NULL AS nvarchar(50)) AS RequestEntryType,
        a.Action AS AuditAction,
        a.Status AS AuditStatus,
        a.Location AS AuditLocation,
        a.Direction AS Direction,
        a.SerialNumber,
        a.ItemId,
        it.Name AS ItemName,
        it.ModelNumber,
        s.SetId,
        a.SetCode,
        1 AS Quantity,
        COALESCE(NULLIF(LTRIM(RTRIM(a.EmployeeName)), ''), setReceived.Name, reqEmp.Name) AS EmployeeName,
        COALESCE(NULLIF(LTRIM(RTRIM(a.BranchName)), ''), setBranch.Name, reqBranch.Name, empBranch.Name) AS BranchName,
        COALESCE(NULLIF(LTRIM(RTRIM(a.DepartmentName)), ''), setDept.Name, reqDept.Name, empDept.Name) AS DepartmentName,
        COALESCE(setCompany.Name, reqCompany.Name, empCompany.Name) AS CompanyName,
        a.CreatedBy AS UserName,
        CAST('AuditTrail' AS nvarchar(30)) AS Source,
        a.Notes AS Notes,
        CAST(CASE WHEN arcAud.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsArchived
    FROM dbo.ItemAuditTrail a
    LEFT JOIN dbo.Item it ON a.ItemId = it.ItemId
    LEFT JOIN dbo.ArchiveStatus arcAud ON arcAud.EntityType = 'Item' AND arcAud.EntityId = a.ItemId AND arcAud.IsArchived = 1
    LEFT JOIN dbo.[Set] s ON s.SetCode = a.SetCode
    OUTER APPLY (
        SELECT TOP (1) r.EmpId, r.ComId, r.BranchId, r.DeptId
        FROM dbo.Request r
        WHERE (s.SetId IS NOT NULL AND r.SetId = s.SetId)
           OR (a.ReferenceType = 'Request' AND a.ReferenceId = r.ReqId)
           OR (a.ItemId IS NOT NULL AND r.ItemId = a.ItemId AND (a.SetCode IS NULL OR r.SetId = s.SetId))
        ORDER BY
            CASE WHEN a.ItemId IS NOT NULL AND r.ItemId = a.ItemId THEN 0 ELSE 1 END,
            r.ReqId DESC
    ) req
    LEFT JOIN dbo.Employee reqEmp ON reqEmp.EmpId = req.EmpId
    LEFT JOIN dbo.Employee setReceived ON setReceived.EmpId = s.ReceivedById
    LEFT JOIN dbo.Branch setBranch ON setBranch.BranchId = s.CurrentBranchId
    LEFT JOIN dbo.Department setDept ON setDept.DeptId = s.CurrentDepartmentId
    LEFT JOIN dbo.Company setCompany ON setCompany.ComId = s.ComId
    LEFT JOIN dbo.Branch reqBranch ON reqBranch.BranchId = req.BranchId
    LEFT JOIN dbo.Department reqDept ON reqDept.DeptId = req.DeptId
    LEFT JOIN dbo.Company reqCompany ON reqCompany.ComId = req.ComId
    LEFT JOIN dbo.Branch empBranch ON empBranch.BranchId = reqEmp.BranchId
    LEFT JOIN dbo.Department empDept ON empDept.DeptId = reqEmp.DeptId
    LEFT JOIN dbo.Company empCompany ON empCompany.ComId = reqEmp.ComId
),
AllEvents AS (
    SELECT * FROM InvEvents
    UNION ALL
    SELECT * FROM ReqOnly
    UNION ALL
    SELECT * FROM UpdateEvents
    UNION ALL
    SELECT * FROM RepairEvents
    UNION ALL
    SELECT * FROM AuditEvents
),
Ranked AS (
    SELECT
        x.*, 
        ROW_NUMBER() OVER (
            PARTITION BY CASE
                WHEN x.SerialNumber IS NULL OR LTRIM(RTRIM(x.SerialNumber)) = ''
                    THEN CASE
                        WHEN x.ItemId IS NULL THEN 'Ref:' + ISNULL(x.ReferenceType, '') + ':'
                            + CAST(ISNULL(x.ReferenceId, 0) AS nvarchar(50)) + ':' + CONVERT(nvarchar(30), x.EventTime, 126)
                        ELSE 'ItemId:' + CAST(x.ItemId AS nvarchar(50))
                    END
                ELSE 'Serial:' + LTRIM(RTRIM(x.SerialNumber))
            END
            ORDER BY x.EventTime DESC, x.ReferenceId DESC
        ) AS rn
    FROM AllEvents x
    WHERE 1=1
      AND (@FromDate IS NULL OR x.EventTime >= @FromDate)
      AND (@ToDate IS NULL OR x.EventTime < DATEADD(DAY, 1, @ToDate))
      AND (@Direction IS NULL OR @Direction = '' OR @Direction = 'All' OR x.Direction = @Direction)
      AND (@Serial IS NULL OR @Serial = '' OR x.SerialNumber LIKE @Serial)
      AND (@ItemId IS NULL OR x.ItemId = @ItemId)
      AND (@SetCode IS NULL OR @SetCode = '' OR x.SetCode LIKE @SetCode)
      AND (
            @Source IS NULL OR @Source = '' OR @Source = 'All'
            OR x.Source = @Source
            OR (
                @Source = 'ITCM'
                AND (
                    x.Source = 'CallMonitoring'
                    OR (x.ReferenceType = 'Inventory' AND (x.Notes LIKE '%Call ticket%' OR x.Notes LIKE '%Call Ticket%'))
                    OR (x.ReferenceType = 'ItemAuditTrail' AND (
                            x.AuditAction LIKE 'Call Ticket %'
                            OR x.Notes LIKE '%Call ticket%'
                            OR x.Notes LIKE '%Call Ticket%'
                        ))
                )
            )
            OR (
                @Source = 'RepairHistory'
                AND (x.Source = 'RepairHistory' OR x.ReferenceType = 'ItemRepairHistory')
            )
          )
      AND (@UserName IS NULL OR @UserName = '' OR x.UserName LIKE @UserName)
)
SELECT TOP (@Top)
    EventTime,
    InventoryEntryType,
    RequestStatus,
    RequestEntryType,
    AuditAction,
    AuditStatus,
    AuditLocation,
    Direction,
    SerialNumber,
    ItemId,
    ItemName,
    ModelNumber,
    SetId,
    SetCode,
    Quantity,
    EmployeeName,
    BranchName,
    DepartmentName,
    CompanyName,
    UserName,
    Source,
    ReferenceType,
    ReferenceId,
    AuditReferenceType,
    AuditReferenceId,
    Notes,
    IsArchived
FROM Ranked
WHERE rn = 1
ORDER BY EventTime DESC, ReferenceType DESC, ReferenceId DESC;";

            var results = new List<ItemMovementAuditDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync().ConfigureAwait(false);
                var includeBorrow = await CallSchemaGate.TableExistsAsync(con, "dbo.BorrowLog").ConfigureAwait(false);
                var sql = includeBorrow ? InjectBorrowEventsIntoLatestSql(baseSql) : baseSql;

                using (var cmd = new SqlCommand(sql, con))
                {
                cmd.Parameters.Add(new SqlParameter("@Top", SqlDbType.Int) { Value = top });
                cmd.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = (object)fromDate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = (object)toDate ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@Direction", SqlDbType.NVarChar, 10) { Value = (object)direction ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@Serial", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(serial) ? (object)DBNull.Value : ("%" + serial.Trim() + "%")
                });
                cmd.Parameters.Add(new SqlParameter("@ItemId", SqlDbType.Int) { Value = (object)itemId ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@SetCode", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(setCode) ? (object)DBNull.Value : ("%" + setCode.Trim() + "%")
                });
                cmd.Parameters.Add(new SqlParameter("@Source", SqlDbType.NVarChar, 30) { Value = (object)source ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@UserName", SqlDbType.NVarChar, 200)
                {
                    Value = string.IsNullOrWhiteSpace(userName) ? (object)DBNull.Value : ("%" + userName.Trim() + "%")
                });
                using (var rdr = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rdr.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(new ItemMovementAuditDto
                        {
                            EventTime = rdr["EventTime"] == DBNull.Value ? DateTime.MinValue : (DateTime)rdr["EventTime"],
                            InventoryEntryType = rdr["InventoryEntryType"] as string,
                            RequestStatus = rdr["RequestStatus"] as string,
                            RequestEntryType = rdr["RequestEntryType"] as string,
                            AuditAction = rdr["AuditAction"] as string,
                            AuditStatus = rdr["AuditStatus"] as string,
                            AuditLocation = rdr["AuditLocation"] as string,
                            Direction = rdr["Direction"] as string,
                            SerialNumber = rdr["SerialNumber"] as string,
                            ItemId = rdr["ItemId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ItemId"]),
                            ItemName = rdr["ItemName"] as string,
                            ModelNumber = rdr["ModelNumber"] as string,
                            SetId = rdr["SetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["SetId"]),
                            SetCode = rdr["SetCode"] as string,
                            Quantity = rdr["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["Quantity"]),
                            EmployeeName = rdr["EmployeeName"] as string,
                            BranchName = rdr["BranchName"] as string,
                            DepartmentName = rdr["DepartmentName"] as string,
                            CompanyName = rdr["CompanyName"] as string,
                            UserName = rdr["UserName"] as string,
                            Source = rdr["Source"] as string,
                            ReferenceType = rdr["ReferenceType"] as string,
                            ReferenceId = rdr["ReferenceId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["ReferenceId"]),
                            AuditReferenceType = rdr["AuditReferenceType"] as string,
                            AuditReferenceId = rdr["AuditReferenceId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["AuditReferenceId"]),
                            Notes = NormalizeAuditNotes(rdr["Notes"] as string),
                            IsArchived = rdr["IsArchived"] != DBNull.Value && Convert.ToBoolean(rdr["IsArchived"])
                        });
                    }
                }
                }
            }

            return results;
        }
    }
}
