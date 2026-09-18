using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class PfSessionGroupViewModel : ViewModelBase
    {
        private bool _isExpanded = true;

        public int?   SetId          { get; }
        public string RequesterName  { get; }
        public string BranchName     { get; }
        public string DepartmentName { get; }

        public ObservableCollection<PfExchangeRowViewModel> Rows { get; }

        public PfSessionGroupViewModel(
            int?   setId,
            string requesterName,
            string branchName,
            string departmentName,
            List<UnfulfilledCartridgeExchangeDto> rows)
        {
            SetId          = setId;
            RequesterName  = requesterName  ?? "Unknown";
            BranchName     = string.IsNullOrWhiteSpace(branchName)     ? null : branchName;
            DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName;
            Rows           = new ObservableCollection<PfExchangeRowViewModel>(
                                 (rows ?? new List<UnfulfilledCartridgeExchangeDto>())
                                 .Select(r => new PfExchangeRowViewModel(r)));

            ToggleExpandCommand = new RelayCommand(() =>
            {
                IsExpanded = !IsExpanded;
            });
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetField(ref _isExpanded, value))
                    OnPropertyChanged(nameof(ToggleIcon));
            }
        }

        public string ToggleIcon => IsExpanded ? "−" : "+";

        public int  TotalIssued  => Rows.Sum(r => r.IssuedFullQty);
        public int  TotalPending => Rows.Sum(r => r.UnfulfilledQty);
        public bool HasPending   => TotalPending > 0;
        public bool IsMultiModel => Rows.Count > 1;

        public string SetLabel => SetId.HasValue ? $"Set #{SetId}" : "Exchange";

        public string PendingBadgeText => $"Pending: {TotalPending}";

        public string HeaderSummary
        {
            get
            {
                string h = RequesterName;
                if (!string.IsNullOrWhiteSpace(BranchName))
                    h += $"  ·  {BranchName}";
                if (!string.IsNullOrWhiteSpace(DepartmentName))
                    h += $"  ·  {DepartmentName}";
                return h;
            }
        }

        // Blue matching the portal nav header
        public SolidColorBrush HeaderBrush
            => new SolidColorBrush(Color.FromRgb(58, 142, 246));

        public SolidColorBrush HeaderBrushDark
            => new SolidColorBrush(Color.FromRgb(44, 118, 220));

        public RelayCommand ToggleExpandCommand { get; }
    }
}
