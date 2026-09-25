using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Services
{
    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    /// <summary>
    /// Service layer for Requester Portal functionality.
    /// REUSES existing request infrastructure - no new tables.
    /// Maps portal-specific fields to existing database columns.
    /// </summary>
    public class RequesterPortalService
    {
        private readonly string _connectionString;
        private readonly RequestRepository _requestRepository;
        private readonly CartridgeAuthorizationRepository _authorizationRepository;

        public RequesterPortalService()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");

            _requestRepository         = new RequestRepository();
            _authorizationRepository   = new CartridgeAuthorizationRepository();
        }

        #region Cartridge Item Queries

        /// <summary>
        /// Gets all active, requestable cartridge models from dbo.CartridgeModel.
        /// Based on the model registry — not filtered by Item stock availability.
        /// Excludes archived and inactive models.
        /// </summary>
        public List<Models.ViewModels.CartridgeModelAvailabilityViewModel> GetCartridgeModelsWithAvailability()
        {
            var models = new List<Models.ViewModels.CartridgeModelAvailabilityViewModel>();

            const string sql = @"
                SELECT
                    cm.ModelNumber  AS ModelKey,
                    cm.ModelNumber,
                    cm.CartridgeModelId AS SampleItemId
                FROM dbo.CartridgeModel cm
                LEFT JOIN dbo.ArchiveStatus archCM
                    ON  archCM.EntityType = 'CartridgeModel'
                    AND archCM.EntityId   = cm.CartridgeModelId
                    AND archCM.IsArchived = 1
                WHERE cm.IsRequestable  = 1
                  AND cm.IsActive       = 1
                  AND archCM.EntityId IS NULL
                ORDER BY cm.ModelNumber";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        models.Add(new Models.ViewModels.CartridgeModelAvailabilityViewModel
                        {
                            ModelKey          = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            ModelNumber       = modelNumber,
                            ItemName          = modelNumber,
                            ItemId            = reader.GetInt32(2),
                            AvailableQuantity = 0
                        });
                    }
                }
            }

            return models;
        }

        /// <summary>
        /// Retrieves all active cartridge items (Category = 'Cartridge').
        /// REUSES existing Item table with category filter.
        ///
        /// IMPORTANT: Returns ONE ROW PER CARTRIDGE - no grouping by ModelNumber.
        /// Each cartridge is uniquely identified by SerialNumber (via ItemId).
        /// ModelNumber is descriptive only and must NEVER be used for lookup or grouping.
        /// </summary>
        public List<CartridgeItemDto> GetCartridgeItems()
        {
            var items = new List<CartridgeItemDto>();

            // IMPORTANT: No PARTITION BY ModelNumber - each cartridge is a unique entity
            // identified by SerialNumber/ItemId. Model-based grouping caused duplicate records.
            const string sql = @"
                SELECT
                    i.ItemId,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    i.SerialNumber,
                    i.Category,
                    i.Description,
                    i.Amount AS UnitPrice,
                    i.StockOnHand,
                    i.Active
                FROM dbo.Item i
                WHERE i.Category = 'Cartridge'
                  AND i.Active = 1
                  AND ISNULL(i.RefillStatus, '') <> 'For Refill'
                ORDER BY i.SerialNumber, i.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new CartridgeItemDto
                        {
                            ItemId = reader.GetInt32(0),
                            ItemName = reader.GetString(1),
                            ModelNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                            SerialNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Category = reader.GetString(4),
                            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                            UnitPrice = reader.GetDecimal(6),
                            StockOnHand = reader.GetInt32(7),
                            Active = reader.GetBoolean(8)
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Category text drifts across this system — dbo.ConsumableModel.Category and
        /// dbo.Item.Category may store "Print Head" / "Printhead" / "Toner" / "Toner Cartridge"
        /// for the same bucket, while the portal dropdown sends one fixed spelling. An exact
        /// "= @Category" match silently returns nothing when they disagree. Collapse both
        /// sides to a lowercase, no-space substring needle and match with LIKE.
        /// MATCHES: Inventory.RequestPortal (Web) CanonicalCategoryNeedle.
        /// </summary>
        private static string CanonicalCategoryNeedle(string category)
        {
            var n = (category ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
            if (n.Contains("ink")) return "ink";
            if (n.Contains("toner")) return "toner";
            if (n.Contains("print") && n.Contains("head")) return "printhead";
            return n;
        }

        /// <summary>
        /// Gets all active, requestable consumable models for a given category
        /// ("Ink", "Printhead", or "Toner") from dbo.ConsumableModel.
        /// Mirrors GetCartridgeModelsWithAvailability's shape for the Model + Quantity
        /// dropdown pattern, but reads the ConsumableModel registry instead of CartridgeModel.
        /// MATCHES: Inventory.RequestPortal (Web) GetConsumableModelsWithAvailability
        /// </summary>
        public List<Models.ViewModels.CartridgeModelAvailabilityViewModel> GetConsumableModelsWithAvailability(string category)
        {
            var models = new List<Models.ViewModels.CartridgeModelAvailabilityViewModel>();

            const string sql = @"
                SELECT
                    cm.ModelNumber AS ModelKey,
                    cm.ModelNumber,
                    cm.ConsumableModelId AS SampleItemId
                FROM dbo.ConsumableModel cm
                WHERE cm.IsRequestable = 1
                  AND cm.IsActive      = 1
                  AND REPLACE(LOWER(cm.Category), ' ', '') LIKE @CategoryPattern
                ORDER BY cm.ModelNumber";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryPattern", "%" + CanonicalCategoryNeedle(category) + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                            models.Add(new Models.ViewModels.CartridgeModelAvailabilityViewModel
                            {
                                ModelKey          = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                ModelNumber       = modelNumber,
                                ItemName          = modelNumber,
                                ItemId            = reader.GetInt32(2),
                                AvailableQuantity = 0
                            });
                        }
                    }
                }
            }

            return models;
        }

        #endregion

        #region Employee Queries

        /// <summary>
        /// Gets all active employees for employee selector dropdown.
        /// Returns ViewModels with organizational details pre-populated.
        /// Used in modernized portal for selecting employee on whose behalf request is created.
        /// </summary>
        public List<Models.ViewModels.EmployeeViewModel> GetActiveEmployees()
        {
            var employees = new List<Models.ViewModels.EmployeeViewModel>();

            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name,
                    e.EmployeeNumber,
                    e.Position,
                    e.ComId,
                    e.BranchId,
                    e.DeptId,
                    c.Name AS CompanyName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        employees.Add(new Models.ViewModels.EmployeeViewModel
                        {
                            EmpId          = reader.GetInt32(0),
                            Name           = reader.GetString(1),
                            EmployeeNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Position       = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ComId          = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            BranchId       = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                            DeptId         = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            CompanyName    = reader.IsDBNull(7) ? null : reader.GetString(7),
                            BranchName     = reader.IsDBNull(8) ? null : reader.GetString(8),
                            DepartmentName = reader.IsDBNull(9) ? null : reader.GetString(9),
                            Active         = true
                        });
                    }
                }
            }

            return employees;
        }

        /// <summary>
        /// Get active employees filtered by the logged-in DepartmentAccount's
        /// CompanyId, DepartmentId, and BranchId.
        /// </summary>
        public List<Models.ViewModels.EmployeeViewModel> GetEmployeesByAccount(
            int companyId, int departmentId, int branchId)
        {
            var employees = new List<Models.ViewModels.EmployeeViewModel>();

            const string sql = @"
                SELECT
                    e.EmpId,
                    e.Name,
                    e.EmployeeNumber,
                    e.Position,
                    e.ComId,
                    e.BranchId,
                    e.DeptId,
                    c.Name AS CompanyName,
                    b.Name AS BranchName,
                    d.Name AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Company    c ON e.ComId    = c.ComId
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active    = 1
                  AND e.ComId     = @CompanyId
                  AND e.DeptId    = @DepartmentId
                  AND e.BranchId  = @BranchId
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId",    companyId);
                    cmd.Parameters.AddWithValue("@DepartmentId", departmentId);
                    cmd.Parameters.AddWithValue("@BranchId",     branchId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employees.Add(new Models.ViewModels.EmployeeViewModel
                            {
                                EmpId          = reader.GetInt32(0),
                                Name           = reader.GetString(1),
                                EmployeeNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Position       = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ComId          = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                BranchId       = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                DeptId         = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                CompanyName    = reader.IsDBNull(7) ? null : reader.GetString(7),
                                BranchName     = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DepartmentName = reader.IsDBNull(9) ? null : reader.GetString(9),
                                Active         = true
                            });
                        }
                    }
                }
            }

            return employees;
        }

        #endregion

        #region Destination Queries (Company/Branch/Department)

        /// <summary>
        /// Get all active companies.
        /// </summary>
        public List<CompanyDto> GetActiveCompanies()
        {
            var companies = new List<CompanyDto>();

            const string sql = @"
                SELECT ComId, Name, Description
                FROM dbo.Company
                WHERE Active = 1
                ORDER BY Name";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyDto
                        {
                            ComId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
                    }
                }
            }

            return companies;
        }

        /// <summary>
        /// Get branches for a specific company.
        /// </summary>
        public List<BranchDto> GetBranchesByCompany(int companyId)
        {
            var branches = new List<BranchDto>();

            // Try BDC-based query first (post-migration schema).
            // Fall back to legacy Branch.ComId column if BDC returns nothing and the column exists.
            const string sqlBdc = @"
                SELECT
                    b.BranchId,
                    b.Name,
                    b.Description,
                    ISNULL(bdc_co.CompanyID, 0) AS ComId,
                    bdc_dept.DepartmentID       AS DeptId
                FROM dbo.Branch b
                OUTER APPLY (
                    SELECT TOP 1 bdc.CompanyID
                    FROM   dbo.BranchDepartmentCompany bdc
                    WHERE  bdc.BranchID = b.BranchId
                    ORDER BY bdc.BranchDeptCompanyID
                ) bdc_co
                OUTER APPLY (
                    SELECT TOP 1 bdc.DepartmentID
                    FROM   dbo.BranchDepartmentCompany bdc
                    WHERE  bdc.BranchID = b.BranchId AND bdc.DepartmentID IS NOT NULL
                    ORDER BY bdc.BranchDeptCompanyID
                ) bdc_dept
                WHERE b.Active = 1
                  AND EXISTS (
                      SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                      WHERE bdc.BranchID = b.BranchId AND bdc.CompanyID = @ComId
                  )
                ORDER BY b.Name";

            // Legacy fallback: Branch still has ComId column (pre-migration or partial migration).
            // Use dynamic SQL so the statement only executes if the column actually exists.
            const string sqlLegacy = @"
                IF COL_LENGTH('dbo.Branch', 'ComId') IS NOT NULL
                BEGIN
                    EXEC sp_executesql N'
                        SELECT b.BranchId, b.Name, b.Description, b.ComId, NULL AS DeptId
                        FROM dbo.Branch b
                        WHERE b.Active = 1 AND b.ComId = @ComId
                        ORDER BY b.Name',
                    N''@ComId INT'', @ComId
                END";

            void ReadBranches(System.Data.SqlClient.SqlDataReader reader)
            {
                while (reader.Read())
                {
                    branches.Add(new BranchDto
                    {
                        BranchId    = reader.GetInt32(0),
                        Name        = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ComId       = reader.IsDBNull(3) ? 0  : reader.GetInt32(3),
                        DeptId      = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                    });
                }
            }

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sqlBdc, con))
                {
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    using (var reader = cmd.ExecuteReader())
                        ReadBranches(reader);
                }

                if (branches.Count == 0)
                {
                    using (var cmd = new SqlCommand(sqlLegacy, con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", companyId);
                        using (var reader = cmd.ExecuteReader())
                            ReadBranches(reader);
                    }
                }
            }

            return branches;
        }

        /// <summary>
        /// Get departments for a specific company.
        /// </summary>
        public List<DepartmentDto> GetDepartmentsByCompany(int companyId)
        {
            var departments = new List<DepartmentDto>();

            // BDC-based query (preferred, post-migration)
            const string sqlBdc = @"
                SELECT
                    d.DeptId,
                    d.Name,
                    d.Description,
                    ISNULL(bdc_co.CompanyID, 0) AS ComId
                FROM dbo.Department d
                OUTER APPLY (
                    SELECT TOP 1 bdc.CompanyID
                    FROM   dbo.BranchDepartmentCompany bdc
                    WHERE  bdc.DepartmentID = d.DeptId
                    ORDER BY bdc.BranchDeptCompanyID
                ) bdc_co
                WHERE d.Active = 1
                  AND EXISTS (
                      SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                      WHERE bdc.DepartmentID = d.DeptId AND bdc.CompanyID = @ComId
                  )
                ORDER BY d.Name";

            // Legacy fallback: derive departments from active employees of this company
            const string sqlLegacy = @"
                SELECT DISTINCT d.DeptId, d.Name, d.Description, @ComId AS ComId
                FROM dbo.Department d
                INNER JOIN dbo.Employee e ON e.DeptId = d.DeptId
                WHERE d.Active = 1
                  AND e.IsActive = 1
                  AND e.ComId = @ComId
                ORDER BY d.Name";

            void ReadDepts(System.Data.SqlClient.SqlDataReader reader)
            {
                while (reader.Read())
                {
                    departments.Add(new DepartmentDto
                    {
                        DeptId      = reader.GetInt32(0),
                        Name        = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ComId       = reader.GetInt32(3)
                    });
                }
            }

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sqlBdc, con))
                {
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    using (var reader = cmd.ExecuteReader())
                        ReadDepts(reader);
                }

                if (departments.Count == 0)
                {
                    using (var cmd = new SqlCommand(sqlLegacy, con))
                    {
                        cmd.Parameters.AddWithValue("@ComId", companyId);
                        using (var reader = cmd.ExecuteReader())
                            ReadDepts(reader);
                    }
                }
            }

            return departments;
        }

        public List<BranchDto> GetAllActiveBranches()
        {
            var branches = new List<BranchDto>();
            const string sql = @"
                SELECT BranchId, Name, Description
                FROM dbo.Branch
                WHERE Active = 1
                ORDER BY Name";
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        branches.Add(new BranchDto
                        {
                            BranchId    = reader.GetInt32(0),
                            Name        = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
            }
            return branches;
        }

        public List<DepartmentDto> GetAllActiveDepartments()
        {
            var departments = new List<DepartmentDto>();
            const string sql = @"
                SELECT DeptId, Name, Description
                FROM dbo.Department
                WHERE Active = 1
                ORDER BY Name";
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        departments.Add(new DepartmentDto
                        {
                            DeptId      = reader.GetInt32(0),
                            Name        = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        });
            }
            return departments;
        }

        /// <summary>
        /// Returns users who hold an approver-level position in the given branch/department.
        /// Delegates to the repository — exposed here so ViewModels can call through the service layer.
        /// </summary>
        public List<ApproverViewModel> GetApproversByScope(int comId, int branchId, int deptId)
        {
            return _authorizationRepository.GetApproversByScope(comId, branchId, deptId);
        }

        /// <summary>
        /// Creates a portal employee record with the user-provided name and position.
        /// This allows external portal users to be tracked as employees in the system.
        /// </summary>
        private int CreatePortalEmployee(string employeeName, string position, int companyId, int branchId, int departmentId, int createdByUserId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();

                // Check if an employee with this exact name and position already exists at this location
                const string sqlFind = @"
                    SELECT TOP 1 EmpId
                    FROM dbo.Employee
                    WHERE Name = @Name
                      AND Position = @Position
                      AND ComId = @ComId
                      AND BranchId = @BranchId
                      AND DeptId = @DeptId
                      AND Active = 1
                    ORDER BY EmpId";

                using (var cmd = new SqlCommand(sqlFind, con))
                {
                    cmd.Parameters.AddWithValue("@Name", employeeName);
                    cmd.Parameters.AddWithValue("@Position", position);
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    cmd.Parameters.AddWithValue("@DeptId", departmentId);

                    var result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        return (int)result;
                    }
                }

                // Create new employee record with user-provided name and position
                const string sqlInsert = @"
                    INSERT INTO dbo.Employee
                        (Name, Description, ComId, BranchId, DeptId, DateCreated, CreatedBy, Active, Position, EmployeeNumber)
                    VALUES
                        (@Name, @Description, @ComId, @BranchId, @DeptId, @DateCreated, @CreatedBy, 1, @Position, @EmployeeNumber);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (var cmd = new SqlCommand(sqlInsert, con))
                {
                    cmd.Parameters.AddWithValue("@Name", employeeName);
                    cmd.Parameters.AddWithValue("@Description", "[PORTAL USER] Requester from external portal");
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    cmd.Parameters.AddWithValue("@DeptId", departmentId);
                    cmd.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CreatedBy", createdByUserId);
                    cmd.Parameters.AddWithValue("@Position", position);
                    cmd.Parameters.AddWithValue("@EmployeeNumber", DBNull.Value);

                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        #endregion

        #region Request Submission

        /// <summary>
        /// Creates cartridge requests using Model + Quantity selection.
        /// MATCHES: Inventory.RequestPortal (Web) CreateCartridgeRequestByModel logic.
        ///
        /// KEY DIFFERENCES FROM CreateCartridgeRequest (preserved for admin-only context):
        /// - empId provided directly from employee dropdown selection (no CreatePortalEmployee call)
        /// - Uses placeholder ItemId (portal requests are intent-only, no inventory allocation)
        /// - EntryType forced to "None" via [PORTAL] tag detection in RequestRepository
        /// - [MODEL:XXX] kept in description so admin ViewRequestsPage can display model info
        ///
        /// PORTAL REQUESTS ARE INTENT-ONLY:
        /// - No inventory allocation at request time
        /// - No stock decrement at request time
        /// - IT Fulfillment resolves the actual items later
        /// </summary>
        public (List<int> createdIds, bool wasAutoApproved, int authorizationId, int? setId) CreateCartridgeRequestByModel(
            Models.ViewModels.CartridgeRequestViewModel request,
            List<Models.ViewModels.CartridgeRequestItemViewModel> requestItems,
            int empId,
            int createdByUserId,
            bool isAssisted = false,
            ITAuthorizationInfo itAuth = null)
        {
            // BACKEND AUTHORIZATION: assisted requests are restricted to IT staff regardless of UI
            if (isAssisted && !AppSession.IsITStaff)
                throw new UnauthorizedAccessException("Only IT staff can submit assisted requests on behalf of other employees.");

            var normalizedItems = (requestItems ?? new List<Models.ViewModels.CartridgeRequestItemViewModel>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ModelKey))
                .ToList();

            if (normalizedItems.Count == 0)
                throw new ArgumentException("At least one cartridge model is required.");

            foreach (var item in normalizedItems)
            {
                if (item.Quantity <= 0)
                    throw new ArgumentException($"Quantity for model '{item.CartridgeModel}' must be greater than 0.");
                // Good/Damaged empty-cartridge tracking only applies to the Cartridge category.
                bool isCartridgeItem = string.IsNullOrWhiteSpace(item.Category) || item.Category == "Cartridge";
                bool returningEmpties = item.GoodEmptyQty > 0 || item.DamagedEmptyQty > 0;
                if (isCartridgeItem && returningEmpties && item.GoodEmptyQty + item.DamagedEmptyQty != item.Quantity)
                    throw new ArgumentException($"Good + Damaged empty qty ({item.GoodEmptyQty + item.DamagedEmptyQty}) must equal the requested quantity ({item.Quantity}) for '{item.CartridgeModel}'.");
            }

            string destinationInfo = GetDestinationInfo(
                request.DestinationCompanyId,
                request.DestinationBranchId,
                request.DestinationDepartmentId);

            // All requests in this batch share the same SubmissionSessionId
            Guid submissionSessionId = Guid.NewGuid();

            string fulfillmentMethod = !string.IsNullOrWhiteSpace(request.FulfillmentMethod)
                ? request.FulfillmentMethod
                : "PICKUP";

            // Always keep [PORTAL] tag so RequestRepository portal-detection works for both flows.
            // RequestSource column distinguishes self vs assisted at the DB level.
            string requestSource = isAssisted ? "PORTAL_ASSISTED" : "PORTAL";
            string baseDescription = $"[PORTAL] {fulfillmentMethod} → {destinationInfo}";

            var createdIds = new List<int>();

            // Composition of the WHOLE submission, computed once — this is the single point
            // that decides Card 1 (Cartridge Management) vs Card 2 (Request & Set Management)
            // routing. A cartridge line inside a MIXED submission must NOT be pooled/placeholder;
            // the entire submission routes through the general Request flow instead, exactly
            // like a pure Ink/Printhead/Toner request (WorkflowType below).
            bool allCartridge = normalizedItems.All(x =>
                string.IsNullOrWhiteSpace(x.Category) || x.Category == "Cartridge");

            // Explicit workflow ownership, assigned once for the whole submission and persisted
            // on every Request row it creates (dbo.Request.WorkflowType). Both portals filter on
            // this column directly now, instead of inferring ownership from CartridgeRequestModel
            // existence or the resolved Item's category.
            string workflowType = allCartridge ? "CartridgeManagement" : "RequestSetManagement";

            foreach (var item in normalizedItems)
            {
                bool isCartridgeItem = string.IsNullOrWhiteSpace(item.Category) || item.Category == "Cartridge";

                int itemId;
                if (isCartridgeItem && allCartridge)
                    // Cartridge-only submission: pooled placeholder ItemId - portal requests are
                    // INTENT-ONLY, no inventory allocation. Feeds Cartridge Management's queue.
                    itemId = GetPlaceholderCartridgeItemId();
                else if (isCartridgeItem)
                    // Cartridge item inside a MIXED submission: resolve a real Item, same as
                    // Ink/Printhead/Toner, so it appears only in the general Request flow.
                    itemId = ResolveCartridgeItemId(item.ModelKey);
                else
                    // Ink/Printhead/Toner Cartridge: resolve to a real Item under the selected
                    // ConsumableModel, since those categories aren't pooled.
                    itemId = ResolveConsumableItemId(item.Category, item.ModelKey);

                // Include [MODEL:XXX] tag for admin display in ViewRequestsPage
                // ADMIN-SPECIFIC: Web portal omits this tag, but admin view needs model info
                string description = $"{baseDescription} - [MODEL:{item.ModelKey}]";

                // CartridgeType ("With Cartridge" / "Without Cartridge") is the primary value read
                // back as ConditionType by GetPendingCartridgeRequests (via crm.Remarks) — but that
                // queue only exists for pure cartridge-only submissions (InsertCartridgeRequestModel
                // below is only ever called in that case). Outside of it, there is no downstream
                // reader expecting a condition/category tag in Remarks, so tagging it there just
                // shows up as a fabricated "remark" the requester never typed (e.g. the bare
                // category "Ink") on pages like Set Details. Only synthesize the tag when it's
                // actually consumed.
                bool needsConditionTag = isCartridgeItem && allCartridge;
                string conditionPart = needsConditionTag
                    ? (!string.IsNullOrWhiteSpace(item.CartridgeType) ? item.CartridgeType : "With Cartridge")
                    : null;
                string remarks = conditionPart == null
                    ? (request.AdditionalRemarks ?? string.Empty)
                    : !string.IsNullOrWhiteSpace(request.AdditionalRemarks)
                        ? $"{conditionPart} | {request.AdditionalRemarks}"
                        : conditionPart;

                var requestDto = new Pages.RequestDto
                {
                    DateRequested = request.DateRequested ?? DateTime.Now,
                    Description = description,
                    Remarks = remarks,
                    // All requests start as 'Awaiting Authorization' and become 'Under Review'
                    // only after the assigned approver signs off. IT-assisted requests no longer
                    // bypass the approval chain — they are routed to the approver just like
                    // self-submitted requests (CreateITAuthorized now inserts as Pending).
                    Status = "Awaiting Authorization",
                    EntryType = "None", // Enforced by [PORTAL] detection in RequestRepository
                    Quantity = item.Quantity,
                    UnitPrice = 0,
                    DateCreated = DateTime.Now,
                    CreatedByUserId = createdByUserId,
                    ItemId = itemId,
                    EmpId = empId > 0 ? empId : (int?)null,
                    ComId    = request.DestinationCompanyId    > 0 ? request.DestinationCompanyId    : (int?)null,
                    BranchId = request.DestinationBranchId     > 0 ? request.DestinationBranchId     : (int?)null,
                    DeptId   = request.DestinationDepartmentId > 0 ? request.DestinationDepartmentId : (int?)null,
                    EmployeeName = request.EmployeeName,
                    SubmissionSessionId = submissionSessionId,
                    RequestSource = requestSource,
                    ReceivedById = request.ReceivedById,
                    WorkflowType = workflowType
                };

                int newReqId = _requestRepository.AddRequest(requestDto);
                createdIds.Add(newReqId);

                // Every cartridge line records its model and declared Good/Damaged empties in
                // dbo.CartridgeRequestModel, whether the submission is cartridge-only or mixed.
                // A mixed line still never reaches Cartridge Management's queue: that queue
                // filters on WorkflowType = 'CartridgeManagement', not on this table. Fulfillment
                // of a mixed line reads the declared empties from here
                // (CartridgeManagementRepository.IssueMixedCartridgeLine).
                if (isCartridgeItem)
                    InsertCartridgeRequestModel(newReqId, item.CartridgeModel, item.Quantity, remarks, item.GoodEmptyQty, item.DamagedEmptyQty);
            }

            // Store as JSON so the authorization preview can render full detail
            // (model name, quantity, good/damaged empty returned).
            string requestedModelsList = System.Text.Json.JsonSerializer.Serialize(
                normalizedItems
                    .Where(x => !string.IsNullOrWhiteSpace(x.CartridgeModel))
                    .Select(x => new
                    {
                        model   = x.CartridgeModel,
                        qty     = x.Quantity,
                        good    = x.GoodEmptyQty,
                        damaged = x.DamagedEmptyQty
                    }));

            bool resultWasAutoApproved;
            int resultAuthId;

            if (isAssisted)
            {
                if (itAuth == null)
                    throw new ArgumentNullException(nameof(itAuth), "Authorization details are required for IT-assisted requests.");

                if (itAuth.UsePortalFlow)
                {
                    // Manual Authorization toggle is OFF: submit as Pending for digital approval.
                    resultAuthId = _authorizationRepository.CreateITPending(
                        empId,
                        request.DestinationDepartmentId,
                        submissionSessionId,
                        requestedModelsList,
                        createdByUserId);
                    resultWasAutoApproved = false;
                }
                else
                {
                    // Manual Authorization toggle is ON: record offline/verbal decision.
                    if (string.IsNullOrWhiteSpace(itAuth.Decision))
                        throw new ArgumentException("An authorization decision (Approved/Rejected) is required.");
                    if (itAuth.Decision == "Rejected" && string.IsNullOrWhiteSpace(itAuth.Remarks))
                        throw new ArgumentException("Remarks are required when rejecting an IT-assisted request.");

                    resultAuthId = _authorizationRepository.CreateITAuthorized(
                        empId,
                        request.DestinationDepartmentId,
                        submissionSessionId,
                        requestedModelsList,
                        createdByUserId,
                        itAuth.AuthorizedByEmpId,
                        itAuth.Decision,
                        itAuth.Remarks);
                    resultWasAutoApproved = itAuth.Decision == "Approved";
                }
            }
            else
            {
                // Self-requests: auto-approve only for IsDeveloper=1 or LevelRank>=999.
                // Otherwise Pending — higher-up personal accounts self-sign via the approval page.
                var (wasAutoApproved, authorizationId) = _authorizationRepository.CreateAutoOrPending(
                    empId,
                    request.DestinationDepartmentId,
                    submissionSessionId,
                    requestedModelsList,
                    createdByUserId);
                resultWasAutoApproved = wasAutoApproved;
                resultAuthId          = authorizationId;
            }

            // Every Request Portal submission (New Request + Assisted Request tabs — the only two
            // callers of this method) is auto-grouped into a Set — a Set of one is still a Set,
            // consistent with how sets already work elsewhere. This uses the same grouping
            // mechanism the manual "Add to Set" flow in Request & Set Management uses, and does
            // NOT apply to BatchAddRequestDialog or other admin entry points, since those call
            // RequestRepository.AddRequest directly and never reach here.
            //
            // Extra guard: only group when every line item is a known consumable model category
            // (see Models.ConsumableCategories — the single place to register a new category).
            // Any other request type must never be swept into a Set here.
            bool isConsumableModelRequest = normalizedItems.All(x => Models.ConsumableCategories.IsKnown(x.Category));

            // Only IT-assisted submissions are grouped into a Set right away. A self-service
            // submission (New Request tab) must first be authorized by the requester's
            // supervisor; once approved it is delivered to the Mixed Request Exchange page,
            // and only when IT fulfills it there is it grouped into a Set (which is what
            // deducts the quantity). Grouping here would put an unapproved request on Request
            // & Set Management and deduct its quantity before anyone approved it.
            int? groupedSetId = isAssisted && isConsumableModelRequest
                ? GroupIntoSet(createdIds, createdByUserId, requestSource)
                : (int?)null;

            return (createdIds, resultWasAutoApproved, resultAuthId, groupedSetId);
        }

        /// <summary>
        /// Wraps a batch of just-created Request rows into a new Set, mirroring the manual
        /// "Add to Set" flow (SetRepository.CreateSetAsync + AddRequestToSetAsync) so multi-item
        /// Request Portal submissions show up grouped in Request & Set Management instead of as
        /// separate unlinked rows. Returns the new SetId on success, or null if grouping failed.
        /// </summary>
        private int? GroupIntoSet(List<int> requestIds, int createdByUserId, string requestSource)
        {
            try
            {
                var setRepository = new SetRepository();
                int setId = setRepository.CreateSetAsync(
                    createdByUserId,
                    remarks: $"Auto-grouped from {requestSource} submission").GetAwaiter().GetResult();

                foreach (int reqId in requestIds)
                    setRepository.AddRequestToSetAsync(reqId, setId).GetAwaiter().GetResult();

                return setId;
            }
            catch
            {
                // Grouping is a convenience, not a correctness requirement — the individual
                // Request rows were already committed above. Never fail the whole submission
                // over a Set-linking error.
                return null;
            }
        }

        /// <summary>
        /// Inserts a row into dbo.CartridgeRequestModel to record the model selected at request time,
        /// along with the Good/Damaged empty cartridge quantities.
        /// MATCHES: Inventory.RequestPortal (Web) InsertCartridgeRequestModel
        /// </summary>
        private void InsertCartridgeRequestModel(int reqId, string cartridgeModel, int requestedQty, string remarks, int goodEmptyQty = 0, int damagedEmptyQty = 0)
        {
            if (string.IsNullOrWhiteSpace(cartridgeModel))
                return;

            const string sql = @"
                IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO dbo.CartridgeRequestModel
                        (ReqId, CartridgeModel, RequestedQty, GoodEmptyQty, DamagedEmptyQty, Status, Remarks)
                    VALUES
                        (@ReqId, @CartridgeModel, @RequestedQty, @GoodEmptyQty, @DamagedEmptyQty, 'Pending', @Remarks)
                END";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel.Trim());
                    cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
                    bool hasEmpties = goodEmptyQty > 0 || damagedEmptyQty > 0;
                    cmd.Parameters.AddWithValue("@GoodEmptyQty",    hasEmpties ? (object)goodEmptyQty    : DBNull.Value);
                    cmd.Parameters.AddWithValue("@DamagedEmptyQty", hasEmpties ? (object)damagedEmptyQty : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Remarks", (object)remarks ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Gets a placeholder cartridge ItemId for portal requests.
        /// Portal requests are INTENT-ONLY and do not allocate specific inventory.
        /// MATCHES: Inventory.RequestPortal (Web) GetPlaceholderCartridgeItemId
        /// </summary>
        private int GetPlaceholderCartridgeItemId()
        {
            const string sql = @"
                SELECT TOP 1 ItemId
                FROM dbo.Item
                WHERE Category = 'Cartridge' AND Active = 1
                ORDER BY ItemId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        return (int)result;
                }
            }

            throw new InvalidOperationException("No cartridge items found in the system.");
        }

        /// <summary>
        /// Resolves a real dbo.Item row for a Cartridge model selection when the cartridge line
        /// is part of a MIXED submission (not all-cartridge) — in that case the whole submission
        /// routes through the general Request flow, so this line needs a genuine ItemId like
        /// Ink/Printhead/Toner instead of the pooled placeholder used for cartridge-only
        /// submissions. Falls back to any active cartridge item if the specific model has no
        /// matching Item row — portal requests are intent-only, no stock requirement.
        /// </summary>
        private int ResolveCartridgeItemId(string modelKey)
        {
            const string sql = @"
                SELECT MIN(i.ItemId)
                FROM dbo.Item i
                INNER JOIN dbo.CartridgeModel cm
                    ON  cm.CartridgeModelId = i.CartridgeModelId
                    AND cm.IsRequestable    = 1
                WHERE i.Category = 'Cartridge'
                  AND i.Active   = 1
                  AND ISNULL(NULLIF(LTRIM(RTRIM(i.ModelNumber)), ''), i.Name) = @ModelKey";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ModelKey", (modelKey ?? string.Empty).Trim());
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return (int)result;
                }
            }

            return GetPlaceholderCartridgeItemId();
        }

        /// <summary>
        /// Resolves a real dbo.Item row for an Ink/Printhead/Toner Cartridge model selection.
        /// Unlike cartridges (pooled by model, placeholder ItemId), these categories map a
        /// ConsumableModel 1:1-ish to real Item rows, so we resolve to an actual item under
        /// that model. Falls back to any active item in the category if the specific model
        /// has no Item rows yet (registry exists but stock hasn't been received).
        /// MATCHES: Inventory.RequestPortal (Web) ResolveConsumableItemId
        /// </summary>
        private int ResolveConsumableItemId(string category, string modelKey)
        {
            const string byModelSql = @"
                SELECT MIN(i.ItemId)
                FROM dbo.Item i
                INNER JOIN dbo.ConsumableModel cm
                    ON  cm.ConsumableModelId = i.ConsumableModelId
                    AND cm.IsRequestable     = 1
                    AND cm.IsActive          = 1
                WHERE REPLACE(LOWER(i.Category), ' ', '') LIKE @CategoryPattern
                  AND i.Active   = 1
                  AND cm.ModelNumber = @ModelKey";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(byModelSql, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryPattern", "%" + CanonicalCategoryNeedle(category) + "%");
                    cmd.Parameters.AddWithValue("@ModelKey", (modelKey ?? string.Empty).Trim());
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return (int)result;
                }
            }

            return GetPlaceholderItemIdForCategory(category);
        }

        /// <summary>
        /// Gets a placeholder ItemId for a given consumable Category, mirroring
        /// GetPlaceholderCartridgeItemId. Used when a ConsumableModel has no Item rows yet.
        /// </summary>
        private int GetPlaceholderItemIdForCategory(string category)
        {
            const string sql = @"
                SELECT TOP 1 ItemId
                FROM dbo.Item
                WHERE REPLACE(LOWER(Category), ' ', '') LIKE @CategoryPattern AND Active = 1
                ORDER BY ItemId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@CategoryPattern", "%" + CanonicalCategoryNeedle(category) + "%");
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        return (int)result;
                }
            }

            throw new InvalidOperationException($"No active '{category}' items found in the system.");
        }

        /// <summary>
        /// Creates a cartridge request from the portal.
        /// FIELD MAPPING:
        /// - Description: Stores fulfillment method (PICKUP/DELIVERY) + destination info + portal marker
        /// - Remarks: Stores cartridge condition (With Cartridge / Without Cartridge)
        /// - EmpId: Employee created from portal user's name and position
        /// - Status: Under Review (initial status, becomes "Submitted" when added to Set and QR/PDF generated)
        /// - EntryType: Determined by inventory logic
        /// </summary>
        public int CreateCartridgeRequest(CartridgeRequestDto portalRequest)
        {
            // Validate inputs
            if (portalRequest.ItemId <= 0)
                throw new ArgumentException("Invalid ItemId");
            if (string.IsNullOrWhiteSpace(portalRequest.EmployeeName))
                throw new ArgumentException("Employee name is required");
            if (string.IsNullOrWhiteSpace(portalRequest.EmployeePosition))
                throw new ArgumentException("Employee position is required");
            if (portalRequest.DestinationCompanyId <= 0)
                throw new ArgumentException("Invalid DestinationCompanyId");
            if (portalRequest.DestinationBranchId <= 0)
                throw new ArgumentException("Invalid DestinationBranchId");
            if (portalRequest.DestinationDepartmentId <= 0)
                throw new ArgumentException("Invalid DestinationDepartmentId");
            if (portalRequest.Quantity <= 0)
                throw new ArgumentException("Quantity must be greater than 0");

            // Create or get employee for this portal request
            int employeeId = CreatePortalEmployee(
                portalRequest.EmployeeName,
                portalRequest.EmployeePosition,
                portalRequest.DestinationCompanyId,
                portalRequest.DestinationBranchId,
                portalRequest.DestinationDepartmentId,
                portalRequest.CreatedByUserId
            );

            // Get destination names for description
            string destinationInfo = GetDestinationInfo(
                portalRequest.DestinationCompanyId,
                portalRequest.DestinationBranchId,
                portalRequest.DestinationDepartmentId
            );

            // Build Description field: includes fulfillment method + destination + portal source marker
            string description = $"[PORTAL] {portalRequest.FulfillmentMethod} → {destinationInfo}";
            if (!string.IsNullOrWhiteSpace(portalRequest.AdditionalDescription))
            {
                description += $" - {portalRequest.AdditionalDescription}";
            }

            // Build Remarks field: includes cartridge condition
            string remarks = portalRequest.CartridgeCondition;
            if (!string.IsNullOrWhiteSpace(portalRequest.AdditionalRemarks))
            {
                remarks += $" | {portalRequest.AdditionalRemarks}";
            }

            // Create standard RequestDto for existing repository method
            var requestDto = new Pages.RequestDto
            {
                DateRequested = portalRequest.DateRequested ?? DateTime.Now,
                Description = description,
                Remarks = remarks,
                Status = "Under Review", // Initial status - will become "Submitted" when added to Set and QR/PDF generated
                EntryType = "Negative", // Will be validated/adjusted by repository
                Quantity = portalRequest.Quantity,
                UnitPrice = 0, // Will be populated from Item table by repository
                DateCreated = DateTime.Now,
                CreatedByUserId = portalRequest.CreatedByUserId,
                ItemId = portalRequest.ItemId,
                EmpId = employeeId, // Portal employee created with user's name and position
                SubmissionSessionId = portalRequest.SubmissionSessionId // Phase 4: Multi-model grouping
            };

            // REUSE existing request repository logic
            // This handles all inventory allocation, validation, and request creation
            int newRequestId = _requestRepository.AddRequest(requestDto);

            return newRequestId;
        }

        #endregion

        #region Request Status Tracking (Read-Only for Portal)

        /// <summary>
        /// Get all requests created via the portal for a specific user.
        /// READ-ONLY view for status tracking.
        /// </summary>
        public List<PortalRequestStatusDto> GetPortalRequestsByUser(int userId)
        {
            var requests = new List<PortalRequestStatusDto>();

            const string sql = @"
                SELECT
                    r.ReqId,
                    r.DateRequested,
                    CASE
                        WHEN r.Status IN ('Under Review', 'Processing')
                             AND OBJECT_ID('dbo.UnfulfilledCartridgeExchange', 'U') IS NOT NULL
                             AND EXISTS (SELECT 1 FROM dbo.UnfulfilledCartridgeExchange uce WHERE uce.ReqId = r.ReqId)
                        THEN
                            CASE
                                WHEN (SELECT SUM(ISNULL(uce2.IssuedFullQty, 0))
                                      FROM dbo.UnfulfilledCartridgeExchange uce2
                                      WHERE uce2.ReqId = r.ReqId) = 0
                                THEN 'Unfulfilled'
                                ELSE 'Partially Fulfilled'
                            END
                        WHEN r.Status = 'Submitted' THEN 'Fulfilled'
                        ELSE r.Status
                    END AS Status,
                    r.Quantity,
                    r.Description,
                    r.Remarks,
                    r.DateCreated,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    e.Name AS DestinationEmployeeName,
                    b.Name AS DestinationBranch,
                    d.Name AS DestinationDepartment,
                    c.Name AS DestinationCompany,
                    ISNULL(crm.GoodEmptyQty, 0) AS GoodEmptyQty,
                    ISNULL(crm.DamagedEmptyQty, 0) AS DamagedEmptyQty,
                    crm.CartridgeModel AS CartridgeName,
                    e.Position AS DestinationEmployeePosition,
                    r.SubmissionSessionId,
                    s.SetCode,
                    recv.Name AS ReceivedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                INNER JOIN dbo.Branch b ON e.BranchId = b.BranchId
                INNER JOIN dbo.Department d ON e.DeptId = d.DeptId
                INNER JOIN dbo.Company c ON e.ComId = c.ComId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                LEFT JOIN dbo.[Set] s ON s.SetId = r.SetId
                LEFT JOIN dbo.Employee recv ON r.ReceivedById = recv.EmpId
                WHERE r.CreatedBy = @UserId
                  AND r.Description LIKE '[[]PORTAL]%' -- Filter portal requests only
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string description = reader.GetString(4);
                            string remarks = reader.IsDBNull(5) ? null : reader.GetString(5);

                            requests.Add(new PortalRequestStatusDto
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.IsDBNull(1) ? reader.GetDateTime(6) : reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                FullDescription = description,
                                FullRemarks = StripCartridgeCondition(remarks),
                                FulfillmentMethod = ExtractFulfillmentMethod(description),
                                GoodEmptyQty = reader.GetInt32(13),
                                DamagedEmptyQty = reader.GetInt32(14),
                                CartridgeName = reader.IsDBNull(15) ? null : reader.GetString(15),
                                DestinationEmployeePosition = reader.IsDBNull(16) ? null : reader.GetString(16),
                                SubmissionSessionId = reader.IsDBNull(17) ? (Guid?)null : reader.GetGuid(17),
                                SetCode = reader.IsDBNull(18) ? null : reader.GetString(18),
                                ReceivedByName = reader.IsDBNull(19) ? null : reader.GetString(19),
                                DateCreated = reader.GetDateTime(6),
                                ItemName = reader.GetString(7),
                                ItemModelNumber = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DestinationEmployeeName = reader.GetString(9),
                                DestinationBranch = reader.GetString(10),
                                DestinationDepartment = reader.GetString(11),
                                DestinationCompany = reader.GetString(12)
                            });
                        }
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Get a single request status by ReqId.
        /// </summary>
        public PortalRequestStatusDto GetRequestStatus(int reqId)
        {
            const string sql = @"
                SELECT
                    r.ReqId,
                    r.DateRequested,
                    r.Status,
                    r.Quantity,
                    r.Description,
                    r.Remarks,
                    r.DateCreated,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    e.Name AS DestinationEmployeeName,
                    b.Name AS DestinationBranch,
                    d.Name AS DestinationDepartment,
                    c.Name AS DestinationCompany,
                    recv.Name AS ReceivedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                INNER JOIN dbo.Branch b ON e.BranchId = b.BranchId
                INNER JOIN dbo.Department d ON e.DeptId = d.DeptId
                INNER JOIN dbo.Company c ON e.ComId = c.ComId
                LEFT JOIN dbo.Employee recv ON r.ReceivedById = recv.EmpId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ReqId", reqId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string description = reader.GetString(4);
                            string remarks = reader.IsDBNull(5) ? null : reader.GetString(5);

                            return new PortalRequestStatusDto
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.IsDBNull(1) ? reader.GetDateTime(6) : reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                FullDescription = description,
                                FullRemarks = StripCartridgeCondition(remarks),
                                FulfillmentMethod = ExtractFulfillmentMethod(description),
                                DateCreated = reader.GetDateTime(6),
                                ItemName = reader.GetString(7),
                                ItemModelNumber = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DestinationEmployeeName = reader.GetString(9),
                                DestinationBranch = reader.GetString(10),
                                DestinationDepartment = reader.GetString(11),
                                DestinationCompany = reader.GetString(12),
                                ReceivedByName = reader.IsDBNull(13) ? null : reader.GetString(13)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get all requests from the same multi-model submission session.
        /// Used to display related requests together.
        /// </summary>
        public List<PortalRequestStatusDto> GetRequestsBySubmissionSession(Guid submissionSessionId)
        {
            var requests = new List<PortalRequestStatusDto>();

            const string sql = @"
                SELECT
                    r.ReqId,
                    r.DateRequested,
                    r.Status,
                    r.Quantity,
                    r.Description,
                    r.Remarks,
                    r.DateCreated,
                    r.SubmissionSessionId,
                    i.Name AS ItemName,
                    i.ModelNumber,
                    e.Name AS DestinationEmployeeName,
                    b.Name AS DestinationBranch,
                    d.Name AS DestinationDepartment,
                    c.Name AS DestinationCompany,
                    u.Name AS CreatedByUserName,
                    recv.Name AS ReceivedByName
                FROM dbo.Request r
                INNER JOIN dbo.Item i ON r.ItemId = i.ItemId
                INNER JOIN dbo.Employee e ON r.EmpId = e.EmpId
                INNER JOIN dbo.Branch b ON e.BranchId = b.BranchId
                INNER JOIN dbo.Department d ON e.DeptId = d.DeptId
                INNER JOIN dbo.Company c ON e.ComId = c.ComId
                INNER JOIN dbo.[User] u ON r.CreatedBy = u.UserId
                LEFT JOIN dbo.Employee recv ON r.ReceivedById = recv.EmpId
                WHERE r.SubmissionSessionId = @SubmissionSessionId
                ORDER BY r.ReqId";

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@SubmissionSessionId", submissionSessionId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string description = reader.GetString(4);
                            string remarks = reader.IsDBNull(5) ? null : reader.GetString(5);

                            requests.Add(new PortalRequestStatusDto
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.IsDBNull(1) ? reader.GetDateTime(6) : reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                FullDescription = description,
                                FullRemarks = StripCartridgeCondition(remarks),
                                FulfillmentMethod = ExtractFulfillmentMethod(description),
                                DateCreated = reader.GetDateTime(6),
                                ItemName = reader.GetString(8),
                                ItemModelNumber = reader.IsDBNull(9) ? null : reader.GetString(9),
                                DestinationEmployeeName = reader.GetString(10),
                                DestinationBranch = reader.GetString(11),
                                DestinationDepartment = reader.GetString(12),
                                DestinationCompany = reader.GetString(13),
                                ReceivedByName = reader.IsDBNull(15) ? null : reader.GetString(15)
                            });
                        }
                    }
                }
            }

            return requests;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get destination information as a formatted string for the Description field.
        /// </summary>
        private string GetDestinationInfo(int companyId, int branchId, int departmentId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT
                            c.Name AS CompanyName,
                            b.Name AS BranchName,
                            d.Name AS DepartmentName
                        FROM dbo.Company c
                        CROSS JOIN dbo.Branch b
                        CROSS JOIN dbo.Department d
                        WHERE c.ComId = @CompanyId
                          AND b.BranchId = @BranchId
                          AND d.DeptId = @DepartmentId";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);
                        cmd.Parameters.AddWithValue("@BranchId", branchId);
                        cmd.Parameters.AddWithValue("@DepartmentId", departmentId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string company = reader.GetString(0);
                                string branch = reader.GetString(1);
                                string department = reader.GetString(2);

                                return $"{branch}, {department}";
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback if query fails
            }

            return $"Branch {branchId}, Dept {departmentId}";
        }

        private string ExtractFulfillmentMethod(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "UNKNOWN";

            if (description.Contains("PICKUP"))
                return "PICKUP";
            if (description.Contains("DELIVERY"))
                return "DELIVERY";

            return "UNKNOWN";
        }

        private string ExtractCartridgeCondition(string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                return "Not Specified";

            if (remarks.Contains("With Cartridge"))
                return "With Cartridge";
            if (remarks.Contains("Without Cartridge"))
                return "Without Cartridge";

            return "Not Specified";
        }

        /// <summary>
        /// Strips the legacy "With Cartridge" / "Without Cartridge" prefix from the Remarks field,
        /// returning only the user-entered portion (everything after the " | " separator), or null
        /// if the field contained only the condition prefix with no user text.
        /// </summary>
        private static string StripCartridgeCondition(string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;

            const string sep = " | ";
            foreach (var prefix in new[] { "With Cartridge", "Without Cartridge" })
            {
                if (remarks.StartsWith(prefix + sep, StringComparison.OrdinalIgnoreCase))
                    return remarks.Substring(prefix.Length + sep.Length);
                if (string.Equals(remarks.Trim(), prefix, StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            return remarks;
        }

        #endregion
    }

    #region DTOs for Portal

    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    /// <summary>
    /// DTO for cartridge items filtered from existing Item table.
    /// SerialNumber is the PRIMARY identifier - ModelNumber is descriptive only.
    /// </summary>
    public class CartridgeItemDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }

        /// <summary>
        /// Model number is for DISPLAY ONLY, not identification.
        /// Multiple cartridges can share the same model.
        /// </summary>
        public string ModelNumber { get; set; }

        /// <summary>
        /// SerialNumber is the UNIQUE identifier for each cartridge.
        /// All inbound/refill operations MUST use SerialNumber for selection.
        /// </summary>
        public string SerialNumber { get; set; }

        public string Category { get; set; }
        public string Description { get; set; }
        public decimal UnitPrice { get; set; }
        public int StockOnHand { get; set; }
        public bool Active { get; set; }

        /// <summary>
        /// Display format: SerialNumber (Model) - Name
        /// SerialNumber is shown FIRST because it's the unique identifier.
        /// Model is secondary, read-only information.
        /// </summary>
        public string DisplayName
        {
            get
            {
                // SerialNumber is PRIMARY - show it first
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(SerialNumber))
                    parts.Add($"SN: {SerialNumber}");

                if (!string.IsNullOrWhiteSpace(ModelNumber))
                    parts.Add($"({ModelNumber})");

                parts.Add(ItemName);

                return string.Join(" - ", parts);
            }
        }
    }

    /// <summary>
    /// DTO for submitting cartridge requests from portal.
    /// Maps to existing Request table fields.
    /// </summary>
    public class CartridgeRequestDto
    {
        // Cartridge Selection
        public int ItemId { get; set; }
        public int Quantity { get; set; }

        // Cartridge Condition → stored in Remarks
        public string CartridgeCondition { get; set; } // "With Cartridge" or "Without Cartridge"

        // Employee information (for portal users who are not in the Employee table yet)
        public string EmployeeName { get; set; }
        public string EmployeePosition { get; set; }

        // Destination (Logistics Target) - for tracking where items should be delivered
        public int DestinationCompanyId { get; set; }
        public int DestinationBranchId { get; set; }
        public int DestinationDepartmentId { get; set; }

        // Fulfillment Method → stored in Description
        public string FulfillmentMethod { get; set; } // "PICKUP" or "DELIVERY"

        // Optional additional info
        public string AdditionalDescription { get; set; }
        public string AdditionalRemarks { get; set; }

        // Multi-model submission tracking (Phase 4)
        // Links related requests from same submission session
        public Guid? SubmissionSessionId { get; set; }

        // Audit fields
        public int CreatedByUserId { get; set; }
        public DateTime? DateRequested { get; set; }
    }

    /// <summary>
    /// DTO for displaying request status in portal (read-only).
    /// </summary>
    public class PortalRequestStatusDto
    {
        public int ReqId { get; set; }
        public DateTime DateRequested { get; set; }
        public string Status { get; set; } // SUBMITTED, RECEIVED, REPLACED, COMPLETED
        public int Quantity { get; set; }
        public DateTime DateCreated { get; set; }

        // Parsed fields
        public string FulfillmentMethod { get; set; } // PICKUP / DELIVERY
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }

        // Raw fields (for reference)
        public string FullDescription { get; set; }
        public string FullRemarks { get; set; }

        // Item details
        public string CartridgeName { get; set; } // From CartridgeRequestModel.CartridgeModel (actual model name)
        public string ItemName { get; set; }
        public string ItemModelNumber { get; set; }

        // Destination details
        public string DestinationEmployeeName { get; set; }
        public string DestinationEmployeePosition { get; set; }
        public string DestinationBranch { get; set; }
        public string DestinationDepartment { get; set; }
        public string DestinationCompany { get; set; }

        // Submission grouping
        public Guid? SubmissionSessionId { get; set; }
        public string SetCode { get; set; } // From dbo.Set (populated after fulfillment)

        public string ReceivedByName { get; set; }
    }

    /// <summary>
    /// Simple DTOs for organizational structure.
    /// </summary>
    public class CompanyDto
    {
        public int ComId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public override string ToString() => Name ?? string.Empty;
    }

    public class BranchDto
    {
        public int BranchId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ComId { get; set; }
        public int? DeptId { get; set; }
        public override string ToString() => Name ?? string.Empty;
    }

    public class DepartmentDto
    {
        public int DeptId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ComId { get; set; }
        public override string ToString() => Name ?? string.Empty;
    }

    #endregion
}
