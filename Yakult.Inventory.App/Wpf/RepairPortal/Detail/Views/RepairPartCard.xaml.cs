using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels;
using Yakult.Inventory.App.Wpf.RepairPortal.Shared;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>One Part's collapsed summary + lazy-expanded diagnostic/repair notes, evidence,
    /// status, mini timeline, and delete — entirely inline, no separate Part Detail window.
    /// DataContext is the RepairPartCardViewModel supplied by the parent ItemsControl's item
    /// template; file pickers/confirmations are bridged to WinForms here since Application.Current
    /// is always null in this hosted-WPF app.</summary>
    public partial class RepairPartCard : UserControl
    {
        public RepairPartCard()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RepairPartCardViewModel oldVm)
            {
                oldVm.RequestFilePicker -= OnRequestFilePicker;
                oldVm.RequestConfirm -= OnRequestConfirm;
                oldVm.RequestError -= OnRequestError;
                oldVm.RequestCompletionDate -= OnRequestCompletionDate;
            }

            if (e.NewValue is RepairPartCardViewModel newVm)
            {
                newVm.RequestFilePicker += OnRequestFilePicker;
                newVm.RequestConfirm += OnRequestConfirm;
                newVm.RequestError += OnRequestError;
                newVm.RequestCompletionDate += OnRequestCompletionDate;
            }
        }

        private DateTime? OnRequestCompletionDate(string statusLabel)
        {
            // No owner passed — matches OnRequestConfirm/OnRequestError above, which also show
            // WinForms.MessageBox without an owner handle in this hosted-WPF app.
            return CompletionDatePromptForm.PromptFor(null, statusLabel);
        }

        private static List<string> OnRequestFilePicker()
        {
            using (var dlg = new WinForms.OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Title = "Select Evidence Files";
                dlg.Filter = "All Supported Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.mp4;*.mov;*.avi;*.wmv;*.mkv;*.pdf;*.doc;*.docx|All Files|*.*";

                return dlg.ShowDialog() == WinForms.DialogResult.OK ? dlg.FileNames.ToList() : new List<string>();
            }
        }

        private static bool OnRequestConfirm(string title, string message)
        {
            return WinForms.MessageBox.Show(message, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
        }

        private static void OnRequestError(string title, string message)
        {
            WinForms.MessageBox.Show(message, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }

        private void Card_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Card_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (!(DataContext is RepairPartCardViewModel vm)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            await vm.AddAttachmentsAsync(paths);
        }
    }
}
