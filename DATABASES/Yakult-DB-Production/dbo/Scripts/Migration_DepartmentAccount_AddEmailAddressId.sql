-- ============================================================
-- Migration: Add EmailAddressId FK to dbo.DepartmentAccount
-- Purpose:   Links each DepartmentAccount row to an email
--            address in dbo.EmailAddress so that the admin
--            page can display and assign a contact email per
--            company/department/branch combination.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount')
      AND name = 'EmailAddressId'
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount
        ADD [EmailAddressId] INT NULL;

    ALTER TABLE dbo.DepartmentAccount
        ADD CONSTRAINT [FK_DepartmentAccount_EmailAddress]
        FOREIGN KEY ([EmailAddressId]) REFERENCES dbo.[EmailAddress] ([EmailId]);

    PRINT 'Column EmailAddressId added to dbo.DepartmentAccount.';
END
ELSE
BEGIN
    PRINT 'Column EmailAddressId already exists. Skipping.';
END
GO
