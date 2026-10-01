using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

public sealed class CompanyInfoRepository : ICompanyInfoRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public CompanyInfoRepository(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    // ── FAQ reads ────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<CompanyFaqItem>> GetPublishedFaqsAsync(string? query = null, int take = 200)
    {
        var items = new List<CompanyFaqItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT TOP (@Take) FaqId, Question, Answer, Category, SortOrder, IsPublished, UpdatedAt
            FROM dbo.CompanyFaq
            WHERE IsPublished = 1
              AND (@Query = '' OR Question LIKE @Like OR Answer LIKE @Like OR Category LIKE @Like)
            ORDER BY SortOrder, FaqId;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Take", SqlDbType.Int, Math.Clamp(take, 1, 500));
        var q = (query ?? string.Empty).Trim();
        Add(cmd, "@Query", SqlDbType.NVarChar, q, 200);
        Add(cmd, "@Like", SqlDbType.NVarChar, $"%{EscapeLike(q)}%", 402);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapFaq(reader));
        return items;
    }

    public async Task<IReadOnlyList<CompanyFaqItem>> GetManagedFaqsAsync()
    {
        var items = new List<CompanyFaqItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "SELECT FaqId, Question, Answer, Category, SortOrder, IsPublished, UpdatedAt FROM dbo.CompanyFaq ORDER BY SortOrder, FaqId;";
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapFaq(reader));
        return items;
    }

    public async Task<CompanyFaqItem?> GetFaqByIdAsync(int faqId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "SELECT FaqId, Question, Answer, Category, SortOrder, IsPublished, UpdatedAt FROM dbo.CompanyFaq WHERE FaqId = @Id;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Id", SqlDbType.Int, faqId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapFaq(reader) : null;
    }

    public async Task<CompanyFaqItem> SaveFaqAsync(SaveCompanyFaqRequest request, int userId)
    {
        var question = (request.Question ?? string.Empty).Trim();
        var answer = (request.Answer ?? string.Empty).Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (question.Length == 0) throw new InvalidOperationException("Question is required.");
        if (answer.Length == 0) throw new InvalidOperationException("Answer is required.");

        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        if (request.FaqId is > 0)
        {
            const string sql = @"
                UPDATE dbo.CompanyFaq
                SET Question = @Question, Answer = @Answer, Category = @Category,
                    SortOrder = @SortOrder, UpdatedBy = @UserId, UpdatedAt = SYSUTCDATETIME()
                WHERE FaqId = @Id;";
            await using var cmd = new SqlCommand(sql, con);
            Add(cmd, "@Id", SqlDbType.Int, request.FaqId.Value);
            Add(cmd, "@Question", SqlDbType.NVarChar, question, 300);
            Add(cmd, "@Answer", SqlDbType.NVarChar, answer, -1);
            Add(cmd, "@Category", SqlDbType.NVarChar, category, 100);
            Add(cmd, "@SortOrder", SqlDbType.Int, request.SortOrder);
            Add(cmd, "@UserId", SqlDbType.Int, userId);
            if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("FAQ not found.");
            return (await GetFaqByIdAsync(request.FaqId.Value))!;
        }

        const string insert = @"
            INSERT INTO dbo.CompanyFaq (Question, Answer, Category, SortOrder, IsPublished, CreatedBy, UpdatedBy)
            OUTPUT INSERTED.FaqId
            VALUES (@Question, @Answer, @Category, @SortOrder, 0, @UserId, @UserId);";
        await using var insertCmd = new SqlCommand(insert, con);
        Add(insertCmd, "@Question", SqlDbType.NVarChar, question, 300);
        Add(insertCmd, "@Answer", SqlDbType.NVarChar, answer, -1);
        Add(insertCmd, "@Category", SqlDbType.NVarChar, category, 100);
        Add(insertCmd, "@SortOrder", SqlDbType.Int, request.SortOrder);
        Add(insertCmd, "@UserId", SqlDbType.Int, userId);
        var id = (int)(await insertCmd.ExecuteScalarAsync())!;
        return (await GetFaqByIdAsync(id))!;
    }

    public async Task SetFaqPublishedAsync(int faqId, bool published, int userId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "UPDATE dbo.CompanyFaq SET IsPublished = @Published, UpdatedBy = @UserId, UpdatedAt = SYSUTCDATETIME() WHERE FaqId = @Id;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Id", SqlDbType.Int, faqId);
        Add(cmd, "@Published", SqlDbType.Bit, published);
        Add(cmd, "@UserId", SqlDbType.Int, userId);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("FAQ not found.");
    }

    public async Task DeleteFaqAsync(int faqId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.CompanyFaq WHERE FaqId = @Id;", con);
        Add(cmd, "@Id", SqlDbType.Int, faqId);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("FAQ not found.");
    }

    // ── Policy reads/writes ──────────────────────────────────────────────────
    public async Task<IReadOnlyList<CompanyPolicyItem>> GetPublishedPoliciesAsync(string? query = null, int take = 200)
    {
        var items = new List<CompanyPolicyItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = @"
            SELECT TOP (@Take) PolicyId, Title, Slug, Summary, Body, Category, EffectiveDate, SortOrder, IsPublished, UpdatedAt
            FROM dbo.CompanyPolicy
            WHERE IsPublished = 1
              AND (@Query = '' OR Title LIKE @Like OR Summary LIKE @Like OR Body LIKE @Like OR Category LIKE @Like)
            ORDER BY SortOrder, PolicyId;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Take", SqlDbType.Int, Math.Clamp(take, 1, 500));
        var q = (query ?? string.Empty).Trim();
        Add(cmd, "@Query", SqlDbType.NVarChar, q, 200);
        Add(cmd, "@Like", SqlDbType.NVarChar, $"%{EscapeLike(q)}%", 402);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapPolicy(reader));
        return items;
    }

    public async Task<CompanyPolicyItem?> GetPublishedPolicyBySlugAsync(string slug)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "SELECT TOP (1) PolicyId, Title, Slug, Summary, Body, Category, EffectiveDate, SortOrder, IsPublished, UpdatedAt FROM dbo.CompanyPolicy WHERE IsPublished = 1 AND Slug = @Slug;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Slug", SqlDbType.VarChar, (slug ?? string.Empty).Trim().ToLowerInvariant(), 200);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapPolicy(reader) : null;
    }

    public async Task<IReadOnlyList<CompanyPolicyItem>> GetManagedPoliciesAsync()
    {
        var items = new List<CompanyPolicyItem>();
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "SELECT PolicyId, Title, Slug, Summary, Body, Category, EffectiveDate, SortOrder, IsPublished, UpdatedAt FROM dbo.CompanyPolicy ORDER BY SortOrder, PolicyId;";
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapPolicy(reader));
        return items;
    }

    public async Task<CompanyPolicyItem?> GetPolicyByIdAsync(int policyId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "SELECT PolicyId, Title, Slug, Summary, Body, Category, EffectiveDate, SortOrder, IsPublished, UpdatedAt FROM dbo.CompanyPolicy WHERE PolicyId = @Id;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Id", SqlDbType.Int, policyId);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapPolicy(reader) : null;
    }

    public async Task<CompanyPolicyItem> SavePolicyAsync(SaveCompanyPolicyRequest request, int userId)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        var summary = (request.Summary ?? string.Empty).Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        if (title.Length == 0) throw new InvalidOperationException("Title is required.");
        if (body.Length == 0) throw new InvalidOperationException("Body is required.");

        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        if (request.PolicyId is > 0)
        {
            var slug = await UniqueSlugAsync(con, null, title, request.PolicyId.Value);
            const string sql = @"
                UPDATE dbo.CompanyPolicy
                SET Title = @Title, Slug = @Slug, Summary = @Summary, Body = @Body, Category = @Category,
                    EffectiveDate = @EffectiveDate, SortOrder = @SortOrder, UpdatedBy = @UserId, UpdatedAt = SYSUTCDATETIME()
                WHERE PolicyId = @Id;";
            await using var cmd = new SqlCommand(sql, con);
            Add(cmd, "@Id", SqlDbType.Int, request.PolicyId.Value);
            Add(cmd, "@Title", SqlDbType.NVarChar, title, 180);
            Add(cmd, "@Slug", SqlDbType.VarChar, slug, 200);
            Add(cmd, "@Summary", SqlDbType.NVarChar, summary, 500);
            Add(cmd, "@Body", SqlDbType.NVarChar, body, -1);
            Add(cmd, "@Category", SqlDbType.NVarChar, category, 100);
            Add(cmd, "@EffectiveDate", SqlDbType.Date, (object?)request.EffectiveDate ?? DBNull.Value);
            Add(cmd, "@SortOrder", SqlDbType.Int, request.SortOrder);
            Add(cmd, "@UserId", SqlDbType.Int, userId);
            if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Policy not found.");
            return (await GetPolicyByIdAsync(request.PolicyId.Value))!;
        }

        var newSlug = await UniqueSlugAsync(con, null, title, null);
        const string insert = @"
            INSERT INTO dbo.CompanyPolicy (Title, Slug, Summary, Body, Category, EffectiveDate, SortOrder, IsPublished, CreatedBy, UpdatedBy)
            OUTPUT INSERTED.PolicyId
            VALUES (@Title, @Slug, @Summary, @Body, @Category, @EffectiveDate, @SortOrder, 0, @UserId, @UserId);";
        await using var insertCmd = new SqlCommand(insert, con);
        Add(insertCmd, "@Title", SqlDbType.NVarChar, title, 180);
        Add(insertCmd, "@Slug", SqlDbType.VarChar, newSlug, 200);
        Add(insertCmd, "@Summary", SqlDbType.NVarChar, summary, 500);
        Add(insertCmd, "@Body", SqlDbType.NVarChar, body, -1);
        Add(insertCmd, "@Category", SqlDbType.NVarChar, category, 100);
        Add(insertCmd, "@EffectiveDate", SqlDbType.Date, (object?)request.EffectiveDate ?? DBNull.Value);
        Add(insertCmd, "@SortOrder", SqlDbType.Int, request.SortOrder);
        Add(insertCmd, "@UserId", SqlDbType.Int, userId);
        var id = (int)(await insertCmd.ExecuteScalarAsync())!;
        return (await GetPolicyByIdAsync(id))!;
    }

    public async Task SetPolicyPublishedAsync(int policyId, bool published, int userId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        const string sql = "UPDATE dbo.CompanyPolicy SET IsPublished = @Published, UpdatedBy = @UserId, UpdatedAt = SYSUTCDATETIME() WHERE PolicyId = @Id;";
        await using var cmd = new SqlCommand(sql, con);
        Add(cmd, "@Id", SqlDbType.Int, policyId);
        Add(cmd, "@Published", SqlDbType.Bit, published);
        Add(cmd, "@UserId", SqlDbType.Int, userId);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Policy not found.");
    }

    public async Task DeletePolicyAsync(int policyId)
    {
        await using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.CompanyPolicy WHERE PolicyId = @Id;", con);
        Add(cmd, "@Id", SqlDbType.Int, policyId);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Policy not found.");
    }

    // ── Mapping / helpers ────────────────────────────────────────────────────
    private static CompanyFaqItem MapFaq(SqlDataReader r) => new()
    {
        FaqId = r.GetInt32(0),
        Question = r.GetString(1),
        Answer = r.GetString(2),
        Category = r.IsDBNull(3) ? "General" : r.GetString(3),
        SortOrder = r.GetInt32(4),
        IsPublished = r.GetBoolean(5),
        UpdatedAtUtc = r.GetDateTime(6)
    };

    private static CompanyPolicyItem MapPolicy(SqlDataReader r) => new()
    {
        PolicyId = r.GetInt32(0),
        Title = r.GetString(1),
        Slug = r.GetString(2),
        Summary = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        Body = r.IsDBNull(4) ? string.Empty : r.GetString(4),
        Category = r.IsDBNull(5) ? "General" : r.GetString(5),
        EffectiveDate = r.IsDBNull(6) ? null : r.GetDateTime(6),
        SortOrder = r.GetInt32(7),
        IsPublished = r.GetBoolean(8),
        UpdatedAtUtc = r.GetDateTime(9)
    };

    private static async Task<string> UniqueSlugAsync(SqlConnection con, SqlTransaction? tx, string title, int? excludePolicyId)
    {
        var baseName = string.Concat(title.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
        if (baseName.Length == 0) baseName = "policy";
        baseName = baseName[..Math.Min(baseName.Length, 80)];
        var slug = baseName;
        for (var n = 2; ; n++)
        {
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CompanyPolicy WHERE Slug = @Slug AND (@Exclude IS NULL OR PolicyId <> @Exclude);", con, tx);
            Add(cmd, "@Slug", SqlDbType.VarChar, slug, 200);
            Add(cmd, "@Exclude", SqlDbType.Int, (object?)excludePolicyId ?? DBNull.Value);
            if ((int)(await cmd.ExecuteScalarAsync())! == 0) return slug;
            var suffix = "-" + n;
            slug = baseName[..Math.Min(baseName.Length, 80 - suffix.Length)] + suffix;
        }
    }

    private static void Add(SqlCommand cmd, string name, SqlDbType type, object? value, int size = 0)
    {
        var p = size > 0 ? cmd.Parameters.Add(name, type, size) : cmd.Parameters.Add(name, type);
        p.Value = value ?? DBNull.Value;
    }

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
