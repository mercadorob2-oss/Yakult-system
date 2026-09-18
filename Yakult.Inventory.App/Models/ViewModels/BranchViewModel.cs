using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for branch selection in dropdowns.
    /// </summary>
    public class BranchViewModel
    {
        /// <summary>
        /// Branch ID (primary key).
        /// </summary>
        public int BranchId { get; set; }

        /// <summary>
        /// Branch name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Branch description (optional).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Company ID this branch belongs to.
        /// </summary>
        public int ComId { get; set; }

        /// <summary>
        /// Department ID (optional - some branches may have a default department).
        /// </summary>
        public int? DeptId { get; set; }

        /// <summary>
        /// Whether branch is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Display name for dropdown.
        /// </summary>
        public string DisplayName => Name;

        public override string ToString() => DisplayName;
    }
}
