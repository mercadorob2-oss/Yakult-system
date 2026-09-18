using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task<CallTicketNotificationData> GetTicketNotificationDataAsync(int ticketId)
        {
            if (ticketId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return null;

                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId");
                var hasBranchTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");
                var hasLastContactAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastContactAt");
                var hasLastReminderSentAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastReminderSentAt");
                var hasContactEmail = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "ContactEmail");

                var sql = @"
 SELECT TOP 1
     t.TicketId,
     t.TicketCode,
     t.ComId,
     c.Name AS Company,
     t.DeptId,
     d.Name AS Department," +
                    (hasBranchId ? "\n    t.BranchId," : "\n    CAST(NULL AS int) AS BranchId,") +
                    (hasBranchId && hasBranchTable
                        ? @"
    CASE
        WHEN b.BranchId IS NULL THEN NULL
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch,"
                        : "\n    CAST(NULL AS nvarchar(300)) AS Branch,") +
                    @"
     t.CallerName,
     t.Issue,
     t.ProvidedSolution,
     t.IssueType,
     t.Priority,
     t.Status," +
                    (hasContactEmail ? "\n    t.ContactEmail," : "\n    CAST(NULL AS nvarchar(255)) AS ContactEmail,") +
                    @"
     t.AssignedToEmpId,
     e.Name AS AssignedTo,
     t.CreatedAt,
     t.UpdatedAt," +
                     (hasLastContactAt ? "\n    t.LastContactAt," : "\n    CAST(NULL AS datetime2(2)) AS LastContactAt,") +
                     (hasLastReminderSentAt ? "\n    t.LastReminderSentAt," : "\n    CAST(NULL AS datetime2(2)) AS LastReminderSentAt,") +
                     @"
     t.SolvedAt
 FROM dbo.CallTicket t
 LEFT JOIN dbo.Company c ON c.ComId = t.ComId
 LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
 LEFT JOIN dbo.Employee e ON e.EmpId = t.AssignedToEmpId" +
                    (hasBranchId && hasBranchTable ? "\n LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId" : string.Empty) +
                    @"
 WHERE t.TicketId = @TicketId;";

                return await connection.QuerySingleOrDefaultAsync<CallTicketNotificationData>(sql, new { TicketId = ticketId });
            }
        }

        public async Task<bool> CanStoreTicketContactEmailAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket")
                    && await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "ContactEmail");
            }
        }

        public async Task SetTicketContactEmailAsync(int ticketId, string contactEmail)
        {
            if (ticketId <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticketId));

            if (!TryNormalizeTicketContactEmail(contactEmail, out var normalizedEmail))
                throw new ArgumentException("Enter a valid ticket contact email.", nameof(contactEmail));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket")
                    || !await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "ContactEmail"))
                {
                    throw new InvalidOperationException("Ticket contact email storage is not installed. Apply the ContactEmail migration before creating tickets.");
                }

                var affected = await connection.ExecuteAsync(
                    "UPDATE dbo.CallTicket SET ContactEmail = @ContactEmail WHERE TicketId = @TicketId;",
                    new { TicketId = ticketId, ContactEmail = normalizedEmail });
                if (affected != 1)
                    throw new InvalidOperationException("The newly created ticket could not be assigned its contact email.");
            }
        }

        public async Task<List<int>> GetTicketIdsNeedingReminderAsync(int reminderDays, int maxRows = 25)
        {
            if (reminderDays < 1) reminderDays = 1;
            if (reminderDays > 60) reminderDays = 60;
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 500) maxRows = 500;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return new List<int>();

                var hasLastContactAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastContactAt");
                var hasLastReminderSentAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastReminderSentAt");

                if (!hasLastContactAt || !hasLastReminderSentAt)
                    return new List<int>();

                // Replacement-aware temporaries: temp tickets WITH issued parts
                // stay parked; Service-Only temporaries remain watchable.
                var hasHistory = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory");
                var tempGate = hasHistory
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

                var sql = @"
SELECT TOP (@MaxRows) t.TicketId
FROM dbo.CallTicket t
WHERE t.Status NOT IN ('Solved', 'Closed')
  " + tempGate + @"
  AND t.LastContactAt IS NOT NULL
  AND DATEDIFF(DAY, t.LastContactAt, SYSUTCDATETIME()) >= @ReminderDays
  AND (
        t.LastReminderSentAt IS NULL
     OR DATEDIFF(DAY, t.LastReminderSentAt, SYSUTCDATETIME()) >= @ReminderDays
  )
ORDER BY t.UpdatedAt ASC, t.TicketId ASC;";

                var rows = await connection.QueryAsync<int>(sql, new { MaxRows = maxRows, ReminderDays = reminderDays });
                return rows?.ToList() ?? new List<int>();
            }
        }

        public async Task MarkReminderSentAsync(int ticketId)
        {
            if (ticketId <= 0)
                return;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastReminderSentAt"))
                    return;

                await connection.ExecuteAsync(
                    "UPDATE dbo.CallTicket SET LastReminderSentAt = SYSUTCDATETIME() WHERE TicketId = @TicketId;",
                    new { TicketId = ticketId });
            }
        }

        public async Task<List<int>> GetTicketIdsNeedingAutoEscalationAsync(int maxRows = 25)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 500) maxRows = 500;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return new List<int>();

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEscalationSettings"))
                    return new List<int>();

                var overrideExists = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketEscalationOverride");
                var hasLastContactAt = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "LastContactAt");
                var hasTicketSource = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "TicketSource");
                var hasAssignedToEmpId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "AssignedToEmpId");
                // Portal invariant (mirrors the server): unassigned Portal
                // submissions stay in desktop triage until an IT employee owns
                // them. Fail closed without both columns.
                if (!hasTicketSource || !hasAssignedToEmpId)
                    return new List<int>();

                var lastActivityExpr = hasLastContactAt
                    ? "COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt)"
                    : "COALESCE(t.UpdatedAt, t.CreatedAt)";

                var thresholdExpr = overrideExists
                    ? "ISNULL(o.DaysToSupervisor, s.DaysToSupervisor)"
                    : "s.DaysToSupervisor";

                // Replacement-aware temporaries (see reminder query above).
                var hasHistory = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory");
                var tempGate = hasHistory
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
  {tempGate}
  AND NOT (UPPER(LTRIM(RTRIM(t.TicketSource))) = 'PORTAL' AND t.AssignedToEmpId IS NULL)
  AND {thresholdExpr} IS NOT NULL
  AND {thresholdExpr} > 0
  AND DATEDIFF(DAY, {lastActivityExpr}, SYSUTCDATETIME()) >= {thresholdExpr}
ORDER BY {lastActivityExpr} ASC, t.TicketId ASC;";

                var rows = await connection.QueryAsync<int>(sql, new { MaxRows = maxRows });
                return rows?.ToList() ?? new List<int>();
            }
        }

        public async Task<CallEscalationSettingsItem> GetEscalationSettingsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                try
                {
                    if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEscalationSettings"))
                    {
                        return await connection.QuerySingleAsync<CallEscalationSettingsItem>(
                            "dbo.sp_Call_GetEscalationSettings",
                            commandType: System.Data.CommandType.StoredProcedure);
                    }
                }
                catch
                {
                    // Fallback below.
                }

                return new CallEscalationSettingsItem
                {
                    DaysToSupervisor = 2,
                    DaysToManager = 3,
                    SupervisorPosition = "IT Supervisor",
                    ManagerPosition = "IT Manager"
                };
            }
        }

        public async Task SaveEscalationSettingsAsync(CallEscalationSettingsItem settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var daysToSupervisor = settings.DaysToSupervisor < 1 ? 1 : settings.DaysToSupervisor;
            var daysToManager = settings.DaysToManager < daysToSupervisor ? daysToSupervisor : settings.DaysToManager;
            var supervisorPosition = string.IsNullOrWhiteSpace(settings.SupervisorPosition) ? "IT Supervisor" : settings.SupervisorPosition.Trim();
            var managerPosition = string.IsNullOrWhiteSpace(settings.ManagerPosition) ? "IT Manager" : settings.ManagerPosition.Trim();

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEscalationSettings"))
                    throw new InvalidOperationException("Escalation settings schema is not installed in this database yet.");

                const string sql = @"
INSERT dbo.CallEscalationSettings
(
    DaysToSupervisor,
    DaysToManager,
    SupervisorPosition,
    ManagerPosition
)
VALUES
(
    @DaysToSupervisor,
    @DaysToManager,
    @SupervisorPosition,
    @ManagerPosition
);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        DaysToSupervisor = daysToSupervisor,
                        DaysToManager = daysToManager,
                        SupervisorPosition = supervisorPosition,
                        ManagerPosition = managerPosition
                    });
            }
        }

        public async Task<bool> AutoEscalationAssigneeSettingsEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallAutoEscalationAssigneeSettings");
            }
        }

        public async Task<int?> GetAutoEscalationAssigneeEmpIdAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallAutoEscalationAssigneeSettings"))
                    return null;

                const string sql = @"
SELECT TOP 1 AssigneeEmpId
FROM dbo.CallAutoEscalationAssigneeSettings
ORDER BY SettingsId DESC;";

                try
                {
                    return await connection.ExecuteScalarAsync<int?>(sql);
                }
                catch
                {
                    return null;
                }
            }
        }

        public async Task SaveAutoEscalationAssigneeEmpIdAsync(int? assigneeEmpId, int? updatedByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallAutoEscalationAssigneeSettings"))
                    throw new InvalidOperationException("Auto escalation assignee schema is not installed in this database yet.");

                const string sql = @"
INSERT dbo.CallAutoEscalationAssigneeSettings
(
    AssigneeEmpId,
    UpdatedByUserId
)
VALUES
(
    @AssigneeEmpId,
    @UpdatedByUserId
);";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        AssigneeEmpId = assigneeEmpId,
                        UpdatedByUserId = updatedByUserId
                    });
            }
        }

        public async Task<CallTicketEscalationOverrideItem> GetTicketEscalationOverrideAsync(int ticketId)
        {
            if (!await EscalationOverridesEnabledAsync())
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await connection.QuerySingleOrDefaultAsync<CallTicketEscalationOverrideItem>(
                    "dbo.sp_Call_GetTicketEscalationOverride",
                    new { TicketId = ticketId },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }

        public async Task<Dictionary<int, CallTicketEscalationOverrideItem>> GetEscalationOverridesForTicketsAsync(IEnumerable<int> ticketIds)
        {
            var result = new Dictionary<int, CallTicketEscalationOverrideItem>();
            if (ticketIds == null) return result;

            var ids = ticketIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0) return result;

            if (!await EscalationOverridesEnabledAsync())
                return result;

            var idList = string.Join(",", ids);
            var sql = $@"
SELECT TicketId, DaysToSupervisor, DaysToManager, Reason, OverriddenByUserId, OverriddenAt
FROM dbo.CallTicketEscalationOverride
WHERE TicketId IN ({idList});";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketEscalationOverrideItem>(sql);
                foreach (var row in rows)
                    result[row.TicketId] = row;
            }

            return result;
        }

        public async Task SetTicketEscalationOverrideAsync(
            int ticketId,
            int? daysToSupervisor,
            int? daysToManager,
            string reason,
            int? changedByUserId,
            bool clear)
        {
            if (!await EscalationOverridesEnabledAsync())
                throw new InvalidOperationException("Escalation override schema is not installed in this database yet.");

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                await connection.ExecuteAsync(
                    "dbo.sp_Call_SetTicketEscalationOverride",
                    new
                    {
                        TicketId = ticketId,
                        DaysToSupervisor = daysToSupervisor,
                        DaysToManager = daysToManager,
                        Reason = reason,
                        ChangedByUserId = changedByUserId,
                        Clear = clear ? 1 : 0
                    },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
        }

        public async Task<bool> DeleteTicketAsync(int ticketId, int? deletedByUserId)
        {
            if (ticketId <= 0)
                return false;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return false;

                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        var exists = await connection.ExecuteScalarAsync<int>(
                            new CommandDefinition(
                                "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CallTicket WHERE TicketId = @TicketId) THEN 1 ELSE 0 END;",
                                new { TicketId = ticketId },
                                tx));

                        if (exists != 1)
                        {
                            try { tx.Rollback(); } catch { }
                            return false;
                        }

                        // Delete children first to satisfy FK constraints (when installed).
                        if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallEmailLog", tx))
                            await connection.ExecuteAsync(new CommandDefinition("DELETE dbo.CallEmailLog WHERE TicketId = @TicketId;", new { TicketId = ticketId }, tx));

                        if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketEscalationOverride", tx))
                            await connection.ExecuteAsync(new CommandDefinition("DELETE dbo.CallTicketEscalationOverride WHERE TicketId = @TicketId;", new { TicketId = ticketId }, tx));

                        if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketNote", tx))
                            await connection.ExecuteAsync(new CommandDefinition("DELETE dbo.CallTicketNote WHERE TicketId = @TicketId;", new { TicketId = ticketId }, tx));

                        if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory", tx))
                            await connection.ExecuteAsync(new CommandDefinition("DELETE dbo.CallTicketHistory WHERE TicketId = @TicketId;", new { TicketId = ticketId }, tx));

                        // Finally delete the ticket itself.
                        await connection.ExecuteAsync(new CommandDefinition("DELETE dbo.CallTicket WHERE TicketId = @TicketId;", new { TicketId = ticketId }, tx));

                        // Best-effort: we currently don't have an audit table for deletions; deletedByUserId is reserved for future use.
                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }
    }
}
