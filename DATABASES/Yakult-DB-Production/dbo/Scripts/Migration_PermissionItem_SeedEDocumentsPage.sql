-- =============================================================================
-- Migration: Seed EDocumentsPage PermissionItem + Report Monitoring nav entry
-- Purpose:   Adds the "E-Documents" entry to the desktop REPORT MONITORING side
--            menu (Yakult.Inventory.App/Forms/Reports/ReportsForm.cs). Seeds one
--            dbo.PermissionItem row (PermissionType='Page', ItemKey='EDocumentsPage')
--            plus one dbo.PageNavEntry row ('Reports', 'Report Monitoring', 7).
-- Run after: Migration_PermissionItem_CreateTables.sql,
--            Migration_PermissionItem_SeedNavPages.sql,
--            Migration_PageNavEntry_CreateAndSeed.sql
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM dbo.PermissionItem
    WHERE PermissionType = 'Page' AND ItemKey = 'EDocumentsPage'
)
BEGIN
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName)
    VALUES ('Page', 'EDocumentsPage', 'E-Documents');

    PRINT 'PermissionItem EDocumentsPage seeded.';
END
ELSE
BEGIN
    PRINT 'PermissionItem EDocumentsPage already exists. Skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM dbo.PageNavEntry n
    INNER JOIN dbo.PermissionItem pi
        ON pi.PermissionItemId = n.PermissionItemId
    WHERE pi.PermissionType = 'Page'
      AND pi.ItemKey = 'EDocumentsPage'
      AND n.PortalKey = 'Reports'
      AND n.MenuGroup = 'Report Monitoring'
)
BEGIN
    INSERT INTO dbo.PageNavEntry (PermissionItemId, PortalKey, MenuGroup, DisplayName, SortOrder)
    SELECT pi.PermissionItemId, 'Reports', 'Report Monitoring', 'E-Documents', 7
    FROM dbo.PermissionItem pi
    WHERE pi.PermissionType = 'Page'
      AND pi.ItemKey = 'EDocumentsPage';

    PRINT 'PageNavEntry EDocumentsPage (Reports / Report Monitoring) seeded.';
END
ELSE
BEGIN
    PRINT 'PageNavEntry EDocumentsPage (Reports / Report Monitoring) already exists. Skipped.';
END
GO
