using System;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// ViewModel for company selection in dropdowns.
    /// </summary>
    public class CompanyViewModel
    {
        /// <summary>
        /// Company ID (primary key).
        /// </summary>
        public int ComId { get; set; }

        /// <summary>
        /// Company name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Company description (optional).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether company is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Display name for dropdown.
        /// </summary>
        public string DisplayName => Name;

        public override string ToString() => DisplayName;
    }
}
