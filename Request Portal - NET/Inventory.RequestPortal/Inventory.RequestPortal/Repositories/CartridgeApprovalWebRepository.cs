using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Services;
using System.Data;

namespace Inventory.RequestPortal.Repositories
{
    public interface ICartridgeApprovalWebRepository
    {
        Task<List<PendingApprovalViewModel>> GetPendingForSupervisorAsync(int supervisorUserId);
        Task ApproveAsync(int approvalId, Guid token, int supervisorUserId,
            byte[] signatureData, string signatureType, string? mimeType,
            string? originalFileName, string? notes, string? ipAddress);
        Task RejectAsync(int approvalId, Guid token, int supervisorUserId,
            string? notes, string? ipAddress);
    }

    public class CartridgeApprovalWebRepository : ICartridgeApprovalWebRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public CartridgeApprovalWebRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        public async Task<List<PendingApprovalViewModel>> GetPendingForSupervisorAsync(int supervisorUserId)
        {
            var result = new List<PendingApprovalViewModel>();

            const string sql = @"
                SELECT
                    ca.ApprovalId,
                    ca.ApprovalToken,
                    emp.Name                        AS EmployeeName,
                    emp.Position                    AS EmployeePosition,
                    com.Name                        AS CompanyName,
                    br.Name                         AS BranchName,
                    ISNULL(dept.Name, N'(No Department)') AS DepartmentName,
                    ca.RequestedAt,
                    ca.[Status],
                    (
                        SELECT TOP 5
                            COALESCE(crm.CartridgeModel, cm.ModelNumber)
                                + ' x' + CAST(crm.RequestedQty AS VARCHAR)
                        FROM   dbo.Request r
                        JOIN   dbo.CartridgeRequestModel crm ON r.ReqId = crm.ReqId
                        LEFT   JOIN dbo.CartridgeModel   cm  ON cm.ModelNumber = crm.CartridgeModel
                        WHERE  r.EmpId = ca.EmpId
                          AND  r.DateRequested >= DATEADD(DAY, -90, SYSUTCDATETIME())
                        ORDER  BY r.DateRequested DESC
                        FOR XML PATH(''), TYPE
                    ).value('.', 'NVARCHAR(500)')
                                        AS RecentCartridgeModels,
                    COALESCE(
                        (SELECT TOP 1 ea.EmailAddress
                         FROM   dbo.EmployeeEmail ee
                         JOIN   dbo.EmailAddress  ea ON ee.EmailId = ea.EmailId
                         WHERE  ee.EmpId = ca.EmpId AND ee.IsActive = 1 AND ea.IsActive = 1
                         ORDER  BY ee.IsPrimary DESC),
                        (SELECT TOP 1 u2.EmailAddress
                         FROM   dbo.[User] u2
                         WHERE  u2.EmpId = ca.EmpId AND u2.IsActive = 1)
                    )                   AS EmployeeEmail
                FROM       dbo.CartridgeApproval ca
                JOIN       dbo.Employee   emp  ON ca.EmpId     = emp.EmpId
                JOIN       dbo.Company    com  ON ca.ComId     = com.ComId
                JOIN       dbo.Branch     br   ON ca.BranchId  = br.BranchId
                LEFT JOIN  dbo.Department dept ON ca.DeptId    = dept.DeptId
                WHERE ca.SupervisorUserId = @SupervisorUserId
                  AND ca.[Status] = 'Pending'
                ORDER BY ca.RequestedAt ASC;";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@SupervisorUserId", supervisorUserId);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                result.Add(new PendingApprovalViewModel
                {
                    ApprovalId          = r.GetInt32(r.GetOrdinal("ApprovalId")),
                    ApprovalToken       = r.GetGuid(r.GetOrdinal("ApprovalToken")),
                    EmployeeName        = r.IsDBNull(r.GetOrdinal("EmployeeName"))    ? "" : r.GetString(r.GetOrdinal("EmployeeName")),
                    EmployeePosition    = r.IsDBNull(r.GetOrdinal("EmployeePosition"))? "" : r.GetString(r.GetOrdinal("EmployeePosition")),
                    CompanyName         = r.IsDBNull(r.GetOrdinal("CompanyName"))     ? "" : r.GetString(r.GetOrdinal("CompanyName")),
                    BranchName          = r.IsDBNull(r.GetOrdinal("BranchName"))      ? "" : r.GetString(r.GetOrdinal("BranchName")),
                    DepartmentName      = r.IsDBNull(r.GetOrdinal("DepartmentName"))  ? "" : r.GetString(r.GetOrdinal("DepartmentName")),
                    RequestedAt         = r.GetDateTime(r.GetOrdinal("RequestedAt")),
                    Status              = r.IsDBNull(r.GetOrdinal("Status"))          ? "" : r.GetString(r.GetOrdinal("Status")),
                    RecentCartridgeModels = r.IsDBNull(r.GetOrdinal("RecentCartridgeModels")) ? null : r.GetString(r.GetOrdinal("RecentCartridgeModels")),
                    EmployeeEmail       = r.IsDBNull(r.GetOrdinal("EmployeeEmail"))   ? null : r.GetString(r.GetOrdinal("EmployeeEmail")),
                });
            }

            return result;
        }

        public async Task ApproveAsync(int approvalId, Guid token, int supervisorUserId,
            byte[] signatureData, string signatureType, string? mimeType,
            string? originalFileName, string? notes, string? ipAddress)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CartridgeApproval_Approve", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ApprovalId",       approvalId);
            cmd.Parameters.AddWithValue("@ApprovalToken",    token);
            cmd.Parameters.AddWithValue("@SupervisorUserId", supervisorUserId);
            cmd.Parameters.AddWithValue("@SignatureType",    signatureType);
            cmd.Parameters.Add("@SignatureData", SqlDbType.VarBinary, -1).Value = (object)signatureData ?? DBNull.Value;
            cmd.Parameters.AddWithValue("@MimeType",         (object?)mimeType         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OriginalFileName", (object?)originalFileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FileSizeBytes",    (object?)signatureData?.Length ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReviewerIpAddress",(object?)ipAddress        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes",            (object?)notes            ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RejectAsync(int approvalId, Guid token, int supervisorUserId,
            string? notes, string? ipAddress)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CartridgeApproval_Reject", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@ApprovalId",       approvalId);
            cmd.Parameters.AddWithValue("@ApprovalToken",    token);
            cmd.Parameters.AddWithValue("@SupervisorUserId", supervisorUserId);
            cmd.Parameters.AddWithValue("@Notes",            (object?)notes    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReviewerIpAddress",(object?)ipAddress ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
