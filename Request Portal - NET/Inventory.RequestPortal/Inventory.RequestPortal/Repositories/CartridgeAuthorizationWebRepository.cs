using System;
using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    public interface ICartridgeAuthorizationWebRepository
    {
        Task<CartridgeAuthorizationViewModel?> GetLatestAsync(int employeeId);
        Task<CartridgeAuthorizationViewModel?> GetApprovedAsync(int employeeId);
        Task<int> CreatePendingAsync(int employeeId, int departmentId, Guid submissionSessionId, string requestedModels);
        /// <summary>
        /// Creates a CartridgeAuthorization record for a new submission.
        /// Only IsDeveloper / LevelRank >= 999 accounts are auto-approved; all other employees
        /// (including higher-up positions) are inserted as Pending so they can self-sign.
        /// Returns (wasApproved, authorizationId).
        /// </summary>
        Task<(bool wasApproved, int authorizationId)> CreateAutoOrPendingAsync(int employeeId, int departmentId, Guid submissionSessionId, string requestedModels, int signerUserId);
        Task<List<CartridgeAuthorizationViewModel>> GetPendingByDepartmentAsync(int departmentId, int? queueUserId = null);
        Task<List<CartridgeAuthorizationViewModel>> GetPendingForApproverAsync(int? companyId, int? branchId, int? departmentId, int? approverEmpId);
        Task<List<CartridgeAuthorizationViewModel>> GetAllPendingAsync();
        Task<List<CartridgeAuthorizationViewModel>> GetApprovedByDepartmentAsync(int departmentId);
        Task<int> CreateITAssistedAsync(int employeeId, int departmentId, Guid submissionSessionId, string requestedModels, int itUserId, int authorizedByEmpId, string decision, string? remarks);
        /// <summary>
        /// Creates a Pending CartridgeAuthorization for an IT-assisted request submitted without manual authorization.
        /// The approver will approve digitally from the Authorization Queue.
        /// </summary>
        Task<int> CreateITPendingAsync(int employeeId, int departmentId, Guid submissionSessionId, string requestedModels, int itUserId);
        Task<List<ITApproverViewModel>> GetApproversByScopeAsync(int comId, int branchId, int deptId);
        Task<CartridgeAuthorizationViewModel?> GetByIdAsync(int authorizationId);
        Task<bool> ApproveAsync(int authorizationId, int supervisorUserId, ApproveSignatureData sig);
        Task RejectAsync(int authorizationId);
        Task MarkUsedAsync(int authorizationId);
        Task<List<CartridgeAuthorizationViewModel>> GetHistoryByEmployeeAsync(int employeeId);

        /// <summary>
        /// Server-side paged + filtered version of GetHistoryByEmployeeAsync, for the History
        /// page's table. Unlike the Request Portal's MyRequests history (which groups rows into
        /// submission sessions in C# and therefore can't page in SQL without re-implementing
        /// that aggregation), each row here is already one CartridgeAuthorization record — no
        /// grouping — so filtering and OFFSET/FETCH paging both push straight into the query.
        /// Search matches signed-by, department, branch, and company; status is an exact match.
        /// </summary>
        Task<(List<CartridgeAuthorizationViewModel> Items, int TotalCount)> GetHistoryByEmployeePagedAsync(
            int employeeId, int pageNumber, int pageSize, string? search, string? status);

        /// <summary>Distinct Status values across this employee's full history, for the status
        /// filter dropdown — queried separately (and cheaply) so the dropdown always lists every
        /// status the employee has ever had, not just the ones on the current filtered page.</summary>
        Task<List<string>> GetDistinctHistoryStatusesByEmployeeAsync(int employeeId);
    }

    public class CartridgeAuthorizationWebRepository : ICartridgeAuthorizationWebRepository
    {
        private readonly IConnectionStringProvider _conn;

        public CartridgeAuthorizationWebRepository(IConnectionStringProvider conn)
        {
            _conn = conn;
        }

        // ── Read ─────────────────────────────────────────────────────────────

        public async Task<CartridgeAuthorizationViewModel?> GetLatestAsync(int employeeId)
        {
            const string sql = @"
                SELECT TOP 1
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name AS EmployeeName, d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.EmployeeId = @EmployeeId
                ORDER BY ca.CreatedDate DESC";

            return await QuerySingleAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId));
        }

        public async Task<CartridgeAuthorizationViewModel?> GetApprovedAsync(int employeeId)
        {
            const string sql = @"
                SELECT TOP 1
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name AS EmployeeName, d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.EmployeeId = @EmployeeId AND ca.Status = 'Approved'
                ORDER BY ca.CreatedDate DESC";

            return await QuerySingleAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId));
        }

        public async Task<List<CartridgeAuthorizationViewModel>> GetPendingByDepartmentAsync(int departmentId, int? queueUserId = null)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels,
                    e.Name AS EmployeeName, e.Position AS EmployeePosition,
                    d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.DepartmentId = @DepartmentId
                  AND ca.Status = 'Pending'
                  AND (@QueueUserId IS NULL OR ca.AssignedToUserId = @QueueUserId)
                ORDER BY ca.CreatedDate ASC";

            return await QueryListAsync(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@DepartmentId", departmentId);
                cmd.Parameters.AddWithValue("@QueueUserId", (object?)queueUserId ?? DBNull.Value);
            });
        }

        /// <summary>
        /// Pending authorizations an approver may see and sign. Same rules as the desktop
        /// (Yakult.Inventory.App CartridgeAuthorizationRepository.GetPendingByScopeAsync):
        ///   - scope: the approver's company, branch and department (null = no filter);
        ///   - a Manager-title requester's request (dbo.ApprovalRoleTitle) is visible only to that
        ///     manager, who self-signs, unless the manager has no active user account, in which
        ///     case the other approvers in scope see it so it can still be signed;
        ///   - everyone else's requests are visible to every approver in scope except the requester.
        /// </summary>
        public async Task<List<CartridgeAuthorizationViewModel>> GetPendingForApproverAsync(
            int? companyId, int? branchId, int? departmentId, int? approverEmpId)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels,
                    e.Name AS EmployeeName, e.Position AS EmployeePosition,
                    d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.Status = 'Pending'
                  AND (@CompanyId    IS NULL OR EXISTS (
                           SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                           WHERE bdc.BranchID = e.BranchId AND bdc.CompanyID = @CompanyId))
                  AND (@BranchId     IS NULL OR e.BranchId      = @BranchId)
                  AND (@DepartmentId IS NULL OR ca.DepartmentId = @DepartmentId)
                  AND (
                      (
                          EXISTS (SELECT 1 FROM dbo.ApprovalRoleTitle art
                                  WHERE UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                                    AND art.IsActive = 1 AND art.ApprovalRole = 'Manager')
                          AND (
                                ca.EmployeeId = @ApproverEmpId
                             OR (NOT EXISTS (SELECT 1 FROM dbo.[User] mu
                                             WHERE mu.EmpId = ca.EmployeeId AND mu.IsActive = 1)
                                 AND (@ApproverEmpId IS NULL OR ca.EmployeeId <> @ApproverEmpId))
                          )
                      )
                      OR
                      (
                          NOT EXISTS (SELECT 1 FROM dbo.ApprovalRoleTitle art
                                      WHERE UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                                        AND art.IsActive = 1 AND art.ApprovalRole = 'Manager')
                          AND (@ApproverEmpId IS NULL OR ca.EmployeeId <> @ApproverEmpId)
                      )
                  )
                ORDER BY ca.CreatedDate ASC";

            return await QueryListAsync(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@CompanyId",     (object?)companyId     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId",      (object?)branchId      ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentId",  (object?)departmentId  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ApproverEmpId", (object?)approverEmpId ?? DBNull.Value);
            });
        }

        public async Task<List<CartridgeAuthorizationViewModel>> GetAllPendingAsync()
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels,
                    e.Name AS EmployeeName, e.Position AS EmployeePosition,
                    d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName,
                    b.Name AS BranchName,    c.Name AS CompanyName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e  ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d  ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId  = se.EmpId
                LEFT  JOIN dbo.Branch     b  ON e.BranchId = b.BranchId
                LEFT  JOIN dbo.Company    c  ON e.ComId    = c.ComId
                WHERE ca.Status = 'Pending'
                ORDER BY ca.CreatedDate ASC";

            return await QueryListAsync(sql, _ => { });
        }

        public async Task<List<CartridgeAuthorizationViewModel>> GetApprovedByDepartmentAsync(int departmentId)
        {
            const string sql = @"
                SELECT TOP 100
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels, ca.SubmissionSessionId, ca.Source,
                    e.Name AS EmployeeName, d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.DepartmentId = @DepartmentId AND ca.Status = 'Approved'
                ORDER BY ca.SignedDate DESC";

            return await QueryListAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@DepartmentId", departmentId));
        }

        public async Task<CartridgeAuthorizationViewModel?> GetByIdAsync(int authorizationId)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.SignatureData, ca.SignatureSource, ca.SignatureFileName,
                    ca.RequestedModels, ca.SignerPosition, ca.SignerCompany, ca.SignerBranch,
                    ca.SubmissionSessionId, ca.Source,
                    ca.SubmittedByUserId, ca.Remarks,
                    e.Name AS EmployeeName, d.Name AS DepartmentName,
                    CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department'
                         ELSE COALESCE(se.Name, u.Name)
                    END AS SignedByName,
                    COALESCE(itse.Name, itu.Name) AS SubmittedByName,
                    dist.FulfillmentMethod, dist.ReceivedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e    ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department d    ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]     u    ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee   se   ON u.EmpId                  = se.EmpId
                LEFT  JOIN dbo.[User]     itu  ON ca.SubmittedByUserId     = itu.UserId
                LEFT  JOIN dbo.Employee   itse ON itu.EmpId                = itse.EmpId
                OUTER APPLY (
                    SELECT TOP 1
                        CASE WHEN r2.Description LIKE '%PICKUP%'   THEN 'Pickup'
                             WHEN r2.Description LIKE '%DELIVERY%' THEN 'Delivery'
                        END AS FulfillmentMethod,
                        recv.Name AS ReceivedByName
                    FROM   dbo.Request r2
                    LEFT   JOIN dbo.Employee recv ON r2.ReceivedById = recv.EmpId
                    WHERE  r2.SubmissionSessionId = ca.SubmissionSessionId
                ) dist
                WHERE ca.AuthorizationId = @AuthorizationId";

            return await QuerySingleAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId));
        }

        public async Task<List<CartridgeAuthorizationViewModel>> GetHistoryByEmployeeAsync(int employeeId)
        {
            const string sql = @"
                SELECT TOP 500
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels, ca.SubmissionSessionId, ca.Source,
                    e.Name AS EmployeeName, d.Name AS DepartmentName, CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.EmployeeId = @EmployeeId
                ORDER BY ca.CreatedDate DESC";

            return await QueryListAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId));
        }

        public async Task<(List<CartridgeAuthorizationViewModel> Items, int TotalCount)> GetHistoryByEmployeePagedAsync(
            int employeeId, int pageNumber, int pageSize, string? search, string? status)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            // BranchName/CompanyName weren't joined in GetHistoryByEmployeeAsync above (always
            // came back null via Map()'s ReadOptional) — added here since the History page's
            // search filter is supposed to match against them.
            const string fromWhere = @"
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e ON ca.EmployeeId          = e.EmpId
                INNER JOIN dbo.Department d ON ca.DepartmentId        = d.DeptId
                LEFT  JOIN dbo.Branch     b ON e.BranchId             = b.BranchId
                LEFT  JOIN dbo.Company    c ON e.ComId                = c.ComId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId = se.EmpId
                WHERE ca.EmployeeId = @EmployeeId
                  AND (@Status IS NULL OR ca.Status = @Status)
                  AND (@Search IS NULL
                       OR (CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END) LIKE '%' + @Search + '%'
                       OR d.Name LIKE '%' + @Search + '%'
                       OR b.Name LIKE '%' + @Search + '%'
                       OR c.Name LIKE '%' + @Search + '%')";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();

            // Counted independently of the OFFSET/FETCH query below — a COUNT(*) OVER()
            // column on the paged query would read 0 whenever the requested page is beyond
            // the last one (OFFSET skips past every matching row, so nothing comes back to
            // read the window value from at all), which would misreport "no results" instead
            // of "this page doesn't exist".
            int totalCount;
            using (var countCmd = new SqlCommand($"SELECT COUNT(*) {fromWhere}", con))
            {
                AddFilterParams(countCmd, employeeId, search, status);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var items = new List<CartridgeAuthorizationViewModel>();
            string pageSql = $@"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels, ca.SubmissionSessionId, ca.Source,
                    e.Name AS EmployeeName, d.Name AS DepartmentName, b.Name AS BranchName, c.Name AS CompanyName,
                    CASE WHEN u.IsDeveloper = 1 THEN 'Information Technology Department' ELSE COALESCE(se.Name, u.Name) END AS SignedByName
                {fromWhere}
                ORDER BY ca.CreatedDate DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using (var cmd = new SqlCommand(pageSql, con))
            {
                AddFilterParams(cmd, employeeId, search, status);
                cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) items.Add(Map(r));
            }

            return (items, totalCount);
        }

        private static void AddFilterParams(SqlCommand cmd, int employeeId, string? search, string? status)
        {
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        }

        public async Task<List<string>> GetDistinctHistoryStatusesByEmployeeAsync(int employeeId)
        {
            const string sql = @"
                SELECT DISTINCT Status FROM dbo.CartridgeAuthorization
                WHERE EmployeeId = @EmployeeId
                ORDER BY Status";

            var statuses = new List<string>();
            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) statuses.Add(r.GetString(0));
            return statuses;
        }

        // ── Write ────────────────────────────────────────────────────────────

        public async Task<int> CreatePendingAsync(int employeeId, int departmentId, Guid submissionSessionId, string requestedModels)
        {
            const string sql = @"
                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status, CreatedDate, SubmissionSessionId, RequestedModels, Source)
                VALUES
                    (@EmployeeId, @DepartmentId, 'Pending', GETDATE(), @SubmissionSessionId, @RequestedModels, 'Web');
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmployeeId",          employeeId);
            cmd.Parameters.AddWithValue("@DepartmentId",        departmentId);
            cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
            cmd.Parameters.AddWithValue("@RequestedModels",     (object?)requestedModels ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<(bool wasApproved, int authorizationId)> CreateAutoOrPendingAsync(
            int employeeId, int departmentId, Guid submissionSessionId,
            string requestedModels, int signerUserId)
        {
            // Single atomic statement:
            //  1. IsDeveloper / LevelRank >= 999 → auto-approved (no longer applies to position titles).
            //  2. All other employees (including higher-up positions) → Pending so they can self-sign.
            //  3. If auto-approved, flips all linked requests to 'Under Review'.
            //     NULLIF(@SignerUserId, 0) stores NULL instead of 0 for unresolved users,
            //     which satisfies the FK to dbo.[User] without raising a violation.
            const string sql = @"
                DECLARE @Status     VARCHAR(20);
                DECLARE @SignedBy   INT      = NULL;
                DECLARE @SignedDate DATETIME = NULL;
                DECLARE @AuthId     INT;

                IF (
                    -- IsDeveloper = 1 OR admin-assigned IT level (LevelRank >= 999)
                    EXISTS (
                        SELECT 1
                        FROM   dbo.[User]          u
                        LEFT JOIN dbo.AccountLevel al ON u.LevelId = al.LevelId
                        WHERE  u.EmpId = @EmployeeId
                          AND  (u.IsDeveloper = 1 OR ISNULL(al.LevelRank, 0) >= 999)
                    )
                )
                BEGIN
                    SET @Status    = 'Approved';
                    SET @SignedBy  = NULLIF(@SignerUserId, 0);
                    SET @SignedDate = GETDATE();
                END
                ELSE
                BEGIN
                    SET @Status = 'Pending';
                END

                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status,
                     SignedBySupervisorId, SignedDate,
                     CreatedDate, SubmissionSessionId, RequestedModels, Source)
                VALUES
                    (@EmployeeId, @DepartmentId, @Status,
                     @SignedBy, @SignedDate,
                     GETDATE(), @SubmissionSessionId, @RequestedModels, 'Web');

                SET @AuthId = CAST(SCOPE_IDENTITY() AS INT);

                -- Immediately unlock requests for IT processing if auto-approved.
                IF @Status = 'Approved'
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SubmissionSessionId
                      AND  Status = 'Awaiting Authorization';
                END

                SELECT @Status AS Status, @AuthId AS AuthId;";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmployeeId",          employeeId);
            cmd.Parameters.AddWithValue("@DepartmentId",        departmentId);
            cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
            cmd.Parameters.AddWithValue("@RequestedModels",     (object?)requestedModels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignerUserId",        signerUserId);

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            string statusResult = reader.GetString(0);
            int authId = reader.GetInt32(1);
            return (statusResult == "Approved", authId);
        }

        public async Task<int> CreateITAssistedAsync(
            int employeeId, int departmentId, Guid submissionSessionId,
            string requestedModels, int itUserId,
            int authorizedByEmpId, string decision, string? remarks)
        {
            const string sql = @"
                DECLARE @AuthId INT;

                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status,
                     AuthorizedByEmpId, SignedDate,
                     CreatedDate, SubmissionSessionId, RequestedModels,
                     SignatureSource, SubmittedByUserId, Remarks, Source)
                VALUES
                    (NULLIF(@EmployeeId, 0), NULLIF(@DepartmentId, 0), @Decision,
                     NULLIF(@AuthorizedByEmpId, 0), GETDATE(),
                     GETDATE(), @SubmissionSessionId, @RequestedModels,
                     'IT_MANUAL', NULLIF(@ItUserId, 0), @Remarks, 'Web');

                SET @AuthId = CAST(SCOPE_IDENTITY() AS INT);

                IF @Decision = 'Approved'
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SubmissionSessionId
                      AND  Status = 'Awaiting Authorization';
                END

                SELECT @AuthId;";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmployeeId",        employeeId);
            cmd.Parameters.AddWithValue("@DepartmentId",      departmentId);
            cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
            cmd.Parameters.AddWithValue("@RequestedModels",   (object?)requestedModels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItUserId",          itUserId);
            cmd.Parameters.AddWithValue("@AuthorizedByEmpId", authorizedByEmpId);
            cmd.Parameters.AddWithValue("@Decision",          decision ?? "Approved");
            cmd.Parameters.AddWithValue("@Remarks",           (object?)remarks ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        private static readonly string[] _hardcodedApproverPositions =
        {
            "COORDINATOR", "ACCOUNT COORDINATOR", "ACTING ACCOUNT COORDINATOR",
            "ASST. COORDINATOR", "ASSISTANT COORDINATOR", "LADY COORDINATOR",
            "MANAGER", "ASST. MANAGER", "ASSISTANT MANAGER",
            "JR. ASST. MANAGER", "JUNIOR ASSISTANT MANAGER", "ACTING JR. ASST. MANAGER",
            "SUPERVISOR",
        };

        private async Task<List<string>> LoadApproverPositionTitlesAsync(SqlConnection con)
        {
            var titles = new List<string>();
            using var cmd = new SqlCommand(
                "SELECT PositionTitle FROM dbo.ApprovalRoleTitle WHERE IsActive = 1", con);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                titles.Add(r.GetString(0).ToUpperInvariant());
            return titles.Count > 0 ? titles : new List<string>(_hardcodedApproverPositions);
        }

        public async Task<List<ITApproverViewModel>> GetApproversByScopeAsync(int comId, int branchId, int deptId)
        {
            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();

            var titles     = await LoadApproverPositionTitlesAsync(con);
            var paramNames = titles.Select((_, i) => $"@pos{i}").ToList();
            var inClause   = string.Join(", ", paramNames);

            string sql = $@"
                SELECT EmpId, DisplayName, Position, ApprovalRole
                FROM (
                    SELECT DISTINCT
                        e.EmpId,
                        e.Name  AS DisplayName,
                        e.Position,
                        COALESCE(art.ApprovalRole,
                            CASE
                                WHEN UPPER(LTRIM(RTRIM(e.Position))) LIKE '%MANAGER%'    THEN 'Manager'
                                WHEN UPPER(LTRIM(RTRIM(e.Position))) LIKE '%SUPERVISOR%' THEN 'Supervisor'
                                ELSE 'Coordinator'
                            END
                        ) AS ApprovalRole
                    FROM dbo.Employee e
                    LEFT JOIN dbo.ApprovalRoleTitle art
                        ON UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                        AND art.IsActive = 1
                    WHERE e.Active    = 1
                      AND e.ComId     = @ComId
                      AND e.BranchId  = @BranchId
                      AND e.DeptId    = @DeptId
                      AND UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause})
                ) sub
                ORDER BY
                    CASE ApprovalRole WHEN 'Manager' THEN 1 WHEN 'Supervisor' THEN 2 ELSE 3 END,
                    DisplayName";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ComId",    comId);
            cmd.Parameters.AddWithValue("@BranchId", branchId);
            cmd.Parameters.AddWithValue("@DeptId",   deptId);
            for (int i = 0; i < titles.Count; i++)
                cmd.Parameters.AddWithValue(paramNames[i], titles[i]);

            var list = new List<ITApproverViewModel>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new ITApproverViewModel
                {
                    EmpId        = r.GetInt32(r.GetOrdinal("EmpId")),
                    DisplayName  = r.IsDBNull(r.GetOrdinal("DisplayName"))  ? string.Empty : r.GetString(r.GetOrdinal("DisplayName")),
                    Position     = r.IsDBNull(r.GetOrdinal("Position"))     ? string.Empty : r.GetString(r.GetOrdinal("Position")),
                    ApprovalRole = r.IsDBNull(r.GetOrdinal("ApprovalRole")) ? string.Empty : r.GetString(r.GetOrdinal("ApprovalRole")),
                });
            }
            return list;
        }

        public async Task<int> CreateITPendingAsync(
            int employeeId, int departmentId, Guid submissionSessionId,
            string requestedModels, int itUserId)
        {
            // Creates a Pending auth record when IT submits without Manual Authorization.
            // The approver will approve digitally from the Authorization Queue.
            const string sql = @"
                DECLARE @AuthId INT;

                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status,
                     CreatedDate, SubmissionSessionId, RequestedModels,
                     SignatureSource, SubmittedByUserId, Source)
                VALUES
                    (NULLIF(@EmployeeId, 0), NULLIF(@DepartmentId, 0), 'Pending',
                     GETDATE(), @SubmissionSessionId, @RequestedModels,
                     'IT_PORTAL', NULLIF(@ItUserId, 0), 'Web');

                SET @AuthId = CAST(SCOPE_IDENTITY() AS INT);
                SELECT @AuthId;";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@EmployeeId",          employeeId);
            cmd.Parameters.AddWithValue("@DepartmentId",        departmentId);
            cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
            cmd.Parameters.AddWithValue("@RequestedModels",     (object?)requestedModels ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItUserId",            itUserId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<bool> ApproveAsync(int authorizationId, int supervisorUserId, ApproveSignatureData sig)
        {
            // 1. Approve the authorization and capture the linked SubmissionSessionId.
            // 2. Flip all matching requests from 'Awaiting Authorization' → 'Under Review'
            //    so they become visible on the WinForms Cartridge Fulfillment page.
            const string sql = @"
                DECLARE @SessionId  UNIQUEIDENTIFIER;
                DECLARE @AuthRows   INT = 0;

                UPDATE dbo.CartridgeAuthorization
                SET    Status               = 'Approved',
                       SignedBySupervisorId = @SupervisorId,
                       SignedDate           = GETDATE(),
                       SignatureData        = @SignatureData,
                       SignatureSource      = @SignatureSource,
                       SignatureFileName    = @SignatureFileName,
                       SignerPosition      = @SignerPosition,
                       SignerCompany       = @SignerCompany,
                       SignerBranch        = @SignerBranch,
                       @SessionId          = SubmissionSessionId
                WHERE  AuthorizationId = @AuthorizationId
                  AND  Status = 'Pending'
                  AND  (
                    -- Developer bypass: always allowed
                    EXISTS (
                        SELECT 1 FROM dbo.[User]
                        WHERE UserId = @SupervisorId AND IsDeveloper = 1
                    )
                    OR
                    -- IT level (LevelRank >= 999): always allowed
                    EXISTS (
                        SELECT 1 FROM dbo.[User] u
                        INNER JOIN dbo.AccountLevel al ON u.LevelId = al.LevelId
                        WHERE u.UserId = @SupervisorId AND al.LevelRank >= 999
                    )
                    OR
                    -- Self-signing: the signer IS the employee on the authorization (higher-up signing own request)
                    EXISTS (
                        SELECT 1
                        FROM dbo.CartridgeAuthorization ca2
                        INNER JOIN dbo.[User] u ON u.EmpId = ca2.EmployeeId
                        WHERE ca2.AuthorizationId = @AuthorizationId
                          AND u.UserId = @SupervisorId
                    )
                    OR
                    -- Rank comparison: approver.LevelRank > requester.LevelRank
                    --
                    -- Approver: LevelId override wins; otherwise derived from Employee.Position.
                    -- Requester: derived directly from Employee.Position — no User account needed
                    --            since most employees use a shared DepartmentAccount.
                    ISNULL((
                        SELECT ISNULL(
                            al_override.LevelRank,
                            CASE
                                WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                         'MANAGER', 'ASST. MANAGER',
                                         'JR. ASST. MANAGER', 'ACTING JR. ASST. MANAGER')
                                    THEN 4
                                WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) = 'SUPERVISOR'
                                    THEN 3
                                WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                         'COORDINATOR', 'ACCOUNT COORDINATOR',
                                         'ACTING ACCOUNT COORDINATOR',
                                         'ASST. COORDINATOR', 'LADY COORDINATOR')
                                    THEN 2
                                WHEN e.EmpId IS NOT NULL THEN 1
                                ELSE 0
                            END
                        )
                        FROM       dbo.[User]       u
                        LEFT JOIN  dbo.AccountLevel al_override ON u.LevelId = al_override.LevelId
                        LEFT JOIN  dbo.Employee     e           ON u.EmpId   = e.EmpId
                        WHERE      u.UserId = @SupervisorId
                    ), 0)
                    >
                    ISNULL((
                        -- Requester rank from Employee.Position — no User join required
                        SELECT CASE
                            WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                     'MANAGER', 'ASST. MANAGER',
                                     'JR. ASST. MANAGER', 'ACTING JR. ASST. MANAGER')
                                THEN 4
                            WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) = 'SUPERVISOR'
                                THEN 3
                            WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                     'COORDINATOR', 'ACCOUNT COORDINATOR',
                                     'ACTING ACCOUNT COORDINATOR',
                                     'ASST. COORDINATOR', 'LADY COORDINATOR')
                                THEN 2
                            ELSE 1
                        END
                        FROM  dbo.CartridgeAuthorization ca
                        INNER JOIN dbo.Employee e ON e.EmpId = ca.EmployeeId
                        WHERE ca.AuthorizationId = @AuthorizationId
                    ), 1)
                  );

                SET @AuthRows = @@ROWCOUNT;

                IF @AuthRows > 0 AND @SessionId IS NOT NULL
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SessionId
                      AND  Status = 'Awaiting Authorization';
                END

                SELECT @AuthRows;";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@AuthorizationId",  authorizationId);
            cmd.Parameters.AddWithValue("@SupervisorId",     supervisorUserId);
            cmd.Parameters.AddWithValue("@SignatureData",    (object?)sig.SignatureData    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignatureSource",  (object?)sig.SignatureSource  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignatureFileName",(object?)sig.SignatureFileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RequestedModels", (object?)sig.RequestedModels  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignerPosition",  (object?)sig.SignerPosition   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignerCompany",   (object?)sig.SignerCompany    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SignerBranch",    (object?)sig.SignerBranch     ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }

        public async Task RejectAsync(int authorizationId)
        {
            const string sql = @"
                UPDATE dbo.CartridgeAuthorization
                SET    Status = 'Rejected'
                WHERE  AuthorizationId = @AuthorizationId AND Status = 'Pending';";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkUsedAsync(int authorizationId)
        {
            const string sql = @"
                UPDATE dbo.CartridgeAuthorization
                SET    Status = 'Used'
                WHERE  AuthorizationId = @AuthorizationId AND Status = 'Approved';";

            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<CartridgeAuthorizationViewModel?> QuerySingleAsync(
            string sql, Action<SqlCommand> setup)
        {
            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            setup(cmd);
            using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? Map(r) : null;
        }

        private async Task<List<CartridgeAuthorizationViewModel>> QueryListAsync(
            string sql, Action<SqlCommand> setup)
        {
            var list = new List<CartridgeAuthorizationViewModel>();
            using var con = new SqlConnection(_conn.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            setup(cmd);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(Map(r));
            return list;
        }

        private static string? SafeStr(SqlDataReader r, int ord) =>
            r.IsDBNull(ord) ? null : r.GetString(ord);

        private static CartridgeAuthorizationViewModel Map(SqlDataReader r)
        {
            int Ord(string n) => r.GetOrdinal(n);

            // Columns may not exist in all queries — guard with try/catch on ordinal lookup
            string? ReadOptional(string col)
            {
                try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetString(o); }
                catch { return null; }
            }
            Guid? ReadOptionalGuid(string col)
            {
                try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetGuid(o); }
                catch { return null; }
            }
            int? ReadOptionalInt(string col)
            {
                try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? null : r.GetInt32(o); }
                catch { return null; }
            }

            return new CartridgeAuthorizationViewModel
            {
                AuthorizationId      = r.GetInt32(Ord("AuthorizationId")),
                EmployeeId           = r.GetInt32(Ord("EmployeeId")),
                DepartmentId         = r.GetInt32(Ord("DepartmentId")),
                Status               = r.GetString(Ord("Status")),
                SignedBySupervisorId = r.IsDBNull(Ord("SignedBySupervisorId")) ? null : r.GetInt32(Ord("SignedBySupervisorId")),
                SignedByName         = SafeStr(r, Ord("SignedByName")),
                SignedDate           = r.IsDBNull(Ord("SignedDate"))   ? null : r.GetDateTime(Ord("SignedDate")),
                CreatedDate          = r.GetDateTime(Ord("CreatedDate")),
                EmployeeName         = SafeStr(r, Ord("EmployeeName")),
                EmployeePosition     = ReadOptional("EmployeePosition"),
                DepartmentName       = SafeStr(r, Ord("DepartmentName")),
                BranchName           = ReadOptional("BranchName"),
                CompanyName          = ReadOptional("CompanyName"),
                SubmissionSessionId  = ReadOptionalGuid("SubmissionSessionId"),
                SourceApp            = ReadOptional("Source"),
                SignatureData        = ReadOptional("SignatureData"),
                SignatureSource      = ReadOptional("SignatureSource"),
                SignatureFileName    = ReadOptional("SignatureFileName"),
                RequestedModels      = ReadOptional("RequestedModels"),
                SignerPosition       = ReadOptional("SignerPosition"),
                SignerCompany        = ReadOptional("SignerCompany"),
                SignerBranch         = ReadOptional("SignerBranch"),
                FulfillmentMethod   = ReadOptional("FulfillmentMethod"),
                ReceivedByName      = ReadOptional("ReceivedByName"),
                Remarks             = ReadOptional("Remarks"),
                SubmittedByName     = ReadOptional("SubmittedByName"),
                SubmittedByUserId   = ReadOptionalInt("SubmittedByUserId"),
            };
        }
    }
}
