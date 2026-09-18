using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>One checkbox entry in a Category (or similar) multi-select filter popup, shared
    /// by any list page's ViewModel that offers this kind of filter (e.g. Manage Sets, Requests).</summary>
    public sealed class CategoryFilterOption : ViewModelBase
    {
        public string Name { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetField(ref _isChecked, value))
                    CheckedChanged?.Invoke();
            }
        }

        /// <summary>Raised whenever IsChecked changes, including programmatic bulk updates —
        /// callers that need to suppress reactions during a bulk update must guard on their own flag.</summary>
        public event Action CheckedChanged;

        public CategoryFilterOption(string name)
        {
            Name = name;
        }
    }
}
