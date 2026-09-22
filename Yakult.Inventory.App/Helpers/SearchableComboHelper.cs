namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Pure decision logic for searchable ComboBoxes (EmployeeDialog pattern).
    /// Extracted so the reopen rule is unit-testable without a WPF dispatcher.
    /// </summary>
    public static class SearchableComboHelper
    {
        /// <summary>
        /// True only when every reopen precondition holds. The actual open must
        /// still be dispatcher-deferred by the caller: when this TextChanged was
        /// raised from inside DropDownClosed (selection re-commit), opening
        /// synchronously throws InvalidOperationException ("Cannot reopen a
        /// popup in the closed event handler").
        /// </summary>
        public static bool ShouldReopenDropdown(bool dialogLoaded, bool isDropDownOpen, int filteredCount, string savedText)
        {
            return dialogLoaded && !isDropDownOpen && filteredCount > 0 && !string.IsNullOrEmpty(savedText);
        }
    }
}
