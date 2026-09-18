using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Wpf.OwnershipHistory
{
    /// <summary>One readable "Field: old → new" row built from an AuditEntry's OldValues/NewValues JSON.</summary>
    public class ChangeLine
    {
        public string Label { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public bool IsPlainNote { get; set; }

        /// <summary>
        /// True when this field did not actually change value but is still shown for context
        /// (e.g. the Company/Branch/Department a Set already sat under, carried through an
        /// Employee ↔ Dept level transfer that only touched the Employee field).
        /// </summary>
        public bool IsUnchanged { get; set; }

        /// <summary>True when this line is an actual old → new change (not a plain note, not context).</summary>
        public bool IsRealDiff => !IsPlainNote && !IsUnchanged;
    }

    /// <summary>One step (e.g. "Ownership Transfer Initiated") in a Set's ItemAuditTrail-based transfer cycle.</summary>
    public class TransferCycleStep
    {
        public DateTime Timestamp { get; set; }
        public string Header { get; set; }
        public string Detail { get; set; }
        public Brush AccentBrush { get; set; }
    }

    /// <summary>
    /// Shared formatting for the Ownership/Site history grid and its "Full View" timeline dialog:
    /// turning a raw AuditEntry's JSON snapshots into a short headline, a detailed field-by-field
    /// diff, and a consistent accent color per action.
    /// </summary>
    public static class AuditFormatting
    {
        private static readonly Brush AccentInitiated = new SolidColorBrush(Color.FromRgb(0xD9, 0x8C, 0x00));
        private static readonly Brush AccentCompleted = new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E));
        private static readonly Brush AccentAdded = new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0xFC));
        private static readonly Brush AccentDispatched = new SolidColorBrush(Color.FromRgb(0x7B, 0x61, 0xFF));
        private static readonly Brush AccentDefault = new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x5A));

        static AuditFormatting()
        {
            AccentInitiated.Freeze();
            AccentCompleted.Freeze();
            AccentAdded.Freeze();
            AccentDispatched.Freeze();
            AccentDefault.Freeze();
        }

        public static Brush AccentBrushForAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return AccentDefault;

            if (action.IndexOf("Initiated", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccentInitiated;
            if (action.IndexOf("Completed", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccentCompleted;
            if (action.IndexOf("Dispatch", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccentDispatched;
            if (action.IndexOf("Added", StringComparison.OrdinalIgnoreCase) >= 0)
                return AccentAdded;

            return AccentDefault;
        }

        /// <summary>
        /// Short, human headline for the Change Summary column. Keyed off Action so new audit
        /// action types can be added here later without touching call sites.
        /// </summary>
        public static string BuildHeadline(AuditEntry entry)
        {
            switch (entry.Action)
            {
                case "OwnershipTransfer":
                    return BuildOwnershipTransferHeadline(entry);
                case "SenderSiteChange":
                    return "Sender / Site Updated";
                default:
                    return !string.IsNullOrWhiteSpace(entry.Notes) ? entry.Notes : (entry.Action ?? "Updated");
            }
        }

        private static string BuildOwnershipTransferHeadline(AuditEntry entry)
        {
            bool oldHasEmployee = HasEmployee(entry.OldValues);
            bool newHasEmployee = HasEmployee(entry.NewValues);

            if (!oldHasEmployee && newHasEmployee) return "Dept Level → Emp Level Transfer";
            if (oldHasEmployee && !newHasEmployee) return "Emp Level → Dept Level Transfer";
            if (oldHasEmployee && newHasEmployee) return "Emp Level → Emp Level Transfer";
            return "Dept Level → Dept Level Transfer";
        }

        private static bool HasEmployee(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var obj = JObject.Parse(json);
                var token = obj["CurrentEmployeeId"];
                return token != null && token.Type != JTokenType.Null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the OldValues/NewValues JSON snapshots and turns them into readable
        /// "Field: old -> new" rows. Raw *Id columns (ComId, CurrentBranchId, ...) are
        /// skipped in favor of their *Name counterpart (CompanyName, BranchName, ...),
        /// which is what a user actually recognizes.
        /// </summary>
        public static List<ChangeLine> BuildChangeLines(AuditEntry entry)
        {
            var lines = new List<ChangeLine>();

            if (string.IsNullOrWhiteSpace(entry.OldValues) || string.IsNullOrWhiteSpace(entry.NewValues))
            {
                if (!string.IsNullOrWhiteSpace(entry.Notes))
                    lines.Add(new ChangeLine { NewValue = entry.Notes, IsPlainNote = true });
                return lines;
            }

            // OwnershipTransfer only ever touches a subset of these fields (e.g. an Employee <->
            // Dept level toggle leaves Company/Branch/Department untouched) — but the reader still
            // wants to see the full "where did it land" picture, not just the one field that moved.
            // So for this action, unchanged fields are kept and flagged IsUnchanged instead of
            // being dropped, while other actions keep the terser changed-fields-only view.
            bool keepUnchanged = entry.Action == "OwnershipTransfer";

            try
            {
                var oldObj = JObject.Parse(entry.OldValues);
                var newObj = JObject.Parse(entry.NewValues);

                foreach (var prop in newObj.Properties())
                {
                    if (prop.Name.EndsWith("Id", StringComparison.Ordinal))
                        continue;

                    var oldToken = oldObj[prop.Name];
                    var oldValue = oldToken == null || oldToken.Type == JTokenType.Null ? "(none)" : oldToken.ToString();
                    var newValue = prop.Value == null || prop.Value.Type == JTokenType.Null ? "(none)" : prop.Value.ToString();

                    if (oldValue == newValue)
                    {
                        if (keepUnchanged && newValue != "(none)")
                            lines.Add(new ChangeLine { Label = FriendlyLabel(prop.Name), NewValue = newValue, IsUnchanged = true });
                        continue;
                    }

                    lines.Add(new ChangeLine { Label = FriendlyLabel(prop.Name), OldValue = oldValue, NewValue = newValue });
                }

                if (lines.Count == 0 && !string.IsNullOrWhiteSpace(entry.Notes))
                    lines.Add(new ChangeLine { NewValue = entry.Notes, IsPlainNote = true });
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(entry.Notes))
                    lines.Add(new ChangeLine { NewValue = entry.Notes, IsPlainNote = true });
            }

            return lines;
        }

        /// <summary>"CompanyName" -> "Company", "SiteBranch" -> "Site Branch".</summary>
        public static string FriendlyLabel(string propName)
        {
            var label = propName.EndsWith("Name", StringComparison.Ordinal)
                ? propName.Substring(0, propName.Length - 4)
                : propName;
            return Regex.Replace(label, "(?<!^)([A-Z])", " $1");
        }
    }
}
