using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Forms.BorrowItems
{
    public sealed class BorrowDetailsDialog : Form
    {
        private static BorrowThemePalette Palette { get { return BorrowUiTheme.Current; } }
        private static Color PageBackColor { get { return Palette.PageBack; } }
        private static Color CardBackColor { get { return Palette.SurfaceBack; } }
        private static Color CardBorderColor { get { return Palette.Border; } }
        private static Color HeaderInkColor { get { return Palette.TextPrimary; } }
        private static Color BodyInkColor { get { return Palette.TextPrimary; } }
        private static Color MutedInkColor { get { return Palette.TextSecondary; } }
        private static Color AccentBlue { get { return Palette.PrimaryButton; } }
        private static Color AccentTeal { get { return Palette.SuccessButton; } }
        private static Color AccentSlate { get { return Palette.SecondaryButton; } }
        private static Color SoftSurface { get { return Palette.SurfaceAltBack; } }

        private readonly BorrowLogRow _row;

        public BorrowDetailsDialog(BorrowLogRow row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));

            BuildUi();
            BindData();
        }

        private void BuildUi()
        {
            Font = new Font("Segoe UI", 9.5F);
            Text = "Borrow Details";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = PageBackColor;
            ClientSize = new Size(980, 660);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(18),
                BackColor = PageBackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildSummary(), 0, 1);
            root.Controls.Add(BuildBody(), 0, 2);
            root.Controls.Add(BuildFooter(), 0, 3);

            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                Margin = new Padding(0)
            };
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var overline = new Label
            {
                Text = "BORROW TRANSACTION",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 0, 0, 6)
            };

            var titleRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            var title = new Label
            {
                Text = $"BRW-{_row.BorrowId:D6}",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 23F, FontStyle.Bold),
                ForeColor = HeaderInkColor,
                Margin = new Padding(0, 0, 12, 0)
            };

            titleRow.Controls.Add(title);
            titleRow.Controls.Add(NewStatusBadge(_row.IsOpen ? "OPEN" : "RETURNED", _row.IsOpen ? AccentTeal : AccentSlate));

            var subtitle = new Label
            {
                Text = Safe(_row.ItemDisplay),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = BodyInkColor,
                Margin = new Padding(0, 8, 0, 0)
            };

            left.Controls.Add(overline, 0, 0);
            left.Controls.Add(titleRow, 0, 1);
            left.Controls.Add(subtitle, 0, 2);

            var serialCard = new AccentCardPanel
            {
                AccentColor = AccentBlue,
                BorderColor = CardBorderColor,
                BackColor = CardBackColor,
                Width = 200,
                Height = 84,
                Margin = new Padding(16, 0, 0, 0)
            };

            var serialWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(16, 14, 16, 12),
                Margin = new Padding(0)
            };
            serialWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            serialWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            serialWrap.Controls.Add(new Label
            {
                Text = "SERIAL NUMBER",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            serialWrap.Controls.Add(new Label
            {
                Text = Safe(_row.SerialNumber),
                AutoSize = true,
                Font = new Font("Consolas", 16F, FontStyle.Bold),
                ForeColor = HeaderInkColor,
                Margin = new Padding(0)
            }, 0, 1);

            serialCard.Controls.Add(serialWrap);

            panel.Controls.Add(left, 0, 0);
            panel.Controls.Add(serialCard, 1, 0);
            return panel;
        }

        private Control BuildSummary()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            grid.Controls.Add(NewMetricTile("Item", Safe(_row.ItemName), Safe(_row.ModelNumber, "Model not set")), 0, 0);
            grid.Controls.Add(NewMetricTile("Borrowed By", Safe(_row.BorrowedByEmpName), Safe(_row.BorrowedByDeptName)), 1, 0);
            grid.Controls.Add(NewMetricTile("Elapsed", Safe(_row.ElapsedText), _row.IsOpen ? "Still active" : "Transaction completed"), 2, 0);
            grid.Controls.Add(NewMetricTile("Borrowed At", Safe(_row.BorrowedAtLocal), $"Encoded by {Safe(_row.BorrowEncodedByUserName)}"), 0, 1);
            grid.Controls.Add(NewMetricTile("Returned At", _row.IsOpen ? "Pending return" : Safe(_row.ReturnedAtLocal), _row.IsOpen ? "Item is currently out" : Safe(_row.ReturnedByEmpName, "Returned user not set")), 1, 1);
            grid.Controls.Add(NewMetricTile("Return Handler", _row.IsOpen ? "Open transaction" : Safe(_row.ReturnEncodedByUserName), _row.IsOpen ? Safe(_row.ReturnedByDeptName, "Waiting for return") : Safe(_row.ReturnedByDeptName, "Dept not set")), 2, 1);

            var card = BuildSectionCard(
                "Quick Overview",
                "Borrow summary",
                "A compact snapshot of the item, borrower, and transaction timing.",
                grid,
                AccentBlue,
                188);
            card.Margin = new Padding(0, 0, 0, 14);
            return card;
        }

        private Control BuildBody()
        {
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));

            var itemContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Margin = new Padding(0)
            };
            itemContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            itemContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddField(itemContent, 0, "Item ID", _row.ItemId > 0 ? _row.ItemId.ToString() : "-");
            AddField(itemContent, 1, "Serial Number", _row.SerialNumber);
            AddField(itemContent, 2, "Item Name", _row.ItemName);
            AddField(itemContent, 3, "Model Number", _row.ModelNumber);
            AddField(itemContent, 4, "Description", CreateDescriptionView(_row.ItemDescription));

            var auditContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AutoSize = true,
                Margin = new Padding(0)
            };
            auditContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            auditContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddField(auditContent, 0, "Borrower", _row.BorrowedByEmpName);
            AddField(auditContent, 1, "Borrower Dept", _row.BorrowedByDeptName);
            AddField(auditContent, 2, "Borrow Encoded By", _row.BorrowEncodedByUserName);
            AddField(auditContent, 3, "Borrow Time", _row.BorrowedAtLocal);
            AddField(auditContent, 4, "Returned By", _row.ReturnedByEmpName);
            AddField(auditContent, 5, "Returned Dept", _row.ReturnedByDeptName);
            AddField(auditContent, 6, "Return Encoded By", _row.ReturnEncodedByUserName);
            AddField(auditContent, 7, "Return Time", _row.ReturnedAtLocal);

            var itemCard = BuildSectionCard(
                "Item Snapshot",
                "Hardware details",
                "Core identifying information for the borrowed item.",
                itemContent,
                AccentTeal);
            itemCard.Margin = new Padding(0, 0, 10, 0);

            var auditCard = BuildSectionCard(
                "Borrow Timeline",
                "Activity and ownership",
                "Who borrowed the item, who encoded it, and when it was returned.",
                auditContent,
                AccentSlate);
            auditCard.Margin = new Padding(10, 0, 0, 0);

            body.Controls.Add(itemCard, 0, 0);
            body.Controls.Add(auditCard, 1, 0);
            return body;
        }

        private Control BuildFooter()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Margin = new Padding(0, 14, 0, 0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            panel.Controls.Add(new Label
            {
                Text = _row.IsOpen
                    ? "This item is still out and has not been returned yet."
                    : "This borrow record has been completed and is read-only.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.25F),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 10, 0, 0)
            }, 0, 0);

            var btnClose = new Button
            {
                Text = "Close",
                Width = 132,
                Height = 40,
                DialogResult = DialogResult.OK,
                BackColor = AccentSlate,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0)
            };
            btnClose.FlatAppearance.BorderSize = 0;

            panel.Controls.Add(btnClose, 1, 0);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            return panel;
        }

        private void BindData()
        {
        }

        private static Control BuildSectionCard(string overline, string title, string subtitle, Control content, Color accentColor, int? fixedHeight = null)
        {
            var card = new AccentCardPanel
            {
                AccentColor = accentColor,
                BorderColor = CardBorderColor,
                BackColor = CardBackColor,
                Dock = DockStyle.Fill
            };

            if (fixedHeight.HasValue)
                card.Height = fixedHeight.Value;

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18, 16, 18, 18),
                Margin = new Padding(0)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = overline.ToUpperInvariant(),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
                ForeColor = HeaderInkColor,
                Margin = new Padding(0)
            }, 0, 1);

            header.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.25F),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 4, 0, 0)
            }, 0, 2);

            shell.Controls.Add(header, 0, 0);
            shell.Controls.Add(NewDivider(), 0, 1);

            content.Dock = DockStyle.Fill;
            shell.Controls.Add(content, 0, 2);

            card.Controls.Add(shell);
            return card;
        }

        private static Control NewMetricTile(string title, string primary, string secondary)
        {
            var tile = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SoftSurface,
                Margin = new Padding(0, 0, 12, 12),
                Padding = new Padding(14, 12, 14, 12)
            };

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            stack.Controls.Add(new Label
            {
                Text = title.ToUpperInvariant(),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.3F, FontStyle.Bold),
                ForeColor = MutedInkColor,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            stack.Controls.Add(new Label
            {
                Text = Safe(primary),
                AutoSize = true,
                MaximumSize = new Size(260, 0),
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = HeaderInkColor,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 1);

            stack.Controls.Add(new Label
            {
                Text = Safe(secondary),
                AutoSize = true,
                MaximumSize = new Size(260, 0),
                Font = new Font("Segoe UI", 9F),
                ForeColor = MutedInkColor,
                Margin = new Padding(0)
            }, 0, 2);

            tile.Controls.Add(stack);
            return tile;
        }

        private static Control NewStatusBadge(string text, Color backColor)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                Padding = new Padding(14, 7, 14, 7),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = backColor,
                Margin = new Padding(0, 6, 0, 0)
            };
        }

        private static Control CreateDescriptionView(string value)
        {
            var shell = new TableLayoutPanel
            {
                BackColor = SoftSurface,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(12)
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var valueLabel = new Label
            {
                Text = Safe(value),
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                BackColor = SoftSurface,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10F),
                ForeColor = BodyInkColor,
                Margin = new Padding(0)
            };

            shell.Controls.Add(valueLabel, 0, 0);
            return shell;
        }

        private static Control NewDivider()
        {
            return new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = CardBorderColor,
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private static void AddField(TableLayoutPanel grid, int rowIndex, string label, string value)
        {
            AddField(grid, rowIndex, label, NewValueLabel(value));
        }

        private static void AddField(TableLayoutPanel grid, int rowIndex, string label, Control valueControl)
        {
            while (grid.RowStyles.Count <= rowIndex)
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var labelControl = new Label
            {
                Text = label.ToUpperInvariant(),
                AutoSize = true,
                ForeColor = MutedInkColor,
                Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
                Margin = new Padding(0, rowIndex == 0 ? 0 : 10, 14, 0)
            };

            valueControl.Margin = new Padding(0, rowIndex == 0 ? 0 : 10, 0, 0);

            grid.Controls.Add(labelControl, 0, rowIndex);
            grid.Controls.Add(valueControl, 1, rowIndex);
        }

        private static Control NewValueLabel(string value)
        {
            return new Label
            {
                Text = Safe(value),
                AutoSize = true,
                MaximumSize = new Size(380, 0),
                ForeColor = BodyInkColor,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0)
            };
        }

        private static string Safe(string value, string fallback = "-")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class AccentCardPanel : Panel
        {
            public Color AccentColor { get; set; } = AccentBlue;
            public Color BorderColor { get; set; } = CardBorderColor;

            public AccentCardPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                Padding = new Padding(1);
                Margin = new Padding(0);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                var rect = ClientRectangle;
                if (rect.Width <= 1 || rect.Height <= 1)
                    return;

                rect.Width -= 1;
                rect.Height -= 1;

                using (var borderPen = new Pen(BorderColor))
                using (var accentBrush = new SolidBrush(AccentColor))
                {
                    g.DrawRectangle(borderPen, rect);
                    g.FillRectangle(accentBrush, rect.Left, rect.Top, rect.Width + 1, 4);
                }
            }
        }
    }
}
