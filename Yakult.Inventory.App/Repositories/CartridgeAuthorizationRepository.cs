using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    public class CartridgeAuthorizationRepository
    {
        private string ConnStr => DatabaseConfig.ConnectionString;

        // ── Read ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Who approved a submission: the employee behind the digital sign-off
        /// (SignedBySupervisorId -> User -> Employee) or, for IT manual authorizations, the
        /// employee recorded in AuthorizedByEmpId. Returns null when the submission has no
        /// Approved authorization or the approver cannot be resolved to a name.
        /// </summary>
        public Task<(string Name, string Position)?> GetApproverForSessionAsync(Guid submissionSessionId)
            => QueryApproverAsync(
                "ca.SubmissionSessionId = @Key",
                new SqlParameter("@Key", submissionSessionId));

        /// <summary>
        /// Same as <see cref="GetApproverForSessionAsync"/>, for a Set: uses the submission(s)
        /// its requests came from. Returns null for sets that were not created from a portal
        /// submission (there is no authorization to read an approver from).
        /// </summary>
        public Task<(string Name, string Position)?> GetApproverForSetAsync(int setId)
            => QueryApproverAsync(
                @"ca.SubmissionSessionId IN (
                      SELECT r.SubmissionSessionId FROM dbo.Request r
                      WHERE r.SetId = @Key AND r.SubmissionSessionId IS NOT NULL)",
                new SqlParameter("@Key", setId));

        private async Task<(string Name, string Position)?> QueryApproverAsync(string whereClause, SqlParameter key)
        {
            string sql = @"
                SELECT TOP 1
                    COALESCE(ae.Name, se.Name, su.Name)                 AS ApproverName,
                    COALESCE(ae.Position, se.Position, ca.SignerPosition) AS ApproverPosition
                FROM dbo.CartridgeAuthorization ca
                LEFT JOIN dbo.Employee ae ON ae.EmpId  = ca.AuthorizedByEmpId
                LEFT JOIN dbo.[User]   su ON su.UserId = ca.SignedBySupervisorId
                LEFT JOIN dbo.Employee se ON se.EmpId  = su.EmpId
                WHERE " + whereClause + @"
                  AND ca.Status = 'Approved'
                ORDER BY ca.SignedDate DESC, ca.AuthorizationId DESC";

            using (var con = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.Add(key);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return null;

                    string name = reader.IsDBNull(0) ? null : reader.GetString(0);
                    if (string.IsNullOrWhiteSpace(name)) return null;

                    string position = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    return (name, position);
                }
            }
        }

        public async Task<CartridgeAuthorizationModel> GetEmployeeAuthorizationAsync(int employeeId)
        {
            const string sql = @"
                SELECT TOP 1
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name  AS EmployeeName,
                    d.Name  AS DepartmentName,
                    u.Name  AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u ON ca.SignedBySupervisorId  = u.UserId
                WHERE ca.EmployeeId = @EmployeeId
                ORDER BY ca.CreatedDate DESC";

            return await QuerySingleAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId));
        }

        public async Task<List<CartridgeAuthorizationModel>> GetPendingAuthorizationsByDepartmentAsync(int departmentId)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name  AS EmployeeName,
                    d.Name  AS DepartmentName,
                    u.Name  AS SignedByName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u ON ca.SignedBySupervisorId  = u.UserId
                WHERE ca.DepartmentId = @DepartmentId
                  AND ca.Status = 'Pending'
                ORDER BY ca.CreatedDate ASC";

            return await QueryListAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@DepartmentId", departmentId));
        }

        /// <summary>
        /// Returns all Pending authorizations with full employee / org context.
        /// </summary>
        public async Task<List<CartridgeAuthorizationModel>> GetAllPendingAsync()
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    -- Build JSON from CartridgeRequestModel rows for this session so we always
                    -- have accurate qty/good/damaged regardless of what was stored in RequestedModels.
                    -- Request & Set Management submissions (mixed, or Ink / Toner / Printhead only) have
                    -- no CartridgeRequestModel row for their non-cartridge lines, so show the full line list
                    -- saved on the authorization at submit time (what the web approval pages show).
                    -- Cartridge-only submissions keep the CartridgeRequestModel-built list.
                    CASE WHEN ca.RequestedModels IS NOT NULL
                          AND EXISTS (SELECT 1 FROM dbo.Request rw
                                      WHERE rw.SubmissionSessionId = ca.SubmissionSessionId
                                        AND rw.WorkflowType = 'RequestSetManagement')
                         THEN ca.RequestedModels
                         ELSE (
                            SELECT crm.CartridgeModel   AS model,
                                   SUM(crm.RequestedQty) AS qty,
                                   SUM(ISNULL(crm.GoodEmptyQty,    0)) AS good,
                                   SUM(ISNULL(crm.DamagedEmptyQty, 0)) AS damaged
                            FROM   dbo.Request r
                            JOIN   dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                            WHERE  r.SubmissionSessionId = ca.SubmissionSessionId
                            GROUP  BY crm.CartridgeModel
                            FOR JSON PATH
                         )
                    END AS RequestedModels,
                    e.Name      AS EmployeeName,
                    e.Position  AS EmployeePosition,
                    d.Name      AS DepartmentName,
                    u.Name      AS SignedByName,
                    b.Name      AS BranchName,
                    c.Name      AS CompanyName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Branch      b ON e.BranchId = b.BranchId
                OUTER APPLY (
                    SELECT TOP 1 c.Name AS Name
                    FROM   dbo.BranchDepartmentCompany bdc
                    JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                    WHERE  bdc.BranchID = b.BranchId
                    ORDER BY bdc.BranchDeptCompanyID
                ) c
                WHERE ca.Status = 'Pending'
                ORDER BY ca.CreatedDate ASC";

            return await QueryListExtendedAsync(sql, _ => { });
        }

        /// <summary>
        /// Returns Pending authorizations scoped to the supervisor's company, branch,
        /// and/or department. Null parameters are treated as "no filter".
        /// </summary>
        public async Task<List<CartridgeAuthorizationModel>> GetPendingByScopeAsync(
            int? companyId, int? branchId, int? departmentId, int? excludeEmpId = null)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    -- Request & Set Management submissions (mixed, or Ink / Toner / Printhead only) have
                    -- no CartridgeRequestModel row for their non-cartridge lines, so show the full line list
                    -- saved on the authorization at submit time (what the web approval pages show).
                    -- Cartridge-only submissions keep the CartridgeRequestModel-built list.
                    CASE WHEN ca.RequestedModels IS NOT NULL
                          AND EXISTS (SELECT 1 FROM dbo.Request rw
                                      WHERE rw.SubmissionSessionId = ca.SubmissionSessionId
                                        AND rw.WorkflowType = 'RequestSetManagement')
                         THEN ca.RequestedModels
                         ELSE (
                            SELECT crm.CartridgeModel   AS model,
                                   SUM(crm.RequestedQty) AS qty,
                                   SUM(ISNULL(crm.GoodEmptyQty,    0)) AS good,
                                   SUM(ISNULL(crm.DamagedEmptyQty, 0)) AS damaged
                            FROM   dbo.Request r
                            JOIN   dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                            WHERE  r.SubmissionSessionId = ca.SubmissionSessionId
                            GROUP  BY crm.CartridgeModel
                            FOR JSON PATH
                         )
                    END AS RequestedModels,
                    e.Name      AS EmployeeName,
                    e.Position  AS EmployeePosition,
                    d.Name      AS DepartmentName,
                    u.Name      AS SignedByName,
                    b.Name      AS BranchName,
                    c.Name      AS CompanyName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Branch      b ON e.BranchId = b.BranchId
                OUTER APPLY (
                    SELECT TOP 1 c.Name AS Name
                    FROM   dbo.BranchDepartmentCompany bdc
                    JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                    WHERE  bdc.BranchID = b.BranchId
                    ORDER BY bdc.BranchDeptCompanyID
                ) c
                WHERE ca.Status = 'Pending'
                  AND (@CompanyId    IS NULL OR EXISTS (
                           SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                           WHERE bdc.BranchID = b.BranchId AND bdc.CompanyID = @CompanyId
                       ))
                  AND (@BranchId     IS NULL OR e.BranchId       = @BranchId)
                  AND (@DepartmentId IS NULL OR ca.DepartmentId  = @DepartmentId)
                  AND (
                      -- Approver requests (any dbo.ApprovalRoleTitle: Manager, Supervisor,
                      -- Coordinator): visible only to the requester themselves (they self-sign,
                      -- so nobody waits on an absent manager). If the requester has no active
                      -- user account (e.g. an IT-Assisted request made on their behalf) nobody
                      -- could ever sign it, so it falls back to the other approvers in scope.
                      -- MATCHES: Inventory.RequestPortal (Web) CartridgeAuthorizationWebRepository.GetPendingForApproverAsync
                      (
                          EXISTS (
                              SELECT 1 FROM dbo.ApprovalRoleTitle art
                              WHERE UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                                AND art.IsActive = 1
                          )
                          AND (
                                ca.EmployeeId = @ExcludeEmpId
                             OR (NOT EXISTS (SELECT 1 FROM dbo.[User] mu
                                             WHERE mu.EmpId = ca.EmployeeId AND mu.IsActive = 1)
                                 AND (@ExcludeEmpId IS NULL OR ca.EmployeeId <> @ExcludeEmpId))
                          )
                      )
                      OR
                      -- Everyone else's requests: visible to every approver in scope except the requester
                      (
                          NOT EXISTS (
                              SELECT 1 FROM dbo.ApprovalRoleTitle art
                              WHERE UPPER(LTRIM(RTRIM(e.Position))) = UPPER(LTRIM(RTRIM(art.PositionTitle)))
                                AND art.IsActive = 1
                          )
                          AND (@ExcludeEmpId IS NULL OR ca.EmployeeId <> @ExcludeEmpId)
                      )
                  )
                ORDER BY ca.CreatedDate ASC";

            return await QueryListExtendedAsync(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@CompanyId",    (object)companyId    ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BranchId",     (object)branchId     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DepartmentId", (object)departmentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ExcludeEmpId", (object)excludeEmpId ?? DBNull.Value);
            });
        }

        /// <summary>
        /// Approves the authorization, stores signature metadata, and unlocks linked requests.
        /// Returns true when the update succeeded (record was still Pending).
        /// </summary>
        public async Task<bool> ApproveWithSignatureAsync(
            int authorizationId, int supervisorUserId,
            string signatureData, string signatureSource,
            string signerPosition, string signerCompany, string signerBranch)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                DECLARE @SessionId UNIQUEIDENTIFIER;
                SELECT @SessionId = SubmissionSessionId
                FROM   dbo.CartridgeAuthorization
                WHERE  AuthorizationId = @AuthorizationId;

                UPDATE dbo.CartridgeAuthorization
                SET    Status               = 'Approved',
                       SignedBySupervisorId = @SupervisorId,
                       SignedDate           = GETDATE(),
                       SignatureData        = @SignatureData,
                       SignatureSource      = @SignatureSource,
                       SignerPosition       = @SignerPosition,
                       SignerCompany        = @SignerCompany,
                       SignerBranch         = @SignerBranch
                WHERE  AuthorizationId = @AuthorizationId
                  AND  Status = 'Pending';

                IF @@ROWCOUNT > 0
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SessionId
                      AND  Status = 'Awaiting Authorization';
                    SELECT 1;
                END
                ELSE
                    SELECT 0;";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    cmd.Parameters.AddWithValue("@SupervisorId",    supervisorUserId);
                    cmd.Parameters.AddWithValue("@SignatureData",   (object)signatureData   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignatureSource", (object)signatureSource ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignerPosition",  (object)signerPosition  ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignerCompany",   (object)signerCompany   ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignerBranch",    (object)signerBranch    ?? DBNull.Value);
                    var result = await cmd.ExecuteScalarAsync();
                    return result is int i ? i == 1 : false;
                }
            }
        }

        public async Task<List<CartridgeAuthorizationModel>> GetAllAuthorizationsAsync()
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    ca.RequestedModels,
                    e.Name      AS EmployeeName,
                    e.Position  AS EmployeePosition,
                    d.Name      AS DepartmentName,
                    COALESCE(se.Name, u.Name) AS SignedByName,
                    b.Name      AS BranchName,
                    c.Name      AS CompanyName
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e  ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d  ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u  ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee    se ON u.EmpId                  = se.EmpId
                LEFT  JOIN dbo.Branch      b  ON e.BranchId = b.BranchId
                OUTER APPLY (
                    SELECT TOP 1 c.Name AS Name
                    FROM   dbo.BranchDepartmentCompany bdc
                    JOIN   dbo.Company c ON bdc.CompanyID = c.ComId
                    WHERE  bdc.BranchID = b.BranchId
                    ORDER BY bdc.BranchDeptCompanyID
                ) c
                ORDER BY ca.CreatedDate DESC";

            return await QueryListExtendedAsync(sql, _ => { });
        }

        public async Task<List<CartridgeAuthorizationModel>> GetHistoryByEmployeeAsync(int employeeId)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name  AS EmployeeName,
                    d.Name  AS DepartmentName,
                    COALESCE(se.Name, u.Name) AS SignedByName,
                    -- Request & Set Management submissions (mixed, or Ink / Toner / Printhead only) have
                    -- no CartridgeRequestModel row for their non-cartridge lines, so show the full line list
                    -- saved on the authorization at submit time (what the web approval pages show).
                    -- Cartridge-only submissions keep the CartridgeRequestModel-built list.
                    CASE WHEN ca.RequestedModels IS NOT NULL
                          AND EXISTS (SELECT 1 FROM dbo.Request rw
                                      WHERE rw.SubmissionSessionId = ca.SubmissionSessionId
                                        AND rw.WorkflowType = 'RequestSetManagement')
                         THEN ca.RequestedModels
                         ELSE (
                            SELECT crm.CartridgeModel AS model,
                                   SUM(crm.RequestedQty) AS qty
                            FROM   dbo.Request r
                            JOIN   dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                            WHERE  r.SubmissionSessionId = ca.SubmissionSessionId
                            GROUP  BY crm.CartridgeModel
                            FOR JSON PATH
                         )
                    END AS RequestedModels
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e  ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d  ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u  ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee    se ON u.EmpId                  = se.EmpId
                WHERE ca.EmployeeId = @EmployeeId
                ORDER BY ca.CreatedDate DESC";

            return await QueryListAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId));
        }

        public async Task<List<CartridgeAuthorizationModel>> GetSignedByUserAsync(int supervisorUserId)
        {
            const string sql = @"
                SELECT
                    ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                    ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                    e.Name  AS EmployeeName,
                    d.Name  AS DepartmentName,
                    COALESCE(se.Name, u.Name) AS SignedByName,
                    -- Request & Set Management submissions (mixed, or Ink / Toner / Printhead only) have
                    -- no CartridgeRequestModel row for their non-cartridge lines, so show the full line list
                    -- saved on the authorization at submit time (what the web approval pages show).
                    -- Cartridge-only submissions keep the CartridgeRequestModel-built list.
                    CASE WHEN ca.RequestedModels IS NOT NULL
                          AND EXISTS (SELECT 1 FROM dbo.Request rw
                                      WHERE rw.SubmissionSessionId = ca.SubmissionSessionId
                                        AND rw.WorkflowType = 'RequestSetManagement')
                         THEN ca.RequestedModels
                         ELSE (
                            SELECT crm.CartridgeModel AS model,
                                   SUM(crm.RequestedQty) AS qty
                            FROM   dbo.Request r
                            JOIN   dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                            WHERE  r.SubmissionSessionId = ca.SubmissionSessionId
                            GROUP  BY crm.CartridgeModel
                            FOR JSON PATH
                         )
                    END AS RequestedModels
                FROM  dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee    e  ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department  d  ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]      u  ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee    se ON u.EmpId                  = se.EmpId
                WHERE ca.SignedBySupervisorId = @SupervisorUserId
                ORDER BY ca.SignedDate DESC";

            return await QueryListAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@SupervisorUserId", supervisorUserId));
        }

        public async Task<CartridgeAuthorizationModel> GetByIdAsync(int authorizationId)
        {
            const string sql = @"
                SELECT ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                       ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                       e.Name AS EmployeeName, d.Name AS DepartmentName,
                       COALESCE(se.Name, u.Name) AS SignedByName
                FROM   dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e  ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department d  ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]     u  ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee   se ON u.EmpId                  = se.EmpId
                WHERE  ca.AuthorizationId = @AuthorizationId";

            return await QuerySingleAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId));
        }

        /// <summary>
        /// Returns a single authorization with full context — employee, org, signer, and
        /// requested cartridge models (JSON) — for display in the detail dialog.
        /// </summary>
        public async Task<CartridgeAuthorizationModel> GetDetailByIdAsync(int authorizationId)
        {
            const string sql = @"
                SELECT ca.AuthorizationId, ca.EmployeeId, ca.DepartmentId, ca.Status,
                       ca.SignedBySupervisorId, ca.SignedDate, ca.CreatedDate,
                       ca.SubmittedByUserId, ca.Remarks,
                       -- Request & Set Management submissions (mixed, or Ink / Toner / Printhead only) have
                       -- no CartridgeRequestModel row for their non-cartridge lines, so show the full line list
                       -- saved on the authorization at submit time (what the web approval pages show).
                       -- Cartridge-only submissions keep the CartridgeRequestModel-built list.
                       CASE WHEN ca.RequestedModels IS NOT NULL
                             AND EXISTS (SELECT 1 FROM dbo.Request rw
                                         WHERE rw.SubmissionSessionId = ca.SubmissionSessionId
                                           AND rw.WorkflowType = 'RequestSetManagement')
                            THEN ca.RequestedModels
                            ELSE (
                               SELECT crm.CartridgeModel   AS model,
                                      SUM(crm.RequestedQty)                AS qty,
                                      SUM(ISNULL(crm.GoodEmptyQty,    0)) AS good,
                                      SUM(ISNULL(crm.DamagedEmptyQty, 0)) AS damaged
                               FROM   dbo.Request r
                               JOIN   dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                               WHERE  r.SubmissionSessionId = ca.SubmissionSessionId
                               GROUP  BY crm.CartridgeModel
                               FOR JSON PATH
                            )
                       END AS RequestedModels,
                       e.Name     AS EmployeeName,
                       e.Position AS EmployeePosition,
                       d.Name     AS DepartmentName,
                       COALESCE(se.Name, u.Name) AS SignedByName,
                       b.Name     AS BranchName,
                       co.Name    AS CompanyName,
                       dist.FulfillmentMethod,
                       dist.ReceivedByName,
                       COALESCE(itse.Name, itu.Name) AS SubmittedByName
                FROM   dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Employee   e    ON ca.EmployeeId           = e.EmpId
                INNER JOIN dbo.Department d    ON ca.DepartmentId         = d.DeptId
                LEFT  JOIN dbo.[User]     u    ON ca.SignedBySupervisorId  = u.UserId
                LEFT  JOIN dbo.Employee   se   ON u.EmpId                  = se.EmpId
                LEFT  JOIN dbo.[User]     itu  ON ca.SubmittedByUserId     = itu.UserId
                LEFT  JOIN dbo.Employee   itse ON itu.EmpId                = itse.EmpId
                LEFT  JOIN dbo.Branch     b    ON e.BranchId               = b.BranchId
                OUTER APPLY (
                    SELECT TOP 1 c2.Name
                    FROM   dbo.BranchDepartmentCompany bdc
                    JOIN   dbo.Company c2 ON bdc.CompanyID = c2.ComId
                    WHERE  bdc.BranchID = b.BranchId
                    ORDER BY bdc.BranchDeptCompanyID
                ) co (Name)
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
                WHERE  ca.AuthorizationId = @AuthorizationId";

            return await QuerySingleExtendedAsync(sql, cmd =>
                cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId));
        }

        // ── Write ────────────────────────────────────────────────────────────

        public async Task<int> CreatePendingAuthorizationAsync(int employeeId, int departmentId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                INSERT INTO dbo.CartridgeAuthorization (EmployeeId, DepartmentId, Status, CreatedDate)
                VALUES (@EmployeeId, @DepartmentId, 'Pending', GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId",   employeeId);
                    cmd.Parameters.AddWithValue("@DepartmentId", departmentId);
                    return (int)await cmd.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        /// Creates a CartridgeAuthorization for an IT-assisted request with manual authorization.
        /// The IT user selects an approver and records the verbal/offline decision (Approved/Rejected).
        /// SignatureSource = 'IT_MANUAL' permanently distinguishes these from digitally-signed records.
        /// When Approved, linked requests are flipped to 'Under Review'.
        /// When Rejected, linked requests remain at 'Awaiting Authorization'.
        /// Synchronous because CreateCartridgeRequestByModel is synchronous.
        /// </summary>
        public int CreateITAuthorized(
            int employeeId, int departmentId,
            Guid submissionSessionId, string requestedModels,
            int itUserId,
            int authorizedByEmpId,
            string decision,
            string remarks)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                DECLARE @AuthId INT;

                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status,
                     AuthorizedByEmpId, SignedDate,
                     CreatedDate, SubmissionSessionId, RequestedModels,
                     SignatureSource, SubmittedByUserId, Remarks)
                VALUES
                    (NULLIF(@EmployeeId, 0), NULLIF(@DepartmentId, 0), @Decision,
                     NULLIF(@AuthorizedByEmpId, 0), GETDATE(),
                     GETDATE(), @SubmissionSessionId, @RequestedModels,
                     'IT_MANUAL', NULLIF(@ItUserId, 0), @Remarks);

                SET @AuthId = CAST(SCOPE_IDENTITY() AS INT);

                IF @Decision = 'Approved'
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SubmissionSessionId
                      AND  Status = 'Awaiting Authorization';
                END

                SELECT @AuthId;";

            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId",        employeeId);
                    cmd.Parameters.AddWithValue("@DepartmentId",      departmentId);
                    cmd.Parameters.AddWithValue("@ItUserId",          itUserId);
                    cmd.Parameters.AddWithValue("@AuthorizedByEmpId", authorizedByEmpId);
                    cmd.Parameters.AddWithValue("@Decision",          decision ?? "Approved");
                    cmd.Parameters.AddWithValue("@Remarks",           (object)remarks ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
                    cmd.Parameters.AddWithValue("@RequestedModels",   (object)requestedModels ?? DBNull.Value);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        /// <summary>
        /// Creates a Pending CartridgeAuthorization for an IT-assisted request submitted without Manual Authorization.
        /// The approver will approve digitally from the Authorization Queue.
        /// </summary>
        public int CreateITPending(
            int employeeId, int departmentId,
            Guid submissionSessionId, string requestedModels,
            int itUserId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                DECLARE @AuthId INT;

                INSERT INTO dbo.CartridgeAuthorization
                    (EmployeeId, DepartmentId, Status,
                     CreatedDate, SubmissionSessionId, RequestedModels,
                     SignatureSource, SubmittedByUserId)
                VALUES
                    (NULLIF(@EmployeeId, 0), NULLIF(@DepartmentId, 0), 'Pending',
                     GETDATE(), @SubmissionSessionId, @RequestedModels,
                     'IT_PORTAL', NULLIF(@ItUserId, 0));

                SET @AuthId = CAST(SCOPE_IDENTITY() AS INT);
                SELECT @AuthId;";

            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId",          employeeId);
                    cmd.Parameters.AddWithValue("@DepartmentId",        departmentId);
                    cmd.Parameters.AddWithValue("@ItUserId",            itUserId);
                    cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
                    cmd.Parameters.AddWithValue("@RequestedModels",     (object)requestedModels ?? DBNull.Value);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        // Matches the hardcoded fallback in AccountPermissionsPage so both pages agree on who is an approver.
        private static readonly string[] _hardcodedApproverPositions =
        {
            "COORDINATOR", "ACCOUNT COORDINATOR", "ACTING ACCOUNT COORDINATOR",
            "ASST. COORDINATOR", "ASSISTANT COORDINATOR", "LADY COORDINATOR",
            "MANAGER", "ASST. MANAGER", "ASSISTANT MANAGER",
            "JR. ASST. MANAGER", "JUNIOR ASSISTANT MANAGER", "ACTING JR. ASST. MANAGER",
            "SUPERVISOR",
        };

        private List<string> LoadApproverPositionTitles(SqlConnection con)
        {
            var titles = new List<string>();
            using (var cmd = new SqlCommand(
                "SELECT PositionTitle FROM dbo.ApprovalRoleTitle WHERE IsActive = 1", con))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    titles.Add(r.GetString(0).ToUpperInvariant());
            return titles.Count > 0 ? titles : new List<string>(_hardcodedApproverPositions);
        }

        /// <summary>
        /// Returns users who hold an approver-level position within the given company, branch, and department.
        /// Uses the same position-title list as AccountPermissionsPage (DB table, falling back to hardcoded set).
        /// </summary>
        public List<ApproverViewModel> GetApproversByScope(int comId, int branchId, int deptId)
        {
            DatabaseConfig.EnsureConfigured();

            var list = new List<ApproverViewModel>();
            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();

                var titles     = LoadApproverPositionTitles(con);
                var paramNames = Enumerable.Range(0, titles.Count).Select(i => $"@pos{i}").ToList();
                var inClause   = string.Join(", ", paramNames);

                string sql = $@"
                    SELECT EmpId, DisplayName, Position, DepartmentName, BranchName, ApprovalRole
                    FROM (
                        SELECT DISTINCT
                            e.EmpId,
                            e.Name  AS DisplayName,
                            e.Position,
                            d.Name  AS DepartmentName,
                            b.Name  AS BranchName,
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
                        LEFT JOIN dbo.Department d  ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Branch     b  ON e.BranchId = b.BranchId
                        WHERE e.Active   = 1
                          AND e.ComId    = @ComId
                          AND e.BranchId = @BranchId
                          AND e.DeptId   = @DeptId
                          AND UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause})
                    ) sub
                    ORDER BY
                        CASE ApprovalRole WHEN 'Manager' THEN 1 WHEN 'Supervisor' THEN 2 ELSE 3 END,
                        DisplayName";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ComId",    comId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    cmd.Parameters.AddWithValue("@DeptId",   deptId);
                    for (int i = 0; i < titles.Count; i++)
                        cmd.Parameters.AddWithValue(paramNames[i], titles[i]);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new ApproverViewModel
                            {
                                EmpId          = r.GetInt32(r.GetOrdinal("EmpId")),
                                DisplayName    = r.IsDBNull(r.GetOrdinal("DisplayName"))    ? string.Empty : r.GetString(r.GetOrdinal("DisplayName")),
                                Position       = r.IsDBNull(r.GetOrdinal("Position"))       ? string.Empty : r.GetString(r.GetOrdinal("Position")),
                                DepartmentName = r.IsDBNull(r.GetOrdinal("DepartmentName")) ? string.Empty : r.GetString(r.GetOrdinal("DepartmentName")),
                                BranchName     = r.IsDBNull(r.GetOrdinal("BranchName"))     ? string.Empty : r.GetString(r.GetOrdinal("BranchName")),
                                ApprovalRole   = r.IsDBNull(r.GetOrdinal("ApprovalRole"))   ? string.Empty : r.GetString(r.GetOrdinal("ApprovalRole")),
                            });
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Creates a CartridgeAuthorization for a self-submitted portal request.
        /// Mirrors CreateAutoOrPendingAsync in the web portal:
        ///   - If the submitting user is IsDeveloper=1 or AccountLevel.LevelRank >= 999 → auto-approved.
        ///   - Otherwise → Pending; a supervisor must sign via the Authorization Queue.
        /// Returns (wasApproved, authorizationId).
        /// Synchronous because CreateCartridgeRequestByModel is synchronous.
        /// </summary>
        public (bool wasApproved, int authorizationId) CreateAutoOrPending(
            int employeeId, int departmentId,
            Guid submissionSessionId, string requestedModels,
            int signerUserId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                DECLARE @Status     VARCHAR(20);
                DECLARE @SignedBy   INT      = NULL;
                DECLARE @SignedDate DATETIME = NULL;
                DECLARE @AuthId     INT;

                IF EXISTS (
                    SELECT 1
                    FROM   dbo.[User]          u
                    LEFT JOIN dbo.AccountLevel  al  ON u.LevelId = al.LevelId
                    WHERE  u.UserId = @SignerUserId
                      AND  (u.IsDeveloper = 1 OR ISNULL(al.LevelRank, 0) >= 999)
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
                     CreatedDate, SubmissionSessionId, RequestedModels,
                     SubmittedByUserId)
                VALUES
                    (@EmployeeId, @DepartmentId, @Status,
                     @SignedBy, @SignedDate,
                     GETDATE(), @SubmissionSessionId, @RequestedModels,
                     NULLIF(@SignerUserId, 0));

                SET @AuthId = SCOPE_IDENTITY();

                -- Immediately unlock requests for IT processing if auto-approved.
                IF @Status = 'Approved'
                BEGIN
                    UPDATE dbo.Request
                    SET    Status = 'Under Review'
                    WHERE  SubmissionSessionId = @SubmissionSessionId
                      AND  Status = 'Awaiting Authorization';
                END

                SELECT @Status AS Status, @AuthId AS AuthId;";

            using (var con = new SqlConnection(ConnStr))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId",          employeeId);
                    cmd.Parameters.AddWithValue("@DepartmentId",        departmentId);
                    cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
                    cmd.Parameters.AddWithValue("@RequestedModels",     (object)requestedModels ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignerUserId",        signerUserId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bool wasApproved   = reader["Status"] is string s && s == "Approved";
                            int authorizationId = reader["AuthId"] != DBNull.Value ? Convert.ToInt32(reader["AuthId"]) : 0;
                            return (wasApproved, authorizationId);
                        }
                    }
                    return (false, 0);
                }
            }
        }

        /// <summary>
        /// Conditionally approves. Returns true if succeeded; false if already signed by someone else.
        /// </summary>
        public async Task<bool> ApproveAuthorizationAsync(int authorizationId, int supervisorUserId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                UPDATE dbo.CartridgeAuthorization
                SET    Status = 'Approved',
                       SignedBySupervisorId = @SupervisorId,
                       SignedDate = GETDATE()
                WHERE  AuthorizationId = @AuthorizationId
                  AND  Status = 'Pending';
                SELECT @@ROWCOUNT;";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    cmd.Parameters.AddWithValue("@SupervisorId",    supervisorUserId);
                    int rows = (int)await cmd.ExecuteScalarAsync();
                    return rows > 0;
                }
            }
        }

        public async Task RejectAuthorizationAsync(int authorizationId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                UPDATE dbo.CartridgeAuthorization
                SET    Status = 'Rejected'
                WHERE  AuthorizationId = @AuthorizationId AND Status = 'Pending';";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task MarkAuthorizationUsedAsync(int authorizationId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                UPDATE dbo.CartridgeAuthorization
                SET    Status = 'Used'
                WHERE  AuthorizationId = @AuthorizationId AND Status = 'Approved';";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task<CartridgeAuthorizationModel> QuerySingleAsync(
            string sql, Action<SqlCommand> paramSetup)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    paramSetup(cmd);
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync()) return MapRow(r);
                        return null;
                    }
                }
            }
        }

        private async Task<List<CartridgeAuthorizationModel>> QueryListAsync(
            string sql, Action<SqlCommand> paramSetup)
        {
            DatabaseConfig.EnsureConfigured();

            var list = new List<CartridgeAuthorizationModel>();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    paramSetup(cmd);
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync()) list.Add(MapRow(r));
                    }
                }
            }

            return list;
        }

        private async Task<CartridgeAuthorizationModel> QuerySingleExtendedAsync(
            string sql, Action<SqlCommand> paramSetup)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    paramSetup(cmd);
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync()) return MapExtendedRow(r);
                        return null;
                    }
                }
            }
        }

        private async Task<List<CartridgeAuthorizationModel>> QueryListExtendedAsync(
            string sql, Action<SqlCommand> paramSetup)
        {
            DatabaseConfig.EnsureConfigured();

            var list = new List<CartridgeAuthorizationModel>();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    paramSetup(cmd);
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync()) list.Add(MapExtendedRow(r));
                    }
                }
            }

            return list;
        }

        private static CartridgeAuthorizationModel MapRow(SqlDataReader r)
        {
            int Ord(string n) => r.GetOrdinal(n);
            string S(string n) => r.IsDBNull(Ord(n)) ? null : r.GetString(Ord(n));

            bool hasRequestedModels = HasColumn(r, "RequestedModels");

            return new CartridgeAuthorizationModel
            {
                AuthorizationId      = r.GetInt32(Ord("AuthorizationId")),
                EmployeeId           = r.GetInt32(Ord("EmployeeId")),
                DepartmentId         = r.GetInt32(Ord("DepartmentId")),
                Status               = r.GetString(Ord("Status")),
                SignedBySupervisorId = r.IsDBNull(Ord("SignedBySupervisorId")) ? (int?)null      : r.GetInt32(Ord("SignedBySupervisorId")),
                SignedDate           = r.IsDBNull(Ord("SignedDate"))           ? (DateTime?)null : r.GetDateTime(Ord("SignedDate")),
                CreatedDate          = r.GetDateTime(Ord("CreatedDate")),
                EmployeeName         = S("EmployeeName"),
                DepartmentName       = S("DepartmentName"),
                SignedByName         = S("SignedByName"),
                RequestedModels      = hasRequestedModels ? S("RequestedModels") : null
            };
        }

        private static bool HasColumn(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static CartridgeAuthorizationModel MapExtendedRow(SqlDataReader r)
        {
            int Ord(string n) => r.GetOrdinal(n);
            string S(string n) => r.IsDBNull(Ord(n)) ? null : r.GetString(Ord(n));
            bool Has(string n) => HasColumn(r, n);
            return new CartridgeAuthorizationModel
            {
                AuthorizationId      = r.GetInt32(Ord("AuthorizationId")),
                EmployeeId           = r.GetInt32(Ord("EmployeeId")),
                DepartmentId         = r.GetInt32(Ord("DepartmentId")),
                Status               = r.GetString(Ord("Status")),
                SignedBySupervisorId = r.IsDBNull(Ord("SignedBySupervisorId")) ? (int?)null      : r.GetInt32(Ord("SignedBySupervisorId")),
                SignedDate           = r.IsDBNull(Ord("SignedDate"))           ? (DateTime?)null : r.GetDateTime(Ord("SignedDate")),
                CreatedDate          = r.GetDateTime(Ord("CreatedDate")),
                RequestedModels      = S("RequestedModels"),
                EmployeeName         = S("EmployeeName"),
                EmployeePosition     = S("EmployeePosition"),
                DepartmentName       = S("DepartmentName"),
                BranchName           = S("BranchName"),
                CompanyName          = S("CompanyName"),
                SignedByName         = S("SignedByName"),
                FulfillmentMethod    = Has("FulfillmentMethod")  ? S("FulfillmentMethod")  : null,
                ReceivedByName       = Has("ReceivedByName")     ? S("ReceivedByName")     : null,
                SubmittedByUserId    = Has("SubmittedByUserId") && !r.IsDBNull(Ord("SubmittedByUserId")) ? (int?)r.GetInt32(Ord("SubmittedByUserId")) : null,
                SubmittedByName      = Has("SubmittedByName")    ? S("SubmittedByName")    : null,
                Remarks              = Has("Remarks")            ? S("Remarks")            : null
            };
        }
    }
}
