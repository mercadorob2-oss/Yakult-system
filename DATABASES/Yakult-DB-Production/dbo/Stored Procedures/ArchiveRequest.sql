
CREATE PROCEDURE dbo.ArchiveRequest
    @ReqId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @ReqId IS NULL
    BEGIN
        RAISERROR('ReqId is required', 16, 1); RETURN;
    END

    BEGIN TRAN;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.Request_Archive WHERE ReqId = @ReqId)
        BEGIN
            INSERT INTO dbo.Request_Archive
            (ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
             DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId, UnitPrice,
             ArchivedAt, ArchivedBy, ArchiveReason)
            SELECT
                ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
                DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId, UnitPrice,
                SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Request
            WHERE ReqId = @ReqId;
        END

        UPDATE dbo.Request SET Active = 0 WHERE ReqId = @ReqId;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
