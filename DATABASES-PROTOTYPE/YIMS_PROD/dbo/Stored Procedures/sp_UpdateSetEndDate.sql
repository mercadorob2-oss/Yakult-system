CREATE PROCEDURE sp_UpdateSetEndDate
    @SetId INT,
    @NewEndDate DATETIME2(7),
    @ModifiedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StartDate DATETIME2(7);
    DECLARE @ErrorMessage NVARCHAR(500);
    
    -- Get the Start Date
    SELECT @StartDate = StartDate
    FROM [Set]
    WHERE SetId = @SetId;
    
    -- Check if Set exists
    IF @StartDate IS NULL
    BEGIN
        RAISERROR('Set not found', 16, 1);
        RETURN;
    END
    
    -- Validate: End Date must be at least 1 year from Start Date
    IF DATEDIFF(YEAR, @StartDate, @NewEndDate) < 1
        OR DATEADD(YEAR, 1, @StartDate) > @NewEndDate
    BEGIN
        SET @ErrorMessage = 'End Date must be at least 1 year from Start Date (' + 
                           FORMAT(@StartDate, 'MM/dd/yyyy') + '). ' +
                           'Minimum allowed date: ' + FORMAT(DATEADD(YEAR, 1, @StartDate), 'MM/dd/yyyy');
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN;
    END
    
    -- Update the End Date
    UPDATE [Set]
    SET EndDate = @NewEndDate
    WHERE SetId = @SetId;
    
    SELECT 'End Date updated successfully' AS Message;
END
