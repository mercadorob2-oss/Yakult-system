/* =====================================================================================
   Borrow Items - PROD Install Script

   Purpose:
   - Adds a lightweight logbook for borrowing serialized hardware (scan/type SerialNumber)
   - Does NOT change Inventory/Request/Set flows; this is a standalone ledger.

   Safe to run multiple times:
   - Creates missing tables
   - Creates missing indexes
   ===================================================================================== */

USE [YIMS_PROD];
GO

/* ---- Preflight: referenced core tables must already exist ---- */
IF OBJECT_ID('dbo.Item', 'U') IS NULL THROW 53001, 'Missing dbo.Item.', 1;
IF OBJECT_ID('dbo.Employee', 'U') IS NULL THROW 53002, 'Missing dbo.Employee.', 1;
IF OBJECT_ID('dbo.Department', 'U') IS NULL THROW 53003, 'Missing dbo.Department.', 1;
IF OBJECT_ID('dbo.[User]', 'U') IS NULL THROW 53004, 'Missing dbo.[User].', 1;
GO

/* =====================================================================================
   1) Base table
   ===================================================================================== */

IF OBJECT_ID('dbo.BorrowLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BorrowLog
    (
        BorrowId INT IDENTITY(1,1) NOT NULL,

        ItemId INT NOT NULL,
        SerialNumber NVARCHAR(255) NOT NULL,
        ItemName NVARCHAR(200) NOT NULL,
        ItemDescription NVARCHAR(400) NULL,
        ModelNumber NVARCHAR(100) NULL,

        BorrowedByEmpId INT NOT NULL,
        BorrowedByEmpName NVARCHAR(200) NOT NULL,
        BorrowedByDeptId INT NOT NULL,
        BorrowedByDeptName NVARCHAR(200) NOT NULL,
        BorrowEncodedByUserId INT NOT NULL,
        BorrowEncodedByUserName NVARCHAR(200) NOT NULL,
        BorrowedAtUtc DATETIME2(2) NOT NULL CONSTRAINT DF_BorrowLog_BorrowedAtUtc DEFAULT (SYSUTCDATETIME()),

        ReturnedByEmpId INT NULL,
        ReturnedByEmpName NVARCHAR(200) NULL,
        ReturnedByDeptId INT NULL,
        ReturnedByDeptName NVARCHAR(200) NULL,
        ReturnEncodedByUserId INT NULL,
        ReturnEncodedByUserName NVARCHAR(200) NULL,
        ReturnedAtUtc DATETIME2(2) NULL,

        RowVer ROWVERSION NOT NULL,

        CONSTRAINT PK_BorrowLog PRIMARY KEY CLUSTERED (BorrowId ASC),
        CONSTRAINT FK_BorrowLog_Item FOREIGN KEY (ItemId) REFERENCES dbo.Item(ItemId),
        CONSTRAINT FK_BorrowLog_BorrowedEmp FOREIGN KEY (BorrowedByEmpId) REFERENCES dbo.Employee(EmpId),
        CONSTRAINT FK_BorrowLog_ReturnedEmp FOREIGN KEY (ReturnedByEmpId) REFERENCES dbo.Employee(EmpId),
        CONSTRAINT FK_BorrowLog_BorrowEncodedBy FOREIGN KEY (BorrowEncodedByUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT FK_BorrowLog_ReturnEncodedBy FOREIGN KEY (ReturnEncodedByUserId) REFERENCES dbo.[User](UserId)
    );
END
GO

/* =====================================================================================
   2) Indexes
   ===================================================================================== */

-- Only one open borrow per item (prevents duplicate open borrows).
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_BorrowLog_OpenItem'
      AND object_id = OBJECT_ID('dbo.BorrowLog')
)
BEGIN
    CREATE UNIQUE INDEX UX_BorrowLog_OpenItem
    ON dbo.BorrowLog (ItemId)
    WHERE ReturnedAtUtc IS NULL;
END
GO

-- Fast lookup of open borrow by serial.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_BorrowLog_OpenSerial'
      AND object_id = OBJECT_ID('dbo.BorrowLog')
)
BEGIN
    CREATE INDEX IX_BorrowLog_OpenSerial
    ON dbo.BorrowLog (SerialNumber, BorrowedAtUtc DESC)
    INCLUDE (BorrowedByEmpName, BorrowedByDeptName, BorrowEncodedByUserName)
    WHERE ReturnedAtUtc IS NULL;
END
GO

-- History browsing (returned rows).
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_BorrowLog_Returned_BorrowedAt'
      AND object_id = OBJECT_ID('dbo.BorrowLog')
)
BEGIN
    CREATE INDEX IX_BorrowLog_Returned_BorrowedAt
    ON dbo.BorrowLog (ReturnedAtUtc, BorrowedAtUtc DESC)
    INCLUDE (SerialNumber, ItemName, BorrowedByEmpName, ReturnedByEmpName);
END
GO

