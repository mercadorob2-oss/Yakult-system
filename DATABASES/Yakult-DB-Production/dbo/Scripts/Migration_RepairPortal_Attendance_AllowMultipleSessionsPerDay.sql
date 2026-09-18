-- Migration: allows more than one Time In/Time Out session per technician per day.
--
-- dbo.RepairTechnicianAttendance had a hard UNIQUE (EmployeeId, WorkDate) constraint — exactly one
-- row per employee per calendar day. That forced sp_RepairPortal_TimeIn to reuse and overwrite that
-- single row on every re-clock-in, which meant TimeIn never moved forward after the first Time In of
-- the day: clocking back in later just cleared TimeOut on the SAME morning row, so the elapsed
-- stopwatch kept counting from hours earlier instead of resetting for the new session.
--
-- Fix: drop the one-row-per-day constraint. sp_RepairPortal_TimeIn now inserts a brand new row
-- (fresh TimeIn default) whenever there's no currently-open session (TimeOut IS NULL) for today,
-- instead of ever touching an existing row. sp_RepairPortal_TimeOut is unchanged — it already found
-- the correct open row via `WorkDate = @WorkDate AND TimeOut IS NULL`, which still works with
-- multiple rows per day since at most one can be open at a time.
-- GetTodayStatusAsync/GetLastTimeOutAsync (C# side) already query with TOP(1) ORDER BY AttendanceId
-- DESC / WorkDate DESC, AttendanceId DESC — both already handle multiple rows per day correctly, no
-- C# changes needed.
--
-- Idempotent — safe to re-run.

IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE name = 'UQ_RepairTechnicianAttendance_OneRowPerDay'
      AND parent_object_id = OBJECT_ID('dbo.RepairTechnicianAttendance')
)
BEGIN
    ALTER TABLE dbo.RepairTechnicianAttendance
        DROP CONSTRAINT UQ_RepairTechnicianAttendance_OneRowPerDay;
END

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_TimeIn
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EmployeeId IS NULL THROW 51050, 'EmployeeId is required.', 1;

    DECLARE @WorkDate DATE = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time');
    DECLARE @AttendanceId INT;

    BEGIN TRAN;

    -- Only reuse a row if there's already an OPEN session today (TimeOut IS NULL) — clicking Time In
    -- while already timed in is then a harmless no-op that just returns the existing session as-is.
    SELECT @AttendanceId = AttendanceId
    FROM dbo.RepairTechnicianAttendance WITH (UPDLOCK, HOLDLOCK)
    WHERE EmployeeId = @EmployeeId AND WorkDate = @WorkDate AND TimeOut IS NULL;

    IF @AttendanceId IS NULL
    BEGIN
        INSERT dbo.RepairTechnicianAttendance (EmployeeId, WorkDate)
        VALUES (@EmployeeId, @WorkDate);

        SET @AttendanceId = SCOPE_IDENTITY();
    END

    COMMIT;

    SELECT AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
    FROM dbo.RepairTechnicianAttendance
    WHERE AttendanceId = @AttendanceId;
END
GO
