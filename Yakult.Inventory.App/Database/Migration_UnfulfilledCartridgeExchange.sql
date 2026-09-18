-- ============================================
-- Migration: Create UnfulfilledCartridgeExchange table
-- Purpose: Track returned but unfulfilled cartridge exchanges
--          for audit trail and future FIFO fulfillment
-- ============================================

-- Create UnfulfilledCartridgeExchange table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UnfulfilledCartridgeExchange')
BEGIN
    CREATE TABLE dbo.UnfulfilledCartridgeExchange (
        UnfulfilledId       INT IDENTITY(1,1) PRIMARY KEY,
        ReqId               INT NOT NULL,                    -- Original request ID
        EmpId               INT NOT NULL,                    -- Requester employee ID
        BranchId            INT NULL,                        -- Branch
        DeptId              INT NULL,                        -- Department
        CartridgeModel      NVARCHAR(100) NOT NULL,          -- Cartridge model number
        RequestedQty        INT NOT NULL,                    -- Originally requested quantity
        ReturnedEmptyQty    INT NOT NULL,                    -- Returned empty cartridges (= RequestedQty)
        IssuedFullQty       INT NOT NULL,                    -- Full cartridges issued
        UnfulfilledQty      INT NOT NULL,                    -- Pending: ReturnedEmptyQty - IssuedFullQty
        Remarks             NVARCHAR(1000) NULL,             -- Auto-generated remarks
        Status              VARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending, Fulfilled
        CreatedDate         DATETIME NOT NULL DEFAULT GETDATE(),
        CreatedBy           INT NOT NULL,
        FulfilledDate       DATETIME NULL,                   -- When fully fulfilled
        FulfilledBy         INT NULL,                        -- Who fulfilled it
        FulfilledRemarks    NVARCHAR(500) NULL,              -- Fulfillment notes

        -- Constraints
        CONSTRAINT CK_UnfulfilledCartridgeExchange_Status 
            CHECK (Status IN ('Pending', 'Fulfilled')),
        CONSTRAINT CK_UnfulfilledCartridgeExchange_UnfulfilledQty 
            CHECK (UnfulfilledQty >= 0),
        CONSTRAINT CK_UnfulfilledCartridgeExchange_Quantities 
            CHECK (UnfulfilledQty = ReturnedEmptyQty - IssuedFullQty)
    );

    PRINT 'Created UnfulfilledCartridgeExchange table';

    -- Create indexes for FIFO ordering and common queries
    CREATE NONCLUSTERED INDEX IX_UnfulfilledCartridgeExchange_Status_CreatedDate
        ON dbo.UnfulfilledCartridgeExchange (Status, CreatedDate ASC);

    CREATE NONCLUSTERED INDEX IX_UnfulfilledCartridgeExchange_CartridgeModel
        ON dbo.UnfulfilledCartridgeExchange (CartridgeModel, Status);

    CREATE NONCLUSTERED INDEX IX_UnfulfilledCartridgeExchange_ReqId
        ON dbo.UnfulfilledCartridgeExchange (ReqId);

    PRINT 'Created indexes on UnfulfilledCartridgeExchange';
END
ELSE
    PRINT 'UnfulfilledCartridgeExchange table already exists';

-- Add foreign key constraints (optional)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UnfulfilledCartridgeExchange_Request')
BEGIN
    ALTER TABLE dbo.UnfulfilledCartridgeExchange
    ADD CONSTRAINT FK_UnfulfilledCartridgeExchange_Request
    FOREIGN KEY (ReqId) REFERENCES dbo.Request(ReqId);

    PRINT 'Added FK_UnfulfilledCartridgeExchange_Request';
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UnfulfilledCartridgeExchange_Employee')
BEGIN
    ALTER TABLE dbo.UnfulfilledCartridgeExchange
    ADD CONSTRAINT FK_UnfulfilledCartridgeExchange_Employee
    FOREIGN KEY (EmpId) REFERENCES dbo.Employee(EmpId);

    PRINT 'Added FK_UnfulfilledCartridgeExchange_Employee';
END

PRINT '';
PRINT '============================================';
PRINT 'Migration complete!';
PRINT 'UnfulfilledCartridgeExchange table created with columns:';
PRINT '  - UnfulfilledId (INT, PK, IDENTITY)';
PRINT '  - ReqId (INT, NOT NULL, FK)';
PRINT '  - EmpId (INT, NOT NULL, FK)';
PRINT '  - BranchId (INT, nullable)';
PRINT '  - DeptId (INT, nullable)';
PRINT '  - CartridgeModel (NVARCHAR(100), NOT NULL)';
PRINT '  - RequestedQty (INT, NOT NULL)';
PRINT '  - ReturnedEmptyQty (INT, NOT NULL)';
PRINT '  - IssuedFullQty (INT, NOT NULL)';
PRINT '  - UnfulfilledQty (INT, NOT NULL)';
PRINT '  - Remarks (NVARCHAR(1000), nullable)';
PRINT '  - Status (VARCHAR(20), Pending/Fulfilled)';
PRINT '  - CreatedDate (DATETIME, NOT NULL)';
PRINT '  - CreatedBy (INT, NOT NULL)';
PRINT '  - FulfilledDate (DATETIME, nullable)';
PRINT '  - FulfilledBy (INT, nullable)';
PRINT '  - FulfilledRemarks (NVARCHAR(500), nullable)';
PRINT '============================================';
