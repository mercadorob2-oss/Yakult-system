CREATE TABLE [dbo].[SetDeploymentConfirmation] (
    [ConfirmationId]      INT             IDENTITY (1, 1) NOT NULL,
    [SetId]               INT             NOT NULL,
    [ConfirmedBy]         NVARCHAR (200)  NULL,
    [ConfirmedAt]         DATETIME2       NOT NULL,
    [SignatureBase64]     NVARCHAR (MAX)  NULL,
    [PhotoBase64]         NVARCHAR (MAX)  NULL,
    [GpsCoordinates]      NVARCHAR (100)  NULL,
    [DeviceId]            NVARCHAR (200)  NULL,
    [Notes]               NVARCHAR (500)  NULL,
    PRIMARY KEY CLUSTERED ([ConfirmationId] ASC),
    CONSTRAINT [FK_SetDeploymentConfirmation_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId])
);

GO
CREATE NONCLUSTERED INDEX [IX_SetDeploymentConfirmation_SetId]
    ON [dbo].[SetDeploymentConfirmation]([SetId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_SetDeploymentConfirmation_ConfirmedAt]
    ON [dbo].[SetDeploymentConfirmation]([ConfirmedAt] ASC);
