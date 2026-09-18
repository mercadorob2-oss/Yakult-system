-- Verifies dbo.AuditTrail exists before deploying the Set/Invoice ownership-transfer
-- audit logging feature. AuditTrail already ships with OldValues/NewValues columns,
-- so no ALTER is needed here, only a safety check.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditTrail' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    RAISERROR('dbo.AuditTrail is missing, run the base AuditTrail.sql table definition first.', 16, 1);
END
ELSE
BEGIN
    PRINT 'dbo.AuditTrail exists, no action needed.';
END
