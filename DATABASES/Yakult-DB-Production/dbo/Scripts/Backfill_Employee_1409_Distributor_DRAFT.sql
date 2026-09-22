-- DRAFT backfill (DO NOT RUN until reviewed): link Accounting Subs. to its distributor.
-- Scope: exactly one row (EmpId 1409). Everyone else stays NULL (selected few only).
-- Run on: Yakult_Inventory_System_DEV first, verify, then YIMS_PROD.
-- Preconditions verified 2026-09-22: zero requests reference EmpId 1409;
-- dbo.Employee.DistributorId + FK_Employee_Distributor exist on both DBs.

-- 0. Verify before (expect one row: ComId 21, DistributorId NULL)
SELECT EmpId, Name, ComId, BranchId, DeptId, DistributorId
FROM dbo.Employee WHERE EmpId = 1409;

-- 1. Fix: true employer is YPI (16); posted at Gensbio (DistributorId 27); keep Dept 66 + Branch 139.
--    Map: Company 21 (Gensbio-as-company) -> Distributor 27 GENSBIO MARKETING CORPORATION.
BEGIN TRANSACTION;
UPDATE dbo.Employee
SET ComId = 16,
    DistributorId = 27
WHERE EmpId = 1409
  AND ComId = 21
  AND DistributorId IS NULL;
-- Expect: (1 row affected). Otherwise ROLLBACK and investigate.
SELECT EmpId, Name, ComId, BranchId, DeptId, DistributorId
FROM dbo.Employee WHERE EmpId = 1409;
-- If correct: COMMIT; else: ROLLBACK.
-- COMMIT;
-- ROLLBACK (rollback statement):
-- UPDATE dbo.Employee SET ComId = 21, DistributorId = NULL WHERE EmpId = 1409;
