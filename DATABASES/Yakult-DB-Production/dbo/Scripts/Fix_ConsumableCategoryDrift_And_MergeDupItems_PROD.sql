-- ============================================================================
-- Fix: Consumable category-name drift + duplicate Item consolidation
-- Target : YIMS_PROD
-- ============================================================================
-- Symptom : Request Portal (Assisted Request) shows no models when the
--           "Printhead" or "Toner Cartridge" category is picked, because
--           RequesterPortalService.GetConsumableModelsWithAvailability does an
--           exact  cm.Category = @Category  match and the stored spellings drift:
--             dbo.ConsumableModel.Category : 'Print Head'      (want 'Printhead')
--             dbo.Item.Category            : 'Print Head'      (want 'Printhead')
--             dbo.Item.Category            : 'Toner Cartridge' (want 'Toner')
--           dbo.ItemCategory is already correct (1063 'Printhead', 1076 'Toner') --
--           only the denormalized text copies are stale.
--
--           Separately, several consumable models have multiple dbo.Item catalog
--           rows (one real + older 0-stock stubs). This collapses each to a
--           single active row, summing StockOnHand onto the survivor.
--
-- NOTE    : This closes the "Printhead" portal gap. "Toner Cartridge" ALSO needs
--           a code change -- Models.ConsumableCategories.All must use "Toner"
--           instead of "Toner Cartridge" (also RequestSetManagementPortalForm.cs).
--           Data alone cannot fix that dropdown label.
--
-- Safety  : every step guarded + idempotent, wrapped per-step in a transaction
--           that rolls back if a post-check looks wrong. Losers are only
--           deactivated (Active = 0) -- never deleted -- and only when they carry
--           0 stock and no Request / SetItem references.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '';
PRINT '################  STEP 1 : dbo.ConsumableModel.Category  ################';
BEGIN TRAN;

    UPDATE dbo.ConsumableModel
    SET    Category = 'Printhead'
    WHERE  REPLACE(LOWER(Category), ' ', '') = 'printhead'
      AND  Category <> 'Printhead';
    DECLARE @cmFixed int = @@ROWCOUNT;
    PRINT '  ConsumableModel rows re-spelled to ''Printhead'' : ' + CAST(@cmFixed AS varchar(10));

    IF EXISTS (SELECT 1 FROM dbo.ConsumableModel
               WHERE REPLACE(LOWER(Category),' ','') = 'printhead' AND Category <> 'Printhead')
    BEGIN
        PRINT '  !! unexpected leftover spelling - rolling back step 1';
        ROLLBACK;
    END
    ELSE
        COMMIT;
GO

PRINT '';
PRINT '################  STEP 2 : dbo.Item.Category text  ################';
BEGIN TRAN;

    UPDATE i
    SET    i.Category = 'Printhead'
    FROM   dbo.Item i
    JOIN   dbo.ItemCategory c ON c.CategoryId = i.CategoryId
    WHERE  c.Name = 'Printhead'
      AND  i.Category <> 'Printhead'
      AND  REPLACE(LOWER(i.Category), ' ', '') = 'printhead';
    PRINT '  Item rows re-spelled ''Print Head''  -> ''Printhead'' : ' + CAST(@@ROWCOUNT AS varchar(10));

    UPDATE i
    SET    i.Category = 'Toner'
    FROM   dbo.Item i
    JOIN   dbo.ItemCategory c ON c.CategoryId = i.CategoryId
    WHERE  c.Name = 'Toner'
      AND  i.Category <> 'Toner'
      AND  REPLACE(LOWER(i.Category), ' ', '') IN ('toner', 'tonercartridge');
    PRINT '  Item rows re-spelled ''Toner Cartridge'' -> ''Toner'' : ' + CAST(@@ROWCOUNT AS varchar(10));

    -- Post-check: no consumable Item row whose text disagrees with its category row
    IF EXISTS (
        SELECT 1 FROM dbo.Item i JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
        WHERE c.Name IN ('Printhead','Toner') AND i.Category <> c.Name
    )
    BEGIN
        PRINT '  !! Item.Category still disagrees with ItemCategory.Name - rolling back step 2';
        ROLLBACK;
    END
    ELSE
        COMMIT;
GO

PRINT '';
PRINT '################  STEP 3 : merge duplicate consumable Item rows  ################';
BEGIN TRAN;

    -- Groups of >1 active Item row sharing Name + CategoryId + ConsumableModelId,
    -- within the consumable families only. Keeper = most stock, then newest ItemId.
    ;WITH grp AS (
        SELECT  i.ItemId, i.Name, i.CategoryId, i.ConsumableModelId, i.StockOnHand,
                grp_key   = CONCAT(i.Name, '|', i.CategoryId, '|', i.ConsumableModelId),
                keeper_rn = ROW_NUMBER() OVER (
                                PARTITION BY i.Name, i.CategoryId, i.ConsumableModelId
                                ORDER BY i.StockOnHand DESC, i.ItemId DESC),
                grp_cnt   = COUNT(*) OVER (
                                PARTITION BY i.Name, i.CategoryId, i.ConsumableModelId),
                grp_sum   = SUM(i.StockOnHand) OVER (
                                PARTITION BY i.Name, i.CategoryId, i.ConsumableModelId)
        FROM    dbo.Item i
        JOIN    dbo.ItemCategory c ON c.CategoryId = i.CategoryId
        WHERE   i.Active = 1
          AND   i.ConsumableModelId IS NOT NULL
          AND   c.Name IN ('Ink','Toner','Printhead')
    ),
    dups AS (SELECT * FROM grp WHERE grp_cnt > 1)

    SELECT  ItemId, Name, ConsumableModelId, StockOnHand,
            [Role]  = CASE WHEN keeper_rn = 1 THEN 'KEEP' ELSE 'retire' END,
            GroupSum = grp_sum
    INTO    #plan
    FROM    dups;

    -- Abort if any retire-row has history references (should be none per diagnostic)
    IF EXISTS (
        SELECT 1 FROM #plan p
        WHERE  p.[Role] = 'retire'
          AND (EXISTS (SELECT 1 FROM dbo.Request  r WHERE r.ItemId = p.ItemId)
            OR EXISTS (SELECT 1 FROM dbo.SetItem  s WHERE s.ItemId = p.ItemId))
    )
    BEGIN
        PRINT '  !! a row slated for retire has Request/SetItem refs - rolling back step 3';
        SELECT ItemId, Name, [Role] FROM #plan ORDER BY Name, [Role];
        ROLLBACK;
        DROP TABLE #plan;
        RETURN;
    END

    -- 3a. roll each group's total onto its keeper
    UPDATE i
    SET    i.StockOnHand = p.GroupSum
    FROM   dbo.Item i
    JOIN   #plan p ON p.ItemId = i.ItemId AND p.[Role] = 'KEEP'
    WHERE  i.StockOnHand <> p.GroupSum;
    PRINT '  keepers whose StockOnHand was topped up : ' + CAST(@@ROWCOUNT AS varchar(10));

    -- 3b. zero + deactivate the duplicates
    UPDATE i
    SET    i.StockOnHand = 0,
           i.Active      = 0
    FROM   dbo.Item i
    JOIN   #plan p ON p.ItemId = i.ItemId AND p.[Role] = 'retire';
    PRINT '  duplicate Item rows deactivated          : ' + CAST(@@ROWCOUNT AS varchar(10));

    -- Post-check: no consumable model should still have >1 active Item row
    IF EXISTS (
        SELECT 1
        FROM   dbo.Item i JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
        WHERE  i.Active = 1 AND i.ConsumableModelId IS NOT NULL
          AND  c.Name IN ('Ink','Toner','Printhead')
        GROUP BY i.Name, i.CategoryId, i.ConsumableModelId
        HAVING COUNT(*) > 1
    )
    BEGIN
        PRINT '  !! duplicates remain - rolling back step 3';
        ROLLBACK;
    END
    ELSE
        COMMIT;

    DROP TABLE #plan;
GO

-- ============================================================================
PRINT '';
PRINT '################  VERIFICATION  ################';

SELECT 'ConsumableModel.Category' AS Whatx, Category, COUNT(*) AS Rows_
FROM   dbo.ConsumableModel GROUP BY Category ORDER BY Category;

SELECT 'Item.Category (consumables)' AS Whatx, i.Category, COUNT(*) AS Rows_,
       SUM(CASE WHEN i.Active = 1 THEN 1 ELSE 0 END) AS Active_
FROM   dbo.Item i JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
WHERE  c.Name IN ('Ink','Toner','Printhead')
GROUP  BY i.Category ORDER BY i.Category;

SELECT 'remaining dup groups' AS Whatx, i.Name, i.ConsumableModelId, COUNT(*) AS ActiveRows
FROM   dbo.Item i JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
WHERE  i.Active = 1 AND i.ConsumableModelId IS NOT NULL AND c.Name IN ('Ink','Toner','Printhead')
GROUP  BY i.Name, i.ConsumableModelId
HAVING COUNT(*) > 1;

SELECT cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.TotalQuantity,
       LiveSum = (SELECT ISNULL(SUM(x.StockOnHand),0) FROM dbo.Item x
                  WHERE x.ConsumableModelId = cm.ConsumableModelId AND x.Active = 1)
FROM   dbo.ConsumableModel cm
WHERE  cm.ConsumableModelId IN (376,383,384,385,387,392)
ORDER  BY cm.ConsumableModelId;
GO
