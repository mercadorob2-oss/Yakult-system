using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    public class RoleRepository
    {
        private readonly string _connectionString;

        public RoleRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Load all employees with HasAccount flag
        /// </summary>
        public async Task<List<EmployeeDto>> GetEmployeesWithAccountStatusAsync()
        {
            var employees = new List<EmployeeDto>();

            string sql = @"
                SELECT 
                    e.EmpId,
                    e.Name,
                    e.EmployeeNumber,
                    e.Position,
                    e.Active,
                    e.ComId,
                    e.DeptId,
                    e.BranchId,
                    c.Name AS CompanyName,
                    d.Name AS DepartmentName,
                    b.Name AS BranchName,
                    u.UserId,
                    CASE WHEN u.UserId IS NOT NULL THEN 1 ELSE 0 END AS HasAccount
                FROM dbo.Employee e
                LEFT JOIN dbo.[User] u ON e.EmpId = u.EmpId
                LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new EmployeeDto
                            {
                                EmpId = reader.GetInt32(reader.GetOrdinal("EmpId")),
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                EmployeeNumber = reader.IsDBNull(reader.GetOrdinal("EmployeeNumber")) ? null : reader.GetString(reader.GetOrdinal("EmployeeNumber")),
                                Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
                                Active = reader.GetBoolean(reader.GetOrdinal("Active")),
                                CompanyId = reader.GetInt32(reader.GetOrdinal("ComId")),
                                DepartmentId = reader.GetInt32(reader.GetOrdinal("DeptId")),
                                BranchId = reader.GetInt32(reader.GetOrdinal("BranchId")),
                                CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                                DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                                UserId = reader.IsDBNull(reader.GetOrdinal("UserId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("UserId")),
                                HasAccount = reader.GetInt32(reader.GetOrdinal("HasAccount")) == 1
                            });
                        }
                    }
                }
            }

            return employees;
        }

        /// <summary>
        /// Load all active roles from dbo.Role
        /// </summary>
        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            var roles = new List<RoleDto>();

            string sql = @"
                SELECT RoleId, RoleName, Description, IsActive, DateCreated
                FROM dbo.Role
                WHERE IsActive = 1
                ORDER BY RoleName";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            roles.Add(new RoleDto
                            {
                                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                                RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated"))
                            });
                        }
                    }
                }
            }

            return roles;
        }

        /// <summary>
        /// Load roles assigned to a specific user
        /// </summary>
        public async Task<List<UserRoleDto>> GetUserRolesAsync(int userId)
        {
            var userRoles = new List<UserRoleDto>();

            string sql = @"
                SELECT 
                    ur.UserId,
                    ur.RoleId,
                    r.RoleName,
                    ur.DateAssigned
                FROM dbo.UserRole ur
                INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY r.RoleName";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            userRoles.Add(new UserRoleDto
                            {
                                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                                RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                                DateAssigned = reader.GetDateTime(reader.GetOrdinal("DateAssigned"))
                            });
                        }
                    }
                }
            }

            return userRoles;
        }

        /// <summary>
        /// Get user account details by employee ID
        /// </summary>
        public async Task<UserAccountDto> GetUserAccountByEmployeeIdAsync(int empId)
        {
            string sql = @"
                SELECT 
                    u.UserId,
                    u.Name,
                    u.EmailAddress,
                    u.IsActive,
                    u.IsDeveloper,
                    u.EmpId,
                    e.Name AS EmployeeName,
                    u.IsTemporaryPassword,
                    u.MustChangePassword,
                    u.LastLoginDate,
                    u.DateCreated
                FROM dbo.[User] u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                WHERE u.EmpId = @EmpId";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var account = new UserAccountDto
                            {
                                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                EmailAddress = reader.GetString(reader.GetOrdinal("EmailAddress")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                IsDeveloper = reader.GetBoolean(reader.GetOrdinal("IsDeveloper")),
                                EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("EmpId")),
                                EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                                IsTemporaryPassword = reader.GetBoolean(reader.GetOrdinal("IsTemporaryPassword")),
                                MustChangePassword = reader.GetBoolean(reader.GetOrdinal("MustChangePassword")),
                                LastLoginDate = reader.IsDBNull(reader.GetOrdinal("LastLoginDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("LastLoginDate")),
                                DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated"))
                            };

                            // Load roles
                            account.Roles = await GetUserRoleNamesAsync(account.UserId);

                            return account;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get role names for a user
        /// </summary>
        private async Task<List<string>> GetUserRoleNamesAsync(int userId)
        {
            var roleNames = new List<string>();

            string sql = @"
                SELECT r.RoleName
                FROM dbo.UserRole ur
                INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                WHERE ur.UserId = @UserId";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            roleNames.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return roleNames;
        }

        /// <summary>
        /// Sync user roles: insert missing, delete unchecked
        /// </summary>
        public async Task SyncUserRolesAsync(int userId, List<int> selectedRoleIds)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // Delete roles not in selected list
                        string deleteSql = @"
                            DELETE FROM dbo.UserRole
                            WHERE UserId = @UserId
                            AND RoleId NOT IN (SELECT value FROM STRING_SPLIT(@RoleIds, ','))";

                        using (var cmd = new SqlCommand(deleteSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@RoleIds", selectedRoleIds.Count > 0 ? string.Join(",", selectedRoleIds) : "-1");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Insert new roles
                        foreach (var roleId in selectedRoleIds)
                        {
                            string insertSql = @"
                                IF NOT EXISTS (SELECT 1 FROM dbo.UserRole WHERE UserId = @UserId AND RoleId = @RoleId)
                                BEGIN
                                    INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned)
                                    VALUES (@UserId, @RoleId, GETDATE())
                                END";

                            using (var cmd = new SqlCommand(insertSql, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@RoleId", roleId);
                                await cmd.ExecuteNonQueryAsync();
                            }
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

        /// <summary>
        /// Create user account from employee
        /// </summary>
        public async Task<int> CreateUserAccountAsync(int empId, string username, string tempPassword)
        {
            // Generate unique email if needed
            string email = $"user{empId}@yakult.local";

            // Hash password
            byte[] salt = Helpers.PasswordHelper.GenerateSalt();
            byte[] hash = Helpers.PasswordHelper.HashPassword(tempPassword, salt);

            string sql = @"
                INSERT INTO dbo.[User] (
                    Name, EmailAddress, DateCreated, IsDeveloper, EmpId,
                    PasswordHash, PasswordSalt, IsTemporaryPassword, MustChangePassword, IsActive
                )
                VALUES (
                    @Name, @EmailAddress, GETDATE(), 0, @EmpId,
                    @PasswordHash, @PasswordSalt, 1, 1, 1
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", username);
                    cmd.Parameters.AddWithValue("@EmailAddress", email);
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    cmd.Parameters.AddWithValue("@PasswordHash", hash);
                    cmd.Parameters.AddWithValue("@PasswordSalt", salt);

                    var userId = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(userId);
                }
            }
        }

        /// <summary>
        /// Reset user password
        /// </summary>
        public async Task ResetPasswordAsync(int userId, string newPassword)
        {
            byte[] salt = Helpers.PasswordHelper.GenerateSalt();
            byte[] hash = Helpers.PasswordHelper.HashPassword(newPassword, salt);

            string sql = @"
                UPDATE dbo.[User]
                SET PasswordHash = @PasswordHash,
                    PasswordSalt = @PasswordSalt,
                    IsTemporaryPassword = 1,
                    MustChangePassword = 1
                WHERE UserId = @UserId";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@PasswordHash", hash);
                    cmd.Parameters.AddWithValue("@PasswordSalt", salt);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Toggle user account active status
        /// </summary>
        public async Task ToggleUserActiveStatusAsync(int userId, bool isActive)
        {
            string sql = @"
                UPDATE dbo.[User]
                SET IsActive = @IsActive
                WHERE UserId = @UserId";

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@IsActive", isActive);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
