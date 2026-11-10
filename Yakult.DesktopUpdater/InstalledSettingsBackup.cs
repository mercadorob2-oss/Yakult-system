using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Yakult.DesktopUpdates
{
    public sealed class InstalledSettingsBackup
    {
        private readonly string _applicationPath, _configBackup;
        private readonly Dictionary<string, string> _jsonBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private InstalledSettingsBackup(string applicationPath, string configBackup)
        {
            _applicationPath = applicationPath;
            _configBackup = configBackup;
        }

        public static InstalledSettingsBackup Capture(string applicationPath, string folder)
        {
            var config = applicationPath + ".config";
            var configBackup = File.Exists(config) ? Path.Combine(folder, "saved-" + Path.GetFileName(config)) : null;
            var result = new InstalledSettingsBackup(applicationPath, configBackup);
            if (configBackup != null)
            {
                ReadConfiguration(config); // Fail before installation if existing settings cannot be read.
                File.Copy(config, configBackup, false);
            }
            foreach (var json in Directory.EnumerateFiles(Path.GetDirectoryName(applicationPath), "appsettings*.json"))
            {
                var backup = Path.Combine(folder, "saved-" + Path.GetFileName(json));
                File.Copy(json, backup, false);
                result._jsonBackups.Add(json, backup);
            }
            return result;
        }

        public void Restore(bool installationSucceeded)
        {
            foreach (var backup in _jsonBackups)
            {
                File.Copy(backup.Value, backup.Key, true);
            }
            // The selected base default may be introduced on PCs that previously had no JSON file.
            // Extra environment files must not override that default or an existing connection.
            foreach (var json in Directory.EnumerateFiles(Path.GetDirectoryName(_applicationPath), "appsettings*.json"))
                if (!_jsonBackups.ContainsKey(json) && !string.Equals(Path.GetFileName(json), "appsettings.json", StringComparison.OrdinalIgnoreCase)) File.Delete(json);

            if (_configBackup != null)
            {
                var config = _applicationPath + ".config";
                if (installationSucceeded && File.Exists(config)) MergeConfiguration(_configBackup, config);
                else File.Copy(_configBackup, config, true);
            }
            // Keep every backup until all restoration steps succeed, so recovery can be retried.
            foreach (var backup in _jsonBackups.Values) File.Delete(backup);
            if (_configBackup != null) File.Delete(_configBackup);
        }

        public static void MergeConfiguration(string previousPath, string installedPath)
        {
            var previous = ReadConfiguration(previousPath);
            var installed = ReadConfiguration(installedPath);
            foreach (var name in new[] { "appSettings", "connectionStrings" })
            {
                var oldSection = previous.DocumentElement.SelectSingleNode(name) as XmlElement;
                if (oldSection == null) continue;
                var newSection = installed.DocumentElement.SelectSingleNode(name) as XmlElement;
                var merged = (XmlElement)installed.ImportNode(oldSection, true);
                // External section files remain authoritative. Inline sections gain newly introduced defaults.
                if (!merged.HasAttribute("configSource") && !merged.HasAttribute("configProtectionProvider") && newSection != null)
                {
                    var identity = name == "appSettings" ? "key" : "name";
                    foreach (XmlNode node in newSection.ChildNodes)
                    {
                        var entry = node as XmlElement;
                        if (entry == null || entry.Name != "add") continue;
                        var found = false;
                        foreach (XmlNode oldNode in merged.ChildNodes)
                        {
                            var oldEntry = oldNode as XmlElement;
                            if (oldEntry?.Name == "add" && string.Equals(oldEntry.GetAttribute(identity), entry.GetAttribute(identity), StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                        }
                        if (!found) merged.AppendChild(installed.ImportNode(entry, true));
                    }
                }
                if (newSection == null) installed.DocumentElement.AppendChild(merged);
                else installed.DocumentElement.ReplaceChild(merged, newSection);
            }
            // Save beside the target so replacement is atomic and the new runtime/binding redirects survive.
            var temporary = installedPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) installed.Save(output);
                File.Replace(temporary, installedPath, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static XmlDocument ReadConfiguration(string path)
        {
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
            if (document.DocumentElement?.Name != "configuration") throw new InvalidDataException("The application configuration is invalid.");
            return document;
        }
    }
}
