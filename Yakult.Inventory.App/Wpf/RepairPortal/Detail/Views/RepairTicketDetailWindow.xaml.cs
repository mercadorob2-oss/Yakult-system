using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels;
using Yakult.Inventory.App.Wpf.RepairPortal.PartIntake.Views;
using Yakult.Inventory.App.Wpf.RepairPortal.Shared;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>
    /// Non-modal Detail window (opened via Show(), not ShowDialog(), so the technician can flip
    /// between Detail and the feed). Raises Closed so the Shell refreshes just this one row.
    /// </summary>
    public partial class RepairTicketDetailWindow : Window
    {
        private readonly RepairTicketDetailViewModel _vm;
        private readonly IRepairTicketRepository _repository;

        public RepairTicketDetailWindow(int repairTicketId, IRepairTicketRepository repository)
        {
            InitializeComponent();

            _repository = repository ?? new RepairTicketRepository();
            _vm = new RepairTicketDetailViewModel(repairTicketId, _repository);
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestFilePicker += OnRequestFilePicker;
            _vm.RequestConfirm += OnRequestConfirm;
            _vm.RequestAddPart += OnRequestAddPart;
            _vm.RequestCompletionDate += OnRequestCompletionDate;
            _vm.RequestSelectRepairedBy += OnRequestSelectRepairedBy;
            _vm.RequestReplacementItemPicker += OnRequestReplacementItemPicker;
            _vm.RequestSpareItemPicker += OnRequestSpareItemPicker;
            _vm.RequestCloseWindow += Close;

            Loaded += RepairTicketDetailWindow_Loaded;
        }

        /// <summary>
        /// This WPF window is hosted inside a non-DPI-aware WinForms process, so the fixed
        /// Height/Width in XAML gets stretched by Windows' DPI virtualization on scaled displays,
        /// making the window far larger on screen than the numbers suggest (same issue fixed for
        /// RepairPortalShellWindow). Clamp it to the actual monitor's working area, converted
        /// through the real device-to-DIP transform, instead of the raw XAML size.
        /// </summary>
        private void RepairTicketDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            if (dpiX <= 0) dpiX = 1.0;
            if (dpiY <= 0) dpiY = 1.0;

            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var workArea = WinForms.Screen.FromHandle(handle).WorkingArea;

            double availWidth = workArea.Width / dpiX;
            double availHeight = workArea.Height / dpiY;
            const double margin = 60;

            double targetWidth = System.Math.Min(Width, availWidth - margin);
            double targetHeight = System.Math.Min(Height, availHeight - margin);

            Width = System.Math.Max(MinWidth, targetWidth);
            Height = System.Math.Max(MinHeight, targetHeight);

            // Re-center over the owner (if any) now that the size may have shrunk, else on screen.
            if (Owner != null)
            {
                Left = Owner.Left + (Owner.ActualWidth - Width) / 2;
                Top = Owner.Top + (Owner.ActualHeight - Height) / 2;
            }
            else
            {
                double screenLeft = workArea.Left / dpiX;
                double screenTop = workArea.Top / dpiY;
                Left = screenLeft + (availWidth - Width) / 2;
                Top = screenTop + (availHeight - Height) / 2;
            }

            // Belt-and-suspenders alongside the Activate() call in RepairPortalShellWindow's
            // OnRequestOpenDetail — resizing/repositioning above can itself disturb activation, so
            // re-assert it once more after that settles, and pull real keyboard focus onto the
            // window itself (not just a specific control) so typing anywhere in it registers.
            Activate();
            Keyboard.Focus(this);
        }

        private bool OnRequestConfirm(string title, string message)
        {
            return WinForms.MessageBox.Show(message, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
        }

        private DateTime? OnRequestCompletionDate(string statusLabel)
        {
            return CompletionDatePromptForm.PromptFor(null, statusLabel);
        }

        private List<int> OnRequestSelectRepairedBy(List<Yakult.Inventory.App.Models.RepairPortal.OrgLookupOption> candidates, List<int> currentlySelected)
        {
            var dialog = new SelectRepairedByDialog(candidates, currentlySelected) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.SelectedIds : null;
        }

        private int? OnRequestReplacementItemPicker()
        {
            var dialog = new ReplacementItemPickerWindow(_repository, _vm.Detail.ItemId) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.SelectedItemId : null;
        }

        private string OnRequestSpareItemPicker()
        {
            var dialog = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowableItemsPickerDialog(new BorrowItemsRepository()) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.SelectedItem?.SerialNumber : null;
        }

        // ── Evidence Gallery (aggregate: ticket-level + all Parts) — click to preview ───────
        // Each entry's bytes are fetched lazily by EvidenceViewerWindow only when actually
        // displayed (see EvidenceViewerEntry.BytesLoader) — fetching every item's full-resolution
        // bytes up front here made opening the viewer slow for anything but a tiny gallery.
        private void EvidenceGalleryItem_Click(object sender, MouseButtonEventArgs e)
        {
            var clicked = (sender as FrameworkElement)?.DataContext as AttachmentGalleryItem;
            if (clicked == null) return;

            var galleryItems = _vm.EvidenceGallery.ToList();
            if (galleryItems.Count == 0) return;

            var entries = new List<EvidenceViewerEntry>();
            var startIndex = 0;

            for (var i = 0; i < galleryItems.Count; i++)
            {
                var item = galleryItems[i];
                var isPartLevel = item.IsPartLevel;
                var attachmentId = item.AttachmentId;

                Func<Task<byte[]>> loader = async () =>
                {
                    if (isPartLevel)
                    {
                        var full = await _repository.GetPartAttachmentAsync(attachmentId);
                        return full?.FileBytes;
                    }
                    var ticketLevel = await _repository.GetAttachmentAsync(attachmentId);
                    return ticketLevel?.FileBytes;
                };

                entries.Add(new EvidenceViewerEntry(item.FileName, item.AttachmentType, loader,
                    item.PartDisplayName ?? "Whole Equipment"));

                if (ReferenceEquals(item, clicked)) startIndex = i;
            }

            var viewer = new EvidenceViewerWindow(entries, startIndex) { Owner = this };
            viewer.ShowDialog();
        }

        // ── QR Code (scan-to-lookup, mirrors ViewSetDetailPage's Generate/View QR) ─────────
        private async void BtnGenerateQr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnGenerateQr.IsEnabled = false;
                BtnGenerateQr.Content = "Generating...";

                var (_, qrToken) = await _repository.GetQrInfoAsync(_vm.RepairTicketId);
                var reportData = await _repository.GetReportDataAsync(_vm.RepairTicketId, "System");

                string qrDataJson = Helpers.RepairTicketQRGenerator.BuildQRDataString(_vm.Detail, reportData);
                using (var qrImage = Helpers.RepairTicketQRGenerator.GenerateQRCodeImage(qrToken))
                {
                    byte[] qrImageBytes = Helpers.RepairTicketQRGenerator.GetQRCodeImageBytes(qrImage);
                    await _repository.UpdateQRDataAsync(_vm.RepairTicketId, qrImageBytes, qrDataJson);
                }

                WinForms.MessageBox.Show("QR Code generated successfully.", "QR Generated", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show("Failed to generate QR code: " + ex.Message, "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
            finally
            {
                BtnGenerateQr.IsEnabled = true;
                BtnGenerateQr.Content = "Generate QR";
            }
        }

        private async void BtnViewQr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (qrImageData, _) = await _repository.GetQrInfoAsync(_vm.RepairTicketId);
                if (qrImageData == null || qrImageData.Length == 0)
                {
                    WinForms.MessageBox.Show("No QR code has been generated yet.", "No QR Code", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    return;
                }

                var viewer = new Yakult.Inventory.App.Pages.ImageViewerDialog(qrImageData, $"{_vm.Detail?.TicketCode} - QR Code");
                viewer.ShowDialog(new Yakult.Inventory.App.Pages.Set.WpfWin32Window(this));
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show("Failed to load QR code: " + ex.Message, "Error", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async void OnRequestAddPart()
        {
            var dialog = new AddPartDialog(_vm.RepairTicketId, _repository) { Owner = this };
            var result = dialog.ShowDialog();
            if (result == true)
                await _vm.LoadAsync();
        }

        private List<string> OnRequestFilePicker()
        {
            using (var dlg = new WinForms.OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = "Select Evidence Files";
                dlg.Filter = "All Supported Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.pdf;*.doc;*.docx|All Files|*.*";

                return dlg.ShowDialog() == WinForms.DialogResult.OK ? dlg.FileNames.ToList() : new List<string>();
            }
        }

        private void EvidencePanel_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void EvidencePanel_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            await _vm.AddAttachmentsAsync(paths);
        }
    }
}
