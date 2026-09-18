
CREATE PROCEDURE dbo.ArchiveItem
    @ItemId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @ItemId IS NULL
    BEGIN
        RAISERROR('ItemId is required', 16, 1); RETURN;
    END

    BEGIN TRAN;
    BEGIN TRY
        -- copy if not already archived
        IF NOT EXISTS (SELECT 1 FROM dbo.Item_Archive WHERE ItemId = @ItemId)
        BEGIN
            INSERT INTO dbo.Item_Archive
            (ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
             DateCreated, CreatedBy, DateModified, ModifiedBy,
             Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
             StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
             DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
             LicenseNumber, WarrantyStartDate, WarrantyEndDate,
             ArchivedAt, ArchivedBy, ArchiveReason)
            SELECT
                ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
                DateCreated, CreatedBy, DateModified, ModifiedBy,
                Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
                StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
                DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
                LicenseNumber, WarrantyStartDate, WarrantyEndDate,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Item
            WHERE ItemId = @ItemId;
        END

        -- mark original inactive
        UPDATE dbo.Item
        SET Active = 0
        WHERE ItemId = @ItemId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
