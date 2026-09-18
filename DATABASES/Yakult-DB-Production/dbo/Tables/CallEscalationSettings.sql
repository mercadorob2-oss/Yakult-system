CREATE TABLE [dbo].[CallEscalationSettings] (
    [SettingsId]         INT           IDENTITY (1, 1) NOT NULL,
    [DaysToSupervisor]   INT           NOT NULL,
    [DaysToManager]      INT           NOT NULL,
    [SupervisorPosition] NVARCHAR (50) NOT NULL,
    [ManagerPosition]    NVARCHAR (50) NOT NULL,
    [UpdatedAt]          DATETIME2 (2) CONSTRAINT [DF_CallEscalationSettings_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    CONSTRAINT [PK_CallEscalationSettings] PRIMARY KEY CLUSTERED ([SettingsId] ASC)
);

