-- Adds the FK column linking each dbo.SetItem row to its owning dbo.SetItemSubTypeGroup row.
-- Must run AFTER Migration_SetItemSubTypeGroup_CreateTable.sql (the referenced table must
-- exist first) and BEFORE Migration_SetItemSubTypeGroup_Backfill.sql populates it.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'GroupId'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD GroupId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SetItem_SetItemSubTypeGroup'
)
BEGIN
    ALTER TABLE dbo.SetItem
        ADD CONSTRAINT FK_SetItem_SetItemSubTypeGroup FOREIGN KEY (GroupId)
        REFERENCES dbo.SetItemSubTypeGroup (GroupId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'IX_SetItem_GroupId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SetItem_GroupId
        ON dbo.SetItem (GroupId ASC);
END
GO
