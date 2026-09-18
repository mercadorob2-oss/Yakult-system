CREATE TABLE [dbo].[CallDepartmentSmtpProfile] (
    [ProfileId]       INT             IDENTITY (1, 1) NOT NULL,
    [DeptId]          INT             NULL,
    [DepartmentName]  NVARCHAR (150)  NOT NULL,
    [SmtpServer]      NVARCHAR (255)  NOT NULL,
    [SmtpPort]        INT             CONSTRAINT [DF_CallDeptSmtp_Port] DEFAULT ((587)) NOT NULL,
    [UseSsl]          BIT             CONSTRAINT [DF_CallDeptSmtp_UseSsl] DEFAULT ((1)) NOT NULL,
    [SmtpUsername]    NVARCHAR (255)  NULL,
    [SmtpPasswordEnc] VARBINARY (512) NULL,
    [FromName]        NVARCHAR (150)  NULL,
    [FromEmail]       NVARCHAR (255)  NULL,
    [UpdatedAt]       DATETIME2 (2)   CONSTRAINT [DF_CallDeptSmtp_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT             NULL,
    CONSTRAINT [PK_CallDepartmentSmtpProfile] PRIMARY KEY CLUSTERED ([ProfileId] ASC),
    CONSTRAINT [FK_CallDeptSmtp_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CallDeptSmtp_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_CallDepartmentSmtpProfile_DepartmentName] UNIQUE NONCLUSTERED ([DepartmentName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CallDeptSmtp_DepartmentName]
    ON [dbo].[CallDepartmentSmtpProfile]([DepartmentName] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CallDeptSmtp_DeptId]
    ON [dbo].[CallDepartmentSmtpProfile]([DeptId] ASC);

