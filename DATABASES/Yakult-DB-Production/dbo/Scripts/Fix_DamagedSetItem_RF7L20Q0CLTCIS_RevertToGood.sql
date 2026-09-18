-- Reverts the ConditionID of the item with Serial 'RF7L20Q0CLTCIS'
-- (Samsung EF-DX620UBEGWW AB S10 FE+ Book Cover Keyboard Slim) from 'Damaged' back
-- to 'Good' -- it was set to Damaged by mistake via the Repair Portal.
-- This does NOT touch dbo.Active or dbo.ArchiveStatus -- see the separate
-- Diagnostic_DamagedSetItem_RF7L20Q0CLTCIS.sql results for whether those need fixing too.

DECLARE @Serial NVARCHAR(100) = 'RF7L20Q0CLTCIS';
DECLARE @GoodConditionId INT = (SELECT TOP (1) ConditionID FROM dbo.Condition WHERE ConditionName = 'Good');

UPDATE dbo.Item
SET ConditionID = @GoodConditionId
WHERE SerialNumber = @Serial;

-- Verify
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
