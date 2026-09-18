using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    public class RenewalChainNodeViewModel : ViewModelBase
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string DocumentNumber { get; set; }
        public int RenewalNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsLast { get; set; }

        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set { SetField(ref _isCurrent, value); OnPropertyChanged(nameof(NodeBackground)); OnPropertyChanged(nameof(NodeForeground)); OnPropertyChanged(nameof(LabelForeground)); OnPropertyChanged(nameof(NodeBorderColor)); }
        }

        public string Label => RenewalNumber == 1 ? "ORIGINAL" : $"RENEWAL #{RenewalNumber - 1}";

        public string Period
        {
            get
            {
                if (StartDate.HasValue && EndDate.HasValue)
                    return $"{StartDate:MMM yy} – {EndDate:MMM yy}";
                if (StartDate.HasValue)
                    return $"From {StartDate:MMM yy}";
                return "No dates";
            }
        }

        public string NodeBackground  => IsCurrent ? "#3A8EF6" : "#FFFFFF";
        public string NodeForeground  => IsCurrent ? "#FFFFFF"  : "#1A2333";
        public string LabelForeground => IsCurrent ? "#AADDFF"  : "#5A6A7E";
        public string NodeBorderColor => IsCurrent ? "#3A8EF6"  : "#DDE3EC";
    }
}
