using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for system email operations
    /// </summary>
    public class EmailRepository
    {
        private static async Task<bool> TableExistsAsync(SqlConnection con, string fullTableName)
        {
            string schemaName = "dbo";
            string tableName = fullTableName;

            // Accept "dbo.Table" or "Table"
            if (!string.IsNullOrWhiteSpace(fullTableName) && fullTableName.Contains("."))
            {
                var parts = fullTableName.Split(new[] { '.' }, 2);
                if (parts.Length == 2)
                {
                    schemaName = parts[0];
                    tableName = parts[1];
                }
            }

            using (var cmd = new SqlCommand(@"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.objects o
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE s.name = @SchemaName
                      AND o.name = @TableName
                      AND o.type = 'U'
                ) THEN 1 ELSE 0 END", con))
            {
                cmd.Parameters.AddWithValue("@SchemaName", schemaName);
                cmd.Parameters.AddWithValue("@TableName", tableName);
                var result = await cmd.ExecuteScalarAsync();
                return result != null && Convert.ToInt32(result) == 1;
            }
        }

        #region EmailAddress Methods

        /// <summary>
        /// Gets all active email addresses
        /// </summary>
        public async Task<List<EmailAddressDto>> GetEmailAddressesAsync(bool activeOnly = true)
        {
            var results = new List<EmailAddressDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT EmailId, EmailAddress, DisplayName, IsActive,
                           DateCreated, CreatedByUserId,
                           CAST(NULL AS datetime2(2)) AS DateModified,
                           CAST(NULL AS int) AS ModifiedByUserId
                    FROM dbo.EmailAddress";

                if (activeOnly)
                    sql += " WHERE IsActive = 1";

                sql += " ORDER BY DisplayName, EmailAddress";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new EmailAddressDto
                        {
                            EmailId = reader.GetInt32(0),
                            EmailAddress = reader.GetString(1),
                            DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            DateCreated = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                            CreatedByUserId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                            DateModified = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                            ModifiedByUserId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets a single email address by ID
        /// </summary>
        public async Task<EmailAddressDto> GetEmailAddressByIdAsync(int emailId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    SELECT EmailId, EmailAddress, DisplayName, IsActive,
                           DateCreated, CreatedByUserId,
                           CAST(NULL AS datetime2(2)) AS DateModified,
                           CAST(NULL AS int) AS ModifiedByUserId
                    FROM dbo.EmailAddress
                    WHERE EmailId = @EmailId", con))
                {
                    cmd.Parameters.AddWithValue("@EmailId", emailId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new EmailAddressDto
                            {
                                EmailId = reader.GetInt32(0),
                                EmailAddress = reader.GetString(1),
                                DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsActive = reader.GetBoolean(3),
                                DateCreated = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                CreatedByUserId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                DateModified = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                ModifiedByUserId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Saves or updates an email address
        /// </summary>
        public async Task<int> SaveEmailAddressAsync(EmailAddressDto email)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (email.EmailId > 0)
                {
                    // Update existing
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.EmailAddress
                        SET EmailAddress = @EmailAddress,
                            DisplayName = @DisplayName,
                            IsActive = @IsActive
                        WHERE EmailId = @EmailId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmailId", email.EmailId);
                        cmd.Parameters.AddWithValue("@EmailAddress", email.EmailAddress);
                        cmd.Parameters.AddWithValue("@DisplayName", (object)email.DisplayName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsActive", email.IsActive);

                        await cmd.ExecuteNonQueryAsync();
                        return email.EmailId;
                    }
                }
                else
                {
                    // Insert new
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive, DateCreated, CreatedByUserId)
                        OUTPUT INSERTED.EmailId
                        VALUES (@EmailAddress, @DisplayName, @IsActive, @DateCreated, @CreatedByUserId)", con))
                    {
                        cmd.Parameters.AddWithValue("@EmailAddress", email.EmailAddress);
                        cmd.Parameters.AddWithValue("@DisplayName", (object)email.DisplayName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsActive", email.IsActive);
                        cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                        cmd.Parameters.AddWithValue("@CreatedByUserId", (object)email.CreatedByUserId ?? DBNull.Value);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Permanently deletes an email address by ID.
        /// </summary>
        public async Task DeleteEmailAddressAsync(int emailId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand("DELETE FROM dbo.EmailAddress WHERE EmailId = @EmailId", con))
                {
                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Returns every record in other tables that still points at this email address.
        /// Used to explain why a hard delete is blocked and what an unlink would change.
        /// </summary>
        public async Task<List<EmailAddressReferenceDto>> GetEmailAddressReferencesAsync(int emailId)
        {
            var refs = new List<EmailAddressReferenceDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (await TableExistsAsync(con, "dbo.SystemSmtpProfile"))
                {
                    await ReadRefsAsync(con,
                        "SELECT ProfileName, IsActive FROM dbo.SystemSmtpProfile WHERE FromEmailId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "SMTP Profile",
                            Description = r.GetString(0) + (r.GetBoolean(1) ? "  (active)" : "  (inactive)"),
                            OnUnlink = "The SMTP profile will be DELETED (its From-address is required and cannot be blanked). Email-log rows that used it are kept but lose the profile link.",
                            RequiresSmtpDeletion = true
                        }, refs);
                }

                if (await TableExistsAsync(con, "dbo.EmployeeEmail"))
                {
                    await ReadRefsAsync(con, @"
                        SELECT emp.Name, ee.EmailRole, ee.IsPrimary
                        FROM dbo.EmployeeEmail ee
                        INNER JOIN dbo.Employee emp ON emp.EmpId = ee.EmpId
                        WHERE ee.EmailId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "Employee Email",
                            Description = r.GetString(0)
                                          + (r.IsDBNull(1) ? "" : "  —  " + r.GetString(1))
                                          + (!r.IsDBNull(2) && r.GetBoolean(2) ? "  (primary)" : ""),
                            OnUnlink = "The employee-to-email link row will be removed."
                        }, refs);
                }

                if (await TableExistsAsync(con, "dbo.Branch"))
                {
                    await ReadRefsAsync(con,
                        "SELECT Name FROM dbo.Branch WHERE EmailId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "Branch",
                            Description = r.GetString(0),
                            OnUnlink = "The branch's email field will be cleared."
                        }, refs);
                }

                if (await TableExistsAsync(con, "dbo.BranchEmail"))
                {
                    await ReadRefsAsync(con,
                        "SELECT CompanyName, BranchName FROM dbo.BranchEmail WHERE EmailAddressId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "Branch Email Mapping",
                            Description = r.GetString(0) + "  /  " + r.GetString(1),
                            OnUnlink = "The mapping's email field will be cleared."
                        }, refs);
                }

                if (await TableExistsAsync(con, "dbo.DepartmentEmail"))
                {
                    await ReadRefsAsync(con,
                        "SELECT CompanyName, DepartmentName FROM dbo.DepartmentEmail WHERE EmailAddressId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "Department Email Mapping",
                            Description = r.GetString(0) + "  /  " + r.GetString(1),
                            OnUnlink = "The mapping's email field will be cleared."
                        }, refs);
                }

                if (await TableExistsAsync(con, "dbo.DepartmentAccount"))
                {
                    await ReadRefsAsync(con,
                        "SELECT CompanyName, DepartmentName, BranchName FROM dbo.DepartmentAccount WHERE EmailAddressId = @id",
                        emailId, r => new EmailAddressReferenceDto
                        {
                            Category = "Department Account",
                            Description = r.GetString(0) + "  /  " + r.GetString(1) + "  /  " + r.GetString(2),
                            OnUnlink = "The account's email field will be cleared."
                        }, refs);
                }
            }

            return refs;
        }

        private static async Task ReadRefsAsync(
            SqlConnection con, string sql, int emailId,
            Func<SqlDataReader, EmailAddressReferenceDto> map, List<EmailAddressReferenceDto> sink)
        {
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@id", emailId);
                using (var r = (SqlDataReader)await cmd.ExecuteReaderAsync())
                {
                    while (await r.ReadAsync())
                        sink.Add(map(r));
                }
            }
        }

        /// <summary>
        /// Clears or removes every reference to the email address so a hard delete can proceed.
        /// Nullable foreign keys are set to NULL; the EmployeeEmail junction rows are deleted.
        /// SMTP profiles are only deleted when <paramref name="includeSmtpProfiles"/> is true
        /// (their FromEmailId column is NOT NULL, so it cannot simply be blanked).
        /// Runs in a single transaction.
        /// </summary>
        public async Task UnlinkEmailAddressAsync(int emailId, bool includeSmtpProfiles)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        await ExecInTxAsync(con, tx, emailId,
                            "IF OBJECT_ID('dbo.Branch','U') IS NOT NULL UPDATE dbo.Branch SET EmailId = NULL WHERE EmailId = @id");
                        await ExecInTxAsync(con, tx, emailId,
                            "IF OBJECT_ID('dbo.BranchEmail','U') IS NOT NULL UPDATE dbo.BranchEmail SET EmailAddressId = NULL WHERE EmailAddressId = @id");
                        await ExecInTxAsync(con, tx, emailId,
                            "IF OBJECT_ID('dbo.DepartmentEmail','U') IS NOT NULL UPDATE dbo.DepartmentEmail SET EmailAddressId = NULL WHERE EmailAddressId = @id");
                        await ExecInTxAsync(con, tx, emailId,
                            "IF OBJECT_ID('dbo.DepartmentAccount','U') IS NOT NULL UPDATE dbo.DepartmentAccount SET EmailAddressId = NULL WHERE EmailAddressId = @id");
                        await ExecInTxAsync(con, tx, emailId,
                            "IF OBJECT_ID('dbo.EmployeeEmail','U') IS NOT NULL DELETE FROM dbo.EmployeeEmail WHERE EmailId = @id");

                        if (includeSmtpProfiles)
                        {
                            // The SMTP profiles about to be deleted may themselves be referenced
                            // by email-log rows (FK_SystemEmailLog_SystemSmtpProfile, no cascade).
                            // Null those links first so the profile delete isn't blocked; the log
                            // history rows are kept. EmailTemplate.DefaultSmtpProfileId is
                            // ON DELETE SET NULL, so it needs no explicit handling here.
                            await ExecInTxAsync(con, tx, emailId, @"
                                IF OBJECT_ID('dbo.SystemEmailLog','U') IS NOT NULL
                                   AND OBJECT_ID('dbo.SystemSmtpProfile','U') IS NOT NULL
                                    UPDATE dbo.SystemEmailLog
                                    SET ProfileId = NULL
                                    WHERE ProfileId IN (
                                        SELECT ProfileId FROM dbo.SystemSmtpProfile WHERE FromEmailId = @id
                                    );");

                            await ExecInTxAsync(con, tx, emailId,
                                "IF OBJECT_ID('dbo.SystemSmtpProfile','U') IS NOT NULL DELETE FROM dbo.SystemSmtpProfile WHERE FromEmailId = @id");
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static async Task ExecInTxAsync(SqlConnection con, SqlTransaction tx, int emailId, string sql)
        {
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@id", emailId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        #region SystemSmtpProfile Methods

        /// <summary>
        /// Gets all SMTP profiles
        /// </summary>
        public async Task<List<SystemSmtpProfileDto>> GetSmtpProfilesAsync(bool activeOnly = true)
        {
            var results = new List<SystemSmtpProfileDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemSmtpProfile"))
                    return results;

                var sql = @"
                    SELECT p.ProfileId, p.ProfileName, p.SmtpServer, p.SmtpPort, p.UseSsl,
                           p.SmtpUsername, p.SmtpPasswordEnc, p.FromEmailId, p.IsActive,
                           e.EmailAddress AS FromEmailAddress, e.DisplayName AS FromDisplayName,
                           CAST(NULL AS datetime2(2)) AS DateCreated,
                           CAST(NULL AS int) AS CreatedByUserId,
                           p.UpdatedAt AS DateModified,
                           p.UpdatedByUserId AS ModifiedByUserId
                    FROM dbo.SystemSmtpProfile p
                    LEFT JOIN dbo.EmailAddress e ON p.FromEmailId = e.EmailId";

                if (activeOnly)
                    sql += " WHERE p.IsActive = 1";

                sql += " ORDER BY p.ProfileName";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SystemSmtpProfileDto
                        {
                            ProfileId = reader.GetInt32(0),
                            ProfileName = reader.GetString(1),
                            SmtpServer = reader.GetString(2),
                            SmtpPort = reader.GetInt32(3),
                            UseSsl = reader.GetBoolean(4),
                            SmtpUsername = reader.IsDBNull(5) ? null : reader.GetString(5),
                            SmtpPasswordEnc = reader.IsDBNull(6) ? null : (byte[])reader[6],
                            FromEmailId = reader.GetInt32(7),
                            IsActive = reader.GetBoolean(8),
                            FromEmailAddress = reader.IsDBNull(9) ? null : reader.GetString(9),
                            FromDisplayName = reader.IsDBNull(10) ? null : reader.GetString(10),
                            DateCreated = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                            CreatedByUserId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12),
                            DateModified = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13),
                            ModifiedByUserId = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets a single SMTP profile by ID
        /// </summary>
        public async Task<SystemSmtpProfileDto> GetSmtpProfileByIdAsync(int profileId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemSmtpProfile"))
                    return null;

                using (var cmd = new SqlCommand(@"
                    SELECT p.ProfileId, p.ProfileName, p.SmtpServer, p.SmtpPort, p.UseSsl,
                           p.SmtpUsername, p.SmtpPasswordEnc, p.FromEmailId, p.IsActive,
                           e.EmailAddress AS FromEmailAddress, e.DisplayName AS FromDisplayName,
                           CAST(NULL AS datetime2(2)) AS DateCreated,
                           CAST(NULL AS int) AS CreatedByUserId,
                           p.UpdatedAt AS DateModified,
                           p.UpdatedByUserId AS ModifiedByUserId
                    FROM dbo.SystemSmtpProfile p
                    LEFT JOIN dbo.EmailAddress e ON p.FromEmailId = e.EmailId
                    WHERE p.ProfileId = @ProfileId", con))
                {
                    cmd.Parameters.AddWithValue("@ProfileId", profileId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new SystemSmtpProfileDto
                            {
                                ProfileId = reader.GetInt32(0),
                                ProfileName = reader.GetString(1),
                                SmtpServer = reader.GetString(2),
                                SmtpPort = reader.GetInt32(3),
                                UseSsl = reader.GetBoolean(4),
                                SmtpUsername = reader.IsDBNull(5) ? null : reader.GetString(5),
                                SmtpPasswordEnc = reader.IsDBNull(6) ? null : (byte[])reader[6],
                                FromEmailId = reader.GetInt32(7),
                                IsActive = reader.GetBoolean(8),
                                FromEmailAddress = reader.IsDBNull(9) ? null : reader.GetString(9),
                                FromDisplayName = reader.IsDBNull(10) ? null : reader.GetString(10),
                                DateCreated = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                                CreatedByUserId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12),
                                DateModified = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13),
                                ModifiedByUserId = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Saves or updates an SMTP profile
        /// </summary>
        public async Task<int> SaveSmtpProfileAsync(SystemSmtpProfileDto profile)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemSmtpProfile"))
                    throw new InvalidOperationException("Missing table dbo.SystemSmtpProfile. Please apply the Email & SMTP database script/migration.");

                if (profile.ProfileId > 0)
                {
                    // Update existing
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.SystemSmtpProfile
                        SET ProfileName = @ProfileName,
                            SmtpServer = @SmtpServer,
                            SmtpPort = @SmtpPort,
                            UseSsl = @UseSsl,
                            SmtpUsername = @SmtpUsername,
                            SmtpPasswordEnc = @SmtpPasswordEnc,
                            FromEmailId = @FromEmailId,
                            IsActive = @IsActive,
                            UpdatedAt = @UpdatedAt,
                            UpdatedByUserId = @UpdatedByUserId
                        WHERE ProfileId = @ProfileId", con))
                    {
                        cmd.Parameters.AddWithValue("@ProfileId", profile.ProfileId);
                        cmd.Parameters.AddWithValue("@ProfileName", profile.ProfileName);
                        cmd.Parameters.AddWithValue("@SmtpServer", profile.SmtpServer);
                        cmd.Parameters.AddWithValue("@SmtpPort", profile.SmtpPort);
                        cmd.Parameters.AddWithValue("@UseSsl", profile.UseSsl);
                        cmd.Parameters.AddWithValue("@SmtpUsername", (object)profile.SmtpUsername ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SmtpPasswordEnc", (object)profile.SmtpPasswordEnc ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FromEmailId", profile.FromEmailId);
                        cmd.Parameters.AddWithValue("@IsActive", profile.IsActive);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@UpdatedByUserId", (object)profile.ModifiedByUserId ?? DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                        return profile.ProfileId;
                    }
                }
                else
                {
                    // Insert new
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.SystemSmtpProfile
                            (ProfileName, SmtpServer, SmtpPort, UseSsl, SmtpUsername, SmtpPasswordEnc,
                             FromEmailId, IsActive, UpdatedAt, UpdatedByUserId)
                        OUTPUT INSERTED.ProfileId
                        VALUES
                            (@ProfileName, @SmtpServer, @SmtpPort, @UseSsl, @SmtpUsername, @SmtpPasswordEnc,
                             @FromEmailId, @IsActive, @UpdatedAt, @UpdatedByUserId)", con))
                    {
                        cmd.Parameters.AddWithValue("@ProfileName", profile.ProfileName);
                        cmd.Parameters.AddWithValue("@SmtpServer", profile.SmtpServer);
                        cmd.Parameters.AddWithValue("@SmtpPort", profile.SmtpPort);
                        cmd.Parameters.AddWithValue("@UseSsl", profile.UseSsl);
                        cmd.Parameters.AddWithValue("@SmtpUsername", (object)profile.SmtpUsername ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SmtpPasswordEnc", (object)profile.SmtpPasswordEnc ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FromEmailId", profile.FromEmailId);
                        cmd.Parameters.AddWithValue("@IsActive", profile.IsActive);
                        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@UpdatedByUserId", (object)profile.CreatedByUserId ?? (object)profile.ModifiedByUserId ?? DBNull.Value);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Deletes an SMTP profile (soft delete by setting IsActive = false)
        /// </summary>
        public async Task DeleteSmtpProfileAsync(int profileId, int userId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemSmtpProfile"))
                    throw new InvalidOperationException("Missing table dbo.SystemSmtpProfile. Please apply the Email & SMTP database script/migration.");

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.SystemSmtpProfile
                    SET IsActive = 0,
                        UpdatedAt = @UpdatedAt,
                        UpdatedByUserId = @UpdatedByUserId
                    WHERE ProfileId = @ProfileId", con))
                {
                    cmd.Parameters.AddWithValue("@ProfileId", profileId);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@UpdatedByUserId", userId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        #endregion

        #region EmailTemplate Methods

        /// <summary>
        /// Gets all email templates
        /// </summary>
        public async Task<List<EmailTemplateDto>> GetEmailTemplatesAsync(bool activeOnly = true)
        {
            var results = new List<EmailTemplateDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT t.TemplateId, t.TemplateKey, t.SubjectTemplate, t.BodyTemplate, t.IsHtml, t.IsActive,
                           t.DateCreated,
                           CAST(NULL AS int) AS CreatedByUserId,
                           CAST(NULL AS datetime2(2)) AS DateModified,
                           CAST(NULL AS int) AS ModifiedByUserId,
                           t.DefaultSmtpProfileId,
                           p.ProfileName AS DefaultSmtpProfileName
                    FROM dbo.EmailTemplate t
                    LEFT JOIN dbo.SystemSmtpProfile p ON p.ProfileId = t.DefaultSmtpProfileId";

                if (activeOnly)
                    sql += " WHERE t.IsActive = 1";

                sql += " ORDER BY t.TemplateKey";

                using (var cmd = new SqlCommand(sql, con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new EmailTemplateDto
                        {
                            TemplateId = reader.GetInt32(0),
                            TemplateKey = reader.GetString(1),
                            SubjectTemplate = reader.GetString(2),
                            BodyTemplate = reader.GetString(3),
                            IsHtml = reader.GetBoolean(4),
                            IsActive = reader.GetBoolean(5),
                            DateCreated = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                            CreatedByUserId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                            DateModified = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                            ModifiedByUserId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                            DefaultSmtpProfileId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                            DefaultSmtpProfileName = reader.IsDBNull(11) ? null : reader.GetString(11)
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets a single email template by key
        /// </summary>
        public async Task<EmailTemplateDto> GetEmailTemplateByKeyAsync(string templateKey)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    SELECT t.TemplateId, t.TemplateKey, t.SubjectTemplate, t.BodyTemplate, t.IsHtml, t.IsActive,
                           t.DateCreated,
                           CAST(NULL AS int) AS CreatedByUserId,
                           CAST(NULL AS datetime2(2)) AS DateModified,
                           CAST(NULL AS int) AS ModifiedByUserId,
                           t.DefaultSmtpProfileId,
                           p.ProfileName AS DefaultSmtpProfileName
                    FROM dbo.EmailTemplate t
                    LEFT JOIN dbo.SystemSmtpProfile p ON p.ProfileId = t.DefaultSmtpProfileId
                    WHERE t.TemplateKey = @TemplateKey", con))
                {
                    cmd.Parameters.AddWithValue("@TemplateKey", templateKey);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new EmailTemplateDto
                            {
                                TemplateId = reader.GetInt32(0),
                                TemplateKey = reader.GetString(1),
                                SubjectTemplate = reader.GetString(2),
                                BodyTemplate = reader.GetString(3),
                                IsHtml = reader.GetBoolean(4),
                                IsActive = reader.GetBoolean(5),
                                DateCreated = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                CreatedByUserId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                DateModified = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                ModifiedByUserId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                DefaultSmtpProfileId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                                DefaultSmtpProfileName = reader.IsDBNull(11) ? null : reader.GetString(11)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Saves or updates an email template
        /// </summary>
        public async Task<int> SaveEmailTemplateAsync(EmailTemplateDto template)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (template.TemplateId > 0)
                {
                    // Update existing
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.EmailTemplate
                        SET TemplateKey = @TemplateKey,
                            SubjectTemplate = @SubjectTemplate,
                            BodyTemplate = @BodyTemplate,
                            IsHtml = @IsHtml,
                            IsActive = @IsActive,
                            DefaultSmtpProfileId = @DefaultSmtpProfileId
                        WHERE TemplateId = @TemplateId", con))
                    {
                        cmd.Parameters.AddWithValue("@TemplateId", template.TemplateId);
                        cmd.Parameters.AddWithValue("@TemplateKey", template.TemplateKey);
                        cmd.Parameters.AddWithValue("@SubjectTemplate", template.SubjectTemplate);
                        cmd.Parameters.AddWithValue("@BodyTemplate", template.BodyTemplate);
                        cmd.Parameters.AddWithValue("@IsHtml", template.IsHtml);
                        cmd.Parameters.AddWithValue("@IsActive", template.IsActive);
                        cmd.Parameters.AddWithValue("@DefaultSmtpProfileId", (object)template.DefaultSmtpProfileId ?? DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                        return template.TemplateId;
                    }
                }
                else
                {
                    // Insert new
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.EmailTemplate
                            (TemplateKey, SubjectTemplate, BodyTemplate, IsHtml, IsActive, DateCreated, DefaultSmtpProfileId)
                        OUTPUT INSERTED.TemplateId
                        VALUES
                            (@TemplateKey, @SubjectTemplate, @BodyTemplate, @IsHtml, @IsActive, @DateCreated, @DefaultSmtpProfileId)", con))
                    {
                        cmd.Parameters.AddWithValue("@TemplateKey", template.TemplateKey);
                        cmd.Parameters.AddWithValue("@SubjectTemplate", template.SubjectTemplate);
                        cmd.Parameters.AddWithValue("@BodyTemplate", template.BodyTemplate);
                        cmd.Parameters.AddWithValue("@IsHtml", template.IsHtml);
                        cmd.Parameters.AddWithValue("@IsActive", template.IsActive);
                        cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                        cmd.Parameters.AddWithValue("@DefaultSmtpProfileId", (object)template.DefaultSmtpProfileId ?? DBNull.Value);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Updates only the DefaultSmtpProfileId on a single email template.
        /// Pass null to clear the profile assignment.
        /// </summary>
        public async Task SetEmailTemplateSmtpProfileAsync(int templateId, int? profileId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.EmailTemplate
                    SET    DefaultSmtpProfileId = @ProfileId
                    WHERE  TemplateId = @TemplateId", con))
                {
                    cmd.Parameters.AddWithValue("@TemplateId", templateId);
                    cmd.Parameters.AddWithValue("@ProfileId", (object)profileId ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Applies the same DefaultSmtpProfileId to every row in dbo.EmailTemplate at once.
        /// Pass null to clear all profile assignments.
        /// </summary>
        public async Task SetAllEmailTemplatesSmtpProfileAsync(int? profileId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.EmailTemplate
                    SET    DefaultSmtpProfileId = @ProfileId", con))
                {
                    cmd.Parameters.AddWithValue("@ProfileId", (object)profileId ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Toggles only the IsActive flag on an email template.
        /// Does not touch any other columns.
        /// </summary>
        public async Task SetEmailTemplateIsActiveAsync(int templateId, bool isActive)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.EmailTemplate
                    SET    IsActive = @IsActive
                    WHERE  TemplateId = @TemplateId", con))
                {
                    cmd.Parameters.AddWithValue("@TemplateId", templateId);
                    cmd.Parameters.AddWithValue("@IsActive", isActive);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        #endregion

        #region EmployeeEmail Methods

        /// <summary>
        /// Gets all employee email mappings
        /// </summary>
        public async Task<List<EmployeeEmailDto>> GetEmployeeEmailsAsync(int? empId = null)
        {
            var results = new List<EmployeeEmailDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                var sql = @"
                    SELECT ee.EmpId, ee.EmailId, ee.EmailRole, ee.IsPrimary, ee.IsActive,
                           e.EmailAddress, e.DisplayName,
                           emp.Name AS EmployeeName
                    FROM dbo.EmployeeEmail ee
                    INNER JOIN dbo.EmailAddress e ON ee.EmailId = e.EmailId
                    LEFT JOIN dbo.Employee emp ON ee.EmpId = emp.EmpId
                    WHERE 1=1";

                if (empId.HasValue)
                    sql += " AND ee.EmpId = @EmpId";

                sql += " ORDER BY emp.Name, ee.IsPrimary DESC, e.EmailAddress";

                using (var cmd = new SqlCommand(sql, con))
                {
                    if (empId.HasValue)
                        cmd.Parameters.AddWithValue("@EmpId", empId.Value);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new EmployeeEmailDto
                            {
                                EmpId = reader.GetInt32(0),
                                EmailId = reader.GetInt32(1),
                                EmailRole = reader.GetString(2),
                                IsPrimary = reader.GetBoolean(3),
                                IsActive = reader.GetBoolean(4),
                                EmailAddress = reader.GetString(5),
                                DisplayName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                EmployeeName = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets primary email for an employee
        /// </summary>
        public async Task<string> GetEmployeePrimaryEmailAsync(int empId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // First try the dedicated EmployeeEmail/EmailAddress tables
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 e.EmailAddress
                    FROM dbo.EmployeeEmail ee
                    INNER JOIN dbo.EmailAddress e ON ee.EmailId = e.EmailId
                    WHERE ee.EmpId = @EmpId
                      AND ee.IsActive = 1
                      AND e.IsActive = 1
                    ORDER BY ee.IsPrimary DESC, ee.EmailId", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && !string.IsNullOrWhiteSpace(result.ToString()))
                        return result.ToString();
                }

                // Fallback: use the email from the linked User account
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 EmailAddress
                    FROM dbo.[User]
                    WHERE EmpId = @EmpId
                      AND IsActive = 1
                      AND EmailAddress IS NOT NULL
                      AND EmailAddress <> ''", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);

                    var result = await cmd.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }

        /// <summary>
        /// Returns the SMTP recipients for an employee using the parent/child email hierarchy:
        ///   Child  (priority) — personal email from dbo.EmployeeEmail (IsPrimary=1)
        ///   Parent (fallback) — department email from dbo.DepartmentEmail via the employee's
        ///                       Company + Department names
        /// Both addresses are included when both exist.
        /// Falls back to whichever one is present if only one exists.
        /// Returns an empty list if neither is set.
        /// </summary>
        public async Task<List<string>> GetEmployeeEmailRecipientsAsync(int empId)
        {
            var result = new List<string>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                // ── Child: personal email (IsPrimary=1) ─────────────────────────────
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 ea.EmailAddress
                    FROM dbo.EmployeeEmail ee
                    INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
                    WHERE ee.EmpId     = @EmpId
                      AND ee.IsPrimary = 1
                      AND ee.IsActive  = 1
                      AND ea.IsActive  = 1", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && !string.IsNullOrWhiteSpace(r.ToString()))
                        result.Add(r.ToString().Trim());
                }

                // ── Parent: department email via Employee → Company + Department ────
                using (var cmd = new SqlCommand(@"
                    SELECT de_ea.EmailAddress
                    FROM dbo.Employee e
                    INNER JOIN dbo.Company      c    ON c.ComId    = e.ComId
                    INNER JOIN dbo.Department   d    ON d.DeptId   = e.DeptId
                    INNER JOIN dbo.DepartmentEmail de
                           ON de.CompanyName    = c.Name
                          AND de.DepartmentName = d.Name
                    INNER JOIN dbo.EmailAddress de_ea ON de_ea.EmailId = de.EmailAddressId
                    WHERE e.EmpId       = @EmpId
                      AND de_ea.IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && !string.IsNullOrWhiteSpace(r.ToString()))
                    {
                        var deptEmail = r.ToString().Trim();
                        // Only add if not already in the list (personal email might match dept email)
                        if (!result.Any(e => string.Equals(e, deptEmail, StringComparison.OrdinalIgnoreCase)))
                            result.Add(deptEmail);
                    }
                }
            }

            return result;
        }

        #endregion

        #region Branch Email Methods

        /// <summary>
        /// Gets branch email address
        /// </summary>
        public async Task<string> GetBranchEmailAsync(int branchId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    SELECT e.EmailAddress
                    FROM dbo.Branch b
                    INNER JOIN dbo.EmailAddress e ON b.EmailId = e.EmailId
                    WHERE b.BranchId = @BranchId
                      AND e.IsActive = 1", con))
                {
                    cmd.Parameters.AddWithValue("@BranchId", branchId);

                    var result = await cmd.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }

        #endregion

        #region Email Log Methods

        /// <summary>
        /// Logs an email send attempt
        /// </summary>
        public async Task<int> LogEmailAsync(SystemEmailLogDto log)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemEmailLog"))
                    return 0;

                try
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.SystemEmailLog
                            (TemplateKey, Recipients, Subject, Status, ErrorMessage, ProfileId,
                             EntityType, EntityId, SentDate, SentByUserId)
                        OUTPUT INSERTED.LogId
                        VALUES
                            (@TemplateKey, @Recipients, @Subject, @Status, @ErrorMessage, @ProfileId,
                             @EntityType, @EntityId, @SentDate, @SentByUserId)", con))
                    {
                        cmd.Parameters.AddWithValue("@TemplateKey", (object)log.TemplateKey ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Recipients", (object)log.Recipients ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Subject", (object)log.Subject ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", log.Status);
                        cmd.Parameters.AddWithValue("@ErrorMessage", (object)log.ErrorMessage ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ProfileId", (object)log.ProfileId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntityType", (object)log.EntityType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntityId", (object)log.EntityId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SentDate", log.SentDate);
                        cmd.Parameters.AddWithValue("@SentByUserId", (object)log.SentByUserId ?? DBNull.Value);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
                catch (SqlException ex) when (ex.Message != null && ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Table not deployed in this DB yet.
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets email logs with pagination
        /// </summary>
        public async Task<List<SystemEmailLogDto>> GetEmailLogsAsync(int pageNumber = 1, int pageSize = 50)
        {
            var results = new List<SystemEmailLogDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemEmailLog"))
                    return results;

                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT l.LogId, l.TemplateKey, l.Recipients, l.Subject, l.Status, l.ErrorMessage,
                               l.ProfileId, p.ProfileName, l.EntityType, l.EntityId, l.SentDate, l.SentByUserId,
                               u.Name AS SentByUserName
                        FROM dbo.SystemEmailLog l
                        LEFT JOIN dbo.SystemSmtpProfile p ON l.ProfileId = p.ProfileId
                        LEFT JOIN dbo.[User] u ON l.SentByUserId = u.UserId
                        ORDER BY l.SentDate DESC
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY", con))
                    {
                        cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new SystemEmailLogDto
                                {
                                    LogId = reader.GetInt32(0),
                                    TemplateKey = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Recipients = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Subject = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    Status = reader.GetString(4),
                                    ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    ProfileId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                    ProfileName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    EntityType = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    EntityId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                    SentDate = reader.GetDateTime(10),
                                    SentByUserId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                                    SentByUserName = reader.IsDBNull(12) ? null : reader.GetString(12)
                                });
                            }
                        }
                    }
                }
                catch (SqlException ex) when (ex.Message != null && ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Table not deployed in this DB yet.
                    return new List<SystemEmailLogDto>();
                }
            }

            return results;
        }

        /// <summary>
        /// Gets email logs without paging (most recent first, capped at <paramref name="maxRows"/>).
        /// Used by the WPF Email Logs page, which does its own client-side filtering,
        /// summary-card counting and pagination over the full result set.
        /// </summary>
        public async Task<List<SystemEmailLogDto>> GetAllEmailLogsAsync(int maxRows = 2000)
        {
            var results = new List<SystemEmailLogDto>();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemEmailLog"))
                    return results;

                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP (@MaxRows)
                               l.LogId, l.TemplateKey, l.Recipients, l.Subject, l.Status, l.ErrorMessage,
                               l.ProfileId, p.ProfileName, l.EntityType, l.EntityId, l.SentDate, l.SentByUserId,
                               u.Name AS SentByUserName
                        FROM dbo.SystemEmailLog l
                        LEFT JOIN dbo.SystemSmtpProfile p ON l.ProfileId = p.ProfileId
                        LEFT JOIN dbo.[User] u ON l.SentByUserId = u.UserId
                        ORDER BY l.SentDate DESC", con))
                    {
                        cmd.Parameters.AddWithValue("@MaxRows", maxRows <= 0 ? 2000 : maxRows);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new SystemEmailLogDto
                                {
                                    LogId = reader.GetInt32(0),
                                    TemplateKey = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Recipients = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Subject = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    Status = reader.GetString(4),
                                    ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    ProfileId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                    ProfileName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    EntityType = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    EntityId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                                    SentDate = reader.GetDateTime(10),
                                    SentByUserId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                                    SentByUserName = reader.IsDBNull(12) ? null : reader.GetString(12)
                                });
                            }
                        }
                    }
                }
                catch (SqlException ex) when (ex.Message != null && ex.Message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Table not deployed in this DB yet.
                    return new List<SystemEmailLogDto>();
                }
            }

            return results;
        }

        #endregion
    }
}
