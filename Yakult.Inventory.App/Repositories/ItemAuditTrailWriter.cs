using System;
using System.Data.SqlClient;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    internal static class ItemAuditTrailWriter
    {
        internal static void TryLog(SqlConnection connection, SqlTransaction transaction, ItemAuditTrailDto audit)
        {
            TryLogWithResult(connection, transaction, audit, out _);
        }

        internal static bool TryLogWithResult(SqlConnection connection, SqlTransaction transaction, ItemAuditTrailDto audit, out Exception error)
        {
            error = null;

            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (audit == null)
                return true;

            try
            {
                const string sql = @"
INSERT INTO dbo.ItemAuditTrail (
    ItemId, SerialNumber, Action, ActionTime, EmployeeId, EmployeeName,
    DepartmentId, DepartmentName, BranchId, BranchName, Direction,
    Status, Location, ReferenceType, ReferenceId, SetCode, Notes, CreatedBy
) VALUES (
    @ItemId, @SerialNumber, @Action, @ActionTime, @EmployeeId, @EmployeeName,
    @DepartmentId, @DepartmentName, @BranchId, @BranchName, @Direction,
    @Status, @Location, @ReferenceType, @ReferenceId, @SetCode, @Notes, @CreatedBy
);";

                using (var cmd = new SqlCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@ItemId", (object)audit.ItemId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SerialNumber", (object)audit.SerialNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Action", (object)audit.Action ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActionTime", audit.ActionTime);
                    cmd.Parameters.AddWithValue("@EmployeeId", (object)audit.EmployeeId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EmployeeName", (object)audit.EmployeeName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DepartmentId", (object)audit.DepartmentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DepartmentName", (object)audit.DepartmentName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId", (object)audit.BranchId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchName", (object)audit.BranchName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Direction", (object)audit.Direction ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", (object)audit.Status ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Location", (object)audit.Location ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReferenceType", (object)audit.ReferenceType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReferenceId", (object)audit.ReferenceId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SetCode", (object)audit.SetCode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notes", (object)audit.Notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedBy", (object)audit.CreatedBy ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
    }
}
