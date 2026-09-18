
CREATE   PROCEDURE [dbo].[usp_Restore_Request_Cascade]
    @ReqId INT,
    @RestoredBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ReqId IS NULL
        THROW 50001, 'ReqId is required.', 1;

    IF @RestoredBy IS NULL OR LTRIM(RTRIM(@RestoredBy)) = ''
        SET @RestoredBy = 'System';

    BEGIN TRAN;

    -- Mark Request active
    UPDATE dbo.Request
    SET Active = 1
    WHERE ReqId = @ReqId;

    -- Unarchive Request in ArchiveStatus (if present)
    UPDATE dbo.ArchiveStatus
    SET IsArchived = 0,
        RestoredAt = SYSDATETIME(),
        RestoredBy = @RestoredBy
    WHERE EntityType = 'Request'
      AND EntityId = @ReqId
      AND IsArchived = 1;

    -- Restore archived Inventory linked to Request
    UPDATE inv
    SET inv.Active = 1
    FROM dbo.Inventory inv
    INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
    WHERE inv.ReqId = @ReqId
      AND a.IsArchived = 1;

    UPDATE a
    SET a.IsArchived = 0,
        a.RestoredAt = SYSDATETIME(),
        a.RestoredBy = @RestoredBy
    FROM dbo.ArchiveStatus a
    INNER JOIN dbo.Inventory inv ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
    WHERE inv.ReqId = @ReqId
      AND a.IsArchived = 1;

    -- Restore Item archived explicitly "with Request <id>"
    UPDATE i
    SET i.Active = 1
    FROM dbo.Item i
    INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Item' AND a.EntityId = i.ItemId
    WHERE a.IsArchived = 1
      AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%');

    UPDATE a
    SET a.IsArchived = 0,
        a.RestoredAt = SYSDATETIME(),
        a.RestoredBy = @RestoredBy
    FROM dbo.ArchiveStatus a
    WHERE a.EntityType = 'Item'
      AND a.IsArchived = 1
      AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%');

    COMMIT;
END
