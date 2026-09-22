-- DRAFT backfill (review before running): move distributor-as-company refs to DistributorId.
-- Map (ID-based, never name-matched): Company 22 -> Distributor 18 (Isabela),
-- Company 23 -> Distributor 6 (Biomate). Company 21 -> Distributor 27 (Gensbio,
-- only Emp 1409 used it; done separately).
-- Rule: distributor-only rows carry ComId NULL (precedent: Req 1503, Sets 1331/1438).
-- HELD OUT: dummy Set 1377 (ComId 23 + DistributorId 2 BIO-TECH, mismatched pair)
--   needs a human ruling before anything touches it. The guards below skip it.
-- Run on: DEV first, verify, then dummy. Each block commits only on exact rowcount.

-- ============ DEV: Request 1460 (ComId 22 -> Dist 18) ============
-- Pre: SELECT ReqId, ComId, DistributorId FROM dbo.Request WHERE ReqId = 1460;
BEGIN TRANSACTION;
UPDATE dbo.Request SET ComId = NULL, DistributorId = 18
WHERE ReqId = 1460 AND ComId = 22 AND DistributorId IS NULL;
IF @@ROWCOUNT = 1 BEGIN COMMIT; PRINT 'DEV Req1460 COMMITTED'; END
ELSE BEGIN ROLLBACK; PRINT 'DEV Req1460 ROLLED BACK'; END;

-- ============ DEV: Request 1514 (ComId 23 -> Dist 6) ============
BEGIN TRANSACTION;
UPDATE dbo.Request SET ComId = NULL, DistributorId = 6
WHERE ReqId = 1514 AND ComId = 23 AND DistributorId IS NULL;
IF @@ROWCOUNT = 1 BEGIN COMMIT; PRINT 'DEV Req1514 COMMITTED'; END
ELSE BEGIN ROLLBACK; PRINT 'DEV Req1514 ROLLED BACK'; END;

-- ============ Dummy: Sets with ComId 22 -> Dist 18, ComId NULL ============
-- Pre: SELECT SetId, ComId, DistributorId FROM dbo.[Set] WHERE ComId IN (22,23) AND DistributorId IS NULL;
BEGIN TRANSACTION;
UPDATE dbo.[Set] SET ComId = NULL, DistributorId = 18
WHERE ComId = 22 AND DistributorId IS NULL;
PRINT 'DUMMY Set22 rows: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));
-- Expect 4 rows (1298, 1312, 1314, 1321). COMMIT only if exact, else ROLLBACK.
-- COMMIT;
-- ROLLBACK;

-- ============ Dummy: Sets with ComId 23 -> Dist 6, ComId NULL ============
BEGIN TRANSACTION;
UPDATE dbo.[Set] SET ComId = NULL, DistributorId = 6
WHERE ComId = 23 AND DistributorId IS NULL;
PRINT 'DUMMY Set23 rows: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));
-- Expect 5 rows (1301, 1310, 1315, 1316, 1319). COMMIT only if exact, else ROLLBACK.
-- COMMIT;
-- ROLLBACK;

-- ============ Verify after (both DBs; expect 0 except held-out 1377) ============
-- SELECT ReqId, ComId, DistributorId FROM dbo.Request WHERE ComId IN (21,22,23);
-- SELECT SetId, ComId, DistributorId FROM dbo.[Set] WHERE ComId IN (21,22,23);
-- SELECT EmpId, ComId, DistributorId FROM dbo.Employee WHERE ComId IN (21,22,23);
