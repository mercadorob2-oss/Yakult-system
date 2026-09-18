using System.Collections.Generic;

namespace Yakult.Inventory.App.Pages.Item
{
    // Tracks checkbox selection by ItemId instead of DataGridView row index, so the
    // selection survives pagination, filtering, sorting, and grid refreshes.
    internal sealed class ItemSelectionManager
    {
        private readonly HashSet<int> _selectedIds = new HashSet<int>();

        public int Count => _selectedIds.Count;

        public bool IsSelected(int itemId) => _selectedIds.Contains(itemId);

        public void SetSelected(int itemId, bool selected)
        {
            if (selected)
                _selectedIds.Add(itemId);
            else
                _selectedIds.Remove(itemId);
        }

        public void Clear() => _selectedIds.Clear();

        // Restores Selected flags on a freshly-loaded item list (e.g. after LoadItems()
        // rebuilds ItemDto instances from the database) and drops ids that no longer
        // exist in the loaded set so stale selections don't linger forever.
        public void ApplyAndReconcile(IEnumerable<ItemDto> items)
        {
            var liveIds = new HashSet<int>();
            foreach (var item in items)
            {
                liveIds.Add(item.ItemId);
                item.Selected = _selectedIds.Contains(item.ItemId);
            }
            _selectedIds.IntersectWith(liveIds);
        }
    }
}
