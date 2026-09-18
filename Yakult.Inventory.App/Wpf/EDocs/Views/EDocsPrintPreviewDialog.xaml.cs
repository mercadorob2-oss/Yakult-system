using System;
using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.EDocs.Views
{
    // Shared print-preview shell for the standalone E-Docs forms, matching
    // PrintRequisitionDialog design: header with page-size/copies radios,
    // large preview, Skip/Print footer.
    public partial class EDocsPrintPreviewDialog : Window
    {
        private readonly Action _onPrint;

        public EDocsPrintPreviewDialog(string title, string subtitle, FrameworkElement previewContent, Action onPrint)
            : this(title, subtitle, previewContent, onPrint, "Letter")
        {
        }

        // defaultPageSize preselects the paper the form is designed for
        // ("Letter", "Legal" or "A4").
        public EDocsPrintPreviewDialog(string title, string subtitle, FrameworkElement previewContent, Action onPrint, string defaultPageSize)
        {
            _onPrint = onPrint;
            InitializeComponent();

            Title = title;
            TitleText.Text = title;
            SubtitleText.Text = subtitle ?? "";
            PreviewHost.Content = previewContent;

            if (string.Equals(defaultPageSize, "Legal", StringComparison.OrdinalIgnoreCase))
                PageSizeLegal.IsChecked = true;
            else if (string.Equals(defaultPageSize, "A4", StringComparison.OrdinalIgnoreCase))
                PageSizeA4.IsChecked = true;
            else
                PageSizeLetter.IsChecked = true;
            Copies1.IsChecked = true;

            var workArea = SystemParameters.WorkArea;
            Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height * 0.85));
        }

        public string SelectedPageSize
        {
            get
            {
                if (PageSizeLetter.IsChecked == true) return "Letter";
                if (PageSizeLegal.IsChecked == true) return "Legal";
                if (PageSizeA4.IsChecked == true) return "A4";
                return "Letter";
            }
        }

        public int SelectedCopies => Copies2.IsChecked == true ? 2 : 1;

        private void OnPrint(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_onPrint != null) _onPrint();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Print",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
