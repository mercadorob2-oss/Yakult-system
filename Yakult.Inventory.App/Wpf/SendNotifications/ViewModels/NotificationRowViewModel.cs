using System;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.SendNotifications.ViewModels
{
    public class NotificationRowViewModel : ViewModelBase
    {
        public FulfilledSetNotificationDto Dto { get; }

        public NotificationRowViewModel(FulfilledSetNotificationDto dto)
        {
            Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        }

        public int    SetId               => Dto.SetId;
        public string SetCode             => Dto.SetCode ?? string.Empty;
        public string RequesterName       => Dto.RequesterName ?? string.Empty;
        public string CompanyName         => Dto.CompanyName ?? string.Empty;
        public string BranchName          => Dto.BranchName ?? string.Empty;
        public string DepartmentName      => Dto.DepartmentName ?? string.Empty;
        public string DistributionMethod  => Dto.DistributionMethod ?? string.Empty;
        public string SetStatus           => Dto.SetStatus ?? string.Empty;
        public int    EmpId               => Dto.EmpId;
        public int    ReqId               => Dto.ReqId;
        public Guid?  SubmissionSessionId => Dto.SubmissionSessionId;
        public int?   ReceivedById        => Dto.ReceivedById;

        public string ReceivedByName => Dto.ReceivedByName ?? string.Empty;

        public string DateDisplay
        {
            get
            {
                if (Dto.CreatedAt == DateTime.MinValue) return "—";
                var today     = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var d         = Dto.CreatedAt.Date;
                if (d == today)     return $"Today  {Dto.CreatedAt:HH:mm}";
                if (d == yesterday) return $"Yesterday  {Dto.CreatedAt:HH:mm}";
                int daysAgo = (today - d).Days;
                return daysAgo <= 6
                    ? $"{daysAgo}d ago  {Dto.CreatedAt:HH:mm}"
                    : Dto.CreatedAt.ToString("MM/dd/yyyy  HH:mm");
            }
        }

        public string BranchDept
        {
            get
            {
                var b = Dto.BranchName ?? string.Empty;
                var d = Dto.DepartmentName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(b) && string.IsNullOrWhiteSpace(d)) return "—";
                if (string.IsNullOrWhiteSpace(d)) return b;
                if (string.IsNullOrWhiteSpace(b)) return d;
                return $"{b} / {d}";
            }
        }

        public string ReceivedByDisplay =>
            string.IsNullOrWhiteSpace(Dto.ReceivedByName) ? "— Not Set —" : Dto.ReceivedByName;

        public string FriendlyStatus
        {
            get
            {
                switch (Dto.SetStatus)
                {
                    case "Dispatched": return "Fulfilled";
                    case "Partial":    return "Partially Fulfilled";
                    case "Pending":    return "Unfulfilled";
                    default:           return Dto.SetStatus ?? "—";
                }
            }
        }

        public string StatusFg
        {
            get
            {
                switch (Dto.SetStatus)
                {
                    case "Dispatched": return "#1E9E5E";
                    case "Partial":    return "#B45309";
                    case "Pending":    return "#C0392B";
                    default:           return "#5A6A7E";
                }
            }
        }

        public string StatusBg
        {
            get
            {
                switch (Dto.SetStatus)
                {
                    case "Dispatched": return "#E3F9EE";
                    case "Partial":    return "#FFF3CD";
                    case "Pending":    return "#FDECEA";
                    default:           return "#F0F2F6";
                }
            }
        }

        public void UpdateReceivedBy(int empId, string name)
        {
            Dto.ReceivedById   = empId;
            Dto.ReceivedByName = name;
            OnPropertyChanged(nameof(ReceivedByName));
            OnPropertyChanged(nameof(ReceivedByDisplay));
        }
    }
}
