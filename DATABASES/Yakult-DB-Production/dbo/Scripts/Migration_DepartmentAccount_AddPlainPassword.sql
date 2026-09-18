IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount') AND name = 'PlainPassword'
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount ADD PlainPassword NVARCHAR(200) NULL;
END
