

-- =============================================
-- FIX 2: Update your stored procedure to handle
-- the Set.ReqId issue properly
-- =============================================

/***********************
 Stored procedure: delete Set(s) safely with stock restoration
 - Restores stock for all items in requests
 - Nullifies Set.ReqId references before deleting requests
 - Deletes Set rows
 - Deletes Request rows that are no longer referenced
 - Deletes related Inventory entries
 - Wraps everything in a transaction
************************/
CREATE   PROCEDURE [dbo].[usp_Set_Delete]
    @SetId INT = NULL,
    @SetIdsCsv VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    -- Validation
    IF @SetId IS NULL AND (@SetIdsCsv IS NULL OR LTRIM(RTRIM(@SetIdsCsv)) = '')
    BEGIN
        RAISERROR('Provide either @SetId or @SetIdsCsv', 16, 1);
        RETURN;
    END
    
    BEGIN TRAN;
    
    BEGIN TRY
        -- Table to hold SetIds to delete
        DECLARE @ToDelete TABLE (SetId INT PRIMARY KEY);
        
        -- Populate @ToDelete
        IF @SetId IS NOT NULL
        BEGIN
            INSERT INTO @ToDelete(SetId) VALUES(@SetId);
        END
        ELSE
        BEGIN
            INSERT INTO @ToDelete(SetId)
            SELECT DISTINCT TRY_CAST(value AS INT) AS SetId
            FROM STRING_SPLIT(@SetIdsCsv, ',')
            WHERE TRY_CAST(value AS INT) IS NOT NULL;
        END
        
        -- Table to hold all RequestIds associated with these Sets
        DECLARE @RequestsToDelete TABLE (ReqId INT PRIMARY KEY);
        
        -- Get all requests belonging to these Sets (via Request.SetId)
        INSERT INTO @RequestsToDelete(ReqId)
        SELECT DISTINCT r.ReqId
        FROM dbo.Request r
        INNER JOIN @ToDelete t ON r.SetId = t.SetId;
        
        -- Also capture the ReqId from Set.ReqId (the primary request reference)
        INSERT INTO @RequestsToDelete(ReqId)
        SELECT DISTINCT s.ReqId
        FROM dbo.[Set] s
        INNER JOIN @ToDelete t ON s.SetId = t.SetId
        WHERE s.ReqId IS NOT NULL
          AND s.ReqId NOT IN (SELECT ReqId FROM @RequestsToDelete);
        
        
        -- STEP 1: Restore stock ONLY for inventory-affecting items
        UPDATE i
        SET i.StockOnHand = i.StockOnHand + r.Quantity
        FROM dbo.Item i
        INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
        INNER JOIN @RequestsToDelete rtd ON r.ReqId = rtd.ReqId
        WHERE i.AffectsInventory = 1;

        
        PRINT 'Stock restored for ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' items';
        
        -- STEP 2: Clear Set.ReqId for ANY Set that references requests we're about to delete
        --         (This prevents FK violation when deleting requests)
        UPDATE s
        SET s.ReqId = NULL
        FROM dbo.[Set] s
        INNER JOIN @RequestsToDelete rtd ON s.ReqId = rtd.ReqId;
        
        PRINT 'Cleared Set.ReqId for ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' sets';
        
        -- STEP 3: Clear Request.SetId for requests in the sets we're deleting
        --         (This unlinks requests from sets before set deletion)
        UPDATE r
        SET r.SetId = NULL
        FROM dbo.Request r
        INNER JOIN @ToDelete t ON r.SetId = t.SetId;
        
        PRINT 'Unlinked ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' requests from sets';
        
        -- STEP 4: Delete Inventory entries related to these requests
        DELETE inv
        FROM dbo.Inventory inv
        INNER JOIN @RequestsToDelete rtd ON inv.ReqId = rtd.ReqId;
        
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' inventory entries';
        
        -- STEP 5: Delete the Sets themselves
        DELETE s
        FROM dbo.[Set] s
        INNER JOIN @ToDelete t ON s.SetId = t.SetId;
        
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' sets';
        
        -- STEP 6: Delete the Requests
        DELETE r
        FROM dbo.Request r
        INNER JOIN @RequestsToDelete rtd ON r.ReqId = rtd.ReqId;
        
        PRINT 'Deleted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' requests';
        
        COMMIT TRAN;
        
        PRINT 'Set deletion completed successfully';
        
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
