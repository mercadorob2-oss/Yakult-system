using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.Receipt
{
    public enum ReceiptStatusFilter
    {
        All,
        Complete,
        Pending
    }

    /// <summary>
    /// WPF row wrapper around ReceiptSetDto adding pre-computed display fields
    /// (status text, image counts, renewed text) so the view stays binding-only.
    /// </summary>
    public sealed class ReceiptSetRow
    {
        public ReceiptSetDto Dto { get; set; }

        public int ReceiptSetId => Dto.ReceiptSetId;
        public string SetCode => string.IsNullOrWhiteSpace(Dto.SetCode) ? "(unlinked)" : Dto.SetCode;
        public string Supplier => Dto.Supplier;
        public string SiNumber => Dto.SiNumber;
        public string DrNumber => Dto.DrNumber;
        public string PoNumber => Dto.PoNumber;
        public string CreatedAtText => Dto.CreatedAt.ToString("M/d/yyyy h:mm tt");

        public bool IsComplete { get; set; }
        public string StatusText => IsComplete ? "Complete" : "Pending";

        public string RenewedText =>
            (Dto.TotalItems.HasValue && Dto.TotalItems.Value > 0)
                ? $"{Dto.RenewedItems ?? 0}/{Dto.TotalItems.Value}"
                : "—";
    }

    public class ReceiptSetsViewModel : ViewModelBase
    {
        private const int PageSize = 50;

        private readonly ReceiptSetRepository _repository = new ReceiptSetRepository();

        private List<ReceiptSetRow> _all = new List<ReceiptSetRow>();
        private List<ReceiptSetRow> _filtered = new List<ReceiptSetRow>();

        private bool _isLoading;
        private string _searchText = string.Empty;
        private ReceiptStatusFilter _statusFilter = ReceiptStatusFilter.All;
        private int _totalCount;
        private int _completeCount;
        private int _pendingCount;

        public ObservableCollection<ReceiptSetRow> Rows { get; } = new ObservableCollection<ReceiptSetRow>();

        public ReceiptSetsViewModel()
        {
            TotalCardCommand = new RelayCommand(() => StatusFilter = ReceiptStatusFilter.All);
            CompleteCardCommand = new RelayCommand(() => StatusFilter = ReceiptStatusFilter.Complete);
            PendingCardCommand = new RelayCommand(() => StatusFilter = ReceiptStatusFilter.Pending);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
        }

        public ICommand ResetFiltersCommand { get; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _statusFilter = ReceiptStatusFilter.All;
            NotifyStatusCardsChanged();

            ApplyFilter();
        }

        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
        public int TotalCount { get => _totalCount; set => SetField(ref _totalCount, value); }
        public int CompleteCount { get => _completeCount; set => SetField(ref _completeCount, value); }
        public int PendingCount { get => _pendingCount; set => SetField(ref _pendingCount, value); }

        private bool _hasRows;
        public bool HasRows { get => _hasRows; set => SetField(ref _hasRows, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public ReceiptStatusFilter StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (!SetField(ref _statusFilter, value)) return;
                NotifyStatusCardsChanged();
                ApplyFilter();
            }
        }

        // ── Clickable summary cards (Total/Complete/Pending) ─────────────────
        public bool IsTotalCardActive => _statusFilter == ReceiptStatusFilter.All;
        public bool IsCompleteCardActive => _statusFilter == ReceiptStatusFilter.Complete;
        public bool IsPendingCardActive => _statusFilter == ReceiptStatusFilter.Pending;

        public ICommand TotalCardCommand { get; }
        public ICommand CompleteCardCommand { get; }
        public ICommand PendingCardCommand { get; }

        private void NotifyStatusCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsCompleteCardActive));
            OnPropertyChanged(nameof(IsPendingCardActive));
        }

        public ReceiptSetRow SelectedRow { get; set; }

        /// <summary>
        /// Mirrors the legacy WinForms ViewReceiptsPage rule: a receipt set that is linked to one
        /// or more Sets is locked for deletion until it's unlinked first (deleting it would orphan
        /// the link/coverage data).
        /// </summary>
        public bool IsReceiptLockedForDelete(ReceiptSetRow row, out string reason)
        {
            reason = string.Empty;
            if (row?.Dto == null)
            {
                reason = "No receipt set selected.";
                return true;
            }

            var linked = (row.Dto.SetCode ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(linked) && !string.Equals(linked, "(unlinked)", StringComparison.OrdinalIgnoreCase))
            {
                reason =
                    "This receipt set is linked to one or more sets and is locked for deletion.\n\n" +
                    "Linked set code(s): " + linked + "\n\n" +
                    "Open the receipt set, unlink it first, then try deleting again.";
                return true;
            }

            return false;
        }

        public void DeleteReceipt(ReceiptSetRow row)
        {
            if (row?.Dto == null)
                return;

            _repository.Delete(row.Dto.ReceiptSetId);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var dtos = await Task.Run(() => _repository.GetAllMetadataCurrentPerSet());

                _all = dtos.Select(dto => new ReceiptSetRow
                {
                    Dto = dto,
                    IsComplete = IsReceiptComplete(dto)
                }).ToList();

                TotalCount = _all.Count;
                CompleteCount = _all.Count(r => r.IsComplete);
                PendingCount = TotalCount - CompleteCount;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("ReceiptSetsViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static bool IsReceiptComplete(ReceiptSetDto dto)
        {
            if (dto == null)
                return false;

            bool HasAttachment(byte[] bytes, string path) => (bytes != null && bytes.Length > 0) || !string.IsNullOrWhiteSpace(path);

            return HasAttachment(dto.SiImage, dto.SiImagePath)
                && HasAttachment(dto.DrImage, dto.DrImagePath)
                && HasAttachment(dto.PoImage, dto.PoImagePath);
        }

        private void ApplyFilter()
        {
            IEnumerable<ReceiptSetRow> query = _all;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim();
                query = query.Where(r =>
                    Contains(r.SetCode, term) ||
                    Contains(r.Supplier, term) ||
                    Contains(r.SiNumber, term) ||
                    Contains(r.DrNumber, term) ||
                    Contains(r.PoNumber, term));
            }

            switch (StatusFilter)
            {
                case ReceiptStatusFilter.Complete:
                    query = query.Where(r => r.IsComplete);
                    break;
                case ReceiptStatusFilter.Pending:
                    query = query.Where(r => !r.IsComplete);
                    break;
            }

            _filtered = query.ToList();

            Rows.Clear();
            foreach (var row in _filtered)
                Rows.Add(row);

            HasRows = Rows.Count > 0;
        }

        private static bool Contains(string haystack, string term)
        {
            return !string.IsNullOrWhiteSpace(haystack) && haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
