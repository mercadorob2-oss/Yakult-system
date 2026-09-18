CREATE PROCEDURE dbo.FinalizeSet
    @SetId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRAN;

    -- Guard 1: Set exists
    IF NOT EXISTS (SELECT 1 FROM dbo.[Set] WHERE SetId = @SetId)
    BEGIN
        ROLLBACK;
        THROW 51000, 'Set does not exist', 1;
    END

    -- Guard 2: Requests exist
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Request WHERE SetId = @SetId
    )
    BEGIN
        ROLLBACK;
        THROW 51001, 'No Requests linked to this Set', 1;
    END

    -- Guard 3: Prevent duplicates
    IF EXISTS (
        SELECT 1 FROM dbo.SetItem WHERE SetId = @SetId
    )
    BEGIN
        ROLLBACK;
        THROW 51002, 'SetItems already created', 1;
    END

    -- Create snapshot
    INSERT dbo.SetItem (
        SetId,
        ItemId,
        Quantity,
        UnitPrice,
        Amount,
        CreatedBy
    )
    SELECT
        r.SetId,
        r.ItemId,
        r.Quantity,
        r.UnitPrice,
        r.Quantity * r.UnitPrice,
        r.CreatedBy
    FROM dbo.Request r
    WHERE r.SetId = @SetId;

    COMMIT;
END
