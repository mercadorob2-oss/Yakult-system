using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    public class TourRepository : ITourRepository
    {
        private readonly IConnectionStringProvider _connProvider;

        public TourRepository(IConnectionStringProvider connProvider)
        {
            _connProvider = connProvider;
        }

        public async Task<bool> HasCompletedAsync(int userId, string tourKey)
        {
            using var conn = new SqlConnection(_connProvider.GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT 1 FROM dbo.UserTourCompletion WHERE UserId = @UserId AND TourKey = @TourKey",
                conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TourKey", tourKey);
            return await cmd.ExecuteScalarAsync() != null;
        }

        public async Task MarkCompletedAsync(int userId, string tourKey)
        {
            using var conn = new SqlConnection(_connProvider.GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.UserTourCompletion
                    WHERE UserId = @UserId AND TourKey = @TourKey
                )
                INSERT INTO dbo.UserTourCompletion (UserId, TourKey, CompletedAt)
                VALUES (@UserId, @TourKey, GETDATE())",
                conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TourKey", tourKey);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
