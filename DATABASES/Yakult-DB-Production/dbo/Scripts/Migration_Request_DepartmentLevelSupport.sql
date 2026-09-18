-- Migration: Department-level request support
-- Allows a request to be assigned to an org unit (Company/Department/Branch)
-- instead of a specific employee.

-- 1. Make EmpId nullable (remove NOT NULL constraint)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Request]') AND name = 'EmpId' AND is_nullable = 0
)
BEGIN
    -- Drop the existing FK before altering the column
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_Request_Employee' AND parent_object_id = OBJECT_ID('dbo.[Request]')
    )
    BEGIN
        ALTER TABLE dbo.[Request] DROP CONSTRAINT [FK_Request_Employee];
    END

    ALTER TABLE dbo.[Request] ALTER COLUMN [EmpId] INT NULL;

    ALTER TABLE dbo.[Request] ADD CONSTRAINT [FK_Request_Employee]
        FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]);

    PRINT 'EmpId made nullable.';
END
ELSE
BEGIN
    PRINT 'EmpId already nullable — skipped.';
END

-- 2. Add ComId column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Request]') AND name = 'ComId'
)
BEGIN
    ALTER TABLE dbo.[Request] ADD [ComId] INT NULL;

    ALTER TABLE dbo.[Request] ADD CONSTRAINT [FK_Request_Company]
        FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]);

    PRINT 'ComId column added.';
END
ELSE
BEGIN
    PRINT 'ComId already exists — skipped.';
END

-- 3. Add DeptId column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Request]') AND name = 'DeptId'
)
BEGIN
    ALTER TABLE dbo.[Request] ADD [DeptId] INT NULL;

    ALTER TABLE dbo.[Request] ADD CONSTRAINT [FK_Request_Department]
        FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]);

    PRINT 'DeptId column added.';
END
ELSE
BEGIN
    PRINT 'DeptId already exists — skipped.';
END

-- 4. Add BranchId column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Request]') AND name = 'BranchId'
)
BEGIN
    ALTER TABLE dbo.[Request] ADD [BranchId] INT NULL;

    ALTER TABLE dbo.[Request] ADD CONSTRAINT [FK_Request_Branch]
        FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]);

    PRINT 'BranchId column added.';
END
ELSE
BEGIN
    PRINT 'BranchId already exists — skipped.';
END
