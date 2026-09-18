CREATE TABLE [dbo].[CallAutoEscalationAssigneeSettings] (
    [SettingsId]      INT           IDENTITY (1, 1) NOT NULL,
    [AssigneeEmpId]   INT           NULL,
    [UpdatedAt]       DATETIME2 (2) CONSTRAINT [DF_CallAutoEscAssignee_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT           NULL,
    CONSTRAINT [PK_CallAutoEscalationAssigneeSettings] PRIMARY KEY CLUSTERED ([SettingsId] ASC),
    CONSTRAINT [FK_CallAutoEscalationAssigneeSettings_Emp] FOREIGN KEY ([AssigneeEmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_CallAutoEscalationAssigneeSettings_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);

