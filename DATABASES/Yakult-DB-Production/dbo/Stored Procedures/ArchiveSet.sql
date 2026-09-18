
CREATE PROCEDURE dbo.ArchiveSet
    @SetId INT,
    @ArchiveItems BIT = 1, -- if 1, archive items referenced by this set
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @SetId IS NULL
    BEGIN
        RAISERROR('SetId is required', 16, 1); RETURN;
    END

    BEGIN TRAN;
    BEGIN TRY
        -- copy set to archive (if not already)
        IF NOT EXISTS (SELECT 1 FROM dbo.Set_Archive WHERE SetId = @SetId)
        BEGIN
            INSERT INTO dbo.Set_Archive
            (SetId, SetCode, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks,
             DispatchDate, SetType, QRData, Subtotal, VatAmount, WhtAmount,
             DiscountAmount, TotalAmountDue, DocumentNumber, ReferenceNumber,
             Status, Site, ComId, StartDate, EndDate, ReqId, CurrentBranchId, CurrentDepartmentId,
             ArchivedAt, ArchivedBy, ArchiveReason)
            SELECT
                SetId, SetCode, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks,
                DispatchDate, SetType, QRData, Subtotal, VatAmount, WhtAmount,
                DiscountAmount, TotalAmountDue, DocumentNumber, ReferenceNumber,
                Status, Site, ComId, StartDate, EndDate, ReqId, CurrentBranchId, CurrentDepartmentId,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.[Set]
            WHERE SetId = @SetId;
        END

        -- mark set inactive
        UPDATE dbo.[Set] SET Active = 0 WHERE SetId = @SetId;

        IF @ArchiveItems = 1
        BEGIN
            -- archive items that are linked via SetItem
            INSERT INTO dbo.Item_Archive
            (ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
             DateCreated, CreatedBy, DateModified, ModifiedBy,
             Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
             StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
             DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
             LicenseNumber, WarrantyStartDate, WarrantyEndDate,
             ArchivedAt, ArchivedBy, ArchiveReason)
            SELECT DISTINCT
                i.ItemId, i.Name, i.Description, i.Active, i.UnitOfMeasure, i.StockOnHand,
                i.DateCreated, i.CreatedBy, i.DateModified, i.ModifiedBy,
                i.Category, i.SerialNumber, i.CategoryId, i.ModelNumber, i.Amount, i.ItemType,
                i.StartDate, i.EndDate, i.ConditionID, i.VendorId, i.Remarks, i.WarrantyYears,
                i.DurationYears, i.DurationStartDate, i.DurationEndDate, i.DatePurchased,
                i.LicenseNumber, i.WarrantyStartDate, i.WarrantyEndDate,
                SYSUTCDATETIME(), @ArchivedBy, CONCAT('Archived via ArchiveSet(', @SetId, '): ', @ArchiveReason)
            FROM dbo.SetItem si
            INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
            WHERE si.SetId = @SetId
              AND NOT EXISTS (SELECT 1 FROM dbo.Item_Archive ia WHERE ia.ItemId = i.ItemId);

            -- mark the items inactive
            UPDATE i
            SET Active = 0
            FROM dbo.Item i
            INNER JOIN dbo.SetItem si ON i.ItemId = si.ItemId
            WHERE si.SetId = @SetId;
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
