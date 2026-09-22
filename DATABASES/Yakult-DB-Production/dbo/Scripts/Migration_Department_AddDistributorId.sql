-- Migration: Add DistributorId to dbo.Department
-- Records the live change (partner-applied) for fidelity. No UI reads or
-- writes this column yet: departments are internal org units and no
-- department-to-distributor mapping exists in the data. Stays NULL until a
-- real use case surfaces. Do not surface in pickers without one.
-- Idempotent: safe to re-run. TRY/CATCH swallows "already exists" races
-- when the script is executed in two windows at once (error 2714/1913).

BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.[Department]') AND name = 'DistributorId'
    )
    BEGIN
        ALTER TABLE dbo.[Department] ADD DistributorId INT NULL;
    END
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2714, 1750) THROW;
END CATCH

-- Only link the FK when the principal table exists (older DBs may not have
-- dbo.Distributor deployed yet; the column stays nullable and unused there).
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Department_Distributor'
    )
    AND OBJECT_ID('dbo.[Distributor]', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.[Department]
            ADD CONSTRAINT FK_Department_Distributor FOREIGN KEY (DistributorId)
            REFERENCES dbo.[Distributor] (DistributorId);
    END
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2714, 1750) THROW;
END CATCH
