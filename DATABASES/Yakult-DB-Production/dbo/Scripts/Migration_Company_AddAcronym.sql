IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Company]') AND name = 'Acronym'
)
BEGIN
    ALTER TABLE dbo.[Company] ADD [Acronym] NVARCHAR(20) NULL;
END
