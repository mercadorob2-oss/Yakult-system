
-- Restore an entity
CREATE   PROCEDURE [dbo].[usp_Restore_Entity]
    @EntityType NVARCHAR(50),
    @EntityId INT,
    @RestoredBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE [dbo].[ArchiveStatus]
    SET 
        IsArchived = 0,
        RestoredAt = SYSDATETIME(),
        RestoredBy = @RestoredBy
    WHERE EntityType = @EntityType
      AND EntityId = @EntityId;
    
    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR('Archive record not found', 16, 1);
        RETURN;
    END
END;
