using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// (GetFulfilledSetsForNotification / GetActiveEmployeesForReceiver / UpdateReceivedByForSession /
    /// UpdateReceivedByForRequest) — straight SQL port, translated to async/Microsoft.Data.SqlClient.
    /// </summary>
    public class SendNotificationsRepository : ISendNotificationsRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public SendNotificationsRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        public async Task<List<FulfilledSetNotificationDto>> GetFulfilledSetsForNotificationAsync()
        {
            var list = new List<FulfilledSetNotificationDto>();

            const string sql = @"
                SELECT TOP 300
                    s.SetId,
                    ISNULL(s.SetCode, '') AS SetCode,
                    CASE
                        WHEN ISNULL(s.Status, '') = 'Dispatched' THEN 'Dispatched'
                        WHEN ISNULL(s.Status, '') = 'Partial'    THEN 'Partial'
                        WHEN (ISNULL(s.IssuedBrandNewQty, 0) + ISNULL(s.IssuedRefilledQty, 0)) > 0 THEN 'Partial'
                        ELSE 'Pending'
                    END AS SetStatus,
                    s.CreatedAt,
                    TOP_REQ.ReqId,
                    TOP_REQ.EmpId,
                    TOP_REQ.SubmissionSessionId,
                    TOP_REQ.ReceivedById,
                    ISNULL(recv_emp.Name, '') AS ReceivedByName,
                    ISNULL(req_emp.Name, '') AS RequesterName,
                    ISNULL(co.Name, '') AS CompanyName,
                    ISNULL(b.Name, '') AS BranchName,
                    ISNULL(d.Name, '') AS DepartmentName,
                    CASE
                        WHEN TOP_REQ.Description LIKE '%PICKUP%' THEN 'PICKUP'
                        WHEN TOP_REQ.Description LIKE '%DELIVERY%' THEN 'DELIVERY'
                        ELSE 'N/A'
                    END AS DistributionMethod,
                    ISNULL(s.IssuedBrandNewQty, 0) AS IssuedBrandNewQty,
                    ISNULL(s.IssuedRefilledQty, 0) AS IssuedRefilledQty
                FROM dbo.[Set] s
                CROSS APPLY (
                    SELECT TOP 1 r.ReqId, r.EmpId, r.SubmissionSessionId, r.ReceivedById, r.Description,
                                 r.ComId, r.BranchId, r.DeptId
                    FROM dbo.Request r
                    WHERE r.SetId = s.SetId
                    ORDER BY r.ReqId
                ) AS TOP_REQ
                LEFT JOIN dbo.Employee req_emp ON req_emp.EmpId = TOP_REQ.EmpId
                LEFT JOIN dbo.Company   co      ON co.ComId     = COALESCE(req_emp.ComId,    TOP_REQ.ComId)
                LEFT JOIN dbo.Branch    b       ON b.BranchId   = COALESCE(req_emp.BranchId, TOP_REQ.BranchId)
                LEFT JOIN dbo.Department d      ON d.DeptId     = COALESCE(req_emp.DeptId,   TOP_REQ.DeptId)
                LEFT JOIN dbo.Employee recv_emp ON recv_emp.EmpId = TOP_REQ.ReceivedById
                WHERE TOP_REQ.Description LIKE '%[[]PORTAL]%'
                  AND s.CreatedAt >= DATEADD(day, -90, GETDATE())
                ORDER BY s.CreatedAt DESC";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FulfilledSetNotificationDto
                {
                    SetId = reader.GetInt32(reader.GetOrdinal("SetId")),
                    SetCode = reader.GetString(reader.GetOrdinal("SetCode")),
                    SetStatus = reader.GetString(reader.GetOrdinal("SetStatus")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                    EmpId = reader.IsDBNull(reader.GetOrdinal("EmpId")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmpId")),
                    SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                    ReceivedById = reader.IsDBNull(reader.GetOrdinal("ReceivedById")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ReceivedById")),
                    ReceivedByName = reader.GetString(reader.GetOrdinal("ReceivedByName")),
                    RequesterName = reader.GetString(reader.GetOrdinal("RequesterName")),
                    CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    DistributionMethod = reader.GetString(reader.GetOrdinal("DistributionMethod")),
                    IssuedBrandNewQty = reader.GetInt32(reader.GetOrdinal("IssuedBrandNewQty")),
                    IssuedRefilledQty = reader.GetInt32(reader.GetOrdinal("IssuedRefilledQty"))
                });
            }

            return list;
        }

        public async Task<List<EmployeeOptionDto>> GetActiveEmployeesForReceiverAsync()
        {
            var list = new List<EmployeeOptionDto>();

            const string sql = @"
                SELECT e.EmpId, e.Name, ISNULL(e.EmployeeNumber, '') AS EmployeeNumber,
                       ISNULL(d.Name, '') AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeOptionDto
                {
                    EmpId = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    EmployeeNumber = reader.GetString(2),
                    DepartmentName = reader.GetString(3)
                });
            }

            return list;
        }

        public async Task UpdateReceivedByForSessionAsync(Guid submissionSessionId, int? receivedById, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET ReceivedById = @ReceivedById,
                    DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                    ModifiedBy    = @ModifiedBy
                WHERE SubmissionSessionId = @SessionId";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReceivedById", (object?)receivedById ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
            cmd.Parameters.AddWithValue("@SessionId", submissionSessionId);
            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateReceivedByForRequestAsync(int reqId, int? receivedById, int modifiedByUserId)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET ReceivedById = @ReceivedById,
                    DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                    ModifiedBy    = @ModifiedBy
                WHERE ReqId = @ReqId";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReceivedById", (object?)receivedById ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
            cmd.Parameters.AddWithValue("@ReqId", reqId);
            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
