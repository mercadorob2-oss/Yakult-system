using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    public enum AssistantOperation { List, Count, Detail, History, Summary }
    public enum AssistantOutcome { Success, NoMatches, Guidance, Clarification, Denied, Unavailable, Cancelled }

    public sealed class AssistantContext
    {
        public int UserId { get; internal set; }
        public int? EmployeeId { get; internal set; }
        public string EmployeeName { get; internal set; }
        public string UserName { get; internal set; }
        public string Position { get; internal set; }
        public string DepartmentName { get; internal set; }
        public string BranchName { get; internal set; }
        public string CompanyName { get; internal set; }
        public IReadOnlyCollection<string> Roles { get; internal set; }
        public bool IsDepartmentAccount { get; internal set; }
        public int? DepartmentId { get; internal set; }
        public bool IsDeveloper { get; internal set; }
        public bool IsSuperAdmin { get; internal set; }
        public string Module { get; internal set; }
        public string Page { get; internal set; }
        public string SelectedCapability { get; internal set; }
        public long? SelectedRecordId { get; internal set; }
        public string SessionStamp { get; internal set; }
        public TimeZoneInfo TimeZone { get; internal set; } = TimeZoneInfo.Local;
        public IReadOnlyCollection<string> Portals { get; internal set; }
        public IReadOnlyCollection<string> DeniedCategories { get; internal set; }
        public IReadOnlyDictionary<string, bool> Pages { get; internal set; }
        public bool IsLoggedIn { get { return UserId > 0 || IsDepartmentAccount; } }
        public bool HasPortal(string portal) { return IsDeveloper || (Portals != null && Portals.Contains(portal, StringComparer.OrdinalIgnoreCase)); }
        public bool HasPage(string page) { bool allowed; return IsDeveloper || string.IsNullOrEmpty(page) || (Pages != null && Pages.TryGetValue(page, out allowed) && allowed); }

        public static AssistantContext Capture(string module = "Workspace", string page = null,
            string selectedCapability = null, long? selectedRecordId = null)
        {
            var pages = AssistantCatalog.All.SelectMany(c => c.PageKeys.Concat(c.AlternatePage==null?new string[0]:new[] {c.AlternatePage})).Concat(AssistantKnowledge.PageKeys).Concat(AssistantScreens.All.Select(s=>s.PageKey))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(key => key, PermissionResolver.HasPageAccess, StringComparer.OrdinalIgnoreCase);
            var portals = Enum.GetValues(typeof(PermissionResolver.Portal)).Cast<PermissionResolver.Portal>()
                .Where(p => PermissionResolver.HasPortalAccess(AppSession.CurrentUserRoles, p)).Select(p => p.ToString()).ToArray();
            var denied = PermissionResolver.GetRestrictedItemCategories().OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            var context = new AssistantContext
            {
                UserId = AppSession.CurrentUserId, EmployeeId = AppSession.CurrentEmployeeId,
                EmployeeName = AppSession.CurrentEmployeeName, UserName = AppSession.CurrentUserName,
                Position = AppSession.CurrentEmployeePosition, DepartmentName = AppSession.CurrentDepartmentName,
                BranchName = AppSession.CurrentBranchName, CompanyName = AppSession.CurrentCompanyName,
                Roles = Array.AsReadOnly((AppSession.CurrentUserRoles ?? new List<string>()).ToArray()),
                IsDepartmentAccount = AppSession.IsDepartmentAccountSession,
                DepartmentId = AppSession.DepartmentAccountDepartmentId ?? AppSession.CurrentDepartmentId,
                IsDeveloper = AppSession.IsDeveloper, IsSuperAdmin = AppSession.IsSuperAdmin,
                Module = module ?? "Workspace", Page = page, SelectedCapability = selectedCapability,
                SelectedRecordId = selectedRecordId, Portals = Array.AsReadOnly(portals),
                DeniedCategories = Array.AsReadOnly(denied), Pages = new ReadOnlyDictionary<string, bool>(pages)
            };
            context.SessionStamp=Fingerprint(context,AppSession.LoginTime,DatabaseConfig.ConnectionString);
            return context;
        }
        internal static string Fingerprint(AssistantContext context,DateTime loginTime,string connectionString)
        {
            // Only a one-way stamp leaves this method. Credentials are never put into prompts or logs.
            string state = context.UserId + "|" + loginTime.Ticks + "|" + context.EmployeeId + "|" +
                context.IsDepartmentAccount + "|" + context.DepartmentId + "|" + context.IsDeveloper + "|" + context.IsSuperAdmin + "|" +
                connectionString + "|" + string.Join(",", context.Portals ?? new string[0]) + "|" + string.Join(",", context.DeniedCategories ?? new string[0]) + "|" +
                string.Join(",", context.Pages.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value))+"|"+
                string.Join(",",(context.Roles ?? new string[0]).OrderBy(role=>role,StringComparer.OrdinalIgnoreCase))+"|"+context.TimeZone.Id;
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(state)));
        }
    }

    public sealed class AssistantRequest
    {
        public string Capability { get; set; }
        public AssistantOperation Operation { get; set; }
        public long? RecordId { get; set; }
        public long? ParentRecordId { get; set; }
        public string Search { get; set; }
        internal AssistantModelMatch ModelMatch { get; set; }
        internal IReadOnlyList<AssistantOrganization> Organizations { get; set; } = new AssistantOrganization[0];
        public string Status { get; set; }
        public string Priority { get; set; }
        public string Employee { get; set; }
        public string Department { get; set; }
        public string Branch { get; set; }
        public string Company { get; set; }
        public string Condition { get; set; }
        public string Category { get; set; }
        public bool ExactCategory { get; set; }
        public bool Personal { get; set; }
        public string PersonalRelation { get; set; }
        public bool IncludeArchived { get; set; }
        public bool ArchivedOnly { get; set; }
        public string DateField { get; set; }
        public DateTime? FromLocal { get; set; }
        public DateTime? ToLocalExclusive { get; set; }
        public string GroupBy { get; set; }
        public bool LowestStock { get; set; }
        public bool OutstandingOnly { get; set; }
        public bool InStockOnly { get; set; }
        public int Page { get; set; }
        public string Label { get; set; }
        public AssistantRequest Copy() { return (AssistantRequest)MemberwiseClone(); }
    }

    public sealed class AssistantPlan
    {
        public readonly List<AssistantRequest> Requests = new List<AssistantRequest>();
        public string GuidanceQuestion { get; set; }
        public string Message { get; set; }
        public bool IsGeneralQuestion { get; set; }
        public bool Compare { get; set; }
        internal bool NeedsInterpretation { get; set; }
        internal string UnresolvedMeaning { get; set; }
        internal string UnresolvedFilter { get; set; }
    }

    /// <summary>Only request scope is shared with the interpreter, never rows, totals or conversation answers.</summary>
    internal sealed class AssistantInterpretationContext
    {
        internal IReadOnlyList<AssistantRequest> Previous { get; }
        internal AssistantOrganizationResolution Organizations { get; }
        internal AssistantInterpretationContext(IEnumerable<AssistantRequest> previous,AssistantOrganizationResolution organizations=null)
        { Previous=(previous ?? new AssistantRequest[0]).Take(2).Select(r=>r.Copy()).ToList().AsReadOnly();Organizations=organizations; }
    }

    public sealed class AssistantResult
    {
        public AssistantOutcome Outcome { get; internal set; }
        internal AssistantPerformanceReport Performance { get; set; }
        public string Text { get; internal set; }
        public string MessageText { get; internal set; }
        public string Source { get; internal set; }
        public string AppliedScope { get; internal set; }
        public DateTime RetrievedAtUtc { get; internal set; }
        public long TotalRecords { get; internal set; }
        public long TotalGroups { get; internal set; }
        public IReadOnlyDictionary<string, decimal> Totals { get; internal set; }
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Rows { get; internal set; }
        public AssistantRequest Request { get; internal set; }
        public bool HasNextPage { get; internal set; }
        public IReadOnlyList<AssistantResult> Children { get; internal set; } = new AssistantResult[0];
        public bool IsComparison { get; internal set; }
        internal IReadOnlyList<AssistantClarificationChoice> ClarificationChoices { get; set; } = new AssistantClarificationChoice[0];
        public string ExplanationText { get; internal set; }
        public IReadOnlyList<AssistantGuideCitation> GuideCitations { get; internal set; } = new AssistantGuideCitation[0];
        public TimeZoneInfo TimeZone { get; internal set; } = TimeZoneInfo.Local;
        public IReadOnlyList<string> Qualifications { get; internal set; } = new string[0];
        public bool IsLive { get { return Outcome == AssistantOutcome.Success || Outcome == AssistantOutcome.NoMatches; } }
        public static AssistantResult Message(AssistantOutcome outcome, string text)
        {
            return new AssistantResult { Outcome = outcome, Text = text, MessageText = text, RetrievedAtUtc = DateTime.UtcNow,
                Totals = new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>()),
                Rows = new List<IReadOnlyDictionary<string, object>>().AsReadOnly() };
        }
    }

    internal sealed class AssistantClarificationChoice
    {
        internal string PromptId { get; }
        internal string Id { get; }
        internal string Label { get; }
        internal AssistantClarificationChoice(string promptId,string id,string label)
        {PromptId=promptId;Id=id;Label=label;}
    }

    public sealed class AssistantGuideCitation
    {
        public string Id { get; internal set; }
        public string Source { get; internal set; }
    }

    internal sealed class AssistantExplanation
    {
        internal string Text;
        internal IReadOnlyList<AssistantGuideCitation> Citations;
        internal string LegacyText;
        internal string ReadableText { get { return LegacyText ?? Text+"\nGuide sources: "+string.Join("; ",Citations.Select(c=>c.Source+" ["+c.Id+"]"))+"."; } }
    }

    public sealed class AssistantCapability
    {
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string Module { get; internal set; }
        public string Source { get; internal set; }
        public string[] PageKeys { get; internal set; }
        public string[] PortalKeys { get; internal set; }
        public string[] Aliases { get; internal set; }
        internal string BaseSql;
        internal bool OrganizationIds;
        internal string[] Columns;
        internal string[] SearchColumns;
        internal string[] GroupColumns;
        internal string[] AggregateColumns;
        internal string[] UtcDateColumns;
        internal string DefaultDateField;
        internal string DefaultStatus;
        internal string ParentColumn;
        internal string HistoryCapability;
        internal string PersonalPredicate;
        internal string ForcedScopePredicate;
        internal string ArchiveColumn;
        internal bool AdminOnly;
        internal bool DeveloperOnly;
        internal bool SuperAdminOnly;
        internal string AlternatePortal;
        internal string AlternatePage;
        internal bool RequesterScope;
        internal bool CategoryScope;
        public bool IsAllowed(AssistantContext context)
        {
            if (RequesterScope && context.IsLoggedIn && context.HasPortal("RequesterPortal")) return true;
            return context.IsLoggedIn && (!AdminOnly || context.IsDeveloper || context.IsSuperAdmin || (context.Roles!=null && context.Roles.Contains("Admin",StringComparer.OrdinalIgnoreCase))) &&
                (!DeveloperOnly || context.IsDeveloper) && (!SuperAdminOnly || context.IsSuperAdmin) &&
                ((PortalKeys.Any(context.HasPortal) && PageKeys.All(context.HasPage)) ||
                 (AlternatePortal!=null && context.HasPortal(AlternatePortal) && context.HasPage(AlternatePage)));
        }
        public string[] SupportedFilters
        {
            get { return Columns.Where(c => new[] { "Status", "Priority", "Employee", "Department", "Branch", "Company", "Condition", "Category" }.Contains(c)).ToArray(); }
        }
    }

    internal sealed class AssistantSqlQuery
    {
        internal AssistantCapability Capability;
        internal AssistantRequest Request;
        internal string CommandText;
        internal readonly Dictionary<string, object> Parameters = new Dictionary<string, object>();
        internal string AppliedScope;
        internal string[] OutputColumns;
        internal bool IsGrouped;
        internal TimeZoneInfo TimeZone;
        internal readonly List<string> Qualifications = new List<string>();
    }
}
