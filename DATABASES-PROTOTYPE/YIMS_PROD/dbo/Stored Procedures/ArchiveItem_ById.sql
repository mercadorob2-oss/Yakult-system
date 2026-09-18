
CREATE PROCEDURE dbo.ArchiveItem_ById
    @ItemId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL,
    @Deactivate BIT = 0  -- if 1 will also set Active = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @ItemId IS NULL
    BEGIN
        RAISERROR('ItemId is required',16,1); RETURN;
    END

    BEGIN TRY
        BEGIN TRAN;

        -- 1) ensure archive table exists (your dump shows it exists). If not, fail early.
        IF OBJECT_ID('dbo.Item_Archive','U') IS NULL
        BEGIN
            RAISERROR('Item_Archive table not found; create it before archiving.',16,1);
            ROLLBACK; RETURN;
        END

        -- 2) copy to archive if not already archived
        IF NOT EXISTS (SELECT 1 FROM dbo.Item_Archive WHERE ItemId = @ItemId)
        BEGIN
            INSERT INTO dbo.Item_Archive
            (
                ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
                DateCreated, CreatedBy, DateModified, ModifiedBy,
                Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
                StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
                DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
                LicenseNumber, WarrantyStartDate, ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT
                ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
                DateCreated, CreatedBy, DateModified, ModifiedBy,
                Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
                StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
                DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
                LicenseNumber, WarrantyStartDate, SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Item
            WHERE ItemId = @ItemId;
        END

        -- 3) mark original as archived (and optionally deactivate)
        UPDATE dbo.Item
        SET IsArchived = 1,
            ArchivedAt = SYSUTCDATETIME(),
            ArchivedBy = @ArchivedBy,
            ArchiveReason = @ArchiveReason,
            Active = CASE WHEN @Deactivate = 1 THEN 0 ELSE Active END
        WHERE ItemId = @ItemId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
