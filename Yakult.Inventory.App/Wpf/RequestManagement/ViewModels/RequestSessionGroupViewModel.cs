using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    public class RequestSessionGroupViewModel : ViewModelBase
    {
        private bool _isExpanded = true;

        public int?   SetId          { get; }
        public string RequesterName  { get; }
        public string BranchName     { get; }
        public string DepartmentName { get; }

        public ObservableCollection<RequestFulfillmentRowViewModel> Rows { get; }

        public RequestSessionGroupViewModel(
            int?   setId,
            string requesterName,
            string branchName,
            string departmentName,
            List<RequestDto> rows)
        {
            SetId          = setId;
            RequesterName  = requesterName  ?? "Unknown";
            BranchName     = string.IsNullOrWhiteSpace(branchName)     ? null : branchName;
            DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName;
            Rows           = new ObservableCollection<RequestFulfillmentRowViewModel>(
                                 (rows ?? new List<RequestDto>())
                                 .Select(r => new RequestFulfillmentRowViewModel(r)));

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

        public int  TotalIssued  => Rows.Sum(r => r.IssuedQty);
        public int  TotalPending => Rows.Sum(r => r.PendingQty);
        public bool HasPending   => TotalPending > 0;

        public string SetLabel => SetId.HasValue ? $"Set #{SetId}" : "Request";

        public string PendingBadgeText => $"Pending: {TotalPending}";

        // Blue matching the portal nav header
        public SolidColorBrush HeaderBrush
            => new SolidColorBrush(Color.FromRgb(58, 142, 246));

        public RelayCommand ToggleExpandCommand { get; }
    }
}
