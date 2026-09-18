using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Extensions;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Controller for user authentication.
    /// TRANSLATED FROM: Yakult.Inventory.App/Pages/LoginPage.cs
    ///
    /// MAPPING:
    /// - Form_Load -> Login (GET)
    /// - LoginBtn_Click -> Login (POST)
    /// - AppSession -> HttpContext.Session
    /// - MessageBox.Show -> TempData/ModelState
    /// </summary>
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserNotificationSettingRepository _notifSettingRepository;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _environment;

        // Session key for user data
        public const string SessionKeyUser = "CurrentUser";

        private static readonly HashSet<string> ApproverPositions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ACCOUNT COORDINATOR",
                "ACTING ACCOUNT COORDINATOR",
                "ASST. COORDINATOR",
                "LADY COORDINATOR",
                "ACTING JR. ASST. MANAGER",
                "ASST. MANAGER",
                "JR. ASST. MANAGER",
                "SUPERVISOR",
                "MANAGER",
                "COORDINATOR"
            };

        public AccountController(
            IUserRepository userRepository,
            IUserNotificationSettingRepository notifSettingRepository,
            ILogger<AccountController> logger,
            IWebHostEnvironment environment)
        {
            _userRepository         = userRepository;
            _notifSettingRepository = notifSettingRepository;
            _logger      = logger;
            _environment = environment;
        }

        /// <summary>
        /// Displays the login form.
        /// </summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // If already logged in, redirect to home
            var currentUser = HttpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
            if (currentUser?.IsLoggedIn == true)
            {
                // Information Technology department employees land on the Portal Hub
                // regardless of job title — an IT employee whose Position also happens to
                // match an approver title (Supervisor, Coordinator, etc.) should still get
                // to pick Request Portal vs Consumable Management, not be routed straight
                // into the approver queue. Approvers outside IT keep going straight to
                // their queue, unchanged.
                if (currentUser.IsITDepartment)
                    return RedirectToAction("Index", "PortalHub");

                // Dept accounts are always requesters, never approvers
                if (!currentUser.IsDepartmentAccountSession
                    && !string.IsNullOrWhiteSpace(currentUser.EmployeePosition)
                    && ApproverPositions.Contains(currentUser.EmployeePosition.Trim()))
                    return RedirectToAction("Landing", "Authorization");

                return RedirectToAction("Index", "PortalHub");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        /// <summary>
        /// Processes login form submission.
        /// TRANSLATED FROM: LoginPage.LoginBtn_Click()
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Authenticate user (same validation as WinForms)
                var result = await _userRepository.AuthenticateAsync(model.Username, model.Password);

                if (!result.Success)
                {
                    // TRANSLATED FROM: MessageBox.Show("Invalid name or password.", ...)
                    ModelState.AddModelError("", result.ErrorMessage ?? "Invalid username or password.");
                    return View(model);
                }

                // Create session object (replaces AppSession in WinForms)
                var userSession = new UserSessionModel
                {
                    UserId = result.UserId,
                    UserName = result.Name,
                    Email = result.Email,
                    LoginTime = DateTime.Now,
                    IsDeveloper = result.IsDeveloper,
                    LevelRank = result.LevelRank,
                    EmployeeId = result.EmployeeId,
                    EmployeeName = result.EmployeeName,
                    EmployeePosition = result.EmployeePosition,
                    CompanyId = result.CompanyId,
                    CompanyName = result.CompanyName,
                    BranchId = result.BranchId,
                    BranchName = result.BranchName,
                    DepartmentId = result.DepartmentId,
                    DepartmentName = result.DepartmentName,
                    Roles = result.Roles,
                    IsDepartmentAccountSession      = result.IsDepartmentAccountSession,
                    DepartmentAccountId             = result.DepartmentAccountId,
                    DepartmentAccountCompanyId      = result.DepartmentAccountCompanyId,
                    DepartmentAccountCompanyName    = result.DepartmentAccountCompanyName,
                    DepartmentAccountDepartmentId   = result.DepartmentAccountDepartmentId,
                    DepartmentAccountDepartmentName = result.DepartmentAccountDepartmentName,
                    DepartmentAccountBranchId       = result.DepartmentAccountBranchId,
                    DepartmentAccountBranchName     = result.DepartmentAccountBranchName,
                    NotificationsEnabled = result.IsDepartmentAccountSession
                        ? true
                        : await _notifSettingRepository.GetNotificationsEnabledAsync(result.UserId),
                };

                // Store in session
                HttpContext.Session.SetObject(SessionKeyUser, userSession);

                _logger.LogInformation("User {Username} logged in successfully", result.Name);

                // Redirect to return URL or default page
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                // IT department employees land on the Portal Hub regardless of job title —
                // see the matching comment in the GET Login branch above.
                if (userSession.IsITDepartment)
                    return RedirectToAction("Index", "PortalHub");

                // Dept accounts are always requesters; approvers go to their landing page
                if (!userSession.IsDepartmentAccountSession
                    && !string.IsNullOrWhiteSpace(userSession.EmployeePosition)
                    && ApproverPositions.Contains(userSession.EmployeePosition.Trim()))
                    return RedirectToAction("Landing", "Authorization");

                return RedirectToAction("Index", "PortalHub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", model.Username);

                // In development, show detailed error message to help diagnose issues
                // In production, show generic error for security
                if (_environment.IsDevelopment())
                {
                    ModelState.AddModelError("", $"Login error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "An error occurred during login. Please try again.");
                }

                return View(model);
            }
        }

        /// <summary>
        /// Logs out the current user.
        /// TRANSLATED FROM: AppSession.Clear()
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var currentUser = HttpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
            var username = currentUser?.UserName ?? "Unknown";

            // Clear session (same as AppSession.Clear())
            HttpContext.Session.Clear();

            _logger.LogInformation("User {Username} logged out", username);

            TempData["SuccessMessage"] = "You have been logged out successfully.";

            return RedirectToAction("Login");
        }

        /// <summary>
        /// Gets the current logged-in user from session.
        /// Helper method for other controllers.
        /// </summary>
        public static UserSessionModel? GetCurrentUser(HttpContext httpContext)
        {
            return httpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
        }

        [HttpGet]
        public async Task<IActionResult> MyAccount()
        {
            var currentUser = HttpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
            if (currentUser?.IsLoggedIn != true)
                return RedirectToAction("Login");

            var account = await _userRepository.GetAccountAsync(currentUser.UserId);
            if (account == null)
                return RedirectToAction("Login");

            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmail(string email)
        {
            var currentUser = HttpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
            if (currentUser?.IsLoggedIn != true)
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            {
                TempData["AccountError"] = "Invalid email address.";
                return RedirectToAction("MyAccount");
            }

            bool updated = await _userRepository.UpdateEmailAsync(currentUser.UserId, email.Trim());
            if (updated)
            {
                currentUser.Email = email.Trim();
                HttpContext.Session.SetObject(SessionKeyUser, currentUser);
                TempData["AccountSuccess"] = "Email address updated successfully.";
            }
            else
            {
                TempData["AccountError"] = "Failed to update email. Please try again.";
            }

            return RedirectToAction("MyAccount");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var currentUser = HttpContext.Session.GetObject<UserSessionModel>(SessionKeyUser);
            if (currentUser?.IsLoggedIn != true)
                return RedirectToAction("Login");

            if (!IsStrongPassword(newPassword, out var passwordError))
            {
                TempData["AccountError"] = passwordError;
                return RedirectToAction("MyAccount");
            }

            if (newPassword != confirmPassword)
            {
                TempData["AccountError"] = "New password and confirmation do not match.";
                return RedirectToAction("MyAccount");
            }

            bool changed = await _userRepository.ChangePasswordAsync(currentUser.UserId, currentPassword, newPassword);
            if (changed)
                TempData["AccountSuccess"] = "Password changed successfully.";
            else
                TempData["AccountError"] = "Current password is incorrect.";

            return RedirectToAction("MyAccount");
        }

        private static bool IsValidEmail(string email)
        {
            try { _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }

        /// <summary>
        /// Minimum viable password policy: 8+ characters with at least one letter and one
        /// digit. Deliberately does not touch the underlying SHA256+salt hashing scheme in
        /// UserRepository — that's shared with the WinForms desktop app's dbo.[User] auth
        /// path and upgrading it (e.g. to a proper KDF) needs a coordinated migration across
        /// both apps, not a drive-by change here.
        /// </summary>
        private static bool IsStrongPassword(string password, out string error)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                error = "New password must be at least 8 characters.";
                return false;
            }
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            {
                error = "New password must contain at least one letter and one number.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
