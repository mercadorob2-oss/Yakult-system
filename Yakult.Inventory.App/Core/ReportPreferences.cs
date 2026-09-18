using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Per-user, JSON-backed memory for the two report dialogs: the signatories last picked for
    /// each role, and the column selections (last-used plus named presets) for each report.
    ///
    /// Deliberately NOT in Properties.Settings — that store is strongly typed and only holds
    /// scalars, whereas both of these are nested collections that grow at runtime (any number of
    /// presets, any number of signatory slots). Lives next to the log folder under
    /// %APPDATA%\YakultInventoryApp so it travels with the user, not the machine.
    ///
    /// Every operation is best-effort: a corrupt or unreadable file must never stop a report from
    /// being generated, so failures are logged and treated as "no preferences saved yet".
    /// </summary>
    public static class ReportPreferences
    {
        public sealed class SignatoryEntry
        {
            public string Name  { get; set; }
            public string Title { get; set; }
        }

        private sealed class PrefsFile
        {
            // role ("Prepared By", "Noted By", …) → the slots last used for it
            public Dictionary<string, List<SignatoryEntry>> Signatories { get; set; }
                = new Dictionary<string, List<SignatoryEntry>>(StringComparer.OrdinalIgnoreCase);

            // report name → last-checked column param names
            public Dictionary<string, List<string>> LastColumns { get; set; }
                = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // report name → preset name → checked column param names
            public Dictionary<string, Dictionary<string, List<string>>> ColumnPresets { get; set; }
                = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YakultInventoryApp",
            "report-preferences.json");

        private static readonly object _lock = new object();
        private static PrefsFile _cache;

        private static PrefsFile Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;
                try
                {
                    if (File.Exists(FilePath))
                        _cache = JsonConvert.DeserializeObject<PrefsFile>(File.ReadAllText(FilePath));
                }
                catch (Exception ex)
                {
                    Logger.LogError("ReportPreferences: could not read " + FilePath, ex);
                }
                return _cache ?? (_cache = new PrefsFile());
            }
        }

        private static void Save()
        {
            lock (_lock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(FilePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(FilePath, JsonConvert.SerializeObject(_cache, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    Logger.LogError("ReportPreferences: could not write " + FilePath, ex);
                }
            }
        }

        // ── Signatories ──────────────────────────────────────────────────────

        /// <summary>Slots last used for <paramref name="role"/>, or an empty list.</summary>
        public static List<SignatoryEntry> GetSignatories(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) role = "(default)";
            var p = Load();
            List<SignatoryEntry> list;
            return p.Signatories.TryGetValue(role, out list) && list != null
                ? new List<SignatoryEntry>(list)
                : new List<SignatoryEntry>();
        }

        public static void SetSignatories(string role, IEnumerable<SignatoryEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(role)) role = "(default)";
            var kept = (entries ?? Enumerable.Empty<SignatoryEntry>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Name))
                .ToList();
            if (kept.Count == 0) return;   // never overwrite a good memory with a blank one

            var p = Load();
            p.Signatories[role] = kept;
            Save();
        }

        /// <summary>
        /// Distinct names used across every role, most-recently-saved first — backs the
        /// "recently used" section at the top of the name dropdown.
        /// </summary>
        public static List<SignatoryEntry> GetRecentSignatories(int max = 8)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<SignatoryEntry>();
            foreach (var entry in Load().Signatories.Values.Where(v => v != null).SelectMany(v => v))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name)) continue;
                if (!seen.Add(entry.Name)) continue;
                result.Add(entry);
                if (result.Count >= max) break;
            }
            return result;
        }

        // ── Column selections ────────────────────────────────────────────────

        /// <summary>Param names checked the last time this report was generated, or null.</summary>
        public static HashSet<string> GetLastColumns(string report)
        {
            var p = Load();
            List<string> list;
            if (string.IsNullOrWhiteSpace(report) || !p.LastColumns.TryGetValue(report, out list) || list == null)
                return null;
            return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }

        public static void SetLastColumns(string report, IEnumerable<string> checkedParams)
        {
            if (string.IsNullOrWhiteSpace(report)) return;
            Load().LastColumns[report] = (checkedParams ?? Enumerable.Empty<string>()).ToList();
            Save();
        }

        public static List<string> GetPresetNames(string report)
        {
            var p = Load();
            Dictionary<string, List<string>> presets;
            if (string.IsNullOrWhiteSpace(report) || !p.ColumnPresets.TryGetValue(report, out presets) || presets == null)
                return new List<string>();
            return presets.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static HashSet<string> GetPreset(string report, string presetName)
        {
            var p = Load();
            Dictionary<string, List<string>> presets;
            List<string> cols;
            if (string.IsNullOrWhiteSpace(report) || string.IsNullOrWhiteSpace(presetName)) return null;
            if (!p.ColumnPresets.TryGetValue(report, out presets) || presets == null) return null;
            if (!presets.TryGetValue(presetName, out cols) || cols == null) return null;
            return new HashSet<string>(cols, StringComparer.OrdinalIgnoreCase);
        }

        public static void SavePreset(string report, string presetName, IEnumerable<string> checkedParams)
        {
            if (string.IsNullOrWhiteSpace(report) || string.IsNullOrWhiteSpace(presetName)) return;
            var p = Load();
            Dictionary<string, List<string>> presets;
            if (!p.ColumnPresets.TryGetValue(report, out presets) || presets == null)
                p.ColumnPresets[report] = presets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            presets[presetName.Trim()] = (checkedParams ?? Enumerable.Empty<string>()).ToList();
            Save();
        }

        public static void DeletePreset(string report, string presetName)
        {
            if (string.IsNullOrWhiteSpace(report) || string.IsNullOrWhiteSpace(presetName)) return;
            var p = Load();
            Dictionary<string, List<string>> presets;
            if (p.ColumnPresets.TryGetValue(report, out presets) && presets != null && presets.Remove(presetName))
                Save();
        }
    }
}
