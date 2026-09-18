using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>
    /// Generic row-selection tracker for list pages with a checkbox column. Tracks selection
    /// against the FULL loaded list (not just the current page), so a selection made on one
    /// page survives paging/filtering — the bug this was built to fix (see EditItemDialog/
    /// EmployeePageViewModel/VendorPageViewModel history: reading selection off the paged
    /// collection silently dropped it whenever the page was rebuilt).
    ///
    /// Non-intrusive by design: takes plain get/set/id delegates instead of requiring row DTOs
    /// to implement a shared interface, so it drops into any existing "Selected" + "XId" pair
    /// without touching the DTO classes themselves.
    /// </summary>
    public class SelectionTracker<T>
    {
        private readonly Func<T, bool> _getSelected;
        private readonly Action<T, bool> _setSelected;
        private readonly Func<T, int> _getId;

        public SelectionTracker(Func<T, bool> getSelected, Action<T, bool> setSelected, Func<T, int> getId)
        {
            _getSelected = getSelected ?? throw new ArgumentNullException(nameof(getSelected));
            _setSelected = setSelected ?? throw new ArgumentNullException(nameof(setSelected));
            _getId = getId ?? throw new ArgumentNullException(nameof(getId));
        }

        public int SelectedCount { get; private set; }
        public bool HasSelection => SelectedCount > 0;

        /// <summary>Fires whenever SelectedCount/HasSelection change — hook this to raise the
        /// owning ViewModel's PropertyChanged for both properties.</summary>
        public event Action Changed;

        public void Sync(IEnumerable<T> allItems)
        {
            SelectedCount = (allItems ?? Enumerable.Empty<T>()).Count(x => x != null && _getSelected(x));
            Changed?.Invoke();
        }

        public List<T> GetSelected(IEnumerable<T> allItems)
            => (allItems ?? Enumerable.Empty<T>()).Where(x => x != null && _getSelected(x)).ToList();

        public void SetSelected(IEnumerable<T> allItems, int id, bool selected)
        {
            var list = (allItems ?? Enumerable.Empty<T>()).ToList();
            var item = list.FirstOrDefault(x => x != null && _getId(x) == id);
            if (item == null) return;

            _setSelected(item, selected);
            Sync(list);
        }

        /// <summary>Scoped to whatever subset is passed in (typically the current page) —
        /// matches the header checkbox's "select/deselect all rows on this page" behavior.</summary>
        public void SetManySelected(IEnumerable<T> items, IEnumerable<T> allItems, bool selected)
        {
            foreach (var item in items ?? Enumerable.Empty<T>())
                _setSelected(item, selected);

            Sync(allItems);
        }

        public void ClearSelection(IEnumerable<T> allItems)
        {
            var list = (allItems ?? Enumerable.Empty<T>()).ToList();
            foreach (var item in list)
                _setSelected(item, false);

            Sync(list);
        }
    }
}
