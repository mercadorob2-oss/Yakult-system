
CREATE PROCEDURE dbo.sp_ArchiveRequest
    @ReqId INT,
    @ArchivedBy NVARCHAR(100),
    @ArchiveReason NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Check if request exists
        IF NOT EXISTS (SELECT 1 FROM dbo.Request WHERE ReqId = @ReqId)
        BEGIN
            RAISERROR('Request with ID %d does not exist.', 16, 1, @ReqId);
            RETURN;
        END
        
        -- Check if already archived with same reason
        IF EXISTS (
            SELECT 1 
            FROM dbo.Request_Archive 
            WHERE ReqId = @ReqId 
            AND ArchiveReason = @ArchiveReason
        )
        BEGIN
            PRINT 'Request already archived with this reason. Skipping duplicate archive.';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Insert into archive (excluding RowVer)
        INSERT INTO dbo.Request_Archive (
            ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
            DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId,
            UnitPrice, Active, ArchivedBy, ArchiveReason
        )
        SELECT 
            ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
            DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId,
            UnitPrice, Active, @ArchivedBy, @ArchiveReason
        FROM dbo.Request
        WHERE ReqId = @ReqId;
        
        -- Mark original as inactive
        UPDATE dbo.Request
        SET Active = 0
        WHERE ReqId = @ReqId;
        
        COMMIT TRANSACTION;
        
        PRINT 'Request ' + CAST(@ReqId AS NVARCHAR(10)) + ' archived successfully.';
        
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
