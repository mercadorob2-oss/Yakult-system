using System;
using System.Diagnostics;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Reusable static service for writing audit entries to dbo.UserActivityLog.
    ///
    /// Usage:
    ///   ActivityLogger.Log("Login", "User", userId, "User logged in successfully");
    ///   ActivityLogger.Log("Approve", "Request", requestId, "Approved request #42");
    ///
    /// DESIGN: Static with internal error suppression so logging never crashes the application.
    /// Every public method is fire-and-forget safe — exceptions are caught and written to
    /// the debug output only.
    /// </summary>
    public static class ActivityLogger
    {
        // ─────────────────────────────────────────────────────────────
        //  Common ActionType constants (open for extension — not exhaustive)
        // ─────────────────────────────────────────────────────────────

        public static class Actions
        {
            public const string Login = "Login";
            public const string Logout = "Logout";
            public const string Create = "Create";
            public const string Update = "Update";
            public const string Delete = "Delete";
            public const string View = "View";
            public const string Export = "Export";
            public const string Approve = "Approve";
            public const string Reject = "Reject";
            public const string Import = "Import";
            public const string ResetPassword = "ResetPassword";
        }

        // ─────────────────────────────────────────────────────────────
        //  CRUD guard — only data-modifying actions are persisted.
        //  View, Login, Logout, Refresh, and any other UI-only actions
        //  are silently dropped here before touching the database.
        // ─────────────────────────────────────────────────────────────
        private static readonly System.Collections.Generic.HashSet<string> _allowedActions =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Actions.Create,
                Actions.Update,
                Actions.Delete,
                Actions.Export,
                Actions.Approve,
                Actions.Reject,
                Actions.Import,
                Actions.ResetPassword,
            };

        // ─────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Logs an activity using the currently logged-in user (AppSession.CurrentUserId).
        /// Safe to call from any context; exceptions are silently suppressed.
        /// </summary>
        public static void Log(
            string actionType,
            string entityType,
            int? entityId,
            string description)
        {
            Log(Session.AppSession.CurrentUserId, actionType, entityType, entityId, description);
        }

        /// <summary>
        /// Logs an activity for an explicit userId.
        /// Safe to call from any context; exceptions are silently suppressed.
        /// </summary>
        public static void Log(
            int userId,
            string actionType,
            string entityType,
            int? entityId,
            string description)
        {
            if (userId <= 0)
                return; // No session — skip silently

            if (!_allowedActions.Contains(actionType ?? string.Empty))
                return; // Non-CRUD action (View, Login, etc.) — drop silently

            try
            {
                var repo = new UserActivityRepository(DatabaseConfig.ConnectionString);
                repo.InsertLog(userId, actionType, entityType, entityId, description);
            }
            catch (Exception ex)
            {
                // Logging must never crash the application.
                Debug.WriteLine($"[ActivityLogger] Failed to write log: {ex.Message}");
            }
        }

        /// <summary>
        /// Async-friendly overload — runs on the thread pool so UI is never blocked.
        /// </summary>
        public static void LogAsync(
            string actionType,
            string entityType,
            int? entityId,
            string description)
        {
            int userId = Session.AppSession.CurrentUserId;
            if (userId <= 0) return;

            System.Threading.Tasks.Task.Run(() =>
                Log(userId, actionType, entityType, entityId, description));
        }
    }
}
