CREATE TABLE [dbo].[Employee] (
    [EmpId]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]           NVARCHAR (150) NOT NULL,
    [Description]    NVARCHAR (400) NULL,
    [DateCreated]    DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]      INT            NULL,
    [DateModified]   DATETIME2 (2)  CONSTRAINT [DF_Employee_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]     INT            NULL,
    [ComId]          INT            NOT NULL,
    [BranchId]       INT            NOT NULL,
    [DeptId]         INT            NULL,
    [RowVer]         ROWVERSION     NOT NULL,
    [Active]         BIT            DEFAULT ((1)) NOT NULL,
    [Position]       NVARCHAR (50)  NULL,
    [EmployeeNumber] VARCHAR (20)   NULL,
    [TitleId]        INT            NULL,
    PRIMARY KEY CLUSTERED ([EmpId] ASC),
    CONSTRAINT [FK_Employee_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_Employee_Company] FOREIGN KEY ([ComId]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_Employee_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Employee_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_Employee_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Employee_Title] FOREIGN KEY ([TitleId]) REFERENCES [dbo].[Title] ([TitleId])
);






GO
CREATE NONCLUSTERED INDEX [IX_Employee_Active]
    ON [dbo].[Employee]([Active] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Employee_BranchId]
    ON [dbo].[Employee]([BranchId] ASC)
    INCLUDE([Name], [Position], [DeptId], [ComId]);


GO
CREATE NONCLUSTERED INDEX [IX_Employee_DeptId]
    ON [dbo].[Employee]([DeptId] ASC)
    INCLUDE([Name], [Position], [BranchId]);


GO
CREATE NONCLUSTERED INDEX [IX_Employee_ComId]
    ON [dbo].[Employee]([ComId] ASC)
    INCLUDE([Name], [Position], [BranchId], [DeptId]);


GO
CREATE NONCLUSTERED INDEX [IX_Employee_CreatedBy]
    ON [dbo].[Employee]([CreatedBy] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Employee_Active_Name]
    ON [dbo].[Employee]([Active] ASC, [Name] ASC)
    INCLUDE([EmpId], [EmployeeNumber], [Position], [ComId], [BranchId], [DeptId]);

