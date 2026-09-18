CREATE TABLE [dbo].[BranchDepartmentCompany] (
    [BranchDeptCompanyID] INT            IDENTITY (1, 1) NOT NULL,
    [BranchID]            INT            NOT NULL,
    [DepartmentID]        INT            NULL,       -- NULL allowed: branch belongs to company but no dept (e.g. YMC-only rows)
    [CompanyID]           INT            NOT NULL,
    [BranchEmail]         NVARCHAR (255) NULL,
    [CreatedDate]         DATETIME2 (7)  CONSTRAINT [DF_BranchDeptCompany_CreatedDate] DEFAULT (GETDATE()) NOT NULL,
    [UpdatedDate]         DATETIME2 (7)  NULL,
    CONSTRAINT [PK_BranchDepartmentCompany]     PRIMARY KEY CLUSTERED ([BranchDeptCompanyID] ASC),
    CONSTRAINT [FK_BranchDeptCompany_Branch]     FOREIGN KEY ([BranchID])     REFERENCES [dbo].[Branch]     ([BranchId]),
    CONSTRAINT [FK_BranchDeptCompany_Department] FOREIGN KEY ([DepartmentID]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_BranchDeptCompany_Company]    FOREIGN KEY ([CompanyID])    REFERENCES [dbo].[Company]    ([ComId]),
    -- Note: uniqueness enforced by two filtered indexes below (handles NULL DepartmentID correctly)
    -- UQ_BDC_WithDept and UQ_BDC_NoDept replace the original non-filtered unique constraint
);

GO
-- Filtered unique: rows WITH a department (standard case)
CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_WithDept]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC, [CompanyID] ASC, [DepartmentID] ASC)
    WHERE [DepartmentID] IS NOT NULL;


GO
-- Filtered unique: rows WITHOUT a department (YMC / company-only binding)
CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_NoDept]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC, [CompanyID] ASC)
    WHERE [DepartmentID] IS NULL;


GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_BranchID]
    ON [dbo].[BranchDepartmentCompany]([BranchID] ASC)
    INCLUDE([DepartmentID], [CompanyID], [BranchEmail]);


GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_DepartmentID]
    ON [dbo].[BranchDepartmentCompany]([DepartmentID] ASC)
    INCLUDE([BranchID], [CompanyID]);


GO
CREATE NONCLUSTERED INDEX [IX_BranchDeptCompany_CompanyID]
    ON [dbo].[BranchDepartmentCompany]([CompanyID] ASC)
    INCLUDE([BranchID], [DepartmentID]);
