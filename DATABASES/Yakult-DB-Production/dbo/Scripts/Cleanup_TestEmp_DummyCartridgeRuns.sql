/*
    Cleanup: delete dummy cartridge exchange runs created by the test requester "TestEmp".

    Scope: every Set (and its associated Request) whose requesting Employee.Name = 'TestEmp'.
    This covers the Fulfilled Cartridges rows shown in the portal (SET-0189..SET-0198, etc.)
    plus any other Sets/Requests tied to the same dummy employee.

    Run the SELECT preview block first and confirm the row counts / SetCodes look right
    before uncommenting the DELETE transaction.
*/

SET NOCOUNT ON;

DECLARE @EmpName NVARCHAR(150) = N'TestEmp';

-- ============================================================
-- PREVIEW — run this first and eyeball the results
-- ============================================================
DECLARE @Sets TABLE (SetId INT PRIMARY KEY);
DECLARE @Reqs TABLE (ReqId INT PRIMARY KEY);

INSERT INTO @Reqs (ReqId)
SELECT DISTINCT r.ReqId
FROM dbo.Request r
INNER JOIN dbo.Employee e ON e.EmpId = r.EmpId
WHERE e.Name = @EmpName;

INSERT INTO @Sets (SetId)
SELECT DISTINCT s.SetId
FROM dbo.[Set] s
WHERE s.ReqId IN (SELECT ReqId FROM @Reqs)
   OR EXISTS (SELECT 1 FROM dbo.Request r WHERE r.SetId = s.SetId AND r.ReqId IN (SELECT ReqId FROM @Reqs));

SELECT s.SetId, s.SetCode, s.SetType, s.Status, s.DispatchDate
FROM dbo.[Set] s
INNER JOIN @Sets t ON t.SetId = s.SetId
ORDER BY s.SetId;

SELECT r.ReqId, r.Description, r.Status, r.SetId, e.Name AS Requester
FROM dbo.Request r
INNER JOIN dbo.Employee e ON e.EmpId = r.EmpId
INNER JOIN @Reqs t ON t.ReqId = r.ReqId
ORDER BY r.ReqId;

/*
-- ============================================================
-- DELETE — uncomment and run once the preview above looks correct
-- ============================================================
BEGIN TRAN;

BEGIN TRY

    -- Break the self-referencing FK (Set.RenewalOfSetId -> Set.SetId) for any dummy
    -- set that is a renewal target/source of another dummy set.
    UPDATE s
    SET s.RenewalOfSetId = NULL
    FROM dbo.[Set] s
    INNER JOIN @Sets t ON t.SetId = s.SetId;

    -- Child rows with a plain (non-cascading) FK to Set — must delete before the Set row.
    -- Guarded with OBJECT_ID + dynamic SQL because some of these tables may not exist
    -- yet on every database (they come from newer migrations not deployed everywhere).
    -- Static DELETEs referencing a missing table fail at parse/bind time even inside an
    -- IF, so each one is deferred to runtime via EXEC — the #TargetSetIds temp table
    -- carries the target SetIds across into the dynamic batch.
    SELECT SetId INTO #TargetSetIds FROM @Sets;
    SELECT ReqId INTO #TargetReqIds FROM @Reqs;

    IF OBJECT_ID('dbo.SetDeploymentConfirmation') IS NOT NULL
        EXEC(N'DELETE sdc FROM dbo.SetDeploymentConfirmation sdc INNER JOIN #TargetSetIds t ON t.SetId = sdc.SetId;');

    IF OBJECT_ID('dbo.SetImages') IS NOT NULL
        EXEC(N'DELETE si FROM dbo.SetImages si INNER JOIN #TargetSetIds t ON t.SetId = si.SetId;');

    IF OBJECT_ID('dbo.SetItem') IS NOT NULL
        EXEC(N'DELETE sti FROM dbo.SetItem sti INNER JOIN #TargetSetIds t ON t.SetId = sti.SetId;');

    IF OBJECT_ID('dbo.SetTransfer') IS NOT NULL
        EXEC(N'DELETE str FROM dbo.SetTransfer str INNER JOIN #TargetSetIds t ON t.SetId = str.SetId;');
    -- ReceiptSetLink has ON DELETE CASCADE from Set, no explicit delete needed.

    -- Other tables with a NOT NULL FK straight to Request — must delete before the Request row.
    IF OBJECT_ID('dbo.CartridgeRequestModel') IS NOT NULL
        EXEC(N'DELETE crm FROM dbo.CartridgeRequestModel crm INNER JOIN #TargetReqIds t ON t.ReqId = crm.ReqId;');

    IF OBJECT_ID('dbo.UnfulfilledCartridgeExchange') IS NOT NULL
        EXEC(N'DELETE uce FROM dbo.UnfulfilledCartridgeExchange uce INNER JOIN #TargetReqIds t ON t.ReqId = uce.ReqId;');

    -- EmptyCartridge.ReqId is a nullable FK — null it out rather than delete the row,
    -- since it represents a physical cartridge that may still be valid inventory.
    IF OBJECT_ID('dbo.EmptyCartridge') IS NOT NULL
        EXEC(N'UPDATE ec SET ec.ReqId = NULL FROM dbo.EmptyCartridge ec INNER JOIN #TargetReqIds t ON t.ReqId = ec.ReqId;');

    DROP TABLE #TargetSetIds;
    DROP TABLE #TargetReqIds;

    -- Inventory rows tied either to the Set or to one of its Requests.
    DELETE inv
    FROM dbo.Inventory inv
    WHERE inv.SetId IN (SELECT SetId FROM @Sets)
       OR inv.ReqId IN (SELECT ReqId FROM @Reqs);

    -- Break Set <-> Request cross-references before deleting either side.
    UPDATE s SET s.ReqId = NULL FROM dbo.[Set] s INNER JOIN @Sets t ON t.SetId = s.SetId;
    UPDATE r SET r.SetId = NULL FROM dbo.Request r INNER JOIN @Reqs t ON t.ReqId = r.ReqId;

    -- Now safe to delete the Sets and Requests themselves.
    DELETE s FROM dbo.[Set] s INNER JOIN @Sets t ON t.SetId = s.SetId;
    DELETE r FROM dbo.Request r INNER JOIN @Reqs t ON t.ReqId = r.ReqId;

    COMMIT TRAN;
    PRINT 'Dummy TestEmp cartridge runs deleted successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH
*/
