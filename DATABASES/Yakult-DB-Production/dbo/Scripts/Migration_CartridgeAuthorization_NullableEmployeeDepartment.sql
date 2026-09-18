-- Makes EmployeeId and DepartmentId nullable in CartridgeAuthorization
-- so IT-assisted requests can be submitted without a target employee or department.
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CartridgeAuthorization_Employee'
      AND parent_object_id = OBJECT_ID('dbo.[CartridgeAuthorization]')
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization] DROP CONSTRAINT [FK_CartridgeAuthorization_Employee];
END

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CartridgeAuthorization_Department'
      AND parent_object_id = OBJECT_ID('dbo.[CartridgeAuthorization]')
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization] DROP CONSTRAINT [FK_CartridgeAuthorization_Department];
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[CartridgeAuthorization]') AND name = 'EmployeeId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization] ALTER COLUMN [EmployeeId] INT NULL;
END

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[CartridgeAuthorization]') AND name = 'DepartmentId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization] ALTER COLUMN [DepartmentId] INT NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CartridgeAuthorization_Employee'
      AND parent_object_id = OBJECT_ID('dbo.[CartridgeAuthorization]')
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD CONSTRAINT [FK_CartridgeAuthorization_Employee]
        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([EmpId]);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CartridgeAuthorization_Department'
      AND parent_object_id = OBJECT_ID('dbo.[CartridgeAuthorization]')
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD CONSTRAINT [FK_CartridgeAuthorization_Department]
        FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department] ([DeptId]);
END
