-- Renames the "Toner Cartridge" ItemCategory to "Toner" and updates every existing
-- dbo.Item row that already carries that literal text, so the master category name
-- and the denormalized Item.Category text stay in sync (Item.Category is a text copy
-- taken at creation time, not a live lookup — renaming only the ItemCategory row would
-- leave existing items permanently mislabeled otherwise).
--
-- Confirmed before writing this: no stored procedure/view/function in this database
-- references the literal string 'Toner Cartridge', and no ItemCategory row is already
-- named exactly 'Toner' (so this can't collide with an existing category).
--
-- Brings this environment's naming in line with PROD, where the equivalent
-- ItemCategory row is already named "Toner".
--
-- Idempotent: every step is guarded, safe to re-run.

DECLARE @CategoryId INT;

SELECT @CategoryId = CategoryId
FROM dbo.ItemCategory
WHERE Name = 'Toner Cartridge';

IF @CategoryId IS NULL
BEGIN
    PRINT 'No ItemCategory named ''Toner Cartridge'' found — nothing to rename. Skipping.';
END
ELSE
BEGIN
    -- Step 1: rename the category itself
    UPDATE dbo.ItemCategory
    SET Name = 'Toner'
    WHERE CategoryId = @CategoryId
      AND Name = 'Toner Cartridge';

    PRINT 'Renamed dbo.ItemCategory.Name from ''Toner Cartridge'' to ''Toner'' (CategoryId = '
        + CAST(@CategoryId AS VARCHAR(10)) + ').';

    -- Step 2: bring existing Item rows' denormalized Category text in line
    UPDATE dbo.Item
    SET Category = 'Toner'
    WHERE CategoryId = @CategoryId
      AND Category = 'Toner Cartridge';

    PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' dbo.Item row(s) from Category = ''Toner Cartridge'' to ''Toner''.';
END
GO

-- Verification: should show 0 remaining rows anywhere still using the old text.
SELECT
    (SELECT COUNT(*) FROM dbo.ItemCategory WHERE Name = 'Toner Cartridge') AS RemainingCategoryRows,
    (SELECT COUNT(*) FROM dbo.Item WHERE Category = 'Toner Cartridge')     AS RemainingItemRows;
GO
