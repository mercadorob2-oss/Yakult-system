using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public sealed class WpfBorrowDetailsDialog : Window
    {
        private readonly BorrowLogRow _row;

        // Palette matching WinForms
        private static readonly Color PageBack = Color.FromRgb(242, 245, 249);
        private static readonly Color CardBack = Colors.White;
        private static readonly Color BorderColor = Color.FromRgb(210, 220, 230);
        private static readonly Color MutedText = Color.FromRgb(100, 116, 139);
        private static readonly Color BodyText = Color.FromRgb(30, 40, 50);
        private static readonly Color HeaderText = Color.FromRgb(15, 23, 42);
        private static readonly Color AccentTeal = Color.FromRgb(20, 150, 125);
        private static readonly Color AccentSlate = Color.FromRgb(100, 116, 139);
        private static readonly Color AccentBlue = Color.FromRgb(45, 137, 196);
        private static readonly Color SoftSurface = Color.FromRgb(248, 250, 252);
        private static readonly Color CloseBtnBack = Color.FromRgb(100, 116, 139);

        public WpfBorrowDetailsDialog(BorrowLogRow row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));

            Title = "Borrow Details";
            Width = 980;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            Background = new SolidColorBrush(PageBack);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Padding = new Thickness(0);
            ShowInTaskbar = false;

            Content = BuildRoot();
        }

        private UIElement BuildRoot()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(18)
            };

            var root = new Grid { Background = new SolidColorBrush(PageBack) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // summary
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // body
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // footer

            root.Children.Add(BuildHeader()); Grid.SetRow(root.Children[0], 0);
            root.Children.Add(BuildSummary()); Grid.SetRow(root.Children[1], 1);
            root.Children.Add(BuildBody()); Grid.SetRow(root.Children[2], 2);
            root.Children.Add(BuildFooter()); Grid.SetRow(root.Children[3], 3);

            scroll.Content = root;
            return scroll;
        }

        private UIElement BuildHeader()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left column
            var left = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

            var overline = new TextBlock
            {
                Text = "BORROW TRANSACTION",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(MutedText),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var title = new TextBlock
            {
                Text = $"BRW-{_row.BorrowId:D6}",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(HeaderText),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badge = NewStatusBadge(
                _row.IsOpen ? "OPEN" : "RETURNED",
                _row.IsOpen ? AccentTeal : AccentSlate);
            badge.Margin = new Thickness(12, 0, 0, 0);
            titleRow.Children.Add(title);
            titleRow.Children.Add(badge);

            var subtitle = new TextBlock
            {
                Text = Safe(_row.ItemDisplay),
                FontSize = 14,
                Foreground = new SolidColorBrush(BodyText),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600
            };

            left.Children.Add(overline);
            left.Children.Add(titleRow);
            left.Children.Add(subtitle);

            // Right: Serial card (Plain border, no accent)
            var serialCard = new Border
            {
                Background = new SolidColorBrush(CardBack),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Width = 200,
                Height = 84,
                Child = new StackPanel 
                { 
                    Margin = new Thickness(16, 14, 16, 12),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "SERIAL NUMBER",
                            FontSize = 11.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(MutedText),
                            Margin = new Thickness(0, 0, 0, 6)
                        },
                        new TextBlock
                        {
                            Text = Safe(_row.SerialNumber),
                            FontSize = 20,
                            FontWeight = FontWeights.Bold,
                            FontFamily = new FontFamily("Consolas"),
                            Foreground = new SolidColorBrush(HeaderText)
                        }
                    }
                }
            };

            grid.Children.Add(left);
            grid.Children.Add(serialCard); Grid.SetColumn(serialCard, 1);

            return grid;
        }

        private UIElement BuildSummary()
        {
            var metrics = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            for (int c = 0; c < 3; c++) metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 2; r++) metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tiles = new[]
            {
                NewMetricTile("Item", Safe(_row.ItemName), Safe(_row.ModelNumber, "Model not set")),
                NewMetricTile("Borrowed By", Safe(_row.BorrowedByEmpName), Safe(_row.BorrowedByDeptName)),
                NewMetricTile("Elapsed", Safe(_row.ElapsedText), _row.IsOpen ? "Still active" : "Transaction completed"),
                NewMetricTile("Borrowed At", Safe(_row.BorrowedAtLocal), $"Encoded by {Safe(_row.BorrowEncodedByUserName)}"),
                NewMetricTile("Returned At", _row.IsOpen ? "Pending return" : Safe(_row.ReturnedAtLocal), _row.IsOpen ? "Item is currently out" : Safe(_row.ReturnedByEmpName, "Returned user not set")),
                NewMetricTile("Return Handler", _row.IsOpen ? "Open transaction" : Safe(_row.ReturnEncodedByUserName), _row.IsOpen ? Safe(_row.ReturnedByDeptName, "Waiting for return") : Safe(_row.ReturnedByDeptName, "Dept not set"))
            };
            for (int i = 0; i < tiles.Length; i++)
            {
                Grid.SetRow(tiles[i], i / 3);
                Grid.SetColumn(tiles[i], i % 3);
                metrics.Children.Add(tiles[i]);
            }

            var card = NewSectionCard("Quick Overview", "Borrow summary", "A compact snapshot of the item, borrower, and transaction timing.", metrics, AccentBlue);
            card.Margin = new Thickness(0, 0, 0, 14);
            return card;
        }

        private UIElement BuildBody()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // gap
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52, GridUnitType.Star) });

            // Item Snapshot
            var itemContent = new Grid();
            itemContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            itemContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddField(itemContent, 0, "Item ID", _row.ItemId > 0 ? _row.ItemId.ToString() : "-");
            AddField(itemContent, 1, "Serial Number", Safe(_row.SerialNumber));
            AddField(itemContent, 2, "Item Name", Safe(_row.ItemName));
            AddField(itemContent, 3, "Model Number", Safe(_row.ModelNumber));
            AddField(itemContent, 4, "Description", Safe(_row.ItemDescription), true);

            var itemCard = NewSectionCard("Item Snapshot", "Hardware details", "Core identifying information for the borrowed item.", itemContent, AccentTeal);

            // Borrow Timeline
            var auditContent = new Grid();
            auditContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            auditContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int auditRow = 0;
            AddField(auditContent, auditRow++, "Borrower", Safe(_row.BorrowedByEmpName));
            AddField(auditContent, auditRow++, "Borrower Dept", Safe(_row.BorrowedByDeptName));
            AddField(auditContent, auditRow++, "Borrow Encoded By", Safe(_row.BorrowEncodedByUserName));
            AddField(auditContent, auditRow++, "Borrow Time", Safe(_row.BorrowedAtLocal));
            AddField(auditContent, auditRow++, "Returned By", Safe(_row.ReturnedByEmpName));
            AddField(auditContent, auditRow++, "Returned Dept", Safe(_row.ReturnedByDeptName));
            AddField(auditContent, auditRow++, "Return Encoded By", Safe(_row.ReturnEncodedByUserName));
            AddField(auditContent, auditRow++, "Return Time", Safe(_row.ReturnedAtLocal));

            var auditCard = NewSectionCard("Borrow Timeline", "Activity and ownership", "Who borrowed the item, who encoded it, and when it was returned.", auditContent, AccentSlate);

            grid.Children.Add(itemCard); Grid.SetColumn(itemCard, 0);
            grid.Children.Add(auditCard); Grid.SetColumn(auditCard, 2);

            return grid;
        }

        private UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var status = new TextBlock
            {
                Text = _row.IsOpen
                    ? "This item is still out and has not been returned yet."
                    : "This borrow record has been completed and is read-only.",
                FontSize = 13,
                Foreground = new SolidColorBrush(MutedText),
                VerticalAlignment = VerticalAlignment.Center
            };

            var closeBtn = new Button
            {
                Content = "Close",
                Width = 132,
                Height = 40,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(CloseBtnBack),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (_, __) => DialogResult = true;

            grid.Children.Add(status);
            grid.Children.Add(closeBtn); Grid.SetColumn(closeBtn, 1);

            return grid;
        }

        // ── Helpers ───────────────────────────────────────────────

        private static Border NewCard(Color accent, double? width = null, double? height = null)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(CardBack),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var accentBar = new Border
            {
                Background = new SolidColorBrush(accent)
            };
            Grid.SetRow(accentBar, 0);
            grid.Children.Add(accentBar);

            var contentPresenter = new ContentPresenter();
            Grid.SetRow(contentPresenter, 1);
            grid.Children.Add(contentPresenter);

            border.Child = grid;

            if (width.HasValue) border.Width = width.Value;
            if (height.HasValue) border.Height = height.Value;

            return border;
        }

        private static Border NewSectionCard(string overline, string title, string subtitle, UIElement content, Color accent)
        {
            var card = NewCard(accent);

            var stack = new StackPanel { Margin = new Thickness(18, 16, 18, 18) };

            stack.Children.Add(new TextBlock
            {
                Text = overline.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(MutedText),
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(HeaderText),
                Margin = new Thickness(0, 0, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(MutedText),
                Margin = new Thickness(0, 4, 0, 14),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(BorderColor),
                Margin = new Thickness(0, 0, 0, 14)
            });

            stack.Children.Add(content);

            if (card.Child is Grid g && g.Children.Count > 1)
            {
                var presenter = g.Children[1] as ContentPresenter;
                if (presenter != null)
                {
                    presenter.Content = stack;
                }
            }

            return card;
        }

        private static Border NewMetricTile(string title, string primary, string secondary)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(SoftSurface),
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(14, 12, 14, 12)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(MutedText),
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(new TextBlock
            {
                Text = Safe(primary),
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(HeaderText),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(new TextBlock
            {
                Text = Safe(secondary),
                FontSize = 12,
                Foreground = new SolidColorBrush(MutedText),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            border.Child = stack;
            return border;
        }

        private static Border NewStatusBadge(string text, Color backColor)
        {
            return new Border
            {
                Background = new SolidColorBrush(backColor),
                Padding = new Thickness(14, 7, 14, 7),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static void AddField(Grid grid, int row, string label, string value, bool isDescription = false)
        {
            while (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(MutedText),
                Margin = new Thickness(0, row == 0 ? 0 : 10, 14, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            UIElement valueElement;
            if (isDescription)
            {
                valueElement = new Border
                {
                    Background = new SolidColorBrush(SoftSurface),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 0),
                    Child = new TextBlock
                    {
                        Text = Safe(value),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(BodyText),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 360
                    }
                };
            }
            else
            {
                valueElement = new TextBlock
                {
                    Text = Safe(value),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(BodyText),
                    Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
            }

            Grid.SetRow(labelBlock, row); Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueElement, row); Grid.SetColumn(valueElement, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(valueElement);
        }

        private static string Safe(string value, string fallback = "-")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

