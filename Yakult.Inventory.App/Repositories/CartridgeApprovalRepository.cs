using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Data-access layer for the cartridge-request supervisor approval workflow.
    /// All queries target the three new tables:
    ///   dbo.DepartmentSupervisor, dbo.CartridgeApproval, dbo.CartridgeApprovalSignature
    /// </summary>
    public class CartridgeApprovalRepository
    {
        private string ConnStr => DatabaseConfig.ConnectionString;

        // ── Approval Check / Create ──────────────────────────────────────────

        /// <summary>
        /// Called at employee login.  Returns the current approval state:
        ///   Status = "Approved" → employee may proceed
        ///   Status = "Pending"  → awaiting supervisor action
        ///   Status = "Created"  → new record inserted, app must send email
        ///   Status = "Rejected" → supervisor declined (show message)
        /// </summary>
        public async Task<CartridgeApprovalDto> CheckOrCreateAsync(int empId, string ipAddress = null)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("dbo.usp_CartridgeApproval_CheckOrCreate", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    cmd.Parameters.AddWithValue("@RequestorIpAddress", (object)ipAddress ?? DBNull.Value);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                            return MapApprovalRow(r);
                    }
                }
            }

            return null;
        }

        // ── Supervisor Queue ─────────────────────────────────────────────────

        /// <summary>
        /// Returns all pending (or all historical) approvals for a supervisor.
        /// </summary>
        public async Task<List<CartridgeApprovalDto>> GetForSupervisorAsync(
            int supervisorUserId, bool includeHistorical = false)
        {
            DatabaseConfig.EnsureConfigured();

            var list = new List<CartridgeApprovalDto>();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("dbo.usp_CartridgeApproval_GetForSupervisor", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SupervisorUserId", supervisorUserId);
                    cmd.Parameters.AddWithValue("@IncludeHistorical", includeHistorical ? 1 : 0);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                            list.Add(MapSupervisorRow(r));
                    }
                }
            }

            return list;
        }

        // ── Approve ──────────────────────────────────────────────────────────

        /// <summary>
        /// Saves the signature and marks the approval as Approved.
        /// Returns the updated approval record.
        /// </summary>
        public async Task<CartridgeApprovalDto> ApproveAsync(
            ApprovalSignatureInput input, string signaturePath)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("dbo.usp_CartridgeApproval_Approve", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ApprovalId",       input.ApprovalId);
                    cmd.Parameters.AddWithValue("@ApprovalToken",     input.ApprovalToken);
                    cmd.Parameters.AddWithValue("@SupervisorUserId",  input.SupervisorUserId);
                    cmd.Parameters.AddWithValue("@SignatureType",      input.SignatureType);
                    cmd.Parameters.AddWithValue("@SignaturePath",      (object)signaturePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SignatureData",      (object)input.SignatureData ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MimeType",           (object)input.MimeType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriginalFileName",   (object)input.OriginalFileName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FileSizeBytes",
                        input.SignatureData != null ? (object)input.SignatureData.Length : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReviewerIpAddress",  (object)input.ReviewerIpAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notes",              (object)input.Notes ?? DBNull.Value);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            var dto = new CartridgeApprovalDto
                            {
                                ApprovalId       = r.GetInt32(r.GetOrdinal("ApprovalId")),
                                Status           = r.GetString(r.GetOrdinal("Status")),
                                EmpId            = r.GetInt32(r.GetOrdinal("EmpId")),
                                SupervisorUserId = r.GetInt32(r.GetOrdinal("SupervisorUserId")),
                                ReviewedAt       = r.IsDBNull(r.GetOrdinal("ReviewedAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ReviewedAt")),
                                ExpiresAt        = r.IsDBNull(r.GetOrdinal("ExpiresAt"))  ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ExpiresAt")),
                                SignatureId      = r.IsDBNull(r.GetOrdinal("SignatureId")) ? (int?)null : r.GetInt32(r.GetOrdinal("SignatureId")),
                                SignaturePath    = r.IsDBNull(r.GetOrdinal("SignaturePath")) ? null : r.GetString(r.GetOrdinal("SignaturePath")),
                                SignedAt         = r.IsDBNull(r.GetOrdinal("SignedAt"))    ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("SignedAt"))
                            };
                            ActivityLogger.Log(input.SupervisorUserId, ActivityLogger.Actions.Approve,
                                "CartridgeApproval", input.ApprovalId,
                                $"Cartridge request approved (ApprovalId #{input.ApprovalId})");
                            return dto;
                        }
                    }
                }
            }

            return null;
        }

        // ── Reject ───────────────────────────────────────────────────────────

        /// <summary>
        /// Marks the approval as Rejected and records the reason.
        /// </summary>
        public async Task<CartridgeApprovalDto> RejectAsync(
            int approvalId, Guid token, int supervisorUserId,
            string notes, string ipAddress = null)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("dbo.usp_CartridgeApproval_Reject", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ApprovalId",        approvalId);
                    cmd.Parameters.AddWithValue("@ApprovalToken",     token);
                    cmd.Parameters.AddWithValue("@SupervisorUserId",  supervisorUserId);
                    cmd.Parameters.AddWithValue("@Notes",             (object)notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReviewerIpAddress", (object)ipAddress ?? DBNull.Value);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        if (await r.ReadAsync())
                        {
                            var dto = new CartridgeApprovalDto
                            {
                                ApprovalId = r.GetInt32(r.GetOrdinal("ApprovalId")),
                                Status     = r.GetString(r.GetOrdinal("Status")),
                                ReviewedAt = r.IsDBNull(r.GetOrdinal("ReviewedAt")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("ReviewedAt")),
                                Notes      = r.IsDBNull(r.GetOrdinal("Notes"))      ? null              : r.GetString(r.GetOrdinal("Notes"))
                            };
                            ActivityLogger.Log(supervisorUserId, ActivityLogger.Actions.Reject,
                                "CartridgeApproval", approvalId,
                                $"Cartridge request rejected (ApprovalId #{approvalId})");
                            return dto;
                        }
                    }
                }
            }

            return null;
        }

        // ── Email sent stamp ─────────────────────────────────────────────────

        public async Task MarkEmailSentAsync(int approvalId, int? emailLogId)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand("dbo.usp_CartridgeApproval_UpdateEmailSent", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                    cmd.Parameters.AddWithValue("@EmailLogId", (object)emailLogId ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ── Department Supervisor Admin ──────────────────────────────────────

        /// <summary>
        /// Lists all department-supervisor assignments (for admin UI).
        /// </summary>
        public async Task<List<DepartmentSupervisorDto>> GetDepartmentSupervisorsAsync(int? deptId = null)
        {
            DatabaseConfig.EnsureConfigured();

            var list = new List<DepartmentSupervisorDto>();

            const string sql = @"
                SELECT ds.DeptSupervisorId, ds.DeptId, dept.Name AS DepartmentName,
                       ds.SupervisorUserId, sup.Name AS SupervisorName,
                       sup.EmailAddress AS SupervisorEmail,
                       ds.IsActive, ds.DateAssigned,
                       ds.AssignedByUserId, ab.Name AS AssignedByName
                FROM   dbo.DepartmentSupervisor ds
                JOIN   dbo.Department dept ON ds.DeptId          = dept.DeptId
                JOIN   dbo.[User]     sup  ON ds.SupervisorUserId = sup.UserId
                LEFT JOIN dbo.[User]  ab   ON ds.AssignedByUserId = ab.UserId
                WHERE  (@DeptId IS NULL OR ds.DeptId = @DeptId)
                ORDER  BY dept.Name, sup.Name";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);

                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new DepartmentSupervisorDto
                            {
                                DeptSupervisorId = r.GetInt32(0),
                                DeptId           = r.GetInt32(1),
                                DepartmentName   = r.GetString(2),
                                SupervisorUserId = r.GetInt32(3),
                                SupervisorName   = r.GetString(4),
                                SupervisorEmail  = r.IsDBNull(5) ? null : r.GetString(5),
                                IsActive         = r.GetBoolean(6),
                                DateAssigned     = r.GetDateTime(7),
                                AssignedByUserId = r.IsDBNull(8)  ? (int?)null  : r.GetInt32(8),
                                AssignedByName   = r.IsDBNull(9)  ? null        : r.GetString(9)
                            });
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Assigns a supervisor to a department (upsert).
        /// </summary>
        public async Task AssignSupervisorAsync(
            int deptId, int supervisorUserId, int assignedByUserId)
        {
            DatabaseConfig.EnsureConfigured();

            const string sql = @"
                MERGE dbo.DepartmentSupervisor AS tgt
                USING (SELECT @DeptId AS DeptId, @SupervisorUserId AS SupervisorUserId) AS src
                ON (tgt.DeptId = src.DeptId AND tgt.SupervisorUserId = src.SupervisorUserId)
                WHEN MATCHED THEN
                    UPDATE SET IsActive = 1, DateAssigned = SYSUTCDATETIME(),
                               AssignedByUserId = @AssignedByUserId
                WHEN NOT MATCHED THEN
                    INSERT (DeptId, SupervisorUserId, IsActive, DateAssigned, AssignedByUserId)
                    VALUES (@DeptId, @SupervisorUserId, 1, SYSUTCDATETIME(), @AssignedByUserId);";

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@DeptId",           deptId);
                    cmd.Parameters.AddWithValue("@SupervisorUserId",  supervisorUserId);
                    cmd.Parameters.AddWithValue("@AssignedByUserId",  assignedByUserId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Removes a supervisor assignment (soft-delete).
        /// </summary>
        public async Task RemoveSupervisorAsync(int deptSupervisorId)
        {
            DatabaseConfig.EnsureConfigured();

            using (var con = new SqlConnection(ConnStr))
            {
                await con.OpenAsync();

                using (var cmd = new SqlCommand(
                    "UPDATE dbo.DepartmentSupervisor SET IsActive = 0 WHERE DeptSupervisorId = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", deptSupervisorId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ── Private mapper helpers ───────────────────────────────────────────

        private static CartridgeApprovalDto MapApprovalRow(SqlDataReader r)
        {
            int Ord(string n) => r.GetOrdinal(n);

            return new CartridgeApprovalDto
            {
                ApprovalId        = r.GetInt32(Ord("ApprovalId")),
                Status            = r.GetString(Ord("Status")),
                SupervisorUserId  = r.GetInt32(Ord("SupervisorUserId")),
                SupervisorEmpId   = r.IsDBNull(Ord("SupervisorEmpId"))  ? (int?)null : r.GetInt32(Ord("SupervisorEmpId")),
                SupervisorName    = r.IsDBNull(Ord("SupervisorName"))   ? null : r.GetString(Ord("SupervisorName")),
                SupervisorEmail   = r.IsDBNull(Ord("SupervisorEmail"))  ? null : r.GetString(Ord("SupervisorEmail")),
                SupervisorPosition = r.IsDBNull(Ord("SupervisorPosition")) ? null : r.GetString(Ord("SupervisorPosition")),
                ApprovalRole      = r.IsDBNull(Ord("ApprovalRole"))     ? null : r.GetString(Ord("ApprovalRole")),
                EmployeeName      = r.IsDBNull(Ord("EmployeeName"))     ? null : r.GetString(Ord("EmployeeName")),
                CompanyName       = r.IsDBNull(Ord("CompanyName"))      ? null : r.GetString(Ord("CompanyName")),
                BranchName        = r.IsDBNull(Ord("BranchName"))       ? null : r.GetString(Ord("BranchName")),
                DepartmentName    = r.IsDBNull(Ord("DepartmentName"))   ? null : r.GetString(Ord("DepartmentName")),
                EmailSentAt       = r.IsDBNull(Ord("EmailSentAt"))      ? (DateTime?)null : r.GetDateTime(Ord("EmailSentAt")),
                RequestedAt       = r.GetDateTime(Ord("RequestedAt")),
                ExpiresAt         = r.IsDBNull(Ord("ExpiresAt"))        ? (DateTime?)null : r.GetDateTime(Ord("ExpiresAt")),
                ApprovalToken     = r.IsDBNull(Ord("ApprovalToken"))
                                        ? Guid.Empty
                                        : r.GetGuid(Ord("ApprovalToken"))
            };
        }

        private static CartridgeApprovalDto MapSupervisorRow(SqlDataReader r)
        {
            int Ord(string n) => r.GetOrdinal(n);

            var dto = new CartridgeApprovalDto
            {
                ApprovalId            = r.GetInt32(Ord("ApprovalId")),
                EmpId                 = r.GetInt32(Ord("EmpId")),
                EmployeeName          = r.IsDBNull(Ord("EmployeeName"))      ? null : r.GetString(Ord("EmployeeName")),
                EmployeePosition      = r.IsDBNull(Ord("EmployeePosition"))  ? null : r.GetString(Ord("EmployeePosition")),
                EmployeeNumber        = r.IsDBNull(Ord("EmployeeNumber"))    ? null : r.GetString(Ord("EmployeeNumber")),
                EmployeeEmail         = r.IsDBNull(Ord("EmployeeEmail"))     ? null : r.GetString(Ord("EmployeeEmail")),
                ComId                 = r.IsDBNull(Ord("ComId"))             ? 0     : r.GetInt32(Ord("ComId")),
                CompanyName           = r.IsDBNull(Ord("CompanyName"))       ? null : r.GetString(Ord("CompanyName")),
                BranchId              = r.IsDBNull(Ord("BranchId"))          ? 0     : r.GetInt32(Ord("BranchId")),
                BranchName            = r.IsDBNull(Ord("BranchName"))        ? null : r.GetString(Ord("BranchName")),
                DeptId                = r.IsDBNull(Ord("DeptId")) ? 0 : r.GetInt32(Ord("DeptId")),
                DepartmentName        = r.IsDBNull(Ord("DepartmentName"))    ? null : r.GetString(Ord("DepartmentName")),
                SupervisorUserId      = r.GetInt32(Ord("SupervisorUserId")),
                SupervisorEmpId       = r.IsDBNull(Ord("SupervisorEmpId"))   ? (int?)null : r.GetInt32(Ord("SupervisorEmpId")),
                SupervisorName        = r.IsDBNull(Ord("SupervisorName"))    ? null : r.GetString(Ord("SupervisorName")),
                ApprovalRole          = r.IsDBNull(Ord("ApprovalRole"))      ? null : r.GetString(Ord("ApprovalRole")),
                SupervisorPosition    = r.IsDBNull(Ord("SupervisorPosition")) ? null : r.GetString(Ord("SupervisorPosition")),
                Status                = r.GetString(Ord("Status")),
                Notes                 = r.IsDBNull(Ord("Notes"))             ? null : r.GetString(Ord("Notes")),
                RequestedAt           = r.GetDateTime(Ord("RequestedAt")),
                ReviewedAt            = r.IsDBNull(Ord("ReviewedAt"))        ? (DateTime?)null : r.GetDateTime(Ord("ReviewedAt")),
                ExpiresAt             = r.IsDBNull(Ord("ExpiresAt"))         ? (DateTime?)null : r.GetDateTime(Ord("ExpiresAt")),
                EmailSentAt           = r.IsDBNull(Ord("EmailSentAt"))       ? (DateTime?)null : r.GetDateTime(Ord("EmailSentAt")),
                RequestorIpAddress    = r.IsDBNull(Ord("RequestorIpAddress")) ? null : r.GetString(Ord("RequestorIpAddress")),
                RecentCartridgeModels = r.IsDBNull(Ord("RecentCartridgeModels")) ? null : r.GetString(Ord("RecentCartridgeModels")),
                SignatureId           = r.IsDBNull(Ord("SignatureId"))        ? (int?)null : r.GetInt32(Ord("SignatureId")),
                SignatureType         = r.IsDBNull(Ord("SignatureType"))      ? null : r.GetString(Ord("SignatureType")),
                SignaturePath         = r.IsDBNull(Ord("SignaturePath"))      ? null : r.GetString(Ord("SignaturePath")),
                SignedAt              = r.IsDBNull(Ord("SignedAt"))           ? (DateTime?)null : r.GetDateTime(Ord("SignedAt"))
            };

            return dto;
        }
    }
}
