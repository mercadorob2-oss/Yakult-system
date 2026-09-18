using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for Branch data access operations
    /// </summary>
    public class BranchRepository
    {
        /// <summary>
        /// Creates a new branch
        /// </summary>
        public async Task<int> CreateAsync(BranchDto branch)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Branch
                        (Name, Description, DateCreated, Createdby,
                         IsFactory, IsDepot, IsDistributor, IsCenter, CenterRegion, BranchType)
                    OUTPUT INSERTED.BranchId
                    VALUES
                        (@Name, @Desc, @DateCreated, @Createdby,
                         @IsFactory, @IsDepot, @IsDistributor, @IsCenter, @CenterRegion, @BranchType);
                ", con))
                {
                    cmd.Parameters.AddWithValue("@Name", branch.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Desc", (object)branch.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateCreated", branch.DateCreated);
                    cmd.Parameters.AddWithValue("@Createdby", branch.CreatedByUserId);
                    cmd.Parameters.AddWithValue("@IsFactory",    branch.IsFactory);
                    cmd.Parameters.AddWithValue("@IsDepot",      branch.IsDepot);
                    cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                    cmd.Parameters.AddWithValue("@IsCenter",     branch.IsCenter);
                    cmd.Parameters.AddWithValue("@CenterRegion", (object)branch.CenterRegion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchType",   DeriveBranchType(branch));

                    int branchId = (int)await cmd.ExecuteScalarAsync();

                    if (branch.CompanyId.HasValue)
                    {
                        using (var bdcCmd = new SqlCommand(@"
                            INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                            VALUES (@BranchId, @DeptId, @ComId);
                        ", con))
                        {
                            bdcCmd.Parameters.AddWithValue("@BranchId", branchId);
                            bdcCmd.Parameters.AddWithValue("@DeptId", branch.DepartmentId.HasValue ? (object)branch.DepartmentId.Value : DBNull.Value);
                            bdcCmd.Parameters.AddWithValue("@ComId", branch.CompanyId.Value);
                            await bdcCmd.ExecuteNonQueryAsync();
                        }
                    }

                    Logger.LogInfo($"Branch created successfully: {branch.Name} (ID: {branchId})");
                    return branchId;
                }
            }
        }

        /// <summary>
        /// Gets a single branch by ID
        /// </summary>
        public async Task<BranchDto> GetByIdAsync(int branchId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                
                using (var cmd = new SqlCommand(@"
                    SELECT
                        b.BranchId, b.Name, b.Description, b.DateCreated, b.Createdby,
                        b.DateModified, b.Modifiedby,
                        bdc_co.CompanyId   AS ComId,
                        bdc_dept.DeptId    AS DeptId,
                        b.IsFactory, b.IsDepot, b.IsCenter, b.CenterRegion, b.IsDistributor,
                        bdc_co.CompanyName,
                        bdc_dept.DeptName  AS DepartmentName,
                        u1.Name AS CreatedByName,
                        u2.Name AS ModifiedByName
                    FROM dbo.Branch b
                    OUTER APPLY (
                        SELECT TOP 1 bdc.CompanyID AS CompanyId, c.Name AS CompanyName
                        FROM   dbo.BranchDepartmentCompany bdc
                        JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                        WHERE  bdc.BranchID = b.BranchId
                        ORDER BY bdc.BranchDeptCompanyID
                    ) bdc_co
                    OUTER APPLY (
                        SELECT TOP 1 bdc.DepartmentID AS DeptId, d.Name AS DeptName
                        FROM   dbo.BranchDepartmentCompany bdc
                        JOIN   dbo.Department d ON bdc.DepartmentID = d.DeptId
                        WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
                        ORDER BY bdc.BranchDeptCompanyID
                    ) bdc_dept
                    LEFT JOIN dbo.[User] u1 ON b.Createdby = u1.UserId
                    LEFT JOIN dbo.[User] u2 ON b.Modifiedby = u2.UserId
                    WHERE b.BranchId = @BranchId
                ", con))
                {
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new BranchDto
                            {
                                BranchId = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                DateCreated = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                                CreatedByUserId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                DateModified = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                ModifiedByUserId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                CompanyId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                DepartmentId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                IsFactory = !reader.IsDBNull(9) && reader.GetBoolean(9),
                                IsDepot = !reader.IsDBNull(10) && reader.GetBoolean(10),
                                IsCenter = !reader.IsDBNull(11) && reader.GetBoolean(11),
                                CenterRegion = reader.IsDBNull(12) ? null : reader.GetString(12),
                                IsDistributor = !reader.IsDBNull(13) && reader.GetBoolean(13),
                                CompanyName = reader.IsDBNull(14) ? "N/A" : reader.GetString(14),
                                DepartmentName = reader.IsDBNull(15) ? "N/A" : reader.GetString(15),
                                CreatedByName = reader.IsDBNull(16) ? "N/A" : reader.GetString(16),
                                ModifiedByName = reader.IsDBNull(17) ? "N/A" : reader.GetString(17)
                            };
                        }
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Updates an existing branch
        /// </summary>
        public async Task<bool> UpdateAsync(BranchDto branch)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                
                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.Branch
                    SET
                        Name          = @Name,
                        Description   = @Desc,
                        DateModified  = @DateModified,
                        Modifiedby    = @Modifiedby,
                        IsFactory     = @IsFactory,
                        IsDepot       = @IsDepot,
                        IsDistributor = @IsDistributor,
                        IsCenter      = @IsCenter,
                        CenterRegion  = @CenterRegion,
                        BranchType    = @BranchType
                    WHERE BranchId = @BranchId
                ", con))
                {
                    cmd.Parameters.AddWithValue("@BranchId", branch.BranchId);
                    cmd.Parameters.AddWithValue("@Name", branch.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Desc", (object)branch.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DateModified", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Modifiedby", branch.ModifiedByUserId ?? AppSession.CurrentUserId);
                    cmd.Parameters.AddWithValue("@IsFactory",    branch.IsFactory);
                    cmd.Parameters.AddWithValue("@IsDepot",      branch.IsDepot);
                    cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                    cmd.Parameters.AddWithValue("@IsCenter",     branch.IsCenter);
                    cmd.Parameters.AddWithValue("@CenterRegion", (object)branch.CenterRegion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchType",   DeriveBranchType(branch));

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    if (rowsAffected <= 0) return false;
                }

                // Upsert BDC row for the primary company/dept assignment
                if (branch.CompanyId.HasValue)
                {
                    using (var bdcCmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany WHERE BranchID = @BranchId)
                        BEGIN
                            UPDATE bdc
                            SET    CompanyID    = @ComId,
                                   DepartmentID = @DeptId
                            FROM   dbo.BranchDepartmentCompany bdc
                            WHERE  bdc.BranchDeptCompanyID = (
                                SELECT TOP 1 BranchDeptCompanyID
                                FROM   dbo.BranchDepartmentCompany
                                WHERE  BranchID = @BranchId
                                ORDER BY BranchDeptCompanyID
                            )
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                            VALUES (@BranchId, @DeptId, @ComId)
                        END
                    ", con))
                    {
                        bdcCmd.Parameters.AddWithValue("@BranchId", branch.BranchId);
                        bdcCmd.Parameters.AddWithValue("@ComId", branch.CompanyId.Value);
                        bdcCmd.Parameters.AddWithValue("@DeptId", branch.DepartmentId.HasValue ? (object)branch.DepartmentId.Value : DBNull.Value);
                        await bdcCmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    using (var bdcCmd = new SqlCommand(
                        "DELETE FROM dbo.BranchDepartmentCompany WHERE BranchID = @BranchId", con))
                    {
                        bdcCmd.Parameters.AddWithValue("@BranchId", branch.BranchId);
                        await bdcCmd.ExecuteNonQueryAsync();
                    }
                }

                Logger.LogInfo($"Branch updated successfully: {branch.Name} (ID: {branch.BranchId})");
                return true;
            }
        }

        /// <summary>
        /// Deletes a branch
        /// </summary>
        public async Task<bool> DeleteAsync(int branchId)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();
                    
                    using (var cmd = new SqlCommand(@"
                        DELETE FROM dbo.Branch WHERE BranchId = @BranchId
                    ", con))
                    {
                        cmd.Parameters.AddWithValue("@BranchId", branchId);
                        
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        
                        if (rowsAffected > 0)
                        {
                            Logger.LogInfo($"Branch deleted successfully (ID: {branchId})");
                            return true;
                        }
                        
                        return false;
                    }
                }
            }
            catch (SqlException ex)
            {
                Logger.LogError($"Failed to delete branch (ID: {branchId}): {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Derives the BranchType string from the boolean flag fields on a BranchDto.
        /// Mirrors the priority order used in Migration_Branch_AddBranchType.sql:
        /// Distributor > Depot > Center > Factory > Office (default).
        /// Call this whenever persisting IsFactory / IsDepot / IsCenter / IsDistributor
        /// so the BranchType column stays in sync.
        /// </summary>
        public static string DeriveBranchType(BranchDto branch)
        {
            if (branch.IsDistributor) return "Distributor";
            if (branch.IsDepot)       return "Depot";
            if (branch.IsCenter)      return "Center";
            if (branch.IsFactory)     return "Factory";
            return "Office";
        }

        /// <summary>
        /// Bulk-assigns a list of branches to a single target department in one statement.
        /// Uses dbo.IntIdList TVP — run Migration_TVP_IntIdList.sql on the DB first.
        /// </summary>
        /// <param name="branchIds">IDs of branches to reassign.</param>
        /// <param name="targetDeptId">DeptId of the destination department.</param>
        /// <returns>Number of rows updated.</returns>
        public async Task<int> BulkAssignDepartmentAsync(IReadOnlyList<int> branchIds, int targetDeptId)
        {
            if (branchIds == null || branchIds.Count == 0)
                throw new ArgumentException("At least one BranchId is required.", nameof(branchIds));

            var idTable = new DataTable();
            idTable.Columns.Add("Id", typeof(int));
            foreach (int id in branchIds)
                idTable.Rows.Add(id);

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(@"
                    -- Insert new BDC rows for each branch → department assignment.
                    -- CompanyID is derived from the branch's existing primary BDC row.
                    -- Branches with no BDC row yet are skipped (CROSS APPLY).
                    INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                    SELECT ids.Id, @DeptId, bdc_primary.CompanyID
                    FROM   @BranchIds ids
                    JOIN   dbo.Branch b ON b.BranchId = ids.Id AND b.Active = 1
                    CROSS APPLY (
                        SELECT TOP 1 CompanyID
                        FROM   dbo.BranchDepartmentCompany
                        WHERE  BranchID = ids.Id
                        ORDER BY BranchDeptCompanyID
                    ) bdc_primary
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM   dbo.BranchDepartmentCompany bdc2
                        WHERE  bdc2.BranchID     = ids.Id
                          AND  bdc2.DepartmentID = @DeptId
                          AND  bdc2.CompanyID    = bdc_primary.CompanyID
                    );", con))
                {
                    cmd.Parameters.AddWithValue("@DeptId",     targetDeptId);
                    cmd.Parameters.AddWithValue("@ModifiedBy", AppSession.CurrentUserId);

                    var tvp      = cmd.Parameters.Add("@BranchIds", SqlDbType.Structured);
                    tvp.TypeName = "dbo.IntIdList";
                    tvp.Value    = idTable;

                    int rows = await cmd.ExecuteNonQueryAsync();
                    Logger.LogInfo($"BulkAssignDepartment: {rows} branch(es) assigned to DeptId {targetDeptId} by UserId {AppSession.CurrentUserId}.");
                    return rows;
                }
            }
        }
    }
}
