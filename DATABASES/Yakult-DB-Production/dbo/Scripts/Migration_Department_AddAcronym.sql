IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Department]') AND name = 'Acronym'
)
BEGIN
    ALTER TABLE dbo.[Department] ADD [Acronym] NVARCHAR(20) NULL;
END
