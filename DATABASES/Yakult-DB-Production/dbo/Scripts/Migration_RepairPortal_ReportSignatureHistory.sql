-- Migration: append-only history of Reviewed By / Received By signatures actually entered when a
-- Repair Report was generated (RepairSignatoryPickerWindow's "Add to Report"/"Skip"). Lets a
-- reprint prefill from whoever signed last time instead of starting blank every time, without ever
-- overwriting/losing what was recorded previously — same append-only shape as
-- dbo.RepairTicketHistory/dbo.RepairPartHistory elsewhere in this module.
--
-- No EmployeeId FK — the picker's Employee field is a free-editable textbox (autocomplete assists
-- but doesn't force a match to a real dbo.Employee row), and prefill only ever needs to redisplay
-- text, never resolve back to a live employee record, matching how the printed report itself
-- already renders name/title as plain text with no FK.
--
-- Idempotent — safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'RepairReportSignature' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.RepairReportSignature (
        SignatureId       INT             IDENTITY (1, 1) NOT NULL,
        RepairTicketId    INT             NOT NULL,
        RoleName          NVARCHAR (50)   NOT NULL,
        EmployeeName      NVARCHAR (200)  NULL,
        Title             NVARCHAR (200)  NULL,
        SignedDate        DATETIME2 (2)   NULL,
        RecordedAt        DATETIME2 (2)   CONSTRAINT DF_RepairReportSignature_RecordedAt DEFAULT (sysutcdatetime()) NOT NULL,
        RecordedByUserId  INT             NULL,
        CONSTRAINT PK_RepairReportSignature PRIMARY KEY CLUSTERED (SignatureId ASC),
        CONSTRAINT FK_RepairReportSignature_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairReportSignature_RecordedByUser FOREIGN KEY (RecordedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairReportSignature_Ticket_Role_RecordedAt' AND object_id = OBJECT_ID('dbo.RepairReportSignature')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_RepairReportSignature_Ticket_Role_RecordedAt
        ON dbo.RepairReportSignature (RepairTicketId, RoleName, RecordedAt DESC);
END
GO
