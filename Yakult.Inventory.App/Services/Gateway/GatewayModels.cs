using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Services.Gateway
{
    // Mirrors Yakult.Inventory.Gateway\Models\SessionModels.cs. Keep the two in sync.

    public sealed class GatewayLoginResponse
    {
        public string AccessToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public GatewayEnvironmentInfo Environment { get; set; }
        public GatewaySession Session { get; set; }
    }

    /// <summary>A database the gateway can sign into (listed by the Ctrl+Shift+D switcher).</summary>
    public sealed class GatewayEnvironmentInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }

        /// <summary>"Production", "Test" (YIMS_PROD, the dummy database) or "Unknown".</summary>
        public string Kind { get; set; }
        public bool IsDefault { get; set; }

        public override string ToString() => DisplayName ?? Name;
    }

    public sealed class GatewaySession
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool IsDeveloper { get; set; }
        public bool IsSuperAdmin { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public bool NotificationsEnabled { get; set; } = true;
        public bool IsDepartmentAccountLogin { get; set; }
        public GatewayEmployee Employee { get; set; }
        public GatewayDepartmentAccount DepartmentAccount { get; set; }
        public GatewayPermissionSnapshot Permissions { get; set; }
    }

    public sealed class GatewayEmployee
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
    }

    public sealed class GatewayDepartmentAccount
    {
        public int AccountId { get; set; }
        public int? CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int? BranchId { get; set; }
    }

    public sealed class GatewayPermissionSnapshot
    {
        public bool Loaded { get; set; }
        public Dictionary<string, List<string>> RolePortals { get; set; }
        public Dictionary<string, bool> UserPortals { get; set; }
        public Dictionary<string, Dictionary<string, Dictionary<string, bool>>> RoleItems { get; set; }
        public Dictionary<string, Dictionary<string, bool>> UserItems { get; set; }
    }

    /// <summary>The (Success, Message) result several delete endpoints return.</summary>
    public sealed class GatewayOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Thrown for any non-success gateway response; StatusCode is 0 when the server could not be reached.</summary>
    public sealed class GatewayException : Exception
    {
        public int StatusCode { get; }

        public GatewayException(int statusCode, string message, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
        }
    }
}
