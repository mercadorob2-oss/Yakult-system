using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Service for cartridge exchange operations.
    /// Supports multi-model cartridge requests and FIFO fulfillment.
    /// 
    /// Business Rules:
    /// - A single request may contain multiple cartridge models
    /// - Each model is processed independently
    /// - Partial or zero fulfillment is allowed per model
    /// - Unfulfilled records are created per model if needed
    /// - FIFO: Oldest pending records are fulfilled first
    /// </summary>
    public sealed class CartridgeExchangeService
    {
        private readonly string _connectionString;
        private readonly CartridgeManagementRepository _cartridgeRepo;
        private readonly UnfulfilledCartridgeExchangeRepository _unfulfilledRepo;

        public CartridgeExchangeService()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            _cartridgeRepo = new CartridgeManagementRepository();
            _unfulfilledRepo = new UnfulfilledCartridgeExchangeRepository();

            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not configured.");
        }

        /// <summary>
        /// Processes a multi-model cartridge exchange request.
        /// Each model is processed independently with its own fulfillment status.
        /// </summary>
        /// <param name="reqId">Request ID</param>
        /// <param name="empId">Employee ID (requester)</param>
        /// <param name="branchId">Branch ID</param>
        /// <param name="deptId">Department ID</param>
        /// <param name="modelRequests">List of cartridge models with quantities</param>
        /// <param name="userId">User performing the fulfillment</param>
        /// <returns>Result containing fulfillment details per model</returns>
        public MultiModelFulfillmentResult ProcessMultiModelExchange(
            int reqId,
            int empId,
            int? branchId,
            int? deptId,
            List<CartridgeModelRequestDto> modelRequests,
            int userId)
        {
            var result = new MultiModelFulfillmentResult
            {
                ReqId = reqId,
                ModelResults = new List<ModelFulfillmentResult>()
            };

            foreach (var modelRequest in modelRequests)
            {
                var modelResult = ProcessSingleModelExchange(
                    reqId, empId, branchId, deptId,
                    modelRequest.CartridgeModel,
                    modelRequest.RequestedQty,
                    userId,
                    modelRequest.GoodEmptyQty,
                    modelRequest.DamagedEmptyQty);

                result.ModelResults.Add(modelResult);
            }

            // Calculate overall status
            result.TotalRequestedQty = result.ModelResults.Sum(m => m.RequestedQty);
            result.TotalIssuedQty = result.ModelResults.Sum(m => m.IssuedQty);
            result.TotalUnfulfilledQty = result.ModelResults.Sum(m => m.UnfulfilledQty);

            if (result.TotalIssuedQty == 0)
                result.OverallStatus = "Unfulfilled";
            else if (result.TotalIssuedQty < result.TotalRequestedQty)
                result.OverallStatus = "Partially Fulfilled";
            else
                result.OverallStatus = "Fulfilled";

            return result;
        }

        /// <summary>
        /// Processes a single cartridge model exchange.
        /// </summary>
        private ModelFulfillmentResult ProcessSingleModelExchange(
            int reqId,
            int empId,
            int? branchId,
            int? deptId,
            string cartridgeModel,
            int requestedQty,
            int userId,
            int goodEmptyQty = 0,
            int damagedEmptyQty = 0)
        {
            var result = new ModelFulfillmentResult
            {
                CartridgeModel = cartridgeModel,
                RequestedQty = requestedQty,
                ReturnedEmptyQty = requestedQty  // Business rule: ReturnedEmptyQty = RequestedQty
            };

            // Get available stock for this model
            int availableStock = 0;
            try
            {
                int? cartridgeModelId = _cartridgeRepo.GetCartridgeModelIdByModelNumber(cartridgeModel);
                availableStock = _cartridgeRepo.GetAvailableIssuableStock(cartridgeModelId);
            }
            catch
            {
                availableStock = 0;
            }

            result.AvailableStock = availableStock;

            // Calculate how many can be issued
            int issuedQty = Math.Min(requestedQty, availableStock);
            result.IssuedQty = issuedQty;
            result.UnfulfilledQty = requestedQty - issuedQty;

            // Generate auto-remarks
            result.Remarks = CartridgeExchangeRemarks.GenerateRemarks(
                issuedQty, requestedQty, cartridgeModel);

            // Determine fulfillment status
            if (issuedQty == 0)
                result.Status = "Unfulfilled";
            else if (issuedQty < requestedQty)
                result.Status = "Partially Fulfilled";
            else
                result.Status = "Fulfilled";

            // Create unfulfilled exchange record if not fully fulfilled
            if (result.UnfulfilledQty > 0)
            {
                result.UnfulfilledExchangeId = _unfulfilledRepo.CreateUnfulfilledExchange(
                    reqId: reqId,
                    empId: empId,
                    branchId: branchId,
                    deptId: deptId,
                    cartridgeModel: cartridgeModel,
                    requestedQty: requestedQty,
                    returnedEmptyQty: requestedQty,
                    issuedFullQty: issuedQty,
                    remarks: result.Remarks,
                    createdBy: userId,
                    goodEmptyQty: goodEmptyQty,
                    damagedEmptyQty: damagedEmptyQty
                );
            }

            return result;
        }

        /// <summary>
        /// Fulfills pending exchanges using FIFO order when stock becomes available.
        /// Call this when new cartridge stock is received.
        /// </summary>
        /// <param name="cartridgeModel">Model to fulfill</param>
        /// <param name="availableStock">New stock available</param>
        /// <param name="userId">User performing the fulfillment</param>
        /// <returns>Number of cartridges actually issued</returns>
        public int FulfillPendingExchangesFifo(string cartridgeModel, int availableStock, int userId)
        {
            return _unfulfilledRepo.FulfillPendingExchangesFifo(
                cartridgeModel, availableStock, userId,
                $"Auto-fulfilled from stock replenishment");
        }

        /// <summary>
        /// Gets pending unfulfilled exchanges for a specific model.
        /// </summary>
        public List<UnfulfilledCartridgeExchangeDto> GetPendingExchangesByModel(string cartridgeModel)
        {
            return _unfulfilledRepo.GetPendingExchangesByModel(cartridgeModel);
        }

        /// <summary>
        /// Gets summary of pending exchanges by model.
        /// </summary>
        public List<(string Model, int PendingCount, int UnfulfilledQty)> GetPendingSummaryByModel()
        {
            return _unfulfilledRepo.GetPendingSummaryByModel();
        }

        /// <summary>
        /// Parses multi-model request from portal description.
        /// Portal format: [MODELS:Model1=Qty1,Model2=Qty2,...]
        /// </summary>
        public static List<CartridgeModelRequestDto> ParseMultiModelRequest(string description)
        {
            var result = new List<CartridgeModelRequestDto>();

            if (string.IsNullOrWhiteSpace(description))
                return result;

            // Look for [MODELS:...] pattern
            const string modelsPrefix = "[MODELS:";
            int startIndex = description.IndexOf(modelsPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                return result;

            startIndex += modelsPrefix.Length;
            int endIndex = description.IndexOf(']', startIndex);
            if (endIndex < 0)
                return result;

            string modelsStr = description.Substring(startIndex, endIndex - startIndex);
            var modelPairs = modelsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in modelPairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2 &&
                    !string.IsNullOrWhiteSpace(parts[0]) &&
                    int.TryParse(parts[1].Trim(), out int qty) &&
                    qty > 0)
                {
                    result.Add(new CartridgeModelRequestDto
                    {
                        CartridgeModel = parts[0].Trim(),
                        RequestedQty = qty
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a request description contains multi-model format.
        /// </summary>
        public static bool IsMultiModelRequest(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;

            return description.IndexOf("[MODELS:", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Result of a multi-model cartridge exchange fulfillment.
    /// </summary>
    public sealed class MultiModelFulfillmentResult
    {
        public int ReqId { get; set; }
        public List<ModelFulfillmentResult> ModelResults { get; set; }
        public int TotalRequestedQty { get; set; }
        public int TotalIssuedQty { get; set; }
        public int TotalUnfulfilledQty { get; set; }
        public string OverallStatus { get; set; }

        public string SummaryDisplay =>
            $"Total: Requested={TotalRequestedQty}, Issued={TotalIssuedQty}, Pending={TotalUnfulfilledQty} | Status: {OverallStatus}";
    }

    /// <summary>
    /// Result of a single model fulfillment within a multi-model request.
    /// </summary>
    public sealed class ModelFulfillmentResult
    {
        public string CartridgeModel { get; set; }
        public int RequestedQty { get; set; }
        public int ReturnedEmptyQty { get; set; }
        public int AvailableStock { get; set; }
        public int IssuedQty { get; set; }
        public int UnfulfilledQty { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public int? UnfulfilledExchangeId { get; set; }

        public string SummaryDisplay =>
            $"{CartridgeModel}: Returned={ReturnedEmptyQty}, Issued={IssuedQty}, Pending={UnfulfilledQty} | {Status}";
    }
}
