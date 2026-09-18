using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.RequestManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestManagement.Views
{
    public partial class FulfillRequestDialog : Window
    {
        private readonly List<FulfillRequestRowStateViewModel> _states;

        public bool AllStatesEmpty => _states.All(s => s.IssueQty <= 0);

        public FulfillRequestDialog(List<FulfillRequestRowStateViewModel> states)
        {
            _states = states ?? throw new ArgumentNullException(nameof(states));

            InitializeComponent();

            StatesPanel.ItemsSource = _states;

            var first = _states.FirstOrDefault();
            if (first != null)
            {
                TitleText.Text = _states.Count == 1
                    ? $"Req #{first.Dto.ReqId}  ·  {first.ItemName}"
                    : $"Fulfill Requests  ·  {_states.Count} Items";

                SubtitleText.Text = $"{first.Dto.EmployeeName}";
            }

            int cardEstimate = Math.Min(_states.Count * 260 + 120, 600);
            Height = Math.Max(300, cardEstimate);
        }

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
