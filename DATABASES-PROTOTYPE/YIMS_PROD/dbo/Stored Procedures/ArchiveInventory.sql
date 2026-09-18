
CREATE PROCEDURE dbo.ArchiveInventory
    @InvId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @InvId IS NULL
    BEGIN
        RAISERROR('InvId is required', 16, 1); RETURN;
    END

    BEGIN TRAN;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.Inventory_Archive WHERE InvId = @InvId)
        BEGIN
            INSERT INTO dbo.Inventory_Archive
            (InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId,
             ArchivedAt, ArchivedBy, ArchiveReason)
            SELECT
                InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Inventory
            WHERE InvId = @InvId;
        END

        UPDATE dbo.Inventory SET Active = 0 WHERE InvId = @InvId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
