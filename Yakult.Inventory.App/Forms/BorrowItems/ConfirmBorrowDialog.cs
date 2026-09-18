using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Forms.BorrowItems
{
    public sealed class ConfirmBorrowDialog : Form
    {
        public sealed class BorrowConfirmationModel
        {
            public string Item { get; set; }
            public string SerialNumber { get; set; }
            public string BorrowedBy { get; set; }
            public string Company { get; set; }
            public string Department { get; set; }
            public string EncodedBy { get; set; }
            public string TimestampLabel { get; set; }
            public string Timestamp { get; set; }
        }

        private readonly BorrowConfirmationModel _model;

        public ConfirmBorrowDialog(BorrowConfirmationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Font;
            Font = ModernUiHelper.FontNormal;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Text = "Confirm Borrow";
            ClientSize = new Size(620, 420);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                AutoScroll = true,
                BackColor = Color.White
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                BackColor = Color.White
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var headerRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var icon = new Panel
            {
                Width = 44,
                Height = 44,
                BackColor = ModernUiHelper.ColorPrimary,
                Margin = new Padding(0, 0, 12, 0)
            };
            icon.Paint += (_, e) =>
            {
                using (var f = new Font("Segoe UI", 18F, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    var s = "i";
                    var size = e.Graphics.MeasureString(s, f);
                    e.Graphics.DrawString(s, f, b, (icon.Width - size.Width) / 2F, (icon.Height - size.Height) / 2F - 1);
                }
            };

            var headerText = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            headerText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerText.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblHeader = new Label
            {
                Text = "Log this borrow?",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblSub = new Label
            {
                Text = "Review the details below. This will create a new borrow log entry for the selected item.",
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Margin = new Padding(0, 6, 0, 0)
            };

            headerText.Controls.Add(lblHeader, 0, 0);
            headerText.Controls.Add(lblSub, 0, 1);

            headerRow.Controls.Add(icon, 0, 0);
            headerRow.Controls.Add(headerText, 1, 0);
            content.Controls.Add(headerRow);

            content.Controls.Add(ModernUiHelper.CreateSectionHeader("Summary"));

            var summaryGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 0)
            };
            summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddRow(summaryGrid, "Item", _model.Item);
            AddRow(summaryGrid, "Serial", _model.SerialNumber);
            AddRow(summaryGrid, "Borrowed By", _model.BorrowedBy);
            AddRow(summaryGrid, "Company", _model.Company);
            AddRow(summaryGrid, "Department", _model.Department);
            AddRow(summaryGrid, "Encoded By", _model.EncodedBy);
            AddRow(summaryGrid, string.IsNullOrWhiteSpace(_model.TimestampLabel) ? "Date/Time" : _model.TimestampLabel, _model.Timestamp);

            content.Controls.Add(summaryGrid);

            contentHost.Controls.Add(content);

            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ModernUiHelper.ColorBackground,
                Padding = new Padding(24, 12, 24, 12)
            };
            buttonsPanel.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220)))
                    e.Graphics.DrawLine(pen, 0, 0, buttonsPanel.Width, 0);
            };

            var btnConfirm = ModernUiHelper.CreatePrimaryButton("Confirm borrow", width: 130);
            btnConfirm.DialogResult = DialogResult.OK;

            var btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel", width: 110);
            btnCancel.DialogResult = DialogResult.Cancel;

            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };
            rightFlow.Controls.Add(btnConfirm);
            rightFlow.Controls.Add(btnCancel);
            buttonsPanel.Controls.Add(rightFlow);

            AcceptButton = btnConfirm;
            CancelButton = btnCancel;

            Controls.Add(contentHost);
            Controls.Add(buttonsPanel);

            ResumeLayout(false);
            PerformLayout();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            BorrowUiTheme.ApplyForm(this);
            ApplyThemeTree(this, false);

            var buttonCount = AcceptButton as Button;
            if (buttonCount != null)
                BorrowUiTheme.ApplyButton(buttonCount, BorrowButtonKind.Primary);

            var cancelButton = CancelButton as Button;
            if (cancelButton != null)
                BorrowUiTheme.ApplyButton(cancelButton, BorrowButtonKind.Secondary);
        }

        private void ApplyThemeTree(Control root, bool surface)
        {
            if (root == null)
                return;

            var palette = BorrowUiTheme.Current;

            foreach (Control child in root.Controls)
            {
                if (child is Button)
                    continue;

                if (child is Label)
                {
                    var label = (Label)child;
                    var secondary = !label.Font.Bold && label.Font.Size <= 10F;
                    BorrowUiTheme.ApplyLabel(label, secondary);
                    continue;
                }

                if (child is Panel || child is TableLayoutPanel || child is FlowLayoutPanel)
                {
                    var nextSurface = surface
                        || child.BackColor == Color.White;
                    child.BackColor = nextSurface ? palette.SurfaceBack : palette.PageBack;
                    child.ForeColor = palette.TextPrimary;
                    ApplyThemeTree(child, nextSurface);
                    continue;
                }

                child.BackColor = surface ? palette.SurfaceBack : palette.PageBack;
                child.ForeColor = palette.TextPrimary;
                ApplyThemeTree(child, surface);
            }
        }

        private static void AddRow(TableLayoutPanel grid, string label, string value)
        {
            AddRow(grid, label, new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextPrimary,
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim()
            });
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control valueControl)
        {
            if (grid == null)
                return;

            var rowIndex = grid.RowCount;
            grid.RowCount = rowIndex + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = label ?? string.Empty,
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Margin = new Padding(0, 8, 10, 0)
            };

            valueControl.Margin = new Padding(0, 8, 0, 0);
            valueControl.Dock = DockStyle.Fill;

            grid.Controls.Add(lbl, 0, rowIndex);
            grid.Controls.Add(valueControl, 1, rowIndex);
        }
    }
}

