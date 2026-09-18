using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

public sealed class EmployeeResourceRepository : IEmployeeResourceRepository
{
    private static readonly HashSet<string> ResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Policy", "Form", "Guide", "FAQ", "Checklist", "Directory", "Handbook", "Reference", "Template"
    };

    private static readonly HashSet<string> ResourceStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Published", "Rejected", "Archived"
    };

    private readonly IConnectionStringProvider _connectionStringProvider;

    public EmployeeResourceRepository(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    public async Task<IReadOnlyList<EmployeeResourceItem>> GetPublishedAsync(string? resourceType = null, string? categorySlug = null, string? query = null, int take = 100)
    {
        var items = new List<EmployeeResourceItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var sql = ResourceSelect.Replace("SELECT er.ResourceId", "SELECT TOP (@Take) er.ResourceId", StringComparison.Ordinal) + @"
            WHERE er.Status = 'Published'
              AND cat.IsActive = 1
              AND (er.PublishStartUtc IS NULL OR er.PublishStartUtc <= SYSUTCDATETIME())
              AND (er.PublishEndUtc IS NULL OR er.PublishEndUtc > SYSUTCDATETIME())
              AND (@Type = '' OR er.ResourceType = @Type OR (@Type = 'Form' AND er.ResourceType = 'Forms'))
              AND (@Category = '' OR cat.Slug = @Category OR cat.Name = @Category)
              AND (@Query = '' OR er.Title LIKE @LikeQuery OR er.Summary LIKE @LikeQuery
                   OR cat.Name LIKE @LikeQuery OR ISNULL(dept.Name, '') LIKE @LikeQuery)
            ORDER BY er.UpdatedAtUtc DESC, er.ResourceId DESC;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Take", SqlDbType.Int, Math.Clamp(take, 1, 200));
        Add(cmd, "@Type", SqlDbType.VarChar, CanonicalizeResourceType(resourceType), 50);
        Add(cmd, "@Category", SqlDbType.VarChar, (categorySlug ?? string.Empty).Trim(), 100);
        var normalizedQuery = (query ?? string.Empty).Trim();
        Add(cmd, "@Query", SqlDbType.NVarChar, normalizedQuery, 200);
        Add(cmd, "@LikeQuery", SqlDbType.NVarChar, $"%{EscapeLike(normalizedQuery)}%", 402);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                items.Add(MapResource(reader));
        }

        return await HydrateManyAsync(con, null, items, includeRevisions: false);
    }

    public async Task<EmployeeResourceItem?> GetPublishedBySlugAsync(string slug)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand(ResourceSelect + @"
            WHERE er.Status = 'Published'
              AND cat.IsActive = 1
              AND er.Slug = @Slug
              AND (er.PublishStartUtc IS NULL OR er.PublishStartUtc <= SYSUTCDATETIME())
              AND (er.PublishEndUtc IS NULL OR er.PublishEndUtc > SYSUTCDATETIME());", con);
        Add(cmd, "@Slug", SqlDbType.VarChar, slug.Trim().ToLowerInvariant(), 200);
        EmployeeResourceItem? item;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            item = await reader.ReadAsync() ? MapResource(reader) : null;
        }
        return item is null ? null : await HydrateAsync(con, null, item, includeRevisions: false);
    }

    public async Task<IReadOnlyList<EmployeeResourceItem>> GetManagedAsync()
    {
        var items = new List<EmployeeResourceItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand(ResourceSelect + " ORDER BY er.UpdatedAtUtc DESC, er.ResourceId DESC;", con);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                items.Add(MapResource(reader));
        }
        return await HydrateManyAsync(con, null, items, includeRevisions: false);
    }

    public async Task<EmployeeResourceItem?> GetByIdAsync(int resourceId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var item = await ReadResourceRowAsync(con, null, resourceId);
        return item is null ? null : await HydrateAsync(con, null, item, includeRevisions: true);
    }

    public async Task<IReadOnlyList<EmployeeResourceOption>> GetCategoriesAsync()
    {
        var options = new List<EmployeeResourceOption>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT CategoryId, Name, Slug FROM dbo.EmployeeResourceCategory WHERE IsActive = 1 ORDER BY SortOrder, Name;", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            options.Add(new EmployeeResourceOption
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Slug = reader.GetString(2)
            });
        }
        return options;
    }

    public async Task<IReadOnlyList<EmployeeResourceOption>> GetDepartmentsAsync()
    {
        var options = new List<EmployeeResourceOption>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT DeptId, Name
FROM dbo.Department
WHERE Active = 1
  AND (Name = N'IT'
       OR Name LIKE N'IT %'
       OR Name LIKE N'% IT'
       OR Name LIKE N'% IT %'
       OR Name LIKE N'%Information Technology%'
       OR Name LIKE N'%Information Systems%'
       OR Name LIKE N'%Cybersecurity%'
       OR Acronym IN (N'IT', N'IS', N'MIS'))
ORDER BY Name;", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            options.Add(new EmployeeResourceOption
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return options;
    }

    public async Task<EmployeeResourceItem> SaveDraftAsync(SaveEmployeeResourceRequest request, int userId, string? userName, string? ipAddress)
    {
        NormalizeAndValidate(request);
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var oldItem = request.ResourceId.HasValue
                ? await ReadResourceRowAsync(con, tx, request.ResourceId.Value)
                : null;
            if (request.ResourceId.HasValue && oldItem is null)
                throw new InvalidOperationException("Employee Resource was not found.");

            await EnsureActiveCategoryAsync(con, tx, request.CategoryId);
            await EnsureItOwnerDepartmentAsync(con, tx, request.OwnerDepartmentId);
            await EnsureSlugAvailableAsync(con, tx, request.Slug, request.ResourceId);
            int resourceId;
            var action = request.ResourceId.HasValue ? "DraftSaved" : "DraftCreated";
            if (request.ResourceId.HasValue)
            {
                var rowVersion = DecodeRowVersion(request.RowVersion);
                const string update = @"
UPDATE dbo.EmployeeResource
SET Slug=@Slug, Title=@Title, Summary=@Summary, Overview=@Overview, ResourceType=@ResourceType,
    CategoryId=@CategoryId, OwnerDepartmentId=@OwnerDepartmentId, Version=@Version,
    NextReviewDateUtc=@ReviewDateUtc, PublishStartUtc=@PublishStartUtc, PublishEndUtc=@PublishEndUtc,
    Status='Draft', ApprovedByUserId=NULL, ApprovedAtUtc=NULL, UpdatedAtUtc=SYSUTCDATETIME()
WHERE ResourceId=@ResourceId AND RowVer=@RowVer AND Status IN ('Draft', 'Rejected');";
                await using var cmd = new SqlCommand(update, con, tx);
                AddResourceParameters(cmd, request);
                Add(cmd, "@ResourceId", SqlDbType.Int, request.ResourceId.Value);
                cmd.Parameters.Add("@RowVer", SqlDbType.Timestamp).Value = rowVersion;
                if (await cmd.ExecuteNonQueryAsync() == 0)
                    throw new DBConcurrencyException("Only draft or rejected resources can be edited, and the resource may have changed. Refresh and try again.");
                resourceId = request.ResourceId.Value;
            }
            else
            {
                const string insert = @"
INSERT dbo.EmployeeResource
    (Slug,Title,Summary,Overview,ResourceType,CategoryId,OwnerDepartmentId,Version,Status,
     PublishStartUtc,PublishEndUtc,NextReviewDateUtc,AuthorUserId,CreatedAtUtc,UpdatedAtUtc)
VALUES
    (@Slug,@Title,@Summary,@Overview,@ResourceType,@CategoryId,@OwnerDepartmentId,@Version,'Draft',
     @PublishStartUtc,@PublishEndUtc,@ReviewDateUtc,@UserId,SYSUTCDATETIME(),SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                await using var cmd = new SqlCommand(insert, con, tx);
                AddResourceParameters(cmd, request);
                Add(cmd, "@UserId", SqlDbType.Int, userId);
                resourceId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            await ReplaceHighlightsAsync(con, tx, resourceId, request.GetHighlights());
            var snapshot = new
            {
                resourceId,
                request.Slug,
                request.Title,
                request.Summary,
                request.Overview,
                request.ResourceType,
                request.CategoryId,
                request.OwnerDepartmentId,
                request.Version,
                request.ReviewDateUtc,
                request.PublishStartUtc,
                request.PublishEndUtc,
                highlights = request.GetHighlights()
            };
            await SaveRevisionAsync(con, tx, resourceId, userId, action, null, snapshot);
            await InsertAuditAsync(con, tx, "EmployeeResource." + action, resourceId, userId, userName, ipAddress, oldItem is null ? null : Snapshot(oldItem), snapshot, null);
            await tx.CommitAsync();
            return await GetByIdAsync(resourceId) ?? throw new InvalidOperationException("Employee Resource was saved but could not be reloaded.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task ChangeStatusAsync(int resourceId, string[] expectedStatuses, string newStatus, int userId, string? userName, string? ipAddress, string? remarks, DateTime? publishStartUtc = null)
    {
        if (!ResourceStatuses.Contains(newStatus)) throw new ArgumentException("Unsupported Employee Resource status.");
        var expected = expectedStatuses.Where(status => !string.IsNullOrWhiteSpace(status)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (expected.Length == 0) throw new ArgumentException("At least one expected status is required.");

        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var current = await ReadResourceRowAsync(con, tx, resourceId) ?? throw new InvalidOperationException("Employee Resource was not found.");
            var expectedParameters = string.Join(",", expected.Select((_, index) => "@Expected" + index));
            var sql = $@"
UPDATE dbo.EmployeeResource
SET Status=@NewStatus,
    UpdatedAtUtc=SYSUTCDATETIME(),
    ApprovedByUserId=CASE WHEN @NewStatus='Published' THEN @UserId ELSE ApprovedByUserId END,
    ApprovedAtUtc=CASE WHEN @NewStatus='Published' THEN SYSUTCDATETIME() ELSE ApprovedAtUtc END,
    PublishStartUtc=CASE WHEN @NewStatus='Published' THEN COALESCE(@PublishStartUtc, PublishStartUtc, SYSUTCDATETIME()) ELSE PublishStartUtc END
WHERE ResourceId=@ResourceId AND Status IN ({expectedParameters});";
            await using var cmd = new SqlCommand(sql, con, tx);
            Add(cmd, "@NewStatus", SqlDbType.VarChar, newStatus, 30);
            Add(cmd, "@UserId", SqlDbType.Int, userId);
            Add(cmd, "@PublishStartUtc", SqlDbType.DateTime2, publishStartUtc);
            Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
            for (var i = 0; i < expected.Length; i++) Add(cmd, "@Expected" + i, SqlDbType.VarChar, expected[i], 30);
            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new DBConcurrencyException("The resource status changed or does not allow this workflow action. Refresh and try again.");

            var newSnapshot = new { resourceId, newStatus, publishStartUtc, remarks };
            await SaveRevisionAsync(con, tx, resourceId, userId, newStatus, remarks, newSnapshot);
            await InsertAuditAsync(con, tx, "EmployeeResource." + newStatus, resourceId, userId, userName, ipAddress, Snapshot(current), newSnapshot, remarks);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> DeleteAsync(int resourceId, string rowVersion, int userId, string? userName, string? ipAddress)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var current = await ReadResourceRowAsync(con, tx, resourceId) ?? throw new InvalidOperationException("Employee Resource was not found.");
            if (!current.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) && !current.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only draft or rejected resources can be deleted. Archive published resources instead.");
            var attachmentPaths = await GetAttachmentPathsAsync(con, tx, resourceId);
            var rowVersionBytes = DecodeRowVersion(rowVersion);
            await using var cmd = new SqlCommand("DELETE FROM dbo.EmployeeResource WHERE ResourceId=@ResourceId AND RowVer=@RowVer AND Status IN ('Draft','Rejected');", con, tx);
            Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
            cmd.Parameters.Add("@RowVer", SqlDbType.Timestamp).Value = rowVersionBytes;
            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new DBConcurrencyException("The resource changed before it could be deleted. Refresh and try again.");
            await InsertAuditAsync(con, tx, "EmployeeResource.Deleted", resourceId, userId, userName, ipAddress, Snapshot(current), null, null);
            await tx.CommitAsync();
            return attachmentPaths;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<EmployeeResourceAttachment> AddAttachmentAsync(int resourceId, string fileName, string storedPath, string extension, long sizeBytes, string mimeType, string? description, int userId, string? userName, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255) throw new ArgumentException("Attachment filename is invalid.");
        if (string.IsNullOrWhiteSpace(storedPath) || !storedPath.StartsWith("/media/employee-resources/", StringComparison.OrdinalIgnoreCase) || storedPath.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Attachment storage path is invalid.");
        if (sizeBytes <= 0) throw new ArgumentException("Attachment is empty.");

        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var resource = await ReadResourceRowAsync(con, tx, resourceId) ?? throw new InvalidOperationException("Employee Resource was not found.");
            if (!resource.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) && !resource.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Attachments can only be changed while a resource is a draft or rejected.");
            const string sql = @"
INSERT dbo.EmployeeResourceAttachment
    (ResourceId,FileName,StoredPath,FileExtension,FileSizeBytes,MimeType,Description,SortOrder,UploadedByUserId,UploadedAtUtc)
VALUES
    (@ResourceId,@FileName,@StoredPath,@Extension,@SizeBytes,@MimeType,@Description,
     (SELECT ISNULL(MAX(SortOrder), -1) + 1 FROM dbo.EmployeeResourceAttachment WHERE ResourceId=@ResourceId),
     @UserId,SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            await using var cmd = new SqlCommand(sql, con, tx);
            Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
            Add(cmd, "@FileName", SqlDbType.NVarChar, fileName.Trim(), 255);
            Add(cmd, "@StoredPath", SqlDbType.NVarChar, storedPath, 1000);
            Add(cmd, "@Extension", SqlDbType.VarChar, extension, 20);
            Add(cmd, "@SizeBytes", SqlDbType.BigInt, sizeBytes);
            Add(cmd, "@MimeType", SqlDbType.VarChar, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, 150);
            Add(cmd, "@Description", SqlDbType.NVarChar, string.IsNullOrWhiteSpace(description) ? null : description.Trim(), 500);
            Add(cmd, "@UserId", SqlDbType.Int, userId);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            var snapshot = new { resourceId, attachmentId = id, fileName, storedPath, extension, sizeBytes, mimeType, description };
            await SaveRevisionAsync(con, tx, resourceId, userId, "AttachmentAdded", null, snapshot);
            await InsertAuditAsync(con, tx, "EmployeeResource.AttachmentAdded", resourceId, userId, userName, ipAddress, null, snapshot, null);
            await tx.CommitAsync();
            return new EmployeeResourceAttachment
            {
                AttachmentId = id,
                FileName = fileName,
                StoredPath = storedPath,
                DownloadUrl = $"/EmployeeResources/attachments/{id}",
                Format = extension.TrimStart('.').ToUpperInvariant(),
                SizeLabel = FormatSize(sizeBytes),
                SizeBytes = sizeBytes,
                MimeType = mimeType,
                Description = description,
                UploadedAtUtc = DateTime.UtcNow,
                IsDemoOnly = false
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<string?> DeleteAttachmentAsync(int attachmentId, int userId, string? userName, string? ipAddress)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            const string select = @"
SELECT a.ResourceId, a.StoredPath, er.Status
FROM dbo.EmployeeResourceAttachment a
INNER JOIN dbo.EmployeeResource er ON er.ResourceId = a.ResourceId
WHERE a.AttachmentId=@AttachmentId;";
            await using var find = new SqlCommand(select, con, tx);
            Add(find, "@AttachmentId", SqlDbType.Int, attachmentId);
            int resourceId;
            string storedPath;
            string status;
            await using (var reader = await find.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("Attachment was not found.");
                resourceId = reader.GetInt32(0);
                storedPath = reader.GetString(1);
                status = reader.GetString(2);
            }
            if (!status.Equals("Draft", StringComparison.OrdinalIgnoreCase) && !status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Attachments can only be changed while a resource is a draft or rejected.");
            await using var delete = new SqlCommand("DELETE FROM dbo.EmployeeResourceAttachment WHERE AttachmentId=@AttachmentId;", con, tx);
            Add(delete, "@AttachmentId", SqlDbType.Int, attachmentId);
            if (await delete.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Attachment was not found.");
            var snapshot = new { attachmentId, resourceId, storedPath };
            await SaveRevisionAsync(con, tx, resourceId, userId, "AttachmentRemoved", null, snapshot);
            await InsertAuditAsync(con, tx, "EmployeeResource.AttachmentRemoved", resourceId, userId, userName, ipAddress, snapshot, null, null);
            await tx.CommitAsync();
            return storedPath;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<EmployeeResourceDownload?> GetPublishedDownloadAsync(int attachmentId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
SELECT a.AttachmentId, a.ResourceId, a.FileName, a.StoredPath, a.MimeType
FROM dbo.EmployeeResourceAttachment a
INNER JOIN dbo.EmployeeResource er ON er.ResourceId=a.ResourceId
INNER JOIN dbo.EmployeeResourceCategory cat ON cat.CategoryId=er.CategoryId
WHERE a.AttachmentId=@AttachmentId
  AND cat.IsActive = 1
  AND er.Status='Published'
  AND (er.PublishStartUtc IS NULL OR er.PublishStartUtc <= SYSUTCDATETIME())
  AND (er.PublishEndUtc IS NULL OR er.PublishEndUtc > SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@AttachmentId", SqlDbType.Int, attachmentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new EmployeeResourceDownload
        {
            AttachmentId = reader.GetInt32(0),
            ResourceId = reader.GetInt32(1),
            FileName = reader.GetString(2),
            StoredPath = reader.GetString(3),
            MimeType = reader.GetString(4)
        };
    }

    public async Task<IReadOnlyList<EmployeeResourceRevision>> GetRevisionsAsync(int resourceId)
    {
        var revisions = new List<EmployeeResourceRevision>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
SELECT rev.RevisionId, rev.Action, rev.Remarks, rev.SnapshotJson, rev.ChangedAtUtc, u.Name
FROM dbo.EmployeeResourceRevision rev
INNER JOIN dbo.[User] u ON u.UserId=rev.ChangedByUserId
WHERE rev.ResourceId=@ResourceId
ORDER BY rev.ChangedAtUtc DESC, rev.RevisionId DESC;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            revisions.Add(new EmployeeResourceRevision
            {
                RevisionId = reader.GetInt32(0),
                Action = reader.GetString(1),
                Remarks = reader.IsDBNull(2) ? null : reader.GetString(2),
                SnapshotJson = reader.IsDBNull(3) ? null : reader.GetString(3),
                ChangedAtUtc = reader.GetDateTime(4),
                ChangedByName = reader.GetString(5)
            });
        }
        return revisions;
    }

    private async Task<IReadOnlyList<EmployeeResourceItem>> HydrateManyAsync(SqlConnection con, SqlTransaction? tx, IReadOnlyList<EmployeeResourceItem> items, bool includeRevisions)
    {
        var result = new List<EmployeeResourceItem>(items.Count);
        foreach (var item in items)
            result.Add(await HydrateAsync(con, tx, item, includeRevisions));
        return result;
    }

    private async Task<EmployeeResourceItem> HydrateAsync(SqlConnection con, SqlTransaction? tx, EmployeeResourceItem item, bool includeRevisions)
    {
        var highlights = new List<string>();
        await using (var cmd = new SqlCommand("SELECT HighlightText FROM dbo.EmployeeResourceHighlight WHERE ResourceId=@ResourceId ORDER BY SortOrder, HighlightId;", con, tx))
        {
            Add(cmd, "@ResourceId", SqlDbType.Int, item.ResourceId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) highlights.Add(reader.GetString(0));
        }
        var attachments = new List<EmployeeResourceAttachment>();
        await using (var cmd = new SqlCommand(@"
SELECT a.AttachmentId, a.FileName, a.StoredPath, a.FileExtension, a.FileSizeBytes, a.MimeType,
       a.Description, a.UploadedAtUtc, u.Name
FROM dbo.EmployeeResourceAttachment a
INNER JOIN dbo.[User] u ON u.UserId=a.UploadedByUserId
WHERE a.ResourceId=@ResourceId
ORDER BY a.SortOrder, a.AttachmentId;", con, tx))
        {
            Add(cmd, "@ResourceId", SqlDbType.Int, item.ResourceId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var storedPath = reader.GetString(2);
                var extension = reader.GetString(3);
                var sizeBytes = reader.GetInt64(4);
                attachments.Add(new EmployeeResourceAttachment
                {
                    AttachmentId = reader.GetInt32(0),
                    FileName = reader.GetString(1),
                    StoredPath = storedPath,
                    DownloadUrl = $"/EmployeeResources/attachments/{reader.GetInt32(0)}",
                    Format = extension.TrimStart('.').ToUpperInvariant(),
                    SizeLabel = FormatSize(sizeBytes),
                    SizeBytes = sizeBytes,
                    MimeType = reader.GetString(5),
                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                    UploadedAtUtc = reader.GetDateTime(7),
                    UploadedByName = reader.GetString(8),
                    IsDemoOnly = false
                });
            }
        }
        var revisions = includeRevisions ? await GetRevisionsUsingConnectionAsync(con, tx, item.ResourceId) : Array.Empty<EmployeeResourceRevision>();
        return Copy(item, highlights, attachments, revisions);
    }

    private static async Task<IReadOnlyList<EmployeeResourceRevision>> GetRevisionsUsingConnectionAsync(SqlConnection con, SqlTransaction? tx, int resourceId)
    {
        var revisions = new List<EmployeeResourceRevision>();
        await using var cmd = new SqlCommand(@"
SELECT rev.RevisionId, rev.Action, rev.Remarks, rev.SnapshotJson, rev.ChangedAtUtc, u.Name
FROM dbo.EmployeeResourceRevision rev
INNER JOIN dbo.[User] u ON u.UserId=rev.ChangedByUserId
WHERE rev.ResourceId=@ResourceId ORDER BY rev.ChangedAtUtc DESC, rev.RevisionId DESC;", con, tx);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) revisions.Add(new EmployeeResourceRevision
        {
            RevisionId = reader.GetInt32(0), Action = reader.GetString(1),
            Remarks = reader.IsDBNull(2) ? null : reader.GetString(2),
            SnapshotJson = reader.IsDBNull(3) ? null : reader.GetString(3),
            ChangedAtUtc = reader.GetDateTime(4), ChangedByName = reader.GetString(5)
        });
        return revisions;
    }

    private static async Task<EmployeeResourceItem?> ReadResourceRowAsync(SqlConnection con, SqlTransaction? tx, int resourceId)
    {
        await using var cmd = new SqlCommand(ResourceSelect + " WHERE er.ResourceId=@ResourceId;", con, tx);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapResource(reader) : null;
    }

    private static async Task ReplaceHighlightsAsync(SqlConnection con, SqlTransaction tx, int resourceId, IReadOnlyList<string> highlights)
    {
        await using (var delete = new SqlCommand("DELETE FROM dbo.EmployeeResourceHighlight WHERE ResourceId=@ResourceId;", con, tx))
        {
            Add(delete, "@ResourceId", SqlDbType.Int, resourceId);
            await delete.ExecuteNonQueryAsync();
        }
        for (var i = 0; i < highlights.Count; i++)
        {
            await using var insert = new SqlCommand("INSERT dbo.EmployeeResourceHighlight(ResourceId,HighlightText,SortOrder) VALUES(@ResourceId,@Text,@SortOrder);", con, tx);
            Add(insert, "@ResourceId", SqlDbType.Int, resourceId);
            Add(insert, "@Text", SqlDbType.NVarChar, highlights[i], 200);
            Add(insert, "@SortOrder", SqlDbType.Int, i);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureActiveCategoryAsync(SqlConnection con, SqlTransaction tx, int categoryId)
    {
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.EmployeeResourceCategory WHERE CategoryId=@CategoryId AND IsActive=1;", con, tx);
        Add(cmd, "@CategoryId", SqlDbType.Int, categoryId);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
            throw new InvalidOperationException("Select an active IT category for this Employee Resource.");
    }

    private static async Task EnsureItOwnerDepartmentAsync(SqlConnection con, SqlTransaction tx, int? departmentId)
    {
        if (!departmentId.HasValue) return;
        const string sql = @"
SELECT COUNT(1)
FROM dbo.Department
WHERE DeptId=@DepartmentId
  AND Active=1
  AND (Name = N'IT'
       OR Name LIKE N'IT %'
       OR Name LIKE N'% IT'
       OR Name LIKE N'% IT %'
       OR Name LIKE N'%Information Technology%'
       OR Name LIKE N'%Information Systems%'
       OR Name LIKE N'%Cybersecurity%'
       OR Acronym IN (N'IT', N'IS', N'MIS'));";
        await using var cmd = new SqlCommand(sql, con, tx);
        Add(cmd, "@DepartmentId", SqlDbType.Int, departmentId.Value);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
            throw new InvalidOperationException("Select an active IT owner team for this Employee Resource.");
    }

    private static async Task EnsureSlugAvailableAsync(SqlConnection con, SqlTransaction tx, string slug, int? resourceId)
    {
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.EmployeeResource WHERE Slug=@Slug AND (@ResourceId IS NULL OR ResourceId<>@ResourceId);", con, tx);
        Add(cmd, "@Slug", SqlDbType.VarChar, slug, 200);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0) throw new InvalidOperationException("That slug is already used by another Employee Resource.");
    }

    private static async Task<IReadOnlyList<string>> GetAttachmentPathsAsync(SqlConnection con, SqlTransaction tx, int resourceId)
    {
        var paths = new List<string>();
        await using var cmd = new SqlCommand("SELECT StoredPath FROM dbo.EmployeeResourceAttachment WHERE ResourceId=@ResourceId;", con, tx);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) paths.Add(reader.GetString(0));
        return paths;
    }

    private static async Task SaveRevisionAsync(SqlConnection con, SqlTransaction tx, int resourceId, int userId, string action, string? remarks, object snapshot)
    {
        await using var cmd = new SqlCommand(@"
INSERT dbo.EmployeeResourceRevision(ResourceId,Action,Remarks,SnapshotJson,ChangedByUserId,ChangedAtUtc)
VALUES(@ResourceId,@Action,@Remarks,@Snapshot,@UserId,SYSUTCDATETIME());", con, tx);
        Add(cmd, "@ResourceId", SqlDbType.Int, resourceId);
        Add(cmd, "@Action", SqlDbType.VarChar, action, 50);
        Add(cmd, "@Remarks", SqlDbType.NVarChar, remarks, 500);
        Add(cmd, "@Snapshot", SqlDbType.NVarChar, JsonSerializer.Serialize(snapshot), -1);
        Add(cmd, "@UserId", SqlDbType.Int, userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertAuditAsync(SqlConnection con, SqlTransaction tx, string action, int resourceId, int userId, string? userName, string? ipAddress, object? oldValues, object? newValues, string? notes)
    {
        await using var cmd = new SqlCommand(@"
INSERT dbo.AuditTrail(Action,EntityId,EntityType,UserId,UserName,[Timestamp],Notes,OldValues,NewValues,IpAddress)
VALUES(@Action,@EntityId,'EmployeeResource',@UserId,@UserName,SYSUTCDATETIME(),@Notes,@OldValues,@NewValues,@IpAddress);", con, tx);
        Add(cmd, "@Action", SqlDbType.NVarChar, action, 100);
        Add(cmd, "@EntityId", SqlDbType.Int, resourceId);
        Add(cmd, "@UserId", SqlDbType.Int, userId);
        Add(cmd, "@UserName", SqlDbType.NVarChar, userName, 255);
        Add(cmd, "@Notes", SqlDbType.NVarChar, notes, -1);
        Add(cmd, "@OldValues", SqlDbType.NVarChar, oldValues is null ? null : JsonSerializer.Serialize(oldValues), -1);
        Add(cmd, "@NewValues", SqlDbType.NVarChar, newValues is null ? null : JsonSerializer.Serialize(newValues), -1);
        Add(cmd, "@IpAddress", SqlDbType.NVarChar, ipAddress, 50);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddResourceParameters(SqlCommand cmd, SaveEmployeeResourceRequest request)
    {
        Add(cmd, "@Slug", SqlDbType.VarChar, request.Slug.Trim().ToLowerInvariant(), 200);
        Add(cmd, "@Title", SqlDbType.NVarChar, request.Title.Trim(), 180);
        Add(cmd, "@Summary", SqlDbType.NVarChar, request.Summary.Trim(), 500);
        Add(cmd, "@Overview", SqlDbType.NVarChar, request.Overview.Trim(), -1);
        Add(cmd, "@ResourceType", SqlDbType.VarChar, request.ResourceType.Trim(), 50);
        Add(cmd, "@CategoryId", SqlDbType.Int, request.CategoryId);
        Add(cmd, "@OwnerDepartmentId", SqlDbType.Int, request.OwnerDepartmentId);
        Add(cmd, "@Version", SqlDbType.NVarChar, request.Version.Trim(), 20);
        Add(cmd, "@ReviewDateUtc", SqlDbType.DateTime2, request.ReviewDateUtc);
        Add(cmd, "@PublishStartUtc", SqlDbType.DateTime2, request.PublishStartUtc);
        Add(cmd, "@PublishEndUtc", SqlDbType.DateTime2, request.PublishEndUtc);
    }

    private static void NormalizeAndValidate(SaveEmployeeResourceRequest request)
    {
        request.Title = (request.Title ?? string.Empty).Trim();
        request.Slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
        request.Summary = (request.Summary ?? string.Empty).Trim();
        request.Overview = (request.Overview ?? string.Empty).Trim();
        request.ResourceType = CanonicalizeResourceType(request.ResourceType);
        request.Version = CanonicalizeVersion(request.Version);
        request.HighlightsText ??= string.Empty;
        if (!ResourceTypes.Contains(request.ResourceType)) throw new ArgumentException("Unsupported Employee Resource type.");
        if (!Regex.IsMatch(request.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new ArgumentException("Slug must use lowercase letters, numbers, and hyphens.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 180) throw new ArgumentException("Title is required and must not exceed 180 characters.");
        if (request.Summary.Length > 500) throw new ArgumentException("Summary must not exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(request.Overview)) throw new ArgumentException("Overview is required.");
        if (request.CategoryId <= 0) throw new ArgumentException("Select an Employee Resources category.");
        if (!Regex.IsMatch(request.Version, "^v\\d+\\.\\d+$")) throw new ArgumentException("Version must use a value such as v1.0.");
        if (request.PublishStartUtc.HasValue && request.PublishEndUtc.HasValue && request.PublishStartUtc >= request.PublishEndUtc) throw new ArgumentException("Publication start must be before publication end.");
        if (request.GetHighlights().Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Highlights cannot be empty.");
    }

    private static string CanonicalizeResourceType(string? value) =>
        string.Equals((value ?? string.Empty).Trim(), "Forms", StringComparison.OrdinalIgnoreCase)
            ? "Form"
            : (value ?? string.Empty).Trim();

    private static string CanonicalizeVersion(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return Regex.IsMatch(normalized, "^[vV]?\\d+\\.\\d+$")
            ? $"v{normalized.TrimStart('v', 'V')}"
            : normalized;
    }

    private static byte[] DecodeRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Resource version is required. Refresh and try again.");
        try { return Convert.FromBase64String(value); }
        catch (FormatException ex) { throw new ArgumentException("Resource version is invalid. Refresh and try again.", ex); }
    }

    private static EmployeeResourceItem MapResource(SqlDataReader reader)
    {
        var rowVersion = reader.IsDBNull(20) ? string.Empty : Convert.ToBase64String((byte[])reader.GetValue(20));
        return new EmployeeResourceItem
        {
            ResourceId = reader.GetInt32(0),
            RowVersion = rowVersion,
            Slug = reader.GetString(1),
            Title = reader.GetString(2),
            Summary = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Overview = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            ResourceType = CanonicalizeResourceType(reader.GetString(5)),
            CategoryId = reader.GetInt32(6),
            Category = reader.GetString(7),
            CategorySlug = reader.GetString(8),
            OwnerDepartmentId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            OwnerDepartment = reader.IsDBNull(10) ? "Unassigned" : reader.GetString(10),
            Version = CanonicalizeVersion(reader.GetString(11)),
            Status = reader.GetString(12),
            PublishStartUtc = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
            PublishEndUtc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
            ReviewDateUtc = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            AuthorName = reader.IsDBNull(16) ? null : reader.GetString(16),
            ApproverName = reader.IsDBNull(17) ? null : reader.GetString(17),
            CreatedAtUtc = reader.GetDateTime(18),
            LastUpdatedUtc = reader.GetDateTime(19),
            IsDemoData = false
        };
    }

    private static EmployeeResourceItem Copy(EmployeeResourceItem source, IReadOnlyList<string> highlights, IReadOnlyList<EmployeeResourceAttachment> attachments, IReadOnlyList<EmployeeResourceRevision> revisions) => new()
    {
        ResourceId = source.ResourceId, RowVersion = source.RowVersion, Slug = source.Slug, Title = source.Title,
        Summary = source.Summary, Overview = source.Overview, CategoryId = source.CategoryId, Category = source.Category,
        CategorySlug = source.CategorySlug, ResourceType = source.ResourceType, OwnerDepartmentId = source.OwnerDepartmentId,
        OwnerDepartment = source.OwnerDepartment, Version = source.Version, Status = source.Status, IsDemoData = source.IsDemoData,
        PublishStartUtc = source.PublishStartUtc, PublishEndUtc = source.PublishEndUtc, LastUpdatedUtc = source.LastUpdatedUtc,
        ReviewDateUtc = source.ReviewDateUtc, CreatedAtUtc = source.CreatedAtUtc, AuthorName = source.AuthorName,
        ApproverName = source.ApproverName, Highlights = highlights, Attachments = attachments, Revisions = revisions
    };

    private static object Snapshot(EmployeeResourceItem item) => new
    {
        item.ResourceId, item.Slug, item.Title, item.Summary, item.Overview, item.ResourceType,
        item.CategoryId, item.OwnerDepartmentId, item.Version, item.Status, item.PublishStartUtc,
        item.PublishEndUtc, item.ReviewDateUtc, item.LastUpdatedUtc
    };

    private static string EscapeLike(string value) => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 1024) return $"{sizeBytes} B";
        var value = sizeBytes / 1024d;
        if (value < 1024) return $"{value:0.#} KB";
        value /= 1024d;
        if (value < 1024) return $"{value:0.#} MB";
        return $"{value / 1024d:0.#} GB";
    }

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = size == 0 ? command.Parameters.Add(name, type) : command.Parameters.Add(name, type, size);
        parameter.Value = value ?? DBNull.Value;
    }

    private const string ResourceSelect = @"
SELECT er.ResourceId, er.Slug, er.Title, er.Summary, er.Overview, er.ResourceType,
       er.CategoryId, cat.Name, cat.Slug, er.OwnerDepartmentId, dept.Name,
       er.Version, er.Status, er.PublishStartUtc, er.PublishEndUtc, er.NextReviewDateUtc,
       author.Name, approver.Name, er.CreatedAtUtc, er.UpdatedAtUtc, er.RowVer
FROM dbo.EmployeeResource er
INNER JOIN dbo.EmployeeResourceCategory cat ON cat.CategoryId=er.CategoryId
LEFT JOIN dbo.Department dept ON dept.DeptId=er.OwnerDepartmentId
INNER JOIN dbo.[User] author ON author.UserId=er.AuthorUserId
LEFT JOIN dbo.[User] approver ON approver.UserId=er.ApprovedByUserId";
}
