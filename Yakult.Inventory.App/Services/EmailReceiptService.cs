using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Business logic for deployment receipt emails (NO SMTP logic)
    /// </summary>
    public class EmailReceiptService
    {
        private readonly ISmtpService _smtpService;
        private readonly EmailRepository _emailRepository;

        public EmailReceiptService(ISmtpService smtpService, EmailRepository emailRepository)
        {
            _smtpService = smtpService ?? throw new ArgumentNullException(nameof(smtpService));
            _emailRepository = emailRepository ?? throw new ArgumentNullException(nameof(emailRepository));
        }

        /// <summary>
        /// Sends deployment receipt email for a dispatched set
        /// </summary>
        public async Task SendDeploymentReceiptAsync(int setId, string setCode)
        {
            try
            {
                // Get deployment data
                var data = await GetDeploymentDataAsync(setId, setCode);
                if (data == null)
                {
                    Logger.LogWarning($"EmailReceiptService: No deployment data found for SetId={setId}");
                    return;
                }

                // Determine recipient
                var recipient = await DetermineRecipientAsync(data);
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    Logger.LogWarning($"EmailReceiptService: No recipient email found for SetId={setId}, EmpId={data.EmployeeId}, BranchId={data.BranchId}");
                    return;
                }

                // Build email content
                var subject = BuildEmailSubject(data);
                var body = BuildEmailBody(data);

                // Send email
                var success = await _smtpService.SendEmailAsync(recipient, subject, body, isHtml: false);

                if (success)
                {
                    Logger.LogInfo($"EmailReceiptService: Deployment receipt sent successfully for SetId={setId} to {recipient}");
                }
                else
                {
                    Logger.LogWarning($"EmailReceiptService: Failed to send deployment receipt for SetId={setId}");
                }
            }
            catch (Exception ex)
            {
                // Never throw - email failures must not block deployment
                Logger.LogError($"EmailReceiptService.SendDeploymentReceiptAsync failed for SetId={setId}", ex);
            }
        }

        /// <summary>
        /// Fetches deployment data from database
        /// </summary>
        private async Task<DeploymentData> GetDeploymentDataAsync(int setId, string setCode)
        {
            try
            {
                var setRepo = new SetRepository();

                // Get set info
                var set = await setRepo.GetSetByIdAsync(setId);
                if (set == null)
                {
                    return null;
                }

                // Get items in set
                var items = await setRepo.GetSetRequestsAsync(setId);

                // Get employee details from first request
                int? employeeId = null;
                int? branchId = null;
                string employeeName = null;
                string branchName = null;
                string departmentName = null;

                if (items != null && items.Count > 0)
                {
                    // items is ordered by DateCreated DESC for display, but every request in a batch
                    // shares the same timestamp, so items[0] is not stable. Pick the lowest ReqId
                    // (earliest request added to the Set) for a deterministic result — see the same
                    // fix in ViewSetDetailPage.xaml.cs (GetRepresentativeReqId).
                    var firstRequest = items.OrderBy(r => r.ReqId).First();
                    var employeeDetail = await setRepo.GetEmployeeDetailsForRequestAsync(firstRequest.ReqId);

                    if (employeeDetail != null)
                    {
                        employeeId = employeeDetail.EmpId;
                        employeeName = employeeDetail.EmployeeName;
                        branchName = employeeDetail.BranchName;
                        departmentName = employeeDetail.DepartmentName;

                        // Get branch ID directly from database
                        branchId = await GetEmployeeBranchIdAsync(employeeDetail.EmpId);
                    }
                }

                var data = new DeploymentData
                {
                    SetId = setId,
                    SetCode = setCode,
                    DispatchDate = set.DispatchDate ?? DateTime.Now,
                    DeployedBy = set.CreatedByName ?? "System",
                    Status = set.Status ?? "Dispatched",
                    DocumentNumber = set.DocumentNumber,
                    ReferenceNumber = set.ReferenceNumber,
                    EmployeeId = employeeId,
                    EmployeeName = employeeName ?? "N/A",
                    BranchId = branchId,
                    BranchName = branchName ?? "N/A",
                    DepartmentName = departmentName ?? "N/A",
                    Items = new List<DeploymentItem>()
                };

                // Add items
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        data.Items.Add(new DeploymentItem
                        {
                            SerialNumber = item.SerialNumber ?? "N/A",
                            ItemName = item.ItemName ?? "N/A",
                            ModelNumber = item.ModelNumber
                        });
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                Logger.LogError($"EmailReceiptService.GetDeploymentDataAsync failed for SetId={setId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves email recipient (employee primary email or branch email)
        /// </summary>
        private async Task<string> DetermineRecipientAsync(DeploymentData data)
        {
            try
            {
                // Try employee primary email first
                if (data.EmployeeId.HasValue)
                {
                    var employeeEmail = await _emailRepository.GetEmployeePrimaryEmailAsync(data.EmployeeId.Value);
                    if (!string.IsNullOrWhiteSpace(employeeEmail))
                    {
                        return employeeEmail;
                    }
                }

                // Fallback to branch email
                if (data.BranchId.HasValue)
                {
                    var branchEmail = await _emailRepository.GetBranchEmailAsync(data.BranchId.Value);
                    if (!string.IsNullOrWhiteSpace(branchEmail))
                    {
                        return branchEmail;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"EmailReceiptService.DetermineRecipientAsync failed for SetId={data.SetId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Constructs email subject
        /// </summary>
        private string BuildEmailSubject(DeploymentData data)
        {
            return $"Deployment Receipt - Set {data.SetCode}";
        }

        /// <summary>
        /// Gets employee's branch ID from database
        /// </summary>
        private async Task<int?> GetEmployeeBranchIdAsync(int empId)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    using (var cmd = new SqlCommand("SELECT BranchId FROM dbo.Employee WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);

                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"EmailReceiptService.GetEmployeeBranchIdAsync failed for EmpId={empId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Constructs plain text email body
        /// </summary>
        private string BuildEmailBody(DeploymentData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Deployment Receipt Confirmation");
            sb.AppendLine();
            sb.AppendLine($"Set Code: {data.SetCode}");
            sb.AppendLine($"Deployment Date: {data.DispatchDate:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Deployed By: {data.DeployedBy}");
            sb.AppendLine($"Status: {data.Status}");

            if (!string.IsNullOrWhiteSpace(data.DocumentNumber))
            {
                sb.AppendLine($"Document #: {data.DocumentNumber}");
            }

            if (!string.IsNullOrWhiteSpace(data.ReferenceNumber))
            {
                sb.AppendLine($"Reference #: {data.ReferenceNumber}");
            }

            sb.AppendLine($"Employee: {data.EmployeeName}");
            sb.AppendLine($"Branch: {data.BranchName}");
            sb.AppendLine($"Department: {data.DepartmentName}");
            sb.AppendLine();

            sb.AppendLine("Items in this Set:");
            if (data.Items != null && data.Items.Count > 0)
            {
                foreach (var item in data.Items)
                {
                    var itemDesc = $"- {item.SerialNumber} ({item.ItemName}";
                    if (!string.IsNullOrWhiteSpace(item.ModelNumber))
                    {
                        itemDesc += $" - {item.ModelNumber}";
                    }
                    itemDesc += ")";
                    sb.AppendLine(itemDesc);
                }

                sb.AppendLine();
                sb.AppendLine($"Total Items: {data.Items.Count}");
            }
            else
            {
                sb.AppendLine("- No items");
            }

            sb.AppendLine();
            sb.AppendLine("This is an automated notification from Yakult Inventory Management System.");
            sb.AppendLine("Please do not reply to this email.");

            return sb.ToString();
        }

        #region Internal Data Classes

        private class DeploymentData
        {
            public int SetId { get; set; }
            public string SetCode { get; set; }
            public DateTime DispatchDate { get; set; }
            public string DeployedBy { get; set; }
            public string Status { get; set; }
            public string DocumentNumber { get; set; }
            public string ReferenceNumber { get; set; }
            public int? EmployeeId { get; set; }
            public string EmployeeName { get; set; }
            public int? BranchId { get; set; }
            public string BranchName { get; set; }
            public string DepartmentName { get; set; }
            public List<DeploymentItem> Items { get; set; }
        }

        private class DeploymentItem
        {
            public string SerialNumber { get; set; }
            public string ItemName { get; set; }
            public string ModelNumber { get; set; }
        }

        #endregion
    }
}
