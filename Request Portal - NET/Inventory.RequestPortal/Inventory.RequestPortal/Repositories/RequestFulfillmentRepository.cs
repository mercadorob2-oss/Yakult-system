using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// PORTED FROM: Yakult.Inventory.App/Repositories/RequestRepository.cs (lines 1406-1562)
    /// Straight port — no behavior changes. Bucketing happens at the Set level: a Set with a
    /// mix of issued/un-issued lines counts as Partially Fulfilled as a whole; a Set where
    /// nothing has been issued anywhere counts as Unfulfilled.
    /// </summary>
    public class RequestFulfillmentRepository : IRequestFulfillmentRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public RequestFulfillmentRepository(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        private const string FulfillmentTrackedRequestsCte = @"
            ;WITH Scoped AS (
                SELECT
                    r.ReqId,
                    r.SetId,
                    r.SubmissionSessionId,
                    r.DateCreated,
                    r.Quantity,
                    r.IssuedQty,
                    r.Remarks,
                    r.Status,
                    r.ItemId,
                    i.Name AS ItemName,
                    e.Name AS EmployeeName,
                    COALESCE(b.Name, rb.Name) AS BranchName,
                    COALESCE(d.Name, rd.Name) AS DepartmentName,
                    st.Status AS SetStatus,
                    ISNULL(CAST(r.SetId AS VARCHAR(20)), 'R' + CAST(r.ReqId AS VARCHAR(20))) AS GroupKey
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                LEFT  JOIN dbo.Employee   e  ON e.EmpId      = r.EmpId
                LEFT  JOIN dbo.Branch     b  ON b.BranchId   = e.BranchId
                LEFT  JOIN dbo.Department d  ON d.DeptId     = e.DeptId
                LEFT  JOIN dbo.Branch     rb ON rb.BranchId  = r.BranchId
                LEFT  JOIN dbo.Department rd ON rd.DeptId    = r.DeptId
                LEFT  JOIN dbo.[Set]      st ON st.SetId      = r.SetId
                WHERE r.Active = 1
                  AND (
                        REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
                     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
                     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
                      )
            ),
            GroupAgg AS (
                SELECT
                    GroupKey,
                    MAX(CASE WHEN IssuedQty > 0 THEN 1 ELSE 0 END) AS AnyIssued,
                    MIN(CASE WHEN IssuedQty >= Quantity OR SetStatus = 'Dispatched' THEN 1 ELSE 0 END) AS AllFulfilled
                FROM Scoped
                GROUP BY GroupKey
            )";

        private async Task<List<RequestDto>> ReadFulfillmentTrackedRequestsAsync(string bucketWhereClause)
        {
            var requests = new List<RequestDto>();

            string sql = FulfillmentTrackedRequestsCte + @"
                SELECT s.*
                FROM Scoped s
                INNER JOIN GroupAgg g ON g.GroupKey = s.GroupKey
                WHERE g.AllFulfilled = 0
                  AND " + bucketWhereClause + @"
                ORDER BY s.SetId, s.ReqId";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                requests.Add(new RequestDto
                {
                    ReqId = reader.GetInt32(reader.GetOrdinal("ReqId")),
                    SetId = reader.IsDBNull(reader.GetOrdinal("SetId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("SetId")),
                    SubmissionSessionId = reader.IsDBNull(reader.GetOrdinal("SubmissionSessionId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("SubmissionSessionId")),
                    DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                    Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                    IssuedQty = reader.GetInt32(reader.GetOrdinal("IssuedQty")),
                    Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                    ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                    EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
                    BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName"))
                });
            }

            return requests;
        }

        public Task<List<RequestDto>> GetUnfulfilledRequestsFullSetAsync()
            => ReadFulfillmentTrackedRequestsAsync("g.AnyIssued = 0");

        public Task<List<RequestDto>> GetPartiallyFulfilledRequestsFullSetAsync()
            => ReadFulfillmentTrackedRequestsAsync("g.AnyIssued = 1");

        public async Task FulfillRequestAsync(int reqId, int additionalIssuedQty, int modifiedByUserId, string? remarks)
        {
            const string sql = @"
                UPDATE dbo.Request
                SET IssuedQty     = CASE
                                        WHEN IssuedQty + @AdditionalIssuedQty > Quantity THEN Quantity
                                        ELSE IssuedQty + @AdditionalIssuedQty
                                     END,
                    Remarks       = ISNULL(@Remarks, Remarks),
                    DateModified  = (SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'),
                    ModifiedBy    = @ModifiedBy
                WHERE ReqId = @ReqId";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReqId", reqId);
            cmd.Parameters.AddWithValue("@AdditionalIssuedQty", additionalIssuedQty);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedByUserId);
            cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetItemStockOnHandAsync(int itemId)
        {
            const string sql = "SELECT ISNULL(StockOnHand, 0) FROM dbo.Item WHERE ItemId = @ItemId";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            await con.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }
    }
}
