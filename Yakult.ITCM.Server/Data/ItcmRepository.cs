using Microsoft.Data.SqlClient;
using Yakult.ITCM.Server.Models;

namespace Yakult.ITCM.Server.Data;

public sealed class ItcmRepository : IItcmRepository
{
    private const string GlobalLockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs";
    private string _connectionString;
    private readonly object _connectionStringLock = new();
    private bool _disposed;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> PresenceThrottle = new();
    private static readonly TimeSpan PresenceThrottleWindow = TimeSpan.FromSeconds(10);

    public string ConnectionString => _connectionString;

    public ItcmRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Yakult_Inventory_System")
            ?? throw new InvalidOperationException("Connection string 'Yakult_Inventory_System' is not configured.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    // ─── App Lock ─────────────────────────────────────────────────────────────────

    public async Task<bool> AcquireGlobalLockAsync(SqlConnection connection, int timeoutMs = 0)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
DECLARE @res int;
EXEC @res = sp_getapplock
    @Resource = @Resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = @TimeoutMs;
SELECT @res;";
        cmd.Parameters.AddWithValue("@Resource", GlobalLockName);
        cmd.Parameters.AddWithValue("@TimeoutMs", timeoutMs);
        var result = await cmd.ExecuteScalarAsync();
        var code = result is int i ? i : Convert.ToInt32(result);
        return code >= 0;
    }

    public async Task ReleaseGlobalLockAsync(SqlConnection connection)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';";
            cmd.Parameters.AddWithValue("@Resource", GlobalLockName);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
        }
    }

    // ─── Notification Rules ────────────────────────────────────────────────────────

    public async Task<CallNotificationRulesItem?> GetNotificationRulesAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallNotificationRules"))
            return null;

        const string sql = @"
SELECT TOP 1
    RulesId, NotifyOnNewTicket, NotifyOnStatusChange, NotifyOnEscalation,
    NotifyOnReminder, GroupEmail, EscalationEmail, ReminderDays,
    UpdatedAt, UpdatedByUserId
FROM dbo.CallNotificationRules
ORDER BY RulesId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CallNotificationRulesItem
        {
            RulesId = reader.GetInt32(0),
            NotifyOnNewTicket = reader.GetBoolean(1),
            NotifyOnStatusChange = reader.GetBoolean(2),
            NotifyOnEscalation = reader.GetBoolean(3),
            NotifyOnReminder = reader.GetBoolean(4),
            GroupEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
            EscalationEmail = reader.IsDBNull(6) ? null : reader.GetString(6),
            ReminderDays = reader.GetInt32(7),
            UpdatedAt = reader.GetDateTime(8),
            UpdatedByUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
        };
    }

    public async Task SaveNotificationRulesAsync(CallNotificationRulesItem rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallNotificationRules"))
            throw new InvalidOperationException("CallNotificationRules table is not installed in this database yet.");

        // Append-only history, matching the desktop workspace: newest row wins.
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

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@NotifyOnNewTicket", rules.NotifyOnNewTicket);
        cmd.Parameters.AddWithValue("@NotifyOnStatusChange", rules.NotifyOnStatusChange);
        cmd.Parameters.AddWithValue("@NotifyOnEscalation", rules.NotifyOnEscalation);
        cmd.Parameters.AddWithValue("@NotifyOnReminder", rules.NotifyOnReminder);
        cmd.Parameters.AddWithValue("@GroupEmail", (object?)rules.GroupEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EscalationEmail", (object?)rules.EscalationEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReminderDays", rules.ReminderDays);
        cmd.Parameters.AddWithValue("@UpdatedByUserId", (object?)rules.UpdatedByUserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Email Templates ───────────────────────────────────────────────────────────

    public async Task<CallEmailTemplateItem?> GetEmailTemplateByTypeAsync(string templateType)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailTemplate"))
            return null;

        const string sql = @"
SELECT TOP 1 TemplateId, TemplateType, Subject, Body, IsActive, UpdatedAt, UpdatedByUserId
FROM dbo.CallEmailTemplate
WHERE TemplateType = @TemplateType
ORDER BY TemplateId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TemplateType", templateType.Trim());
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CallEmailTemplateItem
        {
            TemplateId = reader.GetInt32(0),
            TemplateType = reader.GetString(1),
            Subject = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Body = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            UpdatedAt = reader.GetDateTime(5),
            UpdatedByUserId = reader.IsDBNull(6) ? null : reader.GetInt32(6)
        };
    }

    // ─── Ticket Notification Data ──────────────────────────────────────────────────

    public async Task<CallTicketNotificationData?> GetTicketNotificationDataAsync(int ticketId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallTicket"))
            return null;

        var hasBranchId = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "BranchId");
        var hasBranchTable = await SchemaGate.TableExistsAsync(conn, "dbo.Branch");
        var hasLastContactAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastContactAt");
        var hasTicketSource = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "TicketSource");
        var hasContactEmail = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "ContactEmail");
        var hasLastReminderSentAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastReminderSentAt");

        var sql = @"
SELECT TOP 1
    t.TicketId, t.TicketCode, t.ComId, c.Name AS Company,
    t.DeptId, d.Name AS Department," +
    (hasBranchId ? "\n    t.BranchId," : "\n    CAST(NULL AS int) AS BranchId,") +
    (hasBranchId && hasBranchTable ? @"
    CASE
        WHEN b.BranchId IS NULL THEN NULL
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch," : "\n    CAST(NULL AS nvarchar(300)) AS Branch,") + @"
    t.CallerName, t.Issue, t.ProvidedSolution, t.IssueType,
    t.Priority, t.Status," +
    (hasTicketSource ? "\n    t.TicketSource," : "\n    CAST(NULL AS nvarchar(50)) AS TicketSource,") +
    (hasContactEmail ? "\n    t.ContactEmail," : "\n    CAST(NULL AS nvarchar(255)) AS ContactEmail,") + @"
    t.AssignedToEmpId, e.Name AS AssignedTo,
    t.CreatedAt, t.UpdatedAt," +
    (hasLastContactAt ? "\n    t.LastContactAt," : "\n    CAST(NULL AS datetime2(2)) AS LastContactAt,") +
    (hasLastReminderSentAt ? "\n    t.LastReminderSentAt," : "\n    CAST(NULL AS datetime2(2)) AS LastReminderSentAt,") + @"
    t.SolvedAt
FROM dbo.CallTicket t
LEFT JOIN dbo.Company c ON c.ComId = t.ComId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee e ON e.EmpId = t.AssignedToEmpId" +
    (hasBranchId && hasBranchTable ? "\nLEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId" : "") + @"
WHERE t.TicketId = @TicketId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return MapTicketNotificationData(reader, hasTicketSource, hasContactEmail, hasLastContactAt, hasLastReminderSentAt);
    }

    private static CallTicketNotificationData MapTicketNotificationData(
        SqlDataReader r,
        bool hasTicketSource,
        bool hasContactEmail,
        bool hasLastContactAt,
        bool hasLastReminderSentAt)
    {
        return new CallTicketNotificationData
        {

            TicketId = r.GetInt32(0),
            TicketCode = r.IsDBNull(1) ? null : r.GetString(1),
            ComId = r.IsDBNull(2) ? null : r.GetInt32(2),
            Company = r.IsDBNull(3) ? null : r.GetString(3),
            DeptId = r.IsDBNull(4) ? null : r.GetInt32(4),
            Department = r.IsDBNull(5) ? null : r.GetString(5),
            BranchId = r.IsDBNull(6) ? null : r.GetInt32(6),
            Branch = r.IsDBNull(7) ? null : r.GetString(7),
            CallerName = r.IsDBNull(8) ? null : r.GetString(8),
            Issue = r.IsDBNull(9) ? null : r.GetString(9),
            ProvidedSolution = r.IsDBNull(10) ? null : r.GetString(10),
            IssueType = r.IsDBNull(11) ? null : r.GetString(11),
            Priority = r.IsDBNull(12) ? null : r.GetString(12),
            Status = r.IsDBNull(13) ? null : r.GetString(13),
            TicketSource = hasTicketSource && !r.IsDBNull(14) ? r.GetString(14) : null,
            ContactEmail = hasContactEmail && !r.IsDBNull(15) ? r.GetString(15) : null,
            AssignedToEmpId = r.IsDBNull(16) ? null : r.GetInt32(16),
            AssignedTo = r.IsDBNull(17) ? null : r.GetString(17),
            CreatedAt = r.GetDateTime(18),
            UpdatedAt = r.GetDateTime(19),
            LastContactAt = hasLastContactAt && !r.IsDBNull(20) ? r.GetDateTime(20) : null,
            LastReminderSentAt = hasLastReminderSentAt && !r.IsDBNull(21) ? r.GetDateTime(21) : null,
            SolvedAt = r.IsDBNull(22) ? null : r.GetDateTime(22)
        };
    }

    public async Task<string?> GetEmployeePrimaryEmailAsync(int empId)
    {
        if (empId <= 0) return null;

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.EmpId = @EmpId
  AND ee.IsPrimary = 1
  AND ee.IsActive = 1
  AND ea.IsActive = 1
ORDER BY ee.EmployeeEmailId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", empId);
        var result = await cmd.ExecuteScalarAsync();
        return result is null || result == DBNull.Value ? null : result.ToString()?.Trim();
    }

    // ─── Reminder Queries ──────────────────────────────────────────────────────────

    public async Task<List<int>> GetTicketIdsNeedingReminderAsync(int reminderDays, int maxRows = 25)
    {
        reminderDays = Math.Clamp(reminderDays, 1, 60);
        maxRows = Math.Clamp(maxRows, 1, 500);

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallTicket"))
            return [];

        var hasLastContactAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastContactAt");
        var hasLastReminderSentAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastReminderSentAt");
        if (!hasLastContactAt || !hasLastReminderSentAt)
            return [];

        // Replacement-aware temporaries: temp tickets WITH issued parts stay
        // parked (awaiting return); Service-Only temporaries (no allocation
        // rows) remain watchable under the normal reminder thresholds.
        var hasHistory = await SchemaGate.TableExistsAsync(conn, "dbo.CallTicketHistory");
        var tempClause = hasHistory
            ? @"AND (
    t.Status <> 'Resolved (Temporary)'
    OR NOT EXISTS (
        SELECT 1 FROM dbo.CallTicketHistory h
        WHERE h.TicketId = t.TicketId
          AND h.FieldName = 'ReplacementNewItemId'
          AND TRY_CONVERT(int, h.NewValue) > 0
    )
  )"
            : "AND t.Status <> 'Resolved (Temporary)'";

        var sql = $@"
SELECT TOP (@MaxRows) t.TicketId
FROM dbo.CallTicket t
WHERE t.Status NOT IN ('Solved', 'Closed')
  {tempClause}
  AND t.LastContactAt IS NOT NULL
  AND DATEDIFF(DAY, t.LastContactAt, SYSUTCDATETIME()) >= @ReminderDays
  AND (
        t.LastReminderSentAt IS NULL
     OR DATEDIFF(DAY, t.LastReminderSentAt, SYSUTCDATETIME()) >= @ReminderDays
  )
ORDER BY t.UpdatedAt ASC, t.TicketId ASC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@MaxRows", maxRows);
        cmd.Parameters.AddWithValue("@ReminderDays", reminderDays);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<int>();
        while (await reader.ReadAsync())
            results.Add(reader.GetInt32(0));
        return results;
    }

    public async Task MarkReminderSentAsync(int ticketId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastReminderSentAt"))
            return;

        await using var cmd = new SqlCommand("UPDATE dbo.CallTicket SET LastReminderSentAt = SYSUTCDATETIME() WHERE TicketId = @TicketId;", conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Auto-Escalation Queries ───────────────────────────────────────────────────

    // Kept as a compatibility entry point for existing callers. All scheduler
    // escalation queries must enforce the Portal ownership invariant.
    public Task<List<int>> GetTicketIdsNeedingAutoEscalationAsync(int maxRows = 25) =>
        GetTicketIdsNeedingPortalSafeAutoEscalationAsync(maxRows);

    public async Task<CallEscalationSettingsItem?> GetEscalationSettingsAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        try
        {
            if (await SchemaGate.TableExistsAsync(conn, "dbo.CallEscalationSettings"))
            {
                await using var cmd = new SqlCommand("dbo.sp_Call_GetEscalationSettings", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new CallEscalationSettingsItem
                    {
                        DaysToSupervisor = reader.GetInt32(0),
                        DaysToManager = reader.GetInt32(1),
                        SupervisorPosition = reader.IsDBNull(2) ? "IT Supervisor" : reader.GetString(2),
                        ManagerPosition = reader.IsDBNull(3) ? "IT Manager" : reader.GetString(3)
                    };
                }
            }
        }
        catch
        {
        }

        return new CallEscalationSettingsItem
        {
            DaysToSupervisor = 2,
            DaysToManager = 3
        };
    }

    // ─── Ticket Workflow ───────────────────────────────────────────────────────────

    public async Task SetTicketStatusAsync(int ticketId, string newStatus, int? changedByUserId, string? note = null)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("dbo.sp_Call_SetTicketStatus", conn)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        cmd.Parameters.AddWithValue("@NewStatus", newStatus);
        cmd.Parameters.AddWithValue("@ChangedByUserId", (object?)changedByUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Note", (object?)note ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AssignTicketEmployeeAsync(int ticketId, int? assignedToEmpId, int? changedByUserId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("dbo.sp_Call_AssignTicket", conn)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        cmd.Parameters.AddWithValue("@AssignedToEmpId", (object?)assignedToEmpId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ChangedByUserId", (object?)changedByUserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // Same active-IT rule as the legacy API (CallTicketApiSecurity.IsItEmployee):
    // active employee whose department looks like IT.
    public async Task<bool> IsItEmployeeAsync(int empId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        const string sql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.Employee e
    LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
    WHERE e.EmpId = @EmployeeId
      AND ISNULL(e.Active, 1) = 1
      AND (
          UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE 'IT%'
          OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%INFORMATION TECHNOLOGY%'
          OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%I.T.%'
      )
) THEN 1 ELSE 0 END;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmployeeId", empId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    public async Task<List<ItEmployeeItem>> GetItEmployeesAsync()
    {
        var items = new List<ItEmployeeItem>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.Employee"))
            return items;

        const string sql = @"
SELECT e.EmpId, e.Name
FROM dbo.Employee e
LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
WHERE ISNULL(e.Active, 1) = 1
  AND (
      UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE 'IT%'
      OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%INFORMATION TECHNOLOGY%'
      OR UPPER(LTRIM(RTRIM(ISNULL(d.Name, '')))) LIKE '%I.T.%'
  )
ORDER BY e.Name;";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ItEmployeeItem
            {
                EmpId = reader["EmpId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EmpId"]),
                Name = reader["Name"] == DBNull.Value ? string.Empty : reader["Name"].ToString() ?? string.Empty
            });
        }
        return items;
    }

    public async Task<int?> GetTicketIdByCodeAsync(string ticketCode)
    {
        if (string.IsNullOrWhiteSpace(ticketCode))
            return null;
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallTicket"))
                return null;
            await using var cmd = new SqlCommand(
                "SELECT TOP 1 TicketId FROM dbo.CallTicket WHERE TicketCode = @Code;", conn);
            cmd.Parameters.AddWithValue("@Code", ticketCode.Trim());
            var result = await cmd.ExecuteScalarAsync();
            return result is int i ? i : (int?)(long?)result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<int?> GetAutoEscalationAssigneeEmpIdAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallAutoEscalationAssigneeSettings"))
            return null;

        try
        {
            const string sql = "SELECT TOP 1 AssigneeEmpId FROM dbo.CallAutoEscalationAssigneeSettings ORDER BY SettingsId DESC;";
            await using var cmd = new SqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return result is int i ? i : (int?)(long?)result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> EmployeeAssignmentEnabledAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "AssignedToEmpId");
    }

    // ─── Notification Recipients ───────────────────────────────────────────────────

    public async Task<bool> BranchNotificationRecipientsSchemaExistsAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await SchemaGate.TableExistsAsync(conn, "dbo.CallBranchNotificationRecipient");
    }

    public async Task<bool> DepartmentNotificationRecipientsSchemaExistsAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await SchemaGate.TableExistsAsync(conn, "dbo.CallDepartmentNotificationRecipient");
    }

    public async Task<CallBranchNotificationRecipientItem?> GetBranchNotificationRecipientAsync(int branchId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallBranchNotificationRecipient"))
            return null;

        const string sql = @"
SELECT TOP 1 BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
FROM dbo.CallBranchNotificationRecipient
WHERE BranchId = @BranchId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BranchId", branchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CallBranchNotificationRecipientItem
        {
            BranchId = reader.GetInt32(0),
            RecipientEmails = reader.IsDBNull(1) ? null : reader.GetString(1),
            EscalationEmails = reader.IsDBNull(2) ? null : reader.GetString(2),
            IsActive = reader.GetBoolean(3),
            UpdatedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            UpdatedByUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
        };
    }

    public async Task<CallDepartmentNotificationRecipientItem?> GetDepartmentNotificationRecipientAsync(int deptId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallDepartmentNotificationRecipient"))
            return null;

        const string sql = @"
SELECT TOP 1 DeptId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId
FROM dbo.CallDepartmentNotificationRecipient
WHERE DeptId = @DeptId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@DeptId", deptId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CallDepartmentNotificationRecipientItem
        {
            DeptId = reader.GetInt32(0),
            RecipientEmails = reader.IsDBNull(1) ? null : reader.GetString(1),
            EscalationEmails = reader.IsDBNull(2) ? null : reader.GetString(2),
            IsActive = reader.GetBoolean(3),
            UpdatedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            UpdatedByUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5)
        };
    }

    public async Task<string?> GetBranchEmailAsync(int branchId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.ColumnExistsAsync(conn, "dbo.Branch", "EmailId"))
            return null;
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.EmailAddress"))
            return null;

        const string sql = @"
SELECT TOP 1 ea.EmailAddress
FROM dbo.Branch b
LEFT JOIN dbo.EmailAddress ea ON ea.EmailId = b.EmailId
WHERE b.BranchId = @BranchId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BranchId", branchId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    // ─── Email Log ─────────────────────────────────────────────────────────────────

    public async Task LogEmailAsync(int? ticketId, string emailType, string recipient, string? subject, string status, string? errorMessage, int? createdByUserId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailLog"))
            return;

        const string sql = @"
INSERT dbo.CallEmailLog (TicketId, EmailType, Recipient, Subject, Status, ErrorMessage, CreatedByUserId)
VALUES (@TicketId, @EmailType, @Recipient, @Subject, @Status, @ErrorMessage, @CreatedByUserId);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", (object?)ticketId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EmailType", Truncate((emailType ?? string.Empty).Trim(), 30));
        cmd.Parameters.AddWithValue("@Recipient", Truncate((recipient ?? string.Empty).Trim(), 255));
        cmd.Parameters.AddWithValue("@Subject", (object?)Truncate(subject, 255) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", Truncate((status ?? string.Empty).Trim(), 20));
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)Truncate(errorMessage, 2000) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedByUserId", (object?)createdByUserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value.Substring(0, maxLength);
    }

    // ─── SMTP Sender Resolution ────────────────────────────────────────────────────

    public async Task<bool> BranchSmtpProfilesOptionBEnabledAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await SchemaGate.TableExistsAsync(conn, "dbo.CallBranchSmtpProfileLink")
            && await SchemaGate.TableExistsAsync(conn, "dbo.CallSmtpProfile");
    }

    public async Task<bool> DepartmentSmtpProfilesOptionBEnabledAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await SchemaGate.TableExistsAsync(conn, "dbo.CallDepartmentSmtpProfileLink")
            && await SchemaGate.TableExistsAsync(conn, "dbo.CallSmtpProfile");
    }

    public async Task<CallSmtpProfileItem?> GetBranchSmtpProfileForTicketAsync(int branchId)
    {
        const string sql = @"
SELECT TOP 1
    p.ProfileId, p.ProfileName, p.SmtpServer, p.SmtpPort, p.UseSsl,
    p.SmtpUsername, p.SmtpPasswordEnc, p.FromName, p.FromEmail,
    p.IsActive, p.UpdatedAt, p.UpdatedByUserId
FROM dbo.CallBranchSmtpProfileLink l
INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
WHERE l.BranchId = @BranchId AND l.IsActive = 1 AND p.IsActive = 1;";

        return await QuerySingleSmtpProfileAsync(sql, [new SqlParameter("@BranchId", branchId)]);
    }

    public async Task<CallSmtpProfileItem?> GetDepartmentSmtpProfileLinkAsync(int deptId)
    {
        const string sql = @"
SELECT TOP 1
    p.ProfileId, p.ProfileName, p.SmtpServer, p.SmtpPort, p.UseSsl,
    p.SmtpUsername, p.SmtpPasswordEnc, p.FromName, p.FromEmail,
    p.IsActive, p.UpdatedAt, p.UpdatedByUserId
FROM dbo.CallDepartmentSmtpProfileLink l
INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
WHERE l.DeptId = @DeptId AND l.IsActive = 1 AND p.IsActive = 1;";

        return await QuerySingleSmtpProfileAsync(sql, [new SqlParameter("@DeptId", deptId)]);
    }

    public async Task<CallSmtpProfileItem?> GetDepartmentLegacySmtpProfileAsync(int deptId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallDepartmentSmtpProfile"))
            return null;

        const string sql = @"
SELECT TOP 1
    ProfileId, CAST('Legacy' AS nvarchar(200)) AS ProfileName,
    SmtpServer, SmtpPort, UseSsl, SmtpUsername, SmtpPasswordEnc,
    FromName, FromEmail, CAST(1 AS bit) AS IsActive,
    UpdatedAt, UpdatedByUserId
FROM dbo.CallDepartmentSmtpProfile
WHERE DeptId = @DeptId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@DeptId", deptId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await ReadSmtpProfileAsync(reader);
    }

    public async Task<CallSmtpProfileItem?> GetBranchLegacySmtpProfileAsync(int branchId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallBranchSmtpProfile"))
            return null;

        const string sql = @"
SELECT TOP 1
    ProfileId, CAST('Legacy' AS nvarchar(200)) AS ProfileName,
    SmtpServer, SmtpPort, UseSsl, SmtpUsername, SmtpPasswordEnc,
    FromName, FromEmail, CAST(1 AS bit) AS IsActive,
    UpdatedAt, UpdatedByUserId
FROM dbo.CallBranchSmtpProfile
WHERE BranchId = @BranchId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@BranchId", branchId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await ReadSmtpProfileAsync(reader);
    }

    public async Task<CallEmailSettingsItem?> GetGlobalEmailSettingsAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailSettings"))
            return null;

        const string sql = @"
SELECT TOP 1 SettingsId, SmtpServer, SmtpPort, UseSsl, SmtpUsername,
    SmtpPasswordEnc, FromName, FromEmail, UpdatedAt, UpdatedByUserId
FROM dbo.CallEmailSettings
ORDER BY SettingsId DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new CallEmailSettingsItem
        {
            SettingsId = reader.GetInt32(0),
            SmtpServer = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            SmtpPort = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            UseSsl = reader.GetBoolean(3),
            SmtpUsername = reader.IsDBNull(4) ? null : reader.GetString(4),
            SmtpPasswordEnc = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5),
            FromName = reader.IsDBNull(6) ? null : reader.GetString(6),
            FromEmail = reader.IsDBNull(7) ? null : reader.GetString(7),
            UpdatedAt = reader.GetDateTime(8),
            UpdatedByUserId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
        };
    }

    public async Task SaveEmailSettingsAsync(CallEmailSettingsItem settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailSettings"))
            throw new InvalidOperationException("CallEmailSettings table is not installed in this database yet.");

        // Append-only history, matching the desktop workspace: newest row wins.
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

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SmtpServer", (settings.SmtpServer ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@SmtpPort", settings.SmtpPort);
        cmd.Parameters.AddWithValue("@UseSsl", settings.UseSsl);
        cmd.Parameters.AddWithValue("@SmtpUsername", (object?)settings.SmtpUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SmtpPasswordEnc", (object?)settings.SmtpPasswordEnc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FromName", (object?)settings.FromName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FromEmail", (object?)settings.FromEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UpdatedByUserId", (object?)settings.UpdatedByUserId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<SmtpSenderConfig?> GetSmtpSenderForTicketAsync(int? branchId, int? deptId)
    {
        // Cascade (mirrors the standalone scheduler): 1) Branch Option B
        // profile, 2) Branch legacy profile, 3) Department Option B profile,
        // 4) Department legacy profile, 5) Global CallEmailSettings. A tier
        // with a blank server falls through instead of blocking the rest.

        if (branchId.HasValue && branchId.Value > 0 && await BranchSmtpProfilesOptionBEnabledAsync())
        {
            var profile = await GetBranchSmtpProfileForTicketAsync(branchId.Value);
            if (profile is not null && !string.IsNullOrWhiteSpace(profile.SmtpServer))
                return ToSmtpConfig(profile);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            var legacy = await GetBranchLegacySmtpProfileAsync(branchId.Value);
            if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.SmtpServer))
                return ToSmtpConfig(legacy);
        }

        if (deptId.HasValue && deptId.Value > 0)
        {
            if (await DepartmentSmtpProfilesOptionBEnabledAsync())
            {
                var profile = await GetDepartmentSmtpProfileLinkAsync(deptId.Value);
                if (profile is not null && !string.IsNullOrWhiteSpace(profile.SmtpServer))
                    return ToSmtpConfig(profile);
            }

            var deptLegacy = await GetDepartmentLegacySmtpProfileAsync(deptId.Value);
            if (deptLegacy is not null && !string.IsNullOrWhiteSpace(deptLegacy.SmtpServer))
                return ToSmtpConfig(deptLegacy);
        }

        var global = await GetGlobalEmailSettingsAsync();
        if (global != null && !string.IsNullOrWhiteSpace(global.SmtpServer))
        {
            return new SmtpSenderConfig
            {
                SmtpServer = global.SmtpServer,
                SmtpPort = global.SmtpPort,
                UseSsl = global.UseSsl,
                SmtpUsername = global.SmtpUsername,
                SmtpPasswordEnc = global.SmtpPasswordEnc,
                FromName = global.FromName,
                FromEmail = global.FromEmail
            };
        }

        return null;
    }

    private async Task<CallSmtpProfileItem?> QuerySingleSmtpProfileAsync(string sql, SqlParameter[] parameters)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallSmtpProfile"))
            return null;

        await using var cmd = new SqlCommand(sql, conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await ReadSmtpProfileAsync(reader);
    }

    private static async Task<CallSmtpProfileItem?> ReadSmtpProfileAsync(SqlDataReader reader)
    {
        if (!await reader.ReadAsync()) return null;
        return new CallSmtpProfileItem
        {
            ProfileId = reader.GetInt32(0),
            ProfileName = reader.GetString(1),
            SmtpServer = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            SmtpPort = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            UseSsl = reader.GetBoolean(4),
            SmtpUsername = reader.IsDBNull(5) ? null : reader.GetString(5),
            SmtpPasswordEnc = reader.IsDBNull(6) ? null : (byte[])reader.GetValue(6),
            FromName = reader.IsDBNull(7) ? null : reader.GetString(7),
            FromEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
            IsActive = reader.GetBoolean(9),
            UpdatedAt = reader.GetDateTime(10),
            UpdatedByUserId = reader.IsDBNull(11) ? null : reader.GetInt32(11)
        };
    }

    private static SmtpSenderConfig ToSmtpConfig(CallSmtpProfileItem profile)
    {
        return new SmtpSenderConfig
        {
            SmtpServer = profile.SmtpServer,
            SmtpPort = profile.SmtpPort,
            UseSsl = profile.UseSsl,
            SmtpUsername = profile.SmtpUsername,
            SmtpPasswordEnc = profile.SmtpPasswordEnc,
            FromName = profile.FromName,
            FromEmail = profile.FromEmail
        };
    }

    // ─── Heartbeat ─────────────────────────────────────────────────────────────────
    public async Task<long> WriteHeartbeatAsync(SchedulerHeartbeat heartbeat)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // Create the table if it doesn't exist
        const string createSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'dbo' AND t.name = 'CallSchedulerHeartbeat')
CREATE TABLE dbo.CallSchedulerHeartbeat (
    HeartbeatId BIGINT IDENTITY(1,1) PRIMARY KEY,
    StartedAtUtc DATETIME2 NOT NULL,
    FinishedAtUtc DATETIME2 NULL,
    Succeeded BIT NULL,
    LockAcquired BIT NOT NULL DEFAULT 0,
    ReminderCandidates INT NOT NULL DEFAULT 0,
    RemindersSent INT NOT NULL DEFAULT 0,
    EscalationCandidates INT NOT NULL DEFAULT 0,
    EscalationsApplied INT NOT NULL DEFAULT 0,
    ErrorMessage NVARCHAR(2000) NULL,
    MachineName NVARCHAR(255) NOT NULL DEFAULT '',
    Version NVARCHAR(50) NOT NULL DEFAULT ''
);";
        await using (var createCmd = new SqlCommand(createSql, conn))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        const string insertSql = @"
INSERT dbo.CallSchedulerHeartbeat (StartedAtUtc, FinishedAtUtc, Succeeded, LockAcquired,
    ReminderCandidates, RemindersSent, EscalationCandidates, EscalationsApplied,
    ErrorMessage, MachineName, Version)
OUTPUT INSERTED.HeartbeatId
VALUES (@StartedAtUtc, @FinishedAtUtc, @Succeeded, @LockAcquired,
    @ReminderCandidates, @RemindersSent, @EscalationCandidates, @EscalationsApplied,
    @ErrorMessage, @MachineName, @Version);";

        await using var cmd = new SqlCommand(insertSql, conn);
        cmd.Parameters.AddWithValue("@StartedAtUtc", heartbeat.StartedAtUtc);
        cmd.Parameters.AddWithValue("@FinishedAtUtc", (object?)heartbeat.FinishedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Succeeded", (object?)heartbeat.Succeeded ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LockAcquired", heartbeat.LockAcquired);
        cmd.Parameters.AddWithValue("@ReminderCandidates", heartbeat.ReminderCandidates);
        cmd.Parameters.AddWithValue("@RemindersSent", heartbeat.RemindersSent);
        cmd.Parameters.AddWithValue("@EscalationCandidates", heartbeat.EscalationCandidates);
        cmd.Parameters.AddWithValue("@EscalationsApplied", heartbeat.EscalationsApplied);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)heartbeat.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MachineName", heartbeat.MachineName);
        cmd.Parameters.AddWithValue("@Version", heartbeat.Version);

        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? l : Convert.ToInt64(result);
    }

    // ─── Client Presence ─────────────────────────────────────────────────────────
    // Tracks which desktops are actively calling the server. Guarded reads so a
    // missing table (migration not yet applied) degrades to empty, never throws.

    public async Task<bool> UpsertClientPresenceAsync(string machineName, string userName, string? module, string? clientVersion)
    {
        machineName = (machineName ?? string.Empty).Trim();
        userName = (userName ?? string.Empty).Trim();
        if (machineName.Length == 0 || machineName.Length > 64) return false;
        if (userName.Length == 0 || userName.Length > 128) return false;
        module = string.IsNullOrWhiteSpace(module) ? null : module.Trim();
        if (module is not null && module.Length > 50) module = module.Substring(0, 50);
        clientVersion = string.IsNullOrWhiteSpace(clientVersion) ? null : clientVersion.Trim();
        if (clientVersion is not null && clientVersion.Length > 50) clientVersion = clientVersion.Substring(0, 50);

        // Anonymous beats are cheap to forge: floor repeat writes per client
        // so a tight loop cannot churn the table. Throttled beats still
        // report success (the client is, after all, present).
        var throttleKey = (machineName + "|" + userName).ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        if (PresenceThrottle.TryGetValue(throttleKey, out var lastSeen) && (now - lastSeen) < PresenceThrottleWindow)
            return true;
        PresenceThrottle[throttleKey] = now;
        if (PresenceThrottle.Count > 10000) PresenceThrottle.Clear();

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallClientPresence"))
                return false;

            const string sql = @"
MERGE dbo.CallClientPresence AS tgt
USING (SELECT @MachineName AS MachineName, @UserName AS UserName) AS src
    ON tgt.MachineName = src.MachineName AND tgt.UserName = src.UserName
WHEN MATCHED THEN
    UPDATE SET LastSeenUtc = SYSUTCDATETIME(),
               Module = @Module,
               ClientVersion = @ClientVersion
WHEN NOT MATCHED THEN
    INSERT (MachineName, UserName, LastSeenUtc, FirstSeenUtc, ClientVersion, Module)
    VALUES (@MachineName, @UserName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ClientVersion, @Module);";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MachineName", machineName);
            cmd.Parameters.AddWithValue("@UserName", userName);
            cmd.Parameters.AddWithValue("@Module", (object?)module ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientVersion", (object?)clientVersion ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ClientPresenceReport> GetClientPresenceAsync(int onlineMinutes = 15)
    {
        var report = new ClientPresenceReport
        {
            OnlineMinutesThreshold = Math.Clamp(onlineMinutes <= 0 ? 15 : onlineMinutes, 1, 1440)
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallClientPresence"))
            return report;

        const string sql = @"
SELECT MachineName, UserName, LastSeenUtc, FirstSeenUtc, ClientVersion, Module,
       CASE WHEN LastSeenUtc >= DATEADD(minute, -@OnlineMinutes, SYSUTCDATETIME()) THEN 1 ELSE 0 END AS Online
FROM dbo.CallClientPresence
ORDER BY Online DESC, LastSeenUtc DESC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OnlineMinutes", report.OnlineMinutesThreshold);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new ClientPresenceItem
            {
                MachineName = reader.GetString(0),
                UserName = reader.GetString(1),
                LastSeenUtc = reader.GetDateTime(2),
                FirstSeenUtc = reader.GetDateTime(3),
                ClientVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                Module = reader.IsDBNull(5) ? null : reader.GetString(5),
                Online = reader.GetInt32(6) == 1
            };
            report.Clients.Add(item);
            if (item.Online) report.OnlineCount++;
        }

        return report;
    }

    public async Task<SchedulerHeartbeatSummary?> GetHeartbeatSummaryAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallSchedulerHeartbeat"))
            return null;

        const string sql = @"
SELECT
    MAX(StartedAtUtc) AS LastStartedUtc,
    MAX(FinishedAtUtc) AS LastFinishedUtc,
    MAX(CASE WHEN Succeeded = 1 THEN FinishedAtUtc END) AS LastSuccessUtc,
    COUNT(1) AS TotalRuns,
    SUM(CASE WHEN Succeeded = 1 THEN 1 ELSE 0 END) AS SuccessfulRuns,
    SUM(CASE WHEN Succeeded = 0 THEN 1 ELSE 0 END) AS FailedRuns,
    (SELECT TOP 1 ErrorMessage FROM dbo.CallSchedulerHeartbeat WHERE Succeeded = 0 ORDER BY HeartbeatId DESC) AS LastError,
    (SELECT TOP 1 MachineName FROM dbo.CallSchedulerHeartbeat ORDER BY HeartbeatId DESC) AS MachineName,
    (SELECT TOP 1 Version FROM dbo.CallSchedulerHeartbeat ORDER BY HeartbeatId DESC) AS Version
FROM dbo.CallSchedulerHeartbeat;";

        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new SchedulerHeartbeatSummary
        {
            LastStartedUtc = reader.IsDBNull(0) ? null : reader.GetDateTime(0),
            LastFinishedUtc = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
            LastSuccessUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            TotalRuns = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            SuccessfulRuns = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            FailedRuns = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            LastError = reader.IsDBNull(6) ? null : reader.GetString(6),
            MachineName = reader.IsDBNull(7) ? null : reader.GetString(7),
            Version = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    // ─── Connection Test ───────────────────────────────────────────────────────────

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void UpdateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        lock (_connectionStringLock) { _connectionString = connectionString; }
    }

 public async Task<SchedulerHeartbeatPage> GetHeartbeatPageAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallSchedulerHeartbeat"))
                return new SchedulerHeartbeatPage { Page = page, PageSize = pageSize };

            var offset = (page - 1) * pageSize;

            // Get total count
            const string countSql = "SELECT COUNT(1) FROM dbo.CallSchedulerHeartbeat;";
            int totalCount = 0;
            await using (var countCmd = new SqlCommand(countSql, conn))
            {
                var total = await countCmd.ExecuteScalarAsync();
                totalCount = total is long l ? (int)l : Convert.ToInt32(total ?? 0);
            }

            // Get paged data
            const string sql = @"
SELECT
    HeartbeatId, StartedAtUtc, FinishedAtUtc, Succeeded, LockAcquired,
    ReminderCandidates, RemindersSent, EscalationCandidates, EscalationsApplied,
    ErrorMessage, MachineName, Version
FROM dbo.CallSchedulerHeartbeat
ORDER BY HeartbeatId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);

            await using var reader = await cmd.ExecuteReaderAsync();
            var items = new List<SchedulerHeartbeat>();
            while (await reader.ReadAsync())
            {
                items.Add(new SchedulerHeartbeat
                {
                    HeartbeatId = reader.GetInt64(0),
                    StartedAtUtc = reader.GetDateTime(1),
                    FinishedAtUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    Succeeded = reader.IsDBNull(3) ? null : reader.GetBoolean(3),
                    LockAcquired = reader.GetBoolean(4),
                    ReminderCandidates = reader.GetInt32(5),
                    RemindersSent = reader.GetInt32(6),
                    EscalationCandidates = reader.GetInt32(7),
                    EscalationsApplied = reader.GetInt32(8),
                    ErrorMessage = reader.IsDBNull(9) ? null : reader.GetString(9),
                    MachineName = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    Version = reader.IsDBNull(11) ? "1.0.0" : reader.GetString(11)
                });
            }

            return new SchedulerHeartbeatPage
            {
                Items = items,
                TotalCount = (int)totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

    public async Task<MonitoringDashboard> GetMonitoringDashboardAsync(int maxRows = 20)
    {
        maxRows = Math.Clamp(maxRows, 1, 100);
        var dashboard = new MonitoringDashboard();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var hasTickets = await SchemaGate.TableExistsAsync(conn, "dbo.CallTicket");
        var hasTicketSource = hasTickets && await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "TicketSource");
        var hasAssignment = hasTickets && await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "AssignedToEmpId");
        var hasHistory = await SchemaGate.TableExistsAsync(conn, "dbo.CallTicketHistory");
        var hasHeartbeat = await SchemaGate.TableExistsAsync(conn, "dbo.CallSchedulerHeartbeat");
        var hasEmailLog = await SchemaGate.TableExistsAsync(conn, "dbo.CallEmailLog");

        if (hasTicketSource && hasAssignment)
        {
            const string countSql = @"
SELECT COUNT(1)
FROM dbo.CallTicket
WHERE UPPER(LTRIM(RTRIM(TicketSource))) = 'PORTAL'
  AND AssignedToEmpId IS NULL
  AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed');";
            await using (var countCmd = new SqlCommand(countSql, conn))
            {
                dashboard.UnassignedPortalTicketCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);
            }

            var hasLastContactAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastContactAt");
            var lastActivityExpr = hasLastContactAt
                ? "COALESCE(LastContactAt, UpdatedAt, CreatedAt)"
                : "COALESCE(UpdatedAt, CreatedAt)";
            var triageSql = $@"
SELECT TOP (@MaxRows)
    TicketId, TicketCode, CallerName, Issue, Status, Priority, CreatedAt,
    {lastActivityExpr} AS LastActivityAt
FROM dbo.CallTicket
WHERE UPPER(LTRIM(RTRIM(TicketSource))) = 'PORTAL'
  AND AssignedToEmpId IS NULL
  AND Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
ORDER BY CreatedAt ASC, TicketId ASC;";
            await using var triageCmd = new SqlCommand(triageSql, conn);
            triageCmd.Parameters.AddWithValue("@MaxRows", maxRows);
            await using var triageReader = await triageCmd.ExecuteReaderAsync();
            while (await triageReader.ReadAsync())
            {
                dashboard.UnassignedPortalTickets.Add(new PortalTriageTicket
                {
                    TicketId = triageReader.GetInt32(0),
                    TicketCode = triageReader.IsDBNull(1) ? string.Empty : triageReader.GetString(1),
                    CallerName = triageReader.IsDBNull(2) ? string.Empty : triageReader.GetString(2),
                    Issue = triageReader.IsDBNull(3) ? string.Empty : triageReader.GetString(3),
                    Status = triageReader.IsDBNull(4) ? string.Empty : triageReader.GetString(4),
                    Priority = triageReader.IsDBNull(5) ? string.Empty : triageReader.GetString(5),
                    CreatedAt = triageReader.GetDateTime(6),
                    LastActivityAt = triageReader.IsDBNull(7) ? null : triageReader.GetDateTime(7)
                });
            }
        }

        if (hasHistory && hasTickets)
        {
            const string movementSql = @"
SELECT TOP (@MaxRows)
    h.HistoryId, h.TicketId, t.TicketCode, h.ChangedAt, h.FieldName,
    h.OldValue, h.NewValue, h.Note, h.ChangedByUserId,
    CONVERT(bit, CASE WHEN UPPER(ISNULL(h.NewValue, '')) = 'ESCALATED'
                        AND UPPER(ISNULL(h.Note, '')) LIKE '%AUTO-ESCALAT%' THEN 1 ELSE 0 END) AS IsAutoEscalation
FROM dbo.CallTicketHistory h
INNER JOIN dbo.CallTicket t ON t.TicketId = h.TicketId
ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";
            await using var movementCmd = new SqlCommand(movementSql, conn);
            movementCmd.Parameters.AddWithValue("@MaxRows", maxRows);
            await using var movementReader = await movementCmd.ExecuteReaderAsync();
            while (await movementReader.ReadAsync())
            {
                var movement = new TicketMovement
                {
                    HistoryId = movementReader.GetInt64(0),
                    TicketId = movementReader.GetInt32(1),
                    TicketCode = movementReader.IsDBNull(2) ? string.Empty : movementReader.GetString(2),
                    ChangedAt = movementReader.GetDateTime(3),
                    FieldName = movementReader.IsDBNull(4) ? string.Empty : movementReader.GetString(4),
                    OldValue = movementReader.IsDBNull(5) ? null : movementReader.GetString(5),
                    NewValue = movementReader.IsDBNull(6) ? null : movementReader.GetString(6),
                    Note = movementReader.IsDBNull(7) ? null : movementReader.GetString(7),
                    ChangedByUserId = movementReader.IsDBNull(8) ? null : movementReader.GetInt32(8),
                    IsAutoEscalation = !movementReader.IsDBNull(9) && movementReader.GetBoolean(9)
                };
                dashboard.RecentTicketMovements.Add(movement);
                if (movement.IsAutoEscalation) dashboard.RecentAutoEscalationCount++;
            }
        }

        if (hasHeartbeat)
        {
            const string schedulerSql = @"
SELECT TOP (@MaxRows) HeartbeatId, StartedAtUtc, FinishedAtUtc, Succeeded,
    RemindersSent, EscalationsApplied, ErrorMessage
FROM dbo.CallSchedulerHeartbeat
ORDER BY HeartbeatId DESC;";
            await using var schedulerCmd = new SqlCommand(schedulerSql, conn);
            schedulerCmd.Parameters.AddWithValue("@MaxRows", maxRows);
            await using var schedulerReader = await schedulerCmd.ExecuteReaderAsync();
            while (await schedulerReader.ReadAsync())
            {
                dashboard.RecentSchedulerActivity.Add(new SchedulerActivity
                {
                    HeartbeatId = schedulerReader.GetInt64(0),
                    StartedAtUtc = schedulerReader.GetDateTime(1),
                    FinishedAtUtc = schedulerReader.IsDBNull(2) ? null : schedulerReader.GetDateTime(2),
                    Succeeded = schedulerReader.IsDBNull(3) ? null : schedulerReader.GetBoolean(3),
                    RemindersSent = schedulerReader.IsDBNull(4) ? 0 : schedulerReader.GetInt32(4),
                    EscalationsApplied = schedulerReader.IsDBNull(5) ? 0 : schedulerReader.GetInt32(5),
                    ErrorMessage = schedulerReader.IsDBNull(6) ? null : schedulerReader.GetString(6)
                });
            }
        }

        if (hasEmailLog)
        {
            const string notificationCountSql = @"
SELECT COUNT(1)
FROM dbo.CallEmailLog
WHERE UPPER(EmailType) = 'REMINDER'
  AND UPPER(Status) = 'SENT'
  AND DateSent >= CONVERT(date, SYSUTCDATETIME());";
            await using (var notificationCountCmd = new SqlCommand(notificationCountSql, conn))
            {
                dashboard.ReminderEmailsSentToday = Convert.ToInt32(await notificationCountCmd.ExecuteScalarAsync() ?? 0);
            }

            const string notificationSql = @"
SELECT TOP (@MaxRows)
    l.EmailLogId, l.TicketId, t.TicketCode, l.DateSent, l.EmailType,
    l.Status, l.Recipient, l.ErrorMessage
FROM dbo.CallEmailLog l
LEFT JOIN dbo.CallTicket t ON t.TicketId = l.TicketId
WHERE UPPER(l.EmailType) IN ('REMINDER', 'ESCALATION')
ORDER BY l.DateSent DESC, l.EmailLogId DESC;";
            await using var notificationCmd = new SqlCommand(notificationSql, conn);
            notificationCmd.Parameters.AddWithValue("@MaxRows", maxRows);
            await using var notificationReader = await notificationCmd.ExecuteReaderAsync();
            while (await notificationReader.ReadAsync())
            {
                dashboard.RecentNotifications.Add(new NotificationActivity
                {
                    EmailLogId = notificationReader.GetInt64(0),
                    TicketId = notificationReader.IsDBNull(1) ? null : notificationReader.GetInt32(1),
                    TicketCode = notificationReader.IsDBNull(2) ? null : notificationReader.GetString(2),
                    DateSent = notificationReader.GetDateTime(3),
                    EmailType = notificationReader.IsDBNull(4) ? string.Empty : notificationReader.GetString(4),
                    Status = notificationReader.IsDBNull(5) ? string.Empty : notificationReader.GetString(5),
                    Recipient = notificationReader.IsDBNull(6) ? null : notificationReader.GetString(6),
                    ErrorMessage = notificationReader.IsDBNull(7) ? null : notificationReader.GetString(7)
                });
            }
        }

        return dashboard;
    }

    public async Task<List<int>> GetTicketIdsNeedingPortalSafeAutoEscalationAsync(int maxRows = 25)
    {
        maxRows = Math.Clamp(maxRows, 1, 500);

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallTicket")
            || !await SchemaGate.TableExistsAsync(conn, "dbo.CallEscalationSettings"))
        {
            return [];
        }

        var overrideExists = await SchemaGate.TableExistsAsync(conn, "dbo.CallTicketEscalationOverride");
        var hasLastContactAt = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "LastContactAt");
        var hasTicketSource = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "TicketSource");
        var hasAssignedToEmpId = await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "AssignedToEmpId");
        // Without both columns we cannot prove that a candidate is safe to
        // escalate, so fail closed until the Portal ticket schema is deployed.
        if (!hasTicketSource || !hasAssignedToEmpId)
            return [];

        var lastActivityExpr = hasLastContactAt
            ? "COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt)"
            : "COALESCE(t.UpdatedAt, t.CreatedAt)";
        var thresholdExpr = overrideExists
            ? "ISNULL(o.DaysToSupervisor, s.DaysToSupervisor)"
            : "s.DaysToSupervisor";
        const string portalOwnershipExclusion = "\n  AND NOT (UPPER(LTRIM(RTRIM(t.TicketSource))) = 'PORTAL' AND t.AssignedToEmpId IS NULL)";

        // Replacement-aware temporaries (see reminder query): only temp tickets
        // WITH issued parts stay parked; Service-Only temporaries escalate
        // under the normal thresholds.
        var hasHistory = await SchemaGate.TableExistsAsync(conn, "dbo.CallTicketHistory");
        var tempClause = hasHistory
            ? @"AND (
    t.Status <> 'Resolved (Temporary)'
    OR NOT EXISTS (
        SELECT 1 FROM dbo.CallTicketHistory h
        WHERE h.TicketId = t.TicketId
          AND h.FieldName = 'ReplacementNewItemId'
          AND TRY_CONVERT(int, h.NewValue) > 0
    )
  )"
            : "AND t.Status <> 'Resolved (Temporary)'";

        var sql = $@"
SELECT TOP (@MaxRows) t.TicketId
FROM dbo.CallTicket t
CROSS JOIN (SELECT TOP 1 DaysToSupervisor FROM dbo.CallEscalationSettings ORDER BY SettingsId DESC) s
{(overrideExists ? "LEFT JOIN dbo.CallTicketEscalationOverride o ON o.TicketId = t.TicketId" : string.Empty)}
WHERE t.Status NOT IN ('Solved', 'Closed', 'Escalated', 'Forwarded to Repair')
  {tempClause}
  AND {thresholdExpr} IS NOT NULL
  AND {thresholdExpr} > 0
  AND DATEDIFF(DAY, {lastActivityExpr}, SYSUTCDATETIME()) >= {thresholdExpr}{portalOwnershipExclusion}
ORDER BY {lastActivityExpr} ASC, t.TicketId ASC;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@MaxRows", maxRows);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<int>();
        while (await reader.ReadAsync())
            results.Add(reader.GetInt32(0));
        return results;
    }

    public async Task<bool> HasReplacementAllocationAsync(int ticketId)
    {
        if (ticketId <= 0) return false;

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.TableExistsAsync(conn, "dbo.CallTicketHistory"))
            return false;

        const string sql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.CallTicketHistory h
    WHERE h.TicketId = @TicketId
      AND h.FieldName = 'ReplacementNewItemId'
      AND TRY_CONVERT(int, h.NewValue) > 0
) THEN 1 ELSE 0 END AS bit);";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }

    public async Task<string?> GetPortalTicketContactEmailAsync(int ticketId)    {
        if (ticketId <= 0)
            return null;

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        if (!await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "TicketSource")
            || !await SchemaGate.ColumnExistsAsync(conn, "dbo.CallTicket", "ContactEmail"))
        {
            return null;
        }

        const string sql = @"
SELECT TOP 1 NULLIF(LTRIM(RTRIM(ContactEmail)), '')
FROM dbo.CallTicket
WHERE TicketId = @TicketId
  AND UPPER(LTRIM(RTRIM(TicketSource))) = 'PORTAL';";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        var result = await cmd.ExecuteScalarAsync();
        return result is string email && !string.IsNullOrWhiteSpace(email) ? email.Trim() : null;
    }
}
