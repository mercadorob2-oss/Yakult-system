-- =============================================================================
-- Migration: Add IsGranted to dbo.UserPortalAccess
-- Purpose:   Lets an admin explicitly DENY a portal for a user even when their
--            role would otherwise grant it, not just additively grant portals
--            beyond their role. A row is now authoritative over role access:
--            IsGranted = 1 -> access granted regardless of role
--            IsGranted = 0 -> access denied regardless of role
--            no row        -> falls back to role-based access (unchanged)
-- Run after: Migration_UserPortalAccess_CreateTable.sql
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.UserPortalAccess') AND name = 'IsGranted'
)
BEGIN
    ALTER TABLE dbo.UserPortalAccess ADD IsGranted BIT NOT NULL DEFAULT (1);
END
