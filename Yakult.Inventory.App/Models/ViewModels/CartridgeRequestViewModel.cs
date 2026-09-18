using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.Models.ViewModels
{
    /// <summary>
    /// Main ViewModel for cartridge request submission.
    /// Used by IT staff/developers to create requests on behalf of employees.
    /// </summary>
    public class CartridgeRequestViewModel
    {
        /// <summary>
        /// IMPORTANT: Selected employee for whom this request is being created.
        /// This is the employee who will receive/use the cartridges.
        /// NOT the logged-in user creating the request.
        /// </summary>
        public int SelectedEmployeeId { get; set; }

        /// <summary>
        /// Employee name (auto-filled from selected employee).
        /// This is who the cartridges are for.
        /// </summary>
        public string EmployeeName { get; set; }

        /// <summary>
        /// Employee position (auto-filled from selected employee).
        /// </summary>
        public string EmployeePosition { get; set; }

        /// <summary>
        /// Employee number (auto-filled from selected employee).
        /// </summary>
        public string EmployeeNumber { get; set; }

        /// <summary>
        /// Primary input: User-typed model number (for quick entry).
        /// Alternative to dropdown selection.
        /// </summary>
        public string TypedModelNumber { get; set; }

        /// <summary>
        /// Dropdown-selected model key (normalized identifier).
        /// </summary>
        public string ModelKey { get; set; }

        /// <summary>
        /// Requested quantity (for single-item mode).
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Cartridge condition selection.
        /// Valid values: "With Cartridge" or "Without Cartridge"
        /// </summary>
        public string CartridgeCondition { get; set; }

        /// <summary>
        /// Destination company ID (auto-filled from selected employee's company).
        /// Can be manually overridden if delivery to different location.
        /// </summary>
        public int DestinationCompanyId { get; set; }

        /// <summary>
        /// Destination branch ID (auto-filled from selected employee's branch).
        /// Can be manually overridden if delivery to different location.
        /// </summary>
        public int DestinationBranchId { get; set; }

        /// <summary>
        /// Destination department ID (auto-filled from selected employee's department).
        /// Can be manually overridden if delivery to different location.
        /// </summary>
        public int DestinationDepartmentId { get; set; }

        /// <summary>
        /// Fulfillment method selection.
        /// Valid values: "PICKUP" or "DELIVERY"
        /// </summary>
        public string FulfillmentMethod { get; set; }

        /// <summary>
        /// Additional remarks for the entire request (optional).
        /// </summary>
        public string AdditionalRemarks { get; set; }

        /// <summary>
        /// Employee who will physically receive the cartridges (required when FulfillmentMethod = "PICKUP").
        /// </summary>
        public int? ReceivedById { get; set; }

        /// <summary>
        /// Date the request was actually made (IT-assisted requests may be entered after the fact).
        /// Defaults to now when not set.
        /// </summary>
        public DateTime? DateRequested { get; set; }

        /// <summary>
        /// Multi-model support: List of cartridge items to request.
        /// Each item represents a different cartridge model + quantity + condition.
        /// </summary>
        public List<CartridgeRequestItemViewModel> Items { get; set; }

        public CartridgeRequestViewModel()
        {
            Items = new List<CartridgeRequestItemViewModel>();
            FulfillmentMethod = "PICKUP"; // Default
            CartridgeCondition = "With Cartridge"; // Default
            Quantity = 1; // Default
        }

        /// <summary>
        /// Validates the request before submission.
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            // Employee selection required
            if (SelectedEmployeeId <= 0)
            {
                errorMessage = "Please select an employee for whom this request is being created.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(EmployeeName))
            {
                errorMessage = "Employee name is required.";
                return false;
            }

            // Destination validation
            if (DestinationCompanyId <= 0)
            {
                errorMessage = "Destination company is required.";
                return false;
            }

            if (DestinationBranchId <= 0)
            {
                errorMessage = "Destination branch is required.";
                return false;
            }

            if (DestinationDepartmentId <= 0)
            {
                errorMessage = "Destination department is required.";
                return false;
            }

            // Items validation
            if (Items == null || Items.Count == 0)
            {
                errorMessage = "At least one cartridge model is required.";
                return false;
            }

            // Validate each item
            foreach (var item in Items)
            {
                if (!item.IsValid())
                {
                    errorMessage = $"Invalid item: {item.CartridgeModel ?? "Unknown"}. Please check model, quantity, and condition.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Total quantity across all items.
        /// </summary>
        public int TotalQuantity => Items?.Sum(x => x.Quantity) ?? Quantity;

        /// <summary>
        /// Total number of different models being requested.
        /// </summary>
        public int TotalModels => Items?.Count ?? (string.IsNullOrWhiteSpace(ModelKey) ? 0 : 1);
    }
}
