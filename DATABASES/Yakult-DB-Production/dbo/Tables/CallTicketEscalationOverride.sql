CREATE TABLE [dbo].[CallTicketEscalationOverride] (
    [TicketId]           INT            NOT NULL,
    [DaysToSupervisor]   INT            NOT NULL,
    [DaysToManager]      INT            NOT NULL,
    [Reason]             NVARCHAR (400) NOT NULL,
    [OverriddenByUserId] INT            NULL,
    [OverriddenAt]       DATETIME2 (2)  CONSTRAINT [DF_CallTicketEscalationOverride_OverriddenAt] DEFAULT (sysutcdatetime()) NOT NULL,
    CONSTRAINT [PK_CallTicketEscalationOverride] PRIMARY KEY CLUSTERED ([TicketId] ASC),
    CONSTRAINT [FK_CallTicketEscalationOverride_Ticket] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CallTicket] ([TicketId])
);

