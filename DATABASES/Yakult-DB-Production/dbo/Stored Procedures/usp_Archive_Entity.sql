-- Archive an entity
CREATE   PROCEDURE [dbo].[usp_Archive_Entity]
    @EntityType NVARCHAR(50),
    @EntityId INT,
    @ArchivedBy NVARCHAR(100),
    @ArchiveReason NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @EntityType NOT IN ('Item', 'Inventory', 'Request', 'Set', 'Company', 'Department', 'Branch', 'Employee', 'Vendor', 'ItemCategory', 'Condition')
    BEGIN
        RAISERROR('Invalid EntityType', 16, 1);
        RETURN;
    END
    
    MERGE INTO [dbo].[ArchiveStatus] AS target
    USING (SELECT @EntityType AS EntityType, @EntityId AS EntityId) AS source
    ON target.EntityType = source.EntityType AND target.EntityId = source.EntityId
    WHEN MATCHED THEN
        UPDATE SET 
            IsArchived = 1,
            ArchivedAt = SYSDATETIME(),
            ArchivedBy = @ArchivedBy,
            ArchiveReason = @ArchiveReason,
            RestoredAt = NULL,
            RestoredBy = NULL
    WHEN NOT MATCHED THEN
        INSERT (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
        VALUES (@EntityType, @EntityId, 1, SYSDATETIME(), @ArchivedBy, @ArchiveReason);
END;
