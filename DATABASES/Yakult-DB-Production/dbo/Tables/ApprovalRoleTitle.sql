CREATE TABLE [dbo].[ApprovalRoleTitle]
(
    [TitleId]        INT            IDENTITY(1,1) NOT NULL,
    [PositionTitle]  NVARCHAR(100)  NOT NULL,
    [ApprovalRole]   NVARCHAR(20)   NOT NULL,
    [RolePriority]   INT            NOT NULL,
    [IsActive]       BIT            NOT NULL,
    [DateCreated]    DATETIME2(2)   NOT NULL,
    [CreatedByUserId] INT           NULL,
    CONSTRAINT [PK_ApprovalRoleTitle] PRIMARY KEY CLUSTERED ([TitleId] ASC),
    CONSTRAINT [UQ_ApprovalRoleTitle_PositionTitle] UNIQUE ([PositionTitle])
)
