/* =============================================================================
   Seed YIMS2 with the data Yakult.SystemsPortal needs, copied from
   Yakult_Inventory_System_DEV  (same server: 192.168.100.186,50301).

   Scope: the 20 tables Yakult.SystemsPortal reads/writes that actually hold
   rows in DEV. The 9 empty ones it also touches (AccountRequest, CallTicket,
   CallTicketHistory, CallTicketNote, EmployeeResource, EmployeeResourceAttachment,
   EmployeeResourceHighlight, EmployeeResourceRevision, PortalContentProgress)
   are skipped - nothing to copy.

   Method:
     1. Abort unless run against YIMS2 and every target table is empty.
     2. Disable FK constraints on the target tables (they have a User<->Employee
        cycle, so a strict insert order is impossible).
     3. Copy each table DEV -> YIMS2 with IDENTITY_INSERT, matching columns by
        NAME and intersecting DEV's column set with YIMS2's (YIMS2.[User] has 3
        extra columns DEV lacks; rowversion/computed columns are excluded).
     4. Re-enable + re-check every FK constraint WITH CHECK; report any that fail.
     5. Reseed identity counters and print DEV-vs-YIMS2 row counts.

   Whole thing runs in ONE transaction - it either all lands or rolls back.
   Idempotent by guard: re-running after a successful load aborts at step 1
   (tables no longer empty).
   ============================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- required by the XML .value() call that builds column lists
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SourceDb  sysname = N'Yakult_Inventory_System_DEV';
DECLARE @msg nvarchar(2000), @sql nvarchar(max), @tbl sysname, @cols nvarchar(max),
        @hasIdent bit, @ord int, @rc int, @dbname sysname = DB_NAME();

/* ---- ordered target list (parents first; only cosmetic - FKs are off during load) ---- */
DECLARE @targets TABLE (ord int, tbl sysname);
INSERT INTO @targets (ord, tbl) VALUES
 ( 1,'AccountLevel'), ( 2,'Title'), ( 3,'Role'), ( 4,'User'),
 ( 5,'EmailAddress'), ( 6,'Company'), ( 7,'Department'), ( 8,'Branch'),
 ( 9,'Employee'), (10,'UserRole'), (11,'DepartmentEmail'), (12,'DepartmentAccount'),
 (13,'EmployeeEmail'), (14,'AuditTrail'), (15,'EmployeeResourceCategory'),
 (16,'PortalContentCategory'), (17,'PortalContent'), (18,'PortalContentLink'),
 (19,'PortalCard'), (20,'PortalSetting');

/* =========================== 1. GUARDS =================================== */
IF @dbname <> N'YIMS2'
BEGIN
    RAISERROR('ABORT: run this against YIMS2 (current DB is %s).', 16, 1, @dbname);
    RETURN;
END

IF DB_ID(@SourceDb) IS NULL
BEGIN
    RAISERROR('ABORT: source database %s not found on this server.', 16, 1, @SourceDb);
    RETURN;
END

DECLARE @nonEmpty nvarchar(2000) = N'';
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT tbl FROM @targets ORDER BY ord;
OPEN cur; FETCH NEXT FROM cur INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'IF EXISTS (SELECT 1 FROM dbo.' + QUOTENAME(@tbl) + N') '
             + N'SELECT @o = @o + '', '' + ''' + @tbl + N''';';
    EXEC sp_executesql @sql, N'@o nvarchar(2000) OUTPUT', @o = @nonEmpty OUTPUT;
    FETCH NEXT FROM cur INTO @tbl;
END
CLOSE cur; DEALLOCATE cur;

IF LEN(@nonEmpty) > 0
BEGIN
    SET @msg = N'ABORT: these YIMS2 target tables already have rows -'
             + STUFF(@nonEmpty, 1, 2, N' ')
             + N'. Clear them first if you really mean to reload.';
    RAISERROR(@msg, 16, 1);
    RETURN;
END

PRINT '=== Seed YIMS2 <- ' + @SourceDb + '  started ' + CONVERT(varchar(30), SYSUTCDATETIME(), 126) + ' UTC ===';

BEGIN TRY
BEGIN TRANSACTION;

/* =================== 2. DISABLE FK CONSTRAINTS ========================== */
SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
                    + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
WHERE t.name IN (SELECT tbl FROM @targets);
EXEC sp_executesql @sql;
PRINT '2. FK constraints on target tables disabled.';

/* =================== 3. COPY DATA DEV -> YIMS2 ========================= */
DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR SELECT ord, tbl FROM @targets ORDER BY ord;
OPEN c2; FETCH NEXT FROM c2 INTO @ord, @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    /* column list = columns present in BOTH databases, excluding computed and
       rowversion/timestamp; identity columns are kept (IDENTITY_INSERT).      */
    SET @cols = NULL;
    SET @sql = N'
        SELECT @cols = STUFF((
            SELECT '', '' + QUOTENAME(dc.name)
            FROM ' + QUOTENAME(@SourceDb) + N'.sys.columns dc
            JOIN ' + QUOTENAME(@SourceDb) + N'.sys.tables  dt ON dt.object_id = dc.object_id
            JOIN ' + QUOTENAME(@SourceDb) + N'.sys.types   dy ON dy.user_type_id = dc.user_type_id
            WHERE dt.name = @t
              AND dc.is_computed = 0
              AND dy.name NOT IN (''timestamp'',''rowversion'')
              AND EXISTS (SELECT 1 FROM sys.columns yc
                          WHERE yc.object_id = OBJECT_ID(''dbo.'' + @t) AND yc.name = dc.name)
            ORDER BY dc.column_id
            FOR XML PATH(''''), TYPE).value(''.'',''nvarchar(max)''), 1, 2, '''');';
    EXEC sp_executesql @sql, N'@t sysname, @cols nvarchar(max) OUTPUT', @t = @tbl, @cols = @cols OUTPUT;

    IF @cols IS NULL OR LEN(@cols) = 0
    BEGIN
        RAISERROR('ABORT: could not resolve a column list for %s.', 16, 1, @tbl);
    END

    SET @hasIdent = CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns
                                      WHERE object_id = OBJECT_ID('dbo.' + @tbl)) THEN 1 ELSE 0 END;

    SET @sql =
        CASE WHEN @hasIdent = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@tbl) + N' ON;' + CHAR(10) ELSE N'' END
      + N'INSERT INTO dbo.' + QUOTENAME(@tbl) + N' (' + @cols + N')' + CHAR(10)
      + N'SELECT ' + @cols + N' FROM ' + QUOTENAME(@SourceDb) + N'.dbo.' + QUOTENAME(@tbl) + N';' + CHAR(10)
      + N'SET @rc = @@ROWCOUNT;' + CHAR(10)
      + CASE WHEN @hasIdent = 1 THEN N'SET IDENTITY_INSERT dbo.' + QUOTENAME(@tbl) + N' OFF;' ELSE N'' END;

    EXEC sp_executesql @sql, N'@rc int OUTPUT', @rc = @rc OUTPUT;
    PRINT '3.' + RIGHT('0' + CAST(@ord AS varchar(2)), 2) + '  ' + @tbl
        + '  ->  ' + CAST(@rc AS varchar(12)) + ' rows';

    FETCH NEXT FROM c2 INTO @ord, @tbl;
END
CLOSE c2; DEALLOCATE c2;

/* =================== 4. RE-ENABLE + RE-CHECK FKs ====================== */
DECLARE @fkFail nvarchar(max) = N'';
DECLARE @fkName sysname, @fkParent sysname, @fkSchema sysname;
DECLARE c3 CURSOR LOCAL FAST_FORWARD FOR
    SELECT fk.name, SCHEMA_NAME(t.schema_id), t.name
    FROM sys.foreign_keys fk
    JOIN sys.tables t ON t.object_id = fk.parent_object_id
    WHERE t.name IN (SELECT tbl FROM @targets);
OPEN c3; FETCH NEXT FROM c3 INTO @fkName, @fkSchema, @fkParent;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @sql = N'ALTER TABLE ' + QUOTENAME(@fkSchema) + N'.' + QUOTENAME(@fkParent)
                 + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@fkName) + N';';
        EXEC sp_executesql @sql;
    END TRY
    BEGIN CATCH
        SET @fkFail = @fkFail + CHAR(10) + '    ' + @fkParent + '.' + @fkName + '  -  ' + ERROR_MESSAGE();
    END CATCH
    FETCH NEXT FROM c3 INTO @fkName, @fkSchema, @fkParent;
END
CLOSE c3; DEALLOCATE c3;

IF LEN(@fkFail) > 0
BEGIN
    SET @msg = N'ABORT: FK re-validation failed (rolling back):' + @fkFail;
    RAISERROR(@msg, 16, 1);
END
PRINT '4. All FK constraints on target tables re-enabled and validated.';

/* =================== 5. RESEED IDENTITIES ============================= */
DECLARE c4 CURSOR LOCAL FAST_FORWARD FOR
    SELECT tbl FROM @targets t
    WHERE EXISTS (SELECT 1 FROM sys.identity_columns ic WHERE ic.object_id = OBJECT_ID('dbo.' + t.tbl));
OPEN c4; FETCH NEXT FROM c4 INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    DBCC CHECKIDENT (@tbl, RESEED) WITH NO_INFOMSGS;
    FETCH NEXT FROM c4 INTO @tbl;
END
CLOSE c4; DEALLOCATE c4;
PRINT '5. Identity counters reseeded.';

COMMIT TRANSACTION;
PRINT '=== COMMITTED ===';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT '=== ROLLED BACK - nothing changed ===';
    THROW;
END CATCH

/* =================== VERIFICATION (post-commit) ====================== */
DECLARE @verify TABLE (TableName sysname, DevRows int, Yims2Rows int);
DECLARE c5 CURSOR LOCAL FAST_FORWARD FOR SELECT tbl FROM @targets ORDER BY ord;
OPEN c5; FETCH NEXT FROM c5 INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @d = (SELECT COUNT(*) FROM ' + QUOTENAME(@SourceDb) + N'.dbo.' + QUOTENAME(@tbl) + N'),
                        @y = (SELECT COUNT(*) FROM dbo.' + QUOTENAME(@tbl) + N');';
    DECLARE @d int, @y int;
    EXEC sp_executesql @sql, N'@d int OUTPUT, @y int OUTPUT', @d = @d OUTPUT, @y = @y OUTPUT;
    INSERT INTO @verify VALUES (@tbl, @d, @y);
    FETCH NEXT FROM c5 INTO @tbl;
END
CLOSE c5; DEALLOCATE c5;

SELECT TableName,
       DevRows,
       Yims2Rows,
       CASE WHEN DevRows = Yims2Rows THEN 'OK' ELSE '*** MISMATCH ***' END AS Result
FROM @verify
ORDER BY CASE WHEN DevRows = Yims2Rows THEN 1 ELSE 0 END, TableName;

PRINT '=== Seed YIMS2 finished ' + CONVERT(varchar(30), SYSUTCDATETIME(), 126) + ' UTC ===';
