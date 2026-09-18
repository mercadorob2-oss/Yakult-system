-- Sub-Type Groups (Contract/Subscription/License/Services + ReferenceCode + date range) were
-- previously only an IMPLICIT concept: several dbo.SetItem rows sharing the same SubType +
-- ReferenceCode on the same SetId, aggregated for display via dbo.vw_SetItemSubTypeGroups.
-- This introduces a REAL one-row-per-group table so a future "view/edit sub-type group" page
-- has one row to load/edit/save instead of fanning writes across N SetItem rows. Mirrors the
-- shape of dbo.InvoicePreparation (the pre-invoice staging equivalent of a group) but is the
-- POST-invoice version: rows here always belong to a SetItem-based invoice (SetId NOT NULL).
-- dbo.SetItem's existing SubType/ReferenceCode/BeginDate/EndDate columns are NOT dropped --
-- they remain a synced cache written alongside this table; this table is the new
-- source of truth referenced by SetItem.GroupId (added in a companion migration).
IF OBJECT_ID('dbo.SetItemSubTypeGroup', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SetItemSubTypeGroup (
        GroupId        INT            IDENTITY (1, 1) NOT NULL,
        SetId          INT            NOT NULL,
        SubType        NVARCHAR (20)  NOT NULL,
        ReferenceCode  NVARCHAR (100) NULL,
        BeginDate      DATE           NULL,
        EndDate        DATE           NULL,
        CreatedBy      INT            NULL,
        CreatedAt      DATETIME2 (7)  CONSTRAINT DF_SetItemSubTypeGroup_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        ModifiedBy     INT            NULL,
        ModifiedAt     DATETIME2 (7)  NULL,
        CONSTRAINT PK_SetItemSubTypeGroup PRIMARY KEY CLUSTERED (GroupId ASC),
        CONSTRAINT FK_SetItemSubTypeGroup_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT CK_SetItemSubTypeGroup_SubType CHECK (
            SubType = 'Contract' OR SubType = 'Subscription' OR SubType = 'License' OR SubType = 'Services'
        )
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'IX_SetItemSubTypeGroup_SetId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SetItemSubTypeGroup_SetId
        ON dbo.SetItemSubTypeGroup (SetId ASC);
END
GO

-- One group per SetId + SubType + ReferenceCode (NULL ReferenceCode groups are exempt from the
-- uniqueness constraint via a filtered index, matching how the view already excludes them via
-- WHERE ReferenceCode IS NOT NULL).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SetItemSubTypeGroup') AND name = 'UQ_SetItemSubTypeGroup_SetId_SubType_ReferenceCode'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_SetItemSubTypeGroup_SetId_SubType_ReferenceCode
        ON dbo.SetItemSubTypeGroup (SetId ASC, SubType ASC, ReferenceCode ASC)
        WHERE ReferenceCode IS NOT NULL;
END
GO
