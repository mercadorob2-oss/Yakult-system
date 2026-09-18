using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly PortalOptions _options;
    private readonly IUserRepository _userRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly IPortalCardRepository _portalCardRepository;
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly IDemoModeService _demoMode;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IOptions<PortalOptions> options,
        IUserRepository userRepository,
        IWebHostEnvironment environment,
        IPortalCardRepository portalCardRepository,
        IConnectionStringProvider connectionStringProvider,
        IDemoModeService demoMode,
        ILogger<HomeController> logger)
    {
        _options = options.Value;
        _userRepository = userRepository;
        _environment = environment;
        _portalCardRepository = portalCardRepository;
        _connectionStringProvider = connectionStringProvider;
        _demoMode = demoMode;
        _logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        ViewData["Title"] = _options.Title;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        ViewData["Title"] = $"Request an account - {_options.Title}";
        return View();
    }

    private IActionResult RedirectToHome()
    {
        return User.HasClaim("IsDeveloper", "true")
            ? RedirectToAction("Dashboard", "Admin")
            : RedirectToAction("Index", "Public");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> FindEmployeeRecord(string employeeNumber, string? workEmail = null)
    {
        employeeNumber = (employeeNumber ?? string.Empty).Trim();
        workEmail = (workEmail ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            return BadRequest(new { success = false, message = "Employee number is required." });
        }

        const string sql = @"
            SELECT TOP (1)
                e.EmpId,
                e.EmployeeNumber,
                e.Name AS EmployeeName,
                c.Name AS CompanyName,
                b.Name AS BranchName,
                d.Name AS DepartmentName,
                d.Section,
                e.Position,
                t.Code AS TitleCode,
                t.Description AS TitleDescription,
                ea.EmailAddress AS WorkEmail
            FROM dbo.Employee e
            LEFT JOIN dbo.Company c ON c.ComId = e.ComId
            LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
            LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
            LEFT JOIN dbo.Title t ON t.TitleId = e.TitleId
            LEFT JOIN dbo.EmployeeEmail ee ON ee.EmpId = e.EmpId
                AND ee.IsActive = 1
                AND (ee.IsPrimary = 1 OR ee.EmailRole = N'Work')
            LEFT JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
                AND ea.IsActive = 1
            WHERE e.EmployeeNumber = @EmployeeNumber
              AND e.Active = 1
              AND (
                    @WorkEmail = N''
                    OR EXISTS (
                        SELECT 1
                        FROM dbo.EmployeeEmail ee2
                        INNER JOIN dbo.EmailAddress ea2 ON ea2.EmailId = ee2.EmailId
                        WHERE ee2.EmpId = e.EmpId
                          AND ee2.IsActive = 1
                          AND ea2.IsActive = 1
                          AND LOWER(LTRIM(RTRIM(ea2.EmailAddress))) = LOWER(@WorkEmail)
                    )
                  )
            ORDER BY CASE WHEN ee.IsPrimary = 1 THEN 0 ELSE 1 END, ee.EmployeeEmailId;";

        try
        {
            await using var connection = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EmployeeNumber", employeeNumber);
            command.Parameters.AddWithValue("@WorkEmail", workEmail);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return NotFound(new { success = false, message = "No active employee record matched the employee number." });
            }

            static string ReadString(SqlDataReader reader, int ordinal)
                => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

            return Json(new
            {
                success = true,
                employee = new
                {
                    employeeId = reader.GetInt32(0),
                    employeeNumber = ReadString(reader, 1),
                    name = ReadString(reader, 2),
                    company = ReadString(reader, 3),
                    branch = ReadString(reader, 4),
                    department = ReadString(reader, 5),
                    section = ReadString(reader, 6),
                    position = ReadString(reader, 7),
                    title = !reader.IsDBNull(9) ? reader.GetString(9) : ReadString(reader, 8),
                    workEmail = ReadString(reader, 10)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find employee record for account request.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to verify the employee record." });
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount([FromBody] CreatePortalAccountRequest request)
    {
        var employeeNumber = (request.EmployeeNumber ?? string.Empty).Trim();
        var workEmail = (request.WorkEmail ?? string.Empty).Trim();
        var username = (request.Username ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(employeeNumber) || string.IsNullOrWhiteSpace(workEmail))
            return BadRequest(new { success = false, message = "Employee number and work email are required." });
        if (!System.Net.Mail.MailAddress.TryCreate(workEmail, out _))
            return BadRequest(new { success = false, message = "Enter a valid work email address." });
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[A-Za-z0-9._-]{3,100}$"))
            return BadRequest(new { success = false, message = "Username must be 3–100 characters using letters, numbers, dots, hyphens, or underscores." });
        if (password.Length < 8 || password.Length > 128)
            return BadRequest(new { success = false, message = "Password must contain between 8 and 128 characters." });

        const string employeeSql = @"
            SELECT TOP (1) e.EmpId, e.Name
            FROM dbo.Employee e
            WHERE e.EmployeeNumber = @EmployeeNumber
              AND e.Active = 1
              AND EXISTS (
                    SELECT 1
                    FROM dbo.EmployeeEmail ee
                    INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
                    WHERE ee.EmpId = e.EmpId
                      AND ee.IsActive = 1
                      AND ea.IsActive = 1
                      AND LOWER(LTRIM(RTRIM(ea.EmailAddress))) = LOWER(@WorkEmail)
              );";

        try
        {
            await using var connection = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                int employeeId;
                string employeeName;
                await using (var employeeCommand = new SqlCommand(employeeSql, connection, (SqlTransaction)transaction))
                {
                    employeeCommand.Parameters.AddWithValue("@EmployeeNumber", employeeNumber);
                    employeeCommand.Parameters.AddWithValue("@WorkEmail", workEmail);
                    await using var reader = await employeeCommand.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = "The employee number and work email do not match an active employee record." });
                    }

                    employeeId = reader.GetInt32(0);
                    employeeName = reader.GetString(1);
                }

                const string duplicateSql = @"
                    SELECT CASE
                        WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE EmpId = @EmpId) THEN 'employee'
                        WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE LOWER(Name) = LOWER(@Username)) THEN 'username'
                        WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE LOWER(EmailAddress) = LOWER(@WorkEmail)) THEN 'email'
                        WHEN EXISTS (SELECT 1 FROM dbo.AccountRequest WHERE EmpId = @EmpId AND Status = 'Pending') THEN 'pending employee request'
                        WHEN EXISTS (SELECT 1 FROM dbo.AccountRequest WHERE LOWER(RequestedUsername) = LOWER(@Username) AND Status = 'Pending') THEN 'pending username request'
                        ELSE ''
                    END;";
                string duplicate;
                await using (var duplicateCommand = new SqlCommand(duplicateSql, connection, (SqlTransaction)transaction))
                {
                    duplicateCommand.Parameters.AddWithValue("@EmpId", employeeId);
                    duplicateCommand.Parameters.AddWithValue("@Username", username);
                    duplicateCommand.Parameters.AddWithValue("@WorkEmail", workEmail);
                    duplicate = Convert.ToString(await duplicateCommand.ExecuteScalarAsync()) ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(duplicate))
                {
                    await transaction.RollbackAsync();
                    var message = duplicate == "employee"
                        ? "An account already exists for this employee."
                        : $"That {duplicate} is already registered.";
                    return Conflict(new { success = false, message });
                }

                var salt = RandomNumberGenerator.GetBytes(16);
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var combined = new byte[passwordBytes.Length + salt.Length];
                Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
                var hash = SHA256.HashData(combined);

                const string insertRequestSql = @"
                    INSERT INTO dbo.AccountRequest
                        (EmpId, RequestedUsername, WorkEmail, PasswordHash, PasswordSalt, Status, SubmittedAt)
                    VALUES
                        (@EmpId, @Username, @WorkEmail, @PasswordHash, @PasswordSalt, 'Pending', SYSUTCDATETIME());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";
                int accountRequestId;
                await using (var insertRequestCommand = new SqlCommand(insertRequestSql, connection, (SqlTransaction)transaction))
                {
                    insertRequestCommand.Parameters.AddWithValue("@Username", username);
                    insertRequestCommand.Parameters.AddWithValue("@WorkEmail", workEmail);
                    insertRequestCommand.Parameters.AddWithValue("@EmpId", employeeId);
                    insertRequestCommand.Parameters.Add("@PasswordHash", System.Data.SqlDbType.VarBinary, hash.Length).Value = hash;
                    insertRequestCommand.Parameters.Add("@PasswordSalt", System.Data.SqlDbType.VarBinary, salt.Length).Value = salt;
                    accountRequestId = Convert.ToInt32(await insertRequestCommand.ExecuteScalarAsync());
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Account request {AccountRequestId} submitted for employee {EmployeeId}.", accountRequestId, employeeId);
                return Json(new { success = true, accountRequestId, employeeName, username, status = "Pending" });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            _logger.LogWarning(ex, "Account request encountered a unique-key conflict for {Username}.", username);
            return Conflict(new { success = false, message = "The employee, username, or work email is already registered." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit account request for employee number {EmployeeNumber}.", employeeNumber);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to submit the account request. Please contact IT." });
        }
    }

    public sealed class CreatePortalAccountRequest
    {
        public string? EmployeeNumber { get; init; }
        public string? WorkEmail { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminPreview()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "0"),
            new(ClaimTypes.Name, "Admin Preview"),
            new(ClaimTypes.Email, "admin.preview@local"),
            new(ClaimTypes.Role, "Administrator"),
            new("IsDeveloper", "true"),
            new("IsAdminPreview", "true")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            });

        return RedirectToAction("Dashboard", "Admin");
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = _options.Title;
            return View(model);
        }

        try
        {
            var result = await _userRepository.AuthenticateAsync(model.Username, model.Password);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid username or password.");
                ViewData["Title"] = _options.Title;
                return View(model);
            }

            await SignInPortalUserAsync(result, model.RememberMe);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToHome();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Portal login failed for username {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Unable to sign in right now. Please try again later or contact IT support.");
            ViewData["Title"] = _options.Title;
            return View(model);
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginForLaunch([FromBody] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Enter your username and password." });
        }

        try
        {
            var result = await _userRepository.AuthenticateAsync(model.Username, model.Password);
            if (!result.Success)
            {
                return Unauthorized(new { success = false, message = result.ErrorMessage ?? "Invalid username or password." });
            }

            await SignInPortalUserAsync(result, model.RememberMe);
            return Json(new { success = true, name = result.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Portal launch login failed for username {Username}", model.Username);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to sign in right now. Please try again later or contact IT support." });
        }
    }

    private async Task SignInPortalUserAsync(AuthResult result, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new Claim(ClaimTypes.Name, result.Name),
            new Claim(ClaimTypes.Email, result.Email)
        };

        foreach (var role in result.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (result.IsDeveloper)
            claims.Add(new Claim("IsDeveloper", "true"));

        if (result.EmployeeId.HasValue)
        {
            claims.Add(new Claim("EmployeeId", result.EmployeeId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(result.EmployeePosition))
                claims.Add(new Claim("EmployeePosition", result.EmployeePosition));
        }

        if (result.CompanyId.HasValue)
            claims.Add(new Claim("CompanyId", result.CompanyId.Value.ToString()));
        if (result.BranchId.HasValue)
            claims.Add(new Claim("BranchId", result.BranchId.Value.ToString()));
        if (result.DepartmentId.HasValue)
            claims.Add(new Claim("DepartmentId", result.DepartmentId.Value.ToString()));

        if (result.IsDepartmentAccountSession)
        {
            claims.Add(new Claim("IsDepartmentAccount", "true"));
            if (result.DepartmentAccountCompanyId.HasValue)
                claims.Add(new Claim("DeptAccountCompanyId", result.DepartmentAccountCompanyId.Value.ToString()));
            if (result.DepartmentAccountDepartmentId.HasValue)
                claims.Add(new Claim("DeptAccountDepartmentId", result.DepartmentAccountDepartmentId.Value.ToString()));
            if (result.DepartmentAccountBranchId.HasValue)
                claims.Add(new Claim("DeptAccountBranchId", result.DepartmentAccountBranchId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        _demoMode.SetEnabled(HttpContext, false);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public async Task<IActionResult> Index()
    {
        IReadOnlyList<PortalSystemLink> dbSystems = Array.Empty<PortalSystemLink>();
        var notice = new PortalNoticeSettings();
        try
        {
            dbSystems = await _portalCardRepository.GetVisibleSystemCardsAsync();
            notice = await _portalCardRepository.GetNoticeSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load portal cards/settings from the database. Falling back to configured portal cards.");
        }

        var model = new PortalHomeViewModel
        {
            Title = _options.Title,
            Subtitle = _options.Subtitle,
            Systems = ApplyCardRules(dbSystems.Count > 0 ? dbSystems : _options.Systems),
            MiniCards = _options.MiniCards,
            Notice = notice.IsActive(DateTime.UtcNow) ? notice : new PortalNoticeSettings()
        };

        return View(model);
    }

    /// <summary>
    /// Subsystem reference page. Lists the 7 portal groups of the
    /// Yakult Inventory WinForms app and the 37 modules inside them.
    /// </summary>
    public IActionResult Modules()
    {
        ViewData["Title"] = "Modules";
        return View();
    }

    /// <summary>
    /// Error / status page. Invoked by the unhandled-exception pipeline (without a
    /// <paramref name="code"/>) and by <c>UseStatusCodePagesWithReExecute</c> (with one).
    /// </summary>
    [AllowAnonymous]
    public IActionResult Error(int? code = null)
    {
        if (code is > 0)
            Response.StatusCode = code.Value;

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code
        });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadRdp()
    {
        if (_options.Rdp == null)
        {
            _logger.LogWarning("RDP download requested but Portal:Rdp is not configured.");
            return BadRequest("RDP configuration not found.");
        }

        var rdpConfig = _options.Rdp;
        var rdpContent = GenerateRdpFile(rdpConfig);

        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{rdpConfig.FileName}\"");
        await LogDownloadAsync("RDP", rdpConfig.FileName);
        return File(Encoding.UTF8.GetBytes(rdpContent), "application/rdp");
    }

    private IReadOnlyList<PortalSystemLink> ApplyCardRules(IEnumerable<PortalSystemLink> cards)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;

        return cards
            .Where(card => CanCurrentUserSee(card, userId, roles))
            .Select(card => ApplyMaintenanceWindow(card, nowUtc))
            .ToList();
    }

    private static bool CanCurrentUserSee(PortalSystemLink card, string userId, HashSet<string> roles)
    {
        var allowedUsers = SplitCsv(card.AllowedUserIds);
        if (allowedUsers.Count > 0 && allowedUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
            return true;

        var allowedRoles = SplitCsv(card.AllowedRoles);
        if (allowedRoles.Count == 0 && allowedUsers.Count == 0)
            return true;

        return allowedRoles.Any(role => roles.Contains(role));
    }

    private static PortalSystemLink ApplyMaintenanceWindow(PortalSystemLink card, DateTime nowUtc)
    {
        if (card.MaintenanceStartUtc.HasValue &&
            card.MaintenanceEndUtc.HasValue &&
            nowUtc >= DateTime.SpecifyKind(card.MaintenanceStartUtc.Value, DateTimeKind.Utc) &&
            nowUtc <= DateTime.SpecifyKind(card.MaintenanceEndUtc.Value, DateTimeKind.Utc))
        {
            card.Status = "Maintenance";
            if (string.IsNullOrWhiteSpace(card.MaintenanceNote))
                card.MaintenanceNote = $"Scheduled maintenance until {card.MaintenanceEndUtc.Value:yyyy-MM-dd HH:mm} UTC.";
            card.IsOperational = false;
        }

        return card;
    }

    private static List<string> SplitCsv(string value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    [HttpGet]
    public async Task<IActionResult> DownloadInstaller(string? cardKey = null)
    {
        PortalSystemLink? card = null;
        if (!string.IsNullOrWhiteSpace(cardKey))
        {
            IReadOnlyList<PortalSystemLink> cards;
            try
            {
                cards = await _portalCardRepository.GetVisibleSystemCardsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to load portal cards for installer download {CardKey}.", cardKey);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Installer catalog is unavailable.");
            }

            card = ApplyCardRules(cards).FirstOrDefault(item => item.CardKey.Equals(cardKey, StringComparison.OrdinalIgnoreCase));
            if (card == null || !card.IsInstallerDownload)
                return NotFound("Installer card not found.");

            var status = string.IsNullOrWhiteSpace(card.Status) ? card.IsOperational ? "Connected" : "NotConnected" : card.Status;
            if (!status.Equals("Connected", StringComparison.OrdinalIgnoreCase))
                return BadRequest("This installer is not currently available.");
        }

        var installerPath = card == null ? _options.InstallerPath : card.InstallerPath;
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            _logger.LogWarning("Installer download requested but no installer path is configured for {CardKey}.", card?.CardKey ?? "global");
            return BadRequest(card == null ? "Installer path not configured." : "This service does not have an installer file configured.");
        }

        if (!TryResolveInstallerPath(installerPath, out var fullPath))
            return BadRequest("Installer path is not valid.");

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("Installer download requested but file was not found at {InstallerPath}.", fullPath);
            return NotFound("Installer file not found on the server.");
        }

        var storedFileName = Path.GetFileName(fullPath);
        var downloadFileName = BuildInstallerDownloadFileName(card, fullPath);
        await LogDownloadAsync(card == null ? "Installer" : $"Installer:{card.CardKey}", $"{downloadFileName} ({storedFileName})");
        return PhysicalFile(fullPath, "application/octet-stream", downloadFileName);
    }

    private bool TryResolveInstallerPath(string installerPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (Path.IsPathRooted(installerPath) || installerPath.Contains("..", StringComparison.Ordinal) || installerPath.Contains(':'))
            return false;

        var extension = Path.GetExtension(installerPath);
        if (!extension.Equals(".apk", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        var relative = installerPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relative));
        var root = Path.GetFullPath(_environment.ContentRootPath);
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = resolved;
        return true;
    }
    [HttpGet]
    public async Task<IActionResult> DownloadMobileInstaller()
    {
        var installerPath = _options.MobileInstallerPath;
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            _logger.LogWarning("Mobile installer download requested but Portal:MobileInstallerPath is not configured.");
            return BadRequest("Mobile installer path not configured.");
        }

        var fullPath = Path.Combine(_environment.ContentRootPath, installerPath.Replace("\\", "/"));

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("Mobile installer download requested but file was not found at {InstallerPath}.", fullPath);
            return NotFound("Mobile installer file not found on the server.");
        }

        var storedFileName = Path.GetFileName(fullPath);
        var downloadFileName = BuildMobileInstallerDownloadFileName(fullPath);
        await LogDownloadAsync("MobileInstaller", $"{downloadFileName} ({storedFileName})");
        return PhysicalFile(fullPath, "application/vnd.android.package-archive", downloadFileName);
    }

    private static string BuildInstallerDownloadFileName(PortalSystemLink? card, string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        var baseName = card == null
            ? "Yakult-Inventory-Installer"
            : string.Join("-", new[] { card.Name, card.InstallerVersion }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));

        return $"{SanitizeDownloadFileName(baseName, "Yakult-Installer")}{extension}";
    }

    private static string BuildMobileInstallerDownloadFileName(string fullPath)
    {
        return $"{SanitizeDownloadFileName("Yakult-Mobile-Installer", "Yakult-Mobile-Installer")}{Path.GetExtension(fullPath)}";
    }

    private static string SanitizeDownloadFileName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var ch in value)
        {
            if (invalid.Contains(ch) || char.IsWhiteSpace(ch) || ch is '/' or '\\' or ':' or ';' or ',')
            {
                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-');
            previousWasSeparator = false;
        }

        var fileName = builder.ToString().Trim('-', '.', '_');
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = fallback;
        if (fileName.Length > 80)
            fileName = fileName[..80].Trim('-', '.', '_');

        return fileName;
    }

    private async Task LogDownloadAsync(string downloadType, string fileName)
    {
        try
        {
            var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            await _portalCardRepository.LogPortalEventAsync(
                "Download",
                "PortalDownload",
                null,
                userId,
                User.Identity?.Name,
                $"{downloadType}: {fileName}",
                HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to write portal download audit event for {DownloadType}.", downloadType);
        }
    }

    private static string GenerateRdpFile(RdpOptions config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{config.ServerAddress}");
        sb.AppendLine($"username:s:{config.Username}");
        if (!string.IsNullOrWhiteSpace(config.Domain))
        {
            sb.AppendLine($"domain:s:{config.Domain}");
        }
        sb.AppendLine($"screen mode id:i:2");
        sb.AppendLine($"use multimon:i:0");
        sb.AppendLine($"desktopwidth:i:{config.ScreenWidth}");
        sb.AppendLine($"desktopheight:i:{config.ScreenHeight}");
        sb.AppendLine($"color depth:i:{config.ColorDepth}");
        sb.AppendLine($"authentication level:i:2");
        sb.AppendLine($"prompt for credentials:i:{(config.PromptForCredentials ? 1 : 0)}");
        sb.AppendLine($"enablecredsspsupport:i:1");
        sb.AppendLine("autoreconnection enabled:i:1");
        sb.AppendLine("reconnection attempts:i:5");
        sb.AppendLine("redirectclipboard:i:1");
        sb.AppendLine("redirectprinters:i:0");
        sb.AppendLine("redirectsmartcards:i:0");
        sb.AppendLine("redirectdrives:i:0");
        sb.AppendLine("redirectserialports:i:0");
        sb.AppendLine("redirectposdevices:i:0");

        sb.AppendLine($"alternate shell:s:{config.LauncherPath}");
        var launcherDir = System.IO.Path.GetDirectoryName(config.LauncherPath);
        sb.AppendLine($"shell working directory:s:{(string.IsNullOrWhiteSpace(launcherDir) ? "C:\\" : launcherDir)}");

        return sb.ToString();
    }
}
