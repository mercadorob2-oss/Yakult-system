-- Sub-Type Groups: several SetItem rows sharing the same SubType + ReferenceCode form one
-- group (e.g. one Contract renewal), with a shared date range (BeginDate/EndDate) used by
-- dbo.vw_SetItemSubTypeGroups. These columns were added to the SetItem CREATE TABLE
-- definition file directly (see Migration_SetItem_AddSubType.sql for the SubType column
-- itself), but never had an ALTER TABLE migration to bring them into an existing database.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'ReferenceCode'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD ReferenceCode NVARCHAR (100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'BeginDate'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD BeginDate DATE NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'EndDate'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD EndDate DATE NULL;
END
GO
