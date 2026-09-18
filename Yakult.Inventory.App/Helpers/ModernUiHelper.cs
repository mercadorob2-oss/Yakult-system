using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Helpers
{
    public static class ModernUiHelper
    {
        public static readonly Color ColorPrimary = Color.FromArgb(41, 128, 185);
        public static readonly Color ColorDanger = Color.IndianRed;
        public static readonly Color ColorSuccess = Color.SeaGreen;
        public static readonly Color ColorTextPrimary = Color.FromArgb(44, 62, 80);
        public static readonly Color ColorTextSecondary = Color.FromArgb(90, 100, 110);
        public static readonly Color ColorBackground = Color.WhiteSmoke;
        public static readonly Font FontHeader = new Font("Segoe UI", 12F, FontStyle.Bold);
        public static readonly Font FontSection = new Font("Segoe UI", 10F, FontStyle.Bold);
        public static readonly Font FontLabel = new Font("Segoe UI Semibold", 9.5F);
        public static readonly Font FontNormal = new Font("Segoe UI", 9.5F);

        public static Label CreateHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColorTextPrimary
            };
        }

        public static Label CreateSectionHeader(string text)
        {
            return new Label
            {
                Text = text.ToUpperInvariant(),
                AutoSize = true,
                Padding = new Padding(0, 15, 0, 5),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 160, 160)
            };
        }

        public static Panel CreateStyledPanel()
        {
            var pnl = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(15)
            };
            pnl.Paint += (s, e) =>
            {
                var rect = pnl.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (var pen = new Pen(Color.FromArgb(230, 230, 230)))
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };
            return pnl;
        }

        public static Button CreatePrimaryButton(string text, int width = 100)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int width = 100)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                BackColor = Color.White,
                ForeColor = ColorTextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            return btn;
        }
        
        public static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = FontLabel,
                ForeColor = ColorTextPrimary
            };
        }

        public static TextBox CreateTextBox(bool password = false)
        {
            return new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                UseSystemPasswordChar = password,
                Font = FontNormal,
                Height = 28
            };
        }

        public static void ConfigureModernGrid(DataGridView grid)
        {
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 110, 120);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(50, 60, 70);
            grid.DefaultCellStyle.Padding = new Padding(10, 8, 10, 8);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 245, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(240, 240, 240);
            grid.RowTemplate.Height = 44;
        }
        // --- NEW COMPONENTS ---

        public static Panel CreateCard()
        {
            var pnl = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(20),
                Margin = new Padding(10)
            };
            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = pnl.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                
                // Subtle border
                using (var pen = new Pen(Color.FromArgb(230, 230, 230)))
                {
                    g.DrawRectangle(pen, rect);
                }
            };
            return pnl;
        }

        public static Label CreateBadge(string text, Color color)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(3),
                TextAlign = ContentAlignment.MiddleCenter
            };
            // WinForms Labels don't support border radius natively without custom paint, 
            // but we can simulate a "Pill" look by ensuring specific padding or wrapping in a graphic panel.
            // For now, a clean colored rect is sufficient for "Modern" winforms.
            return lbl;
        }

        public static Panel CreateKpiCard(string title, string value, string subtext, Color accentColor)
        {
            var card = CreateCard();
            card.Size = new Size(200, 100); // Default
            
            var lblTitle = new Label
            {
                Text = title.ToUpper(),
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColorTextSecondary,
                Padding = new Padding(0, 0, 0, 5)
            };

            var lblValue = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = accentColor,
                Height = 40
            };

            var lblSub = new Label
            {
                Text = subtext,
                Dock = DockStyle.Bottom,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = Color.Gray
            };

            card.Controls.Add(lblSub);
            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            return card;
        }

        public static Panel CreateProgressBar(int value, int max, Color color)
        {
            var pnl = new Panel { Height = 6, BackColor = Color.FromArgb(230, 230, 230) }; // Track
            
            var pct = max > 0 ? (double)value / max : 0;
            if (pct > 1) pct = 1;
            if (pct < 0) pct = 0;

            var fill = new Panel
            {
                BackColor = color,
                Height = 6,
                Dock = DockStyle.Left,
                Width = 0 // Will adjust on paint/layout or explicit set? Explicit is safer.
            };
            
            pnl.Controls.Add(fill);
            
            // Layout handler to size the fill correctly when parent sizes
            pnl.SizeChanged += (s, e) =>
            {
                fill.Width = (int)(pnl.Width * pct);
            };
            
            return pnl;
        }

        public static Button CreateFilterChip(string text, bool isActive, System.Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular),
                BackColor = isActive ? ColorPrimary : Color.White,
                ForeColor = isActive ? Color.White : ColorTextPrimary,
                Margin = new Padding(3, 0, 3, 0)
            };
            btn.FlatAppearance.BorderSize = isActive ? 0 : 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }
    }
}
