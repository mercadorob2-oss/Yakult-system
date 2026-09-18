using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Models.ViewModels;
using Inventory.RequestPortal.Repositories;

namespace Inventory.RequestPortal.Services
{
    // CARTRIDGE INBOUND REQUEST FLOW:
    //
    // Employees return cartridges by MODEL, not by serial number.
    // The typed model number in the "Cartridge Model" textbox is the PRIMARY input.
    // Dropdown selection is only a guide to help jog memory.
    //
    // Portal requests are INTENT-ONLY:
    // - No inventory allocation at request time
    // - No stock decrement at request time
    // - IT Fulfillment extracts model from [MODEL:XXX] tag later
    //
    // Request Description format:
    // [PORTAL] [MODEL:XXX] PICKUP → Branch, Department

    /// <summary>
    /// Service layer for Requester Portal functionality.
    /// REUSES existing request infrastructure - no new tables.
    /// Maps portal-specific fields to existing database columns.
    /// </summary>
    public class RequesterPortalService : IRequesterPortalService
    {
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly IRequestRepository _requestRepository;
        private readonly ICartridgeAuthorizationWebRepository _authorizationRepository;

        public RequesterPortalService(
            IConnectionStringProvider connectionStringProvider,
            IRequestRepository requestRepository,
            ICartridgeAuthorizationWebRepository authorizationRepository)
        {
            _connectionStringProvider = connectionStringProvider;
            _requestRepository = requestRepository;
            _authorizationRepository = authorizationRepository;
        }

        #region Cartridge Item Queries

        /// <summary>
        /// Retrieves all active, requestable cartridge models from dbo.CartridgeModel.
        /// Based on the model registry — not filtered by Item stock availability.
        /// Excludes archived and inactive models.
        /// </summary>
        public List<CartridgeModelAvailabilityViewModel> GetCartridgeModelsWithAvailability()
        {
            var models = new List<CartridgeModelAvailabilityViewModel>();

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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var modelNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        models.Add(new CartridgeModelAvailabilityViewModel
                        {
                            ModelKey          = reader.GetString(0),
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
        /// Retrieves DISTINCT cartridge MODELS grouped by ModelNumber and Name.
        /// Stock count is aggregated per model.
        ///
        /// This is for the dropdown guide only - the typed model is the PRIMARY input.
        /// Applies the same availability rules as GetCartridgeModelsWithAvailability:
        ///   CartridgeModel.IsRequestable = 1, Item.Active = 1, net StockOnHand > 0.
        /// ArchiveStatus is intentionally excluded — see GetCartridgeModelsWithAvailability.
        /// </summary>
        public List<CartridgeItemViewModel> GetCartridgeItems()
        {
            var items = new List<CartridgeItemViewModel>();

            // Effective stock = StockOnHand minus quantities already allocated to active requests.
            const string sql = @"
                ;WITH EffectiveStock AS (
                    SELECT
                        i.ItemId,
                        i.Name,
                        i.Description,
                        i.ModelNumber,
                        i.Amount,
                        CASE
                            WHEN i.SerialNumber IS NULL THEN
                                i.StockOnHand - ISNULL((
                                    SELECT SUM(r.Quantity)
                                    FROM dbo.Request r
                                    WHERE r.ItemId = i.ItemId
                                      AND r.Active = 1
                                      AND ISNULL(r.EntryType, 'Negative') != 'None'
                                ), 0)
                            ELSE
                                i.StockOnHand
                        END AS AvailableStock
                    FROM dbo.Item i
                    INNER JOIN dbo.CartridgeModel cm
                        ON  cm.CartridgeModelId = i.CartridgeModelId
                        AND cm.IsRequestable    = 1
                        AND cm.IsActive         = 1
                    LEFT JOIN dbo.ArchiveStatus archCM
                        ON  archCM.EntityType = 'CartridgeModel'
                        AND archCM.EntityId   = cm.CartridgeModelId
                        AND archCM.IsArchived  = 1
                    WHERE i.Category              = 'Cartridge'
                      AND i.Active                = 1
                      AND ISNULL(i.IsTrackedAsset, 0) = 0
                      AND archCM.EntityId IS NULL
                )
                SELECT
                    MIN(es.ItemId) AS ItemId,
                    MIN(es.Name) AS ItemName,
                    ISNULL(es.ModelNumber, MIN(es.Name)) AS ModelNumber,
                    'N/A' AS SerialNumber,
                    'Cartridge' AS Category,
                    MIN(es.Description) AS Description,
                    MIN(es.Amount) AS UnitPrice,
                    SUM(CASE WHEN es.AvailableStock > 0 THEN es.AvailableStock ELSE 0 END) AS AggregatedStock,
                    1 AS Active
                FROM EffectiveStock es
                WHERE es.AvailableStock > 0
                GROUP BY ISNULL(es.ModelNumber, es.Name)
                HAVING SUM(CASE WHEN es.AvailableStock > 0 THEN es.AvailableStock ELSE 0 END) > 0
                ORDER BY ModelNumber";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new CartridgeItemViewModel
                        {
                            ItemId = reader.GetInt32(0),
                            ItemName = reader.GetString(1),
                            ModelNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                            SerialNumber = reader.GetString(3), // Always "N/A" for grouped models
                            Category = reader.GetString(4),
                            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
                            UnitPrice = reader.GetDecimal(6),
                            StockOnHand = reader.GetInt32(7), // Aggregated stock per model
                            Active = reader.GetBoolean(8)
                        });
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Retrieves all active, requestable consumable models for a given category
        /// ("Ink", "Printhead", or "Toner Cartridge") from dbo.ConsumableModel.
        /// Mirrors GetCartridgeModelsWithAvailability's shape/behavior for the Model +
        /// Quantity dropdown pattern, but reads the ConsumableModel registry instead of
        /// CartridgeModel.
        /// </summary>
        /// <summary>
        /// Category text drifts across the system — dbo.ConsumableModel.Category stores
        /// "Ink"/"Print Head"/"Toner" while the portal's dropdown (and dbo.Item.Category from
        /// older imports) uses "Ink"/"Printhead"/"Toner Cartridge" — so an exact string match
        /// silently returns nothing for Printhead/Toner. Collapse either spelling down to a
        /// stable lowercase, no-space substring needle ("ink"/"toner"/"printhead") and match
        /// with LIKE '%needle%' instead, mirroring RequestFulfillmentRepository's category filter.
        /// </summary>
        private static string CanonicalCategoryNeedle(string category)
        {
            var normalized = (category ?? string.Empty).Replace(" ", "").ToLowerInvariant();
            if (normalized.Contains("ink")) return "ink";
            if (normalized.Contains("toner")) return "toner";
            // Covers "Printhead", "Print Head", and "Printerhead" (the portal's display label).
            if (normalized.Contains("print") && normalized.Contains("head")) return "printhead";
            return normalized;
        }

        public List<CartridgeModelAvailabilityViewModel> GetConsumableModelsWithAvailability(string category)
        {
            var models = new List<CartridgeModelAvailabilityViewModel>();

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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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
                            models.Add(new CartridgeModelAvailabilityViewModel
                            {
                                ModelKey          = reader.GetString(0),
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

        #region Destination Queries (Company/Branch/Department)

        /// <summary>
        /// Get all active companies.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        public List<CompanyViewModel> GetActiveCompanies()
        {
            var companies = new List<CompanyViewModel>();

            const string sql = @"
                SELECT ComId, Name, Description
                FROM dbo.Company
                WHERE Active = 1
                ORDER BY Name";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyViewModel
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
        /// Get ALL active branches (not filtered by company).
        /// COPIED FROM: Yakult.Inventory.App/Pages/RequesterPortalForm.cs (LoadAllBranches)
        /// </summary>
        public List<BranchViewModel> GetAllBranches()
        {
            var branches = new List<BranchViewModel>();

            const string sql = @"
                SELECT
                    b.BranchId,
                    b.Name,
                    b.Description,
                    ISNULL(bdc_co.CompanyID,   0)    AS ComId,
                    bdc_dept.DepartmentID            AS DeptId
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
                ORDER BY b.Name";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(0) || reader.IsDBNull(1))
                            continue;

                        branches.Add(new BranchViewModel
                        {
                            BranchId    = reader.GetInt32(0),
                            Name        = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ComId       = reader.GetInt32(3),
                            DeptId      = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                        });
                    }
                }
            }

            return branches;
        }

        /// <summary>
        /// Get ALL active departments (not filtered by company).
        /// COPIED FROM: Yakult.Inventory.App/Pages/RequesterPortalForm.cs (LoadAllDepartments)
        /// </summary>
        public List<DepartmentViewModel> GetAllDepartments()
        {
            var departments = new List<DepartmentViewModel>();

            const string sql = @"
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
                ORDER BY d.Name";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(0) || reader.IsDBNull(1))
                            continue;

                        departments.Add(new DepartmentViewModel
                        {
                            DeptId      = reader.GetInt32(0),
                            Name        = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ComId       = reader.GetInt32(3)
                        });
                    }
                }
            }

            return departments;
        }

        /// <summary>
        /// Get branches for a specific company.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        public List<BranchViewModel> GetBranchesByCompany(int companyId)
        {
            var branches = new List<BranchViewModel>();

            const string sql = @"
                SELECT
                    b.BranchId,
                    b.Name,
                    b.Description,
                    ISNULL(bdc_co.CompanyID,   0) AS ComId,
                    bdc_dept.DepartmentID         AS DeptId
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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            branches.Add(new BranchViewModel
                            {
                                BranchId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ComId = reader.GetInt32(3),
                                DeptId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                            });
                        }
                    }
                }
            }

            return branches;
        }

        /// <summary>
        /// Get departments for a specific company.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        public List<DepartmentViewModel> GetDepartmentsByCompany(int companyId)
        {
            var departments = new List<DepartmentViewModel>();

            const string sql = @"
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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ComId", companyId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departments.Add(new DepartmentViewModel
                            {
                                DeptId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                ComId = reader.GetInt32(3)
                            });
                        }
                    }
                }
            }

            return departments;
        }

        /// <summary>
        /// Get all active employees for the "Received By" dropdown (PICKUP requests).
        /// </summary>
        public List<EmployeeViewModel> GetActiveEmployees()
        {
            var employees = new List<EmployeeViewModel>();

            const string sql = @"
                SELECT e.EmpId, e.Name, e.EmployeeNumber, e.Position,
                       e.ComId, c.Name AS CompanyName,
                       e.BranchId, b.Name AS BranchName,
                       e.DeptId, d.Name AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Company    c ON e.ComId    = c.ComId
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active = 1
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        employees.Add(new EmployeeViewModel
                        {
                            EmpId          = reader.GetInt32(0),
                            Name           = reader.GetString(1),
                            EmployeeNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Position       = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            ComId          = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            CompanyName    = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            BranchId       = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            BranchName     = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            DeptId         = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                            DepartmentName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                        });
                    }
                }
            }

            return employees;
        }

        public List<EmployeeViewModel> GetEmployeesByAccount(int companyId, int departmentId, int branchId)
        {
            var employees = new List<EmployeeViewModel>();

            const string sql = @"
                SELECT e.EmpId, e.Name, e.EmployeeNumber, e.Position,
                       e.ComId, c.Name AS CompanyName,
                       e.BranchId, b.Name AS BranchName,
                       e.DeptId, d.Name AS DepartmentName
                FROM dbo.Employee e
                LEFT JOIN dbo.Company    c ON e.ComId    = c.ComId
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE e.Active = 1
                  AND e.ComId    = @ComId
                  AND e.DeptId   = @DeptId
                  AND e.BranchId = @BranchId
                ORDER BY e.Name";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@ComId",    companyId);
                    cmd.Parameters.AddWithValue("@DeptId",   departmentId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employees.Add(new EmployeeViewModel
                            {
                                EmpId          = reader.GetInt32(0),
                                Name           = reader.GetString(1),
                                EmployeeNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Position       = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                ComId          = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                CompanyName    = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                BranchId       = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                BranchName     = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                DeptId         = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                DepartmentName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                            });
                        }
                    }
                }
            }

            return employees;
        }

        #endregion

        #region Request Submission

        /// <summary>
        /// Creates cartridge request using Model + Quantity selection.
        ///
        /// IMPORTANT: The user-typed model number is the PRIMARY input.
        /// Dropdown selection is only a guide - never the authoritative value.
        ///
        /// PORTAL REQUESTS ARE INTENT-ONLY:
        /// - No inventory allocation at request time
        /// - No stock decrement at request time
        /// - IT Fulfillment extracts model from [MODEL:XXX] tag later
        ///
        /// Description format: [PORTAL] [MODEL:XXX] PICKUP → Branch, Department
        ///
        /// HARD REQUIREMENT: empId must be provided from session.
        /// Portal is READ-ONLY with respect to Employee table.
        /// </summary>
        public List<int> CreateCartridgeRequestByModel(
            CartridgeRequestViewModel request,
            List<CartridgeRequestItemViewModel> requestItems,
            int empId,
            out Guid submissionSessionId)
            => CreateCartridgeRequestByModelCore(request, requestItems, empId, "[PORTAL]", "Under Review", allowDeptLevel: false, out submissionSessionId);

        public List<int> CreateAssistedCartridgeRequestByModel(
            CartridgeRequestViewModel request,
            List<CartridgeRequestItemViewModel> requestItems,
            int? targetEmpId,
            out Guid submissionSessionId)
            => CreateCartridgeRequestByModelCore(request, requestItems, targetEmpId, "[PORTAL_ASSISTED]", "Awaiting Authorization", allowDeptLevel: true, out submissionSessionId);

        private List<int> CreateCartridgeRequestByModelCore(
            CartridgeRequestViewModel request,
            List<CartridgeRequestItemViewModel> requestItems,
            int? empId,
            string descriptionTag,
            string multiModelStatus,
            bool allowDeptLevel,
            out Guid submissionSessionId)
        {
            // Department-level requests (IT Assisted only) have no target employee — EmpId
            // stays NULL and Company/Branch/Department are stored directly on the Request row
            // instead. Every other path (self-service, or assisted-with-employee) still requires
            // a real employee — Portal CANNOT create employees, they are predefined by admins.
            bool isDeptLevel = allowDeptLevel && (empId == null || empId <= 0);
            if (!isDeptLevel && (empId == null || empId <= 0))
                throw new InvalidOperationException("Employee is not linked to this user. Please contact an administrator.");

            var normalizedItems = (requestItems ?? new List<CartridgeRequestItemViewModel>())
                .Where(x => x != null)
                .ToList();

            bool hasItems = normalizedItems.Any(x => !string.IsNullOrWhiteSpace(x.CartridgeModel));

            // Validate inputs
            if (!hasItems)
            {
                if (string.IsNullOrWhiteSpace(request.TypedModelNumber) && string.IsNullOrWhiteSpace(request.ModelKey))
                    throw new ArgumentException("Please enter or select a cartridge model.");
            }
            // Department-level requests have no employee and all destination fields are
            // optional ("Fill in only what you have"), so none of these apply to them.
            if (!isDeptLevel)
            {
                if (string.IsNullOrWhiteSpace(request.EmployeeName))
                    throw new ArgumentException("Employee name is required");
                if (string.IsNullOrWhiteSpace(request.EmployeePosition))
                    throw new ArgumentException("Employee position is required");
                if (request.DestinationCompanyId <= 0)
                    throw new ArgumentException("Invalid DestinationCompanyId");
                if (request.DestinationBranchId <= 0)
                    throw new ArgumentException("Invalid DestinationBranchId");
                if (request.DestinationDepartmentId <= 0)
                    throw new ArgumentException("Invalid DestinationDepartmentId");
            }
            if (hasItems)
            {
                foreach (var item in normalizedItems)
                {
                    if (string.IsNullOrWhiteSpace(item.ModelKey))
                        throw new ArgumentException("Please select a model from the list.");
                    if (item.Quantity <= 0)
                        throw new ArgumentException("Quantity must be greater than 0");
                    // Good/Damaged empty-cartridge tracking only applies to the Cartridge category.
                    bool isCartridgeItem = string.IsNullOrWhiteSpace(item.Category) || item.Category == "Cartridge";
                    bool returningEmpties = item.GoodEmptyQty > 0 || item.DamagedEmptyQty > 0;
                    if (isCartridgeItem && returningEmpties && item.GoodEmptyQty + item.DamagedEmptyQty != item.Quantity)
                        throw new ArgumentException($"Good + Damaged empty qty ({item.GoodEmptyQty + item.DamagedEmptyQty}) must equal the requested quantity ({item.Quantity}) for '{item.CartridgeModel}'.");
                }
            }
            else
            {
                if (request.Quantity <= 0)
                    throw new ArgumentException("Quantity must be greater than 0");
            }

            // Get destination names for description
            string destinationInfo = GetDestinationInfo(
                request.DestinationCompanyId,
                request.DestinationBranchId,
                request.DestinationDepartmentId
            );

            // Generate one SubmissionSessionId per submission (shared by all models in this submit action)
            submissionSessionId = Guid.NewGuid();

            if (!hasItems)
            {
                // Legacy single-model fallback.
                // Model is stored in description as [MODEL:xxx] so GetPendingCartridgeRequests can recover it.
                string typedModel = !string.IsNullOrWhiteSpace(request.TypedModelNumber)
                    ? request.TypedModelNumber
                    : request.ModelKey ?? string.Empty;
                string description = !string.IsNullOrWhiteSpace(typedModel)
                    ? $"{descriptionTag} [MODEL:{typedModel}] {request.DistributionMethod} → {destinationInfo}"
                    : $"{descriptionTag} {request.DistributionMethod} → {destinationInfo}";

                string conditionPart = string.IsNullOrWhiteSpace(request.CartridgeCondition)
                    ? "With Cartridge"
                    : request.CartridgeCondition;
                string remarks = string.IsNullOrWhiteSpace(request.AdditionalRemarks)
                    ? conditionPart
                    : $"{conditionPart} | {request.AdditionalRemarks}";

                // PORTAL REQUESTS ARE INTENT-ONLY
                // Use placeholder ItemId - DO NOT resolve to specific inventory
                // This prevents tying the request to a single Item row
                // Fulfillment will resolve the actual items later
                int itemId = GetPlaceholderCartridgeItemId();

                Console.WriteLine($"[PORTAL REQUEST] Using placeholder ItemId (not tied to specific inventory)");
                Console.WriteLine($"  ModelKey: {request.ModelKey}");

                // Create single request for the quantity (INTENT-ONLY, no inventory allocation)
                var requestDto = new RequestDto
                {
                    DateRequested = request.DateRequested ?? DateTime.Now,
                    Description = description,
                    Remarks = remarks,
                    Status = "Awaiting Authorization",
                    EntryType = "None", // Portal requests do NOT affect inventory
                    Quantity = request.Quantity,
                    UnitPrice = 0,
                    DateCreated = DateTime.Now,
                    CreatedByUserId = request.CreatedByUserId,
                    ItemId = itemId,
                    EmpId = empId, // Use session EmpId - NEVER create or resolve. NULL for dept-level.
                    ComId    = request.DestinationCompanyId    > 0 ? request.DestinationCompanyId    : (int?)null,
                    BranchId = request.DestinationBranchId     > 0 ? request.DestinationBranchId     : (int?)null,
                    DeptId   = request.DestinationDepartmentId > 0 ? request.DestinationDepartmentId : (int?)null,
                    EmployeeName = request.EmployeeName,
                    SubmissionSessionId = submissionSessionId, // Track submission session
                    ReceivedById = request.DistributionMethod == "PICKUP" ? request.ReceivedById : null,
                    // Legacy single-model fallback is always cartridge-only (no Category concept here).
                    WorkflowType = "CartridgeManagement"
                };

                // MANDATORY DEBUG: Verify Item 283 state before submission
                if (itemId == 283)
                {
                    Console.WriteLine($"[CRITICAL DEBUG] About to submit request for ItemId=283");
                    Console.WriteLine($"  Description: {requestDto.Description}");
                    Console.WriteLine($"  EntryType: {requestDto.EntryType}");
                    Console.WriteLine($"  Quantity: {requestDto.Quantity}");
                }

                int newRequestId = _requestRepository.AddRequest(requestDto);

                // MANDATORY DEBUG: Verify Item 283 state after submission
                if (itemId == 283)
                {
                    Console.WriteLine($"[CRITICAL DEBUG] Completed request submission for ItemId=283, ReqId={newRequestId}");
                }

                // Persist the selected model directly into dbo.CartridgeRequestModel.
                // This is the authoritative source read by GetPendingCartridgeRequests;
                // description-tag parsing is only a secondary fallback.
                if (!string.IsNullOrWhiteSpace(typedModel))
                    InsertCartridgeRequestModel(newRequestId, typedModel, request.Quantity, remarks, request.GoodEmptyQty, request.DamagedEmptyQty);

                return new List<int> { newRequestId };
            }

            // MULTI-MODEL BEHAVIOR (matches Inventory System pattern):
            // Create ONE dbo.Request row PER MODEL.
            // All rows from this submission share the SAME SubmissionSessionId.
            // Each row gets its own [MODEL:xxx] tag so GetPendingCartridgeRequests can recover the typed model.

            var createdRequestIds = new List<int>();

            // Composition of the WHOLE submission, computed once — this is the single point
            // that decides Card 1 (Cartridge Management) vs Card 2 (Request & Set Management)
            // routing. A cartridge line inside a MIXED submission must NOT be pooled/placeholder
            // or recorded in CartridgeRequestModel (what Cartridge Management's pending queue
            // reads from) — the entire submission routes through the general Request flow
            // instead, exactly like a pure Ink/Printhead/Toner request.
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

                // PORTAL REQUESTS ARE INTENT-ONLY
                int itemId;
                if (isCartridgeItem && allCartridge)
                    // Cartridge-only submission: pooled placeholder — DO NOT resolve to specific
                    // inventory. Feeds Cartridge Management's queue.
                    itemId = GetPlaceholderCartridgeItemId();
                else if (isCartridgeItem)
                    // Cartridge item inside a MIXED submission: resolve a real Item, same as
                    // Ink/Printhead/Toner, so it appears only in the general Request flow.
                    itemId = ResolveCartridgeItemId(item.ModelKey);
                else
                    // Ink/Printhead/Toner Cartridge: resolve to a real Item under the selected
                    // ConsumableModel, since those categories aren't pooled.
                    itemId = ResolveConsumableItemId(item.Category, item.ModelKey);

                Console.WriteLine($"[PORTAL REQUEST - MULTI] Category={item.Category}, ItemId={itemId}");
                Console.WriteLine($"  ModelKey: {item.ModelKey}");

                string conditionPart = isCartridgeItem
                    ? (string.IsNullOrWhiteSpace(request.CartridgeCondition) ? "With Cartridge" : request.CartridgeCondition)
                    : item.Category;
                string itemRemarks = string.IsNullOrWhiteSpace(request.AdditionalRemarks)
                    ? conditionPart
                    : $"{conditionPart} | {request.AdditionalRemarks}";

                // Embed [MODEL:xxx] per item so downstream fulfillment can read the typed model.
                string itemDescription = $"{descriptionTag} [MODEL:{item.CartridgeModel}] {request.DistributionMethod} → {destinationInfo}";

                var requestDto = new RequestDto
                {
                    DateRequested = request.DateRequested ?? DateTime.Now,
                    Description = itemDescription,
                    Remarks = itemRemarks,
                    Status = multiModelStatus,
                    EntryType = "None",
                    Quantity = item.Quantity,
                    UnitPrice = 0,
                    DateCreated = DateTime.Now,
                    CreatedByUserId = request.CreatedByUserId,
                    ItemId = itemId,
                    EmpId = empId, // NULL for dept-level requests.
                    ComId    = request.DestinationCompanyId    > 0 ? request.DestinationCompanyId    : (int?)null,
                    BranchId = request.DestinationBranchId     > 0 ? request.DestinationBranchId     : (int?)null,
                    DeptId   = request.DestinationDepartmentId > 0 ? request.DestinationDepartmentId : (int?)null,
                    EmployeeName = request.EmployeeName,
                    SubmissionSessionId = submissionSessionId, // Same GUID for all models in this submission
                    ReceivedById = request.DistributionMethod == "PICKUP" ? request.ReceivedById : null,
                    WorkflowType = workflowType
                };

                int newReqId = _requestRepository.AddRequest(requestDto);
                createdRequestIds.Add(newReqId);

                // Only persist into dbo.CartridgeRequestModel — the table Cartridge Management's
                // pending queue reads from — when the whole submission is cartridge-only. A
                // cartridge line inside a mixed submission has a real ItemId (like Ink/Printhead/
                // Toner) and no side-table row, so it never appears in that queue.
                if (isCartridgeItem && allCartridge)
                    InsertCartridgeRequestModel(newReqId, item.CartridgeModel, item.Quantity, itemRemarks, item.GoodEmptyQty, item.DamagedEmptyQty);
            }

            return createdRequestIds;
        }

        /// <summary>
        /// Inserts a row into dbo.CartridgeRequestModel to permanently record the model
        /// selected by the user at request time. This is the direct, reliable path that
        /// GetPendingCartridgeRequests uses to populate TypedModelNumber — no description
        /// tag parsing needed.
        /// </summary>
        private void InsertCartridgeRequestModel(int reqId, string cartridgeModel, int requestedQty, string? remarks, int goodEmptyQty = 0, int damagedEmptyQty = 0)
        {
            if (string.IsNullOrWhiteSpace(cartridgeModel))
                return;

            // Guard against the table not existing (migration not yet run on this environment).
            const string sql = @"
                IF OBJECT_ID('dbo.CartridgeRequestModel', 'U') IS NOT NULL
                BEGIN
                    INSERT INTO dbo.CartridgeRequestModel
                        (ReqId, CartridgeModel, RequestedQty, GoodEmptyQty, DamagedEmptyQty, Status, Remarks)
                    VALUES
                        (@ReqId, @CartridgeModel, @RequestedQty, @GoodEmptyQty, @DamagedEmptyQty, 'Pending', @Remarks)
                END";

            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@ReqId", reqId);
            cmd.Parameters.AddWithValue("@CartridgeModel", cartridgeModel.Trim());
            cmd.Parameters.AddWithValue("@RequestedQty", requestedQty);
            bool hasEmpties = goodEmptyQty > 0 || damagedEmptyQty > 0;
            cmd.Parameters.AddWithValue("@GoodEmptyQty",    hasEmpties ? (object)goodEmptyQty    : DBNull.Value);
            cmd.Parameters.AddWithValue("@DamagedEmptyQty", hasEmpties ? (object)damagedEmptyQty : DBNull.Value);
            cmd.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        private int ResolveCartridgeItemIdFromModelKey(string modelKey)
        {
            if (string.IsNullOrWhiteSpace(modelKey))
                throw new ArgumentException("ModelKey is required.");

            // Mirrors the availability rules in GetCartridgeModelsWithAvailability.
            // IsRequestable = 1 is the gate; ArchiveStatus is excluded for the same reasons.
            const string sql = @"
                SELECT MIN(sub.ItemId)
                FROM (
                    SELECT
                        i.ItemId,
                        i.ModelNumber,
                        i.Name,
                        CASE
                            WHEN i.SerialNumber IS NULL THEN
                                i.StockOnHand - ISNULL((
                                    SELECT SUM(r.Quantity)
                                    FROM dbo.Request r
                                    WHERE r.ItemId = i.ItemId
                                      AND r.Active = 1
                                      AND ISNULL(r.EntryType, 'Negative') != 'None'
                                ), 0)
                            ELSE
                                i.StockOnHand
                        END AS AvailableStock
                    FROM dbo.Item i
                    INNER JOIN dbo.CartridgeModel cm
                        ON  cm.CartridgeModelId = i.CartridgeModelId
                        AND cm.IsRequestable    = 1
                    WHERE i.Category              = 'Cartridge'
                      AND i.Active                = 1
                      AND ISNULL(i.IsTrackedAsset, 0) = 0
                ) sub
                WHERE sub.AvailableStock > 0
                  AND ISNULL(NULLIF(LTRIM(RTRIM(sub.ModelNumber)), ''), sub.Name) = @ModelKey";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ModelKey", modelKey.Trim());
                con.Open();
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return (int)result;
            }

            throw new InvalidOperationException($"No active cartridge items found for model key '{modelKey}'.");
        }

        /// <summary>
        /// Gets a placeholder cartridge ItemId for portal requests.
        /// Since portal requests don't allocate inventory, we just need any valid cartridge reference.
        /// </summary>
        private int GetPlaceholderCartridgeItemId()
        {
            const string sql = @"
                SELECT TOP 1 ItemId
                FROM dbo.Item
                WHERE Category = 'Cartridge' AND Active = 1
                ORDER BY ItemId";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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
        ///
        /// The user-typed value in "Cartridge Model" textbox is the PRIMARY input.
        /// Stores the typed model number in description format:
        /// [PORTAL] [MODEL:XXX] PICKUP → Branch, Department
        ///
        /// PORTAL REQUESTS ARE INTENT-ONLY:
        /// - No inventory allocation at request time
        /// - No stock decrement at request time
        ///
        /// HARD REQUIREMENT: empId must be provided from session.
        /// Portal is READ-ONLY with respect to Employee table.
        /// </summary>
        public int CreateCartridgeRequest(
            CartridgeRequestViewModel portalRequest,
            int empId)
        {
            // HARD REQUIREMENT: Employee ID must be provided from session
            // Portal CANNOT create employees - they are predefined by admins
            if (empId <= 0)
                throw new InvalidOperationException("Employee is not linked to this user. Please contact an administrator.");

            // Validation
            if (string.IsNullOrWhiteSpace(portalRequest.TypedModelNumber) && portalRequest.ItemId <= 0)
                throw new ArgumentException("Please enter a cartridge model or select from the dropdown.");
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

            // PRIMARY INPUT: The typed model number takes precedence
            string modelNumber = !string.IsNullOrWhiteSpace(portalRequest.TypedModelNumber)
                ? portalRequest.TypedModelNumber.Trim()
                : portalRequest.ModelNumber ?? "UNKNOWN";

            // Get destination names for description
            string destinationInfo = GetDestinationInfo(
                portalRequest.DestinationCompanyId,
                portalRequest.DestinationBranchId,
                portalRequest.DestinationDepartmentId
            );

            // Build Description field with exact format:
            // [PORTAL] [MODEL:XXX] PICKUP → Branch, Department
            string description = $"[PORTAL] [MODEL:{modelNumber}] {portalRequest.DistributionMethod} → {destinationInfo}";

            // Build Remarks field
            string remarks = portalRequest.AdditionalRemarks ?? string.Empty;

            // Get placeholder ItemId if none provided (portal requests don't allocate inventory)
            int itemId = portalRequest.ItemId > 0
                ? portalRequest.ItemId
                : GetPlaceholderCartridgeItemId();

            // Generate SubmissionSessionId for this submission
            Guid submissionSessionId = Guid.NewGuid();

            // Create standard RequestDto for repository method
            var requestDto = new RequestDto
            {
                DateRequested = portalRequest.DateRequested ?? DateTime.Now,
                Description = description,
                Remarks = remarks,
                Status = "Under Review",
                EntryType = "None", // Portal requests do NOT affect inventory
                Quantity = portalRequest.Quantity,
                UnitPrice = 0,
                DateCreated = DateTime.Now,
                CreatedByUserId = portalRequest.CreatedByUserId,
                ItemId = itemId,
                EmpId = empId, // Use session EmpId - NEVER create or resolve
                EmployeeName = portalRequest.EmployeeName,
                SubmissionSessionId = submissionSessionId // Track submission session
            };

            // Use repository to create request (will detect [PORTAL] tag and skip inventory)
            int newRequestId = _requestRepository.AddRequest(requestDto);

            return newRequestId;
        }

        #endregion

        #region IT Manual Authorization

        public async Task<List<ITApproverViewModel>> GetApproversByScope(int comId, int branchId, int deptId)
        {
            return await _authorizationRepository.GetApproversByScopeAsync(comId, branchId, deptId);
        }

        #endregion

        #region Request Status Tracking (Read-Only for Portal)

        /// <summary>
        /// Get all requests created via the portal for a specific user.
        /// READ-ONLY view for status tracking.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        public List<PortalRequestStatusViewModel> GetPortalRequestsByUser(int userId)
        {
            var requests = new List<PortalRequestStatusViewModel>();

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
                    -- Department-level requests (EmpId NULL) carry ComId/BranchId/DeptId directly
                    -- on the Request row instead of an Employee — fall back to those so these
                    -- rows show up here instead of being silently dropped by an inner join.
                    e.Name AS DestinationEmployeeName,
                    ISNULL(b.Name, rb.Name) AS DestinationBranch,
                    ISNULL(d.Name, rd.Name) AS DestinationDepartment,
                    ISNULL(c.Name, rc.Name) AS DestinationCompany,
                    ISNULL(crm.GoodEmptyQty, 0) AS GoodEmptyQty,
                    ISNULL(crm.DamagedEmptyQty, 0) AS DamagedEmptyQty,
                    crm.CartridgeModel AS CartridgeName,
                    r.SubmissionSessionId,
                    s.SetCode
                FROM dbo.Request r
                INNER JOIN dbo.Item i         ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e      ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Branch b        ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d    ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Company c       ON e.ComId = c.ComId
                LEFT JOIN dbo.Branch rb       ON r.BranchId = rb.BranchId
                LEFT JOIN dbo.Department rd   ON r.DeptId = rd.DeptId
                LEFT JOIN dbo.Company rc      ON r.ComId = rc.ComId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                LEFT JOIN dbo.[Set] s ON s.SetId = r.SetId
                WHERE r.CreatedBy = @UserId
                  AND r.Description LIKE '[[]PORTAL%'
                ORDER BY r.DateCreated DESC";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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
                            string? remarks = reader.IsDBNull(5) ? null : reader.GetString(5);

                            requests.Add(new PortalRequestStatusViewModel
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                FullDescription = description,
                                FullRemarks = remarks,
                                DistributionMethod = ExtractDistributionMethod(description),
                                DateCreated = reader.GetDateTime(6),
                                ItemName = reader.GetString(7),
                                ItemModelNumber = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DestinationEmployeeName = reader.IsDBNull(9) ? "—" : reader.GetString(9),
                                DestinationBranch = reader.IsDBNull(10) ? "—" : reader.GetString(10),
                                DestinationDepartment = reader.IsDBNull(11) ? "—" : reader.GetString(11),
                                DestinationCompany = reader.IsDBNull(12) ? "—" : reader.GetString(12),
                                GoodEmptyQty = reader.GetInt32(13),
                                DamagedEmptyQty = reader.GetInt32(14),
                                CartridgeName = reader.IsDBNull(15) ? null : reader.GetString(15),
                                SubmissionSessionId = reader.IsDBNull(16) ? (Guid?)null : reader.GetGuid(16),
                                SetCode = reader.IsDBNull(17) ? null : reader.GetString(17)
                            });
                        }
                    }
                }
            }

            return requests;
        }

        /// <summary>
        /// Get a single request status by ReqId.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        public PortalRequestStatusViewModel? GetRequestStatus(int reqId)
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
                    -- Department-level requests (EmpId NULL) carry ComId/BranchId/DeptId directly
                    -- on the Request row instead of an Employee — fall back to those.
                    e.Name AS DestinationEmployeeName,
                    ISNULL(b.Name, rb.Name) AS DestinationBranch,
                    ISNULL(d.Name, rd.Name) AS DestinationDepartment,
                    ISNULL(c.Name, rc.Name) AS DestinationCompany
                FROM dbo.Request r
                INNER JOIN dbo.Item i        ON r.ItemId = i.ItemId
                LEFT JOIN dbo.Employee e     ON r.EmpId = e.EmpId
                LEFT JOIN dbo.Branch b       ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d   ON e.DeptId = d.DeptId
                LEFT JOIN dbo.Company c      ON e.ComId = c.ComId
                LEFT JOIN dbo.Branch rb      ON r.BranchId = rb.BranchId
                LEFT JOIN dbo.Department rd  ON r.DeptId = rd.DeptId
                LEFT JOIN dbo.Company rc     ON r.ComId = rc.ComId
                WHERE r.ReqId = @ReqId";

            using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
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
                            string? remarks = reader.IsDBNull(5) ? null : reader.GetString(5);

                            return new PortalRequestStatusViewModel
                            {
                                ReqId = reader.GetInt32(0),
                                DateRequested = reader.GetDateTime(1),
                                Status = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                FullDescription = description,
                                FullRemarks = remarks,
                                DistributionMethod = ExtractDistributionMethod(description),
                                DateCreated = reader.GetDateTime(6),
                                ItemName = reader.GetString(7),
                                ItemModelNumber = reader.IsDBNull(8) ? null : reader.GetString(8),
                                DestinationEmployeeName = reader.IsDBNull(9) ? "—" : reader.GetString(9),
                                DestinationBranch = reader.IsDBNull(10) ? "—" : reader.GetString(10),
                                DestinationDepartment = reader.IsDBNull(11) ? "—" : reader.GetString(11),
                                DestinationCompany = reader.IsDBNull(12) ? "—" : reader.GetString(12)
                            };
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get destination information as a formatted string for the Description field.
        /// COPIED FROM: Yakult.Inventory.App/Services/RequesterPortalService.cs
        /// </summary>
        private string GetDestinationInfo(int companyId, int branchId, int departmentId)
        {
            // Department-level requests (IT Assisted only) may leave Company/Branch/Department
            // completely blank ("Fill in only what you have").
            if (companyId <= 0 && branchId <= 0 && departmentId <= 0)
                return "Unassigned (Dept-Level Request)";

            try
            {
                using (var con = new SqlConnection(_connectionStringProvider.GetConnectionString()))
                {
                    con.Open();
                    // Independent lookups (not a joined match) — dept-level requests may only
                    // have some of Company/Branch/Department filled in.
                    const string sql = @"
                        SELECT
                            (SELECT Name FROM dbo.Branch     WHERE BranchId = @BranchId)     AS BranchName,
                            (SELECT Name FROM dbo.Department WHERE DeptId   = @DepartmentId) AS DepartmentName";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);
                        cmd.Parameters.AddWithValue("@BranchId", branchId);
                        cmd.Parameters.AddWithValue("@DepartmentId", departmentId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string branch = reader.IsDBNull(0) ? "—" : reader.GetString(0);
                                string department = reader.IsDBNull(1) ? "—" : reader.GetString(1);

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

        private string ExtractDistributionMethod(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "UNKNOWN";

            if (description.Contains("PICKUP"))
                return "PICKUP";
            if (description.Contains("DELIVERY"))
                return "DELIVERY";

            return "UNKNOWN";
        }

        private string ExtractCartridgeCondition(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                return "Not Specified";

            if (remarks.Contains("With Cartridge"))
                return "With Cartridge";
            if (remarks.Contains("Without Cartridge"))
                return "Without Cartridge";

            return "Not Specified";
        }

        #endregion
    }
}
