CREATE TABLE [dbo].[CartridgeAuthorization] (
    [AuthorizationId]      INT              IDENTITY (1, 1) NOT NULL,
    [EmployeeId]           INT              NOT NULL,
    [DepartmentId]         INT              NOT NULL,
    [Status]               VARCHAR (20)     CONSTRAINT [DF_CartridgeAuthorization_Status] DEFAULT ('Pending') NOT NULL,
    [SignedBySupervisorId] INT              NULL,
    [SignedDate]           DATETIME         NULL,
    [CreatedDate]          DATETIME         CONSTRAINT [DF_CartridgeAuthorization_CreatedDate] DEFAULT (getdate()) NOT NULL,
    [SignatureData]        NVARCHAR (MAX)   NULL,
    [SignatureSource]      VARCHAR (10)     NULL,
    [SignatureFileName]    NVARCHAR (255)   NULL,
    [RequestedModels]      NVARCHAR (1000)  NULL,
    [SignerPosition]       NVARCHAR (100)   NULL,
    [SignerCompany]        NVARCHAR (100)   NULL,
    [SignerBranch]         NVARCHAR (100)   NULL,
    [SubmissionSessionId]  UNIQUEIDENTIFIER NULL,
    -- Routing: who should approve this request (NULL = no approver account found yet)
    [AssignedToUserId]    INT              NULL,
    -- Tracking: which IT user submitted on behalf of the employee (IT-assisted requests)
    [SubmittedByUserId]   INT              NULL,
    CONSTRAINT [PK_CartridgeAuthorization] PRIMARY KEY CLUSTERED ([AuthorizationId] ASC),
    CONSTRAINT [CK_CartridgeAuthorization_Status] CHECK ([Status]='Used' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'),
    CONSTRAINT [FK_CartridgeAuthorization_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CartridgeAuthorization_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_CartridgeAuthorization_Supervisor] FOREIGN KEY ([SignedBySupervisorId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_CartridgeAuthorization_AssignedUser] FOREIGN KEY ([AssignedToUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_CartridgeAuthorization_SubmittedBy] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeAuthorization_DepartmentId_Status]
    ON [dbo].[CartridgeAuthorization]([DepartmentId] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeAuthorization_AssignedToUserId_Status]
    ON [dbo].[CartridgeAuthorization]([AssignedToUserId] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeAuthorization_EmployeeId]
    ON [dbo].[CartridgeAuthorization]([EmployeeId] ASC);

