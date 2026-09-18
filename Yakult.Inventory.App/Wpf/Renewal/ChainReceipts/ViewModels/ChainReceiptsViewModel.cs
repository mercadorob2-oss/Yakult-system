using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.ChainReceipts.ViewModels
{
    /// <summary>One scanned Receipt Set (SI/DR/PO), shown under its owning chain group.</summary>
    public class ChainReceiptRowViewModel : ViewModelBase
    {
        public int ReceiptSetId { get; set; }
        public string Supplier { get; set; }
        public string SiNumber { get; set; }
        public string DrNumber { get; set; }
        public string PoNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByUsername { get; set; }
        public int SiCount { get; set; }
        public int DrCount { get; set; }
        public int PoCount { get; set; }

        /// <summary>Full DTO, passed to ReceiptSetViewerWindow when "View" is clicked.</summary>
        public ReceiptSetDto Dto { get; set; }

        public string SupplierDisplay => string.IsNullOrWhiteSpace(Supplier) ? "—" : Supplier;
        public string CreatedAtDisplay => CreatedAt.ToString("MMM d, yyyy");
        public string DocNumbersDisplay
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(SiNumber)) parts.Add($"SI: {SiNumber}");
                if (!string.IsNullOrWhiteSpace(DrNumber)) parts.Add($"DR: {DrNumber}");
                if (!string.IsNullOrWhiteSpace(PoNumber)) parts.Add($"PO: {PoNumber}");
                return parts.Count > 0 ? string.Join("   ", parts) : "—";
            }
        }
        public string ImageCountsDisplay => $"SI: {SiCount}   DR: {DrCount}   PO: {PoCount}";
    }

    /// <summary>One Set's position in the renewal chain (Original / Renewal #N), with every
    /// Receipt Set ever linked to that Set.</summary>
    public class ChainReceiptGroupViewModel : ViewModelBase
    {
        public int SetId { get; set; }
        public string SetCode { get; set; }
        public string PositionLabel { get; set; }
        public bool IsLatest { get; set; }
        public bool IsCurrent { get; set; }

        public ObservableCollection<ChainReceiptRowViewModel> Receipts { get; } = new ObservableCollection<ChainReceiptRowViewModel>();
        public bool HasReceipts => Receipts.Count > 0;

        public string BadgeColor => IsLatest ? "#1E9E5E" : "#5A6A7E";
    }

    public class ChainReceiptsViewModel : ViewModelBase
    {
        private readonly RenewalRepository _renewalRepository = new RenewalRepository();
        private readonly ReceiptSetRepository _receiptRepository = new ReceiptSetRepository();
        private readonly int _anySetId;

        public event Action<ReceiptSetDto> RequestViewReceipt;
        public event Action CloseRequested;
        public event Action<string> RequestError;

        public ObservableCollection<ChainReceiptGroupViewModel> Groups { get; } = new ObservableCollection<ChainReceiptGroupViewModel>();

        private bool _isLoading = true;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private bool _hasNoReceipts;
        public bool HasNoReceipts { get => _hasNoReceipts; set => SetField(ref _hasNoReceipts, value); }

        public ICommand CloseCommand { get; }
        public ICommand ViewReceiptCommand { get; }

        public ChainReceiptsViewModel(int anySetId)
        {
            _anySetId = anySetId;
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
            ViewReceiptCommand = new RelayCommand<ChainReceiptRowViewModel>(row =>
            {
                if (row?.Dto != null) RequestViewReceipt?.Invoke(row.Dto);
            });
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var chain = await Task.Run(() => _renewalRepository.GetRenewalChain(_anySetId));
                var setIds = chain.Select(c => c.SetId).ToList();
                var receipts = await Task.Run(() => _receiptRepository.GetReceiptSetsForSetIds(setIds));

                var userIds = receipts.Where(r => r.CreatedBy.HasValue).Select(r => r.CreatedBy.Value).Distinct().ToList();
                var userNames = userIds.Count > 0
                    ? await Task.Run(() => _receiptRepository.GetUserNamesByIds(userIds))
                    : new Dictionary<int, string>();

                var imageCounts = new Dictionary<int, Dictionary<string, int>>();
                foreach (var r in receipts)
                    imageCounts[r.ReceiptSetId] = await Task.Run(() => _receiptRepository.GetImageCountsForReceiptSet(r.ReceiptSetId));

                Groups.Clear();
                for (int i = 0; i < chain.Count; i++)
                {
                    var c = chain[i];
                    bool isLast = i == chain.Count - 1;
                    string label = chain.Count == 1 ? "Only Set" : (i == 0 ? "Original" : $"Renewal #{i}");

                    var group = new ChainReceiptGroupViewModel
                    {
                        SetId = c.SetId,
                        SetCode = c.SetCode,
                        PositionLabel = isLast && chain.Count > 1 ? $"{label} (Latest)" : label,
                        IsLatest = isLast,
                        IsCurrent = c.SetId == _anySetId
                    };

                    foreach (var r in receipts.Where(x => x.SetId == c.SetId))
                    {
                        var counts = imageCounts.TryGetValue(r.ReceiptSetId, out var ic) ? ic : new Dictionary<string, int>();
                        group.Receipts.Add(new ChainReceiptRowViewModel
                        {
                            ReceiptSetId = r.ReceiptSetId,
                            Supplier = r.Supplier,
                            SiNumber = r.SiNumber,
                            DrNumber = r.DrNumber,
                            PoNumber = r.PoNumber,
                            CreatedAt = r.CreatedAt,
                            CreatedByUsername = r.CreatedBy.HasValue && userNames.TryGetValue(r.CreatedBy.Value, out var name) ? name : "Unknown",
                            SiCount = counts.TryGetValue("SI", out var si) ? si : 0,
                            DrCount = counts.TryGetValue("DR", out var dr) ? dr : 0,
                            PoCount = counts.TryGetValue("PO", out var po) ? po : 0,
                            Dto = r
                        });
                    }

                    Groups.Add(group);
                }

                HasNoReceipts = Groups.All(g => g.Receipts.Count == 0);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke($"Failed to load chain receipts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
