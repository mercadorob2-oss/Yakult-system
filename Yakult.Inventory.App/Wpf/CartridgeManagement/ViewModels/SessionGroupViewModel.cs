using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    /// <summary>
    /// Wraps a RequestSessionGroup for WPF display.
    /// Supports differential updates — call UpdateFrom() on refresh instead of recreating.
    /// Color coding: Blue=Multi, Peach=Single, Green=All Fulfilled (same as WinForms).
    /// </summary>
    public class SessionGroupViewModel : ViewModelBase
    {
        private int     _groupIndex;
        private int     _modelCount;
        private int     _totalQuantity;
        private bool    _allFulfilled;

        // ── Identity (immutable after construction) ──────────────────────────────
        public string SessionKey   { get; }
        public bool   IsMultiModel { get; }
        public int    EmpId        { get; }
        public string EmployeeName { get; }
        public string CompanyName  { get; }
        public string BranchName   { get; }
        public string DepartmentName { get; }
        public string BranchDept   { get; }
        public DateTime DateCreated { get; }

        // ── Mutable (updated on differential refresh) ────────────────────────────
        public int GroupIndex
        {
            get => _groupIndex;
            set
            {
                if (SetField(ref _groupIndex, value))
                {
                    OnPropertyChanged(nameof(IsOldestGroup));
                    OnPropertyChanged(nameof(DateColor));
                }
            }
        }

        public int ModelCount
        {
            get => _modelCount;
            set { if (SetField(ref _modelCount, value)) OnPropertyChanged(nameof(HeaderText)); }
        }

        public int TotalQuantity
        {
            get => _totalQuantity;
            set => SetField(ref _totalQuantity, value);
        }

        public bool AllFulfilled
        {
            get => _allFulfilled;
            set
            {
                if (SetField(ref _allFulfilled, value))
                {
                    OnPropertyChanged(nameof(HeaderBrush));
                    OnPropertyChanged(nameof(HeaderBrushDark));
                }
            }
        }

        // ── Computed display ─────────────────────────────────────────────────────
        public bool IsOldestGroup => _groupIndex == 0;

        public string HeaderText
        {
            get
            {
                string prefix = IsMultiModel ? $"Multi Model Session ({ModelCount} models)" : "Single Model";
                string who    = BuildWhoLabel();
                return $"{prefix} — {who}";
            }
        }

        private string BuildWhoLabel()
        {
            // Dept-level request: no employee — show org info only
            if (EmpId == 0)
            {
                var parts = new[] { CompanyName, BranchName, DepartmentName }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                string org = string.Join(" → ", parts);
                return string.IsNullOrWhiteSpace(org) ? "Dept Level Request" : org;
            }

            // Normal employee request
            string empPart = string.IsNullOrWhiteSpace(EmployeeName) ? "Unknown" : EmployeeName;
            var orgParts = new[] { CompanyName, BranchName, DepartmentName }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            string orgLabel = string.Join(" → ", orgParts);
            return string.IsNullOrWhiteSpace(orgLabel) ? empPart : $"{empPart} — {orgLabel}";
        }

        public SolidColorBrush HeaderBrush
        {
            get
            {
                if (_allFulfilled) return new SolidColorBrush(Color.FromRgb(46, 204, 113));   // Green
                return IsMultiModel
                    ? new SolidColorBrush(Color.FromRgb(52,  152, 219))   // Blue
                    : new SolidColorBrush(Color.FromRgb(255, 218, 185));  // Peach
            }
        }

        // Darker shade for hover/border accent
        public SolidColorBrush HeaderBrushDark
        {
            get
            {
                if (_allFulfilled) return new SolidColorBrush(Color.FromRgb(39, 174, 96));
                return IsMultiModel
                    ? new SolidColorBrush(Color.FromRgb(41,  128, 185))
                    : new SolidColorBrush(Color.FromRgb(230, 190, 160));
            }
        }

        // Foreground for the header label (white for blue/green, dark for peach)
        public SolidColorBrush HeaderForeground
            => IsMultiModel || _allFulfilled
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(60, 50, 30));

        // Date column tint: oldest group gets green, others blue
        public string DateColor => IsOldestGroup ? "#27AE60" : "#3498DB";

        // ── Request rows (bound to the embedded DataGrid) ─────────────────────────
        public ObservableCollection<CartridgeRequestDto> Requests { get; }
            = new ObservableCollection<CartridgeRequestDto>();

        // ── Construction ─────────────────────────────────────────────────────────
        public SessionGroupViewModel(RequestSessionGroup group)
        {
            SessionKey     = group.SessionKey;
            GroupIndex     = group.GroupIndex;
            IsMultiModel   = group.IsMultiModel;
            EmpId          = group.EmpId;
            EmployeeName   = group.EmployeeName ?? "";
            CompanyName    = group.CompanyName  ?? group.Requests?.FirstOrDefault()?.CompanyName ?? "";
            BranchName     = group.BranchName;
            DepartmentName = group.DepartmentName;
            BranchDept     = group.BranchDept ?? "Unknown Location";
            DateCreated    = group.DateCreated;
            ModelCount     = group.ModelCount;
            TotalQuantity  = group.TotalQuantity;

            SyncRequests(group.Requests ?? new List<CartridgeRequestDto>());
            RefreshFulfilledState();
        }

        // ── Differential update ────────────────────────────────────────────────
        /// <summary>
        /// Updates mutable state from a freshly-loaded group without rebuilding the VM.
        /// Preserves scroll position and selection on the owning list.
        /// </summary>
        public void UpdateFrom(RequestSessionGroup updated)
        {
            GroupIndex    = updated.GroupIndex;
            ModelCount    = updated.ModelCount;
            TotalQuantity = updated.TotalQuantity;

            SyncRequests(updated.Requests ?? new List<CartridgeRequestDto>());
            RefreshFulfilledState();
        }

        // ─────────────────────────────────────────────────────────────────────────
        private void SyncRequests(IList<CartridgeRequestDto> incoming)
        {
            var incomingIds = incoming.Select(r => r.ReqId).ToHashSet();

            // Remove rows no longer present
            for (int i = Requests.Count - 1; i >= 0; i--)
                if (!incomingIds.Contains(Requests[i].ReqId))
                    Requests.RemoveAt(i);

            // Update or insert
            for (int i = 0; i < incoming.Count; i++)
            {
                var inRow = incoming[i];
                var existing = Requests.FirstOrDefault(r => r.ReqId == inRow.ReqId);
                if (existing != null)
                {
                    // Shallow-copy mutable fields (Status is the one that changes)
                    existing.Status    = inRow.Status;
                    existing.ModelNumber = inRow.ModelNumber;
                    existing.TypedModelNumber = inRow.TypedModelNumber;
                    existing.Quantity  = inRow.Quantity;
                    existing.GoodEmptyQty     = inRow.GoodEmptyQty;
                    existing.DamagedEmptyQty  = inRow.DamagedEmptyQty;
                    existing.DateCreated = inRow.DateCreated;
                }
                else
                {
                    Requests.Insert(Math.Min(i, Requests.Count), inRow);
                }
            }
        }

        private void RefreshFulfilledState()
        {
            AllFulfilled = Requests.Any() && Requests.All(r =>
                !string.IsNullOrWhiteSpace(r.Status) &&
                (r.Status.Equals("Submitted",  StringComparison.OrdinalIgnoreCase) ||
                 r.Status.Equals("Completed",  StringComparison.OrdinalIgnoreCase)));
        }
    }
}
