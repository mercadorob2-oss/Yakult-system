CREATE TABLE [dbo].[CallTicket] (
    [TicketId]           INT             IDENTITY (1, 1) NOT NULL,
    [TicketCode]         AS              ('TCK-'+right('000000'+CONVERT([varchar](6),[TicketId]),(6))) PERSISTED,
    [ComId]              INT             NULL,
    [DeptId]             INT             NULL,
    [CallerName]         NVARCHAR (150)  NOT NULL,
    [ContactEmail]       NVARCHAR (255)  NULL,
    [Issue]              NVARCHAR (2000) NOT NULL,
    [ProvidedSolution]   NVARCHAR (2000) NULL,
    [Priority]           NVARCHAR (20)   CONSTRAINT [DF_CallTicket_Priority] DEFAULT ('Medium') NOT NULL,
    [Status]             NVARCHAR (20)   CONSTRAINT [DF_CallTicket_Status] DEFAULT ('Pending') NOT NULL,
    [AssignedToUserId]   INT             NULL,
    [CreatedByUserId]    INT             NULL,
    [CreatedAt]          DATETIME2 (2)   CONSTRAINT [DF_CallTicket_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedAt]          DATETIME2 (2)   CONSTRAINT [DF_CallTicket_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [SolvedAt]           DATETIME2 (2)   CONSTRAINT [DF_CallTicket_SolvedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RowVer]             ROWVERSION      NOT NULL,
    [IssueType]          NVARCHAR (20)   NULL,
    [LastContactAt]      DATETIME2 (2)   CONSTRAINT [DF_CallTicket_LastContactAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [LastReminderSentAt] DATETIME2 (2)   CONSTRAINT [DF_CallTicket_LastReminderSentAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [AssignedToEmpId]    INT             NULL,
    [BranchId]           INT             NULL,
    [TicketSource]       NVARCHAR (20)   NULL,
    CONSTRAINT [PK_CallTicket] PRIMARY KEY CLUSTERED ([TicketId] ASC),
    CONSTRAINT [FK_CallTicket_AssignedToEmp] FOREIGN KEY ([AssignedToEmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_CallTicket_AssignedToUser] FOREIGN KEY ([AssignedToUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_CallTicket_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_CallTicket_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_CallTicket_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_CallTicket_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicket_Status_Priority]
    ON [dbo].[CallTicket]([Status] ASC, [Priority] ASC)
    INCLUDE([CreatedAt], [AssignedToUserId]);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicket_ComDept]
    ON [dbo].[CallTicket]([ComId] ASC, [DeptId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicket_Status_Assigned_Updated]
    ON [dbo].[CallTicket]([Status] ASC, [AssignedToEmpId] ASC, [UpdatedAt] DESC)
    INCLUDE([TicketCode], [Priority], [CreatedAt], [DeptId]);


GO


/* =========================================================
   KEEP UpdatedAt CURRENT (even if someone updates directly)
   ========================================================= */
CREATE   TRIGGER dbo.trg_CallTicket_SetUpdatedAt
ON dbo.CallTicket
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.CallTicket t
    INNER JOIN inserted i ON i.TicketId = t.TicketId;
END
