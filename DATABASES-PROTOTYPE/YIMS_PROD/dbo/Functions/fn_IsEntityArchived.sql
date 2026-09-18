CREATE   FUNCTION [dbo].[fn_IsEntityArchived]
(
    @EntityType NVARCHAR(50),
    @EntityId INT
)
RETURNS BIT
AS
BEGIN
    DECLARE @IsArchived BIT = 0;
    
    SELECT @IsArchived = IsArchived
    FROM [dbo].[ArchiveStatus]
    WHERE EntityType = @EntityType
      AND EntityId = @EntityId;
    
    RETURN ISNULL(@IsArchived, 0);
END;
