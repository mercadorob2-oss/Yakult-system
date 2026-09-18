using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Creates a temporary copy of InvoiceReport.rdl with user-adjusted column widths
    /// applied to the 12 TablixColumns. The original file is never modified.
    ///
    /// Hidden columns (width == 0) collapse to 1mm so they vanish visually while
    /// still satisfying the RDLC schema. Visible columns are scaled proportionally
    /// so the total always equals the original page width.
    /// </summary>
    public static class InvoiceSheetRdlHelper
    {
        private const double ScreenDpi      = 96.0;
        private const double CollapsedMm    = 1.0;   // width given to hidden columns
        private const double MinVisibleMm   = 5.0;   // minimum width for visible columns

        // Original TablixColumn widths (mm) from InvoiceReport.rdl in order:
        // DATE, COMPANY, DOCUMENT#, REFERENCE#, STATUS, QTY, ITEM NAME,
        // START DATE, END DATE, LINE TOTAL, SUBTOTAL, SITE
        private static readonly double[] OriginalWidthsMm =
        {
            31.07267, 21.971, 36.64166, 26.05834, 20.76667,
            21.40168, 41.72168, 25.0, 25.0, 25.0, 25.0, 41.23267
        };

        private static double OriginalTotalMm => OriginalWidthsMm.Sum();

        /// <summary>
        /// Reads InvoiceReport.rdl, replaces the 12 TablixColumn widths, and saves
        /// to a unique temp file. Returns the temp file path.
        ///
        /// Pass <c>0</c> for any column the user has hidden; those columns will be
        /// collapsed to <see cref="CollapsedMm"/> in the output.
        /// </summary>
        public static string CreateTempRdlWithWidths(int[] dgvColumnWidthsPx)
        {
            if (dgvColumnWidthsPx == null || dgvColumnWidthsPx.Length != 12)
                throw new ArgumentException(
                    "Exactly 12 column widths required.", nameof(dgvColumnWidthsPx));

            var sourcePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Reports", "InvoiceReport.rdl");

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("InvoiceReport.rdl not found.", sourcePath);

            double[] adjMm = ComputeAdjustedWidths(dgvColumnWidthsPx);

            XDocument doc = XDocument.Load(sourcePath);
            XNamespace ns =
                "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

            var tablix = doc.Descendants(ns + "Tablix").FirstOrDefault();
            if (tablix == null)
                throw new InvalidOperationException(
                    "Tablix element not found in InvoiceReport.rdl.");

            var cols = tablix.Descendants(ns + "TablixColumn").ToList();
            if (cols.Count != 12)
                throw new InvalidOperationException(
                    $"Expected 12 TablixColumns in InvoiceReport.rdl, found {cols.Count}.");

            for (int i = 0; i < 12; i++)
            {
                var widthEl = cols[i].Element(ns + "Width");
                if (widthEl != null)
                    widthEl.Value = adjMm[i].ToString("F5", CultureInfo.InvariantCulture) + "mm";
            }

            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"InvoiceReport_Sheet_{Guid.NewGuid():N}.rdl");

            doc.Save(tempPath);
            return tempPath;
        }

        private static double[] ComputeAdjustedWidths(int[] dgvColumnWidthsPx)
        {
            bool[] visible = dgvColumnWidthsPx.Select(px => px > 0).ToArray();
            double[] result = new double[12];

            // 1. Assign collapsed width to hidden columns.
            int hiddenCount = visible.Count(v => !v);
            double collapsedTotal = hiddenCount * CollapsedMm;

            for (int i = 0; i < 12; i++)
                if (!visible[i])
                    result[i] = CollapsedMm;

            // 2. Scale visible columns to fill the remaining width.
            double availableForVisible = OriginalTotalMm - collapsedTotal;

            double[] visibleRawMm = dgvColumnWidthsPx
                .Select((px, i) => visible[i] ? px * 25.4 / ScreenDpi : 0.0)
                .ToArray();

            double visibleRawTotal = visibleRawMm.Where((w, i) => visible[i]).Sum();

            // Edge case: all columns hidden or all raw widths are 0.
            if (visibleRawTotal <= 0)
            {
                double equalMm = OriginalTotalMm / 12.0;
                for (int i = 0; i < 12; i++)
                    result[i] = equalMm;
                return result;
            }

            double scale = availableForVisible / visibleRawTotal;
            for (int i = 0; i < 12; i++)
                if (visible[i])
                    result[i] = Math.Max(MinVisibleMm, visibleRawMm[i] * scale);

            // 3. Re-normalise to exact target total (accounts for MinVisibleMm clamping).
            double currentTotal = result.Sum();
            double diff = OriginalTotalMm - currentTotal;

            if (Math.Abs(diff) > 0.001)
            {
                // Add the remainder to the last visible column.
                for (int i = 11; i >= 0; i--)
                {
                    if (visible[i])
                    {
                        result[i] = Math.Max(MinVisibleMm, result[i] + diff);
                        break;
                    }
                }
            }

            return result;
        }
    }
}
