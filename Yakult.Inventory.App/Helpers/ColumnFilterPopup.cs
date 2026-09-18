using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Excel-style column filter popup.
    /// Shows Sort A→Z / Sort Z→A buttons, a value search box,
    /// and a checklist of distinct values for the clicked column.
    /// </summary>
    internal class ColumnFilterPopup : Form
    {
        public enum PopupAction { None, SortAscending, SortDescending, Filter }

        // ── Results ─────────────────────────────────────────────────────────
        public PopupAction      Action         { get; private set; } = PopupAction.None;
        public HashSet<string>  SelectedValues { get; private set; }

        // ── State ───────────────────────────────────────────────────────────
        private readonly List<string> _allValues;
        private List<string>          _displayValues;
        private HashSet<string>       _checkedValues;

        private CheckedListBox _clbValues;
        private TextBox        _txtSearch;
        private CheckBox       _chkSelectAll;
        private bool           _updatingCheckState;

        // ── Constructor ─────────────────────────────────────────────────────
        /// <param name="columnHeader">Display name shown in the title bar.</param>
        /// <param name="distinctValues">All unique values for this column.</param>
        /// <param name="currentFilter">
        ///   Currently active filter set, or <c>null</c> if no filter (all values shown).
        /// </param>
        public ColumnFilterPopup(
            string              columnHeader,
            IEnumerable<string> distinctValues,
            HashSet<string>     currentFilter)
        {
            _allValues = (distinctValues ?? Enumerable.Empty<string>())
                .Where(v => v != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // null currentFilter → all values are selected (no active filter)
            _checkedValues = currentFilter != null
                ? new HashSet<string>(currentFilter,  StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(_allValues,     StringComparer.OrdinalIgnoreCase);

            _displayValues = new List<string>(_allValues);

            // ── Form settings ───────────────────────────────────────────────
            Text            = $"Filter: {columnHeader}";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9F);
            KeyPreview      = true;
            KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) { Action = PopupAction.None; Close(); } };

            BuildUI();
        }

        // ── UI Construction ─────────────────────────────────────────────────
        private void BuildUI()
        {
            const int pad  = 8;
            const int btnW = 320;
            const int btnH = 32;
            int y = 8;

            // Sort A→Z
            var btnSortAsc = MakeTextButton("↑  Sort A → Z", pad, y, btnW, btnH);
            btnSortAsc.Click += (s, e) => { Action = PopupAction.SortAscending; Close(); };
            Controls.Add(btnSortAsc);
            y += btnH + 3;

            // Sort Z→A
            var btnSortDesc = MakeTextButton("↓  Sort Z → A", pad, y, btnW, btnH);
            btnSortDesc.Click += (s, e) => { Action = PopupAction.SortDescending; Close(); };
            Controls.Add(btnSortDesc);
            y += btnH + 8;

            // Separator
            Controls.Add(new Panel { Left = pad, Top = y, Width = btnW, Height = 1, BackColor = Color.FromArgb(210, 210, 210) });
            y += 7;

            // Search row: "Search:" label + TextBox
            Controls.Add(new Label
            {
                Text      = "Search:",
                Left      = pad,
                Top       = y + 3,
                Width     = 65,
                Height    = 20,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Gray
            });
            _txtSearch = new TextBox
            {
                Left   = pad + 69,
                Top    = y,
                Width  = btnW - 69,
                Height = 22,
                Font   = new Font("Segoe UI", 9F)
            };
            _txtSearch.TextChanged += (s, e) => RefreshDisplayList();
            Controls.Add(_txtSearch);
            y += 28;

            // (Select All)
            _chkSelectAll = new CheckBox
            {
                Text    = "(Select All)",
                Left    = pad,
                Top     = y,
                Width   = btnW,
                Height  = 28,
                Font    = new Font("Segoe UI", 9F, FontStyle.Bold),
                Checked = _allValues.Count > 0 && _allValues.All(v => _checkedValues.Contains(v))
            };
            _chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
            Controls.Add(_chkSelectAll);
            y += 30;

            // Thin separator under Select All
            Controls.Add(new Panel { Left = pad, Top = y, Width = btnW, Height = 1, BackColor = Color.FromArgb(220, 220, 220) });
            y += 4;

            // Values checklist
            _clbValues = new CheckedListBox
            {
                Left          = pad,
                Top           = y,
                Width         = btnW,
                Height        = 185,
                CheckOnClick  = true,
                BorderStyle   = BorderStyle.FixedSingle,
                Font          = new Font("Segoe UI", 9F),
                IntegralHeight = false
            };
            RefreshDisplayList();
            _clbValues.ItemCheck += ClbValues_ItemCheck;
            Controls.Add(_clbValues);
            y += 189;

            // Cancel / OK
            var btnCancel = MakeTextButton("Cancel", pad, y, btnW / 2 - 2, btnH);
            btnCancel.Click += (s, e) => { Action = PopupAction.None; Close(); };
            Controls.Add(btnCancel);

            var btnOk = MakeTextButton("OK", pad + btnW / 2 + 2, y, btnW / 2 - 2, btnH);
            btnOk.BackColor = Color.FromArgb(0, 120, 215);
            btnOk.ForeColor = Color.White;
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(0, 90, 180);
            btnOk.TextAlign = ContentAlignment.MiddleCenter;
            btnOk.Click += (s, e) =>
            {
                Action = PopupAction.Filter;
                // If a search is active, only return the items currently visible and checked.
                // Items hidden by the search are excluded — otherwise all-values bleeds through
                // and the caller's "count >= total" check wrongly removes the filter.
                bool hasSearch = !string.IsNullOrEmpty(_txtSearch?.Text.Trim());
                SelectedValues = hasSearch
                    ? new HashSet<string>(
                        _displayValues.Where(v => _checkedValues.Contains(v)),
                        StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(_checkedValues, StringComparer.OrdinalIgnoreCase);
                Close();
            };
            Controls.Add(btnOk);

            AcceptButton = btnOk;
            ClientSize   = new Size(btnW + pad * 2, y + btnH + 8);
        }

        private Button MakeTextButton(string text, int left, int top, int width, int height)
        {
            var btn = new Button
            {
                Text      = text,
                Left      = left,
                Top       = top,
                Width     = width,
                Height    = height,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 9F),
                BackColor = Color.White,
                ForeColor = Color.Black
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            return btn;
        }

        // ── List Management ─────────────────────────────────────────────────
        private void RefreshDisplayList()
        {
            string q = _txtSearch?.Text.Trim() ?? "";
            _displayValues = string.IsNullOrEmpty(q)
                ? new List<string>(_allValues)
                : _allValues.Where(v => v.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _clbValues.ItemCheck -= ClbValues_ItemCheck;
            _clbValues.BeginUpdate();
            _clbValues.Items.Clear();
            foreach (var v in _displayValues)
                _clbValues.Items.Add(v, _checkedValues.Contains(v));
            _clbValues.EndUpdate();
            _clbValues.ItemCheck += ClbValues_ItemCheck;

            UpdateSelectAllCheckbox();
        }

        private void UpdateSelectAllCheckbox()
        {
            if (_chkSelectAll == null) return;
            _updatingCheckState = true;
            _chkSelectAll.Checked = _displayValues.Count > 0
                && _displayValues.All(v => _checkedValues.Contains(v));
            _updatingCheckState = false;
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (_updatingCheckState) return;
            bool check = _chkSelectAll.Checked;
            foreach (var v in _displayValues)
            {
                if (check) _checkedValues.Add(v);
                else       _checkedValues.Remove(v);
            }
            _clbValues.ItemCheck -= ClbValues_ItemCheck;
            for (int i = 0; i < _clbValues.Items.Count; i++)
                _clbValues.SetItemChecked(i, check);
            _clbValues.ItemCheck += ClbValues_ItemCheck;
        }

        private void ClbValues_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _displayValues.Count) return;
            string val = _displayValues[e.Index];
            if (e.NewValue == CheckState.Checked) _checkedValues.Add(val);
            else                                   _checkedValues.Remove(val);
            BeginInvoke((Action)UpdateSelectAllCheckbox);
        }
    }
}
