CREATE TABLE [dbo].[EmployeeEmail] (
    [EmployeeEmailId] INT           IDENTITY (1, 1) NOT NULL,
    [EmpId]           INT           NOT NULL,
    [EmailId]         INT           NOT NULL,
    [EmailRole]       NVARCHAR (50) NOT NULL,
    [IsPrimary]       BIT           CONSTRAINT [DF_EmployeeEmail_IsPrimary] DEFAULT ((0)) NOT NULL,
    [IsActive]        BIT           CONSTRAINT [DF_EmployeeEmail_IsActive] DEFAULT ((1)) NOT NULL,
    [DateCreated]     DATETIME2 (2) CONSTRAINT [DF_EmployeeEmail_DateCreated] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedByUserId] INT           NULL,
    CONSTRAINT [PK_EmployeeEmail] PRIMARY KEY CLUSTERED ([EmployeeEmailId] ASC),
    CONSTRAINT [FK_EmployeeEmail_Email] FOREIGN KEY ([EmailId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [FK_EmployeeEmail_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_EmployeeEmail_User] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_EmployeeEmail] UNIQUE NONCLUSTERED ([EmpId] ASC, [EmailId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_EmployeeEmail_EmpId]
    ON [dbo].[EmployeeEmail]([EmpId] ASC, [IsActive] ASC)
    INCLUDE([EmailId], [IsPrimary], [EmailRole]);


GO
CREATE NONCLUSTERED INDEX [IX_EmployeeEmail_EmailId]
    ON [dbo].[EmployeeEmail]([EmailId] ASC, [IsActive] ASC)
    INCLUDE([EmpId], [EmailRole]);

