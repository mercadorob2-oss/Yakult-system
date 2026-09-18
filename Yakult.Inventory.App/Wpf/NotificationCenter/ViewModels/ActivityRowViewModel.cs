using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.NotificationCenter.ViewModels
{
    /// <summary>
    /// One row in the home bell's "Activity" tab — a persisted dbo.Notification owned by the
    /// InventorySystem portal (currently only ITEM_ADDED). A batch row carries an expandable,
    /// per-item clickable <see cref="Children"/> list parsed from Notification.DetailsJson.
    /// </summary>
    public class ActivityRowViewModel : ViewModelBase
    {
        public int      NotificationId   { get; set; }
        public string   Title            { get; set; }
        public string   Message          { get; set; }
        public string   NotificationType { get; set; }
        public int?     ReferenceId      { get; set; }
        public DateTime CreatedDate      { get; set; }

        public IReadOnlyList<ActivityChildViewModel> Children { get; set; }
        public bool HasChildren => Children != null && Children.Count > 0;
        public Visibility ChildToggleVisibility => HasChildren ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Origin flow of the add ("Request Set", "Invoice", "Batch Add", …); null if unknown.</summary>
        public string SourceLabel { get; set; }
        public Visibility SourceVisibility => string.IsNullOrWhiteSpace(SourceLabel) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetField(ref _isExpanded, value))
                {
                    OnPropertyChanged(nameof(ExpandGlyph));
                    OnPropertyChanged(nameof(ChildListVisibility));
                }
            }
        }
        public string ExpandGlyph => _isExpanded ? "▾" : "▸";
        public Visibility ChildListVisibility => _isExpanded && HasChildren ? Visibility.Visible : Visibility.Collapsed;

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (SetField(ref _isRead, value))
                {
                    OnPropertyChanged(nameof(UnreadDotBrush));
                    OnPropertyChanged(nameof(TitleWeight));
                }
            }
        }

        // Invoked with (this) when the row's header is clicked. Set by the ViewModel before the
        // row reaches the UI; it marks the row read and either expands the child list (batch row)
        // or routes navigation through MainForm (single-item row).
        public Action<ActivityRowViewModel> ClickAction { get; set; }

        private ICommand _clickCommand;
        public ICommand ClickCommand => _clickCommand
            ?? (_clickCommand = new RelayCommand(() => ClickAction?.Invoke(this)));

        // Dedicated expand/collapse toggle for the ▸/▾ glyph — separate from the header click so a
        // SET_CREATED row can still navigate while its item list stays independently expandable.
        private ICommand _toggleExpandCommand;
        public ICommand ToggleExpandCommand => _toggleExpandCommand
            ?? (_toggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded));

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - CreatedDate;
                if (diff.TotalMinutes < 1)  return "just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24)   return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7)     return $"{(int)diff.TotalDays}d ago";
                return CreatedDate.ToLocalTime().ToString("MMM d");
            }
        }

        // Unread rows get a solid accent dot; read rows a muted one.
        public Brush UnreadDotBrush => IsRead
            ? new SolidColorBrush(Color.FromRgb(203, 213, 225))   // slate-300
            : new SolidColorBrush(Color.FromRgb(78, 154, 252));   // #4E9AFC

        public FontWeight TitleWeight => IsRead ? FontWeights.Normal : FontWeights.SemiBold;
    }

    /// <summary>One item line under an expanded batch Activity row. Clicking it opens that item.</summary>
    public class ActivityChildViewModel
    {
        public int    ItemId    { get; set; }
        public string Name      { get; set; }
        public string TypeLabel { get; set; }   // e.g. "(Hardware)"; null when unknown
        public string QtyLabel  { get; set; }   // e.g. "×2"; null when qty <= 1

        // Set by the ViewModel: marks the parent notification read and navigates to this item.
        public Action<int> NavigateAction { get; set; }

        private ICommand _clickCommand;
        public ICommand ClickCommand => _clickCommand
            ?? (_clickCommand = new RelayCommand(() => { if (ItemId > 0) NavigateAction?.Invoke(ItemId); }));
    }
}
