using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.ViewModels
{
    /// <summary>One line in the change feed. Either a live diff between two poll snapshots
    /// (IsHistorical == false — carries OldQty/NewQty) or a historical dbo.Inventory ledger
    /// entry (IsHistorical == true — carries a signed Delta, an EntryType and a note).</summary>
    public class StockChangeRow
    {
        public DateTime Timestamp { get; set; }
        public string TimeText => Timestamp.ToString("HH:mm:ss");
        /// <summary>Time only for today's rows, date + time for older (7-day range) rows.</summary>
        public string WhenText =>
            Timestamp.Date == DateTime.Today ? Timestamp.ToString("HH:mm:ss") : Timestamp.ToString("MMM d  HH:mm");
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string Bucket { get; set; }
        public int OldQty { get; set; }
        public int NewQty { get; set; }
        public int Delta { get; set; }
        /// <summary>Live: "Changed", "New" (first time seen), or "Removed" (deactivated / unlinked).
        /// Historical: "Positive", "Negative", or "Fixed Assets" (the dbo.Inventory EntryType).</summary>
        public string Kind { get; set; }
        public bool IsHistorical { get; set; }
        /// <summary>Historical only — dbo.Inventory.Description / who posted it.</summary>
        public string Note { get; set; }
        public string PostedByName { get; set; }
        /// <summary>Historical only — an outstanding request line, not a posted movement.</summary>
        public bool IsPending { get; set; }

        public string ChangeText =>
            IsHistorical ? (string.IsNullOrWhiteSpace(Note) ? Kind : Note) :
            Kind == "New" ? $"— → {NewQty}" :
            Kind == "Removed" ? $"{OldQty} → —" :
            $"{OldQty} → {NewQty}";

        public string TypeText =>
            IsPending ? "pending" :
            IsHistorical ? (string.IsNullOrWhiteSpace(PostedByName) ? Kind : PostedByName) : Kind;
    }

    /// <summary>Roll-up chip for one bucket — live item count, total on-hand stock,
    /// and the time of the most recent change in that bucket.</summary>
    public class BucketSummary : ViewModelBase
    {
        public string Bucket { get; }

        private int _itemCount;
        public int ItemCount { get => _itemCount; set => SetField(ref _itemCount, value); }

        private int _totalStock;
        public int TotalStock { get => _totalStock; set => SetField(ref _totalStock, value); }

        private int _recentChangeCount;
        public int RecentChangeCount { get => _recentChangeCount; set => SetField(ref _recentChangeCount, value); }

        private string _lastChangeText = "no changes yet";
        public string LastChangeText { get => _lastChangeText; set => SetField(ref _lastChangeText, value); }

        public BucketSummary(string bucket) { Bucket = bucket; }
    }

    public class ConsumableStockMonitorViewModel : ViewModelBase
    {
        private const int FeedCap = 250;

        private readonly ConsumableStockMonitorRepository _repo = new ConsumableStockMonitorRepository();
        private readonly DispatcherTimer _timer;

        // itemId -> last known stock; itemId -> last known metadata (for "Removed" events)
        private readonly Dictionary<int, int> _snapshot = new Dictionary<int, int>();
        private readonly Dictionary<int, ConsumableStockItemDto> _meta = new Dictionary<int, ConsumableStockItemDto>();
        private bool _primed;

        private readonly List<StockChangeRow> _allEvents = new List<StockChangeRow>();
        private List<StockChangeRow> _historyEvents = new List<StockChangeRow>();

        public const string RangeLive       = "Live (this session)";
        public const string RangeToday      = "Today";
        public const string RangeYesterday  = "Yesterday";
        public const string RangeLast7Days  = "Last 7 days";

        public ObservableCollection<string> RangeOptions { get; } = new ObservableCollection<string>
        {
            RangeLive, RangeToday, RangeYesterday, RangeLast7Days
        };

        public ObservableCollection<StockChangeRow> Feed { get; } = new ObservableCollection<StockChangeRow>();
        public ObservableCollection<BucketSummary> Summaries { get; } = new ObservableCollection<BucketSummary>();

        public ObservableCollection<int> IntervalOptions { get; } = new ObservableCollection<int> { 5, 10, 30, 60 };

        public ObservableCollection<string> BucketFilterOptions { get; } = new ObservableCollection<string>
        {
            "All categories",
            ConsumableStockBuckets.Cartridge,
            ConsumableStockBuckets.Ink,
            ConsumableStockBuckets.Toner,
            ConsumableStockBuckets.Printhead,
            ConsumableStockBuckets.Other
        };

        public ConsumableStockMonitorViewModel()
        {
            foreach (var b in ConsumableStockBuckets.Monitored)
                Summaries.Add(new BucketSummary(b));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_selectedInterval) };
            _timer.Tick += async (s, e) => await PollAsync();

            TogglePauseCommand = new RelayCommand(() => IsPaused = !IsPaused);
            ClearFeedCommand   = new RelayCommand(ClearFeed);
            RefreshNowCommand  = new RelayCommand(async () => await RefreshNowAsync());
        }

        private async Task RefreshNowAsync()
        {
            if (IsHistoryMode) await LoadHistoryAsync();
            else await PollAsync();
        }

        public RelayCommand TogglePauseCommand { get; }
        public RelayCommand ClearFeedCommand { get; }
        public RelayCommand RefreshNowCommand { get; }

        // ── bindable state ──────────────────────────────────────────────────────────
        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (!SetField(ref _isPaused, value)) return;
                OnPropertyChanged(nameof(PauseButtonText));
                OnPropertyChanged(nameof(StatusText));
            }
        }
        public string PauseButtonText => IsPaused ? "▶  Resume" : "❚❚  Pause";

        private int _selectedInterval = 10;
        public int SelectedInterval
        {
            get => _selectedInterval;
            set
            {
                if (!SetField(ref _selectedInterval, value <= 0 ? 10 : value)) return;
                _timer.Interval = TimeSpan.FromSeconds(_selectedInterval);
            }
        }

        private string _selectedBucketFilter = "All categories";
        public string SelectedBucketFilter
        {
            get => _selectedBucketFilter;
            set { if (SetField(ref _selectedBucketFilter, value)) RebuildFeed(); }
        }

        public const string DirDeductions = "Deductions only";
        public const string DirAdditions  = "Additions only";
        public const string DirAll        = "All movements";

        public ObservableCollection<string> MovementDirectionOptions { get; } = new ObservableCollection<string>
        {
            DirDeductions, DirAdditions, DirAll
        };

        // Historical ranges default to outflows only — issues / deductions — so restocks and
        // empty-cartridge returns don't drown out actual consumption. Ignored in Live mode.
        private string _selectedMovementDirection = DirDeductions;
        public string SelectedMovementDirection
        {
            get => _selectedMovementDirection;
            set { if (SetField(ref _selectedMovementDirection, value)) RebuildFeed(); }
        }

        // Outstanding dbo.Request lines (not yet fulfilled) — off the ledger until dispatch,
        // so combo / assisted-request sets are invisible without this. On by default.
        private bool _includePendingRequests = true;
        public bool IncludePendingRequests
        {
            get => _includePendingRequests;
            set
            {
                if (!SetField(ref _includePendingRequests, value)) return;
                OnPropertyChanged(nameof(RangeCaption));
                RebuildFeed();
            }
        }

        private string _selectedRange = RangeLive;
        public string SelectedRange
        {
            get => _selectedRange;
            set
            {
                if (!SetField(ref _selectedRange, value)) return;
                OnPropertyChanged(nameof(IsHistoryMode));
                OnPropertyChanged(nameof(RangeCaption));
                if (IsHistoryMode) _ = LoadHistoryAsync();
                else { _historyEvents = new List<StockChangeRow>(); RebuildFeed(); }
            }
        }

        public bool IsHistoryMode => _selectedRange != RangeLive;

        public string RangeCaption
        {
            get
            {
                if (_selectedRange == RangeLive) return "Showing live changes detected this session";
                string when = _selectedRange == RangeToday ? "today"
                            : _selectedRange == RangeYesterday ? "yesterday"
                            : "the last 7 days";
                string src = _includePendingRequests
                    ? "dbo.Inventory + dbo.CartridgeMovement + pending dbo.Request"
                    : "dbo.Inventory + dbo.CartridgeMovement";
                return $"Showing stock movements from {when} ({src})";
            }
        }

        private bool _isLoadingHistory;
        public bool IsLoadingHistory { get => _isLoadingHistory; set => SetField(ref _isLoadingHistory, value); }

        private DateTime? _lastChecked;
        public string LastCheckedText =>
            _lastChecked == null ? "not checked yet" : "last checked " + _lastChecked.Value.ToString("HH:mm:ss");

        private bool _isLive;
        public bool IsLive { get => _isLive; set { if (SetField(ref _isLive, value)) OnPropertyChanged(nameof(StatusText)); } }

        public string StatusText =>
            !IsLive ? "starting…" : IsPaused ? "PAUSED" : "LIVE";

        private int _watchedItemCount;
        public int WatchedItemCount { get => _watchedItemCount; set => SetField(ref _watchedItemCount, value); }

        private int _totalWatchedStock;
        public int TotalWatchedStock { get => _totalWatchedStock; set => SetField(ref _totalWatchedStock, value); }

        public int FeedCount => Feed.Count;

        private string _errorText;
        public string ErrorText
        {
            get => _errorText;
            set { if (SetField(ref _errorText, value)) OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorText);

        // ── lifecycle ───────────────────────────────────────────────────────────────
        public async Task StartAsync()
        {
            await PollAsync();          // prime the baseline (no events emitted on first pass)
            IsLive = true;
            _timer.Start();
        }

        /// <summary>Restart polling after a Stop() (e.g. the view was unloaded on a tab
        /// switch and then shown again) without re-priming the baseline.</summary>
        public void Resume()
        {
            if (!_primed) return;
            IsLive = true;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            IsLive = false;
        }

        // ── polling / diffing ───────────────────────────────────────────────────────
        private bool _polling;

        private async Task PollAsync()
        {
            if (_polling) return;
            _polling = true;
            try
            {
                List<ConsumableStockItemDto> current = await _repo.GetItemStockSnapshotAsync();

                var currentIds = new HashSet<int>(current.Select(c => c.ItemId));
                var newRows = new List<StockChangeRow>();

                foreach (var it in current)
                {
                    if (_snapshot.TryGetValue(it.ItemId, out int prev))
                    {
                        if (prev != it.StockOnHand)
                            newRows.Add(MakeRow(it, prev, it.StockOnHand, "Changed"));
                    }
                    else if (_primed)
                    {
                        newRows.Add(MakeRow(it, 0, it.StockOnHand, "New"));
                    }

                    _snapshot[it.ItemId] = it.StockOnHand;
                    _meta[it.ItemId] = it;
                }

                foreach (int goneId in _snapshot.Keys.Where(k => !currentIds.Contains(k)).ToList())
                {
                    if (_primed && _meta.TryGetValue(goneId, out var gone))
                        newRows.Add(MakeRow(gone, _snapshot[goneId], 0, "Removed"));

                    _snapshot.Remove(goneId);
                    _meta.Remove(goneId);
                }

                // newest first within this batch
                foreach (var row in newRows.OrderByDescending(r => r.Timestamp).ThenByDescending(r => r.ItemId))
                    PushEvent(row);

                _primed = true;
                _lastChecked = DateTime.Now;
                ErrorText = null;
                OnPropertyChanged(nameof(LastCheckedText));
                OnPropertyChanged(nameof(HasError));

                RecomputeSummaries(current, newRows);
            }
            catch (Exception ex)
            {
                Logger.LogError("ConsumableStockMonitorViewModel.PollAsync failed", ex);
                ErrorText = "Could not refresh stock: " + ex.Message;
                OnPropertyChanged(nameof(HasError));
            }
            finally
            {
                _polling = false;
            }
        }

        private StockChangeRow MakeRow(ConsumableStockItemDto it, int oldQty, int newQty, string kind) => new StockChangeRow
        {
            Timestamp   = DateTime.Now,
            ItemId      = it.ItemId,
            ItemName    = it.ItemName,
            ModelNumber = it.ModelNumber,
            Bucket      = it.Bucket,
            OldQty      = oldQty,
            NewQty      = newQty,
            Delta       = newQty - oldQty,
            Kind        = kind
        };

        private void PushEvent(StockChangeRow row)
        {
            _allEvents.Insert(0, row);
            if (_allEvents.Count > FeedCap)
                _allEvents.RemoveRange(FeedCap, _allEvents.Count - FeedCap);

            // While a historical range is on screen, keep accumulating the live buffer in the
            // background but don't disturb the displayed feed.
            if (!IsHistoryMode && PassesFilter(row))
            {
                Feed.Insert(0, row);
                while (Feed.Count > FeedCap) Feed.RemoveAt(Feed.Count - 1);
                OnPropertyChanged(nameof(FeedCount));
            }
        }

        private bool PassesFilter(StockChangeRow row)
        {
            if (_selectedBucketFilter != "All categories" && row.Bucket != _selectedBucketFilter)
                return false;

            // Direction filter applies to historical rows only (Live diffs are shown as-is).
            if (row.IsHistorical)
            {
                if (row.IsPending && !_includePendingRequests) return false;
                if (_selectedMovementDirection == DirDeductions && row.Delta >= 0) return false;
                if (_selectedMovementDirection == DirAdditions  && row.Delta <= 0) return false;
            }

            return true;
        }

        private void RebuildFeed()
        {
            var source = IsHistoryMode ? _historyEvents : _allEvents;
            Feed.Clear();
            foreach (var row in source.Where(PassesFilter).Take(FeedCap))
                Feed.Add(row);
            OnPropertyChanged(nameof(FeedCount));
        }

        /// <summary>Loads the dbo.Inventory ledger rows for the selected historical range
        /// (Yesterday / Last 7 days) for consumable + cartridge items, and shows them in the feed.</summary>
        private async Task LoadHistoryAsync()
        {
            if (!IsHistoryMode) return;

            DateTime now = DateTime.Now;
            DateTime from, to;
            if (_selectedRange == RangeToday)
            {
                from = now.Date;
                to   = now;
            }
            else if (_selectedRange == RangeYesterday)
            {
                from = now.Date.AddDays(-1);
                to   = now.Date;
            }
            else // Last 7 days — rolling window ending now
            {
                from = now.AddDays(-7);
                to   = now;
            }

            IsLoadingHistory = true;
            try
            {
                // dbo.Inventory / dbo.CartridgeMovement store server-local timestamps — pass local bounds as-is.
                var rows = await _repo.GetHistoryAsync(from, to);
                _historyEvents = rows.Select(h => new StockChangeRow
                {
                    Timestamp    = h.PostedAtLocal,
                    ItemId       = h.ItemId,
                    ItemName     = h.ItemName,
                    ModelNumber  = h.ModelNumber,
                    Bucket       = h.Bucket,
                    Delta        = h.SignedQty,
                    Kind         = h.EntryType,
                    IsHistorical = true,
                    IsPending    = h.IsPending,
                    Note         = h.Description,
                    PostedByName = h.PostedByName
                }).ToList();

                ErrorText = null;
                RebuildFeed();
            }
            catch (Exception ex)
            {
                Logger.LogError("ConsumableStockMonitorViewModel.LoadHistoryAsync failed", ex);
                ErrorText = "Could not load history: " + ex.Message;
            }
            finally
            {
                IsLoadingHistory = false;
            }
        }

        private void ClearFeed()
        {
            _allEvents.Clear();
            Feed.Clear();
            foreach (var s in Summaries) { s.RecentChangeCount = 0; s.LastChangeText = "no changes yet"; }
            OnPropertyChanged(nameof(FeedCount));
        }

        private void RecomputeSummaries(List<ConsumableStockItemDto> current, List<StockChangeRow> batch)
        {
            WatchedItemCount   = current.Count;
            TotalWatchedStock  = current.Sum(c => c.StockOnHand);

            foreach (var summary in Summaries)
            {
                var forBucket = current.Where(c => c.Bucket == summary.Bucket).ToList();
                summary.ItemCount  = forBucket.Count;
                summary.TotalStock = forBucket.Sum(c => c.StockOnHand);

                var last = batch.Where(b => b.Bucket == summary.Bucket)
                                .OrderByDescending(b => b.Timestamp)
                                .FirstOrDefault();
                if (last != null)
                {
                    summary.RecentChangeCount += batch.Count(b => b.Bucket == summary.Bucket);
                    summary.LastChangeText = $"last change {last.TimeText} ({(last.Delta > 0 ? "+" + last.Delta : last.Delta.ToString())})";
                }
            }
        }
    }
}
