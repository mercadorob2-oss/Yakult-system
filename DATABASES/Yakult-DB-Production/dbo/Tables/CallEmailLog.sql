CREATE TABLE [dbo].[CallEmailLog] (
    [EmailLogId]      BIGINT          IDENTITY (1, 1) NOT NULL,
    [TicketId]        INT             NULL,
    [EmailType]       NVARCHAR (30)   NOT NULL,
    [Recipient]       NVARCHAR (255)  NOT NULL,
    [Subject]         NVARCHAR (255)  NULL,
    [Status]          NVARCHAR (20)   NOT NULL,
    [ErrorMessage]    NVARCHAR (2000) NULL,
    [DateSent]        DATETIME2 (2)   CONSTRAINT [DF_CallEmailLog_DateSent] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedByUserId] INT             NULL,
    CONSTRAINT [PK_CallEmailLog] PRIMARY KEY CLUSTERED ([EmailLogId] ASC),
    CONSTRAINT [FK_CallEmailLog_Ticket] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CallTicket] ([TicketId]),
    CONSTRAINT [FK_CallEmailLog_User] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallEmailLog_DateSent]
    ON [dbo].[CallEmailLog]([DateSent] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_CallEmailLog_TicketId]
    ON [dbo].[CallEmailLog]([TicketId] ASC);

