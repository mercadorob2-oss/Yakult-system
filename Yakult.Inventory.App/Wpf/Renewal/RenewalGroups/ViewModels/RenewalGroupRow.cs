using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels
{
    /// <summary>
    /// One renewal chain (root set + every set that superseded it), ported from the
    /// internal RenewalGroupViewModel in the original ViewRenewalGroupPage.cs. Presentation-layer
    /// aggregate — computed from a group of RenewalDto rows, not a 1:1 DTO wrapper.
    /// </summary>
    public sealed class RenewalGroupRow : ViewModelBase
    {
        public int RootSetId { get; set; }
        public string RootSetCode { get; set; }
        public string CompanyName { get; set; }
        public string SetType { get; set; }
        public string OverallStatus { get; set; }
        public int? DaysUntilExpiry { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? DocumentDate { get; set; }
        public bool Active { get; set; }
        public List<RenewalDto> Chain { get; set; }
        public Dictionary<int, List<string>> ItemNamesBySetId { get; set; }

        /// <summary>Distinct Sub-Types (Contract/Subscription/License/Services) found across
        /// EVERY set in this chain — used by the Sub-Type filter. A chain aggregate, not a
        /// single Set's value, since a group can span several SetIds.</summary>
        public IReadOnlyList<string> SubTypes { get; set; } = Array.Empty<string>();

        /// <summary>True when at least one set in this chain has an item with no Sub-Type at
        /// all. Backs the "Non-Subtype" option in the Sub-Type filter.</summary>
        public bool HasNonSubTypeItems { get; set; } = true;

        /// <summary>Distinct Sub-Type Group Reference Codes (Contract Code/License ID/etc.,
        /// nullable) found across every set in this chain — searchable via SearchText.</summary>
        public IReadOnlyList<string> SubTypeReferenceCodes { get; set; } = Array.Empty<string>();

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set => SetField(ref _selected, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetField(ref _isExpanded, value))
                    OnPropertyChanged(nameof(ArrowText));
            }
        }

        public string ArrowText => IsExpanded ? "▾" : "▸";
        public string SetsCountDisplay => $"{Chain.Count} set{(Chain.Count != 1 ? "s" : "")}";
        public string CreatedAtDisplay => CreatedDate?.ToString("MM/dd/yyyy") ?? "—";

        /// <summary>Header shows the latest (linked) set's countdown; the frozen per-set countdown
        /// only appears inside the expanded chain rows — matches the original exactly.</summary>
        public string DaysDisplay
        {
            get
            {
                if (!DaysUntilExpiry.HasValue) return "";
                return DaysUntilExpiry.Value < 0
                    ? $"Expired {Math.Abs(DaysUntilExpiry.Value)}d ago"
                    : $"{DaysUntilExpiry.Value} days left";
            }
        }

        private ObservableCollection<RenewalChainRowVm> _chainRows;
        public ObservableCollection<RenewalChainRowVm> ChainRows
        {
            get
            {
                if (_chainRows == null) _chainRows = BuildChainRows();
                return _chainRows;
            }
        }

        /// <summary>SetIds within this chain whose child checkbox was manually unchecked —
        /// forces materialization of ChainRows so a never-expanded group (all children default
        /// checked) correctly reports no exclusions.</summary>
        public IEnumerable<int> GetExcludedSetIds() =>
            ChainRows.Where(r => !r.Selected).Select(r => r.Dto.SetId);

        private ObservableCollection<RenewalChainRowVm> BuildChainRows()
        {
            var displayChain = Chain.OrderByDescending(r => r.SetId).ToList();
            int latestId = Chain.Max(r => r.SetId);
            int rootId = Chain.Min(r => r.SetId);

            var rows = new ObservableCollection<RenewalChainRowVm>();
            foreach (var set in displayChain)
            {
                bool isLatest = set.SetId == latestId && Chain.Count > 1;
                bool isRoot = set.SetId == rootId && Chain.Count > 1;
                bool isSuperseded = set.SetId != latestId && Chain.Count > 1;

                DateTime rowRef = DateTime.Today;
                if (isSuperseded)
                {
                    var nextInChain = Chain.Where(r => r.SetId > set.SetId).OrderBy(r => r.SetId).FirstOrDefault();
                    if (nextInChain?.StartDate.HasValue == true)
                        rowRef = nextInChain.StartDate.Value.Date;
                }

                rows.Add(new RenewalChainRowVm(set, isLatest, isRoot, isSuperseded, rowRef));
            }
            return rows;
        }
    }

    /// <summary>One set within a renewal chain, as displayed in an expanded group card's rows.</summary>
    public sealed class RenewalChainRowVm
    {
        public RenewalDto Dto { get; }
        public bool IsLatest { get; }
        public bool IsRoot { get; }
        public bool IsSuperseded { get; }

        /// <summary>Checked by default — uncheck to exclude this specific set from the
        /// generated report even though its chain's root group is selected.</summary>
        public bool Selected { get; set; } = true;

        public RenewalChainRowVm(RenewalDto dto, bool isLatest, bool isRoot, bool isSuperseded, DateTime freezeReferenceDate)
        {
            Dto = dto;
            IsLatest = isLatest;
            IsRoot = isRoot;
            IsSuperseded = isSuperseded;

            int? ownDays = dto.EndDate.HasValue
                ? (int?)(int)(dto.EndDate.Value.Date - freezeReferenceDate).TotalDays
                : null;
            DaysValue = ownDays;
            DaysBold = ownDays.HasValue && ownDays.Value <= 30;
        }

        public bool HasBadge => IsLatest || IsRoot;
        public string SetCodeDisplay => HasBadge
            ? $"{Dto.SetCode ?? "—"} ({(IsLatest ? "Latest" : "Original")})"
            : (Dto.SetCode ?? "—");

        public string DocumentNumber => Dto.DocumentNumber ?? "—";
        public string StartDateDisplay => Dto.StartDate?.ToString("MM/dd/yyyy") ?? "—";
        public string EndDateDisplay => Dto.EndDate?.ToString("MM/dd/yyyy") ?? "—";

        public int? DaysValue { get; }
        public bool DaysBold { get; }
        public string DaysDisplay
        {
            get
            {
                if (!DaysValue.HasValue) return "—";
                string baseText = DaysValue.Value < 0 ? $"-{Math.Abs(DaysValue.Value)}d" : $"{DaysValue.Value}d";
                return IsSuperseded ? baseText + " ⏸" : baseText;
            }
        }

        public string StatusDisplay => Dto.SetLevelStatus ?? Dto.ExpiryStatus ?? "—";
        public string CreatedAtDisplay => Dto.CreatedDate?.ToString("MM/dd/yyyy") ?? "—";
    }
}
