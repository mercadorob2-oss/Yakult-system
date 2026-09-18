CREATE TABLE [dbo].[CallNotificationRules] (
    [RulesId]              INT            IDENTITY (1, 1) NOT NULL,
    [NotifyOnNewTicket]    BIT            CONSTRAINT [DF_CallRules_New] DEFAULT ((1)) NOT NULL,
    [NotifyOnStatusChange] BIT            CONSTRAINT [DF_CallRules_Status] DEFAULT ((1)) NOT NULL,
    [NotifyOnEscalation]   BIT            CONSTRAINT [DF_CallRules_Esc] DEFAULT ((1)) NOT NULL,
    [NotifyOnReminder]     BIT            CONSTRAINT [DF_CallRules_Rem] DEFAULT ((0)) NOT NULL,
    [GroupEmail]           NVARCHAR (255) NULL,
    [EscalationEmail]      NVARCHAR (255) NULL,
    [ReminderDays]         INT            CONSTRAINT [DF_CallRules_RemDays] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]            DATETIME2 (2)  CONSTRAINT [DF_CallRules_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId]      INT            NULL,
    CONSTRAINT [PK_CallNotificationRules] PRIMARY KEY CLUSTERED ([RulesId] ASC),
    CONSTRAINT [FK_CallNotificationRules_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);

