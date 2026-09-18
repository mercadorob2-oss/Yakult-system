
CREATE PROCEDURE [dbo].[sp_ArchiveRenewal]
    @RenewalId INT,
    @ItemId INT,
    @ArchiveReason NVARCHAR(500),
    @ArchivedBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Archive the Renewal record
        UPDATE dbo.Renewals
        SET IsArchived = 1,
            ArchivedDate = GETDATE(),
            ArchiveReason = @ArchiveReason,
            ModifiedAt = GETDATE()
        WHERE RenewalId = @RenewalId

        -- Archive in ArchiveStatus table (EntityType = 'Renewal')
        INSERT INTO dbo.ArchiveStatus (
            EntityType,
            EntityId,
            IsArchived,
            ArchivedAt,
            ArchivedBy,
            ArchiveReason
        )
        VALUES (
            'Renewal',
            @RenewalId,
            1,
            GETDATE(),
            @ArchivedBy,
            @ArchiveReason
        )

        -- Archive the Item as well
        INSERT INTO dbo.ArchiveStatus (
            EntityType,
            EntityId,
            IsArchived,
            ArchivedAt,
            ArchivedBy,
            ArchiveReason
        )
        VALUES (
            'Item',
            @ItemId,
            1,
            GETDATE(),
            @ArchivedBy,
            @ArchiveReason
        )

        -- Mark Item as inactive
        UPDATE dbo.Item
        SET Active = 0,
            DateModified = GETDATE()
        WHERE ItemId = @ItemId

        COMMIT TRANSACTION;

        SELECT 'Success' AS Result
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
