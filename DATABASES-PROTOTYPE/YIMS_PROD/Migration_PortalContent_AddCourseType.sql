-- ============================================================
-- Migration: dbo.PortalContent — allow 'Course' content type
--
-- Prototype/testing migration. Adds the 'Course' value to the
-- CK_PortalContent_Type CHECK constraint so the portal can
-- author completion-tracked learning courses. Only touches the
-- constraint; existing rows and other types are unchanged.
-- Safe to run repeatedly.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints cc
    WHERE cc.name = 'CK_PortalContent_Type'
      AND cc.parent_object_id = OBJECT_ID('dbo.PortalContent')
      AND cc.definition LIKE '%''Course''%'
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = 'CK_PortalContent_Type'
          AND parent_object_id = OBJECT_ID('dbo.PortalContent')
    )
        ALTER TABLE dbo.PortalContent DROP CONSTRAINT CK_PortalContent_Type;

    ALTER TABLE dbo.PortalContent ADD CONSTRAINT CK_PortalContent_Type
        CHECK(ContentType IN('Article','Advisory','Video','Course','FAQ','Announcement','Guide'));
    PRINT 'CK_PortalContent_Type updated to allow Course.';
END
ELSE
BEGIN
    PRINT 'CK_PortalContent_Type already allows Course - skipped.';
END
GO
