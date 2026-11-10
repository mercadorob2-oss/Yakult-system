using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.DesktopUpdates;

[assembly: AssemblyFileVersion("1.6.0.0")]

internal static class Preview
{
    public static void Run(string appPath, string output)
    {
        Directory.CreateDirectory(output);
        System.Windows.Forms.Application.EnableVisualStyles();
        var directory = Path.GetDirectoryName(Path.GetFullPath(appPath));
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            var path = Path.Combine(directory, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var assembly = Assembly.LoadFrom(appPath);
        var serviceType = assembly.GetType("Yakult.Inventory.App.Services.DesktopUpdateService");
        var service = serviceType.GetProperty("Instance").GetValue(null);
        CheckReceipts(serviceType, service);
        var releaseType = assembly.GetType("Yakult.DesktopUpdates.DesktopRelease");
        var release = Activator.CreateInstance(releaseType);
        releaseType.GetProperty("Version").SetValue(release, "1.6.1.0");
        releaseType.GetProperty("ReleaseNotes").SetValue(release, "Improved inventory searches and ticket handling.\nIncludes the latest fixes for your daily work.");
        var stateType = assembly.GetType("Yakult.Inventory.App.Services.DesktopUpdateState");
        var state = Activator.CreateInstance(stateType);
        stateType.GetProperty("Release").SetValue(state, release);
        stateType.GetProperty("Message").SetValue(state, "Version 1.6.1.0 is available.");
        serviceType.GetProperty("State").SetValue(service, state);
        var windowType = assembly.GetType("Yakult.Inventory.App.Wpf.Portal.DesktopUpdateWindow");
        var available = (Window)Activator.CreateInstance(windowType, new object[] { null });
        Render((FrameworkElement)available.Content, 540, 385, Path.Combine(output, "update-available.png")); available.Close();
        var currentState = Activator.CreateInstance(stateType);
        stateType.GetProperty("Message").SetValue(currentState, "You're up to date.");
        serviceType.GetProperty("State").SetValue(service, currentState);
        var complete = (Window)Activator.CreateInstance(windowType, new object[] { "Congratulations! Your system is now running version 1.6.0.0." });
        Render((FrameworkElement)complete.Content, 540, 385, Path.Combine(output, "update-complete.png")); complete.Close();
        serviceType.GetProperty("State").SetValue(service, state);
        var session = assembly.GetType("Yakult.Inventory.App.Session.AppSession");
        session.GetProperty("IsDeveloper").SetValue(null, true);
        session.GetProperty("CurrentUserName").SetValue(null, "Preview User");
        var workspace = (Window)Activator.CreateInstance(assembly.GetType("Yakult.Inventory.App.Wpf.Portal.WpfMainDashboardShell"));
        Render((FrameworkElement)workspace.Content, 1366, 768, Path.Combine(output, "dashboard-notification.png")); workspace.Close();
        using (var helper = new UpdaterForm("unused-preview-job.json"))
        {
            var label = (System.Windows.Forms.Label)typeof(UpdaterForm).GetField("_status", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(helper);
            label.Text = "Installing version 1.6.1.0…\nPlease keep this window open.";
            var handle = helper.Handle;
            foreach (System.Windows.Forms.Control control in helper.Controls) { var childHandle = control.Handle; }
            helper.PerformLayout();
            using (var bitmap = new Bitmap(helper.Width, helper.Height)) { helper.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(Path.Combine(output, "installing.png")); }
        }
        Console.WriteLine("Rendered four isolated update UI states; no windows were shown and no database was used.");
    }

    private static void CheckReceipts(Type serviceType, object service)
    {
        var folder = Path.Combine(UpdateProtocol.CacheRoot, "checks-receipt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var currentPath = System.Windows.Forms.Application.ExecutablePath;
            var receiptPath = Path.Combine(folder, "receipt.json");
            var release = new DesktopRelease { SchemaVersion = 1, Product = UpdateProtocol.Product, Version = "1.6.0.0", Installer = "setup.exe", Sha256 = new string('a',64), SizeBytes = 512 };
            UpdateProtocol.WriteJson(Path.Combine(folder, "job.json"), new DesktopUpdateJob { ApplicationPath = currentPath, Release = release });
            var load = serviceType.GetMethod("LoadReceipt", BindingFlags.NonPublic | BindingFlags.Instance);
            var take = serviceType.GetMethod("TakeCompletionMessage");
            Action invoke = () => load.Invoke(service, new object[] { new[] { "--update-receipt", receiptPath } });
            foreach (var invalid in new[] {
                new DesktopUpdateReceipt { ApplicationPath = currentPath, InstalledVersion = "1.6.1.0", Succeeded = true },
                new DesktopUpdateReceipt { ApplicationPath = currentPath, InstalledVersion = "1.6.0.0", Succeeded = false },
                new DesktopUpdateReceipt { ApplicationPath = Path.Combine(folder,"other.exe"), InstalledVersion = "1.6.0.0", Succeeded = true } })
            {
                UpdateProtocol.WriteJson(receiptPath, invalid); invoke();
                if (take.Invoke(service,null) != null) throw new Exception("An invalid receipt announced success.");
                File.Delete(receiptPath);
            }
            UpdateProtocol.WriteJson(receiptPath, new DesktopUpdateReceipt { ApplicationPath = currentPath, InstalledVersion = "1.6.0.0", Succeeded = true });
            invoke();
            if (!((string)take.Invoke(service,null) ?? "").Contains("1.6.0.0")) throw new Exception("Verified completion receipt was not displayed.");
            if (take.Invoke(service,null) != null) throw new Exception("Completion message was displayed twice.");
            invoke();
            if (take.Invoke(service,null) != null) throw new Exception("Consumed receipt was replayed.");
            Console.WriteLine("PASS: six completion receipt checks against the desktop client.");
        }
        finally
        {
            var absolute = Path.GetFullPath(folder);
            var prefix = Path.GetFullPath(UpdateProtocol.CacheRoot).TrimEnd('\\') + "\\";
            if (!absolute.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("checks-receipt-")) throw new Exception("Invalid receipt test cleanup path.");
            Directory.Delete(absolute,true);
        }
    }

    private static void Render(FrameworkElement element, int width, int height, string path)
    {
        element.Measure(new System.Windows.Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
        image.Render(background); image.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
}
