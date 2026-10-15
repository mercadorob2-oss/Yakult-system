using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    public enum AiSnapshotStatus { Success, NoMatches, Clarification, Denied, Unavailable }

    public sealed class AiSnapshot
    {
        public string Text { get; private set; }
        public AiSnapshotStatus Status { get; private set; }
        public bool IsLive { get { return Status == AiSnapshotStatus.Success || Status == AiSnapshotStatus.NoMatches; } }
        internal AiSnapshot(string text, AiSnapshotStatus status) { Text = text; Status = status; }
    }

    internal sealed class AiSnapshotQuery
    {
        public string Sql;
        public string Title;
        public string Noun;
        public bool HasUnitTotal;
        public readonly Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public Func<DataRow, string> FormatRow;
    }

    /// <summary>
    /// SELECT-only retrieval. Counts and rows come from the same filtered SQL statement.
    /// Factual replies are displayed directly, without asking a model to calculate or rewrite them.
    /// </summary>
    public static class AiContextProviders
    {
        internal const int RowLimit = 8;
        internal const string EmployeeNoise = "who employee employees staff member details profile information name position department branch he she him her them";
        internal const string PolicyNoise = "policy policies guideline guidelines handbook memo memos rules company explain regarding";

        public static async Task<AiSnapshot> FetchAsync(AiRoute route, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (route == null || route.Intent == AiIntent.None) return null;
            if (route.Intent == AiIntent.Clarification) return Message(route.Term);
            if (!AppSession.IsLoggedIn) return Denied("Please sign in before requesting system information.");
            if (route.Intent == AiIntent.SelfProfile ||
                (route.Intent == AiIntent.EmployeeLookup && IsCurrentEmployeeSearch(route.Term)))
                return SelfProfile();
            if (route.Intent == AiIntent.SystemHelp) return SystemHelp(route.Term);
            string accessProblem = AccessProblem(route);
            if (accessProblem != null) return Denied(accessProblem);
            if (route.Intent == AiIntent.EmployeeLookup && AiIntentRouter.SearchTokens(route.Term, EmployeeNoise).Count == 0)
                return Message("Please provide the employee's name or employee number.");
            if (route.Intent == AiIntent.BorrowStatus && route.Personal && AppSession.CurrentEmployeeId.GetValueOrDefault() <= 0)
                return Message("Your account has no linked employee record. I cannot safely identify your borrow records.");
            try
            {
                var query = BuildQuery(route);
                if (query == null) return Message("That lookup is not supported. Try a stock, ticket, request, employee, policy, borrow, or expiry question.");
                DatabaseConfig.EnsureConfigured();
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = CreateCommand(query, con))
                {
                    await con.OpenAsync(ct).ConfigureAwait(false);
                    using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        var table = new DataTable();
                        for (int i = 0; i < reader.FieldCount; i++) table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                        while (await reader.ReadAsync(ct).ConfigureAwait(false))
                        {
                            var values = new object[reader.FieldCount];
                            reader.GetValues(values);
                            table.Rows.Add(values);
                        }
                        ct.ThrowIfCancellationRequested();
                        return FormatSnapshot(query, table);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                try { Logger.LogError("[Yakumi] Lookup failed: " + route.Intent, ex); } catch { }
                return new AiSnapshot("I couldn't retrieve current system data. Please try again or open the relevant page. This is a lookup failure, not a result of zero matches.", AiSnapshotStatus.Unavailable);
            }
        }

        internal static SqlCommand CreateCommand(AiSnapshotQuery query, SqlConnection connection)
        {
            var command = new SqlCommand(query.Sql, connection) { CommandTimeout = 15 };
            foreach (var parameter in query.Parameters)
            {
                var value = parameter.Value;
                if (value is string || parameter.Key == "@Condition") command.Parameters.Add(parameter.Key, SqlDbType.NVarChar, 4000).Value = value;
                else if (value is bool) command.Parameters.Add(parameter.Key, SqlDbType.Bit).Value = value;
                else command.Parameters.Add(parameter.Key, SqlDbType.Int).Value = value ?? DBNull.Value;
            }
            return command;
        }

        private static AiSnapshot Message(string text) { return new AiSnapshot(text, AiSnapshotStatus.Clarification); }
        private static AiSnapshot Denied(string text) { return new AiSnapshot(text, AiSnapshotStatus.Denied); }
        private static bool Portal(PermissionResolver.Portal portal)
        {
            return PermissionResolver.HasPortalAccess(AppSession.CurrentUserRoles, portal);
        }
        private static bool InventoryPage(string page)
        {
            return Portal(PermissionResolver.Portal.InventorySystem) && PermissionResolver.HasPageAccess(page);
        }

        internal static string AccessProblem(AiRoute route)
        {
            bool permitted;
            switch (route.Intent)
            {
                case AiIntent.ItemStock: permitted = InventoryPage("ViewItemsPage"); break;
                case AiIntent.ConsumableStock:
                    permitted = InventoryPage("ConsumableModelsPage") || Portal(PermissionResolver.Portal.CartridgeManagement); break;
                case AiIntent.CartridgeStock:
                    permitted = Portal(PermissionResolver.Portal.CartridgeManagement) && PermissionResolver.CanUseItemCategory("Cartridge"); break;
                case AiIntent.Ticket:
                case AiIntent.TicketCount: permitted = Portal(PermissionResolver.Portal.CallITMonitoring); break;
                case AiIntent.BorrowStatus: permitted = Portal(PermissionResolver.Portal.BorrowItems); break;
                case AiIntent.RenewalsExpiry: permitted = InventoryPage("ViewRenewalPage"); break;
                case AiIntent.SetStatus: permitted = InventoryPage("ViewSetsPage"); break;
                case AiIntent.EmployeeLookup: permitted = InventoryPage("ViewEmployeesPage"); break;
                case AiIntent.RequestStatus:
                case AiIntent.MyRequests:
                    permitted = InventoryPage("ViewRequestsPage") || Portal(PermissionResolver.Portal.RequesterPortal); break;
                default: permitted = true; break;
            }
            return permitted ? null : "Your account does not have permission to view this information.";
        }

        internal static bool IsCurrentEmployeeSearch(string question)
        {
            if (AppSession.IsDepartmentAccountSession || AppSession.CurrentEmployeeId.GetValueOrDefault() <= 0) return false;
            var tokens = AiIntentRouter.SearchTokens(question, EmployeeNoise);
            var ownName = AiIntentRouter.SearchTokens(AppSession.CurrentEmployeeName);
            return tokens.Count > 0 && tokens.All(token => ownName.Contains(token, StringComparer.OrdinalIgnoreCase));
        }

        private static AiSnapshot SelfProfile()
        {
            string name = AppSession.CurrentEmployeeName;
            if (string.IsNullOrWhiteSpace(name)) name = AppSession.CurrentUserName;
            string text = "You are signed in as " + Clean(name, 160) + ".";
            if (AppSession.IsDepartmentAccountSession)
                return Message(text + " This is a department account; it does not identify one employee.");
            text += Field("Position", AppSession.CurrentEmployeePosition) + Field("Department", AppSession.CurrentDepartmentName)
                + Field("Branch", AppSession.CurrentBranchName) + Field("Company", AppSession.CurrentCompanyName);
            if (AppSession.CurrentEmployeeId.GetValueOrDefault() <= 0)
                text += "\nYour account has no linked employee record.";
            // Cached login identity is explicitly labelled; it is not a fresh DB lookup.
            return Message(text + "\nSource: current signed-in session.");
        }

        private static string Field(string label, string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : "\n" + label + ": " + Clean(value, 160);
        }

        private static AiSnapshot SystemHelp(string question)
        {
            if (AiIntentRouter.Has(question, @"\b(access|permission|login|sign in)\b"))
                return Message("The workspace shows the systems your account can access. If a system or page is missing, ask your administrator to review your account's roles and permissions. I can explain access but cannot grant it.");
            if (AiIntentRouter.Has(question, @"\b(stock|inventory|items?)\b"))
                return Message("Open Inventory System, then Items to check item stock. Consumable Management handles cartridge exchanges and refills. You can also ask me 'How many keyboards are in stock?' or specify a toner or cartridge model.");
            if (AiIntentRouter.Has(question, @"\b(borrow|return)\b"))
                return Message("Open Borrow Items to record equipment loans and returns. Ask me 'my borrowed items' to see open loans linked to your employee record. I cannot record a loan or return from chat.");
            if (AiIntentRouter.Has(question, @"\b(request|requests)\b"))
                return Message("Open Request/s Portal to submit a request and follow its approval and fulfillment. Ask 'my requests' or provide a reference such as REQ-123 to check a record. I cannot submit or approve requests from chat.");
            if (AiIntentRouter.Has(question, @"\b(ticket|support|issue|contact)\b"))
                return Message("IT Call Monitoring tracks helpdesk tickets and their resolution. If your account has access, open it to report or follow an issue. Otherwise contact your internal IT team through your usual company channel; I do not have a verified support phone number or email. Ask 'my tickets' or 'status of TCK-69' for a lookup.");
            return Message("I can look up stock, tickets, your requests, employee details, published policies, open borrows, upcoming or expired renewals, and set status, subject to your permissions. Try 'Who am I?', 'my tickets', 'my requests', or 'How many keyboards are in stock?'. I cannot change records. Reports, Repair Technician Portal, and Admin Portal are available from the workspace when your account has access.");
        }

        internal static AiSnapshotQuery BuildQuery(AiRoute route)
        {
            switch (route.Intent)
            {
                case AiIntent.ItemStock: return StockQuery(route, false, false);
                case AiIntent.ConsumableStock: return StockQuery(route, true, false);
                case AiIntent.CartridgeStock: return StockQuery(route, false, true);
                case AiIntent.EmployeeLookup: return EmployeeQuery(route);
                case AiIntent.PolicyLookup: return PolicyQuery(route);
                case AiIntent.Ticket:
                case AiIntent.TicketCount: return TicketQuery(route);
                case AiIntent.MyRequests:
                case AiIntent.RequestStatus: return RequestQuery(route);
                case AiIntent.BorrowStatus: return BorrowQuery(route);
                case AiIntent.RenewalsExpiry: return RenewalQuery(route);
                case AiIntent.SetStatus: return SetQuery(route);
                default: return null;
            }
        }

        // COUNT is evaluated before TOP: the displayed eight rows are never mistaken for the total.
        private static AiSnapshotQuery Query(string title, string noun, string columns, string from,
            string where, string order, Func<DataRow, string> format, string units = null)
        {
            var query = new AiSnapshotQuery
            {
                Title = title, Noun = noun, FormatRow = format, HasUnitTotal = units != null,
                Sql = "SELECT TOP (@Limit) COUNT(*) OVER() AS TotalMatches, " +
                    (units == null ? "" : "SUM(CAST(" + units + " AS BIGINT)) OVER() AS TotalUnits, ") +
                    columns + " FROM " + from + " WHERE " + where + " ORDER BY " + order + ";"
            };
            query.Parameters.Add("@Limit", RowLimit);
            return query;
        }

        internal static string MatchClause(List<string> tokens, Dictionary<string, object> parameters, params string[] columns)
        {
            var clauses = new List<string>();
            foreach (string token in tokens)
            {
                string key = "@Search" + parameters.Count;
                // Parameters prevent SQL injection; escaping also makes LIKE metacharacters literal.
                parameters.Add(key, "%" + token.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%");
                clauses.Add("(" + string.Join(" OR ", columns.Select(column => column + " LIKE " + key + " ESCAPE N'~'")) + ")");
            }
            return clauses.Count == 0 ? "1=1" : string.Join(" AND ", clauses);
        }

        private static void Filter(AiSnapshotQuery query, string predicate)
        {
            int at = query.Sql.LastIndexOf(" ORDER BY ", StringComparison.Ordinal);
            query.Sql = query.Sql.Insert(at, " AND (" + predicate + ")");
        }

        private static void RestrictCategories(AiSnapshotQuery query, string column)
        {
            foreach (string category in PermissionResolver.GetRestrictedItemCategories())
            {
                string key = "@DeniedCategory" + query.Parameters.Count;
                query.Parameters.Add(key, category);
                Filter(query, "ISNULL(" + column + ", '') <> " + key);
            }
        }

        private static AiSnapshotQuery StockQuery(AiRoute route, bool consumable, bool cartridge)
        {
            string from, category, columns, orderKey;
            if (consumable)
            {
                from = @"dbo.ConsumableModel cm OUTER APPLY (SELECT ISNULL(SUM(CAST(ISNULL(i.StockOnHand,0) AS BIGINT)),0) AS Qty
                    FROM dbo.Item i WHERE i.ConsumableModelId=cm.ConsumableModelId AND i.Active=1
                    AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)) s";
                category = "cm.Category"; orderKey = "cm.ModelNumber, cm.ConsumableModelId";
                columns = "cm.ModelNumber AS Name, cm.Category, s.Qty";
            }
            else if (cartridge)
            {
                from = @"dbo.CartridgeModel cm OUTER APPLY (SELECT ISNULL(SUM(CAST(ISNULL(i.StockOnHand,0) AS BIGINT)),0) AS Qty
                    FROM dbo.Item i WHERE i.CartridgeModelId=cm.CartridgeModelId AND i.Category='Cartridge' AND i.Active=1
                    AND ISNULL(i.Remarks,'') NOT LIKE '%IT custody%'
                    AND ((@Condition IS NULL AND (i.RefillStatus IS NULL OR i.RefillStatus='Available'))
                      OR (@Condition='Brand New' AND i.RefillStatus IS NULL)
                      OR (@Condition='Refilled' AND i.RefillStatus='Available'))
                    AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)) s";
                category = "N'Cartridge'"; orderKey = "cm.ModelNumber, cm.CartridgeModelId";
                columns = "cm.ModelNumber AS Name, N'Cartridge' AS Category, s.Qty";
            }
            else
            {
                from = "dbo.Item i CROSS APPLY (SELECT CAST(ISNULL(i.StockOnHand,0) AS BIGINT) AS Qty) s";
                category = "i.Category"; orderKey = "i.Name, i.ItemId";
                columns = "i.Name, i.ModelNumber, i.Category, s.Qty";
            }
            string where = consumable || cartridge ? "cm.IsActive=1" :
                "i.Active=1 AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)";
            var query = Query((route.SortLowest ? "Lowest stock first; no low-stock threshold is configured here. " : "") +
                (cartridge ? "Issuable cartridge stock" : consumable ? "Consumable stock" : "Active item stock") +
                (route.Condition == null ? "" : " (" + route.Condition + ")"), consumable || cartridge ? "models" : "item records",
                columns, from, where, (route.SortLowest ? "s.Qty ASC, " : "") + orderKey,
                row => Text(row,"Name") + (row.Table.Columns.Contains("ModelNumber") ? Optional(row,"ModelNumber"," (",")") : "") +
                    Optional(row,"Category"," [","]") + " — available units: " + Number(row,"Qty"), "s.Qty");
            if (cartridge) query.Parameters.Add("@Condition", (object)route.Condition ?? DBNull.Value);
            var tokens = AiIntentRouter.SearchTokens(route.Term);
            var searchColumns = consumable ? new[] { "cm.ModelNumber", category } : cartridge
                ? new[] { "cm.ModelNumber", "cm.Brand", category } : new[] { "i.Name", "i.ModelNumber", category };
            Filter(query, MatchClause(tokens, query.Parameters, searchColumns));
            RestrictCategories(query, category);
            return query;
        }

        private static AiSnapshotQuery EmployeeQuery(AiRoute route)
        {
            var query = Query("Active employees (matching every search term)", "employees",
                "e.Name, e.Position, e.EmployeeNumber, t.Code AS TitleCode, d.Name AS Department, b.Name AS Branch",
                "dbo.Employee e LEFT JOIN dbo.Title t ON t.TitleId=e.TitleId LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId LEFT JOIN dbo.Branch b ON b.BranchId=e.BranchId",
                "e.Active=1", "e.Name, e.EmpId",
                row => Text(row,"Name") + Optional(row,"TitleCode"," — ","") + Optional(row,"Position",", ","") +
                    Optional(row,"Department",", ","") + Optional(row,"Branch"," (",")") + Optional(row,"EmployeeNumber",", Emp#", ""));
            Filter(query, MatchClause(AiIntentRouter.SearchTokens(route.Term, EmployeeNoise), query.Parameters, "e.Name", "e.EmployeeNumber"));
            return query;
        }

        private static AiSnapshotQuery PolicyQuery(AiRoute route)
        {
            var query = Query("Published policy excerpts (open Company Policy for the full text)", "policies",
                "p.Title, p.Category, p.Summary, LEFT(p.Body, 700) AS Body",
                "dbo.CompanyPolicy p", "p.IsPublished=1", "p.SortOrder, p.Title, p.PolicyId",
                row => Text(row,"Title") + Optional(row,"Category"," [","]") + ": " +
                    (Text(row,"Summary").Length > 0 ? Text(row,"Summary",500) : Text(row,"Body",700)));
            Filter(query, MatchClause(AiIntentRouter.SearchTokens(route.Term, PolicyNoise), query.Parameters, "p.Title", "p.Summary", "p.Body", "p.Category"));
            return query;
        }

        private static AiSnapshotQuery TicketQuery(AiRoute route)
        {
            var query = Query((route.Personal ? "Your tickets (created by or assigned to your account; all dates)" : "Tickets (all dates)") +
                (route.Status == null ? "" : " — " + route.Status), "tickets",
                "t.TicketCode, t.Status, t.CallerName, LEFT(t.Issue,240) AS Issue, e.Name AS Assignee",
                "dbo.CallTicket t LEFT JOIN dbo.Employee e ON e.EmpId=t.AssignedToEmpId", "1=1", "t.CreatedAt DESC, t.TicketId DESC",
                row => Text(row,"TicketCode") + " — " + Text(row,"Status") + Optional(row,"CallerName",", caller: ","") +
                    Optional(row,"Assignee",", assigned to: ","") + Optional(row,"Issue",", issue: ","",240));
            if (route.Intent == AiIntent.Ticket)
            {
                int id;
                if (!int.TryParse(route.Term, out id) || id <= 0) throw new ArgumentException("Invalid ticket number.");
                query.Parameters.Add("@TicketId", id); Filter(query,"t.TicketId=@TicketId");
            }
            if (route.Personal)
            {
                query.Parameters.Add("@UserId",AppSession.CurrentUserId);
                query.Parameters.Add("@EmpId", (object)AppSession.CurrentEmployeeId ?? DBNull.Value);
                Filter(query,"t.CreatedByUserId=@UserId OR t.AssignedToUserId=@UserId OR t.AssignedToEmpId=@EmpId");
            }
            if (route.Status == "Open") Filter(query,"t.Status NOT IN ('Solved','Closed','Resolved (Temporary)','Resolved','Cancelled')");
            else if (route.Status != null) { query.Parameters.Add("@Status",route.Status); Filter(query,"t.Status=@Status"); }
            return query;
        }

        private static AiSnapshotQuery RequestQuery(AiRoute route)
        {
            bool all = InventoryPage("ViewRequestsPage");
            var query = Query(route.Intent == AiIntent.MyRequests ? "Your requests" + (route.Status == "Open" ? " with outstanding quantities" : route.Status == null ? " (all statuses)" : " — " + route.Status) : "Request status",
                "request lines", "r.ReqId, i.Name, r.Quantity, ISNULL(r.IssuedQty,0) AS IssuedQty, r.Status, r.DateCreated",
                "dbo.Request r INNER JOIN dbo.Item i ON i.ItemId=r.ItemId LEFT JOIN dbo.[Set] st ON st.SetId=r.SetId", "r.Active=1", "r.DateCreated DESC, r.ReqId DESC",
                row => "REQ-" + Number(row,"ReqId") + ": " + Text(row,"Name") + " — requested " + Number(row,"Quantity") +
                    ", issued " + Number(row,"IssuedQty") + ", status " + Text(row,"Status") + ", requested on " + Date(row,"DateCreated"));
            if (route.Intent == AiIntent.RequestStatus)
            {
                int id;
                if (!int.TryParse(route.Term,out id) || id <= 0) throw new ArgumentException("Invalid request number.");
                query.Parameters.Add("@ReqId",id); Filter(query,"r.ReqId=@ReqId");
            }
            if (route.Intent == AiIntent.MyRequests || !all)
            {
                query.Parameters.Add("@UserId",AppSession.CurrentUserId);
                Filter(query,"r.CreatedBy=@UserId");
                if (!all) query.Title += " (records created by your account only)";
            }
            if (route.Status == "Open")
                Filter(query,"ISNULL(r.IssuedQty,0)<r.Quantity AND ISNULL(st.Status,'')<>'Dispatched' AND r.Status NOT IN ('Cancelled','Canceled','Rejected','Closed')");
            else if (route.Status != null) { query.Parameters.Add("@Status",route.Status); Filter(query,"r.Status=@Status"); }
            RestrictCategories(query,"i.Category");
            return query;
        }

        private static AiSnapshotQuery BorrowQuery(AiRoute route)
        {
            var query = Query((route.Personal ? "Your " : "") + (route.Status == "Returned" ? "returned borrows" : route.Status == "History" ? "borrow history (open and returned)" : "open borrows"), "borrow records",
                "b.ItemName, b.ModelNumber, b.BorrowedAtUtc, b.BorrowedByEmpName, b.ReturnedAtUtc", "dbo.BorrowLog b",
                route.Status == "Returned" ? "b.ReturnedAtUtc IS NOT NULL" : route.Status == "History" ? "1=1" : "b.ReturnedAtUtc IS NULL",
                "b.BorrowedAtUtc DESC, b.BorrowId DESC",
                row => Text(row,"ItemName") + Optional(row,"ModelNumber"," (",")") + ", borrowed " + Date(row,"BorrowedAtUtc",true) +
                    Optional(row,"BorrowedByEmpName",", holder: ","") + (row.IsNull("ReturnedAtUtc") ? ", still out" : ", returned " + Date(row,"ReturnedAtUtc",true)));
            if (route.Personal) { query.Parameters.Add("@EmpId",AppSession.CurrentEmployeeId.Value); Filter(query,"b.BorrowedByEmpId=@EmpId"); }
            Filter(query,MatchClause(AiIntentRouter.SearchTokens(route.Term),query.Parameters,"b.ItemName","b.ModelNumber","b.BorrowedByEmpName"));
            return query;
        }

        private static AiSnapshotQuery RenewalQuery(AiRoute route)
        {
            var query = Query(route.ExpiredOnly ? "Expired renewal records" : "Renewal records expiring within " + route.DaysAhead + " days", "renewal records",
                "r.RenewalId, i.Name, r.NewEndDate, r.RenewalStatus", "dbo.Renewals r LEFT JOIN dbo.Item i ON i.ItemId=r.ItemId",
                "r.IsArchived=0 AND " + (route.ExpiredOnly ? "r.NewEndDate<CAST(GETDATE() AS DATE)" :
                "r.NewEndDate>=CAST(GETDATE() AS DATE) AND r.NewEndDate<DATEADD(day,@Days+1,CAST(GETDATE() AS DATE))"),
                "r.NewEndDate, r.RenewalId",
                row => (Text(row,"Name").Length == 0 ? "Renewal #" + Number(row,"RenewalId") : Text(row,"Name")) +
                    ", ends " + Date(row,"NewEndDate") + ", status " + Text(row,"RenewalStatus"));
            if (!route.ExpiredOnly) query.Parameters.Add("@Days",route.DaysAhead);
            Filter(query, MatchClause(AiIntentRouter.SearchTokens(route.Term), query.Parameters, "i.Name", "r.PartNumber"));
            RestrictCategories(query,"i.Category");
            return query;
        }

        private static AiSnapshotQuery SetQuery(AiRoute route)
        {
            int id;
            if (!int.TryParse(route.Term,out id) || id <= 0) throw new ArgumentException("Invalid set number.");
            var query = Query("Set status", "sets", "s.SetCode, s.Status, s.CreatedAt, s.IssuedBrandNewQty, s.IssuedRefilledQty",
                "dbo.[Set] s", "s.SetId=@SetId AND s.Active=1", "s.SetId",
                row => Text(row,"SetCode") + ": " + Text(row,"Status") + ", created " + Date(row,"CreatedAt",true) +
                    ", issued brand new " + Number(row,"IssuedBrandNewQty") + ", issued refilled " + Number(row,"IssuedRefilledQty"));
            query.Parameters.Add("@SetId",id);
            return query;
        }

        internal static AiSnapshot FormatSnapshot(AiSnapshotQuery query, DataTable rows)
        {
            long total = rows.Rows.Count == 0 ? 0 : Number(rows.Rows[0],"TotalMatches");
            var text = new StringBuilder(query.Title + ".\n" + total + " matching " + query.Noun + " total. Showing " + rows.Rows.Count + " of " + total + ".");
            if (query.HasUnitTotal) text.Append("\nTotal available units across all matching records: " + (rows.Rows.Count == 0 ? 0 : Number(rows.Rows[0],"TotalUnits")) + ".");
            if (total == 0) text.Append("\nNo matching records within this lookup's scope.");
            foreach (DataRow row in rows.Rows) text.Append("\n- " + query.FormatRow(row));
            if (total > rows.Rows.Count) text.Append("\nThis list is partial; open the relevant page to see the remaining matches.");
            return new AiSnapshot(text.ToString(), total == 0 ? AiSnapshotStatus.NoMatches : AiSnapshotStatus.Success);
        }

        private static long Number(DataRow row,string column)
        {
            return row.IsNull(column) ? 0 : Convert.ToInt64(row[column],CultureInfo.InvariantCulture);
        }
        private static string Text(DataRow row,string column,int limit=160)
        {
            return row.IsNull(column) ? "" : Clean(Convert.ToString(row[column],CultureInfo.InvariantCulture),limit);
        }
        private static string Optional(DataRow row,string column,string before,string after,int limit=160)
        {
            string value=Text(row,column,limit); return value.Length==0 ? "" : before+value+after;
        }
        private static string Date(DataRow row,string column,bool utc=false)
        {
            if(row.IsNull(column)) return "unknown date";
            var date=Convert.ToDateTime(row[column],CultureInfo.InvariantCulture);
            if(utc) date=DateTime.SpecifyKind(date,DateTimeKind.Utc).ToLocalTime();
            return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        }
        internal static string Clean(string value,int limit)
        {
            string text=System.Text.RegularExpressions.Regex.Replace(value ?? "",@"\s+"," ").Trim();
            return text.Length<=limit ? text : text.Substring(0,limit)+"…";
        }
    }
}
