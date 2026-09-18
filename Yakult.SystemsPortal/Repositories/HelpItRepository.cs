using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

/// <summary>
/// ADO.NET repository backing the "Help IT" ticket submission form and tracker.
/// Ticket creation calls the existing dbo.sp_Call_CreateTicket stored procedure
/// directly (the same one the ITCM desktop app and the mobile/remote API use), so
/// no new ticket-creation logic or database objects are introduced by this feature.
/// </summary>
public sealed class HelpItRepository : IHelpItRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogger<HelpItRepository> _logger;

    public HelpItRepository(
        IConnectionStringProvider connectionStringProvider,
        ILogger<HelpItRepository> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _logger = logger;
    }

    public async Task<(string? CompanyName, string? BranchName, string? DepartmentName)> GetEmployeeOrgNamesAsync(int employeeId)
    {
        const string sql = @"
            SELECT c.Name AS CompanyName, b.Name AS BranchName, d.Name AS DepartmentName
            FROM dbo.Employee e
            LEFT JOIN dbo.Company c ON c.ComId = e.ComId
            LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
            LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
            WHERE e.EmpId = @EmpId;";

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (null, null, null);

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task<IReadOnlyList<HelpItOrganizationCompanyOption>> GetOrganizationOptionsAsync()
    {
        const string sql = @"
            SELECT DISTINCT
                c.ComId,
                c.Name AS CompanyName,
                e.DeptId,
                d.Name AS DepartmentName,
                e.BranchId,
                b.Name AS BranchName
            FROM dbo.Employee e
            INNER JOIN dbo.Company c ON c.ComId = e.ComId
            LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
            LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
            WHERE ISNULL(e.Active, 1) = 1
              AND NULLIF(LTRIM(RTRIM(c.Name)), '') IS NOT NULL
              AND (e.DeptId IS NOT NULL OR e.BranchId IS NOT NULL)
            ORDER BY c.Name, d.Name, b.Name;";

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var cmd = new SqlCommand(sql, con);
        using var reader = await cmd.ExecuteReaderAsync();

        var companies = new Dictionary<int, HelpItOrganizationCompanyOption>();
        while (await reader.ReadAsync())
        {
            var companyId = reader.GetInt32(0);
            var departmentId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var branchId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);

            if (!companies.TryGetValue(companyId, out var company))
            {
                company = new HelpItOrganizationCompanyOption
                {
                    CompanyId = companyId,
                    CompanyName = reader.GetString(1)
                };
                companies.Add(companyId, company);
            }

            var departmentName = reader.IsDBNull(3) ? null : reader.GetString(3);
            var branchName = reader.IsDBNull(5) ? null : reader.GetString(5);

            if (departmentId.HasValue
                && company.Departments.All(item => item.DepartmentId != departmentId.Value))
            {
                company.Departments.Add(new HelpItOrganizationDepartmentOption
                {
                    DepartmentId = departmentId.Value,
                    DepartmentName = departmentName ?? string.Empty
                });
            }

            if (branchId.HasValue
                && company.Branches.All(item => item.BranchId != branchId.Value))
            {
                company.Branches.Add(new HelpItOrganizationBranchOption
                {
                    BranchId = branchId.Value,
                    BranchName = branchName ?? string.Empty
                });
            }

            if (!company.Combinations.Any(item => item.DepartmentId == departmentId && item.BranchId == branchId))
            {
                company.Combinations.Add(new HelpItOrganizationCombination
                {
                    DepartmentId = departmentId,
                    DepartmentName = departmentName,
                    BranchId = branchId,
                    BranchName = branchName
                });
            }
        }

        return companies.Values.ToList();
    }

    public async Task<HelpItOrganizationSelection?> ValidateOrganizationSelectionAsync(
        int companyId,
        int? departmentId,
        int? branchId)
    {
        if (companyId <= 0 || (!departmentId.HasValue && !branchId.HasValue))
            return null;

        const string sql = @"
            SELECT TOP (1)
                c.ComId,
                c.Name AS CompanyName,
                d.DeptId,
                d.Name AS DepartmentName,
                b.BranchId,
                b.Name AS BranchName
            FROM dbo.Company c
            LEFT JOIN dbo.Department d ON d.DeptId = @DepartmentId
            LEFT JOIN dbo.Branch b ON b.BranchId = @BranchId
            WHERE c.ComId = @CompanyId
              AND ISNULL(c.Active, 1) = 1
              AND (@DepartmentId IS NULL OR (
                    ISNULL(d.Active, 1) = 1
                    AND EXISTS (
                        SELECT 1 FROM dbo.Employee e
                        WHERE e.ComId = c.ComId
                          AND e.DeptId = @DepartmentId
                          AND ISNULL(e.Active, 1) = 1
                    )
              ))
              AND (@BranchId IS NULL OR (
                    ISNULL(b.Active, 1) = 1
                    AND EXISTS (
                        SELECT 1 FROM dbo.Employee e
                        WHERE e.ComId = c.ComId
                          AND e.BranchId = @BranchId
                          AND ISNULL(e.Active, 1) = 1
                    )
              ))
              AND (@DepartmentId IS NULL OR @BranchId IS NULL OR EXISTS (
                    SELECT 1 FROM dbo.Employee e
                    WHERE e.ComId = c.ComId
                      AND e.DeptId = @DepartmentId
                      AND e.BranchId = @BranchId
                      AND ISNULL(e.Active, 1) = 1
              ));";

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@CompanyId", SqlDbType.Int).Value = companyId;
        cmd.Parameters.Add("@DepartmentId", SqlDbType.Int).Value = (object?)departmentId ?? DBNull.Value;
        cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new HelpItOrganizationSelection
        {
            CompanyId = reader.GetInt32(0),
            CompanyName = reader.GetString(1),
            DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
            BranchId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            BranchName = reader.IsDBNull(5) ? null : reader.GetString(5)
        };
    }

    public async Task<HelpItTicketContactEmail> GetOrganizationContactEmailAsync(int? companyId, int? departmentId, int? branchId)
    {
        if (!companyId.HasValue && !departmentId.HasValue && !branchId.HasValue)
            return new HelpItTicketContactEmail();

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        var hasDepartmentEmail = await HasTableAsync(con, "dbo.DepartmentEmail");
        var hasDepartmentAccount = await HasTableAsync(con, "dbo.DepartmentAccount");
        var branchEmailSelect = hasDepartmentAccount
            ? "branchEmail.EmailAddress AS BranchEmail"
            : "NULL AS BranchEmail";
        var branchEmailJoin = hasDepartmentAccount
            ? @"
                LEFT JOIN dbo.DepartmentAccount da
                    ON da.CompanyName = c.Name
                   AND da.DepartmentName = d.Name
                   AND da.BranchName = b.Name
                LEFT JOIN dbo.EmailAddress branchEmail
                    ON branchEmail.EmailId = da.EmailAddressId
                   AND branchEmail.IsActive = 1"
            : string.Empty;
        var departmentEmailSelect = hasDepartmentEmail
            ? "departmentEmail.EmailAddress AS DepartmentEmail"
            : "NULL AS DepartmentEmail";
        var departmentEmailJoin = hasDepartmentEmail
            ? @"
                LEFT JOIN dbo.DepartmentEmail de
                    ON de.CompanyName = c.Name
                   AND de.DepartmentName = d.Name
                LEFT JOIN dbo.EmailAddress departmentEmail
                    ON departmentEmail.EmailId = de.EmailAddressId
                   AND departmentEmail.IsActive = 1"
            : string.Empty;

        var sql = $@"
            SELECT TOP (1)
                {branchEmailSelect},
                {departmentEmailSelect}
            FROM (VALUES (1)) AS scopeMarker(Value)
            LEFT JOIN dbo.Company c ON c.ComId = @CompanyId
            LEFT JOIN dbo.Department d ON d.DeptId = @DepartmentId
            LEFT JOIN dbo.Branch b ON b.BranchId = @BranchId
            {branchEmailJoin}
            {departmentEmailJoin};";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@CompanyId", SqlDbType.Int).Value = (object?)companyId ?? DBNull.Value;
        cmd.Parameters.Add("@DepartmentId", SqlDbType.Int).Value = (object?)departmentId ?? DBNull.Value;
        cmd.Parameters.Add("@BranchId", SqlDbType.Int).Value = (object?)branchId ?? DBNull.Value;
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new HelpItTicketContactEmail();

        var branchEmail = GetNullableString(reader, "BranchEmail");
        if (!string.IsNullOrWhiteSpace(branchEmail))
            return new HelpItTicketContactEmail { Email = branchEmail, Source = "Branch email" };

        var departmentEmail = GetNullableString(reader, "DepartmentEmail");
        return new HelpItTicketContactEmail
        {
            Email = departmentEmail,
            Source = string.IsNullOrWhiteSpace(departmentEmail) ? null : "Department email"
        };
    }

    public async Task<bool> CanStoreTicketContactEmailAsync()
    {
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        return await HasColumnAsync(con, "dbo.CallTicket", "TicketSource")
            && await HasColumnAsync(con, "dbo.CallTicket", "ContactEmail");
    }

    public async Task<HelpItTicketSummary> CreateTicketAsync(
        CreateHelpItTicketRequest request,
        int? companyId,
        int? departmentId,
        int? branchId,
        string callerName,
        int? createdByUserId)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Issue))
            throw new ArgumentException("Please describe the issue before submitting.", nameof(request));
        if (string.IsNullOrWhiteSpace(callerName))
            throw new ArgumentException("Caller name could not be resolved for the logged-in account.", nameof(callerName));

        var priority = "Medium"; // Portal-submitted tickets always start at Medium; IT sets the real priority once received in the ITCM desktop app.

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        using var cmd = new SqlCommand("dbo.sp_Call_CreateTicket", con)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@ComId", (object?)companyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DeptId", (object?)departmentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CallerName", callerName.Trim());
        cmd.Parameters.AddWithValue("@Issue", request.Issue.Trim());
        cmd.Parameters.AddWithValue("@ProvidedSolution", DBNull.Value);
        cmd.Parameters.AddWithValue("@IssueType", string.IsNullOrWhiteSpace(request.IssueType) ? (object)DBNull.Value : request.IssueType.Trim());
        cmd.Parameters.AddWithValue("@Priority", priority);
        cmd.Parameters.AddWithValue("@CreatedByUserId", (object?)createdByUserId ?? DBNull.Value);

        // Defensive parameter checks: the SP's signature has evolved across migrations
        // (BranchId and AssignedToEmpId were added later). Verify against the actually
        // deployed procedure before binding, matching the same guard used by
        // Yakult.Inventory.Api2_remote/call-tickets.ashx.
        if (await HasParameterAsync(con, "dbo.sp_Call_CreateTicket", "@BranchId"))
            cmd.Parameters.AddWithValue("@BranchId", (object?)branchId ?? DBNull.Value);

        if (await HasParameterAsync(con, "dbo.sp_Call_CreateTicket", "@AssignedToEmpId"))
            cmd.Parameters.AddWithValue("@AssignedToEmpId", DBNull.Value); // Always unassigned - IT staff pick it up in the desktop app.

        if (await HasParameterAsync(con, "dbo.sp_Call_CreateTicket", "@TicketSource"))
            cmd.Parameters.AddWithValue("@TicketSource", "Portal");

        try
        {
            HelpItTicketSummary ticket;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("The ticket could not be created. The stored procedure did not return a result.");

                ticket = new HelpItTicketSummary
                {
                    TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
                    TicketCode = reader.IsDBNull(reader.GetOrdinal("TicketCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("TicketCode")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("Status")),
                    Priority = reader.IsDBNull(reader.GetOrdinal("Priority")) ? string.Empty : reader.GetString(reader.GetOrdinal("Priority")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.UtcNow : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    Issue = request.Issue.Trim(),
                    IssueType = request.IssueType,
                    CallerName = callerName.Trim()
                };
            }

            // The portal is the authoritative origin for this request. Some deployed
            // procedure revisions declare @TicketSource but did not persist it, so
            // repair only a missing value and never overwrite another source.
            await EnsurePortalTicketSourceAsync(con, ticket.TicketId);
            return ticket;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to create Help IT ticket for caller {CallerName}.", callerName);
            throw new InvalidOperationException("Unable to submit the ticket. Please try again or contact IT directly.", ex);
        }
    }

    public async Task<(List<HelpItTicketSummary> Tickets, int TotalCount)> GetTicketsAsync(HelpItTicketFilter filter)
    {
        filter ??= new HelpItTicketFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 25 : filter.PageSize;
        var offset = (page - 1) * pageSize;

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        var sql = @"
            SELECT
                v.TicketId, v.TicketCode, v.Company, v.Department, v.Branch,
                v.Issue, v.IssueType, v.Status, v.Priority, v.CallerName,
                v.ResponsiblePerson, v.CreatedAt, v.SolvedAt,
                COUNT(*) OVER() AS TotalCount
            FROM dbo.vw_Call_TicketList v
            WHERE 1 = 1";

        using var cmd = new SqlCommand();
        cmd.Connection = con;

        var status = (filter.Status ?? string.Empty).Trim();
        if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND v.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')";
        }
        else if (string.Equals(status, "solved", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND v.Status IN ('Solved', 'Resolved (Temporary)', 'Closed')";
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            sql += " AND (v.TicketCode LIKE @Search OR v.Issue LIKE @Search OR v.CallerName LIKE @Search OR v.Company LIKE @Search OR v.Department LIKE @Search OR v.Branch LIKE @Search)";
            cmd.Parameters.Add("@Search", SqlDbType.NVarChar).Value = $"%{filter.Search.Trim()}%";
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            sql += " AND v.Priority = @Priority";
            cmd.Parameters.Add("@Priority", SqlDbType.NVarChar).Value = filter.Priority.Trim();
        }

        if (!string.IsNullOrWhiteSpace(filter.IssueType))
        {
            sql += " AND v.IssueType = @IssueType";
            cmd.Parameters.Add("@IssueType", SqlDbType.NVarChar).Value = filter.IssueType.Trim();
        }

        sql += " ORDER BY v.CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";
        cmd.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
        cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
        cmd.CommandText = sql;

        var tickets = new List<HelpItTicketSummary>();
        var totalCount = 0;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (totalCount == 0)
                totalCount = reader.IsDBNull(reader.GetOrdinal("TotalCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("TotalCount"));

            tickets.Add(new HelpItTicketSummary
            {
                TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
                TicketCode = reader.IsDBNull(reader.GetOrdinal("TicketCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("TicketCode")),
                Company = reader.IsDBNull(reader.GetOrdinal("Company")) ? null : reader.GetString(reader.GetOrdinal("Company")),
                Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                Branch = reader.IsDBNull(reader.GetOrdinal("Branch")) ? null : reader.GetString(reader.GetOrdinal("Branch")),
                Issue = reader.IsDBNull(reader.GetOrdinal("Issue")) ? string.Empty : reader.GetString(reader.GetOrdinal("Issue")),
                IssueType = reader.IsDBNull(reader.GetOrdinal("IssueType")) ? null : reader.GetString(reader.GetOrdinal("IssueType")),
                Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? string.Empty : reader.GetString(reader.GetOrdinal("Status")),
                Priority = reader.IsDBNull(reader.GetOrdinal("Priority")) ? string.Empty : reader.GetString(reader.GetOrdinal("Priority")),
                CallerName = reader.IsDBNull(reader.GetOrdinal("CallerName")) ? string.Empty : reader.GetString(reader.GetOrdinal("CallerName")),
                ResponsiblePerson = reader.IsDBNull(reader.GetOrdinal("ResponsiblePerson")) ? null : reader.GetString(reader.GetOrdinal("ResponsiblePerson")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.UtcNow : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                SolvedAt = reader.IsDBNull(reader.GetOrdinal("SolvedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SolvedAt"))
            });
        }

        return (tickets, totalCount);
    }

    public async Task<HelpItTicketContactEmail?> GetPortalTicketContactEmailAsync(string ticketCode)
    {
        var normalizedCode = NormalizeTicketCode(ticketCode);
        if (normalizedCode == null)
            return null;

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        // Match the detail-page security boundary. Contact information must only be
        // resolved for tickets that can be verified as Portal-originated.
        if (!await HasColumnAsync(con, "dbo.CallTicket", "TicketSource"))
            return null;

        var hasTicketContactEmail = await HasColumnAsync(con, "dbo.CallTicket", "ContactEmail");
        var hasDepartmentEmail = await HasTableAsync(con, "dbo.DepartmentEmail");
        var hasDepartmentAccount = await HasTableAsync(con, "dbo.DepartmentAccount");

        var ticketContactSelect = hasTicketContactEmail
            ? "NULLIF(LTRIM(RTRIM(t.ContactEmail)), '') AS TicketContactEmail"
            : "NULL AS TicketContactEmail";
        var branchEmailSelect = hasDepartmentAccount
            ? "branchEmail.EmailAddress AS BranchEmail"
            : "NULL AS BranchEmail";
        var branchEmailJoin = hasDepartmentAccount
            ? @"
                LEFT JOIN dbo.DepartmentAccount da
                    ON da.CompanyName = c.Name
                   AND da.DepartmentName = d.Name
                   AND da.BranchName = b.Name
                LEFT JOIN dbo.EmailAddress branchEmail
                    ON branchEmail.EmailId = da.EmailAddressId
                   AND branchEmail.IsActive = 1"
            : string.Empty;
        var departmentEmailSelect = hasDepartmentEmail
            ? "departmentEmail.EmailAddress AS DepartmentEmail"
            : "NULL AS DepartmentEmail";
        var departmentEmailJoin = hasDepartmentEmail
            ? @"
                LEFT JOIN dbo.DepartmentEmail de
                    ON de.CompanyName = c.Name
                   AND de.DepartmentName = d.Name
                LEFT JOIN dbo.EmailAddress departmentEmail
                    ON departmentEmail.EmailId = de.EmailAddressId
                   AND departmentEmail.IsActive = 1"
            : string.Empty;

        var sql = $@"
            SELECT
                {branchEmailSelect},
                {departmentEmailSelect},
                {ticketContactSelect}
            FROM dbo.CallTicket t
            LEFT JOIN dbo.Company c ON c.ComId = t.ComId
            LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
            LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId
            {branchEmailJoin}
            {departmentEmailJoin}
            WHERE t.TicketCode = @TicketCode
              AND UPPER(LTRIM(RTRIM(t.TicketSource))) = 'PORTAL';";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 50).Value = normalizedCode;
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var branchEmail = GetNullableString(reader, "BranchEmail");
        if (!string.IsNullOrWhiteSpace(branchEmail))
            return new HelpItTicketContactEmail { Email = branchEmail, Source = "Branch email" };

        var departmentEmail = GetNullableString(reader, "DepartmentEmail");
        if (!string.IsNullOrWhiteSpace(departmentEmail))
            return new HelpItTicketContactEmail { Email = departmentEmail, Source = "Department email" };

        var ticketContactEmail = GetNullableString(reader, "TicketContactEmail");
        return new HelpItTicketContactEmail
        {
            Email = ticketContactEmail,
            Source = string.IsNullOrWhiteSpace(ticketContactEmail) ? null : "Email provided for this ticket"
        };
    }

    public async Task<bool> SetPortalTicketContactEmailAsync(string ticketCode, string contactEmail)
    {
        var normalizedCode = NormalizeTicketCode(ticketCode);
        var normalizedEmail = (contactEmail ?? string.Empty).Trim();
        if (normalizedCode == null
            || normalizedEmail.Length > 255
            || !System.Net.Mail.MailAddress.TryCreate(normalizedEmail, out _))
        {
            return false;
        }

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        // Do not pretend to save the value when the database migration has not
        // been installed. The controller keeps the dialog open and reports it.
        if (!await HasColumnAsync(con, "dbo.CallTicket", "TicketSource")
            || !await HasColumnAsync(con, "dbo.CallTicket", "ContactEmail"))
        {
            return false;
        }

        const string sql = @"
            UPDATE dbo.CallTicket
            SET ContactEmail = @ContactEmail
            WHERE TicketCode = @TicketCode
              AND UPPER(LTRIM(RTRIM(TicketSource))) = 'PORTAL';";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@ContactEmail", SqlDbType.NVarChar, 255).Value = normalizedEmail;
        cmd.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 50).Value = normalizedCode;
        return await cmd.ExecuteNonQueryAsync() == 1;
    }

    private async Task EnsurePortalTicketSourceAsync(SqlConnection con, int ticketId)
    {
        if (ticketId <= 0 || !await HasColumnAsync(con, "dbo.CallTicket", "TicketSource"))
            return;

        const string sql = @"
            UPDATE dbo.CallTicket
            SET TicketSource = 'Portal'
            WHERE TicketId = @TicketId
              AND NULLIF(LTRIM(RTRIM(TicketSource)), '') IS NULL;";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@TicketId", ticketId);
        var updated = await cmd.ExecuteNonQueryAsync();
        if (updated > 0)
            _logger.LogWarning("Repaired blank TicketSource for portal ticket {TicketId}.", ticketId);
    }

    private static async Task<bool> HasColumnAsync(SqlConnection con, string tableName, string columnName)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(@TableName)
                  AND name = @ColumnName
            ) THEN 1 ELSE 0 END;";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@TableName", tableName);
        cmd.Parameters.AddWithValue("@ColumnName", columnName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> HasParameterAsync(SqlConnection con, string spName, string paramName)
    {
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM sys.parameters p
                INNER JOIN sys.objects o ON o.object_id = p.object_id
                WHERE o.object_id = OBJECT_ID(@SpName) AND o.type = 'P' AND p.name = @ParamName
            ) THEN 1 ELSE 0 END;";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@SpName", spName);
        cmd.Parameters.AddWithValue("@ParamName", paramName);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    public async Task<HelpItTicketDetailsViewModel?> GetPortalTicketDetailsAsync(string ticketCode)
    {
        var normalizedCode = NormalizeTicketCode(ticketCode);
        if (normalizedCode == null)
            return null;

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        // Details are intentionally available for every valid ticket source.
        // Ticket-code validation above remains the boundary for malformed or
        // missing identifiers, and the lookup stays parameterized.
        const string ticketSql = @"
            SELECT t.TicketId, t.TicketCode, c.Name AS Company, d.Name AS Department,
                   b.Name AS Branch, t.Issue, t.IssueType, t.Status, t.Priority,
                   t.CallerName, t.CreatedAt, t.SolvedAt
            FROM dbo.CallTicket t
            LEFT JOIN dbo.Company c ON c.ComId = t.ComId
            LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
            LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId
            WHERE t.TicketCode = @TicketCode;";

        HelpItTicketSummary ticket;
        using (var cmd = new SqlCommand(ticketSql, con))
        {
            cmd.Parameters.Add("@TicketCode", SqlDbType.NVarChar, 50).Value = normalizedCode;
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            ticket = new HelpItTicketSummary
            {
                TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
                TicketCode = GetString(reader, "TicketCode"),
                Company = GetNullableString(reader, "Company"),
                Department = GetNullableString(reader, "Department"),
                Branch = GetNullableString(reader, "Branch"),
                Issue = GetString(reader, "Issue"),
                IssueType = GetNullableString(reader, "IssueType"),
                Status = GetString(reader, "Status"),
                Priority = GetString(reader, "Priority"),
                CallerName = GetString(reader, "CallerName"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                SolvedAt = GetDateTime(reader, "SolvedAt")
            };
        }

        var timeline = new List<HelpItTicketTimelineEvent>();
        if (await HasTableAsync(con, "dbo.CallTicketHistory"))
        {
            const string historySql = @"
                SELECT ChangedAt, FieldName, OldValue, NewValue, Note
                FROM dbo.CallTicketHistory WHERE TicketId = @TicketId ORDER BY ChangedAt ASC, HistoryId ASC;";
            using var cmd = new SqlCommand(historySql, con);
            cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticket.TicketId;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var field = GetString(reader, "FieldName");
                var note = GetNullableString(reader, "Note");
                var oldValue = GetNullableString(reader, "OldValue");
                var newValue = GetNullableString(reader, "NewValue");
                timeline.Add(new HelpItTicketTimelineEvent
                {
                    OccurredAt = GetDateTime(reader, "ChangedAt") ?? ticket.CreatedAt,
                    Title = string.IsNullOrWhiteSpace(field) ? "Ticket updated" : $"{field} updated",
                    Detail = !string.IsNullOrWhiteSpace(note) ? note : BuildChangeDetail(oldValue, newValue),
                    Kind = "history"
                });
            }
        }

        if (await HasTableAsync(con, "dbo.CallTicketNote"))
        {
            const string notesSql = @"
                SELECT CreatedAt, NoteType, NoteText
                FROM dbo.CallTicketNote WHERE TicketId = @TicketId ORDER BY CreatedAt ASC, NoteId ASC;";
            using var cmd = new SqlCommand(notesSql, con);
            cmd.Parameters.Add("@TicketId", SqlDbType.Int).Value = ticket.TicketId;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var noteType = GetString(reader, "NoteType");
                timeline.Add(new HelpItTicketTimelineEvent
                {
                    OccurredAt = GetDateTime(reader, "CreatedAt") ?? ticket.CreatedAt,
                    Title = string.IsNullOrWhiteSpace(noteType) ? "Ticket note" : noteType,
                    Detail = GetNullableString(reader, "NoteText"),
                    Kind = "note"
                });
            }
        }

        if (timeline.Count == 0)
        {
            timeline.Add(new HelpItTicketTimelineEvent
            {
                OccurredAt = ticket.CreatedAt,
                Title = "Ticket submitted",
                Detail = "Your request was submitted through the Yakult Employee Portal.",
                Kind = "created"
            });
        }

        timeline.Sort((left, right) => left.OccurredAt.CompareTo(right.OccurredAt));
        return new HelpItTicketDetailsViewModel { Ticket = ticket, Timeline = timeline };
    }

    private static string? NormalizeTicketCode(string? ticketCode)
    {
        var value = (ticketCode ?? string.Empty).Trim().ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"^TCK-[0-9]{6}$") ? value : null;
    }

    private static string? BuildChangeDetail(string? oldValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(oldValue) && string.IsNullOrWhiteSpace(newValue)) return null;
        if (string.IsNullOrWhiteSpace(oldValue)) return $"Set to {newValue}";
        if (string.IsNullOrWhiteSpace(newValue)) return $"Changed from {oldValue}";
        return $"Changed from {oldValue} to {newValue}";
    }

    private static string GetString(SqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? string.Empty : reader.GetString(reader.GetOrdinal(name));

    private static string? GetNullableString(SqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));

    private static DateTime? GetDateTime(SqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetDateTime(reader.GetOrdinal(name));

    private static async Task<bool> HasTableAsync(SqlConnection con, string tableName)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0 ELSE 1 END;";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = tableName;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }
}
