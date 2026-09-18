using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Services
{
    // Temporary diagnostic-only logging for isolating why the physical printed
    // output can differ from Print Preview. Writes to the Debug console AND a
    // plain log file (Debug.WriteLine only shows up with a debugger attached —
    // real printer test runs are usually launched without one, so the file is
    // what actually captures those). Does not affect any print or layout
    // behavior. Safe to delete once the print-pipeline discrepancy is fixed.
    public static class PrintDiagnosticsService
    {
        public static readonly string LogFilePath =
            Path.Combine(Path.GetTempPath(), "YakultPrintDiagnostics.log");

        public static void LogPageMetrics(
            string label,
            PrintDialog dlg,
            TransmittalPrintService.TransmittalPageSize pageSize,
            double pageWidth, double pageHeight,
            double topY, double lineY, double bottomY)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== PRINT DIAGNOSTICS [{label}] — {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
            sb.AppendLine($"Printer Name                    : {dlg.PrintQueue?.FullName}");
            sb.AppendLine($"Selected Page Size               : {pageSize}");
            sb.AppendLine($"PrintTicket.PageMediaSize         : " +
                $"{dlg.PrintTicket?.PageMediaSize?.PageMediaSizeName} " +
                $"({dlg.PrintTicket?.PageMediaSize?.Width:F2} x {dlg.PrintTicket?.PageMediaSize?.Height:F2})");
            sb.AppendLine($"PrintTicket.PageOrientation       : {dlg.PrintTicket?.PageOrientation}");
            sb.AppendLine($"PrintDialog.PrintableAreaWidth    : {dlg.PrintableAreaWidth:F2}");
            sb.AppendLine($"PrintDialog.PrintableAreaHeight   : {dlg.PrintableAreaHeight:F2}");

            try
            {
                var caps = dlg.PrintQueue?.GetPrintCapabilities(dlg.PrintTicket);
                var area = caps?.PageImageableArea;
                if (area != null)
                {
                    sb.AppendLine($"PageImageableArea.OriginWidth     : {area.OriginWidth:F2}");
                    sb.AppendLine($"PageImageableArea.OriginHeight    : {area.OriginHeight:F2}");
                    sb.AppendLine($"PageImageableArea.ExtentWidth     : {area.ExtentWidth:F2}");
                    sb.AppendLine($"PageImageableArea.ExtentHeight    : {area.ExtentHeight:F2}");
                }
                else
                {
                    sb.AppendLine("PageImageableArea                : not reported by driver");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"PageImageableArea                : lookup failed ({ex.Message})");
            }

            sb.AppendLine($"BuildPageVisual.pageWidth         : {pageWidth:F2}");
            sb.AppendLine($"BuildPageVisual.pageHeight        : {pageHeight:F2}");
            sb.AppendLine($"BuildPageVisual.halfHeight        : {pageHeight / 2.0:F2}");
            sb.AppendLine($"Calculated Y - top copy           : {topY:F2}");
            sb.AppendLine($"Calculated Y - dotted line        : {lineY:F2}");
            sb.AppendLine($"Calculated Y - bottom copy        : {bottomY:F2}");
            sb.AppendLine("========================================");

            string text = sb.ToString();
            Debug.WriteLine(text);

            try
            {
                File.AppendAllText(LogFilePath, text + Environment.NewLine);
            }
            catch
            {
                // Best-effort — a locked/inaccessible temp path shouldn't block printing.
            }
        }
    }
}
