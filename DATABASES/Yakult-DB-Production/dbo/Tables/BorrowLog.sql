CREATE TABLE [dbo].[BorrowLog] (
    [BorrowId]                INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]                  INT            NOT NULL,
    [SerialNumber]            NVARCHAR (255) NOT NULL,
    [ItemName]                NVARCHAR (200) NOT NULL,
    [ItemDescription]         NVARCHAR (400) NULL,
    [ModelNumber]             NVARCHAR (100) NULL,
    [BorrowedByEmpId]         INT            NULL,
    [BorrowedByEmpName]       NVARCHAR (200) NOT NULL,
    [BorrowedByDeptId]        INT            NULL,
    [BorrowedByDeptName]      NVARCHAR (200) NULL,
    [BorrowEncodedByUserId]   INT            NOT NULL,
    [BorrowEncodedByUserName] NVARCHAR (200) NOT NULL,
    [BorrowedAtUtc]           DATETIME2 (2)  CONSTRAINT [DF_BorrowLog_BorrowedAtUtc] DEFAULT (sysutcdatetime()) NOT NULL,
    [ReturnedByEmpId]         INT            NULL,
    [ReturnedByEmpName]       NVARCHAR (200) NULL,
    [ReturnedByDeptId]        INT            NULL,
    [ReturnedByDeptName]      NVARCHAR (200) NULL,
    [ReturnEncodedByUserId]   INT            NULL,
    [ReturnEncodedByUserName] NVARCHAR (200) NULL,
    [ReturnedAtUtc]           DATETIME2 (2)  NULL,
    [RowVer]                  ROWVERSION     NOT NULL,
    CONSTRAINT [PK_BorrowLog] PRIMARY KEY CLUSTERED ([BorrowId] ASC),
    CONSTRAINT [CK_BorrowLog_RequesterPresent] CHECK ([BorrowedByEmpId] IS NOT NULL OR [BorrowedByDeptId] IS NOT NULL),
    CONSTRAINT [FK_BorrowLog_BorrowedEmp] FOREIGN KEY ([BorrowedByEmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_BorrowLog_BorrowEncodedBy] FOREIGN KEY ([BorrowEncodedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_BorrowLog_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_BorrowLog_ReturnedEmp] FOREIGN KEY ([ReturnedByEmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_BorrowLog_ReturnEncodedBy] FOREIGN KEY ([ReturnEncodedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_BorrowLog_Returned_BorrowedAt]
    ON [dbo].[BorrowLog]([ReturnedAtUtc] ASC, [BorrowedAtUtc] DESC)
    INCLUDE([SerialNumber], [ItemName], [BorrowedByEmpName], [ReturnedByEmpName]);


GO
CREATE NONCLUSTERED INDEX [IX_BorrowLog_OpenSerial]
    ON [dbo].[BorrowLog]([SerialNumber] ASC, [BorrowedAtUtc] DESC)
    INCLUDE([BorrowedByEmpName], [BorrowedByDeptName], [BorrowEncodedByUserName]) WHERE ([ReturnedAtUtc] IS NULL);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_BorrowLog_OpenItem]
    ON [dbo].[BorrowLog]([ItemId] ASC) WHERE ([ReturnedAtUtc] IS NULL);

