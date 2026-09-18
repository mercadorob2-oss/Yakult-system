using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Session
{
    public static class AppSession
    {
        public static int CurrentUserId { get; set; }
        public static string CurrentUserName { get; set; }
        public static string CurrentEmail { get; set; }
        public static System.DateTime LoginTime { get; set; }
        public static bool IsDeveloper { get; set; }
        public static bool IsSuperAdmin { get; set; }
        // When true, ActivityLogger skips writing to dbo.UserActivityLog for this session.
        // Only honoured when IsDeveloper is true (set via Ctrl+Shift+S).
        public static bool SuppressActivityLogging { get; set; }

        // NEW: Employee information
        public static int? CurrentEmployeeId { get; set; }
        public static string CurrentEmployeeName { get; set; }
        public static string CurrentEmployeePosition { get; set; }

        // NEW: Employee organisation data (resolved at login via Employee master)
        public static int? CurrentCompanyId { get; set; }
        public static string CurrentCompanyName { get; set; }
        public static int? CurrentBranchId { get; set; }
        public static string CurrentBranchName { get; set; }
        public static int? CurrentDepartmentId { get; set; }
        public static string CurrentDepartmentName { get; set; }

        // NEW: Role information
        public static List<string> CurrentUserRoles { get; set; } = new List<string>();

        // Department Account login session
        public static bool IsDepartmentAccountSession { get; set; }
        public static int? DepartmentAccountId { get; set; }
        public static int? DepartmentAccountCompanyId { get; set; }
        public static int? DepartmentAccountDepartmentId { get; set; }
        public static int? DepartmentAccountBranchId { get; set; }

        public static bool NotificationsEnabled { get; set; } = true;

        public static bool IsLoggedIn => CurrentUserId > 0 || IsDepartmentAccountSession;

        /// <summary>
        /// Check if current user has Admin role OR is a Developer
        /// </summary>
        public static bool IsAdmin => IsDeveloper || CurrentUserRoles.Contains("Admin");

        /// <summary>
        /// Returns true when the user has the Viewer role — grants portal access but no write operations.
        /// </summary>
        public static bool IsReadOnly =>
            !IsDeveloper && HasRole("Viewer");

        /// <summary>
        /// Approvers can review and approve / reject cartridge-access requests.
        /// Based on the employee's position (not their system role).
        /// </summary>
        public static bool IsApprover
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CurrentEmployeePosition)) return false;
                var pos = CurrentEmployeePosition.Trim();
                return pos.Equals("ACCOUNT COORDINATOR",        StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ACTING ACCOUNT COORDINATOR", StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ASST. COORDINATOR",          StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ASSISTANT COORDINATOR",      StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("LADY COORDINATOR",           StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ACTING JR. ASST. MANAGER",   StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ASST. MANAGER",              StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("ASSISTANT MANAGER",          StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("JR. ASST. MANAGER",          StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("JUNIOR ASSISTANT MANAGER",   StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("SUPERVISOR",                 StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("MANAGER",                    StringComparison.OrdinalIgnoreCase)
                    || pos.Equals("COORDINATOR",                StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// IT staff are permitted to create assisted requests on behalf of other employees.
        /// Includes: Developer, Admin, IT Manager, Supervisor, Tech Support.
        /// </summary>
        public static bool IsITStaff =>
            IsDeveloper
            || HasRole("Admin")
            || HasRole("IT Manager")
            || HasRole("Supervisor")
            || HasRole("Tech Support");

        /// <summary>
        /// Check if current user has a specific role
        /// </summary>
        public static bool HasRole(string roleName)
        {
            return CurrentUserRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
        }

        public static void Clear()
        {
            CurrentUserId = 0;
            CurrentUserName = null;
            CurrentEmail = null;
            LoginTime = default;
            IsDeveloper = false;
            IsSuperAdmin = false;
            SuppressActivityLogging = false;
            CurrentEmployeeId = null;
            CurrentEmployeeName = null;
            CurrentEmployeePosition = null;
            CurrentCompanyId = null;
            CurrentCompanyName = null;
            CurrentBranchId = null;
            CurrentBranchName = null;
            CurrentDepartmentId = null;
            CurrentDepartmentName = null;
            CurrentUserRoles.Clear();
            IsDepartmentAccountSession = false;
            DepartmentAccountId = null;
            DepartmentAccountCompanyId = null;
            DepartmentAccountDepartmentId = null;
            DepartmentAccountBranchId = null;
            NotificationsEnabled = true;
            SuppressActivityLogging = false;
        }
    }
}
