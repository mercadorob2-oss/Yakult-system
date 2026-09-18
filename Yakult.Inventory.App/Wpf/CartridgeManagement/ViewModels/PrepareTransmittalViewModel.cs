using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class PrepareTransmittalViewModel : ViewModelBase
    {
        // ── Read-only summary fields shown in the dialog header ───────────────────
        public string IssuedBy            { get; }
        public string RequesterName       { get; }
        public string DepartmentName      { get; }
        public string BranchName          { get; }
        public string CartridgeModelLabel { get; }

        // ── Company selection ─────────────────────────────────────────────────────
        private bool _isYakultPhilippines;
        public bool IsYakultPhilippines
        {
            get => _isYakultPhilippines;
            set => SetField(ref _isYakultPhilippines, value);
        }

        private bool _isYakultMarketing;
        public bool IsYakultMarketing
        {
            get => _isYakultMarketing;
            set => SetField(ref _isYakultMarketing, value);
        }

        // ── Noted By (IT employees, searchable) ───────────────────────────────────
        private readonly ObservableCollection<EmployeeSelectItem> _notedBySource;
        public  ICollectionView NotedByView { get; }

        private string _notedBySearch = "";
        public string NotedBySearch
        {
            get => _notedBySearch;
            set
            {
                if (SetField(ref _notedBySearch, value))
                    NotedByView.Refresh();
            }
        }

        private EmployeeSelectItem _selectedNotedBy;
        public EmployeeSelectItem SelectedNotedBy
        {
            get => _selectedNotedBy;
            set => SetField(ref _selectedNotedBy, value);
        }

        // ── Approved By (ApprovalRoleTitle employees, searchable) ─────────────────
        private readonly ObservableCollection<EmployeeSelectItem> _approvedBySource;
        public  ICollectionView ApprovedByView { get; }

        private string _approvedBySearch = "";
        public string ApprovedBySearch
        {
            get => _approvedBySearch;
            set
            {
                if (SetField(ref _approvedBySearch, value))
                    ApprovedByView.Refresh();
            }
        }

        private EmployeeSelectItem _selectedApprovedBy;
        public EmployeeSelectItem SelectedApprovedBy
        {
            get => _selectedApprovedBy;
            set => SetField(ref _selectedApprovedBy, value);
        }

        // ── Approval Date ─────────────────────────────────────────────────────────
        private DateTime? _approvalDate;
        public DateTime? ApprovalDate
        {
            get => _approvalDate;
            set => SetField(ref _approvalDate, value);
        }

        // ── Received By (all employees, searchable) ───────────────────────────────
        private readonly ObservableCollection<EmployeeSelectItem> _receivedBySource;
        public  ICollectionView ReceivedByView { get; }

        private string _receivedBySearch = "";
        public string ReceivedBySearch
        {
            get => _receivedBySearch;
            set
            {
                if (SetField(ref _receivedBySearch, value))
                    ReceivedByView.Refresh();
            }
        }

        private EmployeeSelectItem _selectedReceivedBy;
        public EmployeeSelectItem SelectedReceivedBy
        {
            get => _selectedReceivedBy;
            set => SetField(ref _selectedReceivedBy, value);
        }

        // ── Received Date ─────────────────────────────────────────────────────────
        private DateTime? _receivedDate;
        public DateTime? ReceivedDate
        {
            get => _receivedDate;
            set => SetField(ref _receivedDate, value);
        }

        // ── Free-text fields ──────────────────────────────────────────────────────
        private string _remarks = "";
        public string Remarks
        {
            get => _remarks;
            set => SetField(ref _remarks, value);
        }

        // ── Constructor ───────────────────────────────────────────────────────────
        public PrepareTransmittalViewModel(CartridgeTransmittalViewModel transmittal)
        {
            IssuedBy         = transmittal.IssuedBy;
            RequesterName    = transmittal.RequesterName;
            DepartmentName   = transmittal.DepartmentName;
            BranchName       = transmittal.BranchName;
            _isYakultPhilippines = transmittal.IsYakultPhilippines;
            _isYakultMarketing   = transmittal.IsYakultMarketing;

            CartridgeModelLabel = transmittal.IsOtherModel
                ? $"Cartridge Ribbon ({transmittal.OtherModelName})"
                : transmittal.IsLq2190 && transmittal.IsLx310
                    ? "Cartridge Ribbon (LQ2190 / LX310)"
                    : transmittal.IsLq2190
                        ? "Cartridge Ribbon (LQ2190)"
                        : transmittal.IsLx310
                            ? "Cartridge Ribbon (LX310)"
                            : "Cartridge Ribbon";

            // Pre-populate editable fields from the transmittal VM so navigating back
            // to re-edit (e.g. via the print preview's "Go Back" button) doesn't lose
            // whatever was already filled in.
            _remarks = transmittal.Remarks ?? "";
            _notedBySearch    = transmittal.NotedBy    ?? "";
            _approvedBySearch = transmittal.ApprovedBy ?? "";

            if (DateTime.TryParseExact(transmittal.ApprovalDate, "MM/dd/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var approvalDate))
                _approvalDate = approvalDate;

            if (DateTime.TryParseExact(transmittal.ReceivedDate, "MM/dd/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var receivedDate))
                _receivedDate = receivedDate;

            // Pre-populate received-by search text from existing value ("N/A" is a sentinel, treat as empty)
            string receivedByName = transmittal.ReceivedBy;
            _receivedBySearch = string.IsNullOrWhiteSpace(receivedByName) || receivedByName == "N/A"
                ? "" : receivedByName;

            // Load employees from the repository
            var repo = new CartridgeManagementRepository();
            var raw  = repo.GetActiveEmployeesForReceiver();

            var all = raw.Select(e => new EmployeeSelectItem
            {
                EmpId          = e.EmpId,
                Name           = e.Name,
                Position       = e.Position,
                DepartmentName = e.DepartmentName,
                BranchName     = e.BranchName
            }).ToList();

            // Noted By: all active employees (no department restriction)
            _notedBySource = new ObservableCollection<EmployeeSelectItem>(all);
            NotedByView    = CollectionViewSource.GetDefaultView(_notedBySource);
            NotedByView.Filter = o =>
            {
                if (o is EmployeeSelectItem e)
                    return string.IsNullOrWhiteSpace(_notedBySearch)
                        || e.Name.IndexOf(_notedBySearch, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };

            // Approved By: employees whose position matches dbo.ApprovalRoleTitle
            var approvers = repo.GetApproverEmployees().Select(e => new EmployeeSelectItem
            {
                EmpId          = e.EmpId,
                Name           = e.Name,
                Position       = e.Position,
                DepartmentName = e.DepartmentName,
                BranchName     = e.BranchName
            }).ToList();

            _approvedBySource = new ObservableCollection<EmployeeSelectItem>(approvers);
            ApprovedByView    = CollectionViewSource.GetDefaultView(_approvedBySource);
            ApprovedByView.Filter = o =>
            {
                if (o is EmployeeSelectItem e)
                    return string.IsNullOrWhiteSpace(_approvedBySearch)
                        || e.Name.IndexOf(_approvedBySearch, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };

            _receivedBySource = new ObservableCollection<EmployeeSelectItem>(all);
            ReceivedByView    = CollectionViewSource.GetDefaultView(_receivedBySource);
            ReceivedByView.Filter = o =>
            {
                if (o is EmployeeSelectItem e)
                    return string.IsNullOrWhiteSpace(_receivedBySearch)
                        || e.Name.IndexOf(_receivedBySearch, StringComparison.OrdinalIgnoreCase) >= 0;
                return false;
            };

            // Pre-select received by if we already have a real name from the request
            if (!string.IsNullOrWhiteSpace(_receivedBySearch))
            {
                _selectedReceivedBy = all.FirstOrDefault(e =>
                    e.Name.Equals(_receivedBySearch, StringComparison.OrdinalIgnoreCase));
            }

            // Pre-select Noted By / Approved By if re-entering the form with prior edits
            if (!string.IsNullOrWhiteSpace(_notedBySearch))
            {
                _selectedNotedBy = all.FirstOrDefault(e =>
                    e.Name.Equals(_notedBySearch, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_approvedBySearch))
            {
                _selectedApprovedBy = approvers.FirstOrDefault(e =>
                    e.Name.Equals(_approvedBySearch, StringComparison.OrdinalIgnoreCase));
            }
        }

        // ── Transfer edits back to the transmittal VM ─────────────────────────────
        public void ApplyTo(CartridgeTransmittalViewModel vm)
        {
            vm.IsYakultPhilippines = IsYakultPhilippines;
            vm.IsYakultMarketing   = IsYakultMarketing;
            vm.NotedBy      = SelectedNotedBy?.Name ?? "";
            vm.ApprovedBy   = SelectedApprovedBy?.Name
                              ?? (!string.IsNullOrWhiteSpace(_approvedBySearch)
                                  ? _approvedBySearch : "");
            vm.ApprovalDate = ApprovalDate?.ToString("MM/dd/yyyy") ?? "";
            vm.ReceivedBy   = SelectedReceivedBy?.Name
                              ?? (!string.IsNullOrWhiteSpace(_receivedBySearch)
                                  ? _receivedBySearch : vm.ReceivedBy);
            vm.ReceivedDate = ReceivedDate?.ToString("MM/dd/yyyy") ?? "";
            vm.Remarks      = Remarks ?? "";
        }
    }
}
