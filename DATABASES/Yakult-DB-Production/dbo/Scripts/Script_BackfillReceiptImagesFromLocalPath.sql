-- Backfills receipt document bytes (SI/DR/PO) into the database from a locally-stored file path,
-- for any row where ImagePath is set but the bytes column is still NULL.
--
-- IMPORTANT LIMITATION: OPENROWSET(BULK ...) is executed by the SQL Server SERVICE ACCOUNT, not by
-- the person running this script. It can only read a path that account can see on disk:
--   - A UNC/shared network path the service account has read permission on, OR
--   - A local path that happens to sit on the same box SQL Server itself runs on.
-- Paths like "C:\Users\User\Desktop\Receipt\..." are normally local to one staff member's own PC and
-- are NOT reachable this way. For those, use the WinForms admin tool instead (Admin Portal ->
-- Developer Tools -> "Migrate Receipts to DB"), run from the PC that actually has the file.
--
-- Safe to re-run: every UPDATE is guarded by "<column> IS NULL", so an already-migrated row is a
-- no-op, and a row whose file isn't reachable from the server just fails that one iteration (logged
-- to #BackfillLog below) without affecting any other row.
--
-- If OPENROWSET itself is blocked with "Ad hoc access to OLE DB provider ... has been denied",
-- an admin needs to run once (server-level, may require a restart of the OPENROWSET feature):
--   EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
--   EXEC sp_configure 'Ad Hoc Distributed Queries', 1; RECONFIGURE;

SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#BackfillLog') IS NOT NULL DROP TABLE #BackfillLog;
CREATE TABLE #BackfillLog (
    ReceiptSetId INT NOT NULL,
    ImageId      INT NULL,          -- NULL = legacy ReceiptSet column; set = ReceiptSetDocumentImage row
    DocType      NVARCHAR(10) NOT NULL,
    ImagePath    NVARCHAR(500) NOT NULL,
    Status       NVARCHAR(20) NOT NULL,   -- 'MIGRATED' or 'SKIPPED'
    Message      NVARCHAR(400) NULL
);

-- ───────────────────────────── Legacy dbo.ReceiptSet columns ─────────────────────────────

DECLARE @ReceiptSetId INT, @Path NVARCHAR(500), @DocType NVARCHAR(10), @Column NVARCHAR(10);

DECLARE legacy_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT ReceiptSetId, SiImagePath, 'SI', 'SiImage' FROM dbo.ReceiptSet WHERE SiImagePath IS NOT NULL AND SiImage IS NULL
    UNION ALL
    SELECT ReceiptSetId, DrImagePath, 'DR', 'DrImage' FROM dbo.ReceiptSet WHERE DrImagePath IS NOT NULL AND DrImage IS NULL
    UNION ALL
    SELECT ReceiptSetId, PoImagePath, 'PO', 'PoImage' FROM dbo.ReceiptSet WHERE PoImagePath IS NOT NULL AND PoImage IS NULL;

OPEN legacy_cursor;
FETCH NEXT FROM legacy_cursor INTO @ReceiptSetId, @Path, @DocType, @Column;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        DECLARE @sql NVARCHAR(MAX) = N'
UPDATE dbo.ReceiptSet
SET ' + QUOTENAME(@Column) + N' = (SELECT BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@Path, '''', '''''') + N''', SINGLE_BLOB) AS x)
WHERE ReceiptSetId = @rsid AND ' + QUOTENAME(@Column) + N' IS NULL;';

        EXEC sp_executesql @sql, N'@rsid INT', @rsid = @ReceiptSetId;

        INSERT INTO #BackfillLog (ReceiptSetId, ImageId, DocType, ImagePath, Status, Message)
        VALUES (@ReceiptSetId, NULL, @DocType, @Path, 'MIGRATED', NULL);
    END TRY
    BEGIN CATCH
        INSERT INTO #BackfillLog (ReceiptSetId, ImageId, DocType, ImagePath, Status, Message)
        VALUES (@ReceiptSetId, NULL, @DocType, @Path, 'SKIPPED', ERROR_MESSAGE());
    END CATCH

    FETCH NEXT FROM legacy_cursor INTO @ReceiptSetId, @Path, @DocType, @Column;
END

CLOSE legacy_cursor;
DEALLOCATE legacy_cursor;

-- ─────────────────────── Multi-image dbo.ReceiptSetDocumentImage table ───────────────────────

IF OBJECT_ID('dbo.ReceiptSetDocumentImage', 'U') IS NOT NULL
BEGIN
    DECLARE @ImageId INT;

    DECLARE multi_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT ImageId, ReceiptSetId, DocType, ImagePath
        FROM dbo.ReceiptSetDocumentImage
        WHERE ImagePath IS NOT NULL AND ImageBytes IS NULL;

    OPEN multi_cursor;
    FETCH NEXT FROM multi_cursor INTO @ImageId, @ReceiptSetId, @DocType, @Path;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            -- OPENROWSET(BULK ...) requires a literal path, not a variable, so this must go
            -- through dynamic SQL (same reason the legacy branch above does).
            DECLARE @multiSql NVARCHAR(MAX) = N'
UPDATE dbo.ReceiptSetDocumentImage
SET ImageBytes = (SELECT BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@Path, '''', '''''') + N''', SINGLE_BLOB) AS x)
WHERE ImageId = @imgid AND ImageBytes IS NULL;';

            EXEC sp_executesql @multiSql, N'@imgid INT', @imgid = @ImageId;

            INSERT INTO #BackfillLog (ReceiptSetId, ImageId, DocType, ImagePath, Status, Message)
            VALUES (@ReceiptSetId, @ImageId, @DocType, @Path, 'MIGRATED', NULL);
        END TRY
        BEGIN CATCH
            INSERT INTO #BackfillLog (ReceiptSetId, ImageId, DocType, ImagePath, Status, Message)
            VALUES (@ReceiptSetId, @ImageId, @DocType, @Path, 'SKIPPED', ERROR_MESSAGE());
        END CATCH

        FETCH NEXT FROM multi_cursor INTO @ImageId, @ReceiptSetId, @DocType, @Path;
    END

    CLOSE multi_cursor;
    DEALLOCATE multi_cursor;
END

-- ───────────────────────────────────── Results ─────────────────────────────────────

SELECT * FROM #BackfillLog ORDER BY Status, ReceiptSetId, DocType;

SELECT
    Status,
    Count = COUNT(1)
FROM #BackfillLog
GROUP BY Status;
