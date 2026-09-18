using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public sealed class SmtpSenderResolution
        {
            public string Source { get; set; } // BranchProfile, DepartmentProfile, DepartmentLegacy, SystemSettings
            public int? ProfileId { get; set; }
            public string ProfileName { get; set; }
            public SmtpSenderConfig Sender { get; set; }
        }

        private sealed class SmtpSenderResolutionRow
        {
            public int? ProfileId { get; set; }
            public string ProfileName { get; set; }
            public string SmtpServer { get; set; }
            public int SmtpPort { get; set; }
            public bool UseSsl { get; set; }
            public string SmtpUsername { get; set; }
            public byte[] SmtpPasswordEnc { get; set; }
            public string FromName { get; set; }
            public string FromEmail { get; set; }
        }

        public async Task<List<CallDepartmentSmtpProfileItem>> GetDepartmentSmtpProfilesAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    return new List<CallDepartmentSmtpProfileItem>();

                const string sql = @"
SELECT
    ProfileId,
    DeptId,
    DepartmentName,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    FromName,
    FromEmail,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallDepartmentSmtpProfile
ORDER BY DepartmentName;";

                var rows = await connection.QueryAsync<CallDepartmentSmtpProfileItem>(sql);
                return rows?.ToList() ?? new List<CallDepartmentSmtpProfileItem>();
            }
        }

        public async Task<CallDepartmentSmtpProfileItem> GetDepartmentSmtpProfileByIdAsync(int profileId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    return null;

                const string sql = @"
SELECT
    ProfileId,
    DeptId,
    DepartmentName,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallDepartmentSmtpProfile
WHERE ProfileId = @ProfileId;";

                return await connection.QuerySingleOrDefaultAsync<CallDepartmentSmtpProfileItem>(sql, new { ProfileId = profileId });
            }
        }

        public async Task SaveDepartmentSmtpProfileAsync(CallDepartmentSmtpProfileItem profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (string.IsNullOrWhiteSpace(profile.DepartmentName))
                throw new ArgumentException("Department is required.", nameof(profile));

            if (string.IsNullOrWhiteSpace(profile.SmtpServer))
                throw new ArgumentException("SMTP Server is required.", nameof(profile));

            if (profile.SmtpPort < 1 || profile.SmtpPort > 65535)
                throw new ArgumentException("SMTP Port must be between 1 and 65535.", nameof(profile));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    throw new InvalidOperationException("CallDepartmentSmtpProfile table is not installed in this database yet.");

                if (profile.ProfileId <= 0)
                {
                    const string insertSql = @"
INSERT dbo.CallDepartmentSmtpProfile
(
    DeptId,
    DepartmentName,
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
    @DeptId,
    @DepartmentName,
    @SmtpServer,
    @SmtpPort,
    @UseSsl,
    @SmtpUsername,
    @SmtpPasswordEnc,
    @FromName,
    @FromEmail,
    @UpdatedByUserId
);";

                    await connection.ExecuteAsync(insertSql, profile);
                }
                else
                {
                    const string updateSql = @"
UPDATE dbo.CallDepartmentSmtpProfile
SET
    DeptId = @DeptId,
    DepartmentName = @DepartmentName,
    SmtpServer = @SmtpServer,
    SmtpPort = @SmtpPort,
    UseSsl = @UseSsl,
    SmtpUsername = @SmtpUsername,
    SmtpPasswordEnc = @SmtpPasswordEnc,
    FromName = @FromName,
    FromEmail = @FromEmail,
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedByUserId = @UpdatedByUserId
WHERE ProfileId = @ProfileId;";

                    await connection.ExecuteAsync(updateSql, profile);
                }
            }
        }

        public async Task<SmtpSenderConfig> GetSmtpSenderForDepartmentAsync(int? deptId)
        {
            var resolution = await GetSmtpSenderResolutionForDepartmentAsync(deptId);
            return resolution?.Sender;
        }

        public async Task<SmtpSenderResolution> GetSmtpSenderResolutionForDepartmentAsync(int? deptId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                // 1) Option B department -> SMTP profile mapping
                if (deptId.HasValue && deptId.Value > 0)
                {
                    var optionB = await DepartmentSmtpProfilesOptionBEnabledAsync();
                    if (optionB)
                    {
                        const string sql = @"
	SELECT TOP 1
	    p.ProfileId,
	    p.ProfileName,
	    p.SmtpServer,
	    p.SmtpPort,
	    p.UseSsl,
	    p.SmtpUsername,
	    p.SmtpPasswordEnc,
	    COALESCE(NULLIF(p.FromName,''), NULLIF(p.ProfileName,'')) AS FromName,
	    p.FromEmail
	FROM dbo.CallDepartmentSmtpProfileLink l
	INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
	WHERE l.DeptId = @DeptId
	  AND l.IsActive = 1
	  AND p.IsActive = 1
	ORDER BY p.UpdatedAt DESC, p.ProfileId DESC;";

                        var row = await connection.QuerySingleOrDefaultAsync<SmtpSenderResolutionRow>(sql, new { DeptId = deptId.Value });
                        if (row != null && !string.IsNullOrWhiteSpace(row.SmtpServer))
                            return new SmtpSenderResolution
                            {
                                Source = "DepartmentProfile",
                                ProfileId = row.ProfileId,
                                ProfileName = row.ProfileName,
                                Sender = new SmtpSenderConfig
                                {
                                    SmtpServer = row.SmtpServer,
                                    SmtpPort = row.SmtpPort,
                                    UseSsl = row.UseSsl,
                                    SmtpUsername = row.SmtpUsername,
                                    SmtpPasswordEnc = row.SmtpPasswordEnc,
                                    FromName = row.FromName,
                                    FromEmail = row.FromEmail
                                }
                            };
                    }

                    // 2) Option A legacy per-department table (fallback)
                    if (await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    {
                        const string sqlA = @"
	SELECT TOP 1
	    ProfileId,
	    SmtpServer,
	    SmtpPort,
	    UseSsl,
	    SmtpUsername,
	    SmtpPasswordEnc,
	    FromName,
	    FromEmail
	FROM dbo.CallDepartmentSmtpProfile
	WHERE DeptId = @DeptId
	ORDER BY UpdatedAt DESC, ProfileId DESC;";

                        var rowA = await connection.QuerySingleOrDefaultAsync<SmtpSenderResolutionRow>(sqlA, new { DeptId = deptId.Value });
                        if (rowA != null && !string.IsNullOrWhiteSpace(rowA.SmtpServer))
                            return new SmtpSenderResolution
                            {
                                Source = "DepartmentLegacy",
                                ProfileId = rowA.ProfileId,
                                ProfileName = "(Legacy per-dept)",
                                Sender = new SmtpSenderConfig
                                {
                                    SmtpServer = rowA.SmtpServer,
                                    SmtpPort = rowA.SmtpPort,
                                    UseSsl = rowA.UseSsl,
                                    SmtpUsername = rowA.SmtpUsername,
                                    SmtpPasswordEnc = rowA.SmtpPasswordEnc,
                                    FromName = rowA.FromName,
                                    FromEmail = rowA.FromEmail
                                }
                            };
                    }
                }

                // 3) Global SMTP settings
                var settings = await GetEmailSettingsAsync();
                if (settings == null)
                    return null;

                if (string.IsNullOrWhiteSpace(settings.SmtpServer))
                    return new SmtpSenderResolution { Source = "SystemSettings", Sender = null };

                return new SmtpSenderResolution
                {
                    Source = "SystemSettings",
                    ProfileId = null,
                    ProfileName = "System SMTP settings",
                    Sender = new SmtpSenderConfig
                    {
                        SmtpServer = settings.SmtpServer,
                        SmtpPort = settings.SmtpPort,
                        UseSsl = settings.UseSsl,
                        SmtpUsername = settings.SmtpUsername,
                        SmtpPasswordEnc = settings.SmtpPasswordEnc,
                        FromName = settings.FromName,
                        FromEmail = settings.FromEmail
                    }
                };
            }
        }

        public async Task<SmtpSenderConfig> GetSmtpSenderForTicketAsync(int? branchId, int? deptId)
        {
            var resolution = await GetSmtpSenderResolutionForTicketAsync(branchId, deptId);
            return resolution?.Sender;
        }

        public async Task<SmtpSenderResolution> GetSmtpSenderResolutionForTicketAsync(int? branchId, int? deptId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                // 1) Branch -> SMTP profile mapping (Option B)
                if (branchId.HasValue && branchId.Value > 0)
                {
                    var optionB = await BranchSmtpProfilesOptionBEnabledAsync();
                    if (optionB)
                    {
                        const string sql = @"
	SELECT TOP 1
	    p.ProfileId,
	    p.ProfileName,
	    p.SmtpServer,
	    p.SmtpPort,
	    p.UseSsl,
	    p.SmtpUsername,
	    p.SmtpPasswordEnc,
	    COALESCE(NULLIF(p.FromName,''), NULLIF(p.ProfileName,'')) AS FromName,
	    p.FromEmail
	FROM dbo.CallBranchSmtpProfileLink l
	INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
	WHERE l.BranchId = @BranchId
	  AND l.IsActive = 1
	  AND p.IsActive = 1
	ORDER BY p.UpdatedAt DESC, p.ProfileId DESC;";

                        var row = await connection.QuerySingleOrDefaultAsync<SmtpSenderResolutionRow>(sql, new { BranchId = branchId.Value });
                        if (row != null && !string.IsNullOrWhiteSpace(row.SmtpServer))
                            return new SmtpSenderResolution
                            {
                                Source = "BranchProfile",
                                ProfileId = row.ProfileId,
                                ProfileName = row.ProfileName,
                                Sender = new SmtpSenderConfig
                                {
                                    SmtpServer = row.SmtpServer,
                                    SmtpPort = row.SmtpPort,
                                    UseSsl = row.UseSsl,
                                    SmtpUsername = row.SmtpUsername,
                                    SmtpPasswordEnc = row.SmtpPasswordEnc,
                                    FromName = row.FromName,
                                    FromEmail = row.FromEmail
                                }
                            };
                    }
                }
            }

            // 2) Department sender (Option B / Option A / Global)
            return await GetSmtpSenderResolutionForDepartmentAsync(deptId);
        }

        public async Task<bool> DepartmentSmtpProfilesOptionBEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasProfiles = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallSmtpProfile");
                if (!hasProfiles)
                    return false;

                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfileLink");
            }
        }

        public async Task<bool> BranchSmtpProfilesOptionBEnabledAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasProfiles = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallSmtpProfile");
                if (!hasProfiles)
                    return false;

                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchSmtpProfileLink");
            }
        }

        public async Task<bool> BranchSmtpProfileLinkIsConfiguredAsync(int branchId)
        {
            if (branchId <= 0)
                return false;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var enabled = await BranchSmtpProfilesOptionBEnabledAsync();
                if (!enabled)
                    return false;

                const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.CallBranchSmtpProfileLink l
    INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
    WHERE l.BranchId = @BranchId
      AND l.IsActive = 1
      AND p.IsActive = 1
)
THEN 1 ELSE 0 END;";

                var ok = await connection.ExecuteScalarAsync<int>(sql, new { BranchId = branchId });
                return ok == 1;
            }
        }

        public async Task<bool> DepartmentSmtpProfileLinkIsConfiguredAsync(int deptId)
        {
            if (deptId <= 0)
                return false;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var enabled = await DepartmentSmtpProfilesOptionBEnabledAsync();
                if (!enabled)
                    return false;

                const string sql = @"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.CallDepartmentSmtpProfileLink l
    INNER JOIN dbo.CallSmtpProfile p ON p.ProfileId = l.ProfileId
    WHERE l.DeptId = @DeptId
      AND l.IsActive = 1
      AND p.IsActive = 1
)
THEN 1 ELSE 0 END;";

                var ok = await connection.ExecuteScalarAsync<int>(sql, new { DeptId = deptId });
                return ok == 1;
            }
        }

        public async Task<bool> DepartmentLegacySmtpProfileIsConfiguredAsync(int deptId)
        {
            if (deptId <= 0)
                return false;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    return false;

                const string sql = @"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.CallDepartmentSmtpProfile WHERE DeptId = @DeptId) THEN 1 ELSE 0 END;";
                var ok = await connection.ExecuteScalarAsync<int>(sql, new { DeptId = deptId });
                return ok == 1;
            }
        }

        public async Task<bool> DepartmentNotificationRecipientsSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentNotificationRecipient");
            }
        }

        public async Task<bool> BranchNotificationRecipientsSchemaExistsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchNotificationRecipient");
            }
        }

        public async Task<CallDepartmentNotificationRecipientItem> GetDepartmentNotificationRecipientAsync(int deptId)
        {
            if (deptId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentNotificationRecipient"))
                    return null;

                const string sql = @"
SELECT TOP 1
    r.DeptId,
    d.Name AS DepartmentName,
    r.RecipientEmails,
    r.EscalationEmails,
    r.IsActive,
    r.UpdatedAt,
    r.UpdatedByUserId
FROM dbo.CallDepartmentNotificationRecipient r
LEFT JOIN dbo.Department d ON d.DeptId = r.DeptId
WHERE r.DeptId = @DeptId
  AND r.IsActive = 1
ORDER BY r.UpdatedAt DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallDepartmentNotificationRecipientItem>(sql, new { DeptId = deptId });
            }
        }

        public async Task UpsertDepartmentNotificationRecipientAsync(
            int deptId,
            string recipientEmails,
            string escalationEmails,
            int? updatedByUserId)
        {
            if (deptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deptId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentNotificationRecipient"))
                    throw new InvalidOperationException("CallDepartmentNotificationRecipient table is not installed in this database yet.");

                const string sql = @"
MERGE dbo.CallDepartmentNotificationRecipient AS tgt
USING (SELECT @DeptId AS DeptId) AS src
ON tgt.DeptId = src.DeptId
WHEN MATCHED THEN
    UPDATE SET
        RecipientEmails = @RecipientEmails,
        EscalationEmails = @EscalationEmails,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT (DeptId, RecipientEmails, EscalationEmails, IsActive, UpdatedByUserId)
    VALUES (@DeptId, @RecipientEmails, @EscalationEmails, 1, @UpdatedByUserId);";

                await connection.ExecuteAsync(sql, new
                {
                    DeptId = deptId,
                    RecipientEmails = (recipientEmails ?? string.Empty).Trim(),
                    EscalationEmails = (escalationEmails ?? string.Empty).Trim(),
                    UpdatedByUserId = updatedByUserId
                });
            }
        }

        public async Task<CallBranchNotificationRecipientItem> GetBranchNotificationRecipientAsync(int branchId)
        {
            if (branchId <= 0)
                return null;

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchNotificationRecipient"))
                    return null;

                const string sql = @"
SELECT TOP 1
    r.BranchId,
    b.Name AS BranchName,
    r.RecipientEmails,
    r.EscalationEmails,
    r.IsActive,
    r.UpdatedAt,
    r.UpdatedByUserId
FROM dbo.CallBranchNotificationRecipient r
LEFT JOIN dbo.Branch b ON b.BranchId = r.BranchId
WHERE r.BranchId = @BranchId
  AND r.IsActive = 1
ORDER BY r.UpdatedAt DESC;";

                return await connection.QuerySingleOrDefaultAsync<CallBranchNotificationRecipientItem>(sql, new { BranchId = branchId });
            }
        }

        public async Task UpsertBranchNotificationRecipientAsync(
            int branchId,
            string recipientEmails,
            string escalationEmails,
            int? updatedByUserId)
        {
            if (branchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(branchId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchNotificationRecipient"))
                    throw new InvalidOperationException("CallBranchNotificationRecipient table is not installed in this database yet.");

                const string sql = @"
MERGE dbo.CallBranchNotificationRecipient AS tgt
USING (SELECT @BranchId AS BranchId) AS src
ON tgt.BranchId = src.BranchId
WHEN MATCHED THEN
    UPDATE SET
        RecipientEmails = @RecipientEmails,
        EscalationEmails = @EscalationEmails,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedByUserId)
    VALUES (@BranchId, @RecipientEmails, @EscalationEmails, 1, @UpdatedByUserId);";

                await connection.ExecuteAsync(sql, new
                {
                    BranchId = branchId,
                    RecipientEmails = (recipientEmails ?? string.Empty).Trim(),
                    EscalationEmails = (escalationEmails ?? string.Empty).Trim(),
                    UpdatedByUserId = updatedByUserId
                });
            }
        }

        public async Task<List<CallSmtpProfileItem>> GetSmtpProfilesAsync(bool activeOnly = true)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string sql = @"
SELECT
    ProfileId,
    ProfileName,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    IsActive,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallSmtpProfile
WHERE (@ActiveOnly = 0 OR IsActive = 1)
ORDER BY ProfileName;";

                var rows = await connection.QueryAsync<CallSmtpProfileItem>(sql, new { ActiveOnly = activeOnly ? 1 : 0 });
                return rows.ToList();
            }
        }

        public async Task<CallSmtpProfileItem> GetCallSmtpProfileByIdAsync(int profileId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string sql = @"
SELECT
    ProfileId,
    ProfileName,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    IsActive,
    UpdatedAt,
    UpdatedByUserId
FROM dbo.CallSmtpProfile
WHERE ProfileId = @ProfileId;";
                var rows = await connection.QueryAsync<CallSmtpProfileItem>(sql, new { ProfileId = profileId });
                return rows.FirstOrDefault();
            }
        }

        public async Task SetCallSmtpProfileIsActiveAsync(int profileId, bool isActive, int? updatedByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string sql = @"
UPDATE dbo.CallSmtpProfile
SET IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME(), UpdatedByUserId = @UpdatedByUserId
WHERE ProfileId = @ProfileId;";
                await connection.ExecuteAsync(sql, new { ProfileId = profileId, IsActive = isActive, UpdatedByUserId = updatedByUserId });
            }
        }

        public sealed class CallSmtpProfileUsage
        {
            public List<string> DepartmentLinks { get; set; } = new List<string>();
            public List<string> BranchLinks { get; set; } = new List<string>();
            public bool IsReferenced => DepartmentLinks.Count > 0 || BranchLinks.Count > 0;
        }

        public async Task<CallSmtpProfileUsage> GetCallSmtpProfileUsageAsync(int profileId)
        {
            var usage = new CallSmtpProfileUsage();
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string deptSql = @"
SELECT d.Name
FROM dbo.CallDepartmentSmtpProfileLink l
INNER JOIN dbo.Department d ON d.DeptId = l.DeptId
WHERE l.ProfileId = @ProfileId AND l.IsActive = 1;";
                const string branchSql = @"
SELECT b.Name
FROM dbo.CallBranchSmtpProfileLink l
INNER JOIN dbo.Branch b ON b.BranchId = l.BranchId
WHERE l.ProfileId = @ProfileId AND l.IsActive = 1;";
                usage.DepartmentLinks.AddRange(await connection.QueryAsync<string>(deptSql, new { ProfileId = profileId }));
                usage.BranchLinks.AddRange(await connection.QueryAsync<string>(branchSql, new { ProfileId = profileId }));
            }
            return usage;
        }

        public async Task<int> UpsertSmtpProfileAsync(CallSmtpProfileItem profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (profile.ProfileId > 0)
                {
                    const string updateSql = @"
UPDATE dbo.CallSmtpProfile
SET
    ProfileName = @ProfileName,
    SmtpServer = @SmtpServer,
    SmtpPort = @SmtpPort,
    UseSsl = @UseSsl,
    SmtpUsername = @SmtpUsername,
    SmtpPasswordEnc = @SmtpPasswordEnc,
    FromName = @FromName,
    FromEmail = @FromEmail,
    IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedByUserId = @UpdatedByUserId
WHERE ProfileId = @ProfileId;";

                    await connection.ExecuteAsync(updateSql, profile);
                    return profile.ProfileId;
                }

                const string insertSql = @"
INSERT dbo.CallSmtpProfile
(
    ProfileName,
    SmtpServer,
    SmtpPort,
    UseSsl,
    SmtpUsername,
    SmtpPasswordEnc,
    FromName,
    FromEmail,
    IsActive,
    UpdatedByUserId
)
VALUES
(
    @ProfileName,
    @SmtpServer,
    @SmtpPort,
    @UseSsl,
    @SmtpUsername,
    @SmtpPasswordEnc,
    @FromName,
    @FromEmail,
    @IsActive,
    @UpdatedByUserId
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                return await connection.ExecuteScalarAsync<int>(insertSql, profile);
            }
        }

        public async Task UpsertDepartmentSmtpProfileLinkAsync(int deptId, int profileId, int? updatedByUserId)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                const string sql = @"
MERGE dbo.CallDepartmentSmtpProfileLink AS tgt
USING (SELECT @DeptId AS DeptId, @ProfileId AS ProfileId) AS src
ON tgt.DeptId = src.DeptId
WHEN MATCHED THEN
    UPDATE SET
        ProfileId = src.ProfileId,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT (DeptId, ProfileId, IsActive, UpdatedByUserId)
    VALUES (src.DeptId, src.ProfileId, 1, @UpdatedByUserId);";

                await connection.ExecuteAsync(sql, new { DeptId = deptId, ProfileId = profileId, UpdatedByUserId = updatedByUserId });
            }
        }

        public async Task UpsertBranchSmtpProfileLinkAsync(int branchId, int profileId, int? updatedByUserId)
        {
            if (branchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(branchId));

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchSmtpProfileLink"))
                    throw new InvalidOperationException("CallBranchSmtpProfileLink table is not installed in this database yet.");

                const string sql = @"
MERGE dbo.CallBranchSmtpProfileLink AS tgt
USING (SELECT @BranchId AS BranchId, @ProfileId AS ProfileId) AS src
ON tgt.BranchId = src.BranchId
WHEN MATCHED THEN
    UPDATE SET
        ProfileId = src.ProfileId,
        IsActive = 1,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedByUserId = @UpdatedByUserId
WHEN NOT MATCHED THEN
    INSERT (BranchId, ProfileId, IsActive, UpdatedByUserId)
    VALUES (src.BranchId, src.ProfileId, 1, @UpdatedByUserId);";

                await connection.ExecuteAsync(sql, new { BranchId = branchId, ProfileId = profileId, UpdatedByUserId = updatedByUserId });
            }
        }

        public async Task<List<CallDepartmentSmtpProfileRow>> GetDepartmentSmtpProfilesOptionBAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var nrExists = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentNotificationRecipient");

                var sql = @"
SELECT
    d.DeptId,
    d.Name AS DepartmentName," +
                          (nrExists
                              ? "\n    nr.RecipientEmails,\n    nr.EscalationEmails,"
                              : "\n    CAST(NULL AS nvarchar(2000)) AS RecipientEmails,\n    CAST(NULL AS nvarchar(2000)) AS EscalationEmails,") +
@"
    l.ProfileId,
    p.ProfileName,
    p.SmtpServer,
    p.SmtpPort,
    p.UseSsl,
    p.SmtpUsername,
    p.FromEmail,
    p.UpdatedAt
FROM dbo.Department d
LEFT JOIN dbo.CallDepartmentSmtpProfileLink l
    ON l.DeptId = d.DeptId AND l.IsActive = 1
LEFT JOIN dbo.CallSmtpProfile p
    ON p.ProfileId = l.ProfileId AND p.IsActive = 1
" + (nrExists ? "LEFT JOIN dbo.CallDepartmentNotificationRecipient nr ON nr.DeptId = d.DeptId AND nr.IsActive = 1" : "") + @"
WHERE d.Active = 1
ORDER BY d.Name;";

                var rows = await connection.QueryAsync<CallDepartmentSmtpProfileRow>(sql);
                return rows.ToList();
            }
        }

        public async Task<List<CallBranchSmtpProfileRow>> GetBranchSmtpProfilesOptionBAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var nrExists = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallBranchNotificationRecipient");

                var sql = @"
SELECT
    b.BranchId,
    CASE
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS BranchName,
    c.Name AS CompanyName,
    d.Name AS DepartmentName," +
                          (nrExists
                              ? "\n    nr.RecipientEmails,\n    nr.EscalationEmails,"
                              : "\n    CAST(NULL AS nvarchar(2000)) AS RecipientEmails,\n    CAST(NULL AS nvarchar(2000)) AS EscalationEmails,") +
@"
    l.ProfileId,
    p.ProfileName,
    p.SmtpServer,
    p.SmtpPort,
    p.UseSsl,
    p.SmtpUsername,
    p.FromEmail,
    p.UpdatedAt
FROM dbo.Branch b
OUTER APPLY (
    SELECT TOP 1 c.Name AS Name
    FROM   dbo.BranchDepartmentCompany bdc
    JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
    WHERE  bdc.BranchID = b.BranchId
    ORDER BY bdc.BranchDeptCompanyID
) c
OUTER APPLY (
    SELECT TOP 1 d.Name AS Name
    FROM   dbo.BranchDepartmentCompany bdc
    JOIN   dbo.Department d ON bdc.DepartmentID = d.DeptId
    WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
    ORDER BY bdc.BranchDeptCompanyID
) d
LEFT JOIN dbo.CallBranchSmtpProfileLink l
    ON l.BranchId = b.BranchId AND l.IsActive = 1
LEFT JOIN dbo.CallSmtpProfile p
    ON p.ProfileId = l.ProfileId AND p.IsActive = 1
" + (nrExists ? "LEFT JOIN dbo.CallBranchNotificationRecipient nr ON nr.BranchId = b.BranchId AND nr.IsActive = 1" : "") + @"
WHERE ISNULL(b.Active, 1) = 1
ORDER BY c.Name, b.Name, b.BranchId;";

                var rows = await connection.QueryAsync<CallBranchSmtpProfileRow>(sql);
                return rows.ToList();
            }
        }

        public async Task<List<CallDepartmentSmtpProfileRow>> GetDepartmentSmtpProfilesLegacyRowsAsync()
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentSmtpProfile"))
                    return new List<CallDepartmentSmtpProfileRow>();

                var nrExists = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallDepartmentNotificationRecipient");

                var sql = @"
SELECT
    ISNULL(d.DeptId, ISNULL(p.DeptId, 0)) AS DeptId,
    ISNULL(d.Name, p.DepartmentName) AS DepartmentName," +
                          (nrExists
                              ? "\n    nr.RecipientEmails,\n    nr.EscalationEmails,"
                              : "\n    CAST(NULL AS nvarchar(2000)) AS RecipientEmails,\n    CAST(NULL AS nvarchar(2000)) AS EscalationEmails,") +
@"
    p.ProfileId,
    '(Legacy per-dept)' AS ProfileName,
    p.SmtpServer,
    p.SmtpPort,
    p.UseSsl,
    p.SmtpUsername,
    p.FromEmail,
    p.UpdatedAt
FROM dbo.CallDepartmentSmtpProfile p
LEFT JOIN dbo.Department d ON d.DeptId = p.DeptId
" + (nrExists ? "LEFT JOIN dbo.CallDepartmentNotificationRecipient nr ON nr.DeptId = d.DeptId AND nr.IsActive = 1" : "") + @"
ORDER BY ISNULL(d.Name, p.DepartmentName);";

                var rows = await connection.QueryAsync<CallDepartmentSmtpProfileRow>(sql);
                return rows?.ToList() ?? new List<CallDepartmentSmtpProfileRow>();
            }
        }
    }
}

