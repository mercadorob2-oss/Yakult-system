CREATE TABLE [dbo].[DepartmentSupervisor] (
    [DeptSupervisorId] INT           IDENTITY (1, 1) NOT NULL,
    [DeptId]           INT           NOT NULL,
    [SupervisorUserId] INT           NOT NULL,
    [IsActive]         BIT           CONSTRAINT [DF_DeptSupervisor_IsActive] DEFAULT ((1)) NOT NULL,
    [DateAssigned]     DATETIME2 (2) CONSTRAINT [DF_DeptSupervisor_DateAssigned] DEFAULT (sysutcdatetime()) NOT NULL,
    [AssignedByUserId] INT           NULL,
    CONSTRAINT [PK_DepartmentSupervisor] PRIMARY KEY CLUSTERED ([DeptSupervisorId] ASC),
    CONSTRAINT [FK_DeptSupervisor_AssignedBy] FOREIGN KEY ([AssignedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_DeptSupervisor_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_DeptSupervisor_Supervisor] FOREIGN KEY ([SupervisorUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_DeptSupervisor_DeptUser] UNIQUE NONCLUSTERED ([DeptId] ASC, [SupervisorUserId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_DeptSupervisor_SupervisorUserId]
    ON [dbo].[DepartmentSupervisor]([SupervisorUserId] ASC, [IsActive] ASC)
    INCLUDE([DeptId]);


GO
CREATE NONCLUSTERED INDEX [IX_DeptSupervisor_DeptId]
    ON [dbo].[DepartmentSupervisor]([DeptId] ASC, [IsActive] ASC)
    INCLUDE([SupervisorUserId]);

