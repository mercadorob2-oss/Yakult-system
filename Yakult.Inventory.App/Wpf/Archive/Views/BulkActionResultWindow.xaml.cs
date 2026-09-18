using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Yakult.Inventory.App.WPF.Archive.Views
{
    public sealed class BulkActionResultRow
    {
        public string RecordName { get; set; }
        public string Reason     { get; set; }
        public bool   IsWarning  { get; set; }

        public string Icon      => IsWarning ? "\uE7BA" : "\uE711"; // warning triangle / cancel-x (Segoe MDL2 Assets)
        public Brush  IconBrush => IsWarning
            ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))
            : new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F));
    }

    /// <summary>
    /// Shared result dialog for Archive Records bulk actions (Restore / Permanently Delete).
    /// Replaces a plain MessageBox with a scrollable, per-record breakdown so the outcome of a
    /// batch operation on many rows is actually readable.
    /// </summary>
    public partial class BulkActionResultWindow : Window
    {
        public string WindowTitle  { get; }
        public string HeaderIcon   { get; }
        public string SucceededSummary  { get; }
        public string BlockedSummary    { get; }
        public string OtherErrorsSummary { get; }
        public string BlockedIntro { get; }
        public bool   HasBlocked     { get; }
        public bool   HasOtherErrors { get; }
        public ObservableCollection<BulkActionResultRow> Rows { get; } = new ObservableCollection<BulkActionResultRow>();

        public BulkActionResultWindow(
            string windowTitle,
            string headerIcon,
            int succeededCount,
            string succeededNoun,
            string blockedIntro,
            IEnumerable<BulkActionResultRow> blockedRows,
            IEnumerable<BulkActionResultRow> otherErrorRows)
        {
            InitializeComponent();

            WindowTitle     = windowTitle;
            HeaderIcon      = headerIcon;
            SucceededSummary = $"{succeededCount} {succeededNoun}";
            BlockedIntro    = blockedIntro;

            int blockedCount = 0, otherCount = 0;
            foreach (var row in blockedRows)
            {
                Rows.Add(row);
                blockedCount++;
            }
            foreach (var row in otherErrorRows)
            {
                Rows.Add(row);
                otherCount++;
            }

            HasBlocked       = blockedCount > 0;
            HasOtherErrors   = otherCount > 0;
            BlockedSummary   = $"{blockedCount} blocked";
            OtherErrorsSummary = $"{otherCount} other error{(otherCount == 1 ? "" : "s")}";

            DataContext = this;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => Close();
    }
}
