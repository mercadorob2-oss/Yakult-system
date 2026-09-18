
CREATE PROCEDURE dbo.ArchiveSet_ById
    @SetId INT,
    @ArchiveItems BIT = 1,           -- if 1 archive linked SetItem -> Item copies (into SetItem_Archive & Item_Archive)
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL,
    @DeactivateItems BIT = 0         -- if 1 sets Item.Active = 0 as well
AS
BEGIN
    SET NOCOUNT ON;
    IF @SetId IS NULL
    BEGIN
        RAISERROR('SetId is required',16,1); RETURN;
    END

    BEGIN TRY
        BEGIN TRAN;

        -- archive set row if not present
        IF NOT EXISTS (SELECT 1 FROM dbo.Set_Archive WHERE SetId = @SetId)
        BEGIN
            INSERT INTO dbo.Set_Archive
            (
                SetId, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks, DispatchDate,
                SetType, QRData, Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                DocumentNumber, ReferenceNumber, Status, Site, ComId, StartDate, EndDate, ReqId,
                CurrentBranchId, CurrentDepartmentId, Active, SetCode, ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT
                SetId, CreatedBy, CreatedAt, QRToken, QRImagePath, Remarks, DispatchDate,
                SetType, QRData, Subtotal, VatAmount, WhtAmount, DiscountAmount, TotalAmountDue,
                DocumentNumber, ReferenceNumber, Status, Site, ComId, StartDate, EndDate, ReqId,
                CurrentBranchId, CurrentDepartmentId, Active, SetCode,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.[Set]
            WHERE SetId = @SetId;
        END

        -- mark set as archived
        UPDATE dbo.[Set]
        SET IsArchived = 1,
            ArchivedAt = SYSUTCDATETIME(),
            ArchivedBy = @ArchivedBy,
            ArchiveReason = @ArchiveReason,
            Active = 0  -- usually make sets inactive when archived
        WHERE SetId = @SetId;

        IF @ArchiveItems = 1
        BEGIN
            -- copy SetItem rows into SetItem_Archive (avoid duplicates)
            INSERT INTO dbo.SetItem_Archive
            (
                SetItemId, SetId, ItemId, ItemCode, Description, Quantity, UnitOfMeasure, UnitPrice, Amount,
                LineStartDate, LineEndDate, CreatedBy, CreatedAt, ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT
                si.SetItemId, si.SetId, si.ItemId, si.ItemCode, si.Description, si.Quantity, si.UnitOfMeasure, si.UnitPrice, si.Amount,
                si.LineStartDate, si.LineEndDate, si.CreatedBy, si.CreatedAt,
                SYSUTCDATETIME(), @ArchivedBy, CONCAT('Archived via Set(', @SetId, '): ', @ArchiveReason)
            FROM dbo.SetItem si
            WHERE si.SetId = @SetId
              AND NOT EXISTS (SELECT 1 FROM dbo.SetItem_Archive sia WHERE sia.SetItemId = si.SetItemId);

            -- optionally archive the linked Item rows too (copy to Item_Archive & mark IsArchived)
            INSERT INTO dbo.Item_Archive
            (
                ItemId, Name, Description, Active, UnitOfMeasure, StockOnHand,
                DateCreated, CreatedBy, DateModified, ModifiedBy,
                Category, SerialNumber, CategoryId, ModelNumber, Amount, ItemType,
                StartDate, EndDate, ConditionID, VendorId, Remarks, WarrantyYears,
                DurationYears, DurationStartDate, DurationEndDate, DatePurchased,
                LicenseNumber, WarrantyStartDate, ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT DISTINCT
                i.ItemId, i.Name, i.Description, i.Active, i.UnitOfMeasure, i.StockOnHand,
                i.DateCreated, i.CreatedBy, i.DateModified, i.ModifiedBy,
                i.Category, i.SerialNumber, i.CategoryId, i.ModelNumber, i.Amount, i.ItemType,
                i.StartDate, i.EndDate, i.ConditionID, i.VendorId, i.Remarks, i.WarrantyYears,
                i.DurationYears, i.DurationStartDate, i.DurationEndDate, i.DatePurchased,
                i.LicenseNumber, i.WarrantyStartDate, SYSUTCDATETIME(), @ArchivedBy, CONCAT('Archived via Set(', @SetId, '): ', @ArchiveReason)
            FROM dbo.SetItem si
            INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
            WHERE si.SetId = @SetId
              AND NOT EXISTS (SELECT 1 FROM dbo.Item_Archive ia WHERE ia.ItemId = i.ItemId);

            -- mark items IsArchived = 1 (and optionally Active = 0)
            UPDATE i
            SET i.IsArchived = 1,
                i.ArchivedAt = SYSUTCDATETIME(),
                i.ArchivedBy = @ArchivedBy,
                i.ArchiveReason = CONCAT('Archived via Set(', @SetId, '): ', @ArchiveReason),
                i.Active = CASE WHEN @DeactivateItems = 1 THEN 0 ELSE i.Active END
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
