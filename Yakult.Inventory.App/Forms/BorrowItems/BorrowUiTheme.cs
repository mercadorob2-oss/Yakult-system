using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.BorrowItems
{
    internal enum BorrowButtonKind
    {
        Primary,
        Secondary,
        Success,
        Info
    }

    internal sealed class BorrowThemePalette
    {
        public Color PageBack { get; set; }
        public Color HeaderBack { get; set; }
        public Color HeaderText { get; set; }
        public Color HeaderSubText { get; set; }
        public Color SurfaceBack { get; set; }
        public Color SurfaceAltBack { get; set; }
        public Color Border { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color InputBack { get; set; }
        public Color InputFore { get; set; }
        public Color InputBorder { get; set; }
        public Color GridBack { get; set; }
        public Color GridFore { get; set; }
        public Color GridHeaderBack { get; set; }
        public Color GridHeaderFore { get; set; }
        public Color GridSelectionBack { get; set; }
        public Color GridSelectionFore { get; set; }
        public Color StatusFore { get; set; }
        public Color EmptyFore { get; set; }
        public Color TabActiveBack { get; set; }
        public Color TabInactiveBack { get; set; }
        public Color TabActiveFore { get; set; }
        public Color TabInactiveFore { get; set; }
        public Color TabBorder { get; set; }
        public Color PrimaryButton { get; set; }
        public Color SecondaryButton { get; set; }
        public Color SuccessButton { get; set; }
        public Color InfoButton { get; set; }
        public Color ToggleButton { get; set; }
        public Color KpiOpen { get; set; }
        public Color KpiOldest { get; set; }
    }

    internal static class BorrowUiTheme
    {
        private const string ComboHostTag = "BorrowComboHost";

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        private static readonly BorrowThemePalette LightPalette = new BorrowThemePalette
        {
            PageBack = Color.FromArgb(248, 249, 251),
            HeaderBack = Color.White,
            HeaderText = Color.FromArgb(32, 38, 53),
            HeaderSubText = Color.FromArgb(103, 112, 129),
            SurfaceBack = Color.White,
            SurfaceAltBack = Color.FromArgb(245, 246, 248),
            Border = Color.FromArgb(229, 230, 232),
            TextPrimary = Color.FromArgb(32, 38, 53),
            TextSecondary = Color.FromArgb(103, 112, 129),
            InputBack = Color.White,
            InputFore = Color.FromArgb(32, 38, 53),
            InputBorder = Color.FromArgb(229, 230, 232),
            GridBack = Color.White,
            GridFore = Color.FromArgb(32, 38, 53),
            GridHeaderBack = Color.FromArgb(248, 249, 251),
            GridHeaderFore = Color.FromArgb(103, 112, 129),
            GridSelectionBack = Color.FromArgb(252, 240, 241),
            GridSelectionFore = Color.FromArgb(32, 38, 53),
            StatusFore = Color.FromArgb(103, 112, 129),
            EmptyFore = Color.FromArgb(127, 134, 147),
            TabActiveBack = Color.White,
            TabInactiveBack = Color.White,
            TabActiveFore = Color.FromArgb(185, 35, 53),
            TabInactiveFore = Color.FromArgb(103, 112, 129),
            TabBorder = Color.FromArgb(229, 230, 232),
            PrimaryButton = Color.FromArgb(252, 240, 241),
            SecondaryButton = Color.FromArgb(255, 255, 255),
            SuccessButton = Color.FromArgb(252, 240, 241),
            InfoButton = Color.FromArgb(252, 240, 241),
            ToggleButton = Color.FromArgb(245, 246, 248),
            KpiOpen = Color.FromArgb(185, 35, 53),
            KpiOldest = Color.FromArgb(32, 38, 53)
        };

        private static readonly BorrowThemePalette DarkPalette = new BorrowThemePalette
        {
            PageBack = Color.FromArgb(23, 27, 34),
            HeaderBack = Color.FromArgb(15, 18, 25),
            HeaderText = Color.FromArgb(247, 249, 252),
            HeaderSubText = Color.FromArgb(148, 163, 184),
            SurfaceBack = Color.FromArgb(31, 37, 48),
            SurfaceAltBack = Color.FromArgb(22, 28, 38),
            Border = Color.FromArgb(39, 47, 59),
            TextPrimary = Color.FromArgb(220, 227, 236),
            TextSecondary = Color.FromArgb(143, 154, 173),
            InputBack = Color.FromArgb(20, 26, 36),
            InputFore = Color.FromArgb(241, 245, 249),
            InputBorder = Color.FromArgb(56, 67, 82),
            GridBack = Color.FromArgb(31, 37, 48),
            GridFore = Color.FromArgb(220, 227, 236),
            GridHeaderBack = Color.FromArgb(24, 30, 40),
            GridHeaderFore = Color.FromArgb(160, 174, 192),
            GridSelectionBack = Color.FromArgb(39, 73, 124),
            GridSelectionFore = Color.FromArgb(248, 250, 252),
            StatusFore = Color.FromArgb(143, 154, 173),
            EmptyFore = Color.FromArgb(143, 154, 173),
            TabActiveBack = Color.FromArgb(31, 37, 48),
            TabInactiveBack = Color.FromArgb(22, 28, 38),
            TabActiveFore = Color.FromArgb(244, 247, 251),
            TabInactiveFore = Color.FromArgb(143, 154, 173),
            TabBorder = Color.FromArgb(42, 50, 63),
            PrimaryButton = Color.FromArgb(53, 96, 201),
            SecondaryButton = Color.FromArgb(79, 92, 114),
            SuccessButton = Color.FromArgb(20, 138, 110),
            InfoButton = Color.FromArgb(48, 120, 180),
            ToggleButton = Color.FromArgb(72, 84, 103),
            KpiOpen = Color.FromArgb(47, 76, 168),
            KpiOldest = Color.FromArgb(68, 84, 108)
        };

        public static bool IsDarkMode { get; private set; }

        public static BorrowThemePalette Current
        {
            get { return IsDarkMode ? DarkPalette : LightPalette; }
        }

        public static event EventHandler ThemeChanged;

        public static void Toggle()
        {
            IsDarkMode = !IsDarkMode;
            var handler = ThemeChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        public static void ApplyForm(Form form)
        {
            if (form == null)
                return;

            form.BackColor = Current.PageBack;
            form.ForeColor = Current.TextPrimary;
        }

        public static void ApplyButton(Button button, BorrowButtonKind kind)
        {
            if (button == null)
                return;

            var palette = Current;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = IsDarkMode ? 0 : 1;
            button.UseVisualStyleBackColor = false;

            switch (kind)
            {
                case BorrowButtonKind.Primary:
                    button.BackColor = palette.PrimaryButton;
                    button.ForeColor = IsDarkMode ? Color.White : Color.FromArgb(185, 35, 53);
                    button.FlatAppearance.BorderColor = IsDarkMode ? palette.PrimaryButton : Color.FromArgb(231, 184, 193);
                    break;
                case BorrowButtonKind.Success:
                    button.BackColor = palette.SuccessButton;
                    button.ForeColor = IsDarkMode ? Color.White : Color.FromArgb(185, 35, 53);
                    button.FlatAppearance.BorderColor = IsDarkMode ? palette.SuccessButton : Color.FromArgb(231, 184, 193);
                    break;
                case BorrowButtonKind.Info:
                    button.BackColor = palette.InfoButton;
                    button.ForeColor = IsDarkMode ? Color.White : Color.FromArgb(185, 35, 53);
                    button.FlatAppearance.BorderColor = IsDarkMode ? palette.InfoButton : Color.FromArgb(231, 184, 193);
                    break;
                default:
                    button.BackColor = palette.SecondaryButton;
                    button.ForeColor = IsDarkMode ? Color.White : palette.TextPrimary;
                    button.FlatAppearance.BorderColor = IsDarkMode ? palette.SecondaryButton : palette.Border;
                    break;
            }

            button.FlatAppearance.MouseOverBackColor = Blend(button.BackColor, Color.White, IsDarkMode ? 0.10 : 0.12);
            button.FlatAppearance.MouseDownBackColor = Blend(button.BackColor, Color.Black, IsDarkMode ? 0.16 : 0.12);
        }

        public static void ApplyToggleButton(Button button)
        {
            if (button == null)
                return;

            var palette = Current;
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = IsDarkMode ? palette.TextPrimary : palette.TextSecondary;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = IsDarkMode ? Blend(palette.ToggleButton, Color.Black, 0.20) : palette.Border;
            button.FlatAppearance.MouseOverBackColor = Blend(palette.ToggleButton, Color.White, IsDarkMode ? 0.08 : 0.10);
            button.FlatAppearance.MouseDownBackColor = Blend(palette.ToggleButton, Color.Black, IsDarkMode ? 0.15 : 0.10);
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.ToggleButton;
        }

        public static void ApplyTextBox(TextBox textBox, bool borderless)
        {
            if (textBox == null)
                return;

            var palette = Current;
            textBox.BackColor = palette.InputBack;
            textBox.ForeColor = palette.InputFore;
            if (!borderless)
                textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        public static void ApplyComboBox(ComboBox comboBox)
        {
            if (comboBox == null)
                return;

            var palette = Current;
            comboBox.BackColor = palette.InputBack;
            comboBox.ForeColor = palette.InputFore;
            comboBox.FlatStyle = FlatStyle.Flat;
        }

        public static Panel CreateComboHost(ComboBox comboBox)
        {
            if (comboBox == null)
                return null;

            var host = new Panel
            {
                Tag = ComboHostTag,
                Padding = new Padding(1),
                Margin = comboBox.Margin,
                Dock = comboBox.Dock,
                Width = comboBox.Width > 0 ? comboBox.Width + 2 : 0,
                Height = Math.Max(comboBox.Height, comboBox.PreferredHeight) + 2
            };

            comboBox.Margin = new Padding(0);
            comboBox.Dock = DockStyle.Fill;
            host.Controls.Add(comboBox);
            ApplyComboHost(host);
            return host;
        }

        public static bool IsComboHost(Control control)
        {
            return control is Panel && string.Equals(control.Tag as string, ComboHostTag, StringComparison.Ordinal);
        }

        public static void ApplyComboHost(Panel host)
        {
            if (host == null)
                return;

            host.BackColor = Current.InputBorder;
            host.ForeColor = Current.TextPrimary;

            foreach (Control child in host.Controls)
            {
                var comboBox = child as ComboBox;
                if (comboBox != null)
                    ApplyComboBox(comboBox);
            }
        }

        public static void ApplyLabel(Label label, bool secondary)
        {
            if (label == null)
                return;

            label.ForeColor = secondary ? Current.TextSecondary : Current.TextPrimary;
            if (label.Parent != null && (label.BackColor == Color.Empty || label.BackColor == SystemColors.Control))
                label.BackColor = label.Parent.BackColor;
        }

        public static void ApplySurface(Control control)
        {
            if (control == null)
                return;

            control.BackColor = Current.SurfaceBack;
            control.ForeColor = Current.TextPrimary;
        }

        public static void ApplyAltSurface(Control control)
        {
            if (control == null)
                return;

            control.BackColor = Current.SurfaceAltBack;
            control.ForeColor = Current.TextPrimary;
        }

        public static void ApplyPage(Control control)
        {
            if (control == null)
                return;

            control.BackColor = Current.PageBack;
            control.ForeColor = Current.TextPrimary;
        }

        public static void ApplyGroupBox(GroupBox groupBox)
        {
            if (groupBox == null)
                return;

            groupBox.BackColor = Current.SurfaceBack;
            groupBox.ForeColor = Current.TextPrimary;
            groupBox.FlatStyle = FlatStyle.Flat;
        }

        public static void ApplyGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            var palette = Current;
            grid.BackgroundColor = palette.GridBack;
            grid.GridColor = IsDarkMode ? Blend(palette.GridBack, palette.Border, 0.70) : palette.Border;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.AdvancedCellBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedCellBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedCellBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedCellBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
            grid.AdvancedColumnHeadersBorderStyle.Left = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedColumnHeadersBorderStyle.Right = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedColumnHeadersBorderStyle.Top = DataGridViewAdvancedCellBorderStyle.None;
            grid.AdvancedColumnHeadersBorderStyle.Bottom = DataGridViewAdvancedCellBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = palette.GridHeaderBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.GridHeaderFore;
            grid.DefaultCellStyle.BackColor = palette.GridBack;
            grid.DefaultCellStyle.ForeColor = palette.GridFore;
            grid.DefaultCellStyle.SelectionBackColor = palette.GridSelectionBack;
            grid.DefaultCellStyle.SelectionForeColor = palette.GridSelectionFore;
            grid.RowsDefaultCellStyle.BackColor = palette.GridBack;
            grid.RowsDefaultCellStyle.ForeColor = palette.GridFore;
            grid.RowsDefaultCellStyle.SelectionBackColor = palette.GridSelectionBack;
            grid.RowsDefaultCellStyle.SelectionForeColor = palette.GridSelectionFore;
            grid.AlternatingRowsDefaultCellStyle.BackColor = palette.SurfaceAltBack;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = palette.GridFore;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = palette.GridSelectionBack;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = palette.GridSelectionFore;
        }

        public static void ApplyTabControl(TabControl tabControl)
        {
            if (tabControl == null)
                return;

            tabControl.HandleCreated -= HandleTabControlHandleCreated;
            tabControl.HandleCreated += HandleTabControlHandleCreated;

            tabControl.BackColor = Current.SurfaceBack;
            tabControl.Appearance = TabAppearance.Normal;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.ItemSize = new Size(150, 34);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.DrawItem -= DrawTab;
            tabControl.DrawItem += DrawTab;
            tabControl.Paint -= PaintTabControl;
            tabControl.Paint += PaintTabControl;

            foreach (TabPage page in tabControl.TabPages)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Current.SurfaceBack;
                page.ForeColor = Current.TextPrimary;
                page.Padding = new Padding(0);
                page.Margin = new Padding(0);
            }

            ApplyTabControlWindowTheme(tabControl);
            tabControl.Invalidate();
        }

        public static void ApplyCardPanel(Panel panel)
        {
            if (panel == null)
                return;

            panel.BackColor = Current.SurfaceBack;
        }

        private static void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            var palette = Current;
            var rect = e.Bounds;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var backColor = selected ? palette.TabActiveBack : palette.TabInactiveBack;
            var foreColor = selected ? palette.TabActiveFore : palette.TabInactiveFore;

            using (var back = new SolidBrush(backColor))
            using (var border = new Pen(IsDarkMode ? Blend(palette.SurfaceBack, palette.TabBorder, 0.65) : palette.TabBorder))
            using (var accent = new SolidBrush(IsDarkMode ? palette.PrimaryButton : Color.FromArgb(185, 35, 53)))
            {
                e.Graphics.FillRectangle(back, rect);
                if (selected)
                {
                    e.Graphics.FillRectangle(accent, rect.X + 1, rect.Y + 1, rect.Width - 2, 3);
                    using (var edge = new SolidBrush(backColor))
                        e.Graphics.FillRectangle(edge, rect.X + 1, rect.Bottom - 1, rect.Width - 2, 2);
                }
                else
                {
                    e.Graphics.DrawLine(border, rect.Right - 1, rect.Top + 6, rect.Right - 1, rect.Bottom - 6);
                }

                e.Graphics.DrawLine(border, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);

                TextRenderer.DrawText(
                    e.Graphics,
                    tabControl.TabPages[e.Index].Text,
                    new Font("Segoe UI", 9.5F, selected ? FontStyle.Bold : FontStyle.Regular),
                    rect,
                    foreColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static void PaintTabControl(object sender, PaintEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null)
                return;

            var palette = Current;
            var display = tabControl.DisplayRectangle;
            var full = tabControl.ClientRectangle;
            var edgeColor = IsDarkMode ? palette.SurfaceBack : palette.TabBorder;
            var frameColor = IsDarkMode ? Blend(palette.SurfaceBack, palette.TabBorder, 0.60) : palette.TabBorder;

            using (var stripBrush = new SolidBrush(palette.SurfaceBack))
            using (var pageBrush = new SolidBrush(palette.SurfaceBack))
            using (var edgeBrush = new SolidBrush(edgeColor))
            using (var borderPen = new Pen(frameColor))
            {
                var stripRect = new Rectangle(full.Left, full.Top, full.Width, Math.Max(0, display.Top));
                if (stripRect.Height > 0)
                    e.Graphics.FillRectangle(stripBrush, stripRect);

                e.Graphics.FillRectangle(edgeBrush, display.Left - 1, display.Top - 1, display.Width + 2, 2);
                e.Graphics.FillRectangle(edgeBrush, display.Left - 1, display.Top - 1, 2, display.Height + 2);
                e.Graphics.FillRectangle(edgeBrush, display.Right - 1, display.Top - 1, 2, display.Height + 2);
                e.Graphics.FillRectangle(edgeBrush, display.Left - 1, display.Bottom - 1, display.Width + 2, 2);
                e.Graphics.FillRectangle(pageBrush, display);
                e.Graphics.DrawLine(borderPen, display.Left, display.Top - 1, display.Right - 1, display.Top - 1);
            }
        }

        private static void HandleTabControlHandleCreated(object sender, EventArgs e)
        {
            ApplyTabControlWindowTheme(sender as TabControl);
        }

        private static void ApplyTabControlWindowTheme(TabControl tabControl)
        {
            if (tabControl == null || !tabControl.IsHandleCreated)
                return;

            try
            {
                SetWindowTheme(tabControl.Handle, string.Empty, string.Empty);
            }
            catch
            {
            }
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            var ratio = Math.Max(0D, Math.Min(1D, amount));
            var r = (int)Math.Round(source.R + ((target.R - source.R) * ratio));
            var g = (int)Math.Round(source.G + ((target.G - source.G) * ratio));
            var b = (int)Math.Round(source.B + ((target.B - source.B) * ratio));
            return Color.FromArgb(r, g, b);
        }
    }
}
