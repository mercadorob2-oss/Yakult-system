CREATE PROCEDURE dbo.ArchiveInventory_ById
    @InvId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL,
    @Deactivate BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @InvId IS NULL BEGIN RAISERROR('InvId required',16,1); RETURN; END
    BEGIN TRY
        BEGIN TRAN;
        IF OBJECT_ID('dbo.Inventory_Archive','U') IS NULL
        BEGIN RAISERROR('Inventory_Archive not found',16,1); ROLLBACK; RETURN; END

        IF NOT EXISTS (SELECT 1 FROM dbo.Inventory_Archive WHERE InvId = @InvId)
        BEGIN
            INSERT INTO dbo.Inventory_Archive
            (
                InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active,
                ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT
                InvId, Description, EntryType, Quantity, DatePosted, PostedBy, ReqId, ItemId, SetId, Active,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Inventory WHERE InvId = @InvId;
        END

        UPDATE dbo.Inventory
        SET IsArchived = 1,
            ArchivedAt = SYSUTCDATETIME(),
            ArchivedBy = @ArchivedBy,
            ArchiveReason = @ArchiveReason,
            Active = CASE WHEN @Deactivate = 1 THEN 0 ELSE Active END
        WHERE InvId = @InvId;

        COMMIT;
    END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END
