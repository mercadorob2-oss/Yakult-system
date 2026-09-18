-- =============================================================================
-- Migration: Create dbo.RolePermissionItem
-- Purpose:   Role-level baseline for the generic PermissionItem model (Page /
--            ItemCategory / Action), mirroring how dbo.RolePortalAccess provides
--            a role-level baseline for dbo.Portal. Same default-allow model as
--            dbo.UserPermissionItem: absence = allowed; IsGranted = 0 = this role
--            is restricted from that item by default.
--
--            Precedence when a user has this role: an explicit dbo.UserPermissionItem
--            row for that user is always authoritative (grant or deny). Otherwise, if
--            ANY of the user's assigned roles restricts the item here, the item is
--            restricted for that user (most-restrictive-role-wins), matching the
--            "restrict by exception" spirit of the rest of this model. Otherwise
--            default-allow applies, same as today.
-- Run after: Migration_PermissionItem_CreateTables.sql
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.RolePermissionItem') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.RolePermissionItem (
        RoleId           INT NOT NULL,
        PermissionItemId INT NOT NULL,
        IsGranted        BIT      NOT NULL DEFAULT (0),
        DateModified     DATETIME NOT NULL DEFAULT (getdate()),
        ModifiedByUserId INT NULL,

        CONSTRAINT PK_RolePermissionItem PRIMARY KEY CLUSTERED (RoleId, PermissionItemId),
        CONSTRAINT FK_RolePermissionItem_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role (RoleId),
        CONSTRAINT FK_RolePermissionItem_Item FOREIGN KEY (PermissionItemId) REFERENCES dbo.PermissionItem (PermissionItemId)
    );
END
