SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.AccountRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountRequest (
        AccountRequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountRequest PRIMARY KEY,
        EmpId INT NOT NULL,
        RequestedUsername NVARCHAR(100) NOT NULL,
        WorkEmail NVARCHAR(255) NOT NULL,
        PasswordHash VARBINARY(256) NULL,
        PasswordSalt VARBINARY(128) NULL,
        Status VARCHAR(20) NOT NULL CONSTRAINT DF_AccountRequest_Status DEFAULT ('Pending'),
        SubmittedAt DATETIME2(2) NOT NULL CONSTRAINT DF_AccountRequest_SubmittedAt DEFAULT (SYSUTCDATETIME()),
        ReviewedAt DATETIME2(2) NULL,
        ReviewedByUserId INT NULL,
        ReviewRemarks NVARCHAR(500) NULL,
        CreatedUserId INT NULL,
        CONSTRAINT CK_AccountRequest_Status CHECK (Status IN ('Pending','Approved','Rejected')),
        CONSTRAINT FK_AccountRequest_Employee FOREIGN KEY (EmpId) REFERENCES dbo.Employee(EmpId),
        CONSTRAINT FK_AccountRequest_Reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT FK_AccountRequest_CreatedUser FOREIGN KEY (CreatedUserId) REFERENCES dbo.[User](UserId)
    );

    CREATE UNIQUE INDEX UX_AccountRequest_PendingEmployee ON dbo.AccountRequest(EmpId) WHERE Status = 'Pending';
    CREATE UNIQUE INDEX UX_AccountRequest_PendingUsername ON dbo.AccountRequest(RequestedUsername) WHERE Status = 'Pending';
    CREATE INDEX IX_AccountRequest_StatusSubmitted ON dbo.AccountRequest(Status, SubmittedAt DESC);
END;

COMMIT TRANSACTION;
