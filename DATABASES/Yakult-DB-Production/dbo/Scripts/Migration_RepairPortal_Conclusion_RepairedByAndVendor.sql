-- Migration_RepairPortal_Conclusion_RepairedByAndVendor.sql
-- Adds two pieces of data capture to the Repair Conclusion workflow:
--   1. "Repaired By" — which IT Dept. employee(s) actually performed the repair. Can be more
--      than one person, so this is a many-to-many junction table (RepairConclusion is one row
--      per ticket, so the set can't live as a column on it).
--   2. "3rd Party Handover" — when a repair was sent out to an external vendor instead of (or
--      alongside) being handled in-house. Nullable FK to dbo.Vendor on RepairConclusion itself,
--      since a ticket is only ever handed to at most one vendor.
-- Idempotent (guarded), safe to re-run.

-- ── 1. dbo.RepairConclusionRepairedByEmp (many-to-many: ticket -> repaired-by employees) ─────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairConclusionRepairedByEmp')
)
BEGIN
    CREATE TABLE dbo.RepairConclusionRepairedByEmp (
        RepairTicketId INT NOT NULL,
        EmpId          INT NOT NULL,
        CONSTRAINT PK_RepairConclusionRepairedByEmp PRIMARY KEY CLUSTERED (RepairTicketId ASC, EmpId ASC),
        CONSTRAINT FK_RepairConclusionRepairedByEmp_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairConclusionRepairedByEmp_Emp FOREIGN KEY (EmpId) REFERENCES dbo.Employee (EmpId)
    );
END
GO

-- ── 2. dbo.RepairConclusion.HandedOverToVendorId ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'HandedOverToVendorId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD HandedOverToVendorId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RepairConclusion_Vendor'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion
        ADD CONSTRAINT FK_RepairConclusion_Vendor FOREIGN KEY (HandedOverToVendorId) REFERENCES dbo.Vendor (VendorID);
END
GO

-- ── 3. sp_RepairPortal_SaveConclusion — add @HandedOverToVendorId parameter ───────────────────
-- The RepairedBy employee set is maintained separately by the repository (plain DELETE+INSERT
-- against the junction table in the same connection) rather than via a table-valued parameter
-- here, to keep this proc's signature simple and match how this codebase already handles small
-- many-to-many sets elsewhere.
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SaveConclusion
    @RepairTicketId       INT,
    @RootCause             NVARCHAR(2000) = NULL,
    @WorkPerformed         NVARCHAR(2000) = NULL,
    @FinalOutcome          NVARCHAR(2000) = NULL,
    @Recommendations       NVARCHAR(2000) = NULL,
    @CompletedByUserId     INT = NULL,
    @CompletedByEmpId      INT = NULL,
    @HandedOverToVendorId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51170, 'Repair ticket not found.', 1;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.RepairConclusion WHERE RepairTicketId = @RepairTicketId)
    BEGIN
        UPDATE dbo.RepairConclusion
        SET RootCause = @RootCause,
            WorkPerformed = @WorkPerformed,
            FinalOutcome = @FinalOutcome,
            Recommendations = @Recommendations,
            CompletedByUserId = @CompletedByUserId,
            CompletedByEmpId = @CompletedByEmpId,
            HandedOverToVendorId = @HandedOverToVendorId,
            CompletedAt = COALESCE(CompletedAt, SYSUTCDATETIME()),
            UpdatedAt = SYSUTCDATETIME()
        WHERE RepairTicketId = @RepairTicketId;
    END
    ELSE
    BEGIN
        INSERT dbo.RepairConclusion
            (RepairTicketId, RootCause, WorkPerformed, FinalOutcome, Recommendations, CompletedByUserId, CompletedByEmpId, HandedOverToVendorId, CompletedAt)
        VALUES
            (@RepairTicketId, @RootCause, @WorkPerformed, @FinalOutcome, @Recommendations, @CompletedByUserId, @CompletedByEmpId, @HandedOverToVendorId, SYSUTCDATETIME());
    END

    COMMIT;
END
GO
