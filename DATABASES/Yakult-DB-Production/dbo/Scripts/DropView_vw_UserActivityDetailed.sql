-- ============================================================
-- DROP: dbo.vw_UserActivityDetailed
-- Safe: guarded by IF EXISTS — no error if already absent.
-- ============================================================

IF OBJECT_ID('dbo.vw_UserActivityDetailed', 'V') IS NOT NULL
    DROP VIEW dbo.vw_UserActivityDetailed;

PRINT 'dbo.vw_UserActivityDetailed dropped (or did not exist).';
