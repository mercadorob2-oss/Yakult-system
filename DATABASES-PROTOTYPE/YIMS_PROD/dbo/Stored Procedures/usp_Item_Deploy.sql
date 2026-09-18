CREATE   PROCEDURE dbo.usp_Item_Deploy
    @ItemId INT,
    @StartDate DATETIME2(7) = NULL  -- StartDate is the dispatch date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Use current UTC time if not provided
    IF @StartDate IS NULL 
        SET @StartDate = SYSUTCDATETIME();

    BEGIN TRAN;

    DECLARE @ExistingDurationStart DATETIME2(7);

    SELECT @ExistingDurationStart = DurationStartDate
    FROM dbo.Item
    WHERE ItemId = @ItemId;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK;
        RAISERROR('Item not found.', 16, 1);
        RETURN;
    END

    -- Prevent redeployment - duration cannot be changed once set
    IF @ExistingDurationStart IS NOT NULL
    BEGIN
        ROLLBACK;
        RAISERROR('Item already deployed. Duration cannot be changed once set.', 16, 1);
        RETURN;
    END

    -- Set StartDate (dispatch date) and DurationStartDate (based on StartDate)
    -- DurationEndDate will be computed automatically (DurationStartDate + DurationYears)
    UPDATE dbo.Item
    SET 
        StartDate = @StartDate,
        DurationStartDate = @StartDate  -- Based on StartDate (dispatch date)
    WHERE ItemId = @ItemId;

    COMMIT;
END;
