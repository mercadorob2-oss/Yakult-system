using System;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.NotificationCenter.ViewModels
{
    public class ExpiryRowViewModel
    {
        // ── Data fields ───────────────────────────────────────────────────────
        public string ItemName      { get; set; }
        public string ItemType      { get; set; }
        public int    DaysRemaining { get; set; }
        // True when the row came from dbo.[Set]; false when it came from dbo.Item.
        // Drives the navigation destination: sets go to "Renewal", items go to "Items".
        public bool   IsSet         { get; set; }

        // Navigation destination string used by MainForm.NavigateToItemPage.
        public string Destination => IsSet ? "Renewal" : "Items";

        // ── Navigation (set by the ViewModel before the rows reach the UI) ───
        public Action<ExpiryRowViewModel> NavigateAction { get; set; }

        private ICommand _clickCommand;
        public ICommand ClickCommand => _clickCommand
            ?? (_clickCommand = new RelayCommand(() => NavigateAction?.Invoke(this)));

        // ── Computed display properties ───────────────────────────────────────
        public string DaysLabel
        {
            get
            {
                if (DaysRemaining < 0)  return $"{-DaysRemaining}d overdue";
                if (DaysRemaining == 0) return "expires today";
                return $"{DaysRemaining}d left";
            }
        }

        public Brush DaysBadgeBackground
        {
            get
            {
                if (DaysRemaining < 0)   return new SolidColorBrush(Color.FromRgb(192,  57,  43)); // red
                if (DaysRemaining <= 30) return new SolidColorBrush(Color.FromRgb(211, 100,   0)); // orange
                if (DaysRemaining <= 60) return new SolidColorBrush(Color.FromRgb(180, 130,   0)); // amber
                return new SolidColorBrush(Color.FromRgb(39, 174, 96));                             // green
            }
        }

        // Kept for legacy XAML compatibility — no longer used in the new card design.
        public Brush RowBackground
        {
            get
            {
                if (DaysRemaining < 0)   return new SolidColorBrush(Color.FromRgb(255, 200, 200));
                if (DaysRemaining <= 30) return new SolidColorBrush(Color.FromRgb(255, 230, 200));
                if (DaysRemaining <= 60) return new SolidColorBrush(Color.FromRgb(255, 255, 200));
                return Brushes.Transparent;
            }
        }
    }
}
