using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Helpers
{
    internal class DateRangeFilterPopup : Form
    {
        public enum PopupAction { None, SortAscending, SortDescending, Filter, Clear }

        public PopupAction Action    { get; private set; } = PopupAction.None;
        public DateTime?   DateFrom  { get; private set; }
        public DateTime?   DateTo    { get; private set; }

        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;

        public DateRangeFilterPopup(string columnHeader, DateTime? currentFrom, DateTime? currentTo)
        {
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

            BuildUI(currentFrom, currentTo);
        }

        private void BuildUI(DateTime? currentFrom, DateTime? currentTo)
        {
            const int pad  = 8;
            const int btnW = 300;
            const int btnH = 32;
            int y = 8;

            // Sort Oldest → Newest
            var btnSortAsc = MakeTextButton("↑  Sort Oldest → Newest", pad, y, btnW, btnH);
            btnSortAsc.Click += (s, e) => { Action = PopupAction.SortAscending; Close(); };
            Controls.Add(btnSortAsc);
            y += btnH + 3;

            // Sort Newest → Oldest
            var btnSortDesc = MakeTextButton("↓  Sort Newest → Oldest", pad, y, btnW, btnH);
            btnSortDesc.Click += (s, e) => { Action = PopupAction.SortDescending; Close(); };
            Controls.Add(btnSortDesc);
            y += btnH + 8;

            // Separator
            Controls.Add(new Panel { Left = pad, Top = y, Width = btnW, Height = 1, BackColor = Color.FromArgb(210, 210, 210) });
            y += 7;

            // "From:" label + DateTimePicker
            Controls.Add(new Label
            {
                Text      = "From:",
                Left      = pad,
                Top       = y + 3,
                Width     = 50,
                Height    = 20,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Gray
            });
            _dtpFrom = new DateTimePicker
            {
                Left        = pad + 54,
                Top         = y,
                Width       = btnW - 54,
                Format      = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Checked     = currentFrom.HasValue,
                Value       = currentFrom ?? DateTime.Today
            };
            Controls.Add(_dtpFrom);
            y += 30;

            // "To:" label + DateTimePicker
            Controls.Add(new Label
            {
                Text      = "To:",
                Left      = pad,
                Top       = y + 3,
                Width     = 50,
                Height    = 20,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.Gray
            });
            _dtpTo = new DateTimePicker
            {
                Left        = pad + 54,
                Top         = y,
                Width       = btnW - 54,
                Format      = DateTimePickerFormat.Short,
                ShowCheckBox = true,
                Checked     = currentTo.HasValue,
                Value       = currentTo ?? DateTime.Today
            };
            Controls.Add(_dtpTo);
            y += 30 + 8;

            // Separator
            Controls.Add(new Panel { Left = pad, Top = y, Width = btnW, Height = 1, BackColor = Color.FromArgb(210, 210, 210) });
            y += 7;

            // Clear Filter / OK buttons
            var btnClear = MakeTextButton("Clear Filter", pad, y, btnW / 2 - 2, btnH);
            btnClear.Click += (s, e) => { Action = PopupAction.Clear; Close(); };
            Controls.Add(btnClear);

            var btnOk = MakeTextButton("OK", pad + btnW / 2 + 2, y, btnW / 2 - 2, btnH);
            btnOk.BackColor = Color.FromArgb(0, 120, 215);
            btnOk.ForeColor = Color.White;
            btnOk.FlatAppearance.BorderColor = Color.FromArgb(0, 90, 180);
            btnOk.TextAlign = ContentAlignment.MiddleCenter;
            btnOk.Click += (s, e) =>
            {
                Action   = PopupAction.Filter;
                DateFrom = _dtpFrom.Checked ? _dtpFrom.Value.Date : (DateTime?)null;
                DateTo   = _dtpTo.Checked   ? _dtpTo.Value.Date   : (DateTime?)null;
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
    }
}
