using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// Correction popup for the invoice CSV import. When a row's Category doesn't match any
    /// active item category (dbo.Item.CategoryId is NOT NULL), this dialog lists each
    /// unrecognized/blank value and lets the user map it onto a valid category — so the import
    /// continues instead of dead-ending on a SQL NULL error. Built entirely in code (no XAML)
    /// to avoid a separate Page/BAML registration.
    /// </summary>
    internal sealed class CategoryFixDialog : Window
    {
        private readonly Dictionary<string, ComboBox> _combos =
            new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);

        // Invalid value (trimmed; "" for blank) -> chosen valid category name.
        public Dictionary<string, string> Mapping { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CategoryFixDialog(IEnumerable<string> invalidValues, IReadOnlyList<string> validCategories)
        {
            Title = "Fix Item Categories";
            Width = 580;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF9, 0xFC));

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var intro = new TextBlock
            {
                Text = "Some rows have a Category that doesn't match your item categories. "
                     + "Pick a valid category for each value below — every row that uses it will be updated. "
                     + "Matching is not case-sensitive (mouse = Mouse = MOUSE).",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
                Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51))
            };
            Grid.SetRow(intro, 0);
            root.Children.Add(intro);

            var list = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 360,
                Content = list
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            foreach (var raw in invalidValues)
            {
                var val = raw ?? string.Empty;
                var rowPanel = new DockPanel { Margin = new Thickness(0, 5, 0, 5) };

                var label = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(val) ? "(blank)" : val,
                    Width = 220,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
                    ToolTip = string.IsNullOrWhiteSpace(val) ? "Rows with no Category" : val
                };
                DockPanel.SetDock(label, Dock.Left);

                var arrow = new TextBlock
                {
                    Text = "  →   ",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x5B, 0x6A, 0x7D))
                };
                DockPanel.SetDock(arrow, Dock.Left);

                var combo = new ComboBox { Height = 28, VerticalContentAlignment = VerticalAlignment.Center };
                foreach (var c in validCategories) combo.Items.Add(c);
                if (combo.Items.Count > 0) combo.SelectedIndex = 0;

                rowPanel.Children.Add(label);
                rowPanel.Children.Add(arrow);
                rowPanel.Children.Add(combo);   // fills remaining width
                list.Children.Add(rowPanel);

                _combos[val] = combo;
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var apply = new Button
            {
                Content = "Apply & Continue",
                MinWidth = 140,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancel = new Button { Content = "Cancel", MinWidth = 90, Height = 32, IsCancel = true };

            apply.Click += (s, e) =>
            {
                Mapping.Clear();
                foreach (var kv in _combos)
                {
                    var chosen = kv.Value.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(chosen))
                    {
                        MessageBox.Show(this, "Please choose a category for every value.",
                            "Fix Item Categories", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Mapping[kv.Key] = chosen;
                }
                DialogResult = true;
            };

            buttons.Children.Add(apply);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            Content = root;
        }
    }
}
