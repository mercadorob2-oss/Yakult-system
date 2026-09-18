-- Migration: Create CompanyHoliday table
-- Used by the Holiday Management page (Reference Data) and
-- UserActivityInsightsService to exclude holidays from active-day calculations.

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.[CompanyHoliday]') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.[CompanyHoliday]
    (
        HolidayId   INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        HolidayName NVARCHAR(100) NOT NULL,
        HolidayDate DATE          NOT NULL,
        HolidayType NVARCHAR(50)  NOT NULL DEFAULT ('Regular Holiday'),
        IsRecurring BIT           NOT NULL DEFAULT (0),
        Notes       NVARCHAR(255) NULL,
        IsActive    BIT           NOT NULL DEFAULT (1),
        CreatedBy   INT           NULL,
        CreatedDate DATETIME      NOT NULL DEFAULT (GETDATE())
    );
END

-- Add IsRecurring if table already existed without it
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[CompanyHoliday]') AND name = 'IsRecurring'
)
BEGIN
    ALTER TABLE dbo.[CompanyHoliday] ADD IsRecurring BIT NOT NULL DEFAULT (0);
END
