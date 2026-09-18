using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.CartridgeManagement.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class PrintTransmittalDialog : Window
    {
        // Preview boxes are capped to this height; width is derived from the selected
        // page size's real aspect ratio so Letter/Legal/A4 each look like their true
        // shape instead of being squeezed into one fixed box.
        private const double PreviewMaxHeight = 537;

        private readonly CartridgeTransmittalViewModel _vm;
        private TransmittalPrintService.TransmittalPageSize _selectedPageSize =
            TransmittalPrintService.TransmittalPageSize.Legal;

        // Set when the user clicks "Go Back" so TransmittalPrintService.ShowPrintDialog
        // knows to re-open the Prepare Transmittal form instead of ending the flow.
        public bool WentBack { get; private set; }

        // XAML sets _legalRadio's IsChecked="True" and the copy checkboxes' IsChecked
        // during InitializeComponent, which raises Checked before every field later in
        // the tree (_previewPanel, etc.) is connected yet. Guard against handling those
        // premature events.
        private bool _initialized;

        public PrintTransmittalDialog(CartridgeTransmittalViewModel vm)
        {
            _vm = vm;
            InitializeComponent();
            _initialized = true;
            RenderPreview();
        }

        private void OnPageSizeChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            if (!(sender is RadioButton rb) || rb.Tag == null) return;
            if (!Enum.TryParse(rb.Tag.ToString(), out TransmittalPrintService.TransmittalPageSize size)) return;

            _selectedPageSize = size;
            RenderPreview();
        }

        private void OnCopySelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            RenderPreview();
        }

        // Selected copies, always in TransmittalPrintService.CanonicalCopyOrder so
        // pairing onto pages matches the order the user sees them in the header
        // (Gatepass, Transmittal, File).
        private List<TransmittalPrintService.CopyType> GetSelectedCopies()
        {
            var selected = new List<TransmittalPrintService.CopyType>();
            if (_gatepassCheck.IsChecked == true) selected.Add(TransmittalPrintService.CopyType.Gatepass);
            if (_transmittalCheck.IsChecked == true) selected.Add(TransmittalPrintService.CopyType.Transmittal);
            if (_fileCheck.IsChecked == true) selected.Add(TransmittalPrintService.CopyType.File);
            return selected;
        }

        // Preview the exact pages Print() will produce — built from a cloned VM so
        // previewing doesn't disturb the flags on the original — sized to the
        // selected page's real aspect ratio so switching Letter/Legal/A4 visually
        // shows its true shape. Column count and labels adjust to whichever copies
        // are currently checked.
        private void RenderPreview()
        {
            var selected = GetSelectedCopies();
            _printButton.IsEnabled = selected.Count > 0;

            _previewPanel.Children.Clear();

            if (selected.Count == 0)
            {
                _descriptionText.Text = "Select at least one copy to print.";
                return;
            }

            var (pageWidth, pageHeight) = TransmittalPrintService.GetPageDimensions(_selectedPageSize);
            double previewWidth = PreviewMaxHeight * (pageWidth / pageHeight);

            var pages = TransmittalPrintService.BuildPages(_vm.Clone(), selected, _selectedPageSize);

            _descriptionText.Text = pages.Count == 1
                ? "Review the transmittal form below. Printing will produce 1 page."
                : $"Review the transmittal form below. Printing will produce {pages.Count} pages.";

            for (int i = 0; i < pages.Count; i++)
            {
                var column = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
                column.Children.Add(new TextBlock
                {
                    Text = DescribePage(selected, i),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2340")),
                    Margin = new Thickness(0, 0, 0, 6)
                });

                var box = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(6),
                    Height = PreviewMaxHeight,
                    Width = previewWidth,
                    Child = pages[i]
                };
                var border = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCC")),
                    BorderThickness = new Thickness(1),
                    Child = box
                };
                column.Children.Add(border);

                _previewPanel.Children.Add(column);
            }
        }

        private static string DescribePage(IReadOnlyList<TransmittalPrintService.CopyType> selected, int pageIndex)
        {
            int i = pageIndex * 2;
            var label = "PAGE " + (pageIndex + 1) + " — " + selected[i].ToString().ToUpperInvariant();
            if (i + 1 < selected.Count)
                label += " + " + selected[i + 1].ToString().ToUpperInvariant();
            return label;
        }

        private void OnPrint(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedCopies();
            if (selected.Count == 0) return;

            TransmittalPrintService.Print(_vm, selected, _selectedPageSize);
            Close();
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnGoBack(object sender, RoutedEventArgs e)
        {
            WentBack = true;
            Close();
        }

        // TEMPORARY diagnostic entry point — see the button's comment in the XAML.
        private void OnCalibrationPrint(object sender, RoutedEventArgs e)
        {
            CalibrationPrintService.Print(_selectedPageSize);
        }
    }
}
