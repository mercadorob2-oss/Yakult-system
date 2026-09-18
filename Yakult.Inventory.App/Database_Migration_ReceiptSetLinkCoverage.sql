/* =====================================================================================
   Receipt Sets - Coverage Period Support (PROD/TEST Migration)

   Purpose:
   - Allow multiple Receipt Sets per Set across renewal periods (e.g., 2025-2026, 2026-2027)
   - Periods are based on dbo.Renewals.NewStartDate/NewEndDate (latest renewal per item)
   - Enforce ONE receipt set per Set per coverage period

   Notes:
   - Existing deployments may already have dbo.ReceiptSetLink with a UNIQUE SetId constraint
     which blocks multiple years. This script replaces that with a period-based unique index.
   - Safe to run multiple times (idempotent checks).
   ===================================================================================== */

SET NOCOUNT ON;
GO

/* ---- Preconditions ---- */
IF OBJECT_ID('dbo.ReceiptSet', 'U') IS NULL
    THROW 54001, 'Missing dbo.ReceiptSet.', 1;
IF OBJECT_ID('dbo.ReceiptSetLink', 'U') IS NULL
    THROW 54002, 'Missing dbo.ReceiptSetLink.', 1;
IF OBJECT_ID('dbo.[Set]', 'U') IS NULL
    THROW 54003, 'Missing dbo.[Set].', 1;
GO

/* =====================================================================================
   1) Add coverage columns (nullable)
   ===================================================================================== */

IF COL_LENGTH('dbo.ReceiptSetLink', 'CoverageStartDate') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptSetLink
    ADD CoverageStartDate DATETIME2(7) NULL;
END
GO

IF COL_LENGTH('dbo.ReceiptSetLink', 'CoverageEndDate') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptSetLink
    ADD CoverageEndDate DATETIME2(7) NULL;
END
GO

/* =====================================================================================
   2) Backfill coverage for existing links using Set.StartDate/EndDate
      (Option A behavior: sets with no renewals still have a current period label)
   ===================================================================================== */

UPDATE l
SET
    CoverageStartDate = COALESCE(l.CoverageStartDate, s.StartDate),
    CoverageEndDate = COALESCE(l.CoverageEndDate, s.EndDate)
FROM dbo.ReceiptSetLink l
INNER JOIN dbo.[Set] s ON s.SetId = l.SetId
WHERE (l.CoverageStartDate IS NULL OR l.CoverageEndDate IS NULL)
  AND (s.StartDate IS NOT NULL OR s.EndDate IS NOT NULL);
GO

/* =====================================================================================
   3) Drop old UNIQUE constraint on SetId (blocks multiple periods)
      Common names: UQ_ReceiptSetLink_SetId or similar
   ===================================================================================== */

DECLARE @uqName SYSNAME;
SELECT TOP 1 @uqName = kc.name
FROM sys.key_constraints kc
INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
WHERE t.name = 'ReceiptSetLink'
  AND kc.[type] = 'UQ';

IF @uqName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'ALTER TABLE dbo.ReceiptSetLink DROP CONSTRAINT [' + REPLACE(@uqName, ']', ']]') + N'];';
    EXEC sp_executesql @sql;
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ReceiptSetLink') AND name = 'UQ_ReceiptSetLink_SetId')
BEGIN
    DROP INDEX UQ_ReceiptSetLink_SetId ON dbo.ReceiptSetLink;
END
GO

/* =====================================================================================
   4) Add period-based unique index: one receipt set per Set per coverage
   ===================================================================================== */

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ReceiptSetLink')
      AND name = 'UX_ReceiptSetLink_Set_Coverage'
)
BEGIN
    CREATE UNIQUE INDEX UX_ReceiptSetLink_Set_Coverage
    ON dbo.ReceiptSetLink (SetId, CoverageStartDate, CoverageEndDate)
    WHERE CoverageStartDate IS NOT NULL AND CoverageEndDate IS NOT NULL;
END
GO

