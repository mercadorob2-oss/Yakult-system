-- Adds the FK column linking each dbo.SetItem row to its owning dbo.SetItemParentTagGroup row.
-- Must run AFTER Migration_SetItemParentTagGroup_CreateTable.sql (the referenced table must
-- exist first). Independent of, and orthogonal to, the existing GroupId (Sub-Type Group) column.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'ParentTagGroupId'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD ParentTagGroupId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SetItem_SetItemParentTagGroup'
)
BEGIN
    ALTER TABLE dbo.SetItem
        ADD CONSTRAINT FK_SetItem_SetItemParentTagGroup FOREIGN KEY (ParentTagGroupId)
        REFERENCES dbo.SetItemParentTagGroup (ParentTagGroupId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'IX_SetItem_ParentTagGroupId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SetItem_ParentTagGroupId
        ON dbo.SetItem (ParentTagGroupId ASC);
END
GO
