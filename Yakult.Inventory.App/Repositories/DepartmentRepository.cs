using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for Department data access operations
    /// </summary>
    public class DepartmentRepository
    {
        /// <summary>
        /// Creates a new department
        /// </summary>
        public async Task<int> CreateAsync(DepartmentDto department)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Department (Name, Section, Description, DateCreated, Createdby)
                    OUTPUT INSERTED.DeptId
                    VALUES (@Name, @Section, @Desc, @DateCreated, @Createdby);
                ", con))
                {
                    cmd.Parameters.AddWithValue("@Name",    department.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Section", (object)department.Section ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Desc",    (object)department.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCreated", department.DateCreated);
                    cmd.Parameters.AddWithValue("@Createdby",   department.CreatedByUserId);
                    
                    int deptId = (int)await cmd.ExecuteScalarAsync();
                    Logger.LogInfo($"Department created successfully: {department.Name} (ID: {deptId})");
                    
                    return deptId;
                }
            }
        }
    }
}
