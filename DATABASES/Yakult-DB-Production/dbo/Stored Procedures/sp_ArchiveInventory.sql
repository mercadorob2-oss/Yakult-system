
CREATE PROCEDURE dbo.sp_ArchiveInventory
    @InvId INT,
    @ArchivedBy NVARCHAR(100),
    @ArchiveReason NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Check if inventory record exists
        IF NOT EXISTS (SELECT 1 FROM dbo.Inventory WHERE InvId = @InvId)
        BEGIN
            RAISERROR('Inventory record with ID %d does not exist.', 16, 1, @InvId);
            RETURN;
        END
        
        -- Check if already archived with same reason
        IF EXISTS (
            SELECT 1 
            FROM dbo.Inventory_Archive 
            WHERE InvId = @InvId 
            AND ArchiveReason = @ArchiveReason
        )
        BEGIN
            PRINT 'Inventory record already archived with this reason. Skipping duplicate archive.';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Insert into archive (excluding RowVer)
        INSERT INTO dbo.Inventory_Archive (
            InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId,
            ItemId, SetId, Active, ArchivedBy, ArchiveReason
        )
        SELECT 
            InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId,
            ItemId, SetId, Active, @ArchivedBy, @ArchiveReason
        FROM dbo.Inventory
        WHERE InvId = @InvId;
        
        -- Mark original as inactive
        UPDATE dbo.Inventory
        SET Active = 0
        WHERE InvId = @InvId;
        
        COMMIT TRANSACTION;
        
        PRINT 'Inventory record ' + CAST(@InvId AS NVARCHAR(10)) + ' archived successfully.';
        
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
