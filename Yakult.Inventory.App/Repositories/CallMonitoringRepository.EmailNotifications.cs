using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        private sealed class ItcmDbInfoRow
        {
            public string ServerName { get; set; }
            public string DatabaseName { get; set; }
        }

        private sealed class ItcmSchedulerSignalRow
        {
            public DateTime? LastActivityUtc { get; set; }
            public int ActivityCountLast24Hours { get; set; }
        }

        public sealed class ItcmSchemaCheckResult
        {
            public string Area { get; set; }
            public string ObjectName { get; set; }
            public bool Exists { get; set; }
            public string Detail { get; set; }
            public string Severity { get; set; }
            public string SuggestedFix { get; set; }
        }

        public sealed class ItcmDiagnosticCheckResult
        {
            public string Area { get; set; }
            public string Name { get; set; }
            public string Severity { get; set; }
            public string Message { get; set; }
            public string SuggestedFix { get; set; }
        }

        public sealed class ItcmDiagnosticsTrendPoint
        {
            public DateTime Date { get; set; }
            public int FailedEmails { get; set; }
            public int Escalations { get; set; }
            public int SchedulerSignals { get; set; }
            public int TicketVolume { get; set; }
            public int? AvgResolutionMinutes { get; set; }
        }

        public sealed class ItcmDiagnosticsVersionInfo
        {
            public string DesktopVersion { get; set; }
            public string SchedulerVersion { get; set; }
            public string DatabaseMigrationVersion { get; set; }
        }

        public sealed class ItcmSchedulerHeartbeat
        {
            public bool TableExists { get; set; }
            public DateTime? LastStartedUtc { get; set; }
            public DateTime? LastFinishedUtc { get; set; }
            public DateTime? LastSuccessUtc { get; set; }
            public string LastError { get; set; }
            public string MachineName { get; set; }
            public string Version { get; set; }
        }

        public sealed class ItcmDiagnosticsSnapshot
        {
            public string ServerName { get; set; }
            public string DatabaseName { get; set; }
            public List<ItcmSchemaCheckResult> SchemaChecks { get; set; } = new List<ItcmSchemaCheckResult>();

            public CallDashboardMetrics DashboardMetrics { get; set; }
            public int PendingTickets { get; set; }
            public string SchedulerTaskState { get; set; }
            public bool? SchedulerTaskEnabled { get; set; }
            public DateTime? SchedulerTaskNextRunLocal { get; set; }
            public string SchedulerTaskLastResult { get; set; }
            public bool? SchedulerIsRunning { get; set; }
            public DateTime? SchedulerLastActivityUtc { get; set; }
            public int SchedulerSignalCountLast24Hours { get; set; }

            public DateTime? LastAutoEscalationUtc { get; set; }
            public CallEmailLogItem LastEmailFailed { get; set; }
            public CallEmailLogItem LastEmailSkipped { get; set; }
            public CallEmailLogItem LastReminderEmail { get; set; }
            public CallEmailLogItem LastEscalationEmail { get; set; }

            public ItcmBackgroundLockProbe BackgroundLockProbe { get; set; }
            public ItcmSchedulerHeartbeat SchedulerHeartbeat { get; set; }
            public ItcmDiagnosticsVersionInfo VersionInfo { get; set; }
            public List<ItcmDiagnosticCheckResult> HealthChecks { get; set; } = new List<ItcmDiagnosticCheckResult>();
            public List<ItcmDiagnosticsTrendPoint> Trends { get; set; } = new List<ItcmDiagnosticsTrendPoint>();
        }

        public sealed class ItcmBackgroundLockProbe
        {
            public string LockName { get; set; }
            public bool? IsFree { get; set; }
            public int? ResultCode { get; set; }
            public string Error { get; set; }
        }

        public async Task<ItcmBackgroundLockProbe> ProbeItcmBackgroundLockAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                return await ProbeItcmBackgroundLockAsync(connection);
            }
        }

        private static async Task<ItcmBackgroundLockProbe> ProbeItcmBackgroundLockAsync(SqlConnection connection)
        {
            var probe = new ItcmBackgroundLockProbe
            {
                LockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs"
            };

            try
            {
                const string acquireSql = @"
DECLARE @res int;
EXEC @res = sp_getapplock
    @Resource = @Resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = 0;
SELECT @res;";

                var res = await connection.ExecuteScalarAsync<int>(acquireSql, new { Resource = probe.LockName });
                probe.ResultCode = res;
                probe.IsFree = res >= 0;

                if (res >= 0)
                {
                    try
                    {
                        await connection.ExecuteAsync(
                            "EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';",
                            new { Resource = probe.LockName });
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                probe.IsFree = null;
                probe.Error = ex.Message;
            }

            return probe;
        }

        public async Task<CallEmailSettingsItem> GetEmailSettingsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailSettings"))
                    return null;

                const string sql = @"
SELECT TOP 1
    SettingsId,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallEmailSettings
ORDER BY SettingsId DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallEmailSettingsItem>(sql);
            }
        }

        public async Task SaveEmailSettingsAsync(CallEmailSettingsItem settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailSettings"))
                    throw new InvalidOperationException("CallEmailSettings table is not installed in this database yet.");

                const string sql = @"
INSERT dbo.CallEmailSettings
(
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    UpdatedByUserId
)
VALUES
(
    @SmtpServer,
    @SmtpPort,
    @UseSsl,
    @SmtpUsername,
    @SmtpPasswordEnc,
    @FromName,
    @FromEmail,
    @UpdatedByUserId
);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        settings.SmtpServer,
                        settings.SmtpPort,
                        settings.UseSsl,
                        settings.SmtpUsername,
                        settings.SmtpPasswordEnc,
                        settings.FromName,
                        settings.FromEmail,
                        settings.UpdatedByUserId
                    });
            }
        }

        public async Task<List<CallEmailLogItem>> GetEmailLogAsync(int take = 250)
        {
            if (take < 1)
                take = 1;

            if (take > 5000)
                take = 5000;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"))
                    return new List<CallEmailLogItem>();

                var hasTicketListView = await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_TicketList");
                var hasBranchNameColumn = hasTicketListView
                    ? await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Branch")
                    : false;

                var sql = @"
SELECT TOP (@Take)
    l.EmailLogId,
    l.TicketId," +
                          (hasTicketListView && hasBranchNameColumn
                              ? "\n    t.Branch,"
                              : "\n    CAST(NULL AS nvarchar(255)) AS Branch,") +
@"
    l.EmailType,
    l.Recipient,
    l.Subject,
    l.Status,
    l.ErrorMessage,
    l.DateSent,
    l.CreatedByUserId
FROM dbo.CallEmailLog l
" + (hasTicketListView ? "LEFT JOIN dbo.vw_Call_TicketList t ON t.TicketId = l.TicketId\n" : "") + @"
ORDER BY l.DateSent DESC, l.EmailLogId DESC;";

                var rows = await connection.QueryAsync<CallEmailLogItem>(sql, new { Take = take });
                return rows?.ToList() ?? new List<CallEmailLogItem>();
            }
        }

        public async Task<List<CallEmailLogItem>> GetEmailLogPageAsync(string searchText, string emailTypeFilter, int pageIndex, int pageSize)
        {
            if (pageIndex < 1)
                pageIndex = 1;

            if (pageSize < 1)
                pageSize = 1;

            if (pageSize > 500)
                pageSize = 500;

            var skip = (pageIndex - 1) * pageSize;
            var takePlusOne = pageSize + 1;
            var startRow = skip + 1;
            var endRow = skip + takePlusOne;

            var q = (searchText ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var normalizedEmailType = string.IsNullOrWhiteSpace(emailTypeFilter) ? null : emailTypeFilter.Trim();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"))
                    return new List<CallEmailLogItem>();

                var hasTicketListView = await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_TicketList");
                var hasBranchNameColumn = hasTicketListView
                    ? await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Branch")
                    : false;

                var sql = @"
 WITH LogRows AS
 (
     SELECT
        l.EmailLogId,
        l.TicketId," +
                          (hasTicketListView && hasBranchNameColumn
                              ? "\n        t.Branch,"
                              : "\n        CAST(NULL AS nvarchar(255)) AS Branch,") +
@"
        l.EmailType,
        l.Recipient,
        l.Subject,
        l.Status,
        l.ErrorMessage,
        l.DateSent,
        l.CreatedByUserId,
        ROW_NUMBER() OVER (ORDER BY l.DateSent DESC, l.EmailLogId DESC) AS rn
    FROM dbo.CallEmailLog l
" + (hasTicketListView ? "    LEFT JOIN dbo.vw_Call_TicketList t ON t.TicketId = l.TicketId\n" : "") + @"
    WHERE (@EmailType IS NULL OR l.EmailType = @EmailType)
      AND
      (
        @HasQuery = 0
        OR
        (
            (@HasTicketIdSearch = 1 AND l.TicketId = @TicketIdSearch)
         OR l.EmailType LIKE @Search
         OR l.Recipient LIKE @Search
         OR l.Status LIKE @Search
         OR l.ErrorMessage LIKE @Search
         OR l.Subject LIKE @Search" + (hasTicketListView && hasBranchNameColumn ? "\n         OR t.Branch LIKE @Search" : string.Empty) + @"
        )
      )
 )
 SELECT
    EmailLogId,
    TicketId,
    Branch,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    DateSent,
    CreatedByUserId
 FROM LogRows
 WHERE rn BETWEEN @StartRow AND @EndRow
 ORDER BY rn;";

                var parameters = new
                {
                    EmailType = normalizedEmailType,
                    HasQuery = hasQuery ? 1 : 0,
                    Search = "%" + q + "%",
                    HasTicketIdSearch = int.TryParse(q, out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                    TicketIdSearch = int.TryParse(q, out var ticketIdSearch2) ? ticketIdSearch2 : 0,
                    StartRow = startRow,
                    EndRow = endRow
                };

                var rows = await connection.QueryAsync<CallEmailLogItem>(sql, parameters);
                return rows?.ToList() ?? new List<CallEmailLogItem>();
            }
        }

        public async Task<int> GetEmailLogCountAsync(string searchText, string emailTypeFilter)
        {
            var q = (searchText ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);
            var normalizedEmailType = string.IsNullOrWhiteSpace(emailTypeFilter) ? null : emailTypeFilter.Trim();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"))
                    return 0;

                var hasTicketListView = await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_TicketList");
                var hasBranchNameColumn = hasTicketListView
                    ? await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Branch")
                    : false;

                var sql = @"
SELECT COUNT(1)
FROM dbo.CallEmailLog l
" + (hasTicketListView && hasBranchNameColumn ? "LEFT JOIN dbo.vw_Call_TicketList t ON t.TicketId = l.TicketId\n" : "") + @"
WHERE (@EmailType IS NULL OR l.EmailType = @EmailType)
  AND
  (
    @HasQuery = 0
    OR
    (
        (@HasTicketIdSearch = 1 AND l.TicketId = @TicketIdSearch)
     OR l.EmailType LIKE @Search
     OR l.Recipient LIKE @Search
     OR l.Status LIKE @Search
     OR l.ErrorMessage LIKE @Search
     OR l.Subject LIKE @Search" + (hasTicketListView && hasBranchNameColumn ? "\n     OR t.Branch LIKE @Search" : string.Empty) + @"
    )
  );";

                var parameters = new
                {
                    EmailType = normalizedEmailType,
                    HasQuery = hasQuery ? 1 : 0,
                    Search = "%" + q + "%",
                    HasTicketIdSearch = int.TryParse(q, out var ticketIdSearch) && ticketIdSearch > 0 ? 1 : 0,
                    TicketIdSearch = int.TryParse(q, out var ticketIdSearch2) ? ticketIdSearch2 : 0
                };

                var count = await connection.ExecuteScalarAsync<int>(sql, parameters);
                return count < 0 ? 0 : count;
            }
        }

        public async Task<CallEmailLogItem> GetLastEmailLogForTicketAsync(int ticketId)
        {
            if (ticketId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"))
                    return null;

                var hasTicketListView = await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_TicketList");
                var hasBranchColumn = hasTicketListView
                    ? await CallSchemaGate.ColumnExistsAsync(connection, "dbo.vw_Call_TicketList", "Branch")
                    : false;

                var branchSelect = hasBranchColumn ? "t.Branch AS Branch," : "CAST(NULL AS nvarchar(300)) AS Branch,";
                var join = hasTicketListView ? "LEFT JOIN dbo.vw_Call_TicketList t ON t.TicketId = l.TicketId" : string.Empty;

                var sql = $@"
SELECT TOP 1
    l.EmailLogId,
    l.TicketId,
    {branchSelect}
    l.EmailType,
    l.Recipient,
    l.Subject,
    l.Status,
    l.ErrorMessage,
    l.DateSent,
    l.CreatedByUserId
FROM dbo.CallEmailLog l
{join}
WHERE l.TicketId = @TicketId
ORDER BY l.DateSent DESC, l.EmailLogId DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallEmailLogItem>(sql, new { TicketId = ticketId });
            }
        }

        public async Task LogEmailAsync(
            int? ticketId,
            string emailType,
            string recipient,
            string subject,
            string status,
            string errorMessage,
            int? createdByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"))
                    return;

                const string sql = @"
INSERT dbo.CallEmailLog
(
    TicketId,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    CreatedByUserId
)
VALUES
(
    @TicketId,
    @EmailType,
    @Recipient,
    @Subject,
    @Status,
    @ErrorMessage,
    @CreatedByUserId
);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        TicketId = ticketId,
                        EmailType = (emailType ?? string.Empty).Trim(),
                        Recipient = (recipient ?? string.Empty).Trim(),
                        Subject = (object)subject ?? System.DBNull.Value,
                        Status = (status ?? string.Empty).Trim(),
                        ErrorMessage = (object)errorMessage ?? System.DBNull.Value,
                        CreatedByUserId = createdByUserId
                    });
            }
        }

        public async Task<CallEmailTemplateItem> GetEmailTemplateByTypeAsync(string templateType)
        {
            templateType = (templateType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(templateType))
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailTemplate"))
                    return null;

                const string sql = @"
SELECT TOP 1
    TemplateId,
    TemplateType,
    Subject,
    Body,
    IsActive,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallEmailTemplate
WHERE TemplateType = @TemplateType
ORDER BY TemplateId DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallEmailTemplateItem>(sql, new { TemplateType = templateType });
            }
        }

        public async Task SaveEmailTemplateAsync(CallEmailTemplateItem template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            template.TemplateType = (template.TemplateType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(template.TemplateType))
                throw new ArgumentException("TemplateType is required.", nameof(template));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailTemplate"))
                    throw new InvalidOperationException("CallEmailTemplate table is not installed in this database yet.");

                const string sql = @"
MERGE dbo.CallEmailTemplate AS tgt
USING (SELECT @TemplateType AS TemplateType) AS src
    ON tgt.TemplateType = src.TemplateType
WHEN MATCHED THEN
    UPDATE SET
        Subject = @Subject,
        Body = @Body,
        IsActive = @IsActive,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT (TemplateType, Subject, Body, IsActive, UpdatedByUserId)
    VALUES (@TemplateType, @Subject, @Body, @IsActive, @UpdatedByUserId);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        TemplateType = template.TemplateType,
                        Subject = template.Subject ?? string.Empty,
                        Body = template.Body ?? string.Empty,
                        IsActive = template.IsActive,
                        template.UpdatedByUserId
                    });
            }
        }

        public async Task<CallNotificationRulesItem> GetNotificationRulesAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallNotificationRules"))
                    return null;

                const string sql = @"
SELECT TOP 1
    RulesId,
    NotifyOnNewTicket,
    NotifyOnStatusChange,
    NotifyOnEscalation,
    NotifyOnReminder,
    GroupEmail,
    EscalationEmail,
    ReminderDays,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallNotificationRules
ORDER BY RulesId DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallNotificationRulesItem>(sql);
            }
        }

        public async Task SaveNotificationRulesAsync(CallNotificationRulesItem rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallNotificationRules"))
                    throw new InvalidOperationException("CallNotificationRules table is not installed in this database yet.");

                const string sql = @"
INSERT dbo.CallNotificationRules
(
    NotifyOnNewTicket,
    NotifyOnStatusChange,
    NotifyOnEscalation,
    NotifyOnReminder,
    GroupEmail,
    EscalationEmail,
    ReminderDays,
    UpdatedByUserId
)
VALUES
(
    @NotifyOnNewTicket,
    @NotifyOnStatusChange,
    @NotifyOnEscalation,
    @NotifyOnReminder,
    @GroupEmail,
    @EscalationEmail,
    @ReminderDays,
    @UpdatedByUserId
);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        rules.NotifyOnNewTicket,
                        rules.NotifyOnStatusChange,
                        rules.NotifyOnEscalation,
                        rules.NotifyOnReminder,
                        rules.GroupEmail,
                        rules.EscalationEmail,
                        rules.ReminderDays,
                        rules.UpdatedByUserId
                    });
            }
        }

        public async Task<int> GetOverdueDaysAsync(int defaultDays = 3)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string findColumnSql = @"
SELECT TOP 1 c.name
FROM sys.columns c
INNER JOIN sys.objects o ON o.object_id = c.object_id
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE s.name = 'dbo'
  AND o.name = 'CallNotificationRules'
  AND o.type = 'U'
  AND c.name IN ('ReminderDays', 'ReminderIntervalDays', 'ReminderInterval', 'ReminderDaysInterval')
ORDER BY
    CASE c.name
        WHEN 'ReminderDays' THEN 1
        WHEN 'ReminderIntervalDays' THEN 2
        WHEN 'ReminderInterval' THEN 3
        WHEN 'ReminderDaysInterval' THEN 4
        ELSE 99
    END;";

                var columnName = await connection.ExecuteScalarAsync<string>(findColumnSql);
                if (string.IsNullOrWhiteSpace(columnName))
                    return defaultDays;

                var sql = $@"
DECLARE @Days INT;
SELECT TOP 1 @Days = ISNULL([{columnName}], @DefaultDays)
FROM dbo.CallNotificationRules;
SELECT CASE WHEN @Days IS NULL OR @Days < 1 THEN @DefaultDays ELSE @Days END;";

                return await connection.ExecuteScalarAsync<int>(sql, new { DefaultDays = defaultDays });
            }
        }

        public async Task<ItcmDiagnosticsSnapshot> GetDiagnosticsSnapshotAsync()
        {
            var snapshot = new ItcmDiagnosticsSnapshot();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                try
                {
                    const string dbInfoSql = @"
SELECT
    CAST(SERVERPROPERTY('ServerName') AS nvarchar(255)) AS ServerName,
    DB_NAME() AS DatabaseName;";

                    var dbInfo = await connection.QuerySingleOrDefaultAsync<ItcmDbInfoRow>(dbInfoSql);
                    snapshot.ServerName = (dbInfo?.ServerName ?? string.Empty).Trim();
                    snapshot.DatabaseName = (dbInfo?.DatabaseName ?? string.Empty).Trim();
                }
                catch
                {
                    snapshot.ServerName = string.Empty;
                    snapshot.DatabaseName = string.Empty;
                }

                // ---------- Schema checks ----------
                AddSchemaCheck(snapshot, area: "Core", objectName: "dbo.CallTicket", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"));
                AddSchemaCheck(snapshot, area: "Core", objectName: "dbo.CallTicketHistory", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory"));
                AddSchemaCheck(snapshot, area: "Core", objectName: "dbo.CallTicketNote", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketNote"));

                AddSchemaCheck(snapshot, area: "Views", objectName: "dbo.vw_Call_TicketList", exists: await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_TicketList"));
                AddSchemaCheck(snapshot, area: "Views", objectName: "dbo.vw_Call_DashboardMetrics", exists: await CallSchemaGate.ViewExistsAsync(connection, "dbo.vw_Call_DashboardMetrics"));

                AddSchemaCheck(snapshot, area: "Email", objectName: "dbo.CallNotificationRules", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallNotificationRules"));
                AddSchemaCheck(snapshot, area: "Email", objectName: "dbo.CallEmailTemplate", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailTemplate"));
                AddSchemaCheck(snapshot, area: "Email", objectName: "dbo.CallEmailLog", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog"));
                AddSchemaCheck(snapshot, area: "Email", objectName: "dbo.CallEmailSettings", exists: await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailSettings"));

                // ---------- Counts & metrics ----------
                var hasCallTicket = snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallTicket" && c.Exists);
                if (hasCallTicket)
                {
                    try
                    {
                        snapshot.PendingTickets = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM dbo.CallTicket WHERE Status = 'Pending';");
                    }
                    catch
                    {
                        snapshot.PendingTickets = 0;
                    }
                }

                var hasMetricsView = snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.vw_Call_DashboardMetrics" && c.Exists);
                if (hasMetricsView)
                {
                    try
                    {
                        const string sql = @"SELECT TOP 1 OpenTickets, CriticalTickets, AvgResolutionMinutes, TodaysVolume FROM dbo.vw_Call_DashboardMetrics;";
                        snapshot.DashboardMetrics = await connection.QuerySingleOrDefaultAsync<CallDashboardMetrics>(sql);
                    }
                    catch
                    {
                        snapshot.DashboardMetrics = null;
                    }
                }

                if (snapshot.DashboardMetrics == null && hasCallTicket)
                {
                    try
                    {
                        var hasSolvedAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "SolvedAt");
                        var avgResolutionExpr = hasSolvedAt
                            ? "AVG(CASE WHEN t.SolvedAt IS NOT NULL THEN DATEDIFF(MINUTE, t.CreatedAt, t.SolvedAt) END)"
                            : "CAST(NULL AS int)";
                        var hasHistoryTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory");
                        // Aggregates cannot contain subqueries (Msg 130), so the
                        // allocation check is a LEFT JOIN, not NOT EXISTS.
                        var allocJoin = hasHistoryTable
                            ? @"LEFT JOIN (
            SELECT DISTINCT h.TicketId
            FROM dbo.CallTicketHistory h
            WHERE h.FieldName = 'ReplacementNewItemId'
              AND TRY_CONVERT(int, h.NewValue) > 0
        ) alloc ON alloc.TicketId = t.TicketId"
                            : string.Empty;
                        var tempOpenGate = hasHistoryTable
                            ? "AND (t.Status <> 'Resolved (Temporary)' OR alloc.TicketId IS NULL)"
                            : "AND t.Status <> 'Resolved (Temporary)'";

                        var sql = $@"
SELECT
    OpenTickets = SUM(CASE WHEN t.Status NOT IN ('Solved') {tempOpenGate} THEN 1 ELSE 0 END),
    CriticalTickets = SUM(CASE WHEN t.Status NOT IN ('Solved') {tempOpenGate} AND t.Priority = 'Critical' THEN 1 ELSE 0 END),
    AvgResolutionMinutes = {avgResolutionExpr},
    TodaysVolume = SUM(CASE WHEN CONVERT(date, t.CreatedAt) = CONVERT(date, SYSUTCDATETIME()) THEN 1 ELSE 0 END)
FROM dbo.CallTicket t
{allocJoin};";

                        snapshot.DashboardMetrics = await connection.QuerySingleOrDefaultAsync<CallDashboardMetrics>(sql);
                    }
                    catch
                    {
                        snapshot.DashboardMetrics = null;
                    }
                }

                // ---------- Last email problems ----------
                var hasEmailLog = snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallEmailLog" && c.Exists);
                if (hasEmailLog)
                {
                    try
                    {
                        const string lastFailedSql = @"
SELECT TOP 1
    EmailLogId,
    TicketId,
    CAST(NULL AS nvarchar(255)) AS Branch,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    DateSent,
    CreatedByUserId
FROM dbo.CallEmailLog
WHERE Status = 'Failed'
ORDER BY DateSent DESC, EmailLogId DESC;";
                        snapshot.LastEmailFailed = await connection.QuerySingleOrDefaultAsync<CallEmailLogItem>(lastFailedSql);
                    }
                    catch
                    {
                        snapshot.LastEmailFailed = null;
                    }

                    try
                    {
                        const string lastSkippedSql = @"
SELECT TOP 1
    EmailLogId,
    TicketId,
    CAST(NULL AS nvarchar(255)) AS Branch,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    DateSent,
    CreatedByUserId
FROM dbo.CallEmailLog
WHERE Status = 'Skipped'
ORDER BY DateSent DESC, EmailLogId DESC;";
                        snapshot.LastEmailSkipped = await connection.QuerySingleOrDefaultAsync<CallEmailLogItem>(lastSkippedSql);
                    }
                    catch
                    {
                        snapshot.LastEmailSkipped = null;
                    }

                    try
                    {
                        const string lastReminderSql = @"
SELECT TOP 1
    EmailLogId,
    TicketId,
    CAST(NULL AS nvarchar(255)) AS Branch,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    DateSent,
    CreatedByUserId
FROM dbo.CallEmailLog
WHERE EmailType = 'Reminder'
ORDER BY DateSent DESC, EmailLogId DESC;";
                        snapshot.LastReminderEmail = await connection.QuerySingleOrDefaultAsync<CallEmailLogItem>(lastReminderSql);
                    }
                    catch
                    {
                        snapshot.LastReminderEmail = null;
                    }

                    try
                    {
                        const string lastEscalationSql = @"
SELECT TOP 1
    EmailLogId,
    TicketId,
    CAST(NULL AS nvarchar(255)) AS Branch,
    EmailType,
    Recipient,
    Subject,
    Status,
    ErrorMessage,
    DateSent,
    CreatedByUserId
FROM dbo.CallEmailLog
WHERE EmailType = 'Escalation'
ORDER BY DateSent DESC, EmailLogId DESC;";
                        snapshot.LastEscalationEmail = await connection.QuerySingleOrDefaultAsync<CallEmailLogItem>(lastEscalationSql);
                    }
                    catch
                    {
                        snapshot.LastEscalationEmail = null;
                    }

                    try
                    {
                        const string schedulerSignalSql = @"
SELECT
    MAX(CASE WHEN EmailType IN ('Reminder', 'Escalation') THEN DateSent END) AS LastActivityUtc,
    SUM(CASE WHEN EmailType IN ('Reminder', 'Escalation') AND DateSent >= DATEADD(HOUR, -24, SYSUTCDATETIME()) THEN 1 ELSE 0 END) AS ActivityCountLast24Hours
FROM dbo.CallEmailLog;";
                        var signal = await connection.QuerySingleOrDefaultAsync<ItcmSchedulerSignalRow>(schedulerSignalSql);
                        snapshot.SchedulerLastActivityUtc = signal?.LastActivityUtc;
                        snapshot.SchedulerSignalCountLast24Hours = signal?.ActivityCountLast24Hours ?? 0;
                    }
                    catch
                    {
                        snapshot.SchedulerLastActivityUtc = null;
                        snapshot.SchedulerSignalCountLast24Hours = 0;
                    }
                }

                // ---------- Last auto-escalation activity ----------
                var hasHistory = snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallTicketHistory" && c.Exists);
                if (hasHistory)
                {
                    try
                    {
                        const string lastAutoEscalationSql = @"
SELECT TOP 1 ChangedAt
FROM dbo.CallTicketHistory
WHERE FieldName = 'Status'
  AND NewValue = 'Escalated'
  AND Note LIKE 'Auto-escalated%'
ORDER BY ChangedAt DESC, HistoryId DESC;";

                        snapshot.LastAutoEscalationUtc = await connection.ExecuteScalarAsync<DateTime?>(lastAutoEscalationSql);
                    }
                    catch
                    {
                        snapshot.LastAutoEscalationUtc = null;
                    }

                    try
                    {
                        const string recentAutoEscalationCountSql = @"
SELECT COUNT(1)
FROM dbo.CallTicketHistory
WHERE FieldName = 'Status'
  AND NewValue = 'Escalated'
  AND Note LIKE 'Auto-escalated%'
  AND ChangedAt >= DATEADD(HOUR, -24, SYSUTCDATETIME());";
                        snapshot.SchedulerSignalCountLast24Hours += await connection.ExecuteScalarAsync<int>(recentAutoEscalationCountSql);
                    }
                    catch
                    {
                    }

                    if (snapshot.LastAutoEscalationUtc.HasValue
                        && (!snapshot.SchedulerLastActivityUtc.HasValue || snapshot.LastAutoEscalationUtc.Value > snapshot.SchedulerLastActivityUtc.Value))
                    {
                        snapshot.SchedulerLastActivityUtc = snapshot.LastAutoEscalationUtc.Value;
                    }
                }

                // Add small details for missing items (helps non-dev admins).
                foreach (var check in snapshot.SchemaChecks.Where(c => !c.Exists))
                {
                    if (check.Area == "Email" && check.ObjectName == "dbo.CallEmailSettings")
                        check.Detail = "Email sending may be disabled until SMTP is configured.";
                    else if (check.Area == "Email" && check.ObjectName == "dbo.CallEmailTemplate")
                        check.Detail = "Templates missing/inactive will cause notifications to be skipped.";
                    else if (check.Area == "Email" && check.ObjectName == "dbo.CallEmailLog")
                        check.Detail = "Email log UI and diagnostics will be limited.";
                    else if (check.Area == "Views" && check.ObjectName == "dbo.vw_Call_DashboardMetrics")
                        check.Detail = "Dashboard metrics may be unavailable (fallback will be used where possible).";
                }

                // ---------- Background lock probe ----------
                try
                {
                    snapshot.BackgroundLockProbe = await ProbeItcmBackgroundLockAsync(connection);
                    snapshot.SchedulerIsRunning = snapshot.BackgroundLockProbe?.IsFree.HasValue == true
                        ? !snapshot.BackgroundLockProbe.IsFree.Value
                        : (bool?)null;
                }
                catch
                {
                    snapshot.BackgroundLockProbe = new ItcmBackgroundLockProbe
                    {
                        LockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs",
                        IsFree = null,
                        Error = "Probe failed."
                    };
                    snapshot.SchedulerIsRunning = null;
                }

                snapshot.SchedulerHeartbeat = await ProbeSchedulerHeartbeatAsync(connection);
                // Use heartbeat as the source of truth; treat scheduler as running via remote server
                snapshot.SchedulerTaskState = snapshot.SchedulerHeartbeat?.TableExists == true
                    ? (snapshot.SchedulerHeartbeat.LastStartedUtc.HasValue ? "Remote (active)" : "Remote (idle)")
                    : "Remote (unknown)";
                snapshot.SchedulerTaskEnabled = true;
            }

            snapshot.VersionInfo = GetDiagnosticsVersionInfo(snapshot);
            snapshot.HealthChecks = BuildHealthChecks(snapshot);
            return snapshot;
        }

        private static void AddSchemaCheck(ItcmDiagnosticsSnapshot snapshot, string area, string objectName, bool exists)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            snapshot.SchemaChecks.Add(new ItcmSchemaCheckResult
            {
                Area = area,
                ObjectName = objectName,
                Exists = exists,
                Detail = GetNonTechnicalSchemaExplanation(objectName),
                Severity = GetSchemaSeverity(area, objectName, exists),
                SuggestedFix = GetSchemaSuggestedFix(objectName)
            });
        }

        private static string GetSchemaSeverity(string area, string objectName, bool exists)
        {
            if (exists)
                return "Healthy";

            if (string.Equals(objectName, "dbo.CallTicket", StringComparison.OrdinalIgnoreCase))
                return "Critical";

            if (string.Equals(area, "Core", StringComparison.OrdinalIgnoreCase))
                return "Critical";

            if (string.Equals(area, "Email", StringComparison.OrdinalIgnoreCase))
                return "Warning";

            return "Warning";
        }

        private static string GetSchemaSuggestedFix(string objectName)
        {
            switch ((objectName ?? string.Empty).Trim())
            {
                case "dbo.CallTicket":
                case "dbo.CallTicketHistory":
                case "dbo.CallTicketNote":
                    return "Run or verify the IT Call Monitoring core database migration script.";
                case "dbo.vw_Call_TicketList":
                case "dbo.vw_Call_DashboardMetrics":
                    return "Recreate the IT Call Monitoring database views or rerun the latest view migration.";
                case "dbo.CallNotificationRules":
                    return "Open Email Notifications and save the notification rules, or run the email migration script.";
                case "dbo.CallEmailTemplate":
                    return "Open Email Notifications > Templates and save the required templates.";
                case "dbo.CallEmailLog":
                    return "Run the email logging migration so diagnostics can track sent, skipped, and failed emails.";
                case "dbo.CallEmailSettings":
                    return "Open Email Notifications > SMTP settings and save a valid sender profile.";
                default:
                    return "Review database setup for this object.";
            }
        }

        private static ItcmDiagnosticsVersionInfo GetDiagnosticsVersionInfo(ItcmDiagnosticsSnapshot snapshot)
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            return new ItcmDiagnosticsVersionInfo
            {
                DesktopVersion = assembly.Version == null ? "Unknown" : assembly.Version.ToString(),
                SchedulerVersion = snapshot?.SchedulerHeartbeat != null && !string.IsNullOrWhiteSpace(snapshot.SchedulerHeartbeat.Version)
                    ? snapshot.SchedulerHeartbeat.Version
                    : "Unknown",
                DatabaseMigrationVersion = snapshot != null && snapshot.SchemaChecks.All(c => c.Exists)
                    ? "Schema complete"
                    : "Schema partial"
            };
        }

        private static List<ItcmDiagnosticCheckResult> BuildHealthChecks(ItcmDiagnosticsSnapshot snapshot)
        {
            var checks = new List<ItcmDiagnosticCheckResult>();
            if (snapshot == null)
                return checks;

            foreach (var schema in snapshot.SchemaChecks)
            {
                checks.Add(new ItcmDiagnosticCheckResult
                {
                    Area = schema.Area,
                    Name = schema.ObjectName,
                    Severity = schema.Severity,
                    Message = schema.Exists ? "Available" : (schema.Detail ?? "Missing"),
                    SuggestedFix = schema.Exists ? string.Empty : schema.SuggestedFix
                });
            }

            checks.Add(new ItcmDiagnosticCheckResult
            {
                Area = "Scheduler",
                Name = "ITCM Server (central)",
                Severity = snapshot.SchedulerHeartbeat?.TableExists == true ? "Healthy" : "Warning",
                Message = snapshot.SchedulerHeartbeat?.TableExists == true
                    ? (snapshot.SchedulerHeartbeat.LastStartedUtc.HasValue ? "Active" : "Installed (no runs yet)")
                    : "Heartbeat table not detected",
                SuggestedFix = "Deploy Yakult.ITCM.Server and run the scheduler heartbeat migration script."
            });

            var recentFailure = snapshot.LastEmailFailed?.DateSent;
            var hasRecentFailure = recentFailure.HasValue && (DateTime.UtcNow - DateTime.SpecifyKind(recentFailure.Value, DateTimeKind.Utc)).TotalHours <= 24;
            checks.Add(new ItcmDiagnosticCheckResult
            {
                Area = "Email",
                Name = "Recent failures",
                Severity = hasRecentFailure ? "Warning" : "Healthy",
                Message = hasRecentFailure ? "Failed email detected in the last 24 hours" : "No recent failed email detected",
                SuggestedFix = hasRecentFailure ? "Open Email Log and SMTP Test; verify SMTP profile, templates, and recipient rules." : string.Empty
            });

            checks.Add(new ItcmDiagnosticCheckResult
            {
                Area = "Background",
                Name = "Processing lock",
                Severity = snapshot.BackgroundLockProbe?.IsFree == null ? "Warning" : "Healthy",
                Message = snapshot.BackgroundLockProbe?.IsFree == true ? "Free" : snapshot.BackgroundLockProbe?.IsFree == false ? "Locked by active worker" : "Unknown",
                SuggestedFix = snapshot.BackgroundLockProbe?.IsFree == null ? "Check SQL permissions for sp_getapplock and database connectivity." : string.Empty
            });

            return checks;
        }

        private static async Task<ItcmSchedulerHeartbeat> ProbeSchedulerHeartbeatAsync(SqlConnection connection)
        {
            var heartbeat = new ItcmSchedulerHeartbeat();
            try
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallSchedulerHeartbeat"))
                    return heartbeat;

                heartbeat.TableExists = true;
                // Server table has physical columns (StartedAtUtc, FinishedAtUtc,
                // Succeeded, ErrorMessage, MachineName, Version). Derive the
                // display fields with aggregates instead of selecting
                // LastSuccessUtc/LastError, which do not exist as columns.
                const string sql = @"
SELECT TOP 1
    StartedAtUtc AS LastStartedUtc,
    FinishedAtUtc AS LastFinishedUtc,
    (SELECT MAX(FinishedAtUtc) FROM dbo.CallSchedulerHeartbeat WHERE Succeeded = 1) AS LastSuccessUtc,
    (SELECT TOP 1 ErrorMessage FROM dbo.CallSchedulerHeartbeat WHERE Succeeded = 0 ORDER BY HeartbeatId DESC) AS LastError,
    MachineName,
    Version
FROM dbo.CallSchedulerHeartbeat
ORDER BY HeartbeatId DESC;";
                var row = await connection.QuerySingleOrDefaultAsync<ItcmSchedulerHeartbeat>(sql);
                if (row != null)
                {
                    row.TableExists = true;
                    return row;
                }
            }
            catch (Exception ex)
            {
                heartbeat.LastError = ex.Message;
            }
            return heartbeat;
        }

        public async Task<List<CallClientPresenceItem>> GetConnectedClientsAsync(int onlineMinutes = 15)
        {
            var empty = new List<CallClientPresenceItem>();
            try
            {
                if (onlineMinutes <= 0) onlineMinutes = 15;
                if (onlineMinutes > 1440) onlineMinutes = 1440;

                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    await connection.OpenAsync();
                    if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallClientPresence"))
                        return empty;

                    const string sql = @"
SELECT MachineName, UserName, LastSeenUtc, FirstSeenUtc, ClientVersion, Module,
       CASE WHEN LastSeenUtc >= DATEADD(minute, -@OnlineMinutes, SYSUTCDATETIME()) THEN 1 ELSE 0 END AS Online
FROM dbo.CallClientPresence
ORDER BY Online DESC, LastSeenUtc DESC;";
                    var rows = await connection.QueryAsync<CallClientPresenceItem>(sql, new { OnlineMinutes = onlineMinutes });
                    return (rows ?? Enumerable.Empty<CallClientPresenceItem>()).ToList();
                }
            }
            catch
            {
                return empty;
            }
        }

        public async Task<List<ItcmDiagnosticsTrendPoint>> LoadSnapshotTrendsAsync(ItcmDiagnosticsSnapshot snapshot)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();
                return await LoadDiagnosticsTrendsAsync(connection, snapshot);
            }
        }

        private static async Task<List<ItcmDiagnosticsTrendPoint>> LoadDiagnosticsTrendsAsync(SqlConnection connection, ItcmDiagnosticsSnapshot snapshot)
        {
            var trends = Enumerable.Range(0, 14)
                .Select(i => DateTime.UtcNow.Date.AddDays(-13 + i))
                .Select(d => new ItcmDiagnosticsTrendPoint { Date = d })
                .ToList();

            try
            {
                if (snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallEmailLog" && c.Exists))
                {
                    const string emailSql = @"
SELECT
    CONVERT(date, DateSent) AS [Date],
    SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) AS FailedEmails,
    SUM(CASE WHEN EmailType IN ('Reminder','Escalation') THEN 1 ELSE 0 END) AS SchedulerSignals
FROM dbo.CallEmailLog
WHERE DateSent >= DATEADD(day, -13, CONVERT(date, SYSUTCDATETIME()))
GROUP BY CONVERT(date, DateSent);";
                    var rows = await connection.QueryAsync<ItcmDiagnosticsTrendPoint>(emailSql);
                    foreach (var row in rows)
                    {
                        var target = trends.FirstOrDefault(t => t.Date.Date == row.Date.Date);
                        if (target == null) continue;
                        target.FailedEmails = row.FailedEmails;
                        target.SchedulerSignals = row.SchedulerSignals;
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallTicketHistory" && c.Exists))
                {
                    const string escalationSql = @"
SELECT CONVERT(date, ChangedAt) AS [Date], COUNT(1) AS Escalations
FROM dbo.CallTicketHistory
WHERE FieldName = 'Status'
  AND NewValue = 'Escalated'
  AND ChangedAt >= DATEADD(day, -13, CONVERT(date, SYSUTCDATETIME()))
GROUP BY CONVERT(date, ChangedAt);";
                    var rows = await connection.QueryAsync<ItcmDiagnosticsTrendPoint>(escalationSql);
                    foreach (var row in rows)
                    {
                        var target = trends.FirstOrDefault(t => t.Date.Date == row.Date.Date);
                        if (target != null) target.Escalations = row.Escalations;
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (snapshot.SchemaChecks.Any(c => c.ObjectName == "dbo.CallTicket" && c.Exists))
                {
                    var hasSolvedAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "SolvedAt");
                    var avgResolutionExpr = hasSolvedAt
                        ? "AVG(CASE WHEN SolvedAt IS NOT NULL THEN DATEDIFF(MINUTE, CreatedAt, SolvedAt) END)"
                        : "CAST(NULL AS int)";
                    var ticketSql = $@"
SELECT
    CONVERT(date, CreatedAt) AS [Date],
    COUNT(1) AS TicketVolume,
    {avgResolutionExpr} AS AvgResolutionMinutes
FROM dbo.CallTicket
WHERE CreatedAt >= DATEADD(day, -13, CONVERT(date, SYSUTCDATETIME()))
GROUP BY CONVERT(date, CreatedAt);";
                    var rows = await connection.QueryAsync<ItcmDiagnosticsTrendPoint>(ticketSql);
                    foreach (var row in rows)
                    {
                        var target = trends.FirstOrDefault(t => t.Date.Date == row.Date.Date);
                        if (target == null) continue;
                        target.TicketVolume = row.TicketVolume;
                        target.AvgResolutionMinutes = row.AvgResolutionMinutes;
                    }
                }
            }
            catch
            {
            }

            return trends;
        }

        public async Task<int?> CreateSyntheticTestTicketAsync(int? createdByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    throw new InvalidOperationException("CallTicket table does not exist. Cannot create test ticket.");

                const string sql = @"
INSERT dbo.CallTicket
(
    CallerName,
    Issue,
    IssueType,
    Priority,
    Status,
    ComId,
    DeptId,
    BranchId,
    CreatedByUserId,
    CreatedAt
)
VALUES
(
    'ITCM Diagnostic Test',
    '[TEST] Synthetic ticket created from ITCM diagnostics. Safe to delete.',
    'Test',
    'Low',
    'Pending',
    NULL,
    NULL,
    NULL,
    @CreatedByUserId,
    SYSUTCDATETIME()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var ticketId = await connection.ExecuteScalarAsync<int?>(sql, new { CreatedByUserId = createdByUserId });
                return ticketId;
            }
        }

        public async Task<int> CleanupSyntheticTestTicketsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return 0;

                const string deleteHistorySql = @"
DELETE h
FROM dbo.CallTicketHistory h
INNER JOIN dbo.CallTicket t ON t.TicketId = h.TicketId
WHERE t.Issue LIKE '%[TEST]%'
   OR t.CallerName = 'ITCM Diagnostic Test';";

                const string deleteNotesSql = @"
DELETE n
FROM dbo.CallTicketNote n
INNER JOIN dbo.CallTicket t ON t.TicketId = n.TicketId
WHERE t.Issue LIKE '%[TEST]%'
   OR t.CallerName = 'ITCM Diagnostic Test';";

                const string deleteTicketsSql = @"
DELETE FROM dbo.CallTicket
WHERE Issue LIKE '%[TEST]%'
   OR CallerName = 'ITCM Diagnostic Test';";

                await connection.ExecuteAsync(deleteHistorySql);
                await connection.ExecuteAsync(deleteNotesSql);
                var deleted = await connection.ExecuteAsync(deleteTicketsSql);
                return deleted;
            }
        }

        public async Task<bool> AnySyntheticTestTicketsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.OpenAsync();

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return false;

                const string sql = @"
SELECT COUNT(1)
FROM dbo.CallTicket
WHERE Issue LIKE '%[TEST]%'
   OR CallerName = 'ITCM Diagnostic Test';";

                var count = await connection.ExecuteScalarAsync<int>(sql);
                return count > 0;
            }
        }

        private static string GetNonTechnicalSchemaExplanation(string objectName)
        {
            switch ((objectName ?? string.Empty).Trim())
            {
                case "dbo.CallTicket":
                    return "Stores IT call tickets; the system can't create/track tickets without it.";
                case "dbo.CallTicketHistory":
                    return "Keeps a history of ticket changes; the audit trail may be missing without it.";
                case "dbo.CallTicketNote":
                    return "Stores notes/comments on tickets; notes may not save or show without it.";
                case "dbo.vw_Call_TicketList":
                    return "Feeds the ticket list screen; the list may be empty or incomplete without it.";
                case "dbo.vw_Call_DashboardMetrics":
                    return "Feeds dashboard totals/averages; dashboard numbers may be blank or wrong without it.";
                case "dbo.CallNotificationRules":
                    return "Controls reminder/escalation rules; notifications may not follow your settings without it.";
                case "dbo.CallEmailTemplate":
                    return "Stores email templates; notification emails may fail or look incorrect without it.";
                case "dbo.CallEmailLog":
                    return "Records sent/failed emails; you won't see email history without it.";
                case "dbo.CallEmailSettings":
                    return "Stores SMTP settings; the system can't send email notifications without it.";
                default:
                    return string.Empty;
            }
        }
    }
}