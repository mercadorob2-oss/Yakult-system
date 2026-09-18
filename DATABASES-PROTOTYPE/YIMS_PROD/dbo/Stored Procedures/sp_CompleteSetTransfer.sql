CREATE PROCEDURE sp_CompleteSetTransfer
    @SetTransferId INT,
    @ApprovedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @SetId INT, @ToBranchId INT, @ToDepartmentId INT;
        
        -- Get transfer details
        SELECT 
            @SetId = SetId, 
            @ToBranchId = ToBranchId, 
            @ToDepartmentId = ToDepartmentId
        FROM dbo.SetTransfer
        WHERE SetTransferId = @SetTransferId 
        AND Status = 'Pending';

        IF @SetId IS NULL
        BEGIN
            RAISERROR('Transfer not found or already processed', 16, 1);
            ROLLBACK;
            RETURN 0;
        END

        -- Update Set current ownership
        UPDATE dbo.[Set]
        SET CurrentBranchId = @ToBranchId,
            CurrentDepartmentId = @ToDepartmentId
        WHERE SetId = @SetId;

        -- Update Inventory items to new branch (if Inventory table has BranchId)
        -- Uncomment if your Inventory table tracks branch
        /*
        UPDATE i
        SET i.BranchId = @ToBranchId,
            i.DeptId = @ToDepartmentId
        FROM dbo.Inventory i
        WHERE i.SetId = @SetId;
        */

        -- Mark transfer as completed
        UPDATE dbo.SetTransfer
        SET Status = 'Completed',
            ApprovedBy = @ApprovedBy,
            TransferDate = GETDATE()
        WHERE SetTransferId = @SetTransferId;

        COMMIT;
        RETURN 1;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;