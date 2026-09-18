-- ============================================
-- Migration: Create CartridgeRequestModel table
-- Purpose: Support multiple cartridge models per request
-- ============================================

-- Create CartridgeRequestModel table for multi-model requests
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CartridgeRequestModel')
BEGIN
    CREATE TABLE dbo.CartridgeRequestModel (
        RequestModelId      INT IDENTITY(1,1) PRIMARY KEY,
        ReqId               INT NOT NULL,                    -- Parent request ID
        CartridgeModel      NVARCHAR(100) NOT NULL,          -- Cartridge model number
        RequestedQty        INT NOT NULL,                    -- Quantity requested for this model
        ReturnedEmptyQty    INT NULL,                        -- Returned empty (= RequestedQty when processed)
        IssuedFullQty       INT NULL,                        -- Full cartridges issued
        UnfulfilledQty      INT NULL,                        -- Pending: ReturnedEmptyQty - IssuedFullQty
        Status              VARCHAR(20) NULL,                -- Pending, Fulfilled, Partially Fulfilled, Unfulfilled
        Remarks             NVARCHAR(500) NULL,              -- Auto-generated remarks
        ProcessedDate       DATETIME NULL,                   -- When this model was processed
        ProcessedBy         INT NULL,                        -- Who processed it
        CreatedDate         DATETIME NOT NULL DEFAULT GETDATE(),

        -- Constraints
        CONSTRAINT FK_CartridgeRequestModel_Request
            FOREIGN KEY (ReqId) REFERENCES dbo.Request(ReqId)
    );

    PRINT 'Created CartridgeRequestModel table';

    -- Create indexes
    CREATE NONCLUSTERED INDEX IX_CartridgeRequestModel_ReqId
        ON dbo.CartridgeRequestModel (ReqId);

    CREATE NONCLUSTERED INDEX IX_CartridgeRequestModel_Model_Status
        ON dbo.CartridgeRequestModel (CartridgeModel, Status);

    PRINT 'Created indexes on CartridgeRequestModel';
END
ELSE
    PRINT 'CartridgeRequestModel table already exists';

PRINT '';
PRINT '============================================';
PRINT 'Migration complete!';
PRINT 'CartridgeRequestModel table supports multi-model requests:';
PRINT '  - RequestModelId (INT, PK, IDENTITY)';
PRINT '  - ReqId (INT, NOT NULL, FK)';
PRINT '  - CartridgeModel (NVARCHAR(100), NOT NULL)';
PRINT '  - RequestedQty (INT, NOT NULL)';
PRINT '  - ReturnedEmptyQty (INT, nullable)';
PRINT '  - IssuedFullQty (INT, nullable)';
PRINT '  - UnfulfilledQty (INT, nullable)';
PRINT '  - Status (VARCHAR(20), nullable)';
PRINT '  - Remarks (NVARCHAR(500), nullable)';
PRINT '  - ProcessedDate (DATETIME, nullable)';
PRINT '  - ProcessedBy (INT, nullable)';
PRINT '  - CreatedDate (DATETIME, NOT NULL)';
PRINT '============================================';
