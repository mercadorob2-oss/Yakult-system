using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    /// <summary>Checkbox item backing the multi-select Category filter (mirrors ItemsPageViewModel's CategoryFilterOption).</summary>
    public sealed class CategoryFilterOption : ViewModelBase
    {
        private readonly Func<bool> _isSuppressed;
        private readonly Action _onChanged;

        public string Name { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!SetField(ref _isSelected, value)) return;
                if (_isSuppressed()) return;
                _onChanged();
            }
        }

        public CategoryFilterOption(string name, Func<bool> isSuppressed, Action onChanged)
        {
            Name = name;
            _isSuppressed = isSuppressed;
            _onChanged = onChanged;
        }
    }
}
