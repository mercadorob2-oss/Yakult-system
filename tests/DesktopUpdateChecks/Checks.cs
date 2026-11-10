using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Yakult.DesktopUpdates;

internal static class Checks
{
    private static int _passed;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); _passed++; }
    private static void Reject(Action action, string message) { bool failed = false; try { action(); } catch { failed = true; } Assert(failed, message); }
    private static DesktopRelease Release(string file) => new DesktopRelease { SchemaVersion = 1, Product = UpdateProtocol.Product, Version = "1.6.1.0", Installer = "YISP_Setup_1.6.1.0.exe", Sha256 = UpdateProtocol.HashFile(file), SizeBytes = new FileInfo(file).Length, ReleaseNotes = "A test release" };
    private const string ProductionJson = "{\"ConnectionStrings\":{\"DefaultConnection\":\"Server=production.example;Database=Production;Integrated Security=True\"}}";
    private static string ConfigurationXml(bool packaged) => "<configuration><appSettings><add key='ApiBaseUrl' value='" + (packaged ? "https://default.example" : "https://custom.example")
        + "'/><add key='DesktopUpdateShare' value='" + (packaged ? "default-share" : "custom-share") + "'/>" + (packaged ? "<add key='NewSetting' value='new-default'/>" : "")
        + "</appSettings><connectionStrings><add name='Database' connectionString='" + (packaged ? "packaged-default" : "local-connection") + "'/></connectionStrings>"
        + "<runtime><assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'><dependentAssembly><assemblyIdentity name='Example' publicKeyToken='0000000000000000' culture='neutral'/>"
        + "<bindingRedirect oldVersion='0.0.0.0-9.0.0.0' newVersion='" + (packaged ? "2.0.0.0" : "1.0.0.0") + "'/></dependentAssembly></assemblyBinding></runtime></configuration>";
    private static byte[] Executable()
    {
        var bytes = new byte[512]; bytes[0] = 0x4d; bytes[1] = 0x5a;
        BitConverter.GetBytes(128).CopyTo(bytes, 60); bytes[128] = 0x50; bytes[129] = 0x45; return bytes;
    }

    private sealed class Host : IUpdateHost
    {
        public bool Registered = true, OtherOpen, CancelParent;
        public int Code, Installs, Relaunches;
        public Version Before = new Version(1, 6, 0, 0), After = new Version(1, 6, 1, 0);
        public string Config, ExeConfig;
        private bool _installed;
        public bool IsRegistered(string path) => Registered;
        public Version InstalledVersion(string path) => _installed ? After : Before;
        public Task WaitForParentAsync(DesktopUpdateJob job, string folder)
        { if (CancelParent) throw new OperationCanceledException(); return Task.CompletedTask; }
        public void EnsureOtherCopiesClosed(string path) { if (OtherOpen) throw new InvalidOperationException("Other session open"); }
        public Task<int> InstallAsync(string installer, string args, string directory)
        {
            Installs++; _installed = true;
            Assert(args.Contains("/NORESTART") && args.Contains("/NOCLOSEAPPLICATIONS") && args.Contains("/DIR=\""), "Installer arguments must protect open sessions and select the existing installation.");
            Reject(() => File.AppendAllText(installer, "modified during execution"), "The verified installer must stay read-only throughout execution.");
            File.WriteAllText(Config, "installer default settings");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Config), "appsettings.Development.json"), "unexpected development database");
            File.WriteAllText(ExeConfig, ConfigurationXml(true));
            return Task.FromResult(Code);
        }
        public void Relaunch(string app, string receipt)
        { Relaunches++; Assert(UpdateProtocol.ReadJson<DesktopUpdateReceipt>(receipt).Succeeded, "Relaunch must carry a successful receipt."); }
    }

    private static async Task EngineCase(string scenario, string manifest)
    {
        var root = UpdateCache.CreateFolder();
        IDisposable competingUpdate = null;
        try
        {
            var appFolder = Path.Combine(root, "application with spaces"); Directory.CreateDirectory(appFolder);
            var app = Path.Combine(appFolder, UpdateProtocol.ApplicationFile); File.WriteAllBytes(app, Executable());
            var installer = Path.Combine(root, "installer.exe"); File.WriteAllBytes(installer, Executable());
            var host = new Host { Config = Path.Combine(appFolder, "appsettings.json"), ExeConfig = app + ".config" };
            File.WriteAllText(host.Config, ProductionJson);
            File.WriteAllText(host.ExeConfig, ConfigurationXml(false));
            var selected = UpdateProtocol.ReadRelease(manifest);
            UpdateProtocol.StageRelease(Path.Combine(root, "release.json"), selected);
            if (scenario == "installer failure") host.Code = 5;
            if (scenario == "reboot required") host.Code = 3010;
            if (scenario == "wrong version") host.After = host.Before;
            if (scenario == "cancelled shutdown") host.CancelParent = true;
            if (scenario == "another session") host.OtherOpen = true;
            if (scenario == "unregistered") host.Registered = false;
            if (scenario == "downgrade") host.Before = new Version(2, 0, 0, 0);
            if (scenario == "already installed") host.Before = host.After;
            if (scenario == "cancel marker") File.WriteAllText(Path.Combine(root, "cancel"), "cancel");
            if (scenario == "concurrent updater") competingUpdate = UpdateProtocol.LockInstallation(app);
            if (scenario == "missing manifest") File.Delete(Path.Combine(root, "release.json"));
            if (scenario == "unsigned manifest") { File.Delete(Path.Combine(root, "release.json")); UpdateProtocol.WriteJson(Path.Combine(root, "release.json"), selected); }
            if (scenario == "forged manifest")
            {
                var envelope = UpdateProtocol.ReadJson<SignedDesktopRelease>(manifest, 131072); envelope.Signature = Convert.ToBase64String(new byte[384]);
                File.Delete(Path.Combine(root, "release.json")); UpdateProtocol.WriteJson(Path.Combine(root, "release.json"), envelope);
            }
            if (scenario == "tampered job hash") selected.Sha256 = new string('a', 64);
            if (scenario == "tampered job version") selected.Version = "9.0.0.0";
            if (scenario == "tampered job notes") selected.ReleaseNotes = "Unapproved release";
            var jobPath = Path.Combine(root, "job.json");
            UpdateProtocol.WriteJson(jobPath, new DesktopUpdateJob { ApplicationPath = app, Release = selected });
            Exception failure = null;
            try
            {
                await new UpdateInstaller(host).RunAsync(jobPath, status => {
                    if (scenario == "tampered download" && status.StartsWith("Installing")) File.AppendAllText(installer, "changed");
                });
            }
            catch (Exception ex) { failure = ex; }
            if (scenario == "success")
            {
                Assert(failure == null && host.Installs == 1 && host.Relaunches == 1, "A valid update must install and relaunch once: " + failure);
                Assert(File.Exists(Path.Combine(root, "receipt.json")), "A verified update must write a receipt.");
                Assert(!File.Exists(Path.Combine(root, "saved-appsettings.json")), "Restored configuration backup must be removed.");
            }
            else
            {
                Assert(failure != null, scenario + " must fail safely.");
                Assert(host.Relaunches == 0 && !File.Exists(Path.Combine(root, "receipt.json")), scenario + " must not announce success or relaunch.");
                if (scenario != "installer failure" && scenario != "wrong version" && scenario != "reboot required") Assert(host.Installs == 0, scenario + " must not execute the installer.");
            }
            if (scenario.StartsWith("tampered job")) Assert(failure.Message.Contains("does not match"), "Job fields must be checked against independently verified metadata.");
            if (scenario == "forged manifest") Assert(failure.Message.Contains("signature is invalid"), "The helper must verify the signature itself.");
            if (scenario == "unsigned manifest") Assert(failure.Message.Contains("not signed"), "The helper must require signed metadata.");
            Assert(File.ReadAllText(host.Config) == ProductionJson, scenario + " must preserve the production database configuration.");
            Assert(!File.Exists(Path.Combine(appFolder, "appsettings.Development.json")), scenario + " must not introduce a database environment override.");
            var config = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = host.ExeConfig }, ConfigurationUserLevel.None);
            Assert(config.AppSettings.Settings["ApiBaseUrl"].Value == "https://custom.example" && config.AppSettings.Settings["DesktopUpdateShare"].Value == "custom-share", scenario + " must preserve API and update-source overrides.");
            Assert(config.ConnectionStrings.ConnectionStrings["Database"].ConnectionString == "local-connection", scenario + " must preserve connection strings.");
            if (host.Installs == 1 && host.Code == 0)
            {
                Assert(config.AppSettings.Settings["NewSetting"].Value == "new-default", "New app settings must receive their packaged defaults.");
                var xml = new XmlDocument(); xml.Load(host.ExeConfig);
                Assert(xml.SelectSingleNode("//*[local-name()='bindingRedirect']").Attributes["newVersion"].Value == "2.0.0.0", "New binding redirects must survive settings restoration.");
            }
            else Assert(File.ReadAllText(host.ExeConfig) == ConfigurationXml(false), "A failed or skipped install must preserve the original EXE configuration.");
            Assert(!File.Exists(installer), scenario + " must release cached installer bytes when the helper finishes.");
        }
        finally
        {
            competingUpdate?.Dispose();
            var expectedParent = Path.GetFullPath(UpdateProtocol.CacheRoot).TrimEnd('\\') + "\\";
            var absolute = Path.GetFullPath(root);
            Guid id;
            if (!absolute.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(absolute), "N", out id)) throw new Exception("Invalid test cleanup path.");
            Directory.Delete(absolute, true);
        }
    }

    private static void DeleteFixture(string folder)
    {
        var absolute = Path.GetFullPath(folder);
        Guid id;
        if (!string.Equals(Path.GetDirectoryName(absolute), Path.GetFullPath(UpdateProtocol.CacheRoot), StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(absolute), "N", out id)) throw new Exception("Invalid fixture cleanup path.");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
    }

    private static void CheckSettingsRecovery()
    {
        var root = UpdateCache.CreateFolder();
        try
        {
            var appFolder = Path.Combine(root, "application"); Directory.CreateDirectory(appFolder);
            var app = Path.Combine(appFolder, UpdateProtocol.ApplicationFile);
            var config = app + ".config";
            var existingEnvironment = Path.Combine(appFolder, "appsettings.Development.json");
            File.WriteAllText(config, ConfigurationXml(false));
            File.WriteAllText(existingEnvironment, "existing intentional environment");
            var backup = InstalledSettingsBackup.Capture(app, root);
            File.WriteAllText(config, "<invalid");
            File.WriteAllText(existingEnvironment, "installer replacement");
            Reject(() => backup.Restore(true), "A malformed installed configuration must fail restoration.");
            Assert(File.Exists(Path.Combine(root, "saved-" + Path.GetFileName(config))) && File.Exists(Path.Combine(root, "saved-appsettings.Development.json")), "Failed restoration must retain all recovery backups.");
            UpdateCache.Complete(root);
            File.SetLastWriteTimeUtc(Path.Combine(root, "finished"), DateTime.UtcNow.AddDays(-40));
            UpdateCache.Prune(new[] { root });
            Assert(File.Exists(Path.Combine(root, "saved-" + Path.GetFileName(config))), "Cache retention must preserve unresolved settings backups.");
            backup.Restore(false);
            Assert(File.ReadAllText(config) == ConfigurationXml(false) && File.ReadAllText(existingEnvironment) == "existing intentional environment", "A failed restoration must be recoverable without losing existing environment settings.");
            var newDefaultBackup = InstalledSettingsBackup.Capture(app, root);
            var baseSettings = Path.Combine(appFolder, "appsettings.json");
            File.WriteAllText(baseSettings, ProductionJson);
            File.WriteAllText(Path.Combine(appFolder, "appsettings.Unexpected.json"), "unexpected override");
            newDefaultBackup.Restore(true);
            Assert(File.ReadAllText(baseSettings) == ProductionJson, "An update must provision the selected base default on a PC that previously had no JSON configuration.");
            Assert(!File.Exists(Path.Combine(appFolder, "appsettings.Unexpected.json")), "New environment overrides must still be removed.");
            File.WriteAllText(config, ConfigurationXml(true));
            var previous = Path.Combine(root, "manual-previous.config"); File.WriteAllText(previous, ConfigurationXml(false));
            var helper = typeof(UpdateInstaller).Assembly.Location;
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper, "--merge-config \"" + previous + "\" \"" + config + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            {
                Assert(process.WaitForExit(10000) && process.ExitCode == 0, "Manual installer settings merge must work through the headless helper.");
            }
            var merged = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = config }, ConfigurationUserLevel.None);
            Assert(merged.AppSettings.Settings["DesktopUpdateShare"].Value == "custom-share" && merged.AppSettings.Settings["NewSetting"].Value == "new-default", "Manual upgrades must preserve overrides and add new defaults.");
            File.WriteAllText(previous, ConfigurationXml(false).Replace("ApiBaseUrl", "apibaseurl"));
            File.WriteAllText(config, ConfigurationXml(true));
            InstalledSettingsBackup.MergeConfiguration(previous, config);
            merged = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = config }, ConfigurationUserLevel.None);
            Assert(merged.AppSettings.Settings.AllKeys.Count(key => key.Equals("ApiBaseUrl", StringComparison.OrdinalIgnoreCase)) == 1 && merged.AppSettings.Settings["ApiBaseUrl"].Value == "https://custom.example", "Settings merging must not introduce duplicate keys with different casing.");
            File.WriteAllText(previous, "<configuration><appSettings configSource='external.config'/></configuration>");
            File.WriteAllText(config, ConfigurationXml(true));
            File.WriteAllText(Path.Combine(appFolder, "external.config"), "<appSettings><add key='ApiBaseUrl' value='https://external.example'/></appSettings>");
            InstalledSettingsBackup.MergeConfiguration(previous, config);
            merged = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = config }, ConfigurationUserLevel.None);
            Assert(merged.AppSettings.Settings["ApiBaseUrl"].Value == "https://external.example", "An existing external settings section must remain authoritative and valid.");
        }
        finally { DeleteFixture(root); }
    }

    private static void CheckCache()
    {
        var folders = new List<string>();
        Func<string> create = () => { var folder = UpdateCache.CreateFolder(); folders.Add(folder); return folder; };
        try
        {
            var completed = create();
            File.WriteAllBytes(Path.Combine(completed, "installer.exe"), Executable());
            File.WriteAllText(Path.Combine(completed, "updater.exe"), "unused helper");
            File.WriteAllText(Path.Combine(completed, "receipt.json"), "pending receipt");
            File.WriteAllText(Path.Combine(completed, "installer.log"), "diagnostic log");
            UpdateCache.Complete(completed);
            Assert(!File.Exists(Path.Combine(completed, "installer.exe")) && !File.Exists(Path.Combine(completed, "updater.exe")), "Finished jobs must remove installer and helper payloads.");
            Assert(File.Exists(Path.Combine(completed, "receipt.json")) && File.Exists(Path.Combine(completed, "installer.log")), "Cleanup must retain completion receipts and diagnostic logs.");

            var active = create();
            File.WriteAllBytes(Path.Combine(active, "installer.exe"), Executable());
            File.WriteAllText(Path.Combine(active, "finished"), "finished");
            File.SetLastWriteTimeUtc(Path.Combine(active, "finished"), DateTime.UtcNow.AddDays(-40));
            using (UpdateCache.Acquire(active))
            {
                UpdateCache.Complete(active); UpdateCache.Prune(new[] { active });
                Assert(File.Exists(Path.Combine(active, "installer.exe")), "Cache maintenance must never remove an active job's payload.");
            }
            UpdateCache.Prune(new[] { active });
            Assert(!Directory.Exists(active), "An expired inactive job must be removed after releasing its lease.");

            var stale = create(); File.WriteAllBytes(Path.Combine(stale, "installer.exe"), Executable());
            Directory.SetCreationTimeUtc(stale, DateTime.UtcNow.AddDays(-2));
            UpdateCache.Prune(new[] { stale });
            Assert(!File.Exists(Path.Combine(stale, "installer.exe")), "Abandoned downloads must release payload bytes.");

            var retained = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                var folder = create(); retained.Add(folder); UpdateCache.Complete(folder);
                File.WriteAllText(Path.Combine(folder, "installer.log"), "diagnostic log");
                File.SetLastWriteTimeUtc(Path.Combine(folder, "finished"), DateTime.UtcNow.AddMinutes(-i));
            }
            UpdateCache.Prune(retained.Concat(new[] { completed }));
            Assert(File.Exists(Path.Combine(completed, "receipt.json")), "A recent unconsumed receipt must survive retention limits.");
            Assert(retained.Count(Directory.Exists) <= 5, "Cache retention must bound completed diagnostic folders.");
            Assert(Directory.Exists(retained[0]) && !Directory.Exists(retained[6]), "Retention must prefer the newest completed jobs.");

            var unknown = create(); UpdateCache.Complete(unknown);
            var child = Path.Combine(unknown, "unrecognized"); Directory.CreateDirectory(child);
            var sentinel = Path.Combine(child, "keep.txt"); File.WriteAllText(sentinel, "do not delete");
            File.SetLastWriteTimeUtc(Path.Combine(unknown, "finished"), DateTime.UtcNow.AddDays(-40));
            UpdateCache.Prune(new[] { unknown });
            Assert(File.Exists(sentinel), "Cache maintenance must not recursively delete unknown contents.");
            UpdateCache.Complete(child); UpdateCache.Prune(new[] { child });
            Assert(File.Exists(sentinel), "Cache cleanup must reject paths outside direct GUID-named job folders.");
        }
        finally { foreach (var folder in folders) DeleteFixture(folder); }
    }

    [STAThread]
    private static int Main(string[] args) => RunAsync(args).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "--preview") { Preview.Run(args[1], args[2]); return 0; }
            if (args.Length == 2 && args[0] == "--verify-package")
            {
                var package = UpdateProtocol.ReadRelease(args[1]);
                UpdateProtocol.VerifyInstaller(Path.Combine(Path.GetDirectoryName(args[1]), package.Installer), package);
                Console.WriteLine("PASS: built installer " + package.Version + ", release signature, byte length, and SHA256 verified by the client protocol.");
                return 0;
            }
            Assert(args.Length == 1, "Pass an isolated signed release manifest.");
            var manifest = args[0];
            var release = UpdateProtocol.ReadRelease(manifest);
            var modified = UpdateProtocol.ReadRelease(manifest); modified.Version = "9.0.0.0";
            Reject(() => UpdateProtocol.StageRelease(Path.Combine(Path.GetDirectoryName(manifest), "modified-release.json"), modified), "Download staging must reject changed fields before trusting metadata.");
            Reject(() => UpdateProtocol.StageRelease(Path.Combine(Path.GetDirectoryName(manifest), "unsigned-release.json"), Release(Path.Combine(Path.GetDirectoryName(manifest), release.Installer))), "Download staging must require the original signature.");
            Assert(release.Validate() == new Version(1, 6, 1, 0), "Published release must be readable by the updater.");
            Assert(UpdateProtocol.ParseVersion("1.6") == new Version(1, 6, 0, 0), "Version normalization failed.");
            Reject(() => UpdateProtocol.ParseVersion("1.6-beta"), "Prerelease text is not a numeric file version.");
            Reject(() => UpdateProtocol.ParseVersion("65535.1.0.0"), "Out of range version accepted.");
            foreach (var name in new[] { "..\\evil.exe", "../evil.exe", "C:\\evil.exe", "\\\\host\\evil.exe", "setup.exe /SILENT", "setup.cmd" })
            { var value = new DesktopRelease { SchemaVersion = 1, Product = UpdateProtocol.Product, Version = "2.0", Installer = name, Sha256 = new string('a', 64), SizeBytes = 512 }; Reject(() => value.Validate(), "Unsafe installer path accepted: " + name); }
            var wrongProduct = new DesktopRelease { SchemaVersion = 1, Product = "OtherApp", Version = "2.0", Installer = "setup.exe", Sha256 = new string('a', 64), SizeBytes = 512 };
            Reject(() => wrongProduct.Validate(), "Wrong product accepted.");
            Reject(() => UpdateProtocol.ValidateJobPath(Path.Combine(Path.GetTempPath(), "job.json")), "External updater job accepted.");
            var directory = Path.GetDirectoryName(manifest);
            var forged = Path.Combine(directory, "forged.json"); var envelope = UpdateProtocol.ReadJson<SignedDesktopRelease>(manifest, 131072);
            envelope.Signature = Convert.ToBase64String(new byte[384]); UpdateProtocol.WriteJson(forged, envelope);
            Reject(() => UpdateProtocol.ReadRelease(forged), "Forged signature accepted.");
            var unsigned = Path.Combine(directory, "unsigned.json"); UpdateProtocol.WriteJson(unsigned, release);
            Reject(() => UpdateProtocol.ReadRelease(unsigned), "Unsigned manifest accepted.");
            var fixture = Path.Combine(directory, release.Installer);
            UpdateProtocol.VerifyInstaller(fixture, release); _passed++;
            var original = File.ReadAllBytes(fixture); var changed = (byte[])original.Clone(); changed[200] ^= 1; File.WriteAllBytes(fixture, changed);
            Reject(() => UpdateProtocol.VerifyInstaller(fixture, release), "Same-size installer tampering accepted."); File.WriteAllBytes(fixture, original);
            foreach (var scenario in new[] { "success", "installer failure", "reboot required", "wrong version", "cancelled shutdown", "another session", "unregistered", "downgrade", "already installed", "cancel marker", "tampered download", "concurrent updater", "missing manifest", "unsigned manifest", "forged manifest", "tampered job hash", "tampered job version", "tampered job notes" }) await EngineCase(scenario, manifest);
            CheckSettingsRecovery();
            CheckCache();
            Console.WriteLine("PASS: " + _passed + " desktop updater checks. No real installer, app session, or business database was used."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
