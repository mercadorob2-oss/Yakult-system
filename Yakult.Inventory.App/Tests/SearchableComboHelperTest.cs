using System;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Standalone assertions for SearchableComboHelper.ShouldReopenDropdown.
    /// Compile + run WITHOUT the app: csc SearchableComboHelperTest.cs ..\Helpers\SearchableComboHelper.cs
    /// Exit code 0 = all pass, 1 = failure. Loose file (Tests/ is not in the app csproj).
    /// Covers the InvalidOperationException "Cannot reopen a popup in the closed event
    /// handler": the reopen decision must be false while the dropdown is closing.
    /// </summary>
    public static class SearchableComboHelperTest
    {
        private static int _failures;

        private static void Check(bool condition, string name)
        {
            if (condition) { Console.WriteLine("  PASS: " + name); return; }
            Console.WriteLine("  FAIL: " + name);
            _failures++;
        }

        public static int Main()
        {
            Console.WriteLine("== SearchableComboHelper ==");

            Check(SearchableComboHelper.ShouldReopenDropdown(true, false, 3, "bio"),
                "loaded + closed + matches + text => reopen");
            Check(!SearchableComboHelper.ShouldReopenDropdown(false, false, 3, "bio"),
                "not loaded => no reopen");
            Check(!SearchableComboHelper.ShouldReopenDropdown(true, true, 3, "bio"),
                "already open => no reopen");
            Check(!SearchableComboHelper.ShouldReopenDropdown(true, false, 0, "bio"),
                "no matches => no reopen");
            Check(!SearchableComboHelper.ShouldReopenDropdown(true, false, 3, ""),
                "empty text => no reopen");
            Check(!SearchableComboHelper.ShouldReopenDropdown(true, false, 3, null),
                "null text => no reopen");

            Console.WriteLine(_failures == 0 ? "ALL PASS" : _failures + " FAILURES");
            return _failures == 0 ? 0 : 1;
        }
    }
}
