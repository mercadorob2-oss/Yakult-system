using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Final decision point in the ITCM resolution flow. It intentionally appears after the
    /// operator has selected Service or Replacement, so repair tickets are only created for items
    /// that genuinely need physical assessment or repair.
    /// </summary>
    internal sealed class WpfForwardToRepairDialog : Window
    {
        private readonly ComboBox _itemCombo;
        private readonly int? _fixedItemId;
        private readonly TextBlock _validationText;

        public int? RepairItemId { get; private set; }

        public WpfForwardToRepairDialog(
            IEnumerable<ItemLookupDto> repairableItems,
            int? fixedItemId,
            bool isReplacement,
            bool usesUnlistedOldItem)
        {
            _fixedItemId = fixedItemId.HasValue && fixedItemId.Value > 0 ? fixedItemId : null;

            Title = "Forward to Repair Portal";
            Width = 590;
            Height = 360;
            MinWidth = 520;
            MinHeight = 320;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = Brush(245, 247, 250);

            var root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var title = new TextBlock
            {
                Text = "Forward affected item to Repair Portal?",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(15, 23, 42)
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var description = new TextBlock
            {
                Text = isReplacement
                    ? "A linked Repair Ticket will be created for the replaced old item. The IT Call will remain open as Forwarded to Repair."
                    : "Choose the physical item that requires technician assessment. A linked Repair Ticket will be created and the IT Call will remain open.",
                Margin = new Thickness(0, 8, 0, 18),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = Brush(71, 85, 105)
            };
            Grid.SetRow(description, 1);
            root.Children.Add(description);

            var itemPanel = new StackPanel();
            itemPanel.Children.Add(new TextBlock
            {
                Text = isReplacement ? "Repair item" : "Item to forward",
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(51, 65, 85),
                Margin = new Thickness(0, 0, 0, 5)
            });

            _itemCombo = new ComboBox
            {
                MinHeight = 34,
                Padding = new Thickness(8, 4, 8, 4),
                DisplayMemberPath = "DisplayText",
                SelectedValuePath = "ItemId",
                ItemsSource = (repairableItems ?? Enumerable.Empty<ItemLookupDto>())
                    .Where(item => item != null)
                    .OrderBy(item => item.DisplayText)
                    .ToList()
            };

            if (_fixedItemId.HasValue)
            {
                _itemCombo.SelectedValue = _fixedItemId.Value;
                _itemCombo.IsEnabled = false;
                itemPanel.Children.Add(_itemCombo);
                itemPanel.Children.Add(new TextBlock
                {
                    Text = "The old item from this replacement will remain out of stock while it is assessed by Repair.",
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5,
                    Foreground = Brush(146, 64, 14)
                });
            }
            else if (isReplacement && usesUnlistedOldItem)
            {
                _itemCombo.Visibility = Visibility.Collapsed;
                itemPanel.Children.Add(_itemCombo);
                itemPanel.Children.Add(new TextBlock
                {
                    Text = "The unlisted old item will be added to inventory and then forwarded to Repair.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11.5,
                    Foreground = Brush(146, 64, 14)
                });
            }
            else
            {
                _itemCombo.SelectedIndex = -1;
                itemPanel.Children.Add(_itemCombo);
            }

            Grid.SetRow(itemPanel, 2);
            root.Children.Add(itemPanel);

            _validationText = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = Brush(185, 28, 28),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_validationText, 3);
            root.Children.Add(_validationText);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };
            var skip = Button("Continue without forwarding", Brush(71, 85, 105));
            skip.Click += (_, __) => { DialogResult = false; Close(); };
            var forward = Button("Forward to Repair", Brush(22, 163, 74));
            forward.Click += (_, __) => Forward();
            actions.Children.Add(skip);
            actions.Children.Add(forward);
            Grid.SetRow(actions, 4);
            root.Children.Add(actions);
        }

        private void Forward()
        {
            RepairItemId = _fixedItemId ?? (_itemCombo.SelectedItem as ItemLookupDto)?.ItemId;
            if (!RepairItemId.HasValue && _itemCombo.Visibility == Visibility.Visible)
            {
                _validationText.Text = "Select the physical item that should be forwarded to Repair.";
                _validationText.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
            Close();
        }

        private static Button Button(string text, Brush background) => new Button
        {
            Content = text,
            MinHeight = 34,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(13, 7, 13, 7),
            Background = background,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        private static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
