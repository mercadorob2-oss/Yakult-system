CREATE TABLE [dbo].[CallTicketHistory] (
    [HistoryId]       BIGINT          IDENTITY (1, 1) NOT NULL,
    [TicketId]        INT             NOT NULL,
    [ChangedAt]       DATETIME2 (2)   CONSTRAINT [DF_CallTicketHistory_ChangedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [ChangedByUserId] INT             NULL,
    [FieldName]       NVARCHAR (50)   NOT NULL,
    [OldValue]        NVARCHAR (2000) NULL,
    [NewValue]        NVARCHAR (2000) NULL,
    [Note]            NVARCHAR (2000) NULL,
    CONSTRAINT [PK_CallTicketHistory] PRIMARY KEY CLUSTERED ([HistoryId] ASC),
    CONSTRAINT [FK_CallTicketHistory_Ticket] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CallTicket] ([TicketId]),
    CONSTRAINT [FK_CallTicketHistory_User] FOREIGN KEY ([ChangedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicketHistory_TicketId_ChangedAt]
    ON [dbo].[CallTicketHistory]([TicketId] ASC, [ChangedAt] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicketHistory_Ticket_Field_ChangedAt]
    ON [dbo].[CallTicketHistory]([TicketId] ASC, [FieldName] ASC, [ChangedAt] DESC)
    INCLUDE([NewValue], [ChangedByUserId]);

