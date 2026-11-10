using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Yakult.DesktopUpdates
{
    [DataContract]
    public sealed class SignedDesktopRelease
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "payload")] public string Payload { get; set; }
        [DataMember(Name = "signature")] public string Signature { get; set; }
    }

    [DataContract]
    public sealed class DesktopRelease
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "product")] public string Product { get; set; }
        [DataMember(Name = "version")] public string Version { get; set; }
        [DataMember(Name = "installer")] public string Installer { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
        [DataMember(Name = "sizeBytes")] public long SizeBytes { get; set; }
        [DataMember(Name = "releaseNotes")] public string ReleaseNotes { get; set; }
        [IgnoreDataMember] public SignedDesktopRelease SignedMetadata { get; private set; }

        internal void AttachSignature(SignedDesktopRelease metadata) { SignedMetadata = metadata; }

        public Version Validate()
        {
            if (SchemaVersion != 1 || Product != UpdateProtocol.Product)
                throw new InvalidDataException("This release is not for the Yakult desktop application.");
            var version = UpdateProtocol.ParseVersion(Version);
            if (string.IsNullOrWhiteSpace(Installer) || !Regex.IsMatch(Installer, @"\A[A-Za-z0-9][A-Za-z0-9._-]*\.exe\z", RegexOptions.IgnoreCase))
                throw new InvalidDataException("The release must name one installer EXE in the update folder.");
            if (string.IsNullOrWhiteSpace(Sha256) || !Regex.IsMatch(Sha256, @"\A[0-9a-fA-F]{64}\z"))
                throw new InvalidDataException("The release is missing its installer checksum.");
            if (SizeBytes <= 0 || SizeBytes > 1073741824L)
                throw new InvalidDataException("The installer size is invalid (maximum 1 GB).");
            if ((ReleaseNotes ?? "").Length > 16000) throw new InvalidDataException("The release notes are too long.");
            return version;
        }
    }

    [DataContract]
    public sealed class DesktopUpdateJob
    {
        [DataMember] public int ParentProcessId { get; set; }
        [DataMember] public long ParentStartedUtcTicks { get; set; }
        [DataMember] public string ApplicationPath { get; set; }
        [DataMember] public DesktopRelease Release { get; set; }
    }

    [DataContract]
    public sealed class DesktopUpdateReceipt
    {
        [DataMember] public string ApplicationPath { get; set; }
        [DataMember] public string InstalledVersion { get; set; }
        [DataMember] public bool Succeeded { get; set; }
    }

    public static class UpdateProtocol
    {
        public const string Product = "Yakult.Inventory.App";
        public const string ApplicationFile = "Yakult.Inventory.App.exe";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{98EE8B2E-10D8-4524-A66B-FE50D449AEA4}_is1";
        public const string DefaultShare = @"\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions";
        public static string CacheRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yakult", "DesktopUpdates");

        public static Version ParseVersion(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A\d{1,5}\.\d{1,5}(\.\d{1,5}){0,2}\z"))
                throw new InvalidDataException("Use a numeric release version, for example 1.6.1.0.");
            Version v;
            if (!Version.TryParse(value, out v) || v.Major > 65534 || v.Minor > 65534 || v.Build > 65534 || v.Revision > 65534)
                throw new InvalidDataException("The release version is outside the supported range.");
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        public static Version FileVersion(string path) => ParseVersion(FileVersionInfo.GetVersionInfo(path).FileVersion);

        public static IDisposable LockInstallation(string applicationPath)
        {
            try
            {
                return new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(applicationPath)), ".yakult-update.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (UnauthorizedAccessException ex) { throw new InvalidOperationException("This installation requires IT write access before it can be updated.", ex); }
            catch (IOException ex) { throw new InvalidOperationException("Another update is already using this installation. Please try again when it finishes.", ex); }
        }

        public static DesktopRelease ReadRelease(string path)
        {
            return VerifyRelease(ReadJson<SignedDesktopRelease>(path, 131072));
        }

        public static DesktopRelease VerifyRelease(SignedDesktopRelease envelope)
        {
            if (envelope?.SchemaVersion != 1 || envelope.Payload == null || envelope.Signature == null)
                throw new InvalidDataException("The release metadata is not signed.");
            var payload = Convert.FromBase64String(envelope.Payload);
            var signature = Convert.FromBase64String(envelope.Signature);
            if (payload.Length > 65536) throw new InvalidDataException("Release metadata is too large.");
            using (var keyStream = typeof(UpdateProtocol).Assembly.GetManifestResourceStream("Yakult.DesktopUpdates.ReleasePublicKey.xml"))
            using (var reader = new StreamReader(keyStream ?? throw new InvalidDataException("The trusted release key is missing.")))
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(reader.ReadToEnd());
                if (!rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), signature))
                    throw new InvalidDataException("The release signature is invalid. This update will not be installed.");
            }
            using (var input = new MemoryStream(payload))
            {
                var release = (DesktopRelease)new DataContractJsonSerializer(typeof(DesktopRelease)).ReadObject(input);
                if (release == null) throw new InvalidDataException("Release metadata is empty.");
                release.Validate();
                release.AttachSignature(envelope);
                return release;
            }
        }

        public static void RequireSameRelease(DesktopRelease expected, DesktopRelease actual)
        {
            if (expected == null || actual == null || expected.Validate() != actual.Validate()
                || expected.SizeBytes != actual.SizeBytes
                || !string.Equals(expected.Installer, actual.Installer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(expected.ReleaseNotes ?? "", actual.ReleaseNotes ?? "", StringComparison.Ordinal))
                throw new InvalidDataException("The signed release does not match the update you selected. Check for updates again.");
        }

        public static void StageRelease(string path, DesktopRelease release)
        {
            var verified = VerifyRelease(release?.SignedMetadata);
            RequireSameRelease(release, verified);
            WriteJson(path, verified.SignedMetadata);
        }

        public static bool IsRegisteredInstallation(string applicationPath)
        {
            var expected = Path.GetDirectoryName(Path.GetFullPath(applicationPath)).TrimEnd('\\');
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
                    using (var key = root.OpenSubKey(UninstallKey))
                    {
                        var location = key?.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(location) && string.Equals(Path.GetFullPath(location).TrimEnd('\\'), expected, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                catch (System.Security.SecurityException) { }
            }
            return false;
        }

        public static T ReadJson<T>(string path, int maximumBytes = 65536)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length <= 0 || input.Length > maximumBytes) throw new InvalidDataException("The update information is invalid or too large.");
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(input);
            }
        }

        public static void WriteJson<T>(string path, T value)
        {
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(T)).WriteObject(output, value);
        }

        public static string HashFile(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        public static void VerifyInstaller(string path, DesktopRelease release)
        {
            release.Validate();
            if (new FileInfo(path).Length != release.SizeBytes || !string.Equals(HashFile(path), release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installer is incomplete or changed. Download it again before updating.");
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (stream.Length < 64 || reader.ReadUInt16() != 0x5a4d) throw new InvalidDataException("The update is not a Windows installer.");
                stream.Position = 60;
                var offset = reader.ReadInt32();
                if (offset < 64 || offset > stream.Length - 4) throw new InvalidDataException("The installer executable is invalid.");
                stream.Position = offset;
                if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("The installer executable is invalid.");
            }
        }

        public static string ValidateJobPath(string path)
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(CacheRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(full) != "job.json")
                throw new InvalidDataException("The updater job must be in the current user's local update cache.");
            return full;
        }

        public static string InstallerArguments(string applicationPath, string logPath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(applicationPath));
            if (folder.Contains("\"") || logPath.Contains("\"")) throw new InvalidDataException("Invalid installation path.");
            return "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010 /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /DIR=\"" + folder + "\" /LOG=\"" + logPath + "\"";
        }
    }
}
