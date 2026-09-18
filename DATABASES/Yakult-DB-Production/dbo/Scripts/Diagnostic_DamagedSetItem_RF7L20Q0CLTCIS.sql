-- Diagnostic script: check whether the "Samsung EF-DX620UBEGWW AB S10 FE+ Book Cover Keyboard Slim"
-- item (Serial: RF7L20Q0CLTCIS) exists in dbo.Item, whether it is archived, and how it's
-- referenced by dbo.RepairTicket / dbo.Request. Read-only — no data is modified.

DECLARE @Serial NVARCHAR(100) = 'RF7L20Q0CLTCIS';

-- 1. Does the item exist in dbo.Item at all, and what's its current state?
SELECT
    i.ItemId,
    i.Name,
    i.SerialNumber,
    i.Active,
    i.ConditionID,
    c.ConditionName
FROM dbo.Item i
LEFT JOIN dbo.Condition c ON c.ConditionID = i.ConditionID
WHERE i.SerialNumber = @Serial;

-- 2. Is it archived? (This is the filter that hides it from the Items Page unconditionally)
SELECT
    arch.EntityType,
    arch.EntityId,
    arch.IsArchived,
    arch.ArchivedAt,
    arch.ArchivedBy,
    arch.ArchiveReason,
    arch.RestoredAt,
    arch.RestoredBy
FROM dbo.ArchiveStatus arch
INNER JOIN dbo.Item i ON i.ItemId = arch.EntityId AND arch.EntityType = 'Item'
WHERE i.SerialNumber = @Serial;

-- 3. Is/was it tied to a Set via dbo.Request (active or not)?
SELECT
    r.ReqId,
    r.SetId,
    r.ItemId,
    r.Active AS RequestActive,
    r.Status,
    r.DateRequested
FROM dbo.Request r
INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
WHERE i.SerialNumber = @Serial;

-- 4. Does a Repair Ticket reference it (by live ItemId join, and by snapshot)?
SELECT
    t.RepairTicketId,
    t.TicketCode,
    t.ItemId,
    t.SetId,
    t.ItemNameSnapshot,
    t.ItemSerialSnapshot,
    t.Status,
    t.CreatedAt
FROM dbo.RepairTicket t
WHERE t.ItemSerialSnapshot = @Serial
   OR t.ItemId IN (SELECT ItemId FROM dbo.Item WHERE SerialNumber = @Serial);
