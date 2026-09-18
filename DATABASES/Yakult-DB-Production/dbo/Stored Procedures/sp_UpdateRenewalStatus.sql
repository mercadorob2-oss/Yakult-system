
CREATE PROCEDURE [dbo].[sp_UpdateRenewalStatus]
    @RenewalId INT,
    @RenewalStatus NVARCHAR(20),
    @ModifiedBy INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Renewals
    SET RenewalStatus = @RenewalStatus,
        OnHoldDate = CASE WHEN @RenewalStatus = 'On Hold' AND OnHoldDate IS NULL THEN GETDATE() ELSE OnHoldDate END,
        RenewedDate = CASE WHEN @RenewalStatus = 'Renewed' AND RenewedDate IS NULL THEN GETDATE() ELSE RenewedDate END,
        ModifiedBy = @ModifiedBy,
        ModifiedAt = GETDATE()
    WHERE RenewalId = @RenewalId

    SELECT 'Success' AS Result
END
