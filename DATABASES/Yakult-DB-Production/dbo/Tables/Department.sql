CREATE TABLE [dbo].[Department] (
    [DeptId]       INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (150) NOT NULL,
    [Description]  NVARCHAR (400) NULL,
    [DateCreated]  DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]    INT            NOT NULL,
    [DateModified] DATETIME2 (2)  CONSTRAINT [DF_Department_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]   INT            NULL,
    [RowVer]       ROWVERSION     NOT NULL,
    [Active]       BIT            DEFAULT ((1)) NOT NULL,
    [Section]      NVARCHAR (200) NULL,
    [ParentDeptId] INT            NULL,
    [Acronym]      NVARCHAR (20)  NULL,
    PRIMARY KEY CLUSTERED ([DeptId] ASC),
    CONSTRAINT [FK_Department_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Department_ModifiedBy] FOREIGN KEY ([ModifiedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Department_ParentDept] FOREIGN KEY ([ParentDeptId]) REFERENCES [dbo].[Department] ([DeptId])
);










GO
CREATE NONCLUSTERED INDEX [IX_Department_Active]
    ON [dbo].[Department]([Active] ASC);


GO



GO
CREATE NONCLUSTERED INDEX [IX_Department_ParentDeptId]
    ON [dbo].[Department]([ParentDeptId] ASC)
    INCLUDE([Name], [Active]);

