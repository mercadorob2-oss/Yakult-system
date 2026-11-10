using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Yakult.DesktopUpdates
{
    public static class UpdateCache
    {
        private const int RetainedJobs = 5;
        private static readonly string[] JobFiles = { "job.json", "release.json", "ready", "cancel", "receipt.json", "receipt-consumed.json", "installer.log", "update.log", "finished" };

        public static string CreateFolder()
        {
            var folder = Path.Combine(UpdateProtocol.CacheRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            ValidateFolder(folder);
            return folder;
        }

        public static IDisposable Acquire(string folder)
        {
            folder = ValidateFolder(folder);
            return new FileStream(Path.Combine(folder, ".active"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }

        public static void Complete(string folder)
        {
            try
            {
                using (Acquire(folder))
                {
                    RemovePayload(folder);
                    MarkFinished(folder);
                }
            }
            catch (Exception ex) { Debug.WriteLine("[DesktopUpdate] Cache cleanup deferred: " + ex.Message); }
        }

        public static void Prune()
        {
            try
            {
                if (!Directory.Exists(UpdateProtocol.CacheRoot)) return;
                Prune(Directory.GetDirectories(UpdateProtocol.CacheRoot));
            }
            catch (Exception ex) { Debug.WriteLine("[DesktopUpdate] Cache scan deferred: " + ex.Message); }
        }

        public static void Prune(IEnumerable<string> folders)
        {
            try
            {
                var retained = 0;
                foreach (var entry in folders.OrderByDescending(JobTime))
                {
                    try
                    {
                        var folder = ValidateFolder(entry);
                        var finished = File.Exists(Path.Combine(folder, "finished"));
                        var time = JobTime(folder);
                        if (!finished && time > DateTime.UtcNow.AddDays(-1)) continue;
                        var remove = time < DateTime.UtcNow.AddDays(-30) || retained >= RetainedJobs;
                        using (Acquire(folder))
                        {
                            RemovePayload(folder);
                            // Never remove settings that still need recovery, or a recent unconsumed receipt.
                            if (Directory.EnumerateFiles(folder, "saved-*").Any()) continue;
                            if (File.Exists(Path.Combine(folder, "receipt.json")) && time > DateTime.UtcNow.AddDays(-30)) { retained++; continue; }
                            if (remove)
                                foreach (var name in JobFiles) DeleteFile(Path.Combine(folder, name));
                            else
                            {
                                if (!finished) MarkFinished(folder);
                                retained++;
                            }
                        }
                        if (remove) Directory.Delete(folder, false);
                    }
                    catch (Exception ex) { Debug.WriteLine("[DesktopUpdate] Cache entry retained: " + ex.Message); }
                }
            }
            catch (Exception ex) { Debug.WriteLine("[DesktopUpdate] Cache scan deferred: " + ex.Message); }
        }

        private static void RemovePayload(string folder)
        {
            DeleteFile(Path.Combine(folder, "installer.exe"));
            DeleteFile(Path.Combine(folder, "updater.exe")); // Windows keeps the running helper until the next scan.
        }

        private static DateTime JobTime(string folder)
        {
            var finished = Path.Combine(folder, "finished");
            return File.Exists(finished) ? File.GetLastWriteTimeUtc(finished) : Directory.GetCreationTimeUtc(folder);
        }

        private static void MarkFinished(string folder)
        {
            var path = Path.Combine(folder, "finished");
            if (!File.Exists(path))
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(output)) writer.Write(DateTime.UtcNow.ToString("O"));
        }

        private static void DeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string ValidateFolder(string folder)
        {
            var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetFullPath(UpdateProtocol.CacheRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Guid id;
            if (!string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(full), "N", out id))
                throw new InvalidDataException("Cache maintenance is limited to local Yakult update job folders.");
            for (var directory = new DirectoryInfo(full); directory != null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Cache maintenance does not follow directory junctions.");
            return full;
        }
    }
}
