

-- =============================================
-- FIX 3: Create stored procedure for deleting 
-- individual Requests with stock restoration
-- =============================================

/***********************
 Stored procedure: delete Request(s) safely with stock restoration
 - Restores stock for the item
 - Clears Set.ReqId if this request is referenced
 - Unlinks request from Set
 - Deletes Inventory entries
 - Deletes the Request
************************/
CREATE   PROCEDURE [dbo].[usp_Request_Delete]
    @ReqId INT = NULL,
    @ReqIdsCsv VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    -- Validation
    IF @ReqId IS NULL AND (@ReqIdsCsv IS NULL OR LTRIM(RTRIM(@ReqIdsCsv)) = '')
    BEGIN
        RAISERROR('Provide either @ReqId or @ReqIdsCsv', 16, 1);
        RETURN;
    END
    
    BEGIN TRAN;
    
    BEGIN TRY
        -- Table to hold ReqIds to delete
        DECLARE @ToDelete TABLE (ReqId INT PRIMARY KEY);
        
        -- Populate @ToDelete
        IF @ReqId IS NOT NULL
        BEGIN
            INSERT INTO @ToDelete(ReqId) VALUES(@ReqId);
        END
        ELSE
        BEGIN
            INSERT INTO @ToDelete(ReqId)
            SELECT DISTINCT TRY_CAST(value AS INT) AS ReqId
            FROM STRING_SPLIT(@ReqIdsCsv, ',')
            WHERE TRY_CAST(value AS INT) IS NOT NULL;
        END
        
        
        -- STEP 1: Restore stock ONLY for inventory-affecting items
        UPDATE i
        SET i.StockOnHand = i.StockOnHand + r.Quantity
        FROM dbo.Item i
        INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
        INNER JOIN @ToDelete td ON r.ReqId = td.ReqId
        WHERE i.AffectsInventory = 1;



        
        PRINT 'Stock restored for ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' items';
        
        -- STEP 2: Clear Set.ReqId for any Set that references these requests
        --         (Prevents FK violation)
        UPDATE s
        SET s.ReqId = NULL
        FROM dbo.[Set] s
        INNER JOIN @ToDelete td ON s.ReqId = td.ReqId;
        
        PRINT 'Cleared Set.ReqId for ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' sets';
        
        -- STEP 3: Delete Inventory entries
        DELETE inv
        FROM dbo.Inventory inv
        INNER JOIN @ToDelete td ON inv.ReqId = td.ReqId;
        
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' inventory entries';
        
        -- STEP 4: Delete the Requests
        DELETE r
        FROM dbo.Request r
        INNER JOIN @ToDelete td ON r.ReqId = td.ReqId;
        
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' requests';
        
        COMMIT TRAN;
        
        PRINT 'Request deletion completed successfully';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
