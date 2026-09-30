using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Security;

namespace Yakult.Inventory.Gateway.Endpoints;

/// <summary>
/// Item conditions, item categories and vendors. Ported from the desktop's
/// ItemConditionRepository, CategoryRepository and VendorRepository (the desktop
/// keeps its SQL for direct mode, so change both together).
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // ── Item conditions ─────────────────────────────────────────────────
        api.MapGet("/conditions", async (DbSession db, CancellationToken ct) =>
        {
            var results = new List<ConditionModel>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT ConditionId, ConditionName FROM dbo.[Condition] ORDER BY ConditionId", con);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                results.Add(new ConditionModel
                {
                    ConditionId = r.GetInt32(0),
                    ConditionName = r.GetString(1),
                    SortOrder = r.GetInt32(0),
                    IsActive = true
                });
            }
            return Results.Ok(results);
        });

        // ── Item categories ─────────────────────────────────────────────────
        api.MapGet("/categories", async (DbSession db, CancellationToken ct) =>
        {
            var categories = new List<ItemCategoryModel>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                SELECT
                    c.CategoryId,
                    c.Name,
                    c.Active,
                    c.DateCreated,
                    c.CreatedBy,
                    u.Name AS CreatedByName,
                    ISNULL(a.IsArchived, 0) AS IsArchived,
                    ISNULL(ic.ItemCount, 0) AS ItemCount
                FROM dbo.ItemCategory c
                LEFT JOIN [User] u ON c.CreatedBy = u.UserId
                LEFT JOIN dbo.ArchiveStatus a ON a.EntityType = 'ItemCategory' AND a.EntityId = c.CategoryId AND a.IsArchived = 1
                LEFT JOIN (
                    SELECT i.CategoryId, COUNT(*) AS ItemCount
                    FROM dbo.Item i
                    LEFT JOIN dbo.ArchiveStatus arch
                        ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
                    WHERE i.Active = 1 AND arch.EntityId IS NULL
                    GROUP BY i.CategoryId
                ) ic ON ic.CategoryId = c.CategoryId
                WHERE c.Active = 1
                ORDER BY c.DateCreated DESC, c.CategoryId DESC", con);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                categories.Add(new ItemCategoryModel
                {
                    CategoryId = r.GetInt32(0),
                    Name = r.GetString(1),
                    Active = r.GetBoolean(2),
                    DateCreated = r.GetDateTime(3),
                    CreatedBy = r.GetInt32(4),
                    CreatedByName = r.IsDBNull(5) ? null : r.GetString(5),
                    IsArchived = r.GetBoolean(6),
                    ItemCount = r.GetInt32(7)
                });
            }
            return Results.Ok(categories);
        });

        api.MapPost("/categories", async (ItemCategoryModel category, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                INSERT INTO dbo.ItemCategory (Name, Active, DateCreated, CreatedBy)
                OUTPUT INSERTED.CategoryId
                VALUES (@Name, @Active, @DateCreated, @CreatedBy);", con);
            cmd.Parameters.AddWithValue("@Name", (object?)category.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Active", category.Active);
            cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
            cmd.Parameters.AddWithValue("@CreatedBy", db.UserId);
            return Results.Ok((int)(await cmd.ExecuteScalarAsync(ct))!);
        });

        api.MapPut("/categories/{categoryId:int}", async (int categoryId, ItemCategoryModel category, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                UPDATE dbo.ItemCategory
                SET Name = @Name,
                    Active = @Active
                WHERE CategoryId = @CategoryId", con);
            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
            cmd.Parameters.AddWithValue("@Name", (object?)category.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Active", category.Active);
            return Results.Ok(await cmd.ExecuteNonQueryAsync(ct) > 0);
        });

        api.MapDelete("/categories/{categoryId:int}", async (int categoryId, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditMasterDataAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            int itemCount;
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Item WHERE CategoryId = @CategoryId", con))
            {
                cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
                itemCount = (int)(await cmd.ExecuteScalarAsync(ct))!;
            }
            if (itemCount > 0)
                return Results.Ok(new OperationResult(false,
                    $"Cannot delete category: {itemCount} item(s) are linked to this category. Please reassign or delete them first."));

            await using (var cmd = new SqlCommand("DELETE FROM dbo.ItemCategory WHERE CategoryId = @CategoryId", con))
            {
                cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
                if (await cmd.ExecuteNonQueryAsync(ct) > 0)
                    return Results.Ok(new OperationResult(true, "Category deleted successfully."));
            }
            return Results.Ok(new OperationResult(false, "Category not found."));
        });

        api.MapGet("/categories/{categoryId:int}/items", async (int categoryId, DbSession db, CancellationToken ct) =>
        {
            var items = new List<CategoryItemLocationModel>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                SELECT
                    i.ItemId,
                    i.Name,
                    i.SerialNumber,
                    i.StockOnHand,
                    i.Active,
                    CASE
                        WHEN EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1) THEN 'Archived'
                        WHEN EXISTS (SELECT 1 FROM dbo.SetItem si WHERE si.ItemId = i.ItemId) THEN 'In Set'
                        WHEN EXISTS (SELECT 1 FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Status = 'Submitted') THEN 'In Request'
                        WHEN EXISTS (SELECT 1 FROM dbo.SetItemUpdate siu WHERE siu.ItemId = i.ItemId AND siu.Processed = 0) THEN 'Pending Update'
                        ELSE 'In Inventory'
                    END AS CurrentLocation
                FROM dbo.Item i
                WHERE i.CategoryId = @CategoryId
                    AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
                ORDER BY i.Name", con);
            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                items.Add(new CategoryItemLocationModel
                {
                    ItemId = r.GetInt32(0),
                    ItemName = r.GetString(1),
                    SerialNumber = r.IsDBNull(2) ? null : r.GetString(2),
                    StockOnHand = r.GetInt32(3),
                    Active = r.GetBoolean(4),
                    CurrentLocation = r.GetString(5)
                });
            }
            return Results.Ok(items);
        });

        // ── Vendors ─────────────────────────────────────────────────────────
        api.MapGet("/vendors", async (DbSession db, CancellationToken ct) =>
        {
            var vendors = new List<VendorModel>();
            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                SELECT
                    v.VendorID,
                    v.VendorName,
                    v.Address,
                    v.IsActive,
                    v.CreatedDate,
                    v.TIN,
                    ISNULL(v.IsRefiller, 0) AS IsRefiller,
                    ISNULL(v.IsDisposer, 0) AS IsDisposer,
                    ISNULL(v.IsBuyer,    0) AS IsBuyer,
                    CASE WHEN arc.ArchiveId IS NULL THEN 0 ELSE 1 END AS IsArchived
                FROM dbo.Vendor v
                LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                ORDER BY v.CreatedDate DESC, v.VendorID DESC", con);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                bool isArchived = r.GetInt32(9) == 1;
                vendors.Add(new VendorModel
                {
                    VendorId = r.GetInt32(0),
                    VendorName = r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    IsActive = r.GetBoolean(3) && !isArchived,
                    CreatedDate = r.GetDateTime(4),
                    TIN = r.IsDBNull(5) ? null : r.GetString(5),
                    IsRefiller = r.GetBoolean(6),
                    IsDisposer = r.GetBoolean(7),
                    IsBuyer = r.GetBoolean(8),
                    IsArchived = isArchived
                });
            }
            return Results.Ok(vendors);
        });

        api.MapPost("/vendors", async (VendorModel vendor, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditVendorsAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                INSERT INTO dbo.Vendor (VendorName, Address, IsActive, IsRefiller, IsDisposer, IsBuyer, CreatedDate, TIN)
                VALUES (@VendorName, @Address, @IsActive, @IsRefiller, @IsDisposer, @IsBuyer, @CreatedDate, @TIN);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", con);
            cmd.Parameters.AddWithValue("@VendorName", (object?)vendor.VendorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)vendor.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", vendor.IsActive);
            cmd.Parameters.AddWithValue("@IsRefiller", vendor.IsRefiller);
            cmd.Parameters.AddWithValue("@IsDisposer", vendor.IsDisposer);
            cmd.Parameters.AddWithValue("@IsBuyer", vendor.IsBuyer);
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            cmd.Parameters.AddWithValue("@TIN", (object?)vendor.TIN ?? DBNull.Value);
            return Results.Ok(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)));
        });

        api.MapPut("/vendors/{vendorId:int}", async (int vendorId, VendorModel vendor, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditVendorsAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                UPDATE dbo.Vendor
                SET VendorName = @VendorName,
                    Address    = @Address,
                    IsActive   = @IsActive,
                    IsRefiller = @IsRefiller,
                    IsDisposer = @IsDisposer,
                    IsBuyer    = @IsBuyer,
                    TIN        = @TIN
                WHERE VendorID = @VendorId", con);
            cmd.Parameters.AddWithValue("@VendorId", vendorId);
            cmd.Parameters.AddWithValue("@VendorName", (object?)vendor.VendorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)vendor.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", vendor.IsActive);
            cmd.Parameters.AddWithValue("@IsRefiller", vendor.IsRefiller);
            cmd.Parameters.AddWithValue("@IsDisposer", vendor.IsDisposer);
            cmd.Parameters.AddWithValue("@IsBuyer", vendor.IsBuyer);
            cmd.Parameters.AddWithValue("@TIN", (object?)vendor.TIN ?? DBNull.Value);
            return Results.Ok(await cmd.ExecuteNonQueryAsync(ct) > 0);
        });

        api.MapPost("/vendors/{vendorId:int}/archive", async (int vendorId, ArchiveRequest request, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditVendorsAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                VALUES ('Vendor', @VendorId, 1, GETDATE(), @ArchivedBy, @Reason)", con);
            cmd.Parameters.AddWithValue("@VendorId", vendorId);
            cmd.Parameters.AddWithValue("@ArchivedBy", db.UserName);
            cmd.Parameters.AddWithValue("@Reason", request.Reason ?? "Archived from Vendor page");
            return Results.Ok(await cmd.ExecuteNonQueryAsync(ct) > 0);
        });

        api.MapDelete("/vendors/{vendorId:int}", async (int vendorId, DbSession db, AccessPolicy access, CancellationToken ct) =>
        {
            if (!await access.CanEditVendorsAsync(ct)) return Http.Forbidden();

            await using var con = await db.OpenAsync(ct);
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.[Set] WHERE VendorId = @VendorId", con))
            {
                cmd.Parameters.Add("@VendorId", SqlDbType.Int).Value = vendorId;
                int count = (int)(await cmd.ExecuteScalarAsync(ct))!;
                if (count > 0)
                    return Results.Ok(new OperationResult(false,
                        $"Cannot delete vendor: {count} invoice(s)/set(s) reference this vendor. Please use Archive instead."));
            }
            await using (var cmd = new SqlCommand("DELETE FROM dbo.Vendor WHERE VendorID = @VendorId", con))
            {
                cmd.Parameters.Add("@VendorId", SqlDbType.Int).Value = vendorId;
                if (await cmd.ExecuteNonQueryAsync(ct) > 0)
                    return Results.Ok(new OperationResult(true, "Vendor deleted successfully."));
            }
            return Results.Ok(new OperationResult(false, "Vendor not found."));
        });
    }
}
