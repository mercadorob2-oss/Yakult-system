-- Migration: Add TicketSource column to dbo.CallTicket
-- Date: 2026-07-16
-- Purpose: Distinguish tickets submitted via the Yakult Systems Portal
--          ('Portal') from those created by IT staff ('CallIT'), enabling
--          the ITCM desktop app to show incoming portal tickets separately.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CallTicket')
      AND name = 'TicketSource'
)
BEGIN
    ALTER TABLE dbo.CallTicket
        ADD [TicketSource] NVARCHAR(20) NULL;

    PRINT 'Column TicketSource added to dbo.CallTicket.';
END
ELSE
BEGIN
    PRINT 'Column TicketSource already exists on dbo.CallTicket. Skipped.';
END
GO

-- Update vw_Call_TicketList to include TicketSource
IF EXISTS (
    SELECT 1 FROM sys.views WHERE object_id = OBJECT_ID('dbo.vw_Call_TicketList')
)
BEGIN
    EXEC ('
        CREATE OR ALTER VIEW dbo.vw_Call_TicketList
        AS
        SELECT
            t.TicketId,
            t.TicketCode,
            t.ComId,
            c.Name AS Company,
            t.Issue,
            d.Name AS Department,
            t.BranchId,
            t.AssignedToEmpId,
            CASE
                WHEN b.BranchId IS NULL THEN NULL
                WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + '' (Center)''
                WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + '' (Depot)''
                WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + '' (Factory)''
                WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + '' (Distributor)''
                ELSE b.Name
            END AS Branch,
            eAssignee.Name AS ResponsiblePerson,
            DATEDIFF(DAY, t.CreatedAt, COALESCE(t.SolvedAt, SYSUTCDATETIME())) AS TicketAgeDays,
            COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt) AS LastActivityAt,
            DATEDIFF(DAY, COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt), SYSUTCDATETIME()) AS IdleDays,
            t.LastContactAt,
            t.Status,
            t.Priority,
            t.CallerName,
            t.IssueType,
            t.CreatedAt,
            t.UpdatedAt,
            t.SolvedAt,
            t.TicketSource
        FROM dbo.CallTicket t
        LEFT JOIN dbo.Company c ON c.ComId = t.ComId
        LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
        LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId
        LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId;
    ');

    PRINT 'View vw_Call_TicketList updated with TicketSource column.';
END
GO

-- Update sp_Call_CreateTicket to accept @TicketSource parameter
IF EXISTS (
    SELECT 1 FROM sys.procedures WHERE object_id = OBJECT_ID('dbo.sp_Call_CreateTicket')
)
BEGIN
    DECLARE @hasRequiredParams BIT = CASE WHEN NOT EXISTS (
        SELECT 1
        FROM (VALUES ('@TicketSource'), ('@CreatedAtUtc'), ('@LastContactAtUtc')) AS required(name)
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.parameters p
            WHERE p.object_id = OBJECT_ID('dbo.sp_Call_CreateTicket')
              AND p.name = required.name
        )
    ) THEN 1 ELSE 0 END;

    IF @hasRequiredParams = 0
    BEGIN
        EXEC ('
            CREATE OR ALTER PROCEDURE dbo.sp_Call_CreateTicket
                @ComId            INT            = NULL,
                @DeptId           INT            = NULL,
                @BranchId         INT            = NULL,
                @CallerName       NVARCHAR(150),
                @Issue            NVARCHAR(2000),
                @ProvidedSolution NVARCHAR(2000) = NULL,
                @IssueType        NVARCHAR(20)   = NULL,
                @Priority         NVARCHAR(20)   = NULL,
                @AssignedToEmpId  INT            = NULL,
                @CreatedByUserId  INT            = NULL,
                @TicketSource     NVARCHAR(20)   = NULL,
                @CreatedAtUtc     DATETIME2(2)   = NULL,
                @LastContactAtUtc DATETIME2(2)   = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF @CallerName IS NULL OR LTRIM(RTRIM(@CallerName)) = '''' THROW 50001, ''CallerName is required.'', 1;
                IF @Issue IS NULL OR LTRIM(RTRIM(@Issue)) = '''' THROW 50002, ''Issue is required.'', 1;

                IF @Priority IS NULL OR LTRIM(RTRIM(@Priority)) = '''' SET @Priority = ''Medium'';

                SET @Priority = LTRIM(RTRIM(@Priority));
                DECLARE @PriorityCanonical NVARCHAR(20) =
                    CASE UPPER(@Priority)
                        WHEN ''LOW'' THEN ''Low''
                        WHEN ''MEDIUM'' THEN ''Medium''
                        WHEN ''HIGH'' THEN ''High''
                        WHEN ''CRITICAL'' THEN ''Critical''
                        ELSE NULL
                    END;

                IF @PriorityCanonical IS NULL
                    THROW 50014, ''Invalid priority. Allowed: Low, Medium, High, Critical.'', 1;

                IF @AssignedToEmpId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @AssignedToEmpId)
                    THROW 50016, ''AssignedToEmpId is invalid (employee not found).'', 1;

                IF @BranchId IS NOT NULL
                   AND OBJECT_ID(''dbo.Branch'', ''U'') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Branch WHERE BranchId = @BranchId)
                    THROW 50017, ''BranchId is invalid (branch not found).'', 1;

                /* Validate org combination via BranchDepartmentCompany (CompanyID) */
                IF @ComId IS NOT NULL
                   AND OBJECT_ID(''dbo.BranchDepartmentCompany'', ''U'') IS NOT NULL
                BEGIN
                    IF @BranchId IS NOT NULL
                    BEGIN
                        IF @DeptId IS NOT NULL
                        BEGIN
                            IF NOT EXISTS (
                                SELECT 1
                                FROM dbo.BranchDepartmentCompany bdc
                                WHERE bdc.CompanyID = @ComId
                                  AND bdc.BranchID = @BranchId
                                  AND bdc.DepartmentID = @DeptId
                            )
                                THROW 50030, ''Invalid Company/Branch/Department combination.'', 1;
                        END
                        ELSE
                        BEGIN
                            IF NOT EXISTS (
                                SELECT 1
                                FROM dbo.BranchDepartmentCompany bdc
                                WHERE bdc.CompanyID = @ComId
                                  AND bdc.BranchID = @BranchId
                            )
                                THROW 50031, ''Invalid Company/Branch combination.'', 1;
                        END
                    END
                    ELSE IF @DeptId IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM dbo.BranchDepartmentCompany bdc
                            WHERE bdc.CompanyID = @ComId
                              AND bdc.DepartmentID = @DeptId
                        )
                            THROW 50032, ''Invalid Company/Department combination.'', 1;
                    END
                END

                DECLARE @TicketId INT;

                BEGIN TRAN;

                INSERT dbo.CallTicket
                (
                    ComId, DeptId, BranchId, CallerName, Issue, ProvidedSolution,
                    IssueType, Priority, Status,
                    AssignedToEmpId,
                    CreatedByUserId,
                    LastContactAt,
                    TicketSource
                )
                VALUES
                (
                    @ComId, @DeptId, @BranchId, @CallerName, @Issue, @ProvidedSolution,
                    @IssueType, @PriorityCanonical, ''Pending'',
                    @AssignedToEmpId,
                    @CreatedByUserId,
                    COALESCE(@LastContactAtUtc, @CreatedAtUtc, SYSUTCDATETIME()),
                    @TicketSource
                );

                SET @TicketId = SCOPE_IDENTITY();

                IF OBJECT_ID(''dbo.CallTicketHistory'', ''U'') IS NOT NULL
                BEGIN
                    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
                    VALUES (@TicketId, @CreatedByUserId, ''Created'', NULL, NULL, ''Ticket created'');

                    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
                    VALUES
                        (@TicketId, @CreatedByUserId, ''Status'',   NULL, ''Pending''),
                        (@TicketId, @CreatedByUserId, ''Priority'', NULL, @PriorityCanonical);

                    IF @AssignedToEmpId IS NOT NULL
                        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
                        VALUES (@TicketId, @CreatedByUserId, ''AssignedToEmpId'', NULL, CONVERT(NVARCHAR(50), @AssignedToEmpId));

                    IF @BranchId IS NOT NULL
                        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
                        VALUES (@TicketId, @CreatedByUserId, ''BranchId'', NULL, CONVERT(NVARCHAR(50), @BranchId));
                END

                COMMIT;

                SELECT
                    t.TicketId,
                    t.TicketCode,
                    t.Status,
                    t.Priority,
                    t.CreatedAt
                FROM dbo.CallTicket t
                WHERE t.TicketId = @TicketId;
            END
        ');

        PRINT 'Stored procedure sp_Call_CreateTicket updated with @TicketSource parameter.';
    END
    ELSE
    BEGIN
        PRINT 'Required ticket-creation parameters already exist. Skipped.';
    END
END
GO
