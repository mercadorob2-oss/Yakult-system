-- Check for triggers on the Item table

SELECT
    t.name AS TriggerName,
    OBJECT_NAME(t.parent_id) AS TableName,
    te.type_desc AS TriggerType,
    m.definition AS TriggerDefinition
FROM sys.triggers t
INNER JOIN sys.trigger_events te ON t.object_id = te.object_id
INNER JOIN sys.sql_modules m ON t.object_id = m.object_id
WHERE t.parent_id = OBJECT_ID('dbo.Item')
ORDER BY t.name
