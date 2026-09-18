using System;
using System.Linq;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Single source of truth for the consumable model categories offered on the Request
    /// Portal's item pickers (New Request + Assisted Request tabs). To add a new category
    /// (e.g. "Drum"), add it here — both tabs' Category dropdowns and the auto-grouping
    /// guard in RequesterPortalService.CreateCartridgeRequestByModel pick it up automatically.
    /// </summary>
    public static class ConsumableCategories
    {
        // Canonical spellings. These MUST match dbo.ItemCategory.Name / dbo.ConsumableModel.Category
        // exactly — the portal's model lookup filters on them. Historically "Toner" was carried
        // here as "Toner Cartridge" and printheads drifted between "Printhead"/"Print Head";
        // Canonicalize() below collapses every such variant back to these.
        public static readonly string[] All = { "Cartridge", "Ink", "Printhead", "Toner" };

        public static bool IsKnown(string category) =>
            string.IsNullOrWhiteSpace(category) ||
            All.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Collapses any free-text/imported spelling of a consumable category down to its
        /// canonical form: "Ink", "Toner", "Printhead", or "Cartridge". Whitespace-, dash-
        /// and case-insensitive, and substring based, so "Print Head", "print-head",
        /// "PRINTERHEAD", "Toner Cartridge", "toner cart.", etc. all normalize correctly.
        /// Unrecognized input is returned trimmed and unchanged.
        /// Mirrors CanonicalCategoryNeedle in the Web RequestPortal's RequesterPortalService.
        /// </summary>
        public static string Canonicalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw?.Trim();

            var n = raw.Replace(" ", string.Empty)
                       .Replace("-", string.Empty)
                       .Replace("_", string.Empty)
                       .ToLowerInvariant();

            // Order matters: "toner cartridge" contains both "toner" and "cartridge" —
            // the consumable family wins over the generic "Cartridge" bucket.
            if (n.Contains("ink")) return "Ink";
            if (n.Contains("toner")) return "Toner";
            if (n.Contains("print") && n.Contains("head")) return "Printhead";
            if (n.Contains("cartridge")) return "Cartridge";

            return raw.Trim();
        }

        /// <summary>True when the category text belongs to any consumable family (incl. Cartridge).</summary>
        public static bool IsConsumable(string category)
        {
            var k = Canonicalize(category);
            return k == "Ink" || k == "Toner" || k == "Printhead" || k == "Cartridge";
        }
    }
}
