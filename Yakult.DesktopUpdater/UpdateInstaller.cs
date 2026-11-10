using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Yakult.DesktopUpdates
{
    public interface IUpdateHost
    {
        bool IsRegistered(string applicationPath);
        Version InstalledVersion(string applicationPath);
        Task WaitForParentAsync(DesktopUpdateJob job, string folder);
        void EnsureOtherCopiesClosed(string applicationPath);
        Task<int> InstallAsync(string installer, string arguments, string workingDirectory);
        void Relaunch(string applicationPath, string receipt);
    }

    public sealed class UpdateInstaller
    {
        private readonly IUpdateHost _host;
        public UpdateInstaller(IUpdateHost host) { _host = host; }

        public async Task RunAsync(string jobPath, Action<string> status)
        {
            jobPath = UpdateProtocol.ValidateJobPath(jobPath);
            var folder = Path.GetDirectoryName(jobPath);
            try
            {
                using (UpdateCache.Acquire(folder)) await InstallJobAsync(jobPath, folder, status);
            }
            finally { UpdateCache.Complete(folder); }
        }

        private async Task InstallJobAsync(string jobPath, string folder, Action<string> status)
        {
            var job = UpdateProtocol.ReadJson<DesktopUpdateJob>(jobPath);
            if (job?.Release == null || string.IsNullOrWhiteSpace(job.ApplicationPath) || !Path.IsPathRooted(job.ApplicationPath)
                || !string.Equals(Path.GetFileName(job.ApplicationPath), UpdateProtocol.ApplicationFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The update job does not identify a Yakult installation.");
            // The cache job supplies process information, never the authority to execute an installer.
            var release = UpdateProtocol.ReadRelease(Path.Combine(folder, "release.json"));
            UpdateProtocol.RequireSameRelease(job.Release, release);
            var target = release.Validate();
            var appPath = Path.GetFullPath(job.ApplicationPath);
            if (!File.Exists(appPath)) throw new FileNotFoundException("The Yakult installation could not be found.");
            if (!_host.IsRegistered(appPath)) throw new InvalidOperationException("Install the updater-enabled Yakult release once before using automatic updates.");
            using (UpdateProtocol.LockInstallation(appPath))
            {
                if (_host.InstalledVersion(appPath) >= target) throw new InvalidOperationException("This release is not newer than the installed application.");
                var installer = Path.Combine(folder, "installer.exe");
                await Task.Run(() => UpdateProtocol.VerifyInstaller(installer, release));
                File.WriteAllText(Path.Combine(folder, "ready"), "ready");
                status("Waiting for Yakult to close safely…");
                await _host.WaitForParentAsync(job, folder);
                if (File.Exists(Path.Combine(folder, "cancel"))) throw new OperationCanceledException("The application stayed open. The update was cancelled.");
                _host.EnsureOtherCopiesClosed(appPath);
                var settings = InstalledSettingsBackup.Capture(appPath, folder);
                var installed = false;
                try
                {
                    status("Installing version " + release.Version + "…\nPlease keep this window open.");
                    // Keep the verified bytes read-only until the installer exits.
                    using (var installerLock = new FileStream(installer, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await Task.Run(() => UpdateProtocol.VerifyInstaller(installer, release));
                        var code = await _host.InstallAsync(installer, UpdateProtocol.InstallerArguments(appPath, Path.Combine(folder, "installer.log")), folder);
                        if (code != 0) throw new InvalidOperationException("The installer did not complete (code " + code + "). See installer.log in " + folder);
                        installed = true;
                    }
                }
                finally { settings.Restore(installed); }
                if (_host.InstalledVersion(appPath) != target)
                    throw new InvalidOperationException("The installed version does not match this release. The update has not been marked complete.");
                var receipt = Path.Combine(folder, "receipt.json");
                UpdateProtocol.WriteJson(receipt, new DesktopUpdateReceipt { ApplicationPath = appPath, InstalledVersion = release.Version, Succeeded = true });
                status("Update complete! Opening Yakult " + release.Version + "…");
                _host.Relaunch(appPath, receipt);
            }
        }
    }

    public sealed class WindowsUpdateHost : IUpdateHost
    {
        public bool IsRegistered(string path) => UpdateProtocol.IsRegisteredInstallation(path);
        public Version InstalledVersion(string path) => UpdateProtocol.FileVersion(path);

        public async Task WaitForParentAsync(DesktopUpdateJob job, string folder)
        {
            Process parent;
            try { parent = Process.GetProcessById(job.ParentProcessId); }
            catch (ArgumentException) { return; }
            using (parent)
            {
                if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartedUtcTicks) return;
                for (var i = 0; i < 180; i++)
                {
                    if (File.Exists(Path.Combine(folder, "cancel"))) throw new OperationCanceledException("The update was cancelled because the application stayed open.");
                    if (parent.HasExited) return;
                    await Task.Delay(500);
                }
                throw new TimeoutException("Yakult is still open. Save your work, close its windows, and try the update again.");
            }
        }

        public void EnsureOtherCopiesClosed(string appPath)
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appPath)))
            using (process)
            {
                if (process.HasExited) continue;
                string otherPath;
                try { otherPath = process.MainModule.FileName; }
                catch { throw new InvalidOperationException("Another Yakult session is running. Close other sessions before updating."); }
                if (string.Equals(Path.GetFullPath(otherPath), appPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Another Yakult window is using this installation. Close it before updating.");
            }
        }

        public async Task<int> InstallAsync(string installer, string arguments, string folder)
        {
            using (var process = Process.Start(new ProcessStartInfo(installer, arguments) { UseShellExecute = false, WorkingDirectory = folder }))
            {
                if (process == null) throw new IOException("The installer could not start.");
                await Task.Run(() => process.WaitForExit());
                return process.ExitCode;
            }
        }

        public void Relaunch(string appPath, string receipt) => Process.Start(new ProcessStartInfo(appPath, "--update-receipt \"" + receipt + "\"") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(appPath) });
    }
}
