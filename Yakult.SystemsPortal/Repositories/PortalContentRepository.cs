using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

public sealed class PortalContentRepository : IPortalContentRepository
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase) { "Article", "Advisory", "Video", "FAQ", "Announcement", "Guide", "Course" };
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly int _completionThresholdPercent;

    public PortalContentRepository(IConnectionStringProvider connectionStringProvider, IConfiguration config)
    {
        _connectionStringProvider = connectionStringProvider;
        _completionThresholdPercent = Math.Clamp(config.GetValue<int>("Learning:CompletionThresholdPercent", 90), 1, 100);
    }

    public async Task<IReadOnlyList<PortalContentItem>> GetPublishedAsync(string? contentType = null, string? categorySlug = null, string? query = null, int take = 50)
    {
        var items = new List<PortalContentItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT TOP (@Take) pc.ContentId, pc.ContentType, pc.Slug, pc.Title, pc.Summary, pc.BodyMarkdown,
                   cat.Name, cat.Slug, pc.ThumbnailUrl, pc.MediaUrl, pc.Status, pc.IsFeatured, pc.SortOrder,
                   pc.PublishStartUtc, pc.PublishEndUtc, pc.CreatedAtUtc, pc.UpdatedAtUtc, author.Name, approver.Name
            FROM dbo.PortalContent pc
            INNER JOIN dbo.PortalContentCategory cat ON cat.CategoryId = pc.CategoryId
            LEFT JOIN dbo.[User] author ON author.UserId = pc.AuthorUserId
            LEFT JOIN dbo.[User] approver ON approver.UserId = pc.ApprovedByUserId
            WHERE pc.Status = 'Published'
              AND (pc.PublishStartUtc IS NULL OR pc.PublishStartUtc <= SYSUTCDATETIME())
              AND (pc.PublishEndUtc IS NULL OR pc.PublishEndUtc > SYSUTCDATETIME())
              AND (@Type = '' OR pc.ContentType = @Type)
              AND (@Category = '' OR cat.Slug = @Category)
              AND (@Query = '' OR pc.Title LIKE @LikeQuery OR pc.Summary LIKE @LikeQuery OR cat.Name LIKE @LikeQuery)
            ORDER BY pc.IsFeatured DESC, pc.SortOrder, COALESCE(pc.PublishStartUtc, pc.UpdatedAtUtc) DESC;";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 100));
        cmd.Parameters.AddWithValue("@Type", (contentType ?? "").Trim());
        cmd.Parameters.AddWithValue("@Category", (categorySlug ?? "").Trim());
        var normalizedQuery = (query ?? "").Trim();
        cmd.Parameters.AddWithValue("@Query", normalizedQuery);
        cmd.Parameters.AddWithValue("@LikeQuery", $"%{EscapeLike(normalizedQuery)}%");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(Map(reader));
        return items;
    }

    public async Task<PortalContentItem?> GetPublishedBySlugAsync(string slug)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT TOP (1) pc.ContentId, pc.ContentType, pc.Slug, pc.Title, pc.Summary, pc.BodyMarkdown,
                   cat.Name, cat.Slug, pc.ThumbnailUrl, pc.MediaUrl, pc.Status, pc.IsFeatured, pc.SortOrder,
                   pc.PublishStartUtc, pc.PublishEndUtc, pc.CreatedAtUtc, pc.UpdatedAtUtc, author.Name, approver.Name
            FROM dbo.PortalContent pc
            INNER JOIN dbo.PortalContentCategory cat ON cat.CategoryId = pc.CategoryId
            LEFT JOIN dbo.[User] author ON author.UserId = pc.AuthorUserId
            LEFT JOIN dbo.[User] approver ON approver.UserId = pc.ApprovedByUserId
            WHERE pc.Status = 'Published'
              AND pc.Slug = @Slug
              AND (pc.PublishStartUtc IS NULL OR pc.PublishStartUtc <= SYSUTCDATETIME())
              AND (pc.PublishEndUtc IS NULL OR pc.PublishEndUtc > SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Slug", slug.Trim());
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var item = Map(reader);
        return WithLinks(item, await GetLinksAsync(item.ContentId));
    }

    public async Task<IReadOnlyList<PortalContentItem>> GetManagedAsync()
    {
        var items = new List<PortalContentItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT pc.ContentId, pc.ContentType, pc.Slug, pc.Title, pc.Summary, pc.BodyMarkdown,
                   cat.Name, cat.Slug, pc.ThumbnailUrl, pc.MediaUrl,
                   CASE WHEN pc.Status IN ('PendingReview','Rejected') THEN 'Draft' ELSE pc.Status END,
                   pc.IsFeatured, pc.SortOrder,
                   pc.PublishStartUtc, pc.PublishEndUtc, pc.CreatedAtUtc, pc.UpdatedAtUtc, author.Name, approver.Name
            FROM dbo.PortalContent pc INNER JOIN dbo.PortalContentCategory cat ON cat.CategoryId = pc.CategoryId
            LEFT JOIN dbo.[User] author ON author.UserId = pc.AuthorUserId
            LEFT JOIN dbo.[User] approver ON approver.UserId = pc.ApprovedByUserId
            ORDER BY pc.UpdatedAtUtc DESC;";
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(Map(reader));
        var hydrated = new List<PortalContentItem>(items.Count);
        foreach (var item in items) hydrated.Add(WithLinks(item, await GetLinksAsync(item.ContentId)));
        return hydrated;
    }

    public async Task<IReadOnlyList<PortalContentCategory>> GetCategoriesAsync()
    {
        var items = new List<PortalContentCategory>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT CategoryId, Name, Slug, Description FROM dbo.PortalContentCategory WHERE IsActive=1 ORDER BY SortOrder, Name", con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) items.Add(new PortalContentCategory { CategoryId = reader.GetInt32(0), Name = reader.GetString(1), Slug = reader.GetString(2), Description = reader.IsDBNull(3) ? null : reader.GetString(3) });
        return items;
    }

    public async Task<PortalContentItem> SaveDraftAsync(SavePortalContentRequest request, int userId, string? userName)
    {
        Validate(request);
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        try
        {
            int id;
            if (request.ContentId.HasValue)
            {
                const string update = @"
                    UPDATE dbo.PortalContent SET ContentType=@Type, Slug=@Slug, Title=@Title, Summary=@Summary,
                        BodyMarkdown=@Body, CategoryId=@CategoryId, ThumbnailUrl=@Thumbnail, MediaUrl=@Media,
                        IsFeatured=@Featured, SortOrder=@SortOrder, PublishStartUtc=@Start, PublishEndUtc=@End,
                        Status='Draft', ApprovedByUserId=NULL, ApprovedAtUtc=NULL, UpdatedAtUtc=SYSUTCDATETIME()
                    WHERE ContentId=@Id AND Status IN ('Draft','PendingReview','Rejected');";
                await using var cmd = BuildSaveCommand(update, con, tx, request);
                cmd.Parameters.AddWithValue("@Id", request.ContentId.Value);
                if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Only draft content can be edited.");
                id = request.ContentId.Value;
                await using var deleteLinks = new SqlCommand("DELETE FROM dbo.PortalContentLink WHERE ContentId=@Id", con, tx);
                deleteLinks.Parameters.AddWithValue("@Id", id);
                await deleteLinks.ExecuteNonQueryAsync();
            }
            else
            {
                const string insert = @"
                    INSERT dbo.PortalContent (ContentType,Slug,Title,Summary,BodyMarkdown,CategoryId,ThumbnailUrl,MediaUrl,
                        Status,IsFeatured,SortOrder,PublishStartUtc,PublishEndUtc,AuthorUserId,CreatedAtUtc,UpdatedAtUtc)
                    VALUES (@Type,@Slug,@Title,@Summary,@Body,@CategoryId,@Thumbnail,@Media,'Draft',@Featured,@SortOrder,@Start,@End,@UserId,SYSUTCDATETIME(),SYSUTCDATETIME());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";
                await using var cmd = BuildSaveCommand(insert, con, tx, request);
                cmd.Parameters.AddWithValue("@UserId", userId);
                id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            foreach (var link in request.Links)
            {
                ValidateUrl(link.Url, "link");
                await using var linkCmd = new SqlCommand("INSERT dbo.PortalContentLink(ContentId,Label,Url,LinkType,SortOrder) VALUES(@Id,@Label,@Url,@Type,@Sort)", con, tx);
                linkCmd.Parameters.AddWithValue("@Id", id); linkCmd.Parameters.AddWithValue("@Label", link.Label.Trim());
                linkCmd.Parameters.AddWithValue("@Url", link.Url.Trim()); linkCmd.Parameters.AddWithValue("@Type", link.LinkType.Trim());
                linkCmd.Parameters.AddWithValue("@Sort", request.Links.IndexOf(link)); await linkCmd.ExecuteNonQueryAsync();
            }
            await SaveRevisionAsync(con, tx, id, userId, "DraftSaved", null, request);
            await tx.CommitAsync();
            return (await GetManagedAsync()).Single(x => x.ContentId == id);
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task ChangeStatusAsync(int contentId, string[] expectedStatuses, string newStatus, int userId, string? userName, string? remarks, DateTime? publishStartUtc = null)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Published", "Archived", "Draft" };
        if (!allowed.Contains(newStatus)) throw new ArgumentException("Unsupported content status.");
        var expected = expectedStatuses
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (expected.Length == 0) throw new ArgumentException("At least one expected content status is required.");
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);
        var expectedParameters = string.Join(",", expected.Select((_, index) => $"@Expected{index}"));
        var sql = $@"
            UPDATE dbo.PortalContent SET Status=@NewStatus, UpdatedAtUtc=SYSUTCDATETIME(),
                ApprovedByUserId=CASE WHEN @NewStatus='Published' THEN @UserId ELSE ApprovedByUserId END,
                ApprovedAtUtc=CASE WHEN @NewStatus='Published' THEN SYSUTCDATETIME() ELSE ApprovedAtUtc END,
                PublishStartUtc=CASE WHEN @NewStatus='Published' THEN COALESCE(@Start,PublishStartUtc,SYSUTCDATETIME()) ELSE PublishStartUtc END
            WHERE ContentId=@Id AND Status IN ({expectedParameters});";
        await using var cmd = new SqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("@NewStatus", newStatus); cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Start", (object?)publishStartUtc ?? DBNull.Value); cmd.Parameters.AddWithValue("@Id", contentId);
        for (var i = 0; i < expected.Length; i++) cmd.Parameters.AddWithValue($"@Expected{i}", expected[i]);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new DBConcurrencyException("Content status changed; refresh and try again.");
        await SaveRevisionAsync(con, tx, contentId, userId, newStatus, remarks, new { expectedStatuses = expected, newStatus, publishStartUtc });
        await tx.CommitAsync();
    }

    public async Task TrackAsync(string eventType, string targetType, string targetKey)
    {
        if (eventType is not ("View" or "Click")) return;
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            MERGE dbo.PortalContentMetricDaily WITH (HOLDLOCK) AS target
            USING (SELECT CAST(SYSUTCDATETIME() AS date) MetricDate, @Event EventType, @Type TargetType, @Key TargetKey) source
            ON target.MetricDate=source.MetricDate AND target.EventType=source.EventType AND target.TargetType=source.TargetType AND target.TargetKey=source.TargetKey
            WHEN MATCHED THEN UPDATE SET EventCount=target.EventCount+1
            WHEN NOT MATCHED THEN INSERT(MetricDate,EventType,TargetType,TargetKey,EventCount) VALUES(source.MetricDate,source.EventType,source.TargetType,source.TargetKey,1);";
        await using var cmd = new SqlCommand(sql, con); cmd.Parameters.AddWithValue("@Event", eventType); cmd.Parameters.AddWithValue("@Type", targetType[..Math.Min(100,targetType.Length)]); cmd.Parameters.AddWithValue("@Key", targetKey[..Math.Min(200,targetKey.Length)]); await cmd.ExecuteNonQueryAsync();
    }

    public async Task<PortalCourseProgress?> GetCourseProgressAsync(int userId, int contentId)
    {
        if (!await ProgressTableExistsAsync()) return null;
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT ContentId, ProgressPercent, LastPositionSec, DurationSec, IsCompleted, CompletedAtUtc
            FROM dbo.PortalContentProgress WHERE UserId=@UserId AND ContentId=@ContentId;";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ContentId", contentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return MapProgress(reader);
    }

    public async Task<PortalCourseProgress> SaveCourseProgressAsync(int userId, int contentId, int positionSec, int durationSec)
    {
        if (!await ProgressTableExistsAsync()) throw new InvalidOperationException("Course progress tracking is not yet configured.");
        if (contentId <= 0) throw new ArgumentException("Content not found.");
        positionSec = Math.Clamp(positionSec, 0, 6 * 60 * 60);
        durationSec = Math.Clamp(durationSec, 0, 6 * 60 * 60);
        var percent = durationSec > 0 ? Math.Clamp((int)Math.Round(positionSec * 100.0 / durationSec), 0, 100) : 0;
        var completed = durationSec > 0 && positionSec >= durationSec * _completionThresholdPercent / 100;
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            MERGE dbo.PortalContentProgress WITH (HOLDLOCK) AS target
            USING (SELECT @UserId AS UserId, @ContentId AS ContentId
                   WHERE EXISTS (SELECT 1 FROM dbo.PortalContent pc
                                 WHERE pc.ContentId=@ContentId AND pc.Status='Published' AND pc.ContentType IN ('Course','Video'))) AS source
            ON target.UserId=source.UserId AND target.ContentId=source.ContentId
            WHEN MATCHED THEN UPDATE SET
                ProgressPercent=CASE WHEN @Percent>target.ProgressPercent THEN @Percent ELSE target.ProgressPercent END,
                LastPositionSec=CASE WHEN @Position>target.LastPositionSec THEN @Position ELSE target.LastPositionSec END,
                DurationSec=CASE WHEN @Duration>0 THEN @Duration ELSE target.DurationSec END,
                IsCompleted=CASE WHEN target.IsCompleted=1 OR @Completed=1 THEN 1 ELSE target.IsCompleted END,
                CompletedAtUtc=CASE WHEN @Completed=1 AND target.IsCompleted=0 THEN SYSUTCDATETIME() ELSE target.CompletedAtUtc END,
                UpdatedAtUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(UserId,ContentId,ProgressPercent,LastPositionSec,DurationSec,IsCompleted,CompletedAtUtc,StartedAtUtc,UpdatedAtUtc)
                VALUES(@UserId,@ContentId,@Percent,@Position,@Duration,@Completed,
                       CASE WHEN @Completed=1 THEN SYSUTCDATETIME() ELSE NULL END,SYSUTCDATETIME(),SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ContentId", contentId);
        cmd.Parameters.AddWithValue("@Percent", percent);
        cmd.Parameters.AddWithValue("@Position", positionSec);
        cmd.Parameters.AddWithValue("@Duration", durationSec);
        cmd.Parameters.AddWithValue("@Completed", completed);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new ArgumentException("Content not found.");
        return (await GetCourseProgressAsync(userId, contentId)) ?? throw new InvalidOperationException("Course progress could not be saved.");
    }

    public async Task<IReadOnlyDictionary<int, PortalCourseProgress>> GetCourseProgressBatchAsync(int userId, IReadOnlyCollection<int> contentIds)
    {
        if (!await ProgressTableExistsAsync() || contentIds.Count == 0) return new Dictionary<int, PortalCourseProgress>();
        var ids = contentIds.Distinct().Take(100).ToList();
        var result = new Dictionary<int, PortalCourseProgress>(ids.Count);
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        var parameters = string.Join(",", ids.Select((_, index) => $"@Id{index}"));
        var sql = $@"
            SELECT ContentId, ProgressPercent, LastPositionSec, DurationSec, IsCompleted, CompletedAtUtc
            FROM dbo.PortalContentProgress WHERE UserId=@UserId AND ContentId IN ({parameters});";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);
        for (var i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue($"@Id{i}", ids[i]);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = MapProgress(reader);
            result[row.ContentId] = row;
        }
        return result;
    }

    public async Task<IReadOnlyList<MyLearningItem>> GetUserCourseProgressAsync(int userId)
    {
        if (!await ProgressTableExistsAsync()) return Array.Empty<MyLearningItem>();
        var items = new List<MyLearningItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT p.ProgressPercent, p.LastPositionSec, p.DurationSec, p.IsCompleted, p.CompletedAtUtc, p.UpdatedAtUtc,
                   pc.ContentId, pc.ContentType, pc.Slug, pc.Title, pc.Summary, pc.ThumbnailUrl, pc.MediaUrl, cat.Name
            FROM dbo.PortalContentProgress p
            INNER JOIN dbo.PortalContent pc ON pc.ContentId = p.ContentId
            INNER JOIN dbo.PortalContentCategory cat ON cat.CategoryId = pc.CategoryId
            WHERE p.UserId=@UserId
              AND pc.Status='Published'
              AND (pc.PublishStartUtc IS NULL OR pc.PublishStartUtc <= SYSUTCDATETIME())
              AND (pc.PublishEndUtc IS NULL OR pc.PublishEndUtc > SYSUTCDATETIME())
            ORDER BY p.UpdatedAtUtc DESC;";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new MyLearningItem
            {
                ProgressPercent = reader.GetInt32(0),
                LastPositionSec = reader.GetInt32(1),
                DurationSec = reader.GetInt32(2),
                IsCompleted = reader.GetBoolean(3),
                CompletedAtUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                UpdatedAtUtc = reader.GetDateTime(5),
                ContentId = reader.GetInt32(6),
                ContentType = reader.GetString(7),
                Slug = reader.GetString(8),
                Title = reader.GetString(9),
                Summary = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                ThumbnailUrl = reader.IsDBNull(11) ? null : reader.GetString(11),
                MediaUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                CategoryName = reader.GetString(13)
            });
        }
        return items;
    }

    public async Task<CourseCompletionStats> GetCourseCompletionStatsAsync(int contentId, int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var skip = (page - 1) * pageSize;
        if (!await ProgressTableExistsAsync()) return new CourseCompletionStats { Page = page, PageSize = pageSize };
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT COUNT(*) FROM dbo.PortalContentProgress WHERE ContentId=@ContentId;
            SELECT COUNT(*) FROM dbo.PortalContentProgress WHERE ContentId=@ContentId AND IsCompleted=1;
            SELECT p.UserId, u.Name, p.CompletedAtUtc
            FROM dbo.PortalContentProgress p INNER JOIN dbo.[User] u ON u.UserId=p.UserId
            WHERE p.ContentId=@ContentId AND p.IsCompleted=1
            ORDER BY p.CompletedAtUtc
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@ContentId", contentId);
        cmd.Parameters.AddWithValue("@Skip", skip);
        cmd.Parameters.AddWithValue("@Take", pageSize);
        await using var reader = await cmd.ExecuteReaderAsync();
        var started = 0;
        var completed = 0;
        var items = new List<CourseCompletionStatItem>();
        if (await reader.ReadAsync()) started = reader.GetInt32(0);
        await reader.NextResultAsync();
        if (await reader.ReadAsync()) completed = reader.GetInt32(0);
        await reader.NextResultAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new CourseCompletionStatItem
            {
                UserId = reader.GetInt32(0),
                Name = reader.GetString(1),
                CompletedAtUtc = reader.GetDateTime(2)
            });
        }
        return new CourseCompletionStats
        {
            StartedCount = started,
            CompletedCount = completed,
            Page = page,
            PageSize = pageSize,
            TotalPages = completed == 0 ? 0 : (int)Math.Ceiling(completed / (double)pageSize),
            Completed = items
        };
    }

    private bool _tableProbed;
    private bool _progressTableExists;

    private async Task<bool> ProgressTableExistsAsync()
    {
        if (_tableProbed) return _progressTableExists;
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.PortalContentProgress') IS NULL THEN 0 ELSE 1 END", con);
        _progressTableExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
        _tableProbed = true;
        return _progressTableExists;
    }

    private static PortalCourseProgress MapProgress(SqlDataReader r) => new()
    {
        ContentId = r.GetInt32(0),
        ProgressPercent = r.GetInt32(1),
        LastPositionSec = r.GetInt32(2),
        DurationSec = r.GetInt32(3),
        IsCompleted = r.GetBoolean(4),
        CompletedAtUtc = r.IsDBNull(5) ? null : r.GetDateTime(5)
    };

    private async Task<IReadOnlyList<PortalContentLink>> GetLinksAsync(int contentId)
    {
        var links = new List<PortalContentLink>(); await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString()); await con.OpenAsync();
        await using var cmd = new SqlCommand("SELECT LinkId,Label,Url,LinkType FROM dbo.PortalContentLink WHERE ContentId=@Id ORDER BY SortOrder,LinkId", con); cmd.Parameters.AddWithValue("@Id", contentId);
        await using var reader = await cmd.ExecuteReaderAsync(); while(await reader.ReadAsync()) links.Add(new PortalContentLink { LinkId=reader.GetInt32(0), Label=reader.GetString(1), Url=reader.GetString(2), LinkType=reader.GetString(3) }); return links;
    }

    private static PortalContentItem Map(SqlDataReader r) => new() { ContentId=r.GetInt32(0),ContentType=r.GetString(1),Slug=r.GetString(2),Title=r.GetString(3),Summary=r.IsDBNull(4)?"":r.GetString(4),Body=r.IsDBNull(5)?"":r.GetString(5),CategoryName=r.GetString(6),CategorySlug=r.GetString(7),ThumbnailUrl=r.IsDBNull(8)?null:r.GetString(8),MediaUrl=r.IsDBNull(9)?null:r.GetString(9),Status=r.GetString(10),IsFeatured=r.GetBoolean(11),SortOrder=r.GetInt32(12),PublishStartUtc=r.IsDBNull(13)?null:r.GetDateTime(13),PublishEndUtc=r.IsDBNull(14)?null:r.GetDateTime(14),CreatedAtUtc=r.GetDateTime(15),UpdatedAtUtc=r.GetDateTime(16),AuthorName=r.IsDBNull(17)?null:r.GetString(17),ApproverName=r.IsDBNull(18)?null:r.GetString(18)};
    private static PortalContentItem WithLinks(PortalContentItem x, IReadOnlyList<PortalContentLink> links) => new() { ContentId=x.ContentId,ContentType=x.ContentType,Slug=x.Slug,Title=x.Title,Summary=x.Summary,Body=x.Body,CategoryName=x.CategoryName,CategorySlug=x.CategorySlug,ThumbnailUrl=x.ThumbnailUrl,MediaUrl=x.MediaUrl,Status=x.Status,IsFeatured=x.IsFeatured,SortOrder=x.SortOrder,PublishStartUtc=x.PublishStartUtc,PublishEndUtc=x.PublishEndUtc,CreatedAtUtc=x.CreatedAtUtc,UpdatedAtUtc=x.UpdatedAtUtc,AuthorName=x.AuthorName,ApproverName=x.ApproverName,Links=links };
    private static SqlCommand BuildSaveCommand(string sql, SqlConnection con, SqlTransaction tx, SavePortalContentRequest r) { var c=new SqlCommand(sql,con,tx); c.Parameters.AddWithValue("@Type",r.ContentType.Trim());c.Parameters.AddWithValue("@Slug",r.Slug.Trim().ToLowerInvariant());c.Parameters.AddWithValue("@Title",r.Title.Trim());c.Parameters.AddWithValue("@Summary",r.Summary.Trim());c.Parameters.AddWithValue("@Body",r.Body??"");c.Parameters.AddWithValue("@CategoryId",r.CategoryId);c.Parameters.AddWithValue("@Thumbnail",(object?)r.ThumbnailUrl?.Trim()??DBNull.Value);c.Parameters.AddWithValue("@Media",(object?)r.MediaUrl?.Trim()??DBNull.Value);c.Parameters.AddWithValue("@Featured",r.IsFeatured);c.Parameters.AddWithValue("@SortOrder",r.SortOrder);c.Parameters.AddWithValue("@Start",(object?)r.PublishStartUtc??DBNull.Value);c.Parameters.AddWithValue("@End",(object?)r.PublishEndUtc??DBNull.Value);return c; }
    private static async Task SaveRevisionAsync(SqlConnection c,SqlTransaction t,int id,int uid,string action,string? remarks,object snapshot){await using var cmd=new SqlCommand("INSERT dbo.PortalContentRevision(ContentId,Action,Remarks,SnapshotJson,ChangedByUserId,ChangedAtUtc) VALUES(@Id,@Action,@Remarks,@Json,@User,SYSUTCDATETIME())",c,t);cmd.Parameters.AddWithValue("@Id",id);cmd.Parameters.AddWithValue("@Action",action);cmd.Parameters.AddWithValue("@Remarks",(object?)remarks??DBNull.Value);cmd.Parameters.AddWithValue("@Json",JsonSerializer.Serialize(snapshot));cmd.Parameters.AddWithValue("@User",uid);await cmd.ExecuteNonQueryAsync();}
    private static void Validate(SavePortalContentRequest r){if(!Types.Contains(r.ContentType))throw new ArgumentException("Unsupported content type.");if(!Regex.IsMatch(r.Slug??"", "^[a-z0-9]+(?:-[a-z0-9]+)*$"))throw new ArgumentException("Slug must use lowercase letters, numbers, and hyphens.");if(string.IsNullOrWhiteSpace(r.Title)||r.Title.Length>180)throw new ArgumentException("Title is required and must not exceed 180 characters.");if(r.PublishStartUtc.HasValue&&r.PublishEndUtc.HasValue&&r.PublishStartUtc>=r.PublishEndUtc)throw new ArgumentException("Publication start must be before the end.");if(!string.IsNullOrWhiteSpace(r.ThumbnailUrl))ValidateUrl(r.ThumbnailUrl,"thumbnail");if(!string.IsNullOrWhiteSpace(r.MediaUrl))ValidateUrl(r.MediaUrl,"media");}
    private static void ValidateUrl(string value,string label)
    {
        var allowsServerPath = label is "thumbnail" or "media";
        if (allowsServerPath && IsSafeMediaPath(value)) return;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https")
            throw new ArgumentException(allowsServerPath ? $"{label} URL must be an absolute HTTPS address or a /media/ server path." : $"{label} URL must be an absolute HTTPS address.");
    }
    private static bool IsSafeMediaPath(string value) =>
        value.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains('\\')
        && Uri.TryCreate(value, UriKind.Relative, out _);
    private static string EscapeLike(string value)=>value.Replace("[","[[]").Replace("%","[%]").Replace("_","[_]");
}
