
CREATE PROCEDURE dbo.sp_ArchiveSet
    @SetId INT,
    @ArchivedBy NVARCHAR(100),
    @ArchiveReason NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Check if set exists
        IF NOT EXISTS (SELECT 1 FROM dbo.[Set] WHERE SetId = @SetId)
        BEGIN
            RAISERROR('Set with ID %d does not exist.', 16, 1, @SetId);
            RETURN;
        END
        
        -- Check if already archived with same reason
        IF EXISTS (
            SELECT 1 
            FROM dbo.Set_Archive 
            WHERE SetId = @SetId 
            AND ArchiveReason = @ArchiveReason
        )
        BEGIN
            PRINT 'Set already archived with this reason. Skipping duplicate archive.';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Archive all SetItems for this set
        INSERT INTO dbo.SetItem_Archive (
            SetItemId, SetId, ItemId, ItemCode, Description, Quantity,
            UnitOfMeasure, UnitPrice, Amount, LineStartDate, LineEndDate,
            CreatedBy, CreatedAt, ArchivedBy, ArchiveReason
        )
        SELECT 
            SetItemId, SetId, ItemId, ItemCode, Description, Quantity,
            UnitOfMeasure, UnitPrice, Amount, LineStartDate, LineEndDate,
            CreatedBy, CreatedAt, @ArchivedBy, @ArchiveReason
        FROM dbo.SetItem
        WHERE SetId = @SetId
        AND NOT EXISTS (
            SELECT 1 FROM dbo.SetItem_Archive sa
            WHERE sa.SetItemId = SetItem.SetItemId
            AND sa.ArchiveReason = @ArchiveReason
        );
        
        DECLARE @SetItemsArchived INT = @@ROWCOUNT;
        
        -- Insert Set into archive (storing SetCode as regular column)
        INSERT INTO dbo.Set_Archive (
            SetId, SetCode, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks,
            DispatchDate, SetType, QRData, Subtotal, VatAmount, WhtAmount,
            DiscountAmount, TotalAmountDue, DocumentNumber, ReferenceNumber,
            Status, Site, ComId, StartDate, EndDate, ReqId, CurrentBranchId,
            CurrentDepartmentId, Active, ArchivedBy, ArchiveReason
        )
        SELECT 
            SetId, SetCode, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks,
            DispatchDate, SetType, QRData, Subtotal, VatAmount, WhtAmount,
            DiscountAmount, TotalAmountDue, DocumentNumber, ReferenceNumber,
            Status, Site, ComId, StartDate, EndDate, ReqId, CurrentBranchId,
            CurrentDepartmentId, Active, @ArchivedBy, @ArchiveReason
        FROM dbo.[Set]
        WHERE SetId = @SetId;
        
        -- Mark original Set as inactive
        UPDATE dbo.[Set]
        SET Active = 0
        WHERE SetId = @SetId;
        
        COMMIT TRANSACTION;
        
        PRINT 'Set ' + CAST(@SetId AS NVARCHAR(10)) + ' archived successfully.';
        PRINT 'Archived ' + CAST(@SetItemsArchived AS NVARCHAR(10)) + ' SetItem records.';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
