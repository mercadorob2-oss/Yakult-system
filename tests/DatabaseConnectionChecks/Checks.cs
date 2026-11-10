using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.Admin.Database;

internal static class Checks
{
    private static int _passed;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); _passed++; }
    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
    private static void Reject(Action action, string message) { var rejected = false; try { action(); } catch { rejected = true; } Assert(rejected, message); }
    private sealed class RejectDeveloper : IDeveloperConnectionAuthorizer { public int Calls; public Task<bool> VerifyAsync(string user, string password) { Calls++; return Task.FromResult(false); } }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Assert(args.Length == 1, "Pass an isolated preview output folder."); Directory.CreateDirectory(args[0]);
            CheckProfiles(); CheckDeveloperVerification(); CheckWindow(args[0]);
            var loader = typeof(DatabaseConfig).Assembly.GetType("Yakult.Inventory.App.Core.AppSettingsLoader");
            var defaultConnection = (string)loader.GetMethod("LoadConnectionString", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var configured = new SqlConnectionStringBuilder(defaultConnection);
            Assert(configured.InitialCatalog == DatabaseConnectionProfile.DefaultDatabase, "The packaged default must select Yakult_Inventory_System_DEV.");
            Assert(configured.DataSource == DatabaseConnectionProfile.DefaultServer, "The packaged default must select the intended SQL host/port.");
            Console.WriteLine("PASS: " + _passed + " database connection checks; rendered masked/revealed properties and developer verification. No SQL connection, settings save, app launch, or session switch was performed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static string Fixture(string database)
    {
        return new SqlConnectionStringBuilder { DataSource = "sql-preview.example,1433", InitialCatalog = database, UserID = "preview_user", Password = "preview-only;=test", Encrypt = false, TrustServerCertificate = true, ApplicationName = "Connection preview", Pooling = false }.ConnectionString;
    }
    private static void CheckProfiles()
    {
        var profile = new DatabaseConnectionProfile(null);
        Assert(profile.Server == "192.168.100.186,50301" && profile.Database == "Yakult_Inventory_System_DEV", "New connection fields must start with the intended working database.");
        Assert(profile.EnvironmentLabel == "Working database", "The historical _DEV suffix must not classify the working database as dummy.");
        profile = new DatabaseConnectionProfile(Fixture(DatabaseConnectionProfile.DummyDatabase));
        Assert(profile.EnvironmentLabel == "Dummy database", "Only the known dummy catalog should have the dummy indicator.");
        profile.Database = "SomeOtherCatalog"; Assert(profile.EnvironmentLabel == "Custom database", "Unknown catalogs must be labeled explicitly.");
        var parsed = new SqlConnectionStringBuilder(profile.BuildConnectionString());
        Assert(parsed.Password == "preview-only;=test", "The connection builder must preserve password punctuation safely.");
        Assert(parsed.ApplicationName == "Connection preview" && !parsed.Pooling, "Editing must preserve advanced connection options.");
        Assert(profile.GetAllProperties().Any(p => p.Name == "Password" && p.Value == "preview-only;=test"), "Authorized all-properties data must include the password.");
        profile.WindowsAuthentication = true; parsed = new SqlConnectionStringBuilder(profile.BuildConnectionString());
        Assert(parsed.IntegratedSecurity && parsed.Password == "" && parsed.UserID == "", "Windows authentication must not carry SQL credentials.");
        profile.Server = " "; Reject(() => profile.BuildConnectionString(), "An empty server must not be saved.");
        profile.Server = "preview"; profile.Database = " "; Reject(() => profile.BuildConnectionString(), "An empty database must not be saved.");
        profile.Database = "test"; profile.WindowsAuthentication = false; profile.Password = ""; Reject(() => profile.BuildConnectionString(), "SQL authentication must require its password.");
    }
    private static void CheckDeveloperVerification()
    {
        const string password = "developer-test-only";
        var salt = PasswordHelper.GenerateSalt(); var hash = PasswordHelper.HashPassword(password, salt);
        Assert(DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, hash, salt, false, password), "An active developer with a correct salted password must be authorized.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, false, hash, salt, true, password), "A correct non-developer password must never reveal connection values.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(false, true, hash, salt, true, password), "Inactive developers must be denied.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, hash, salt, true, "wrong"), "Legacy storage must not bypass an existing salted password.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, hash, null, true, password), "Incomplete salted credentials must fail closed.");
        Assert(DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, null, null, true, password), "Existing legacy developer credentials must remain supported.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, null, null, false, password), "An incorrect legacy password must be denied.");
        Assert(!DeveloperConnectionAuthorizer.VerifyStoredDeveloper(true, true, hash, salt, true, ""), "A blank developer password must be denied.");
        var authorizer = new DeveloperConnectionAuthorizer("invalid connection never used");
        Assert(!authorizer.VerifyAsync("", password).GetAwaiter().GetResult(), "A blank developer username must fail before opening a connection.");
        Assert(!authorizer.VerifyAsync(new string('a', 257), password).GetAwaiter().GetResult(), "Oversized developer input must fail before opening a connection.");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var descendant in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return descendant;
    }
    private static void CheckWindow(string output)
    {
        var authorizer = new RejectDeveloper(); var session = AppSession.CurrentUserId;
        var window = new DatabaseConnectionWindow(Fixture(DatabaseConnectionProfile.DummyDatabase), Fixture(DatabaseConnectionProfile.DefaultDatabase), "Saved override on this PC", authorizer);
        var raw = (TextBox)Get(window, "_connectionString"); var properties = (DataGrid)Get(window, "_properties"); var save = (Button)Get(window, "_save");
        Render((FrameworkElement)window.Content, 780, 690, Path.Combine(output, "connection-masked.png"));
        Assert(raw.Text == "" && properties.ItemsSource == null && !save.IsEnabled, "Connection details and save must start locked.");
        Assert(Descendants<TextBox>((DependencyObject)window.Content).All(t => !t.Text.Contains("preview-only") && !t.Text.Contains("sql-preview")), "Plaintext controls must not contain protected values while locked.");
        Assert(((TextBlock)Get(window, "_environment")).Text == "Dummy database", "The active dummy connection must be visible through its environment label.");
        Call(window, "LoadDefault");
        Assert(((DatabaseConnectionProfile)Get(window, "_profile")).Database == DatabaseConnectionProfile.DefaultDatabase, "Use default must replace the draft with the working database.");
        Assert(!save.IsEnabled && Get(window, "_testedConnection") == null, "Loading the default must require a new connection test.");
        Call(window, "SetRevealed", true);
        Assert(raw.Text.Contains("Yakult_Inventory_System_DEV") && raw.Text.Contains("preview-only") && properties.ItemsSource != null, "The approved details view must show the complete selected connection.");
        Render((FrameworkElement)window.Content, 780, 1100, Path.Combine(output, "connection-revealed.png"));
        Call(window, "SetRevealed", false);
        Assert(raw.Text == "" && properties.ItemsSource == null, "Hide all must remove raw credentials and detail rows.");
        Assert(Descendants<TextBox>((DependencyObject)window.Content).All(t => !t.Text.Contains("preview-only")), "Hide all must clear plaintext input copies.");
        var serverField = Get(window, "_server");
        typeof(DatabaseConnectionWindow).GetField("_testedConnection", PrivateInstance).SetValue(window, "previous test"); Call(window, "SetBusy", false);
        Assert(save.IsEnabled, "A completed connection test enables saving.");
        ((PasswordBox)Get(serverField, "_masked")).Password = "changed-preview.example";
        Assert(!save.IsEnabled && Get(window, "_testedConnection") == null, "Editing after a test must invalidate the saved test result.");
        window.Close();
        Assert((string)serverField.GetType().GetProperty("Value").GetValue(serverField) == "", "Closing must clear field contents.");

        var prompt = new DeveloperPasswordWindow(authorizer);
        ((TextBox)Get(prompt, "_username")).Text = "non-developer"; ((PasswordBox)Get(prompt, "_password")).Password = "wrong-password";
        ((Task)Call(prompt, "UnlockAsync")).GetAwaiter().GetResult();
        Assert(authorizer.Calls == 1 && prompt.DialogResult != true, "Failed developer verification must not approve View all.");
        Assert(((PasswordBox)Get(prompt, "_password")).Password == "", "Developer passwords must be cleared after submission.");
        Render((FrameworkElement)prompt.Content, 430, 355, Path.Combine(output, "developer-verification.png")); prompt.Close();
        Assert(AppSession.CurrentUserId == session, "Viewing connection properties must not change the signed-in account.");
        var recovery = new DatabaseConnectionWindow("invalid", "also invalid", "Manual setup", authorizer);
        Assert(((DatabaseConnectionProfile)Get(recovery, "_profile")).Database == DatabaseConnectionProfile.DefaultDatabase, "Malformed saved/default strings must still open editable connection recovery with the intended catalog.");
        recovery.Close();
    }
    private static void Render(FrameworkElement content, int width, int height, string path)
    {
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        bitmap.Render(background); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
    }
}
