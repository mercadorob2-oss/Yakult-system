namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// A fulfilled/partially-fulfilled/pending portal cartridge Set shown on the
    /// Send Notifications page.
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// (FulfilledSetNotificationDto) — same 90-day / [PORTAL] query desktop uses.
    /// </summary>
    public class FulfilledSetNotificationDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; } = string.Empty;
        public string SetStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ReqId { get; set; }
        public int EmpId { get; set; }
        public Guid? SubmissionSessionId { get; set; }
        public int? ReceivedById { get; set; }
        public string ReceivedByName { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DistributionMethod { get; set; } = string.Empty;
        public int IssuedBrandNewQty { get; set; }
        public int IssuedRefilledQty { get; set; }

        public bool IsPickup => string.Equals(DistributionMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);

        public string BranchDept
        {
            get
            {
                var b = BranchName ?? string.Empty;
                var d = DepartmentName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(b) && string.IsNullOrWhiteSpace(d)) return "—";
                if (string.IsNullOrWhiteSpace(d)) return b;
                if (string.IsNullOrWhiteSpace(b)) return d;
                return $"{b} / {d}";
            }
        }

        public string ReceivedByDisplay =>
            string.IsNullOrWhiteSpace(ReceivedByName) ? "— Not Set —" : ReceivedByName;

        public string DateDisplay
        {
            get
            {
                if (CreatedAt == DateTime.MinValue) return "—";
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var d = CreatedAt.Date;
                if (d == today) return $"Today  {CreatedAt:HH:mm}";
                if (d == yesterday) return $"Yesterday  {CreatedAt:HH:mm}";
                int daysAgo = (today - d).Days;
                return daysAgo <= 6 ? $"{daysAgo}d ago  {CreatedAt:HH:mm}" : CreatedAt.ToString("MM/dd/yyyy  HH:mm");
            }
        }

        public string FriendlyStatus => SetStatus switch
        {
            "Dispatched" => "Fulfilled",
            "Partial" => "Partially Fulfilled",
            "Pending" => "Unfulfilled",
            _ => string.IsNullOrWhiteSpace(SetStatus) ? "—" : SetStatus
        };

        public string StatusFg => SetStatus switch
        {
            "Dispatched" => "#1E9E5E",
            "Partial" => "#B45309",
            "Pending" => "#C0392B",
            _ => "#5A6A7E"
        };

        public string StatusBg => SetStatus switch
        {
            "Dispatched" => "#E3F9EE",
            "Partial" => "#FFF3CD",
            "Pending" => "#FDECEA",
            _ => "#F0F2F6"
        };
    }

    /// <summary>
    /// One entry in the "Received By" lookup ComboBox on the Send Notifications detail panel.
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs
    /// (GetActiveEmployeesForReceiver tuple).
    /// </summary>
    public class EmployeeOptionDto
    {
        public int EmpId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;

        public string Label
        {
            get
            {
                var empNum = !string.IsNullOrWhiteSpace(EmployeeNumber) ? $" ({EmployeeNumber})" : "";
                var deptPart = !string.IsNullOrWhiteSpace(DepartmentName) ? $" – {DepartmentName}" : "";
                return $"{Name}{empNum}{deptPart}";
            }
        }
    }
}
