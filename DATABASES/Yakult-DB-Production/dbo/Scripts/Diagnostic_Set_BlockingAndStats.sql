-- ============================================================
-- Diagnostic_Set_BlockingAndStats.sql
--
-- Run this if the Invoice page times out after an UPDATE script.
-- Step 1: Find any sessions blocking dbo.[Set]
-- Step 2: Kill the blocking session (if any)
-- Step 3: Refresh statistics on dbo.[Set]
-- ============================================================

-- ── Step 1: Check for blocking sessions on dbo.[Set] ────────
SELECT
    blocking.session_id        AS BlockingSessionId,
    blocking.status            AS BlockingStatus,
    blocking.login_name        AS BlockingLogin,
    blocking.host_name         AS BlockingHost,
    blocking.program_name      AS BlockingProgram,
    blocked.session_id         AS BlockedSessionId,
    blocked_req.wait_type,
    blocked_req.wait_time / 1000 AS WaitSeconds,
    SUBSTRING(blocked_sql.text, 1, 200) AS BlockedQuery
FROM sys.dm_exec_sessions   AS blocking
JOIN sys.dm_exec_requests   AS blocked_req ON blocking.session_id = blocked_req.blocking_session_id
JOIN sys.dm_exec_sessions   AS blocked     ON blocked_req.session_id = blocked.session_id
CROSS APPLY sys.dm_exec_sql_text(blocked_req.sql_handle) AS blocked_sql
WHERE blocked_req.blocking_session_id > 0;

-- ── Step 2: Kill a blocking session ─────────────────────────
-- Replace <BlockingSessionId> with the session_id from Step 1.
-- Only run this if Step 1 shows an open uncommitted transaction
-- left over from running the cleanup script in SSMS.
--
-- KILL <BlockingSessionId>;

-- ── Step 3: Refresh statistics on dbo.[Set] ─────────────────
-- Run after any bulk UPDATE to make sure SQL Server has an
-- accurate row estimate for vw_Invoices queries.
UPDATE STATISTICS dbo.[Set] WITH FULLSCAN;
UPDATE STATISTICS dbo.[User] WITH FULLSCAN;
UPDATE STATISTICS dbo.Company WITH FULLSCAN;
