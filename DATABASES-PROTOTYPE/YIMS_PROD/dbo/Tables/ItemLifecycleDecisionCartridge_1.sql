CREATE TABLE [dbo].[ItemLifecycleDecisionCartridge] (
    [DecisionCartridgeId] INT IDENTITY (1, 1) NOT NULL,
    [DecisionId]          INT NOT NULL,
    [EmptyCartridgeId]    INT NOT NULL,
    CONSTRAINT [PK_ItemLifecycleDecisionCartridge] PRIMARY KEY CLUSTERED ([DecisionCartridgeId] ASC),
    CONSTRAINT [FK_ItemLifecycleDecisionCartridge_Decision] FOREIGN KEY ([DecisionId]) REFERENCES [dbo].[ItemLifecycleDecision] ([DecisionId]),
    CONSTRAINT [FK_ItemLifecycleDecisionCartridge_EmptyCartridge] FOREIGN KEY ([EmptyCartridgeId]) REFERENCES [dbo].[EmptyCartridge] ([EmptyCartridgeId]),
    CONSTRAINT [UQ_ItemLifecycleDecisionCartridge_Decision] UNIQUE NONCLUSTERED ([DecisionId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecisionCartridge_EmptyCartridgeId]
    ON [dbo].[ItemLifecycleDecisionCartridge]([EmptyCartridgeId] ASC)
    INCLUDE([DecisionId]);

