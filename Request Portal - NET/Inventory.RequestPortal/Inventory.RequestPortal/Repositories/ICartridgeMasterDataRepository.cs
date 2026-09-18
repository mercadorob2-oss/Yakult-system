using Inventory.RequestPortal.Models;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Cartridge Master Data — Cartridge Models (full CRUD) and View Cartridges (read-only).
    /// PORTED FROM: Yakult.Inventory.App/Repositories/CartridgeModelRepository.cs and
    /// Wpf/CartridgeManagement/ViewModels/ViewCartridgesViewModel.cs.
    /// </summary>
    public interface ICartridgeMasterDataRepository
    {
        Task<List<CartridgeModelDto>> GetAllActiveModelsAsync();
        Task<CartridgeModelDto?> GetModelByIdAsync(int cartridgeModelId);
        Task<CartridgeModelDto?> FindModelByModelNumberAsync(string modelNumber);
        Task<int> CreateModelAsync(CartridgeModelDto model);
        Task UpdateModelAsync(CartridgeModelDto model);
        Task ArchiveModelAsync(int cartridgeModelId, string reason, bool deactivate, string archivedBy);

        /// <summary>
        /// ItemCount covers dbo.Item (nullable FK, safely cleared on delete). EmptyCartridgeCount
        /// and BatchLineCount cover dbo.EmptyCartridge / dbo.VendorCartridgeBatchLine (NOT NULL
        /// FKs) — those represent physical inventory / audit history and block a permanent
        /// delete outright.
        /// </summary>
        Task<(bool HasItems, int ItemCount, int EmptyCartridgeCount, int BatchLineCount)> CheckModelDependenciesAsync(int cartridgeModelId);

        Task SetModelActiveAsync(int cartridgeModelId, bool isActive);

        /// <summary>
        /// Permanently deletes a model. Throws InvalidOperationException if EmptyCartridge or
        /// VendorCartridgeBatchLine rows still reference it (see CheckModelDependenciesAsync).
        /// Unlinks (does not delete) any dbo.Item / dbo.VendorCartridgeBatch rows first.
        /// </summary>
        Task DeleteModelAsync(int cartridgeModelId);

        Task<List<CartridgeItemViewDto>> GetCartridgeItemsAsync(bool includeInactive);
    }
}
