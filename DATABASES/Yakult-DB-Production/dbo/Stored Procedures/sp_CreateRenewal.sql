
CREATE PROCEDURE [dbo].[sp_CreateRenewal]
    @ItemId INT,
    @RenewalStatus NVARCHAR(20),
    @NewStartDate DATETIME2(7),
    @NewEndDate DATETIME2(7),
    @RenewalYears INT,
    @RenewalAmount DECIMAL(18,2) = NULL,
    @RenewalNotes NVARCHAR(1000) = NULL,
    @CreatedBy INT,
    @RenewalId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Increment renewal count
        DECLARE @NewRenewalCount INT = (
            SELECT ISNULL(MAX(RenewalCount), 0) + 1
            FROM dbo.Renewals
            WHERE ItemId = @ItemId
        )

        -- Insert new renewal record
        INSERT INTO dbo.Renewals (
            ItemId,
            RenewalStatus,
            OnHoldDate,
            RenewedDate,
            RenewalCount,
            NewStartDate,
            NewEndDate,
            RenewalYears,
            IsArchived,
            CreatedBy,
            CreatedAt,
            RenewalNotes,
            RenewalAmount
        )
        VALUES (
            @ItemId,
            @RenewalStatus,
            CASE WHEN @RenewalStatus = 'On Hold' THEN GETDATE() ELSE NULL END,
            CASE WHEN @RenewalStatus = 'Renewed' THEN GETDATE() ELSE NULL END,
            @NewRenewalCount,
            @NewStartDate,
            @NewEndDate,
            @RenewalYears,
            0,
            @CreatedBy,
            GETDATE(),
            @RenewalNotes,
            @RenewalAmount
        )

        SET @RenewalId = SCOPE_IDENTITY()

        -- If status is Renewed, update the Item table with new dates
        IF @RenewalStatus = 'Renewed'
        BEGIN
            UPDATE dbo.Item
            SET StartDate = @NewStartDate,
                EndDate = @NewEndDate,
                DateModified = GETDATE(),
                ModifiedBy = @CreatedBy
            WHERE ItemId = @ItemId
        END

        COMMIT TRANSACTION;

        SELECT @RenewalId AS RenewalId, 'Success' AS Result
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
        RAISERROR(@ErrorMessage, 16, 1)
    END CATCH
END
