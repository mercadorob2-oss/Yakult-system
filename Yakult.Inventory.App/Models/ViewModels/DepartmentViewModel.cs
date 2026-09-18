using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for department selection in dropdowns.
    /// </summary>
    public class DepartmentViewModel
    {
        /// <summary>
        /// Department ID (primary key).
        /// </summary>
        public int DeptId { get; set; }

        /// <summary>
        /// Department name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Department description (optional).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Company ID this department belongs to.
        /// </summary>
        public int ComId { get; set; }

        /// <summary>
        /// Whether department is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Display name for dropdown.
        /// </summary>
        public string DisplayName => Name;

        public override string ToString() => DisplayName;
    }
}
