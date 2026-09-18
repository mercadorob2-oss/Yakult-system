-- Updates dbo.Item Name, Description, and ModelNumber from dbo.Item_Staging.
-- Run on Yakult_Inventory_System_DEV (and then PROD after verification).

USE Yakult_Inventory_System_DEV;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE i
    SET
        i.Name         = s.Name,
        i.Description  = s.Description,
        i.ModelNumber  = s.ModelNumber,
        i.DateModified = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
        i.ModifiedBy   = 8
    FROM dbo.Item i
    JOIN dbo.Item_Staging s ON s.ItemId = i.ItemId
    WHERE s.Name IS NOT NULL;

    COMMIT;

    SELECT
        i.ItemId,
        i.Name        AS UpdatedName,
        i.Description AS UpdatedDescription,
        i.ModelNumber AS UpdatedModelNumber
    FROM dbo.Item i
    JOIN dbo.Item_Staging s ON s.ItemId = i.ItemId
    WHERE s.Name IS NOT NULL
    ORDER BY i.ItemId;

    PRINT 'Done. ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows updated.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
