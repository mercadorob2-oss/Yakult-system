
CREATE PROCEDURE [dbo].[sp_AutoArchiveExpiredRenewals]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ArchivedCount INT = 0

    -- Find all renewals that are On Hold for more than 1 year
    DECLARE @RenewalId INT, @ItemId INT

    DECLARE renewal_cursor CURSOR FOR
    SELECT RenewalId, ItemId
    FROM dbo.Renewals
    WHERE RenewalStatus = 'On Hold'
        AND IsArchived = 0
        AND OnHoldDate IS NOT NULL
        AND DATEDIFF(DAY, OnHoldDate, GETDATE()) > 365

    OPEN renewal_cursor
    FETCH NEXT FROM renewal_cursor INTO @RenewalId, @ItemId

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            EXEC sp_ArchiveRenewal
                @RenewalId = @RenewalId,
                @ItemId = @ItemId,
                @ArchiveReason = 'Auto-archived: On Hold for more than 1 year without renewal',
                @ArchivedBy = 'SYSTEM'

            SET @ArchivedCount = @ArchivedCount + 1
        END TRY
        BEGIN CATCH
            PRINT 'Error archiving RenewalId: ' + CAST(@RenewalId AS NVARCHAR(10))
        END CATCH

        FETCH NEXT FROM renewal_cursor INTO @RenewalId, @ItemId
    END

    CLOSE renewal_cursor
    DEALLOCATE renewal_cursor

    SELECT @ArchivedCount AS ArchivedCount, 'Success' AS Result
END
