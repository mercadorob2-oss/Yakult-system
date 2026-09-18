-- CORRECTED View: All Archived Entities
CREATE   VIEW [dbo].[vw_AllArchivedEntities]
AS
SELECT 
    a.ArchiveId,
    a.EntityType,
    a.EntityId,
    a.IsArchived,
    a.ArchivedAt,
    a.ArchivedBy,
    a.ArchiveReason,
    a.RestoredAt,
    a.RestoredBy,
    CASE a.EntityType
        WHEN 'Item' THEN (SELECT Name FROM dbo.Item WHERE ItemId = a.EntityId)
        WHEN 'Request' THEN 'Request #' + CAST(a.EntityId AS NVARCHAR(20))
        WHEN 'Set' THEN (SELECT SetCode FROM dbo.[Set] WHERE SetId = a.EntityId)
        WHEN 'Inventory' THEN 'Inventory #' + CAST(a.EntityId AS NVARCHAR(20))
        WHEN 'Company' THEN (SELECT Name FROM dbo.Company WHERE ComId = a.EntityId)
        WHEN 'Department' THEN (SELECT Name FROM dbo.Department WHERE DeptId = a.EntityId)
        WHEN 'Branch' THEN (SELECT Name FROM dbo.Branch WHERE BranchId = a.EntityId)
        WHEN 'Employee' THEN (SELECT Name FROM dbo.Employee WHERE EmpId = a.EntityId)
        WHEN 'Vendor' THEN (SELECT VendorName FROM dbo.Vendor WHERE VendorID = a.EntityId)
        WHEN 'ItemCategory' THEN (SELECT Name FROM dbo.ItemCategory WHERE CategoryId = a.EntityId)
        WHEN 'Condition' THEN (SELECT ConditionName FROM dbo.Condition WHERE ConditionID = a.EntityId)
    END AS EntityName
FROM [dbo].[ArchiveStatus] a
WHERE a.IsArchived = 1;
