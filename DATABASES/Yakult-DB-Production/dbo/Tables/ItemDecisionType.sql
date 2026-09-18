CREATE TABLE [dbo].[ItemDecisionType] (
    [DecisionTypeId]   INT            IDENTITY (1, 1) NOT NULL,
    [DecisionTypeName] NVARCHAR (20)  NOT NULL,
    [Description]      NVARCHAR (200) NULL,
    CONSTRAINT [PK_ItemDecisionType] PRIMARY KEY CLUSTERED ([DecisionTypeId] ASC),
    CONSTRAINT [UQ_ItemDecisionType_Name] UNIQUE NONCLUSTERED ([DecisionTypeName] ASC)
);

