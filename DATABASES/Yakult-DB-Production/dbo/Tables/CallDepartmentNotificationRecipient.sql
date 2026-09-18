CREATE TABLE [dbo].[CallDepartmentNotificationRecipient] (
    [DeptId]           INT             NOT NULL,
    [RecipientEmails]  NVARCHAR (2000) NULL,
    [EscalationEmails] NVARCHAR (2000) NULL,
    [IsActive]         BIT             CONSTRAINT [DF_CallDeptNotif_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]        DATETIME2 (2)   CONSTRAINT [DF_CallDeptNotif_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId]  INT             NULL,
    CONSTRAINT [PK_CallDepartmentNotificationRecipient] PRIMARY KEY CLUSTERED ([DeptId] ASC),
    CONSTRAINT [FK_CallDeptNotif_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CallDeptNotif_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallDeptNotif_IsActive]
    ON [dbo].[CallDepartmentNotificationRecipient]([IsActive] ASC)
    INCLUDE([DeptId]);

