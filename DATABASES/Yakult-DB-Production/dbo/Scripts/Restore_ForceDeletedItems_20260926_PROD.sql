-- ============================================================================
-- Data recovery: restore the 1,050 items force-deleted on YIMS_PROD on
-- 2026-09-26 16:03-16:06 (ItemAuditTrail 'Item Force Deleted', user Russel),
-- together with every row ForceDeleteItemWithRelations removed with them.
--
-- Source: YIMS_PROD_PIT_1603, a point-in-time copy restored from
--   YIMS_PROD.bak (backup set 28, 2026-09-25 17:23) +
--   YIMS_PROD_log_copyonly_20260926.trn  WITH STOPAT '2026-09-26T16:03:00'
-- i.e. after the 202 Sets were deleted (15:59-16:01, kept deleted on purpose)
-- and before the force delete began.
--
-- Only rows missing from YIMS_PROD are inserted (by primary key), so rows
-- created after the delete are kept. Also re-links Set.ReqId and
-- EmptyCartridge.ReqId, which the force delete set to NULL.
-- All-or-nothing; safe to re-run (a second run inserts nothing).
-- Run with: sqlcmd -I (QUOTED_IDENTIFIER ON is required by the filtered index on Item).
-- ============================================================================
USE YIMS_PROD;
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @Src SYSNAME = N'YIMS_PROD_PIT_1603';
DECLARE @Tables TABLE (Seq INT PRIMARY KEY, Name SYSNAME);
INSERT INTO @Tables VALUES
    (1, N'Item'), (2, N'Request'), (3, N'SetItem'), (4, N'Inventory'),
    (5, N'Renewals'), (6, N'Cartridge'), (7, N'CartridgeMovement'), (8, N'CartridgeRequestModel');

BEGIN TRANSACTION;

DECLARE @Seq INT = 0, @T SYSNAME, @Cols NVARCHAR(MAX), @Pk NVARCHAR(MAX), @HasId BIT, @Sql NVARCHAR(MAX), @N INT;
WHILE 1 = 1
BEGIN
    SELECT TOP 1 @Seq = Seq, @T = Name FROM @Tables WHERE Seq > @Seq ORDER BY Seq;
    IF @@ROWCOUNT = 0 BREAK;

    -- insertable columns: skip computed and rowversion columns
    SELECT @Cols = STUFF((SELECT N',' + QUOTENAME(c.name)
                          FROM sys.columns c
                          WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@T))
                            AND c.is_computed = 0 AND c.system_type_id <> 189
                          ORDER BY c.column_id FOR XML PATH('')), 1, 1, '');
    SELECT @Pk = STUFF((SELECT N' AND y.' + QUOTENAME(c.name) + N' = x.' + QUOTENAME(c.name)
                        FROM sys.indexes i
                        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE i.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@T)) AND i.is_primary_key = 1
                        FOR XML PATH('')), 1, 5, '');
    SET @HasId = OBJECTPROPERTY(OBJECT_ID(N'dbo.' + QUOTENAME(@T)), 'TableHasIdentity');

    SET @Sql =
          CASE WHEN @HasId = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@T) + N' ON; ' ELSE N'' END
        + N'INSERT INTO dbo.' + QUOTENAME(@T) + N' (' + @Cols + N') SELECT ' + @Cols
        + N' FROM ' + QUOTENAME(@Src) + N'.dbo.' + QUOTENAME(@T) + N' x'
        + N' WHERE NOT EXISTS (SELECT 1 FROM dbo.' + QUOTENAME(@T) + N' y WHERE ' + @Pk + N'); '
        + N'SET @n = @@ROWCOUNT; '
        + CASE WHEN @HasId = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@T) + N' OFF;' ELSE N'' END;
    EXEC sp_executesql @Sql, N'@n INT OUTPUT', @n = @N OUTPUT;
    PRINT CONCAT(@T, ': ', @N, ' row(s) restored');
END

-- Links the force delete nulled out
UPDATE s SET s.ReqId = p.ReqId
FROM   dbo.[Set] s
JOIN   YIMS_PROD_PIT_1603.dbo.[Set] p ON p.SetId = s.SetId
WHERE  s.ReqId IS NULL AND p.ReqId IS NOT NULL
  AND  EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ReqId = p.ReqId);
PRINT CONCAT('Set.ReqId re-linked: ', @@ROWCOUNT);

UPDATE e SET e.ReqId = p.ReqId
FROM   dbo.EmptyCartridge e
JOIN   YIMS_PROD_PIT_1603.dbo.EmptyCartridge p ON p.EmptyCartridgeId = e.EmptyCartridgeId
WHERE  e.ReqId IS NULL AND p.ReqId IS NOT NULL
  AND  EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ReqId = p.ReqId);
PRINT CONCAT('EmptyCartridge.ReqId re-linked: ', @@ROWCOUNT);

COMMIT TRANSACTION;
PRINT 'Committed.';
GO

SELECT COUNT(*) AS TotalItems, SUM(CASE WHEN Active = 1 THEN 1 ELSE 0 END) AS ActiveItems FROM dbo.Item;
GO
