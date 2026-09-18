CREATE TABLE [dbo].[Condition] (
    [ConditionID]   INT           IDENTITY (1, 1) NOT NULL,
    [ConditionName] NVARCHAR (20) NOT NULL,
    PRIMARY KEY CLUSTERED ([ConditionID] ASC),
    UNIQUE NONCLUSTERED ([ConditionName] ASC),
    CONSTRAINT [UQ_Condition_ConditionName] UNIQUE NONCLUSTERED ([ConditionName] ASC)
);

