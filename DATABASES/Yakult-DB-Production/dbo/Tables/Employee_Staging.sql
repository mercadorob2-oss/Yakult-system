CREATE TABLE [dbo].[Employee_Staging] (
    [EmployeeName]   NVARCHAR (150)   NULL,
    [CompanyName]    NVARCHAR (150)   NULL,
    [BranchName]     NVARCHAR (150)   NULL,
    [DepartmentName] NVARCHAR (150)   NULL,
    [Position]       NVARCHAR (50)    NULL,
    [EmployeeNumber] VARCHAR (20)     NULL,
    [EmployeeEmail]  NVARCHAR (255)   NULL,
    [ImportBatchId]  UNIQUEIDENTIFIER NULL,
    [ImportedAt]     DATETIME2 (2)    CONSTRAINT [DF_Employee_Staging_ImportedAt] DEFAULT (sysdatetime()) NOT NULL,
    [Name]           NVARCHAR (150)   DEFAULT ('') NOT NULL,
    [IsFactory]      BIT              CONSTRAINT [DF_EmpStaging_IsFactory] DEFAULT ((0)) NULL,
    [IsDepot]        BIT              CONSTRAINT [DF_EmpStaging_IsDepot] DEFAULT ((0)) NULL,
    [IsDistributor]  BIT              CONSTRAINT [DF_EmpStaging_IsDistributor] DEFAULT ((0)) NULL,
    [IsCenter]       BIT              CONSTRAINT [DF_EmpStaging_IsCenter] DEFAULT ((0)) NULL,
    [Processed]      BIT              CONSTRAINT [DF_EmpStaging_Processed] DEFAULT ((0)) NOT NULL,
    [Title]          NVARCHAR (10)    NULL
);

