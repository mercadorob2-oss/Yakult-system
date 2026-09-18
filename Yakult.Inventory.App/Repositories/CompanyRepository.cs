using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for Company data access operations
    /// </summary>
    public class CompanyRepository
    {
        /// <summary>
        /// Constructor does NOT throw if connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public CompanyRepository()
        {
            // Connection string is accessed via DatabaseConfig at method execution time.
        }

        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }
        /// <summary>
        /// Creates a new company with optional departments and branches
        /// </summary>
        public async Task<int> CreateAsync(CompanyDto company)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        // Insert Company
                        int companyId;
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.Company (Name, Description, DateCreated, Createdby)
                            OUTPUT INSERTED.ComId
                            VALUES (@Name, @Desc, @DateCreated, @Createdby);
                        ", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Name", company.Name ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Desc", (object)company.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DateCreated", company.DateCreated);
                            cmd.Parameters.AddWithValue("@Createdby", company.CreatedByUserId);
                            
                            companyId = (int)await cmd.ExecuteScalarAsync();
                        }

                        // Insert Departments if any
                        var departmentIds = new List<int>();
                        if (company.Departments != null && company.Departments.Count > 0)
                        {
                            foreach (var dept in company.Departments)
                            {
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Department (Name, Description, DateCreated, Createdby)
                                    OUTPUT INSERTED.DeptId
                                    VALUES (@Name, @Desc, @DateCreated, @Createdby);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", dept.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)dept.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", dept.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", dept.CreatedByUserId);

                                    int deptId = (int)await cmd.ExecuteScalarAsync();
                                    departmentIds.Add(deptId);
                                }
                            }
                        }

                        // Insert Branches if any
                        if (company.Branches != null && company.Branches.Count > 0)
                        {
                            int? defaultDeptId = departmentIds.Count > 0 ? departmentIds[0] : (int?)null;
                            
                            foreach (var branch in company.Branches)
                            {
                                int branchId;
                                using (var cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Branch (Name, Description, DateCreated, Createdby)
                                    OUTPUT INSERTED.BranchId
                                    VALUES (@Name, @Desc, @DateCreated, @Createdby);
                                ", con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name", branch.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Desc", (object)branch.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@DateCreated", branch.DateCreated);
                                    cmd.Parameters.AddWithValue("@Createdby", branch.CreatedByUserId);

                                    branchId = (int)await cmd.ExecuteScalarAsync();
                                }

                                using (var bdcCmd = new SqlCommand(@"
                                    INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                                    VALUES (@BranchId, @DeptId, @ComId);
                                ", con, tx))
                                {
                                    bdcCmd.Parameters.AddWithValue("@BranchId", branchId);
                                    bdcCmd.Parameters.AddWithValue("@DeptId", defaultDeptId.HasValue ? (object)defaultDeptId.Value : DBNull.Value);
                                    bdcCmd.Parameters.AddWithValue("@ComId", companyId);
                                    await bdcCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        await Task.Run(() => tx.Commit());
                        Logger.LogInfo($"Company created successfully: {company.Name} (ID: {companyId})");
                        
                        return companyId;
                    }
                    catch (Exception ex)
                    {
                        await Task.Run(() => tx.Rollback());
                        Logger.LogError($"Failed to create company: {company.Name}", ex);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all companies
        /// </summary>
        public async Task<List<CompanyDto>> GetAllAsync()
        {
            var companies = new List<CompanyDto>();

            using (var con = new SqlConnection(GetConnectionString()))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        companies.Add(new CompanyDto
                        {
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            return companies;
        }

        /// <summary>
        /// Deactivates a company and all related entities (departments, branches)
        /// </summary>
        public (bool Success, int DepartmentsAffected, int BranchesAffected, int EmployeesAffected, string ErrorMessage)
            DeactivateCompany(int comId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var command = new SqlCommand("sp_DeactivateCompanyCascade", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ComId", comId);
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@IsActivating", 0); // Deactivate

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return (
                                    true,
                                    reader.GetInt32(0), // DepartmentsAffected
                                    reader.GetInt32(1), // BranchesAffected
                                    reader.GetInt32(2), // EmployeesAffected
                                    null
                                );
                            }
                        }
                    }
                }
                return (false, 0, 0, 0, "No data returned from stored procedure");
            }
            catch (Exception ex)
            {
                return (false, 0, 0, 0, ex.Message);
            }
        }

        /// <summary>
        /// Reactivates a company and all related entities
        /// </summary>
        public (bool Success, int DepartmentsAffected, int BranchesAffected, int EmployeesAffected, string ErrorMessage)
            ActivateCompany(int comId, int userId)
        {
            try
            {
                using (var connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (var command = new SqlCommand("sp_DeactivateCompanyCascade", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@ComId", comId);
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@IsActivating", 1); // Activate

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return (
                                    true,
                                    reader.GetInt32(0),
                                    reader.GetInt32(1),
                                    reader.GetInt32(2),
                                    null
                                );
                            }
                        }
                    }
                }
                return (false, 0, 0, 0, "No data returned from stored procedure");
            }
            catch (Exception ex)
            {
                return (false, 0, 0, 0, ex.Message);
            }
        }

        /// <summary>
        /// Gets all companies with option to include inactive
        /// </summary>
        public List<CompanyDto> GetAllCompanies(bool includeInactive = false)
        {
            var companies = new List<CompanyDto>();

            string query = @"
            SELECT ComId, Name, Description, Active, DateCreated, CreatedBy, u.Name AS CreatedByName
            FROM Company c
            LEFT JOIN [User] u ON c.CreatedBy = u.UserId";

            if (!includeInactive)
            {
                query += " WHERE c.Active = 1";
            }

            query += " ORDER BY c.DateCreated DESC, c.ComId DESC";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyDto
                        {
                            ComId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Active = reader.GetBoolean(3),
                            DateCreated = reader.GetDateTime(4),
                            CreatedByUserId = reader.GetInt32(5),
                            CreatedByName = reader.IsDBNull(6) ? null : reader.GetString(6)
                        });
                    }
                }
            }

            return companies;
        }

        /// <summary>
        /// Permanently deletes a company from the database
        /// WARNING: This is a hard delete and cannot be undone. Use Archive instead for normal operations.
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteCompanyAsync(int comId)
        {
            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                {
                    await con.OpenAsync();

                    // Check for dependencies first
                    int deptCount = 0, branchCount = 0, empCount = 0;

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(DISTINCT DepartmentID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId AND DepartmentID IS NOT NULL", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        deptCount = (int)await cmd.ExecuteScalarAsync();
                    }

                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(DISTINCT BranchID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        branchCount = (int)await cmd.ExecuteScalarAsync();
                    }

                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Employee WHERE ComId = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        empCount = (int)await cmd.ExecuteScalarAsync();
                    }

                    // If there are dependencies, cannot delete
                    if (deptCount > 0 || branchCount > 0 || empCount > 0)
                    {
                        return (false, $"Cannot delete company: {deptCount} department(s), {branchCount} branch(es), {empCount} employee(s) are linked to this company. Please delete or reassign them first.");
                    }

                    // No dependencies, safe to delete
                    using (var cmd = new SqlCommand("DELETE FROM dbo.Company WHERE ComId = @ComId", con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", comId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Logger.LogInfo($"Company deleted permanently: ID {comId}");
                            return (true, "Company deleted successfully.");
                        }
                        else
                        {
                            return (false, "Company not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete company ID {comId}", ex);
                return (false, $"Error deleting company: {ex.Message}");
            }
        }
    }
}
