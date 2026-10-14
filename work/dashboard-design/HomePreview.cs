// Standalone, read-only visual and interaction check against the built desktop assembly.
// Run without opening a window or connecting to a database.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.Portal;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.Portal;

class HomePreview
{
    static string Output;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool pass, string message)
    { if (!pass) throw new Exception(message); Console.WriteLine("PASS: " + message); }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Output = args[0];
            DatabaseConfig.Bootstrap("Data Source=(local);Initial Catalog=PreviewOnly;Integrated Security=True");
            AppSession.CurrentUserName = "Russel";
            AppSession.CurrentCompanyName = "YPI";
            AppSession.CurrentDepartmentName = "Information Technology";
            AppSession.CurrentUserRoles = new List<string> { "Developer" };
            AppSession.IsDeveloper = true;
            AppSession.CurrentUserId = 0; // No preference writes or database activity.

            var home = new WpfMainDashboardShell();
            var root = (Grid)home.Content;
            home.Content = null; // Render the actual visual tree without showing an HWND.
            var cards = Field<UniformGrid>(home, "_cardsPanel");
            Check(cards.Children.Count == 8, "Developer sees all eight systems");
            var preferences = Field<object>(home, "_preferences");
            preferences.GetType().GetProperty("Recent").SetValue(preferences,
                new List<string> { "InventorySystem", "ITCallMonitoring", "Reports" });
            Call(home, "RenderRecent");
            Render(root, 1440, 960, "home-wide.png");
            Check(Grid.GetColumn(Field<StackPanel>(home, "_secondary")) == 1, "Wide layout has a separate recent/support column");
            Check(cards.Columns == 3, "Wide layout uses three readable system columns");
            Render(root, 1024, 768, "home-1024.png");
            Check(Field<StackPanel>(home, "_secondary").Visibility == Visibility.Collapsed, "Compact layout reserves space for all systems");
            Check(cards.Columns == 2, "1024px layout fits two system columns");
            Render(root, 820, 600, "home-820.png");
            Check(cards.Columns == 2, "Minimum width fits all systems in two compact columns");
            Check(Field<Grid>(home, "_workspace") != null, "Home uses a fixed grid instead of a scrolling body");
            foreach (var viewport in new[] { new Size(1440, 960), new Size(1366, 768), new Size(1024, 768), new Size(820, 600), new Size(804, 561) })
            {
                Render(root, (int)viewport.Width, (int)viewport.Height, "home-" + (int)viewport.Width + "-fit.png");
                foreach (Grid card in cards.Children)
                {
                    var open = (Button)card.Children[0];
                    var bounds = open.TransformToAncestor(root).TransformBounds(new Rect(open.RenderSize));
                    Check(bounds.Bottom <= viewport.Height - 30 && bounds.Top >= 76 && open.ActualHeight >= 40,
                        "System card fits at " + viewport.Width + "px: " + AutomationProperties.GetName(open));
                }
            }

            var search = Field<TextBox>(home, "_search");
            search.Text = "  RePoRtS  ";
            Check(cards.Children.Count == 1, "Search trims whitespace and ignores case");
            Render(root, 1440, 960, "home-search.png");
            var onlyCard = (Grid)cards.Children[0];
            Check(((Button)onlyCard.Children[0]).ActualHeight <= 210, "Single search result retains a readable card height");
            var pin = (Button)onlyCard.Children[1];
            var oldName = AutomationProperties.GetName(pin);
            pin.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(home.SelectedModule == PortalSelection.None, "Pinning does not launch a system");
            Check(AutomationProperties.GetName((Button)((Grid)cards.Children[0]).Children[1]) != oldName,
                "Pin state and accessible label update");
            search.Text = "not a system";
            Check(cards.Children.Count == 0 && Field<TextBlock>(home, "_emptyResults").Visibility == Visibility.Visible,
                "An unmatched search has a clear empty state");
            search.Clear();

            Call(home, "ShowHelpDialog");
            Check(root.Children.Count == 4, "Help opens inside the home overlay");
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, new PreviewSource(root), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            Call(home, "HandleShortcut", home, escape);
            Check(root.Children.Count == 3 && home.SelectedModule == PortalSelection.None,
                "Escape closes Help without logging out");
            search.Text = "report";
            Call(home, "HandleShortcut", home, new KeyEventArgs(Keyboard.PrimaryDevice, new PreviewSource(root), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(search.Text == "" && home.SelectedModule == PortalSelection.None, "Escape clears a search without logging out");

            var reports = cards.Children.Cast<Grid>().Select(g => (Button)g.Children[0])
                .First(b => AutomationProperties.GetName(b) == "Open Reports");
            reports.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(home.SelectedModule == PortalSelection.Reports, "Native system button returns the caller's existing portal selection");
            Check(((List<string>)preferences.GetType().GetProperty("Recent").GetValue(preferences))[0] == "Reports",
                "Launching moves the system to the front of recent history");

            AppSession.IsDeveloper = false;
            AppSession.CurrentUserRoles = new List<string> { "Requester" };
            var restricted = new WpfMainDashboardShell();
            Check(Field<UniformGrid>(restricted, "_cardsPanel").Children.Count == 1, "Requester only sees the permitted portal");
            Call(restricted, "SelectAndClose", PortalSelection.InventorySystem);
            Check(restricted.SelectedModule == PortalSelection.None, "An inaccessible launch is blocked");
            var restrictedRoot = (Grid)restricted.Content;
            restricted.Content = null;
            Render(restrictedRoot, 1280, 800, "home-requester.png");
            restricted.Close();

            // The app intentionally gives role-less accounts RequesterPortal access.
            // An unassigned role exercises the genuinely empty permission state.
            AppSession.CurrentUserRoles = new List<string> { "NoAssignedPortal" };
            var empty = new WpfMainDashboardShell();
            Check(Field<UniformGrid>(empty, "_cardsPanel").Children.Count == 0, "No-access account receives no system cards");
            Check(Field<TextBlock>(empty, "_emptyResults").Text.Contains("Contact IT support"), "No-access home explains how to request access");
            empty.Close();
            Console.WriteLine("All home checks passed. No windows shown; no database connections made.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void Render(Grid root, int width, int height, string name)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        // A second pass stabilizes width-dependent columns after SizeChanged.
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(Output, name))) png.Save(file);
    }

    sealed class PreviewSource : PresentationSource
    {
        Visual root;
        public PreviewSource(Visual visual) { root = visual; }
        public override Visual RootVisual { get => root; set => root = value; }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null;
    }
}
