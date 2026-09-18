-- ============================================================
-- Migration: Make account-specific columns nullable in
--            dbo.DepartmentAccount
-- Purpose:   The table is now a semi-lookup table — every
--            unique Company/Department/Branch combination has
--            a row, but not every row has an account yet.
--            Username, IsActive, and DateCreated only apply
--            once an account is created, so they must be NULL
--            for lookup-only rows.
-- ============================================================

-- Username: NULL until an account is created for the row
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount')
      AND name = 'Username'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount
        ALTER COLUMN [Username] NVARCHAR(200) NULL;

    PRINT 'Column Username set to NULL.';
END
ELSE
    PRINT 'Column Username already nullable. Skipping.';
GO

-- IsActive: NULL until an account is created
-- Drop the default constraint first if it exists
DECLARE @IsActiveDefault SYSNAME;
SELECT @IsActiveDefault = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id
                        AND dc.parent_column_id  = c.column_id
WHERE c.object_id = OBJECT_ID('dbo.DepartmentAccount')
  AND c.name      = 'IsActive';

IF @IsActiveDefault IS NOT NULL
BEGIN
    EXEC ('ALTER TABLE dbo.DepartmentAccount DROP CONSTRAINT [' + @IsActiveDefault + ']');
    PRINT 'Dropped DEFAULT constraint on IsActive.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount')
      AND name = 'IsActive'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount
        ALTER COLUMN [IsActive] BIT NULL;

    PRINT 'Column IsActive set to NULL.';
END
ELSE
    PRINT 'Column IsActive already nullable. Skipping.';
GO

-- DateCreated: NULL until an account is created
-- Drop the default constraint first if it exists
DECLARE @DateCreatedDefault SYSNAME;
SELECT @DateCreatedDefault = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id
                        AND dc.parent_column_id  = c.column_id
WHERE c.object_id = OBJECT_ID('dbo.DepartmentAccount')
  AND c.name      = 'DateCreated';

IF @DateCreatedDefault IS NOT NULL
BEGIN
    EXEC ('ALTER TABLE dbo.DepartmentAccount DROP CONSTRAINT [' + @DateCreatedDefault + ']');
    PRINT 'Dropped DEFAULT constraint on DateCreated.';
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount')
      AND name = 'DateCreated'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount
        ALTER COLUMN [DateCreated] DATETIME NULL;

    PRINT 'Column DateCreated set to NULL.';
END
ELSE
    PRINT 'Column DateCreated already nullable. Skipping.';
GO
