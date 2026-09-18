using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ReaLTaiizor.Controls;

namespace Yakult.Inventory.App.Helpers
{
    internal static class UiFactory
    {
        internal static void MakePill(Control control)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            int radius = control.Height;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius, radius, 90, 180);
                path.AddArc(control.Width - radius, 0, radius, radius, 270, 180);
                path.CloseAllFigures();
                control.Region = new Region(path);
            }
        }

        internal static void ConfigurePillHopeButton(HopeButton button, Color baseColor, Color hoverColor)
        {
            if (button == null)
                return;

            button.PrimaryColor = baseColor;
            button.DefaultColor = baseColor;
            button.BorderColor = baseColor;
            button.TextColor = Color.White;
            button.HoverTextColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverColor;
                button.DefaultColor = hoverColor;
                button.BorderColor = hoverColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = baseColor;
                button.DefaultColor = baseColor;
                button.BorderColor = baseColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        internal static void ConfigureOutlineHopeButton(HopeButton button, Color borderColor, Color hoverBackColor)
        {
            if (button == null)
                return;

            button.PrimaryColor = Color.White;
            button.DefaultColor = Color.White;
            button.BorderColor = borderColor;
            button.TextColor = borderColor;
            button.HoverTextColor = borderColor;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;

            button.Paint += (s, e) =>
            {
                var b = s as Control;
                if (b == null || b.Width <= 1 || b.Height <= 1)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(borderColor, 2.2f))
                using (var path = new GraphicsPath())
                {
                    pen.Alignment = PenAlignment.Inset;
                    var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                    int radius = 6;
                    int d = radius * 2;
                    path.AddArc(rect.X, rect.Y, d, d, 90, 180);
                    path.AddLine(rect.X + (d / 2), rect.Bottom, rect.Right - (d / 2), rect.Bottom);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
                    path.AddLine(rect.Right - (d / 2), rect.Y, rect.X + (d / 2), rect.Y);
                    path.CloseFigure();
                    e.Graphics.DrawPath(pen, path);
                }
            };

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverBackColor;
                button.DefaultColor = hoverBackColor;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = Color.White;
                button.DefaultColor = Color.White;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        internal static void MakeCircle(HopeButton button)
        {
            if (button == null)
                return;

            button.Resize += (s, e) =>
            {
                using (var path = new GraphicsPath())
                {
                    int w = Math.Max(1, button.Width - 1);
                    int h = Math.Max(1, button.Height - 1);
                    path.AddEllipse(0, 0, w, h);
                    button.Region = new Region(path);
                }
            };
        }

        internal static MaterialCard CreateSummaryCard(string title, out Label valueLabel, Color accent)
        {
            var card = new MaterialCard
            {
                Size = UiTheme.Sizes.SummaryCard,
                BackColor = UiTheme.Colors.CardBack,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 10, 0)
            };

            var lblCardTitle = new Label
            {
                Text = title,
                AutoSize = true,
                Font = UiTheme.Fonts.SummaryTitle,
                ForeColor = UiTheme.Colors.TextMuted,
                Location = new Point(12, 10)
            };

            valueLabel = new Label
            {
                Text = "0",
                AutoSize = true,
                Font = UiTheme.Fonts.SummaryValue,
                ForeColor = accent,
                Location = new Point(12, 32)
            };

            card.Controls.Add(lblCardTitle);
            card.Controls.Add(valueLabel);
            return card;
        }

        internal static void StyleGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            DataGridViewSafety.Attach(grid);

            grid.BackgroundColor = UiTheme.Colors.CardBack;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = UiTheme.Colors.CardBack;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.Colors.GridHeaderBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.Fonts.GridHeader;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            grid.DefaultCellStyle.Font = UiTheme.Fonts.GridCell;
            grid.DefaultCellStyle.SelectionBackColor = UiTheme.Colors.GridSelectionBack;
            grid.DefaultCellStyle.SelectionForeColor = UiTheme.Colors.GridSelectionFore;
            grid.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.Colors.GridAltRowBack;
            grid.RowTemplate.Height = 56;
            grid.RowTemplate.Resizable = DataGridViewTriState.False;

            DefaultListPageTemplate.DisableDefaultRowHighlight(grid);
        }

        internal static HopeButton CreateCircularRefreshButton(Action onClick)
        {
            var btn = new HopeButton
            {
                Text = "⟳",
                Font = UiTheme.Fonts.RefreshIcon,
                Size = UiTheme.Sizes.RefreshButton,
                MinimumSize = UiTheme.Sizes.RefreshButton,
                MaximumSize = UiTheme.Sizes.RefreshButton,
                Margin = new Padding(0)
            };

            ConfigurePillHopeButton(btn, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            MakeCircle(btn);
            if (onClick != null)
                btn.Click += (s, e) => onClick();

            return btn;
        }
    }
}
