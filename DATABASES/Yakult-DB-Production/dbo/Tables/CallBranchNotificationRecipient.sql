CREATE TABLE [dbo].[CallBranchNotificationRecipient] (
    [BranchId]         INT             NOT NULL,
    [RecipientEmails]  NVARCHAR (2000) NULL,
    [EscalationEmails] NVARCHAR (2000) NULL,
    [IsActive]         BIT             CONSTRAINT [DF_CallBranchNotif_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]        DATETIME2 (2)   CONSTRAINT [DF_CallBranchNotif_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId]  INT             NULL,
    CONSTRAINT [PK_CallBranchNotificationRecipient] PRIMARY KEY CLUSTERED ([BranchId] ASC),
    CONSTRAINT [FK_CallBranchNotif_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_CallBranchNotif_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallBranchNotif_IsActive]
    ON [dbo].[CallBranchNotificationRecipient]([IsActive] ASC)
    INCLUDE([BranchId]);

