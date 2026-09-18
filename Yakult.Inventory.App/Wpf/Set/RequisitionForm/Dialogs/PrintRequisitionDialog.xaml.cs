using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Services;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Dialogs
{
    public partial class PrintRequisitionDialog : Window
    {
        // Preview box is capped to this height; width is derived from the selected
        // page size's real aspect ratio so Letter/Legal/A4 each look like their true
        // shape instead of being squeezed into one fixed portrait box.
        private const double PreviewMaxHeight = 760;

        private readonly RequisitionFormViewModel _vm;
        private RequisitionPageSize _selectedPageSize = RequisitionPageSize.Letter;
        private int _selectedCopyCount = 2;

        // XAML sets _letterRadio's IsChecked="True" during InitializeComponent, which
        // raises Checked before _previewBox/_pageHost (declared later in the tree) are
        // connected to their fields yet. Guard against handling that premature event.
        private bool _initialized;

        public PrintRequisitionDialog(RequisitionFormViewModel vm)
        {
            _vm = vm;
            InitializeComponent();

            // The XAML's fixed Height (980) can exceed the available screen work area on
            // smaller or scaled displays, pushing the title bar and Print/Skip buttons off
            // screen. Cap it to a comfortable fraction of the actual work area instead — the
            // preview area already scrolls (see PrintRequisitionDialog.xaml), so shrinking
            // the window just means more scrolling, not lost content.
            var workArea = SystemParameters.WorkArea;
            Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height * 0.85));

            _initialized = true;
            RenderPreview();
        }

        private void OnPageSizeChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            if (!(sender is RadioButton rb) || rb.Tag == null) return;
            if (!Enum.TryParse(rb.Tag.ToString(), out RequisitionPageSize size)) return;

            _selectedPageSize = size;
            RenderPreview();
        }

        private void OnCopyCountChanged(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            if (!(sender is RadioButton rb) || rb.Tag == null) return;
            if (!int.TryParse(rb.Tag.ToString(), out int count)) return;

            _selectedCopyCount = count;
            RenderPreview();
        }

        // Preview the exact page Print() will produce — built from a cloned VM so
        // previewing doesn't disturb the original — sized to the selected page's real
        // aspect ratio so switching Letter/Legal/A4 visually shows its true shape.
        private void RenderPreview()
        {
            var (pageWidth, pageHeight) = RequisitionFormPrintService.GetPageDimensions(_selectedPageSize);

            _previewBox.Height = PreviewMaxHeight;
            _previewBox.Width  = PreviewMaxHeight * (pageWidth / pageHeight);
            _pageHost.Content  = RequisitionFormPrintService.BuildPageVisual(_vm.Clone(), _selectedPageSize, _selectedCopyCount);
        }

        private void OnPrint(object sender, RoutedEventArgs e)
        {
            RequisitionFormPrintService.Print(_vm, _selectedPageSize, _selectedCopyCount);
            Close();
        }

        // Routes straight to the "Microsoft Print to PDF" virtual printer instead of
        // showing the normal print dialog. That driver prompts for the save location
        // itself, so no SaveFileDialog is needed here — mirrors the pattern used by
        // EDocsDashboardViewModel's PDF export.
        private void OnPrintAsPdf(object sender, RoutedEventArgs e)
        {
            RequisitionFormPrintService.Print(_vm, _selectedPageSize, _selectedCopyCount, "Microsoft Print to PDF");
            Close();
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
