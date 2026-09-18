using System;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Business logic layer for the CartridgeAuthorization workflow.
    /// All authorization rules are centralized here so UI code stays clean.
    /// </summary>
    public class AuthorizationService
    {
        private readonly CartridgeAuthorizationRepository _repo;

        public AuthorizationService(CartridgeAuthorizationRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        /// <summary>
        /// Ensures the employee has an authorization record.
        /// If none exists (or the last one is Rejected/Used), creates a new Pending record.
        /// Returns the current (or newly created) authorization.
        /// </summary>
        public async Task<CartridgeAuthorizationModel> EnsureAuthorizationForEmployeeAsync(
            int employeeId, int departmentId)
        {
            var current = await _repo.GetEmployeeAuthorizationAsync(employeeId);

            if (current == null
                || current.Status == "Rejected"
                || current.Status == "Used")
            {
                int newId = await _repo.CreatePendingAuthorizationAsync(employeeId, departmentId);
                return new CartridgeAuthorizationModel
                {
                    AuthorizationId = newId,
                    EmployeeId      = employeeId,
                    DepartmentId    = departmentId,
                    Status          = "Pending",
                    CreatedDate     = DateTime.Now
                };
            }

            return current;
        }

        /// <summary>
        /// Checks whether the employee has a valid Approved authorization that has not been Used.
        /// Returns the authorization if valid, null otherwise.
        /// </summary>
        public async Task<CartridgeAuthorizationModel> ValidateEmployeeAuthorizationAsync(int employeeId)
        {
            var auth = await _repo.GetEmployeeAuthorizationAsync(employeeId);
            return (auth != null && auth.Status == "Approved") ? auth : null;
        }

        /// <summary>
        /// Approves an authorization. Returns true on success; false if already signed by someone else.
        /// </summary>
        public async Task<bool> ApproveAuthorizationAsync(int authorizationId, int supervisorUserId)
        {
            return await _repo.ApproveAuthorizationAsync(authorizationId, supervisorUserId);
        }

        /// <summary>Rejects an authorization.</summary>
        public async Task RejectAuthorizationAsync(int authorizationId)
        {
            await _repo.RejectAuthorizationAsync(authorizationId);
        }

        /// <summary>Marks an Approved authorization as Used after a successful request submission.</summary>
        public async Task ConsumeAuthorizationAsync(int authorizationId)
        {
            await _repo.MarkAuthorizationUsedAsync(authorizationId);
        }
    }
}
