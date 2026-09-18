CREATE PROCEDURE dbo.ArchiveRequest_ById
    @ReqId INT,
    @ArchivedBy NVARCHAR(100) = NULL,
    @ArchiveReason NVARCHAR(255) = NULL,
    @Deactivate BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @ReqId IS NULL BEGIN RAISERROR('ReqId required',16,1); RETURN; END
    BEGIN TRY
        BEGIN TRAN;
        IF OBJECT_ID('dbo.Request_Archive','U') IS NULL
        BEGIN RAISERROR('Request_Archive not found',16,1); ROLLBACK; RETURN; END

        IF NOT EXISTS (SELECT 1 FROM dbo.Request_Archive WHERE ReqId = @ReqId)
        BEGIN
            INSERT INTO dbo.Request_Archive
            (
                ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
                DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId, UnitPrice,
                Active, ArchivedAt, ArchivedBy, ArchiveReason
            )
            SELECT
                ReqId, DateRequested, Description, Remarks, Status, EntryType, Quantity,
                DateCreated, CreatedBy, DateModified, ModifiedBy, ItemId, EmpId, SetId, UnitPrice,
                Active, SYSUTCDATETIME(), @ArchivedBy, @ArchiveReason
            FROM dbo.Request WHERE ReqId = @ReqId;
        END

        UPDATE dbo.Request
        SET IsArchived = 1,
            ArchivedAt = SYSUTCDATETIME(),
            ArchivedBy = @ArchivedBy,
            ArchiveReason = @ArchiveReason,
            Active = CASE WHEN @Deactivate = 1 THEN 0 ELSE Active END
        WHERE ReqId = @ReqId;

        COMMIT;
    END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END
