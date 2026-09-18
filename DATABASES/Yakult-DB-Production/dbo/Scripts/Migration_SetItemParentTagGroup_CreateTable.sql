-- Parent Tag Groups: a free-text label (e.g. "Cisco", or a whole project title like
-- "IT Infrastructure for Yakult El Salvador Factory") that clusters related dbo.SetItem rows
-- on an invoice for readability, independent of Sub-Type Group. An item can belong to both a
-- Sub-Type Group (dbo.SetItemSubTypeGroup) and a Parent Tag Group at the same time -- these are
-- two orthogonal groupings. Unlike Sub-Type Group, this table intentionally has no
-- VAT/WHT/Discount/Subtotal override columns: Parent Tag is a read-only display/rollup grouping
-- with no financial semantics of its own.
-- Mirrors the shape of dbo.SetItemSubTypeGroup (see Migration_SetItemSubTypeGroup_CreateTable.sql).
IF OBJECT_ID('dbo.SetItemParentTagGroup', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SetItemParentTagGroup (
        ParentTagGroupId INT            IDENTITY (1, 1) NOT NULL,
        SetId            INT            NOT NULL,
        Label            NVARCHAR (200) NOT NULL,
        CreatedBy        INT            NULL,
        CreatedAt        DATETIME2 (7)  CONSTRAINT DF_SetItemParentTagGroup_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        ModifiedBy       INT            NULL,
        ModifiedAt       DATETIME2 (7)  NULL,
        CONSTRAINT PK_SetItemParentTagGroup PRIMARY KEY CLUSTERED (ParentTagGroupId ASC),
        CONSTRAINT FK_SetItemParentTagGroup_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SetItemParentTagGroup') AND name = 'IX_SetItemParentTagGroup_SetId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SetItemParentTagGroup_SetId
        ON dbo.SetItemParentTagGroup (SetId ASC);
END
GO
