using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class FulfillExchangeDialog : Window
    {
        private readonly List<FulfillRowStateViewModel> _states;

        public bool AllStatesEmpty => _states.All(s => s.TotalToIssue <= 0);

        public FulfillExchangeDialog(List<FulfillRowStateViewModel> states)
        {
            _states = states ?? throw new ArgumentNullException(nameof(states));

            InitializeComponent();

            // Bind the ItemsControl to the states list
            StatesPanel.ItemsSource = _states;

            // Set dialog title from first state
            var first = _states.FirstOrDefault();
            if (first != null)
            {
                TitleText.Text = _states.Count == 1
                    ? $"Req #{first.Dto.ReqId}  ·  {first.CartridgeModel}"
                    : $"Fulfill Cartridge Exchange  ·  {_states.Count} Models";

                SubtitleText.Text = $"{first.Dto.RequesterName}";
            }

            // Size dialog height to content (cap at MaxHeight)
            int cardEstimate = Math.Min(_states.Count * 260 + 120, 600);
            Height = Math.Max(300, cardEstimate);
        }

        // Allow dragging the borderless window by the header
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
