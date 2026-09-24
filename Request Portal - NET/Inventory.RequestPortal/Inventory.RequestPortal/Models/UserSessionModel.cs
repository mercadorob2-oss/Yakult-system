namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// Model for storing user session data.
    /// TRANSLATED FROM: Yakult.Inventory.App/Session/AppSession.cs
    /// Stored in ASP.NET Core Session as JSON.
    /// </summary>
    public class UserSessionModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public bool IsDeveloper { get; set; }
        /// <summary>AccountLevel.LevelRank for this user. 0 = not yet assigned.</summary>
        public int LevelRank { get; set; }

        // Employee information
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeePosition { get; set; }

        // Employee organization data (from Employee master record)
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }

        // Role information
        public List<string> Roles { get; set; } = new List<string>();

        // Department Account session fields (set when the user is a dept account, not an employee)
        public bool IsDepartmentAccountSession { get; set; }
        public int? DepartmentAccountId { get; set; }
        public int? DepartmentAccountCompanyId { get; set; }
        public string? DepartmentAccountCompanyName { get; set; }
        public int? DepartmentAccountDepartmentId { get; set; }
        public string? DepartmentAccountDepartmentName { get; set; }
        public int? DepartmentAccountBranchId { get; set; }
        public string? DepartmentAccountBranchName { get; set; }

        public bool NotificationsEnabled { get; set; } = true;

        public bool IsLoggedIn => UserId > 0 || IsDepartmentAccountSession;
        public bool IsAdmin => IsDeveloper || Roles.Contains("Admin");

        /// <summary>
        /// IT staff are permitted to create assisted requests on behalf of other employees.
        /// Mirrors Yakult.Inventory.App/Session/AppSession.cs's IsITStaff exactly (Developer,
        /// Admin, IT Manager, Supervisor, Tech Support) — this portal previously only checked
        /// IsDeveloper for IT Assisted Request access, which left Tech Support (and Admin/IT
        /// Manager/Supervisor) unable to see or use the feature even though the desktop app
        /// already grants them access.
        /// </summary>
        public bool IsITStaff =>
            IsDeveloper
            || HasRole("Admin")
            || HasRole("IT Manager")
            || HasRole("IT Supervisor")
            || HasRole("Tech Support");

        public bool HasRole(string roleName)
        {
            return Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for Developer accounts and employees in the Information Technology department.
        /// Gates access to the Consumable Management portal — unlike IsITStaff (which is
        /// Role-based: Admin/IT Manager/Supervisor/Tech Support and can apply to any
        /// department), this is Department-based, matching the requirement that Consumable
        /// Management is limited to actual IT department employees specifically.
        /// </summary>
        public bool IsITDepartment => IsDeveloper || IsInformationTechnologyDept(DepartmentName);

        // Matches "Information Technology", "IT", "IT Department", "I.T.", etc.
        // Mirrors Yakult.Inventory.App/Wpf/Set/RequisitionForm/ViewModels/PrepareRequisitionViewModel.cs's
        // IsInformationTechnologyDept — the sanctioned "is this employee IT" pattern in this
        // codebase, since department names are free-typed and not a fixed enum.
        private static bool IsInformationTechnologyDept(string? departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName)) return false;
            string n = departmentName.Trim();
            return n.Equals("IT", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("IT", StringComparison.OrdinalIgnoreCase)
                || n.IndexOf("Information Technology", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
