-- Migration: Create dbo.Portal and dbo.RolePortalAccess, then seed initial access rules
-- PortalKey values must match the C# PermissionResolver.Portal enum names exactly.
-- To change access: INSERT / DELETE rows in dbo.RolePortalAccess.
-- The app re-reads this table on each login (cached per session).

-- ── 1. Create dbo.Portal ─────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.Portal')
)
BEGIN
    CREATE TABLE dbo.Portal (
        PortalId    INT           IDENTITY(1,1) NOT NULL,
        PortalKey   VARCHAR(50)   NOT NULL,
        DisplayName VARCHAR(100)  NOT NULL,
        IsActive    BIT           NOT NULL DEFAULT (1),
        DateCreated DATETIME      NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT PK_Portal       PRIMARY KEY CLUSTERED (PortalId ASC),
        CONSTRAINT UQ_Portal_Key   UNIQUE (PortalKey)
    );
END

-- ── 2. Create dbo.RolePortalAccess ───────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RolePortalAccess')
)
BEGIN
    CREATE TABLE dbo.RolePortalAccess (
        RoleId   INT NOT NULL,
        PortalId INT NOT NULL,
        CONSTRAINT PK_RolePortalAccess        PRIMARY KEY CLUSTERED (RoleId ASC, PortalId ASC),
        CONSTRAINT FK_RolePortalAccess_Role   FOREIGN KEY (RoleId)   REFERENCES dbo.Role(RoleId),
        CONSTRAINT FK_RolePortalAccess_Portal FOREIGN KEY (PortalId) REFERENCES dbo.Portal(PortalId)
    );
END

-- ── 3. Seed portals (idempotent) ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'InventorySystem')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('InventorySystem',     'Inventory System');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'CallITMonitoring')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('CallITMonitoring',    'Call IT Monitoring');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'RequesterPortal')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('RequesterPortal',     'Requester Portal');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'CartridgeManagement')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('CartridgeManagement', 'Cartridge Management');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'BorrowItems')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('BorrowItems',         'Borrow Items');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'Reports')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('Reports',             'Reports');

IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'AdminPortal')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('AdminPortal',         'Admin Portal');

-- ── 4. Seed role-portal access (idempotent) ───────────────────────────────────
-- Helper: inserts one row only if it doesn't exist yet.
-- Format: (RoleName, PortalKey)

-- Admin — full access except Admin Portal
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Admin'
  AND p.PortalKey IN ('InventorySystem','CallITMonitoring','RequesterPortal',
                      'CartridgeManagement','BorrowItems','Reports')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- InventoryManager — Inventory System + Reports
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'InventoryManager'
  AND p.PortalKey IN ('InventorySystem','Reports')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- Requester — Requester Portal only
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Requester'
  AND p.PortalKey IN ('RequesterPortal')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- Viewer — read-only across three portals
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Viewer'
  AND p.PortalKey IN ('InventorySystem','CallITMonitoring','RequesterPortal')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- IT Manager — all operational portals + Admin Portal
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'IT Manager'
  AND p.PortalKey IN ('InventorySystem','CallITMonitoring','CartridgeManagement',
                      'BorrowItems','Reports','AdminPortal')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- Supervisor
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Supervisor'
  AND p.PortalKey IN ('InventorySystem','CallITMonitoring','CartridgeManagement',
                      'BorrowItems','Reports')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- Tech Support
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Tech Support'
  AND p.PortalKey IN ('CallITMonitoring','CartridgeManagement','BorrowItems')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );
