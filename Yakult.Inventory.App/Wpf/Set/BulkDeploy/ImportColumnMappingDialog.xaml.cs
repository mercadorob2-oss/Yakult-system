using System.Linq;
using System.Windows;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    /// <summary>
    /// Shown after picking a file in "Import Excel File" and before any rows
    /// are added to the grid. Reports which template columns were actually
    /// found in the workbook (by header name) versus which are missing (and
    /// will import blank), so a header mismatch is caught up front instead
    /// of producing confusing all-blank-looking rows in the grid.
    /// </summary>
    public partial class ImportColumnMappingDialog : Window
    {
        public bool Confirmed { get; private set; }

        public ImportColumnMappingDialog(string fileName, BulkDeployExcelImporter.ColumnMappingReport report)
        {
            InitializeComponent();

            SubtitleText.Text = $"{fileName} \u2014 {report.DataRowCount} data row(s) detected, "
                + (report.IsLegacyFormat ? "legacy YPI format." : "template format.");

            MatchedText.Text = report.MatchedColumns.Count > 0
                ? string.Join(", ", report.MatchedColumns)
                : "(none)";

            MissingText.Text = report.MissingColumns.Count > 0
                ? string.Join(", ", report.MissingColumns)
                : "(none - every template column was found)";

            if (report.UnrecognizedFileColumns.Count > 0)
            {
                UnrecognizedCard.Visibility = Visibility.Visible;
                UnrecognizedText.Text = string.Join(", ", report.UnrecognizedFileColumns.Take(20));
            }

            // No data rows at all is almost always a wrong-sheet-order or
            // completely-mismatched-headers problem (see BulkDeployExcelImporter
            // reading dataSet.Tables[0] unconditionally) - disable Import so the
            // user isn't left wondering why "0 rows imported" happened silently.
            if (report.DataRowCount == 0)
            {
                BtnImport.IsEnabled = false;
                BtnImport.ToolTip = "No data rows were detected in this file's first sheet.";
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
        }
    }
}
