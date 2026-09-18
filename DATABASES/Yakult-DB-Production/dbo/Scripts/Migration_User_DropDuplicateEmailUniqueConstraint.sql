-- Migration: Drop the plain UNIQUE constraint on User.EmailAddress that blocks multiple NULLs.
-- The filtered unique index UX_User_Email (WHERE EmailAddress IS NOT NULL) already enforces
-- uniqueness for non-NULL values and correctly allows multiple accounts without an email.
--
-- Root cause: User.sql defined BOTH a bare UNIQUE NONCLUSTERED constraint (auto-named
-- UQ__User__49A1474...) AND the filtered index. The bare constraint does not permit
-- more than one NULL, so creating a second account without an email fails.

-- Step 1: Find and drop the auto-named UNIQUE constraint on EmailAddress.
DECLARE @ConstraintName NVARCHAR(256);

SELECT @ConstraintName = kc.name
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.type = 'UQ'
  AND kc.parent_object_id = OBJECT_ID('dbo.[User]')
  AND c.name = 'EmailAddress';

IF @ConstraintName IS NOT NULL
BEGIN
    DECLARE @Sql NVARCHAR(512) = N'ALTER TABLE dbo.[User] DROP CONSTRAINT [' + @ConstraintName + N']';
    EXEC sp_executesql @Sql;
    PRINT 'Dropped UNIQUE constraint: ' + @ConstraintName;
END
ELSE
BEGIN
    PRINT 'No plain UNIQUE constraint found on EmailAddress — already clean.';
END

-- Step 2: Ensure the filtered unique index exists (idempotent).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[User]')
      AND name = 'UX_User_Email'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Email]
        ON [dbo].[User]([EmailAddress] ASC)
        WHERE ([EmailAddress] IS NOT NULL);
    PRINT 'Created filtered unique index UX_User_Email.';
END
ELSE
BEGIN
    PRINT 'Filtered unique index UX_User_Email already exists.';
END
