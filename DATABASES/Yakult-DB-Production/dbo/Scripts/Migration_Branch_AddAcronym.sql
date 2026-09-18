-- ============================================================
-- Migration: Add Acronym column to dbo.Branch
-- Acronym = {ConsonantAcronym}-{BranchTypeCode}
-- ConsonantAcronym = first 3 consonants of the place name
--   (vowels A E I O U removed; spaces removed)
--   e.g. PASAY→PSY, PASIG→PSG, PARANAQUE→PRN, SAN ANDRES→SNN
--   If < 3 consonants exist, last letter(s) of the name pad to 3.
--   Remaining collisions get a numeric suffix: PSY2, PSY3, etc.
-- BranchType mappings: Center=CT, Factory=Fac, Depot=DP,
--                      Distributor=DST
-- ============================================================

-- ── Step 1: Add Acronym column ────────────────────────────────
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.Branch')
      AND  name      = N'Acronym'
)
BEGIN
    ALTER TABLE [dbo].[Branch]
        ADD [Acronym] NVARCHAR(20) NULL;

    PRINT 'Column Acronym added to dbo.Branch.';
END
ELSE
BEGIN
    PRINT 'Column Acronym already exists on dbo.Branch — skipped.';
END
GO

-- ── Step 2: Populate/fix Acronym using consonant-based algorithm ────────────
--   Runs unconditionally so it also corrects previously populated rows.
--   Algorithm:
--     1. Strip the BranchType word from the end of Name → place name
--     2. Remove vowels (A E I O U) and spaces → consonant string
--     3. Take first 3 consonants; if < 3, pad with last char(s) of place name
--     4. ROW_NUMBER suffix (2, 3 …) handles any remaining collisions
;WITH
Stripped AS (
    -- Remove BranchType suffix to isolate the place name
    SELECT
        [BranchId], [Name], [BranchType],
        RTRIM(
            CASE
                WHEN [BranchType] IS NOT NULL
                     AND LEN(RTRIM([Name])) > LEN([BranchType])
                     AND UPPER(RIGHT(RTRIM([Name]), LEN([BranchType]))) = UPPER([BranchType])
                THEN LEFT(RTRIM([Name]), LEN(RTRIM([Name])) - LEN([BranchType]))
                ELSE [Name]
            END
        ) AS PlaceName
    FROM [dbo].[Branch]
    WHERE [BranchType] IS NOT NULL
),
WithConsonants AS (
    SELECT
        [BranchId], [Name], [BranchType], PlaceName,
        -- Strip vowels and spaces to get consonant-only string
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            UPPER(PlaceName),
            N' ', N''), N'A', N''), N'E', N''), N'I', N''), N'O', N''), N'U', N''
        ) AS Cstr
    FROM Stripped
),
WithAcrBase AS (
    SELECT
        [BranchId], [Name], [BranchType],
        -- Take first 3 consonants; if < 3, pad with last letter(s) of place name
        UPPER(
            CASE
                WHEN LEN(Cstr) >= 3
                    THEN LEFT(Cstr, 3)
                ELSE
                    LEFT(
                        Cstr + RIGHT(REPLACE(UPPER(PlaceName), N' ', N''), 3),
                        3
                    )
            END
        ) AS AcrBase
    FROM WithConsonants
),
Ranked AS (
    SELECT
        [BranchId], [Name], [BranchType], AcrBase,
        ROW_NUMBER() OVER (
            PARTITION BY AcrBase, [BranchType]
            ORDER BY [BranchId]
        ) AS rn
    FROM WithAcrBase
)
UPDATE b
SET    [Acronym] =
           r.AcrBase
           + CASE WHEN r.rn > 1 THEN CAST(r.rn AS NVARCHAR(5)) ELSE N'' END
           + N'-'
           + CASE r.[BranchType]
               WHEN N'Center'      THEN N'CT'
               WHEN N'Factory'     THEN N'Fac'
               WHEN N'Depot'       THEN N'DP'
               WHEN N'Distributor' THEN N'DST'
               ELSE UPPER(LEFT(ISNULL(r.[BranchType], N''), 3))
           END
FROM   [dbo].[Branch] b
JOIN   Ranked r ON b.[BranchId] = r.[BranchId];

PRINT CAST(@@ROWCOUNT AS VARCHAR) + N' Branch row(s) updated with consonant-based Acronym.';
GO

-- ── Verification ─────────────────────────────────────────────
SELECT [BranchId], [Name], [BranchType], [Acronym]
FROM   [dbo].[Branch]
ORDER  BY [BranchType], [Name];
GO
