using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.WPF.Inventory.Views;
using Yakult.Inventory.App.WPF.Items.Views;
using Yakult.Inventory.App.WPF.Admin.EmailManagement.Views;
using Yakult.Inventory.App.Wpf.CallMonitoring;

internal static class PaletteSmoke
{
    private const string Base = "/Yakult.Inventory.App;component/";
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
    [STAThread]
    private static int Main()
    {
        try
        {
            // Register pack URI handling for the standalone WPF smoke process.
            var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var palette = new ResourceDictionary { Source = new Uri(Base + "Wpf/Shared/Styles/YakultPalette.xaml", UriKind.Relative) };
            var primary = (SolidColorBrush)palette["YakultPrimaryBrush"];
            Check(primary.Color == Color.FromRgb(185, 35, 53), "Yakult primary brush resolves to red");
            var shared = new ResourceDictionary { Source = new Uri(Base + "Wpf/Shared/Styles/ListPageStyles.xaml", UriKind.Relative) };
            Check(((SolidColorBrush)shared["PrimaryBrush"]).Color == primary.Color, "Shared list controls use the Yakult accent");
            Check(((SolidColorBrush)shared["PrimaryHoverBrush"]).Color == Color.FromRgb(158, 30, 46), "Shared hover uses the darker red");
            Check(((SolidColorBrush)shared["SuccessBrush"]).Color != primary.Color, "Success remains distinct from the brand accent");
            var portal = new ResourceDictionary { Source = new Uri(Base + "Wpf/RequestPortal/RequestPortalStyles.xaml", UriKind.Relative) };
            Check(((SolidColorBrush)portal["YakultBlue"]).Color == primary.Color, "Shared request styles resolve the brand accent");

            foreach (var view in new UserControl[] { new InventoryPageView(), new ItemsPageView(), new EmailAddressesTabView() })
            {
                view.Measure(new Size(1200, 800));
                view.Arrange(new Rect(0, 0, 1200, 800));
                Check(view.RenderSize.Width > 0, view.GetType().Name + " loads and lays out with the new resources");
            }
            var callDashboard = new WpfCallMonitoringWorkspace();
            callDashboard.Measure(new Size(1280, 800));
            callDashboard.Arrange(new Rect(0, 0, 1280, 800));
            Check(callDashboard.RenderSize.Width > 0, "IT Call Monitoring dashboard lays out with the shared palette");
            var bitmap = new RenderTargetBitmap(1280, 800, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(callDashboard);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create("work/itcm-theme/dashboard-preview.png")) encoder.Save(output);
            Console.WriteLine("Palette UI smoke checks passed; no windows were shown.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
