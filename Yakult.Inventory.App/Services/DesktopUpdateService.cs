using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yakult.DesktopUpdates;

namespace Yakult.Inventory.App.Services
{
    public sealed class DesktopUpdateState
    {
        public DesktopRelease Release { get; internal set; }
        public bool Checking { get; internal set; }
        public string Message { get; internal set; }
    }

    /// <summary>One process-wide update check, independent of the signed-in portal or database.</summary>
    public sealed class DesktopUpdateService : IDisposable
    {
        public static DesktopUpdateService Instance { get; } = new DesktopUpdateService();
        public event Action Changed;
        public DesktopUpdateState State { get; private set; } = new DesktopUpdateState { Message = "Check for a new version of Yakult." };
        public Version InstalledVersion => UpdateProtocol.FileVersion(System.Windows.Forms.Application.ExecutablePath);
        public string SourceFolder { get; }
        private Timer _timer;
        private int _checking, _downloading;
        private string _pendingFolder;
        private string _completion;

        private DesktopUpdateService()
        {
            SourceFolder = ConfigurationManager.AppSettings["DesktopUpdateShare"] ?? UpdateProtocol.DefaultShare;
        }

        public void Start(string[] args)
        {
            LoadReceipt(args);
            _ = Task.Run(() => UpdateCache.Prune());
            _timer = new Timer(async _ => await CheckAsync(), null, TimeSpan.FromSeconds(8), TimeSpan.FromMinutes(15));
        }

        public async Task CheckAsync()
        {
            if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0) return;
            SetState(new DesktopUpdateState { Checking = true, Release = State.Release, Message = "Checking for updates…" });
            // A disconnected UNC share must not block the UI or accumulate polling workers.
            var read = Task.Run(() =>
            {
                try
                {
                    var manifestPath = Path.Combine(SourceFolder, "latest.json");
                    if (!File.Exists(manifestPath)) return new DesktopUpdateState { Message = "No release is published, or the update folder is unavailable. You can continue using Yakult." };
                    var release = UpdateProtocol.ReadRelease(manifestPath);
                    if (release == null) throw new InvalidDataException("The release information is empty.");
                    var target = release.Validate();
                    if (target <= InstalledVersion) return new DesktopUpdateState { Message = "You're up to date. Installed version: " + InstalledVersion };
                    return new DesktopUpdateState { Release = release, Message = "Version " + release.Version + " is available." };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[DesktopUpdate] " + ex);
                    return new DesktopUpdateState { Message = "Updates are temporarily unavailable. Your installed version is ready to use." };
                }
            });
            try
            {
                if (await Task.WhenAny(read, Task.Delay(8000)) != read)
                {
                    SetState(new DesktopUpdateState { Message = "The update folder is taking too long to respond. You can continue using Yakult." });
                    // Keep the gate until the network operation actually returns.
                    _ = read.ContinueWith(_ => Interlocked.Exchange(ref _checking, 0), TaskScheduler.Default);
                    return;
                }
                SetState(await read);
                Interlocked.Exchange(ref _checking, 0);
            }
            catch { Interlocked.Exchange(ref _checking, 0); throw; }
        }

        private void SetState(DesktopUpdateState state)
        {
            State = state;
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList()) { try { handler(); } catch (Exception ex) { Debug.WriteLine(ex); } }
        }

        public async Task<string> DownloadAsync(DesktopRelease release, IProgress<int> progress, CancellationToken cancellation)
        {
            if (!UpdateProtocol.IsRegisteredInstallation(System.Windows.Forms.Application.ExecutablePath))
                throw new InvalidOperationException("Install the updater-enabled Yakult release once using its setup EXE. Updates do not replace development builds or older installer identities.");
            release.Validate();
            if (release.Validate() <= InstalledVersion) throw new InvalidOperationException("This version is already installed.");
            if (Interlocked.CompareExchange(ref _downloading, 1, 0) != 0) throw new InvalidOperationException("Another update download is still finishing. Please try again shortly.");
            string folder = null;
            try
            {
                folder = UpdateCache.CreateFolder();
                await Task.Run(async () =>
                {
                    using (UpdateCache.Acquire(folder))
                    {
                        UpdateCache.Prune();
                        UpdateProtocol.StageRelease(Path.Combine(folder, "release.json"), release);
                        var source = Path.Combine(SourceFolder, release.Installer);
                        var destination = Path.Combine(folder, "installer.exe");
                        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            if (input.Length != release.SizeBytes) throw new InvalidDataException("The installer does not match the published release size.");
                            var buffer = new byte[81920]; long copied = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
                            {
                                copied += count;
                                if (copied > release.SizeBytes) throw new InvalidDataException("The installer changed during download.");
                                await output.WriteAsync(buffer, 0, count, cancellation).ConfigureAwait(false);
                                progress?.Report((int)(copied * 100L / release.SizeBytes));
                            }
                        }
                        cancellation.ThrowIfCancellationRequested();
                        UpdateProtocol.VerifyInstaller(destination, release);
                    }
                }, cancellation);
                return folder;
            }
            catch { if (folder != null) UpdateCache.Complete(folder); throw; }
            finally { Interlocked.Exchange(ref _downloading, 0); }
        }

        public async Task StartInstallerAsync(string folder, DesktopRelease release, CancellationToken cancellation)
        {
            UpdateProtocol.ValidateJobPath(Path.Combine(folder, "job.json"));
            UpdateProtocol.RequireSameRelease(release, UpdateProtocol.ReadRelease(Path.Combine(folder, "release.json")));
            var helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Yakult.DesktopUpdater.exe");
            if (!File.Exists(helper)) throw new FileNotFoundException("The updater component is missing. Reinstall the updater-enabled release once.");
            File.Copy(helper, Path.Combine(folder, "updater.exe"), false);
            using (var parent = Process.GetCurrentProcess())
                UpdateProtocol.WriteJson(Path.Combine(folder, "job.json"), new DesktopUpdateJob {
                    ApplicationPath = System.Windows.Forms.Application.ExecutablePath,
                    ParentProcessId = parent.Id, ParentStartedUtcTicks = parent.StartTime.ToUniversalTime().Ticks, Release = release
                });
            _pendingFolder = folder;
            try
            {
                using (var helperProcess = Process.Start(new ProcessStartInfo(Path.Combine(folder, "updater.exe"), "--job \"" + Path.Combine(folder, "job.json") + "\"") { UseShellExecute = false, WorkingDirectory = folder }))
                {
                    if (helperProcess == null) throw new IOException("The updater could not start.");
                    for (var i = 0; i < 80; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (File.Exists(Path.Combine(folder, "ready"))) return;
                        if (helperProcess.HasExited) throw new IOException("The updater stopped before it was ready. Your app has stayed open.");
                        await Task.Delay(250, cancellation);
                    }
                    throw new TimeoutException("The updater did not become ready. Your app has stayed open.");
                }
            }
            catch { CancelPendingInstall(); throw; }
        }

        public void CancelPendingInstall()
        {
            if (_pendingFolder == null) return;
            var folder = _pendingFolder;
            try { File.WriteAllText(Path.Combine(folder, "cancel"), "Application shutdown cancelled."); } catch { }
            _pendingFolder = null;
            UpdateCache.Complete(folder);
        }

        private void LoadReceipt(string[] args)
        {
            if (args == null || args.Length != 2 || args[0] != "--update-receipt") return;
            try
            {
                var receiptPath = Path.GetFullPath(args[1]);
                var jobPath = UpdateProtocol.ValidateJobPath(Path.Combine(Path.GetDirectoryName(receiptPath), "job.json"));
                if (Path.GetFileName(receiptPath) != "receipt.json") return;
                var receipt = UpdateProtocol.ReadJson<DesktopUpdateReceipt>(receiptPath);
                var job = UpdateProtocol.ReadJson<DesktopUpdateJob>(jobPath);
                if (receipt?.Succeeded != true || !string.Equals(Path.GetFullPath(receipt.ApplicationPath), Path.GetFullPath(System.Windows.Forms.Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)
                    || job?.Release == null || !string.Equals(job.ApplicationPath, receipt.ApplicationPath, StringComparison.OrdinalIgnoreCase)
                    || job.Release.Validate() != InstalledVersion || UpdateProtocol.ParseVersion(receipt.InstalledVersion) != InstalledVersion) return;
                File.Move(receiptPath, Path.Combine(Path.GetDirectoryName(receiptPath), "receipt-consumed.json"));
                _completion = "Congratulations! Your system is now running version " + InstalledVersion + ".";
            }
            catch (Exception ex) { Debug.WriteLine("[DesktopUpdate] Receipt ignored: " + ex.Message); }
        }

        public string TakeCompletionMessage() { var result = _completion; _completion = null; return result; }
        public void Dispose() { _timer?.Dispose(); _timer = null; }
    }
}
