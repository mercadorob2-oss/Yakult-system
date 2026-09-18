CREATE TABLE [dbo].[CartridgeApproval] (
    [ApprovalId]         INT              IDENTITY (1, 1) NOT NULL,
    [EmpId]              INT              NOT NULL,
    [DeptId]             INT              NOT NULL,
    [SupervisorUserId]   INT              NOT NULL,
    [Status]             NVARCHAR (20)    NOT NULL,
    [Notes]              NVARCHAR (500)   NULL,
    [RequestedAt]        DATETIME2 (2)    CONSTRAINT [DF_CartridgeApproval_RequestedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [ReviewedAt]         DATETIME2 (2)    NULL,
    [ExpiresAt]          DATETIME2 (2)    NULL,
    [ApprovalToken]      UNIQUEIDENTIFIER CONSTRAINT [DF_CartridgeApproval_Token] DEFAULT (newid()) NOT NULL,
    [RequestorIpAddress] NVARCHAR (45)    NULL,
    [ReviewerIpAddress]  NVARCHAR (45)    NULL,
    [EmailSentAt]        DATETIME2 (2)    NULL,
    [EmailLogId]         INT              NULL,
    [ComId]              INT              NULL,
    [BranchId]           INT              NULL,
    [SupervisorEmpId]    INT              NULL,
    [ApprovalRole]       NVARCHAR (20)    NULL,
    [SupervisorPosition] NVARCHAR (50)    NULL,
    CONSTRAINT [PK_CartridgeApproval] PRIMARY KEY CLUSTERED ([ApprovalId] ASC),
    CONSTRAINT [CK_CartridgeApproval_ApprovalRole] CHECK ([ApprovalRole]='Manager' OR [ApprovalRole]='Supervisor' OR [ApprovalRole]='Coordinator'),
    CONSTRAINT [CK_CartridgeApproval_Status] CHECK ([Status]='Expired' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'),
    CONSTRAINT [FK_CartridgeApproval_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_CartridgeApproval_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_CartridgeApproval_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CartridgeApproval_EmailLog] FOREIGN KEY ([EmailLogId]) REFERENCES [dbo].[SystemEmailLog] ([LogId]),
    CONSTRAINT [FK_CartridgeApproval_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_CartridgeApproval_Supervisor] FOREIGN KEY ([SupervisorUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_CartridgeApproval_SupervisorEmp] FOREIGN KEY ([SupervisorEmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [UQ_CartridgeApproval_Token] UNIQUE NONCLUSTERED ([ApprovalToken] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_CartridgeApproval_SingleActive]
    ON [dbo].[CartridgeApproval]([EmpId] ASC) WHERE ([Status] IN ('Pending', 'Approved'));


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeApproval_Token]
    ON [dbo].[CartridgeApproval]([ApprovalToken] ASC)
    INCLUDE([ApprovalId], [Status], [SupervisorUserId]);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeApproval_SupervisorUserId_Status]
    ON [dbo].[CartridgeApproval]([SupervisorUserId] ASC, [Status] ASC)
    INCLUDE([EmpId], [DeptId], [RequestedAt]);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeApproval_EmpId_Status]
    ON [dbo].[CartridgeApproval]([EmpId] ASC, [Status] ASC)
    INCLUDE([SupervisorUserId], [RequestedAt], [ExpiresAt]);

