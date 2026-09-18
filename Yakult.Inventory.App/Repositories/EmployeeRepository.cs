using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for Employee data access operations
    /// </summary>
    public class EmployeeRepository
    {
        /// <summary>
        /// Creates a new employee
        /// </summary>
        public async Task<int> CreateAsync(EmployeeDto employee)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Employee (Name, Description, DateCreated, Createdby, ComId, BranchId, DeptId, Position, EmployeeNumber)
                    OUTPUT INSERTED.EmpId
                    VALUES (@Name, @Description, @DateCreated, @Createdby, @ComId, @BranchId, @DeptId, @Position, @EmployeeNumber);
                ", con))
                {
                    cmd.Parameters.AddWithValue("@Name", employee.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object)employee.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCreated", employee.DateCreated);
                    cmd.Parameters.AddWithValue("@Createdby", employee.CreatedByUserId);
                    cmd.Parameters.AddWithValue("@ComId", employee.CompanyId);
                    cmd.Parameters.AddWithValue("@BranchId", employee.BranchId);
                    cmd.Parameters.AddWithValue("@DeptId", employee.DepartmentId);
                    cmd.Parameters.AddWithValue("@Position", string.IsNullOrWhiteSpace(employee.Position) ? (object)DBNull.Value : employee.Position.Trim());
                    cmd.Parameters.AddWithValue("@EmployeeNumber", string.IsNullOrWhiteSpace(employee.EmployeeNumber) ? (object)DBNull.Value : employee.EmployeeNumber.Trim());
                    
                    int empId = (int)await cmd.ExecuteScalarAsync();
                    Logger.LogInfo($"Employee created successfully: {employee.Name} (ID: {empId})");
                    
                    return empId;
                }
            }
        }
    }
}
