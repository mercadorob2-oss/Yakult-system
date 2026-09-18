-- ============================================================
-- Diagnostic: Department — Orphaned DeptId reference check
-- ============================================================
-- PURPOSE
--   After the department deduplication and cleanup migrations,
--   checks every table that references dbo.Department to confirm
--   no rows still point at deleted or missing DeptIds.
--
-- HOW TO RUN
--   Run each block individually (highlight + execute).
--   Expected result for every block: 0 rows.
--   Any rows returned need a follow-up remap migration.
-- ============================================================

-- ── BorrowLog ─────────────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- BorrowLog.BorrowedByDeptId ---';
SELECT bl.BorrowId, bl.BorrowedByDeptId, 'BorrowedByDeptId' AS Column_
FROM   dbo.BorrowLog bl
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = bl.BorrowedByDeptId);
-- Expected: 0 rows
GO

PRINT '--- BorrowLog.ReturnedByDeptId ---';
SELECT bl.BorrowId, bl.ReturnedByDeptId, 'ReturnedByDeptId' AS Column_
FROM   dbo.BorrowLog bl
WHERE  bl.ReturnedByDeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = bl.ReturnedByDeptId);
-- Expected: 0 rows
GO

-- ── CartridgeApproval ─────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- CartridgeApproval.DeptId ---';
SELECT ca.ApprovalId, ca.DeptId
FROM   dbo.CartridgeApproval ca
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = ca.DeptId);
-- Expected: 0 rows
GO

-- ── CartridgeAuthorization ────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- CartridgeAuthorization.DepartmentId ---';
SELECT ca.AuthorizationId, ca.DepartmentId
FROM   dbo.CartridgeAuthorization ca
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = ca.DepartmentId);
-- Expected: 0 rows
GO

-- ── CartridgeMovement ─────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- CartridgeMovement.DeptId ---';
SELECT cm.MovementId, cm.DeptId
FROM   dbo.CartridgeMovement cm
WHERE  cm.DeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = cm.DeptId);
-- Expected: 0 rows
GO

-- ── EmptyCartridge ────────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- EmptyCartridge.DeptId ---';
SELECT ec.EmptyCartridgeId, ec.DeptId
FROM   dbo.EmptyCartridge ec
WHERE  ec.DeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = ec.DeptId);
-- Expected: 0 rows
GO

-- ── Set ───────────────────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- Set.CurrentDepartmentId ---';
SELECT s.SetId, s.CurrentDepartmentId
FROM   dbo.[Set] s
WHERE  s.CurrentDepartmentId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = s.CurrentDepartmentId);
-- Expected: 0 rows
GO

-- ── SetTransfer ───────────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- SetTransfer.FromDepartmentId ---';
SELECT st.SetTransferId, st.FromDepartmentId
FROM   dbo.SetTransfer st
WHERE  st.FromDepartmentId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = st.FromDepartmentId);
-- Expected: 0 rows
GO

PRINT '--- SetTransfer.ToDepartmentId ---';
SELECT st.SetTransferId, st.ToDepartmentId
FROM   dbo.SetTransfer st
WHERE  st.ToDepartmentId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = st.ToDepartmentId);
-- Expected: 0 rows
GO

-- ── CallTicket ────────────────────────────────────────────────
-- Highlight this block and execute.
PRINT '--- CallTicket.DeptId ---';
SELECT ct.TicketId, ct.DeptId
FROM   dbo.CallTicket ct
WHERE  ct.DeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = ct.DeptId);
-- Expected: 0 rows
GO

-- ── CallDepartmentNotificationRecipient ───────────────────────
-- Highlight this block and execute.
PRINT '--- CallDepartmentNotificationRecipient.DeptId ---';
SELECT DeptId
FROM   dbo.CallDepartmentNotificationRecipient
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = CallDepartmentNotificationRecipient.DeptId);
-- Expected: 0 rows
GO

-- ── CallDepartmentSmtpProfile ─────────────────────────────────
-- Highlight this block and execute.
PRINT '--- CallDepartmentSmtpProfile.DeptId ---';
SELECT DeptId
FROM   dbo.CallDepartmentSmtpProfile
WHERE  DeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = CallDepartmentSmtpProfile.DeptId);
-- Expected: 0 rows
GO

-- ── CallDepartmentSmtpProfileLink ────────────────────────────
-- Highlight this block and execute.
PRINT '--- CallDepartmentSmtpProfileLink.DeptId ---';
SELECT DeptId
FROM   dbo.CallDepartmentSmtpProfileLink
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = CallDepartmentSmtpProfileLink.DeptId);
-- Expected: 0 rows
GO

-- ── ItemAuditTrail ────────────────────────────────────────────
-- (no FK constraint — soft reference, needs explicit check)
-- Highlight this block and execute.
PRINT '--- ItemAuditTrail.DepartmentId ---';
SELECT iat.Id, iat.DepartmentId
FROM   dbo.ItemAuditTrail iat
WHERE  iat.DepartmentId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = iat.DepartmentId);
-- Expected: 0 rows
GO

-- ── UnfulfilledCartridgeExchange ──────────────────────────────
-- Highlight this block and execute.
PRINT '--- UnfulfilledCartridgeExchange.DeptId ---';
SELECT uce.UnfulfilledId, uce.DeptId
FROM   dbo.UnfulfilledCartridgeExchange uce
WHERE  uce.DeptId IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = uce.DeptId);
-- Expected: 0 rows
GO
