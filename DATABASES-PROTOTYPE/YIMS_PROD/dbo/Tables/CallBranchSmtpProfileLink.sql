CREATE TABLE [dbo].[CallBranchSmtpProfileLink] (
    [BranchId]        INT           NOT NULL,
    [ProfileId]       INT           NOT NULL,
    [IsActive]        BIT           CONSTRAINT [DF_CallBranchSmtpLink_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]       DATETIME2 (2) CONSTRAINT [DF_CallBranchSmtpLink_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT           NULL,
    CONSTRAINT [PK_CallBranchSmtpProfileLink] PRIMARY KEY CLUSTERED ([BranchId] ASC),
    CONSTRAINT [FK_CallBranchSmtpLink_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_CallBranchSmtpLink_Profile] FOREIGN KEY ([ProfileId]) REFERENCES [dbo].[CallSmtpProfile] ([ProfileId]),
    CONSTRAINT [FK_CallBranchSmtpLink_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CallBranchSmtpLink_ProfileId]
    ON [dbo].[CallBranchSmtpProfileLink]([ProfileId] ASC);

