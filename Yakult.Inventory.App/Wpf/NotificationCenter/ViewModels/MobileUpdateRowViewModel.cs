using System;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.NotificationCenter.ViewModels
{
    public class MobileUpdateRowViewModel
    {
        // ── Data fields ───────────────────────────────────────────────────────
        public string   SetCode      { get; set; }
        public string   SerialNumber { get; set; }
        public string   NewStatus    { get; set; }
        public DateTime CreatedAt    { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────
        public Action<MobileUpdateRowViewModel> NavigateAction { get; set; }

        private ICommand _clickCommand;
        public ICommand ClickCommand => _clickCommand
            ?? (_clickCommand = new RelayCommand(() => NavigateAction?.Invoke(this)));

        // ── Computed display properties ───────────────────────────────────────
        public string CreatedAtFormatted => CreatedAt.ToString("MM/dd HH:mm");

        public string SecondaryLine
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(SerialNumber)) parts.Add(SerialNumber);
                if (!string.IsNullOrEmpty(NewStatus))    parts.Add(NewStatus);
                parts.Add(CreatedAtFormatted);
                return string.Join("  ·  ", parts);
            }
        }
    }
}
