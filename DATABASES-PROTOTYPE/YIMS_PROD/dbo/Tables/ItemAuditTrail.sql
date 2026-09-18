CREATE TABLE [dbo].[ItemAuditTrail] (
    [Id]             INT           IDENTITY (1, 1) NOT NULL,
    [ItemId]         INT           NULL,
    [SerialNumber]   VARCHAR (50)  NULL,
    [Action]         VARCHAR (100) NOT NULL,
    [ActionTime]     DATETIME      DEFAULT (getdate()) NOT NULL,
    [EmployeeId]     INT           NULL,
    [EmployeeName]   VARCHAR (100) NULL,
    [DepartmentId]   INT           NULL,
    [DepartmentName] VARCHAR (100) NULL,
    [BranchId]       INT           NULL,
    [BranchName]     VARCHAR (100) NULL,
    [Direction]      VARCHAR (10)  NULL,
    [Status]         VARCHAR (50)  NULL,
    [Location]       VARCHAR (100) NULL,
    [ReferenceType]  VARCHAR (50)  NULL,
    [ReferenceId]    INT           NULL,
    [SetCode]        VARCHAR (20)  NULL,
    [Notes]          TEXT          NULL,
    [CreatedAt]      DATETIME      DEFAULT (getdate()) NULL,
    [CreatedBy]      VARCHAR (50)  NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemAuditTrail_Serial]
    ON [dbo].[ItemAuditTrail]([SerialNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_ItemAuditTrail_ItemId]
    ON [dbo].[ItemAuditTrail]([ItemId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_ItemAuditTrail_ActionTime]
    ON [dbo].[ItemAuditTrail]([ActionTime] ASC);

