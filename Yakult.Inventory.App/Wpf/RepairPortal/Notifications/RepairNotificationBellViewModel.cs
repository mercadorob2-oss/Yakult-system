using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Notifications
{
    /// <summary>
    /// Backs the Repair Technician Portal's header notification bell. Reads/writes the shared
    /// dbo.Notification table through <see cref="NotificationRepository"/> (the same table the
    /// Request Portal web app uses). Rows are produced by RepairPortalNotificationPoller; this
    /// view-model only displays them and handles mark-read / mark-all-read / clear-all.
    /// </summary>
    public sealed class RepairNotificationBellViewModel : ViewModelBase
    {
        private const int FetchLimit = 50;
        private readonly NotificationRepository _repo = new NotificationRepository();

        /// <summary>dbo.Notification is shared with the Request Portal and other consumers. The
        /// bell only ever reads / marks-read / clears rows owned by this portal (PortalId FK), so
        /// notifications from other portals never appear here and "Clear all" never touches them.</summary>
        private const string PortalKey = NotificationType.RepairPortalKey;

        public ObservableCollection<RepairNotificationItemViewModel> Items { get; }
            = new ObservableCollection<RepairNotificationItemViewModel>();

        /// <summary>Raised when the user clicks a notification — the shell opens that repair
        /// ticket's detail window. Argument is the ticket's RepairTicketId (Notification.ReferenceId).</summary>
        public event Action<int> RequestOpenTicket;

        public RelayCommand ToggleDropdownCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand MarkAllReadCommand { get; }
        public RelayCommand ClearAllCommand { get; }
        public RelayCommand<RepairNotificationItemViewModel> OpenNotificationCommand { get; }

        public RepairNotificationBellViewModel()
        {
            ToggleDropdownCommand   = new RelayCommand(() => IsDropdownOpen = !IsDropdownOpen);
            RefreshCommand          = new RelayCommand(async () => await LoadAsync());
            MarkAllReadCommand      = new RelayCommand(async () => await MarkAllReadAsync());
            ClearAllCommand         = new RelayCommand(async () => await ClearAllAsync());
            OpenNotificationCommand = new RelayCommand<RepairNotificationItemViewModel>(OpenNotification);
        }

        private bool _isDropdownOpen;
        public bool IsDropdownOpen
        {
            get => _isDropdownOpen;
            set
            {
                if (!SetField(ref _isDropdownOpen, value)) return;
                if (value) _ = LoadAsync();
            }
        }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            private set
            {
                if (!SetField(ref _unreadCount, value)) return;
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(BadgeText));
            }
        }

        public bool HasUnread => _unreadCount > 0;
        public string BadgeText => _unreadCount > 99 ? "99+" : _unreadCount.ToString();

        private bool _isEmpty = true;
        public bool IsEmpty { get => _isEmpty; private set => SetField(ref _isEmpty, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

        /// <summary>Full reload of the dropdown list + badge from dbo.Notification.</summary>
        public async Task LoadAsync()
        {
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0)
            {
                Items.Clear();
                UnreadCount = 0;
                IsEmpty = true;
                return;
            }

            IsLoading = true;
            try
            {
                int userId = AppSession.CurrentUserId;
                var rows = await Task.Run(() => _repo.GetByUserAndPortal(userId, PortalKey, FetchLimit));

                Items.Clear();
                foreach (var dto in rows)
                    Items.Add(new RepairNotificationItemViewModel(dto));

                UnreadCount = rows.Count(r => !r.IsRead);
                IsEmpty = Items.Count == 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>Called by the shell when RepairPortalNotificationPoller reports freshly
        /// persisted rows — reloads so the new items and badge appear without waiting for the
        /// user to open the dropdown.</summary>
        public void OnNewNotifications(IReadOnlyList<NotificationDto> freshRows) => _ = LoadAsync();

        private async Task MarkAllReadAsync()
        {
            if (AppSession.CurrentUserId <= 0) return;
            int userId = AppSession.CurrentUserId;

            await Task.Run(() => _repo.MarkAllAsReadByPortal(userId, PortalKey));
            foreach (var item in Items) item.IsRead = true;
            UnreadCount = 0;
        }

        private async Task ClearAllAsync()
        {
            if (AppSession.CurrentUserId <= 0) return;
            int userId = AppSession.CurrentUserId;

            await Task.Run(() => _repo.ClearAllByPortal(userId, PortalKey));
            Items.Clear();
            UnreadCount = 0;
            IsEmpty = true;
        }

        private void OpenNotification(RepairNotificationItemViewModel item)
        {
            if (item == null) return;

            if (!item.IsRead && AppSession.CurrentUserId > 0)
            {
                int userId = AppSession.CurrentUserId;
                int id = item.NotificationId;
                _ = Task.Run(() => _repo.MarkAsRead(id, userId));
                item.IsRead = true;
                UnreadCount = Math.Max(0, UnreadCount - 1);
            }

            IsDropdownOpen = false;

            if (item.ReferenceId.HasValue && item.ReferenceId.Value > 0)
                RequestOpenTicket?.Invoke(item.ReferenceId.Value);
        }
    }
}
