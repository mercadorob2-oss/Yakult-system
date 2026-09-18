CREATE TABLE [dbo].[CallTicketNote] (
    [NoteId]          BIGINT          IDENTITY (1, 1) NOT NULL,
    [TicketId]        INT             NOT NULL,
    [NoteType]        NVARCHAR (30)   CONSTRAINT [DF_CallTicketNote_Type] DEFAULT ('Note') NOT NULL,
    [NoteText]        NVARCHAR (4000) NOT NULL,
    [CreatedAt]       DATETIME2 (2)   CONSTRAINT [DF_CallTicketNote_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedByUserId] INT             NULL,
    CONSTRAINT [PK_CallTicketNote] PRIMARY KEY CLUSTERED ([NoteId] ASC),
    CONSTRAINT [FK_CallTicketNote_Ticket] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CallTicket] ([TicketId]),
    CONSTRAINT [FK_CallTicketNote_User] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallTicketNote_TicketId_CreatedAt]
    ON [dbo].[CallTicketNote]([TicketId] ASC, [CreatedAt] DESC);

