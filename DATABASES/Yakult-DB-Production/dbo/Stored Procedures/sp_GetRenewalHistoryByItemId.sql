
CREATE PROCEDURE [dbo].[sp_GetRenewalHistoryByItemId]
    @ItemId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.RenewalId,
        r.ItemId,
        r.RenewalStatus,
        r.OnHoldDate,
        r.RenewedDate,
        r.RenewalCount,
        r.NewStartDate,
        r.NewEndDate,
        r.RenewalYears,
        r.IsArchived,
        r.ArchivedDate,
        r.ArchiveReason,
        r.CreatedBy,
        r.CreatedAt,
        r.ModifiedBy,
        r.ModifiedAt,
        r.RenewalNotes,
        r.RenewalAmount,
        u.Name AS CreatedByUsername,      -- FIXED
        u2.Name AS ModifiedByUsername     -- FIXED
    FROM dbo.Renewals r
    LEFT JOIN dbo.[User] u ON r.CreatedBy = u.UserId
    LEFT JOIN dbo.[User] u2 ON r.ModifiedBy = u2.UserId
    WHERE r.ItemId = @ItemId
    ORDER BY r.CreatedAt DESC
END
