CREATE PROCEDURE sp_DeactivateCompanyCascade
    @ComId INT,
    @UserId INT,
    @IsActivating BIT = 0  -- 0 = Deactivate, 1 = Activate
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        DECLARE @NewActiveStatus BIT = @IsActivating;
        DECLARE @Now DATETIME = GETDATE();
        
        -- Update Company
        UPDATE Company
        SET Active = @NewActiveStatus,
            DateModified = @Now,
            ModifiedBy = @UserId
        WHERE ComId = @ComId;
        
        -- Cascade to Departments
        UPDATE Department
        SET Active = @NewActiveStatus,
            DateModified = @Now,
            ModifiedBy = @UserId
        WHERE ComId = @ComId;
        
        -- Cascade to Branches
        UPDATE Branch
        SET Active = @NewActiveStatus,
            DateModified = @Now,
            ModifiedBy = @UserId
        WHERE ComId = @ComId;
        
        -- Cascade to Employees (optional - you might want to keep employees active)
        -- Uncomment if you want to deactivate employees too
        /*
        UPDATE Employee
        SET Active = @NewActiveStatus,
            DateModified = @Now,
            ModifiedBy = @UserId
        WHERE ComId = @ComId;
        */
        
        COMMIT TRANSACTION;
        
        -- Return affected counts
        SELECT 
            (SELECT COUNT(*) FROM Department WHERE ComId = @ComId) AS DepartmentsAffected,
            (SELECT COUNT(*) FROM Branch WHERE ComId = @ComId) AS BranchesAffected,
            (SELECT COUNT(*) FROM Employee WHERE ComId = @ComId) AS EmployeesAffected;
            
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
