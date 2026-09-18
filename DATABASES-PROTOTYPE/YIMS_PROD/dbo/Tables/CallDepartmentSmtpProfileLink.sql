CREATE TABLE [dbo].[CallDepartmentSmtpProfileLink] (
    [DeptId]          INT           NOT NULL,
    [ProfileId]       INT           NOT NULL,
    [IsActive]        BIT           CONSTRAINT [DF_CallDeptSmtpLink_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]       DATETIME2 (2) CONSTRAINT [DF_CallDeptSmtpLink_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT           NULL,
    CONSTRAINT [PK_CallDepartmentSmtpProfileLink] PRIMARY KEY CLUSTERED ([DeptId] ASC),
    CONSTRAINT [FK_CallDeptSmtpLink_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CallDeptSmtpLink_Profile] FOREIGN KEY ([ProfileId]) REFERENCES [dbo].[CallSmtpProfile] ([ProfileId]),
    CONSTRAINT [FK_CallDeptSmtpLink_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallDeptSmtpLink_ProfileId]
    ON [dbo].[CallDepartmentSmtpProfileLink]([ProfileId] ASC);

