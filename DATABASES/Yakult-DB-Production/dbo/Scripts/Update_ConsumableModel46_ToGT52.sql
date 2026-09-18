-- Update: rename ConsumableModelId 46 from "HP GT52/53 (BLACK)" to "HP GT52 (BLACK)"

SELECT ConsumableModelId, ModelNumber, Category FROM dbo.ConsumableModel WHERE ConsumableModelId = 46;

UPDATE dbo.ConsumableModel
SET ModelNumber = 'HP GT52 (BLACK)'
WHERE ConsumableModelId = 46;

SELECT ConsumableModelId, ModelNumber, Category FROM dbo.ConsumableModel WHERE ConsumableModelId = 46;
GO
