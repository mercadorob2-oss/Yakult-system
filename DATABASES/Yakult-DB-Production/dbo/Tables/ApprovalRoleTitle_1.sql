CREATE TABLE [dbo].[ApprovalRoleTitle] (
    [TitleId]         INT            IDENTITY (1, 1) NOT NULL,
    [PositionTitle]   NVARCHAR (100) NOT NULL,
    [ApprovalRole]    NVARCHAR (20)  NOT NULL,
    [RolePriority]    INT            NOT NULL,
    [IsActive]        BIT            CONSTRAINT [DF_ApprovalRoleTitle_IsActive] DEFAULT ((1)) NOT NULL,
    [DateCreated]     DATETIME2 (2)  CONSTRAINT [DF_ApprovalRoleTitle_DateCreated] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedByUserId] INT            NULL,
    CONSTRAINT [PK_ApprovalRoleTitle] PRIMARY KEY CLUSTERED ([TitleId] ASC),
    CONSTRAINT [CK_ApprovalRoleTitle_Role] CHECK ([ApprovalRole]='Manager' OR [ApprovalRole]='Supervisor' OR [ApprovalRole]='Coordinator'),
    CONSTRAINT [FK_ApprovalRoleTitle_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_ApprovalRoleTitle_Title] UNIQUE NONCLUSTERED ([PositionTitle] ASC)
);




GO
CREATE NONCLUSTERED INDEX [IX_ApprovalRoleTitle_Active]
    ON [dbo].[ApprovalRoleTitle]([IsActive] ASC, [RolePriority] DESC)
    INCLUDE([PositionTitle], [ApprovalRole]);

