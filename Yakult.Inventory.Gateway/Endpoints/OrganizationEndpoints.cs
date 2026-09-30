using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Security;

namespace Yakult.Inventory.Gateway.Endpoints;

/// <summary>
/// Company / Branch / Department. Ported from the desktop's CompanyRepository,
/// BranchRepository and DepartmentRepository (the desktop keeps its SQL for direct
/// mode, so change both together). Created/modified-by always come from the token.
/// </summary>
public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ── Company ─────────────────────────────────────────────────────────
        api.MapDelete("/companies/{comId:int}", async (int comId, DbSession db, AccessPolicy access, ILogger<DbSession> log, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);

            int deptCount, branchCount, empCount;
            await using (var cmd = new SqlCommand(
                "SELECT COUNT(DISTINCT DepartmentID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId AND DepartmentID IS NOT NULL", con))
            {
                cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId;
                deptCount = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }
            await using (var cmd = new SqlCommand(
                "SELECT COUNT(DISTINCT BranchID) FROM dbo.BranchDepartmentCompany WHERE CompanyID = @ComId", con))
            {
                cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId;
                branchCount = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Employee WHERE ComId = @ComId", con))
            {
                cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId;
                empCount = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }

            if (deptCount > 0 || branchCount > 0 || empCount > 0)
                return Results.Ok(new OperationResult(false,
                    $"Cannot delete company: {deptCount} department(s), {branchCount} branch(es), {empCount} employee(s) are linked to this company. Please delete or reassign them first."));

            await using (var cmd = new SqlCommand("DELETE FROM dbo.Company WHERE ComId = @ComId", con))
            {
                cmd.Parameters.Add("@ComId", SqlDbType.Int).Value = comId;
                if (await cmd.ExecuteNonQueryAsync(ct) > 0)
                {
                    log.LogInformation("Company {ComId} deleted by user {UserId}.", comId, db.UserId);
                    return Results.Ok(new OperationResult(true, "Company deleted successfully."));
                }
            }
            return Results.Ok(new OperationResult(false, "Company not found."));
        });

        // ── Branch ──────────────────────────────────────────────────────────
        api.MapPost("/branches", async (BranchModel branch, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

            int branchId;
            await using (var cmd = new SqlCommand(@"
                INSERT INTO dbo.Branch
                    (Name, Description, DateCreated, Createdby,
                     IsFactory, IsDepot, IsDistributor, IsCenter, CenterRegion, BranchType)
                OUTPUT INSERTED.BranchId
                VALUES
                    (@Name, @Desc, @DateCreated, @Createdby,
                     @IsFactory, @IsDepot, @IsDistributor, @IsCenter, @CenterRegion, @BranchType);", con, tx))
            {
                cmd.Parameters.AddWithValue("@Name", (object?)branch.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Desc", (object?)branch.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                cmd.Parameters.AddWithValue("@Createdby", db.UserId);
                cmd.Parameters.AddWithValue("@IsFactory", branch.IsFactory);
                cmd.Parameters.AddWithValue("@IsDepot", branch.IsDepot);
                cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                cmd.Parameters.AddWithValue("@IsCenter", branch.IsCenter);
                cmd.Parameters.AddWithValue("@CenterRegion", (object?)branch.CenterRegion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchType", DeriveBranchType(branch));
                branchId = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }

            if (branch.CompanyId.HasValue)
            {
                await using var bdc = new SqlCommand(@"
                    INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
                    VALUES (@BranchId, @DeptId, @ComId);", con, tx);
                bdc.Parameters.AddWithValue("@BranchId", branchId);
                bdc.Parameters.AddWithValue("@DeptId", (object?)branch.DepartmentId ?? DBNull.Value);
                bdc.Parameters.AddWithValue("@ComId", branch.CompanyId.Value);
                await bdc.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Results.Ok(branchId);
        });

        api.MapGet("/branches/{branchId:int}", async (int branchId, DbSession db, CancellationToken ct) =>
        {
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
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
                WHERE b.BranchId = @BranchId", con);
            cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return Results.Ok<BranchModel?>(null);

            return Results.Ok<BranchModel?>(new BranchModel
            {
                BranchId = r.GetInt32(0),
                Name = r.IsDBNull(1) ? "" : r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                DateCreated = r.IsDBNull(3) ? DateTime.MinValue : r.GetDateTime(3),
                CreatedByUserId = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                DateModified = r.IsDBNull(5) ? null : r.GetDateTime(5),
                ModifiedByUserId = r.IsDBNull(6) ? null : r.GetInt32(6),
                CompanyId = r.IsDBNull(7) ? null : r.GetInt32(7),
                DepartmentId = r.IsDBNull(8) ? null : r.GetInt32(8),
                IsFactory = !r.IsDBNull(9) && r.GetBoolean(9),
                IsDepot = !r.IsDBNull(10) && r.GetBoolean(10),
                IsCenter = !r.IsDBNull(11) && r.GetBoolean(11),
                CenterRegion = r.IsDBNull(12) ? null : r.GetString(12),
                IsDistributor = !r.IsDBNull(13) && r.GetBoolean(13),
                CompanyName = r.IsDBNull(14) ? "N/A" : r.GetString(14),
                DepartmentName = r.IsDBNull(15) ? "N/A" : r.GetString(15),
                CreatedByName = r.IsDBNull(16) ? "N/A" : r.GetString(16),
                ModifiedByName = r.IsDBNull(17) ? "N/A" : r.GetString(17)
            });
        });

        api.MapPut("/branches/{branchId:int}", async (int branchId, BranchModel branch, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

            await using (var cmd = new SqlCommand(@"
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
                WHERE BranchId = @BranchId", con, tx))
            {
                cmd.Parameters.AddWithValue("@BranchId", branchId);
                cmd.Parameters.AddWithValue("@Name", (object?)branch.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Desc", (object?)branch.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateModified", DateTime.Now);
                cmd.Parameters.AddWithValue("@Modifiedby", db.UserId);
                cmd.Parameters.AddWithValue("@IsFactory", branch.IsFactory);
                cmd.Parameters.AddWithValue("@IsDepot", branch.IsDepot);
                cmd.Parameters.AddWithValue("@IsDistributor", branch.IsDistributor);
                cmd.Parameters.AddWithValue("@IsCenter", branch.IsCenter);
                cmd.Parameters.AddWithValue("@CenterRegion", (object?)branch.CenterRegion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchType", DeriveBranchType(branch));
                if (await cmd.ExecuteNonQueryAsync(ct) <= 0)
                    return Results.Ok(false);
            }

            // Upsert BDC row for the primary company/dept assignment
            if (branch.CompanyId.HasValue)
            {
                await using var bdc = new SqlCommand(@"
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
                    END", con, tx);
                bdc.Parameters.AddWithValue("@BranchId", branchId);
                bdc.Parameters.AddWithValue("@ComId", branch.CompanyId.Value);
                bdc.Parameters.AddWithValue("@DeptId", (object?)branch.DepartmentId ?? DBNull.Value);
                await bdc.ExecuteNonQueryAsync(ct);
            }
            else
            {
                await using var bdc = new SqlCommand("DELETE FROM dbo.BranchDepartmentCompany WHERE BranchID = @BranchId", con, tx);
                bdc.Parameters.AddWithValue("@BranchId", branchId);
                await bdc.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Results.Ok(true);
        });

        // A foreign-key conflict (error 547) comes back as 409 with SQL's message;
        // the desktop's ForeignKeyErrorHelper recognizes it from the text.
        api.MapDelete("/branches/{branchId:int}", async (int branchId, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand("DELETE FROM dbo.Branch WHERE BranchId = @BranchId", con);
            cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
            return Results.Ok(await cmd.ExecuteNonQueryAsync(ct) > 0);
        });

        api.MapPost("/branches/bulk-assign-department", async (BulkAssignDepartmentRequest request, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();
            if (request.BranchIds == null || request.BranchIds.Count == 0)
                return Http.BadRequest("At least one BranchId is required.");

            var idTable = new DataTable();
            idTable.Columns.Add("Id", typeof(int));
            foreach (var id in request.BranchIds)
                idTable.Rows.Add(id);

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
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
                );", con);
            cmd.Parameters.Add("@DeptId", SqlDbType.Int).Value = request.TargetDeptId;
            var tvp = cmd.Parameters.Add("@BranchIds", SqlDbType.Structured);
            tvp.TypeName = "dbo.IntIdList";
            tvp.Value = idTable;
            return Results.Ok(await cmd.ExecuteNonQueryAsync(ct));
        });

        // ── Department ──────────────────────────────────────────────────────
        api.MapPost("/departments", async (DepartmentModel department, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                INSERT INTO dbo.Department (Name, Section, Description, DateCreated, Createdby)
                OUTPUT INSERTED.DeptId
                VALUES (@Name, @Section, @Desc, @DateCreated, @Createdby);", con);
            cmd.Parameters.AddWithValue("@Name", (object?)department.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Section", (object?)department.Section ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Desc", (object?)department.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
            cmd.Parameters.AddWithValue("@Createdby", db.UserId);
            return Results.Ok((int)(await cmd.ExecuteScalarAsync(ct))!);
        });
    }

    /// <summary>Same priority as the desktop's BranchRepository.DeriveBranchType.</summary>
    private static string DeriveBranchType(BranchModel branch)
    {
        if (branch.IsDistributor) return "Distributor";
        if (branch.IsDepot) return "Depot";
        if (branch.IsCenter) return "Center";
        if (branch.IsFactory) return "Factory";
        return "Office";
    }
}
