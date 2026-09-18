using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.NotificationCenter.ViewModels
{
    public class NotificationCenterViewModel : ViewModelBase
    {
        // ── Collections ──────────────────────────────────────────────────────
        public ObservableCollection<ExpiryRowViewModel>       LicenseItems  { get; } = new ObservableCollection<ExpiryRowViewModel>();
        public ObservableCollection<ExpiryRowViewModel>       WarrantyItems { get; } = new ObservableCollection<ExpiryRowViewModel>();
        public ObservableCollection<MobileUpdateRowViewModel> MobileItems   { get; } = new ObservableCollection<MobileUpdateRowViewModel>();
        public ObservableCollection<ActivityRowViewModel>     ActivityItems { get; } = new ObservableCollection<ActivityRowViewModel>();

        // ── State ─────────────────────────────────────────────────────────────
        private bool _isLoading;
        public  bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

        private string _mobilePendingLabel = "Unprocessed: 0";
        public  string MobilePendingLabel  { get => _mobilePendingLabel; private set => SetField(ref _mobilePendingLabel, value); }

        private Brush _mobilePendingColor = Brushes.DarkGreen;
        public  Brush MobilePendingColor  { get => _mobilePendingColor; private set => SetField(ref _mobilePendingColor, value); }

        private int _unreadActivityCount;
        public  int UnreadActivityCount
        {
            get => _unreadActivityCount;
            private set { if (SetField(ref _unreadActivityCount, value)) OnPropertyChanged(nameof(HasUnreadActivity)); }
        }
        public bool HasUnreadActivity => _unreadActivityCount > 0;

        // Every Activity row from the last DB read; ActivityItems is this list filtered by date.
        private readonly List<ActivityRowViewModel> _allActivityRows = new List<ActivityRowViewModel>();

        public string[] ActivityFilters { get; } = { "Today", "Yesterday", "Last 7 days", "All" };

        private string _selectedActivityFilter = "Last 7 days";
        public string SelectedActivityFilter
        {
            get => _selectedActivityFilter;
            set { if (SetField(ref _selectedActivityFilter, value)) ApplyActivityFilter(); }
        }

        // ── "View All" navigation — invoked by the header link of each card ──
        public Action OnViewAllLicense  { get; set; }
        public Action OnViewAllWarranty { get; set; }
        public Action OnViewAllMobile   { get; set; }
        public Action OnClose           { get; set; }

        // ── Row-level navigation — invoked when the user clicks a single row ─
        // License: (searchQuery, destination) — destination is "Renewal" for sets, "Items" for standalone items.
        public Action<string, string> OnNavigateToLicenseItem  { get; set; }
        public Action<string>         OnNavigateToWarrantyItem { get; set; }
        // Mobile Updates page has no search API yet; string is SerialNumber for future use.
        public Action<string>         OnNavigateToMobileItem   { get; set; }
        // Activity row: (notificationType, referenceId, sourceLabel). MainForm routes to the
        // target entity — sourceLabel picks Set vs Invoice detail for SET_CREATED rows.
        public Action<string, int, string> OnNavigateToActivityItem { get; set; }
        // Raised after the Activity list / unread count changes so the host can refresh the bell badge.
        public Action                 OnActivityChanged        { get; set; }

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand ViewAllLicenseCommand  { get; }
        public ICommand ViewAllWarrantyCommand { get; }
        public ICommand ViewAllMobileCommand   { get; }
        public ICommand CloseCommand           { get; }
        public ICommand MarkAllActivityReadCommand { get; }

        public NotificationCenterViewModel()
        {
            ViewAllLicenseCommand  = new RelayCommand(() => OnViewAllLicense?.Invoke());
            ViewAllWarrantyCommand = new RelayCommand(() => OnViewAllWarranty?.Invoke());
            ViewAllMobileCommand   = new RelayCommand(() => OnViewAllMobile?.Invoke());
            CloseCommand           = new RelayCommand(() => OnClose?.Invoke());
            MarkAllActivityReadCommand = new RelayCommand(MarkAllActivityRead);
        }

        // ── Data loading ─────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                var licenseList  = new List<ExpiryRowViewModel>();
                var warrantyList = new List<ExpiryRowViewModel>();
                var mobileList   = new List<MobileUpdateRowViewModel>();
                var activityRows = new List<NotificationDto>();

                var repo = new Repositories.ExpiryNotificationRepository();

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    // ── License & Service Expiry ──────────────────────────────────────
                    // For sets the ItemName is the SetCode (passed to ViewRenewalPage.ApplyInitialSearch).
                    // For standalone items the ItemName is the Item.Name (passed to the same search).
                    // IsSet: false = standalone Item, true = Set.
                    // Drives the navigation destination in MainForm (Renewal vs Items).
                    foreach (var item in await repo.GetLicenseExpiryAsync(con))
                    {
                        licenseList.Add(new ExpiryRowViewModel
                        {
                            ItemName      = item.ItemName,
                            ItemType      = item.ItemType,
                            DaysRemaining = item.DaysRemaining,
                            IsSet         = item.IsSet,
                            NavigateAction = row => OnNavigateToLicenseItem?.Invoke(row.ItemName, row.Destination)
                        });
                    }

                    // ── Warranty Expiry ───────────────────────────────────────────────
                    foreach (var item in await repo.GetWarrantyExpiryAsync(con))
                    {
                        warrantyList.Add(new ExpiryRowViewModel
                        {
                            ItemName      = item.ItemName,
                            ItemType      = item.ItemType,
                            DaysRemaining = item.DaysRemaining,
                            NavigateAction = row => OnNavigateToWarrantyItem?.Invoke(row.ItemName)
                        });
                    }

                    // ── Unprocessed Mobile Updates ────────────────────────────────────
                    foreach (var item in await repo.GetUnprocessedMobileUpdatesAsync(con))
                    {
                        mobileList.Add(new MobileUpdateRowViewModel
                        {
                            SetCode      = item.SetCode,
                            SerialNumber = item.SerialNumber,
                            NewStatus    = item.NewStatus,
                            CreatedAt    = item.CreatedAt,
                            NavigateAction = row => OnNavigateToMobileItem?.Invoke(row.SerialNumber)
                        });
                    }
                }

                // ── Activity feed (persisted dbo.Notification rows, InventorySystem portal) ──
                // NotificationRepository is synchronous; run it off the captured UI context.
                if (AppSession.IsLoggedIn)
                {
                    activityRows = await Task.Run(() =>
                        new NotificationRepository().GetByUserAndPortal(
                            AppSession.CurrentUserId, NotificationType.InventorySystemKey, 50));
                }

                // Resume on the UI thread (WinForms SynchronizationContext captured before await).
                LicenseItems.Clear();
                foreach (var r in licenseList)  LicenseItems.Add(r);

                WarrantyItems.Clear();
                foreach (var r in warrantyList) WarrantyItems.Add(r);

                MobileItems.Clear();
                foreach (var r in mobileList)   MobileItems.Add(r);

                _allActivityRows.Clear();
                foreach (var n in activityRows) _allActivityRows.Add(ToActivityRow(n));
                ApplyActivityFilter();
                RecomputeUnreadActivity();
                OnActivityChanged?.Invoke();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Activity helpers ─────────────────────────────────────────────────
        private ActivityRowViewModel ToActivityRow(NotificationDto n)
        {
            var row = new ActivityRowViewModel
            {
                NotificationId   = n.NotificationId,
                Title            = n.Title,
                Message          = n.Message,
                NotificationType = n.NotificationType,
                ReferenceId      = n.ReferenceId,
                IsRead           = n.IsRead,
                CreatedDate      = n.CreatedDate,
                ClickAction      = OnActivityRowClicked,
            };

            ParseDetails(n.DetailsJson, out var source, out var children);

            row.SourceLabel = source;

            if (children != null)
            {
                foreach (var child in children)
                    child.NavigateAction = itemId =>
                    {
                        MarkRowRead(row);
                        OnNavigateToActivityItem?.Invoke(Models.NotificationType.ItemAdded, itemId, null);
                    };
                row.Children = children;
            }

            return row;
        }

        private static void ParseDetails(string detailsJson, out string source, out List<ActivityChildViewModel> children)
        {
            source = null;
            children = null;
            if (string.IsNullOrWhiteSpace(detailsJson)) return;

            try
            {
                List<InventoryActivityNotifier.ActivityItemDetail> items;

                // New shape: { "source": "...", "items": [...] }. Old shape (pre-release): a bare [...].
                if (detailsJson.TrimStart().StartsWith("["))
                {
                    items = JsonSerializer.Deserialize<List<InventoryActivityNotifier.ActivityItemDetail>>(detailsJson);
                }
                else
                {
                    var d = JsonSerializer.Deserialize<InventoryActivityNotifier.ActivityDetails>(detailsJson);
                    source = string.IsNullOrWhiteSpace(d?.source) ? null : d.source.Trim();
                    items  = d?.items;
                }

                if (items != null && items.Count > 0)
                {
                    children = items.Select(d => new ActivityChildViewModel
                    {
                        ItemId    = d.id,
                        // Serialized items share one name, so the serial is what tells them apart.
                        Name      = !string.IsNullOrWhiteSpace(d.serial) ? d.serial.Trim()
                                    : (string.IsNullOrWhiteSpace(d.name) ? $"Item #{d.id}" : d.name.Trim()),
                        TypeLabel = string.IsNullOrWhiteSpace(d.type) ? null : $"({d.type.Trim()})",
                        QtyLabel  = d.qty > 1 ? $"×{d.qty}" : null,
                    }).ToList();
                }
            }
            catch
            {
                // leave whatever parsed so far
            }
        }

        private void MarkRowRead(ActivityRowViewModel row)
        {
            if (row == null || row.IsRead) return;
            if (AppSession.IsLoggedIn)
            {
                try { new NotificationRepository().MarkAsRead(row.NotificationId, AppSession.CurrentUserId); }
                catch { /* best-effort */ }
            }
            row.IsRead = true;
            RecomputeUnreadActivity();
            OnActivityChanged?.Invoke();
        }

        private void OnActivityRowClicked(ActivityRowViewModel row)
        {
            if (row == null) return;

            MarkRowRead(row);

            // Navigation wins when there's a target (single ITEM_ADDED, any SET_CREATED). The ▸/▾
            // glyph has its own ToggleExpandCommand for expanding the item list. Rows with children
            // but no target (batch ITEM_ADDED) fall back to toggling on a header click.
            if (row.ReferenceId.HasValue)
                OnNavigateToActivityItem?.Invoke(row.NotificationType, row.ReferenceId.Value, row.SourceLabel);
            else if (row.HasChildren)
                row.IsExpanded = !row.IsExpanded;
        }

        private void MarkAllActivityRead()
        {
            if (!AppSession.IsLoggedIn) return;
            try
            {
                new NotificationRepository().MarkAllAsReadByPortal(
                    AppSession.CurrentUserId, NotificationType.InventorySystemKey);
            }
            catch { /* best-effort */ }

            foreach (var row in _allActivityRows) row.IsRead = true;
            RecomputeUnreadActivity();
            OnActivityChanged?.Invoke();
        }

        private void RecomputeUnreadActivity()
        {
            int count = 0;
            foreach (var r in _allActivityRows) if (!r.IsRead) count++;
            UnreadActivityCount = count;
        }

        // Date filter: "Today" / "Yesterday" / "Last 7 days" / "All". CreatedDate is UTC —
        // compare against the viewer's local calendar day.
        private void ApplyActivityFilter()
        {
            ActivityItems.Clear();
            foreach (var r in _allActivityRows)
                if (MatchesActivityFilter(r))
                    ActivityItems.Add(r);
        }

        private bool MatchesActivityFilter(ActivityRowViewModel r)
        {
            var day   = r.CreatedDate.ToLocalTime().Date;
            var today = DateTime.Now.Date;
            switch (_selectedActivityFilter)
            {
                case "Today":       return day == today;
                case "Yesterday":   return day == today.AddDays(-1);
                case "Last 7 days": return day >= today.AddDays(-6);
                default:            return true; // "All"
            }
        }

        // ── Called by the background connection poller without re-querying ────
        public void SetMobilePendingCount(int count)
        {
            MobilePendingLabel = $"Unprocessed: {count}";
            MobilePendingColor = count > 0 ? Brushes.DarkRed : Brushes.DarkGreen;
        }
    }
}
