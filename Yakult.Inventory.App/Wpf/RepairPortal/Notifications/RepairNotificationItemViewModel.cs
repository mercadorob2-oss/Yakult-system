using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Notifications
{
    /// <summary>One row in the notification-bell dropdown. Wraps a persisted dbo.Notification
    /// record (via <see cref="NotificationDto"/>) with the display-only bits the template binds to.</summary>
    public sealed class RepairNotificationItemViewModel : ViewModelBase
    {
        private static readonly SolidColorBrush GreenBrush = Freeze("#16A34A");
        private static readonly SolidColorBrush BlueBrush  = Freeze("#2563EB");
        private static readonly SolidColorBrush GrayBrush  = Freeze("#64748B");

        private static readonly SolidColorBrush GreenBg = Freeze("#DCFCE7");
        private static readonly SolidColorBrush BlueBg  = Freeze("#DBEAFE");
        private static readonly SolidColorBrush GrayBg  = Freeze("#F1F5F9");

        public RepairNotificationItemViewModel(NotificationDto dto)
        {
            NotificationId   = dto.NotificationId;
            Title            = dto.Title;
            Message          = dto.Message;
            TimeAgo          = dto.TimeAgo;
            NotificationType = dto.NotificationType;
            ReferenceId      = dto.ReferenceId;
            _isRead          = dto.IsRead;
        }

        public int NotificationId { get; }
        public string Title { get; }
        public string Message { get; }
        public string TimeAgo { get; }
        public string NotificationType { get; }
        public int? ReferenceId { get; }

        private bool _isRead;
        public bool IsRead { get => _isRead; set { if (SetField(ref _isRead, value)) OnPropertyChanged(nameof(IsUnread)); } }
        public bool IsUnread => !_isRead;

        // Segoe MDL2 Assets glyphs — same font the shell header already uses.
        //   E90F = Repair (wrench + screwdriver) for a new ticket
        //   E930 = Completed (check) for a repaired ticket
        //   E7E7 = Ringer as a generic fallback
        public string IconGlyph =>
            NotificationType == Models.NotificationType.RepairTicketCompleted ? ""
          : NotificationType == Models.NotificationType.RepairTicketNew       ? ""
          : "";

        public Brush IconForeground =>
            NotificationType == Models.NotificationType.RepairTicketCompleted ? GreenBrush
          : NotificationType == Models.NotificationType.RepairTicketNew       ? BlueBrush
          : GrayBrush;

        public Brush IconBackground =>
            NotificationType == Models.NotificationType.RepairTicketCompleted ? GreenBg
          : NotificationType == Models.NotificationType.RepairTicketNew       ? BlueBg
          : GrayBg;

        private static SolidColorBrush Freeze(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }
}
