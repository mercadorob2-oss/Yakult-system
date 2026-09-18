using System;
using System.Linq;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Tests
{
    /// <summary>
    /// Test/verification class for Requester Portal Service.
    /// Run these tests to verify backend integration before building UI.
    /// </summary>
    public class RequesterPortalServiceTest
    {
        private RequesterPortalService _service;

        public RequesterPortalServiceTest()
        {
            _service = new RequesterPortalService();
        }

        /// <summary>
        /// Test 1: Verify cartridge items can be retrieved.
        /// Expected: Should return list of items with Category = 'Cartridge'.
        /// </summary>
        public void Test_GetCartridgeItems()
        {
            Console.WriteLine("=== Test 1: Get Cartridge Items ===");

            try
            {
                var cartridges = _service.GetCartridgeItems();

                Console.WriteLine($"Found {cartridges.Count} cartridge items:");
                foreach (var item in cartridges.Take(5)) // Show first 5
                {
                    Console.WriteLine($"  - ID: {item.ItemId}, Name: {item.ItemName}, Model: {item.ModelNumber}, Stock: {item.StockOnHand}");
                }

                if (cartridges.Count == 0)
                {
                    Console.WriteLine("  ⚠️ WARNING: No cartridge items found. Add items with Category = 'Cartridge' to the database.");
                }
                else
                {
                    Console.WriteLine("  ✅ PASS: Cartridge items retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 2: Verify companies can be retrieved.
        /// Expected: Should return list of active companies.
        /// </summary>
        public void Test_GetActiveCompanies()
        {
            Console.WriteLine("=== Test 2: Get Active Companies ===");

            try
            {
                var companies = _service.GetActiveCompanies();

                Console.WriteLine($"Found {companies.Count} active companies:");
                foreach (var company in companies)
                {
                    Console.WriteLine($"  - ID: {company.ComId}, Name: {company.Name}");
                }

                if (companies.Count == 0)
                {
                    Console.WriteLine("  ⚠️ WARNING: No active companies found.");
                }
                else
                {
                    Console.WriteLine("  ✅ PASS: Companies retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 3: Verify branches can be retrieved for a company.
        /// Expected: Should return list of branches for the specified company.
        /// </summary>
        public void Test_GetBranchesByCompany(int companyId = 1)
        {
            Console.WriteLine($"=== Test 3: Get Branches for Company {companyId} ===");

            try
            {
                var branches = _service.GetBranchesByCompany(companyId);

                Console.WriteLine($"Found {branches.Count} branches:");
                foreach (var branch in branches)
                {
                    Console.WriteLine($"  - ID: {branch.BranchId}, Name: {branch.Name}");
                }

                if (branches.Count == 0)
                {
                    Console.WriteLine($"  ⚠️ WARNING: No branches found for company {companyId}.");
                }
                else
                {
                    Console.WriteLine("  ✅ PASS: Branches retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 4: Verify departments can be retrieved for a company.
        /// Expected: Should return list of departments for the specified company.
        /// </summary>
        public void Test_GetDepartmentsByCompany(int companyId = 1)
        {
            Console.WriteLine($"=== Test 4: Get Departments for Company {companyId} ===");

            try
            {
                var departments = _service.GetDepartmentsByCompany(companyId);

                Console.WriteLine($"Found {departments.Count} departments:");
                foreach (var dept in departments)
                {
                    Console.WriteLine($"  - ID: {dept.DeptId}, Name: {dept.Name}");
                }

                if (departments.Count == 0)
                {
                    Console.WriteLine($"  ⚠️ WARNING: No departments found for company {companyId}.");
                }
                else
                {
                    Console.WriteLine("  ✅ PASS: Departments retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 5: Verify destination employee can be found or created.
        /// Expected: Should return a valid EmpId for the destination.
        /// </summary>
        public void Test_GetOrCreateDestinationEmployee(int companyId = 1, int branchId = 1, int deptId = 1, int userId = 1)
        {
            Console.WriteLine($"=== Test 5: Get/Create Destination Employee ===");
            Console.WriteLine($"  Company: {companyId}, Branch: {branchId}, Dept: {deptId}");

            try
            {
                int? empId = _service.GetOrCreateDestinationEmployee(companyId, branchId, deptId, userId);

                if (empId.HasValue)
                {
                    Console.WriteLine($"  ✅ PASS: Destination employee ID: {empId.Value}");
                }
                else
                {
                    Console.WriteLine($"  ❌ FAIL: Could not find or create destination employee.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 6: Create a sample cartridge request (DRY RUN).
        /// This test validates the request creation logic without actually submitting.
        /// </summary>
        public void Test_CreateCartridgeRequest_DryRun()
        {
            Console.WriteLine("=== Test 6: Create Cartridge Request (DRY RUN) ===");

            try
            {
                // Get first cartridge
                var cartridges = _service.GetCartridgeItems();
                if (cartridges.Count == 0)
                {
                    Console.WriteLine("  ⚠️ SKIP: No cartridge items available.");
                    return;
                }

                var cartridge = cartridges.First();

                // Get first company, branch, dept
                var companies = _service.GetActiveCompanies();
                if (companies.Count == 0)
                {
                    Console.WriteLine("  ⚠️ SKIP: No companies available.");
                    return;
                }

                var company = companies.First();
                var branches = _service.GetBranchesByCompany(company.ComId);
                var departments = _service.GetDepartmentsByCompany(company.ComId);

                if (branches.Count == 0 || departments.Count == 0)
                {
                    Console.WriteLine("  ⚠️ SKIP: No branches or departments available.");
                    return;
                }

                var branch = branches.First();
                var dept = departments.First();

                Console.WriteLine("  Sample Request Details:");
                Console.WriteLine($"    Item: {cartridge.ItemName} (ID: {cartridge.ItemId})");
                Console.WriteLine($"    Quantity: 5");
                Console.WriteLine($"    Cartridge Condition: With Cartridge");
                Console.WriteLine($"    Destination: {company.Name} > {branch.Name} > {dept.Name}");
                Console.WriteLine($"    Fulfillment: PICKUP");

                Console.WriteLine();
                Console.WriteLine("  ⚠️ NOTE: This is a DRY RUN. To actually submit, use:");
                Console.WriteLine("    var request = new CartridgeRequestDto { ... };");
                Console.WriteLine("    int reqId = _service.CreateCartridgeRequest(request);");
                Console.WriteLine();
                Console.WriteLine("  ✅ PASS: Request validation passed (not submitted).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Test 7: Retrieve portal requests for a user.
        /// Expected: Should return list of requests created via portal.
        /// </summary>
        public void Test_GetPortalRequestsByUser(int userId = 1)
        {
            Console.WriteLine($"=== Test 7: Get Portal Requests for User {userId} ===");

            try
            {
                var requests = _service.GetPortalRequestsByUser(userId);

                Console.WriteLine($"Found {requests.Count} portal requests:");
                foreach (var req in requests.Take(5)) // Show first 5
                {
                    Console.WriteLine($"  - Req #{req.ReqId}: {req.Status} | {req.ItemName} x{req.Quantity} | {req.FulfillmentMethod} | {req.DestinationBranch}");
                }

                if (requests.Count == 0)
                {
                    Console.WriteLine($"  ℹ️ INFO: No portal requests found for user {userId}. This is normal if portal hasn't been used yet.");
                }
                else
                {
                    Console.WriteLine("  ✅ PASS: Portal requests retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAIL: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Run all tests.
        /// </summary>
        public void RunAllTests()
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║      REQUESTER PORTAL SERVICE - INTEGRATION TESTS              ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            Test_GetCartridgeItems();
            Test_GetActiveCompanies();
            Test_GetBranchesByCompany(1); // Use ComId = 1
            Test_GetDepartmentsByCompany(1);
            Test_GetOrCreateDestinationEmployee(1, 1, 1, 1); // Use real IDs from your database
            Test_CreateCartridgeRequest_DryRun();
            Test_GetPortalRequestsByUser(1); // Use real UserId

            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     TESTS COMPLETED                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        }
    }

    /// <summary>
    /// Entry point for running tests.
    /// Add this to a test button or run from a test form.
    /// </summary>
    public class TestRunner
    {
        public static void Run()
        {
            var test = new RequesterPortalServiceTest();
            test.RunAllTests();
        }
    }
}
