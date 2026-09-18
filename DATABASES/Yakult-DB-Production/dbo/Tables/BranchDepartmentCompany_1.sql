CREATE TABLE [dbo].[BranchDepartmentCompany] (
    [BranchDeptCompanyID] INT            IDENTITY (1, 1) NOT NULL,
    [BranchID]            INT            NOT NULL,
    [DepartmentID]        INT            NULL,
    [CompanyID]           INT            NOT NULL,
    [BranchEmail]         NVARCHAR (255) NULL,
    [CreatedDate]         DATETIME2 (7)  CONSTRAINT [DF_BranchDeptCompany_CreatedDate] DEFAULT (getdate()) NOT NULL,
    [UpdatedDate]         DATETIME2 (7)  NULL,
    CONSTRAINT [PK_BranchDepartmentCompany] PRIMARY KEY CLUSTERED ([BranchDeptCompanyID] ASC),
    CONSTRAINT [FK_BranchDeptCompany_Branch] FOREIGN KEY ([BranchID]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_BranchDeptCompany_Company] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[Company] ([ComId]),
    CONSTRAINT [FK_BranchDeptCompany_Department] FOREIGN KEY ([DepartmentID]) REFERENCES [dbo].[Department] ([DeptId])
);




GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_CompanyID]
    ON [dbo].[BranchDepartmentCompany]([CompanyID] ASC)
    INCLUDE([BranchID], [DepartmentID]);


GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_DepartmentID]
    ON [dbo].[BranchDepartmentCompany]([DepartmentID] ASC)
    INCLUDE([BranchID], [CompanyID]);


GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_BranchID]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC)
    INCLUDE([DepartmentID], [CompanyID], [BranchEmail]);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_BranchDeptCompany]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC, [CompanyID] ASC, [DepartmentID] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_WithDept]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC, [CompanyID] ASC, [DepartmentID] ASC) WHERE ([DepartmentID] IS NOT NULL);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_NoDept]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC, [CompanyID] ASC) WHERE ([DepartmentID] IS NULL);

