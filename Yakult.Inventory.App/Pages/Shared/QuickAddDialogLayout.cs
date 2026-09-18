using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Shared
{
    /// <summary>
    /// Shared layout for the small "quick add" dialogs (category, vendor, cartridge model,
    /// consumable model).
    ///
    /// Each of them was originally hand-positioned with absolute Left/Top/Width pixels and a fixed
    /// form Size. That only lines up at 100% display scaling — at 125% or 150% the fonts grow but
    /// the coordinates do not, so the lower rows and the buttons fall outside the form and get
    /// clipped. Building them from an auto-sizing TableLayoutPanel instead means the form measures
    /// itself from its contents, at any scaling and any font.
    ///
    /// Usage: create the buttons, call <see cref="BuildShell"/>, then add label/control rows to the
    /// returned panel with <see cref="AddRow"/> or <see cref="AddFullWidth"/>.
    /// </summary>
    internal static class QuickAddDialogLayout
    {
        /// <summary>Default width of the input column, in device-independent pixels.</summary>
        private const int DefaultFieldWidth = 300;

        /// <summary>
        /// Applies the standard chrome to <paramref name="form"/> and returns the two-column panel
        /// that rows should be added to. The form sizes itself to whatever ends up in that panel.
        /// </summary>
        public static TableLayoutPanel BuildShell(Form form, Button save, Button cancel, int fieldWidth = DefaultFieldWidth)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            // Font first: AutoScaleMode.Font measures against the font in effect, so setting it
            // afterwards would scale everything a second time.
            form.Font = new Font("Segoe UI", 9F);
            form.AutoScaleMode = AutoScaleMode.Font;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterParent;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.BackColor = SystemColors.Window;

            // GrowAndShrink lets the form shrink-wrap its contents; without it the form keeps
            // whatever size it was constructed with and can still clip.
            form.AutoSize = true;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(18, 16, 18, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // fields
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // buttons

            var fields = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));                 // labels
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, fieldWidth));     // inputs

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 14, 0, 0)
            };

            // RightToLeft flow lays these out Save-then-Cancel from the right edge.
            StyleButton(save, primary: true);
            StyleButton(cancel, primary: false);
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(buttons, 0, 1);
            form.Controls.Add(root);

            form.AcceptButton = save;
            form.CancelButton = cancel;

            return fields;
        }

        private static void StyleButton(Button button, bool primary)
        {
            if (button == null) return;

            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            // A minimum keeps Save and Cancel the same width without pinning either to a pixel
            // size that a larger font would overflow.
            button.MinimumSize = new Size(88, 30);
            button.Padding = new Padding(10, 4, 10, 4);
            button.Margin = new Padding(6, 0, 0, 0);
            button.UseVisualStyleBackColor = true;

            if (primary)
            {
                button.BackColor = Color.FromArgb(58, 142, 246);
                button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.UseVisualStyleBackColor = false;
            }
        }

        /// <summary>Adds a "label : control" row.</summary>
        public static void AddRow(TableLayoutPanel fields, string label, Control input)
        {
            var caption = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 14, 8),
                ForeColor = Color.FromArgb(55, 65, 81)
            };

            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Margin = new Padding(0, 5, 0, 5);

            int row = fields.RowCount;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(caption, 0, row);
            fields.Controls.Add(input, 1, row);
            fields.RowCount = row + 1;
        }

        /// <summary>Adds a control spanning both columns — checkboxes, role rows, notes.</summary>
        public static void AddFullWidth(TableLayoutPanel fields, Control control, int topMargin = 4)
        {
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(0, topMargin, 0, 4);

            int row = fields.RowCount;
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(control, 0, row);
            fields.SetColumnSpan(control, 2);
            fields.RowCount = row + 1;
        }

        /// <summary>
        /// A row of checkboxes laid out left to right, wrapping if the font is large enough to
        /// need it — the fixed Left coordinates these replace were the main clipping culprit.
        /// </summary>
        public static FlowLayoutPanel CheckBoxRow(params CheckBox[] boxes)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0)
            };

            foreach (var box in boxes)
            {
                box.AutoSize = true;
                box.Margin = new Padding(0, 3, 18, 3);
                row.Controls.Add(box);
            }

            return row;
        }
    }
}
