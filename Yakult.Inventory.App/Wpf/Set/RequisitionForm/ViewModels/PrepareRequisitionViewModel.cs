using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels
{
    public class EmployeeOption
    {
        public int    EmpId          { get; }
        public string Name           { get; }
        public string Position       { get; }
        public string DepartmentName { get; }

        public EmployeeOption(int empId, string name, string position, string departmentName)
        {
            EmpId          = empId;
            Name           = name;
            Position       = position;
            DepartmentName = departmentName;
        }

        public override string ToString()
            => string.IsNullOrWhiteSpace(Position) ? Name : $"{Name} ({Position})";
    }

    public class PrepareRequisitionViewModel : ViewModelBase
    {
        // ── Read-only summary fields shown in the dialog header ───────────────────
        public string Department  { get; }
        public string DateDisplay { get; }

        // ── Prepared By (searchable, restricted to Information Technology staff) ──
        private readonly ObservableCollection<EmployeeOption> _preparedBySource;
        public  ICollectionView PreparedByView { get; }

        private string _preparedBySearch = "";
        public string PreparedBySearch
        {
            get => _preparedBySearch;
            set { if (SetField(ref _preparedBySearch, value)) PreparedByView.Refresh(); }
        }

        private EmployeeOption _selectedPreparedBy;
        public EmployeeOption SelectedPreparedBy
        {
            get => _selectedPreparedBy;
            set => SetField(ref _selectedPreparedBy, value);
        }

        // ── Noted By (searchable) ──────────────────────────────────────────────────
        private readonly ObservableCollection<EmployeeOption> _notedBySource;
        public  ICollectionView NotedByView { get; }

        private string _notedBySearch = "";
        public string NotedBySearch
        {
            get => _notedBySearch;
            set { if (SetField(ref _notedBySearch, value)) NotedByView.Refresh(); }
        }

        private EmployeeOption _selectedNotedBy;
        public EmployeeOption SelectedNotedBy
        {
            get => _selectedNotedBy;
            set => SetField(ref _selectedNotedBy, value);
        }

        // ── Approved By (searchable) ───────────────────────────────────────────────
        private readonly ObservableCollection<EmployeeOption> _approvedBySource;
        public  ICollectionView ApprovedByView { get; }

        private string _approvedBySearch = "";
        public string ApprovedBySearch
        {
            get => _approvedBySearch;
            set { if (SetField(ref _approvedBySearch, value)) ApprovedByView.Refresh(); }
        }

        private EmployeeOption _selectedApprovedBy;
        public EmployeeOption SelectedApprovedBy
        {
            get => _selectedApprovedBy;
            set => SetField(ref _selectedApprovedBy, value);
        }

        // ── Received By (searchable) ───────────────────────────────────────────────
        private readonly ObservableCollection<EmployeeOption> _receivedBySource;
        public  ICollectionView ReceivedByView { get; }

        private string _receivedBySearch = "";
        public string ReceivedBySearch
        {
            get => _receivedBySearch;
            set { if (SetField(ref _receivedBySearch, value)) ReceivedByView.Refresh(); }
        }

        private EmployeeOption _selectedReceivedBy;
        public EmployeeOption SelectedReceivedBy
        {
            get => _selectedReceivedBy;
            set => SetField(ref _selectedReceivedBy, value);
        }

        public PrepareRequisitionViewModel(RequisitionFormViewModel requisition)
        {
            Department       = requisition.Department;
            DateDisplay      = requisition.Date;

            var repo = new SetRepository();
            var all = repo.GetActiveEmployeesForReceiver()
                .Select(e => new EmployeeOption(e.EmpId, e.Name, e.Position, e.DepartmentName))
                .ToList();
            var itEmployees = all.Where(e => IsInformationTechnologyDept(e.DepartmentName)).ToList();

            // Each combo gets its own ObservableCollection wrapping the same data so
            // CollectionViewSource.GetDefaultView (keyed by source collection identity)
            // returns four independent, independently-filterable views.
            _preparedBySource = new ObservableCollection<EmployeeOption>(itEmployees);
            PreparedByView    = CollectionViewSource.GetDefaultView(_preparedBySource);
            PreparedByView.Filter = o => Matches(o, _preparedBySearch);

            _notedBySource = new ObservableCollection<EmployeeOption>(all);
            NotedByView    = CollectionViewSource.GetDefaultView(_notedBySource);
            NotedByView.Filter = o => Matches(o, _notedBySearch);

            _approvedBySource = new ObservableCollection<EmployeeOption>(all);
            ApprovedByView    = CollectionViewSource.GetDefaultView(_approvedBySource);
            ApprovedByView.Filter = o => Matches(o, _approvedBySearch);

            _receivedBySource = new ObservableCollection<EmployeeOption>(all);
            ReceivedByView    = CollectionViewSource.GetDefaultView(_receivedBySource);
            ReceivedByView.Filter = o => Matches(o, _receivedBySearch);

            _selectedPreparedBy = FindByName(itEmployees, requisition.PreparedByName);
            _selectedNotedBy    = FindByName(all, requisition.NotedByName);
            _selectedApprovedBy = FindByName(all, requisition.ApprovedByName);

            // Noted By, Approved By, and Received By are no longer preset to a fixed
            // default person — different people can fill these roles, so they start
            // blank unless the requisition already carries a saved value.
        }

        // Matches "Information Technology", "IT", "IT Department", "I.T.", etc.
        private static bool IsInformationTechnologyDept(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName)) return false;
            string n = departmentName.Trim();
            return n.Equals("IT", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("IT", StringComparison.OrdinalIgnoreCase)
                || n.IndexOf("Information Technology", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Case/punctuation-insensitive so "RODOLFO ANG JR." matches "Rodolfo Ang, Jr." etc.
        private static string NormalizeName(string s)
            => Regex.Replace((s ?? "").ToUpperInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

        private static EmployeeOption FindByName(List<EmployeeOption> options, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string target = NormalizeName(name);
            return options.FirstOrDefault(e => NormalizeName(e.Name) == target);
        }

        // Per-word prefix match — NOT a raw substring-anywhere match. A plain
        // Contains()/IndexOf() check would match "lester" against "Ballesteros"
        // (...bal-LESTER-os), pulling unrelated employees into the filtered list.
        private static readonly char[] NameWordSeparators = { ' ', ',', '.', '-' };

        private static bool Matches(object o, string search)
        {
            if (!(o is EmployeeOption e)) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;

            var words = e.Name.Split(NameWordSeparators, StringSplitOptions.RemoveEmptyEntries);
            return words.Any(w => w.StartsWith(search, StringComparison.OrdinalIgnoreCase));
        }

        // ── Transfer edits back to the requisition VM ─────────────────────────────
        // Signatory names print in ALL CAPS regardless of how they're cased in
        // dbo.Employee (camel case, etc.) — positions are left as-is.
        public void ApplyTo(RequisitionFormViewModel vm)
        {
            vm.PreparedByName     = ToUpper(SelectedPreparedBy?.Name
                                    ?? (!string.IsNullOrWhiteSpace(_preparedBySearch) ? _preparedBySearch : vm.PreparedByName ?? ""));
            vm.PreparedByPosition = SelectedPreparedBy?.Position ?? "";
            vm.NotedByName        = ToUpper(SelectedNotedBy?.Name
                                    ?? (!string.IsNullOrWhiteSpace(_notedBySearch) ? _notedBySearch : ""));
            vm.NotedByPosition    = SelectedNotedBy?.Position ?? "";
            vm.ApprovedByName     = ToUpper(SelectedApprovedBy?.Name
                                    ?? (!string.IsNullOrWhiteSpace(_approvedBySearch) ? _approvedBySearch : ""));
            vm.ApprovedByPosition = SelectedApprovedBy?.Position ?? "";
            vm.ReceivedByLabel    = ToUpper(SelectedReceivedBy?.Name
                                    ?? (!string.IsNullOrWhiteSpace(_receivedBySearch) ? _receivedBySearch : ""));
            vm.ReceivedByPosition = SelectedReceivedBy?.Position ?? "";
        }

        private static string ToUpper(string s) => (s ?? "").ToUpperInvariant();
    }
}
