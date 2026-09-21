-- Migration: Add DistributorId to dbo.Request
-- Used by the Batch Add Requests dept-level flow (Distributor picker beside
-- Company/Department/Branch) and Set creation propagation
-- (Request.DistributorId -> Set.DistributorId).
-- Idempotent: safe to re-run. TRY/CATCH swallows "already exists" races
-- when the script is executed in two windows at once (error 2714/1913).

BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.[Request]') AND name = 'DistributorId'
    )
    BEGIN
        ALTER TABLE dbo.[Request] ADD DistributorId INT NULL;
    END
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2714, 1750) THROW;
END CATCH

-- Only link the FK when the principal table exists (older DBs may not have
-- dbo.Distributor deployed yet; the column stays nullable and unused there).
BEGIN TRY
    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Request_Distributor'
    )
    AND OBJECT_ID('dbo.[Distributor]', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.[Request]
            ADD CONSTRAINT FK_Request_Distributor FOREIGN KEY (DistributorId)
            REFERENCES dbo.[Distributor] (DistributorId);
    END
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2714, 1750) THROW;
END CATCH

BEGIN TRY
    IF COL_LENGTH('dbo.[Request]', 'DistributorId') IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Request_DistributorId'
          AND object_id = OBJECT_ID('dbo.[Request]')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Request_DistributorId]
            ON [dbo].[Request] ([DistributorId] ASC);
    END
END TRY
BEGIN CATCH
    -- 1913 = index/stats name already exists (double execution race). Benign.
    IF ERROR_NUMBER() <> 1913 THROW;
END CATCH
