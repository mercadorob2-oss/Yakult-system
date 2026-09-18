-- Backfill_Request_WorkflowType.sql
--
-- Populates dbo.Request.WorkflowType for existing rows. Run AFTER Migration_Request_AddWorkflowType.sql.
--
-- Rule:
--   - Requests with a matching dbo.CartridgeRequestModel row -> 'CartridgeManagement'
--     (pure-cartridge submissions; this is the only reliable historical signal for them,
--      since the WorkflowType column didn't exist yet when they were created).
--   - Remaining requests whose resolved Item.Category is Ink / Printhead / Toner Cartridge
--     -> 'RequestSetManagement'.
--   - Everything else (Cable, Projector, Furniture, and any other unrelated category) is left
--     NULL — those requests don't participate in the Cartridge Management / Request & Set
--     Management split at all, matching how new requests of those categories will continue to
--     have WorkflowType = NULL going forward (they're created through the general Add Request
--     flow, not through RequesterPortalService, which is the only code path that sets this
--     column at submission time).
--
-- Only touches rows where WorkflowType IS NULL, so this is safe to re-run.

UPDATE r
SET r.WorkflowType = 'CartridgeManagement'
FROM dbo.Request r
WHERE r.WorkflowType IS NULL
  AND EXISTS (
      SELECT 1 FROM dbo.CartridgeRequestModel crm WHERE crm.ReqId = r.ReqId
  );
GO

UPDATE r
SET r.WorkflowType = 'RequestSetManagement'
FROM dbo.Request r
INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
WHERE r.WorkflowType IS NULL
  AND i.Category IN ('Ink', 'Printhead', 'Toner Cartridge');
GO
