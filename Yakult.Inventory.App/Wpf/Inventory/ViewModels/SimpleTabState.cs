using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Yakult.Inventory.App.WPF.Inventory.ViewModels
{
    /// <summary>
    /// Shared load/filter/sort/page state for the four read-only summary tabs
    /// (Category Stock, Hardware, Software/License, Services), which are structurally
    /// identical in the original ViewInventoryPage.cs (SortList&lt;T&gt; + per-tab
    /// Skip/Take pagination), differing only in DTO type and search predicate.
    /// </summary>
    internal sealed class SimpleTabState<T>
    {
        public List<T> All { get; set; } = new List<T>();
        public List<T> Filtered { get; set; } = new List<T>();
        public int CurrentPage { get; set; } = 1;
        public bool Loaded { get; set; }
        public string SortColumnKey { get; set; }
        public ListSortDirection? SortDirection { get; set; }
        public ObservableCollection<T> Paged { get; } = new ObservableCollection<T>();

        public const int PageSize = 10;

        public int TotalPages => Filtered.Count == 0 ? 0 : (int)Math.Ceiling((double)Filtered.Count / PageSize);

        /// <summary>Mirrors SortList&lt;T&gt;: reflection-based OrderBy/OrderByDescending by property name.</summary>
        public void Sort()
        {
            if (SortColumnKey == null || SortDirection == null) return;

            var prop = typeof(T).GetProperty(SortColumnKey);
            if (prop == null) return;

            Filtered = SortDirection == ListSortDirection.Ascending
                ? Filtered.OrderBy(x => prop.GetValue(x, null)).ToList()
                : Filtered.OrderByDescending(x => prop.GetValue(x, null)).ToList();
        }

        public void UpdatePage()
        {
            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var pageItems = Filtered.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Paged.Clear();
            foreach (var item in pageItems) Paged.Add(item);
        }
    }
}
