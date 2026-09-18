using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.Shared.Helpers
{
    /// <summary>A "Sort By" dropdown entry: a column + direction pair, optionally a boolean-column special case.</summary>
    public sealed class ListSortOption
    {
        public string DisplayText { get; set; }
        public string ColumnKey { get; set; }
        public ListSortDirection Direction { get; set; }
        public bool? BooleanValue { get; set; }
        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Reusable "Sort By" dropdown population and default-sort-column resolution shared by the
    /// WPF list-page conversions. Mirrors the decision rules in the WinForms
    /// DefaultListPageTemplate (ResolveDefaultSortColumnKey / PopulateSortByOptions /
    /// ResolveCreatedDateColumn), reimplemented against WPF DataGridColumn since
    /// DefaultListPageTemplate itself is WinForms-only and stays untouched.
    /// </summary>
    public static class ListSortHelper
    {
        private static readonly string[] CreatedDateCandidates =
        {
            "DateCreated", "CreatedAt", "CreatedDate", "DatePosted", "DateRequested", "AuditDate", "Timestamp"
        };

        public static string SortKey(DataGridColumn col)
            => col == null ? null : (!string.IsNullOrEmpty(col.SortMemberPath) ? col.SortMemberPath : col.Header?.ToString());

        private static string HeaderText(DataGridColumn col) => col?.Header?.ToString();

        /// <summary>Builds ASC/DESC (or Checked/Unchecked for boolean columns) options from a DataGrid's current columns.</summary>
        public static List<ListSortOption> BuildOptions(DataGrid grid, Func<DataGridColumn, bool> isBooleanColumn = null)
        {
            var options = new List<ListSortOption>();
            if (grid == null) return options;

            foreach (var col in grid.Columns)
            {
                if (col.Visibility != Visibility.Visible) continue;
                if (!col.CanUserSort) continue;

                string key = SortKey(col);
                if (string.IsNullOrEmpty(key)) continue;

                string displayName = HeaderText(col) ?? key;
                bool isBool = isBooleanColumn?.Invoke(col) ?? false;

                if (isBool)
                {
                    options.Add(new ListSortOption { DisplayText = $"{displayName} (Checked)", ColumnKey = key, Direction = ListSortDirection.Descending, BooleanValue = true });
                    options.Add(new ListSortOption { DisplayText = $"{displayName} (Unchecked)", ColumnKey = key, Direction = ListSortDirection.Ascending, BooleanValue = false });
                }
                else
                {
                    options.Add(new ListSortOption { DisplayText = $"{displayName} (ASC)", ColumnKey = key, Direction = ListSortDirection.Ascending });
                    options.Add(new ListSortOption { DisplayText = $"{displayName} (DESC)", ColumnKey = key, Direction = ListSortDirection.Descending });
                }
            }

            return options;
        }

        /// <summary>Prefers an ID-like column (header "ID" or key ending in Id/ID); falls back to the first sortable column.</summary>
        public static string ResolveDefaultSortColumnKey(DataGrid grid)
        {
            if (grid == null || grid.Columns.Count == 0) return null;

            var sortable = grid.Columns
                .Where(c => c.Visibility == Visibility.Visible && c.CanUserSort)
                .ToList();

            var idCol = sortable.FirstOrDefault(c =>
            {
                var key = SortKey(c) ?? string.Empty;
                var header = HeaderText(c) ?? string.Empty;

                if (string.Equals(header.Trim(), "ID", StringComparison.OrdinalIgnoreCase)) return true;
                return key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                    || key.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase);
            });

            return SortKey(idCol ?? sortable.FirstOrDefault());
        }

        /// <summary>Resolves the first matching "created date"-like property on a type, for Filter-By
        /// ("Most Recently Added"/"Oldest Added") fallback sorting when no explicit column sort is active.</summary>
        public static PropertyInfo ResolveCreatedDateProperty(Type type)
        {
            if (type == null) return null;
            foreach (var name in CreatedDateCandidates)
            {
                var p = type.GetProperty(name);
                if (p != null) return p;
            }
            return null;
        }
    }
}
