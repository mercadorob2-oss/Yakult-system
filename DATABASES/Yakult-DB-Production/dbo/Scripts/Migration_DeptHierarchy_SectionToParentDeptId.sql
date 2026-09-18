-- ============================================================
-- DATA MIGRATION — Convert Section values into ParentDeptId
--
-- Background:
--   Department.Section stores sub-section labels (e.g. "ACTG SUBS")
--   that logically belong under a parent department (e.g. the
--   Accounting Department).  This script maps those relationships
--   by matching Section values to known parent department names.
--
-- Approach:
--   1. A mapping table (temp) declares which Section label belongs
--      under which parent Department.Name.
--   2. For each mapping, look up the parent DeptId by name and
--      company (ComId) then set ParentDeptId on the child rows.
--   3. Only updates rows where ParentDeptId is still NULL — safe
--      to re-run (idempotent).
--
-- !! BEFORE RUNNING !!
--   Review / extend the mapping block below to match your actual
--   Section values and parent department names in production.
--   Run the diagnostic SELECT at the bottom first to preview.
-- ============================================================

-- ── Pre-flight: show current Section distribution ────────────
SELECT [Section],
       COUNT(*) AS DeptCount
FROM   [dbo].[Department]
WHERE  [Section] IS NOT NULL
GROUP  BY [Section]
ORDER  BY DeptCount DESC;
GO

-- ── Migration ─────────────────────────────────────────────────
BEGIN TRANSACTION;

BEGIN TRY

    -- Mapping: Section label → parent Department name fragment
    -- Add / adjust rows here to match your production data.
    DECLARE @Mapping TABLE (
        SectionLabel    NVARCHAR(200),  -- exact value stored in Section
        ParentNameLike  NVARCHAR(200)   -- LIKE pattern to find the parent dept
    );

    INSERT INTO @Mapping (SectionLabel, ParentNameLike) VALUES
    -- Example mappings — edit these to match your real data:
        (N'ACTG SUBS',   N'%Accounting%'),
        (N'ACTG MAIN',   N'%Accounting%'),
        (N'HR SUBS',     N'%Human Resource%'),
        (N'HR MAIN',     N'%Human Resource%'),
        (N'IT SUBS',     N'%Information Technology%'),
        (N'IT MAIN',     N'%Information Technology%'),
        (N'OPS SUBS',    N'%Operations%'),
        (N'SALES SUBS',  N'%Sales%');
    -- Add more rows as needed.

    -- Apply mappings
    UPDATE  child
    SET     child.[ParentDeptId] = parent.[DeptId]
    FROM    [dbo].[Department] AS child
    JOIN    @Mapping            AS m
                ON child.[Section]  = m.[SectionLabel]
    JOIN    [dbo].[Department] AS parent
                ON parent.[Name]    LIKE m.ParentNameLike
               -- Scope to same company where both rows share a ComId;
               -- if your departments span companies, remove this clause.
               AND (parent.[ComId]  = child.[ComId]
                    OR (parent.[ComId] IS NULL AND child.[ComId] IS NULL))
               -- A department cannot be its own parent
               AND parent.[DeptId] <> child.[DeptId]
    WHERE   child.[ParentDeptId] IS NULL;   -- idempotent guard

    DECLARE @Updated INT = @@ROWCOUNT;
    PRINT CAST(@Updated AS VARCHAR) + ' Department row(s) linked to a parent.';

    COMMIT TRANSACTION;

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;

    DECLARE @Msg  NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @Line INT            = ERROR_LINE();
    RAISERROR('Migration failed at line %d: %s', 16, 1, @Line, @Msg);
END CATCH;
GO

-- ── Post-migration verification ───────────────────────────────
-- Shows which departments are now linked and which still have
-- a Section value but no ParentDeptId (needs a mapping entry).
SELECT
    child.[DeptId]       AS ChildDeptId,
    child.[Name]         AS ChildName,
    child.[Section]      AS Section,
    child.[ParentDeptId] AS ParentDeptId,
    parent.[Name]        AS ParentName,
    CASE
        WHEN child.[Section] IS NOT NULL AND child.[ParentDeptId] IS NULL
        THEN 'UNMAPPED — add to @Mapping'
        WHEN child.[ParentDeptId] IS NOT NULL
        THEN 'OK'
        ELSE 'No section'
    END AS Status
FROM       [dbo].[Department] AS child
LEFT JOIN  [dbo].[Department] AS parent ON parent.[DeptId] = child.[ParentDeptId]
ORDER BY   Status DESC, child.[Name];
GO
