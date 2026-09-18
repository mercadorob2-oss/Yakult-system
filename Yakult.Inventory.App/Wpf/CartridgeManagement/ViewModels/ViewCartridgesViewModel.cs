using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class ViewCartridgesViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private List<ItemDto> _all      = new List<ItemDto>();
        private List<ItemDto> _filtered = new List<ItemDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText   = string.Empty;
        private bool   _showInactive;
        private int    _totalRecords;
        private int    _totalActive;
        private string _pageInfo = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<ItemDto> PagedRows { get; }
            = new ObservableCollection<ItemDto>();

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public int    TotalActive  { get => _totalActive;  set => SetField(ref _totalActive,  value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterFromTop(); }
        }

        public bool ShowInactive
        {
            get => _showInactive;
            set { if (SetField(ref _showInactive, value)) _ = LoadAsync(); }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all         = await FetchItemsAsync(_showInactive);
                TotalRecords = _all.Count;
                TotalActive  = _all.Count(x => x.Active);
                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("ViewCartridgesViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage() { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }

        private void ApplyFilterFromTop() { _currentPage = 1; ApplyFilter(); }

        private void ApplyFilter()
        {
            var search = (_searchText ?? string.Empty).Trim().ToLowerInvariant();

            _filtered = string.IsNullOrEmpty(search)
                ? _all.ToList()
                : _all.Where(r =>
                    (r.Name          ?? "").ToLowerInvariant().Contains(search) ||
                    (r.Description   ?? "").ToLowerInvariant().Contains(search) ||
                    (r.ModelNumber   ?? "").ToLowerInvariant().Contains(search) ||
                    (r.SerialNumber  ?? "").ToLowerInvariant().Contains(search) ||
                    (r.VendorName    ?? "").ToLowerInvariant().Contains(search) ||
                    (r.ConditionName ?? "").ToLowerInvariant().Contains(search) ||
                    (r.LatestStatus  ?? "").ToLowerInvariant().Contains(search) ||
                    (r.ItemType      ?? "").ToLowerInvariant().Contains(search)
                ).ToList();

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} record{(_filtered.Count == 1 ? "" : "s")})";
        }

        private static Task<List<ItemDto>> FetchItemsAsync(bool includeInactive)
        {
            return Task.Run(() =>
            {
                var items = new List<ItemDto>();

                string activeClause = includeInactive ? "" : "\n  AND i.Active = 1";

                string sql = @"
SELECT
    i.ItemId, i.Name, i.Description, i.Category, i.ItemType,
    i.SerialNumber, i.ModelNumber, i.Active AS DbActive, i.UnitOfMeasure,
    ISNULL(i.StockOnHand, 0) AS StockOnHand,
    i.DateCreated, u1.Name AS CreatedByName,
    i.DateModified, u2.Name AS ModifiedByName,
    i.Active AS EffectiveActive,
    i.Amount, c.ConditionName, i.ConditionId, i.Remarks,
    i.WarrantyYears, i.WarrantyStartDate, i.WarrantyEndDate, i.DatePurchased,
    CASE WHEN i.ConditionId = 1 THEN 'Good' WHEN i.ConditionId = 2 THEN 'Damaged' ELSE NULL END AS LatestStatus,
    su.Remark AS LatestRemark,
    ISNULL(rh.RepairCount, 0) AS RepairCount,
    lr.RepairAction AS LastRepairAction,
    i.VendorId, v.VendorName,
    i.IsTrackedAsset, i.AcquisitionType,
    CASE WHEN cm.ModelNumber = 'Unassigned' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS UsedDefaultCartridgeModel
FROM dbo.Item i
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.[User] u2 ON i.ModifiedBy = u2.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = i.CartridgeModelId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT SerialNumber, MAX(CreatedAt) AS LatestCreatedAt
    FROM dbo.SetItemUpdate WHERE Processed = 1 GROUP BY SerialNumber
) lu ON lu.SerialNumber = i.SerialNumber
LEFT JOIN dbo.SetItemUpdate su
    ON su.SerialNumber = i.SerialNumber AND su.CreatedAt = lu.LatestCreatedAt AND su.Processed = 1
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount, MAX(CreatedAt) AS LastRepairAt
    FROM dbo.ItemRepairHistory GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN dbo.ItemRepairHistory lr
    ON lr.SerialNumber = i.SerialNumber AND lr.CreatedAt = rh.LastRepairAt
WHERE i.Category = 'Cartridge'
  AND arch.EntityId IS NULL" + activeClause + @"
ORDER BY i.DateCreated DESC, i.ItemId DESC";

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            items.Add(new ItemDto
                            {
                                ItemId            = rd.GetInt32(0),
                                Name              = rd.GetString(1),
                                Description       = rd.IsDBNull(2)  ? null : rd.GetString(2),
                                Category          = rd.IsDBNull(3)  ? null : rd.GetString(3),
                                ItemType          = rd.IsDBNull(4)  ? null : rd.GetString(4),
                                SerialNumber      = rd.IsDBNull(5)  ? null : rd.GetString(5),
                                ModelNumber       = rd.IsDBNull(6)  ? null : rd.GetString(6),
                                DbActive          = rd.GetBoolean(7),
                                UnitOfMeasure     = rd.IsDBNull(8)  ? null : rd.GetString(8),
                                StockOnHand       = rd.IsDBNull(9)  ? 0    : rd.GetInt32(9),
                                DateCreated       = rd.IsDBNull(10) ? DateTime.MinValue : rd.GetDateTime(10),
                                CreatedByName     = rd.IsDBNull(11) ? "N/A" : rd.GetString(11),
                                DateModified      = rd.IsDBNull(12) ? (DateTime?)null  : rd.GetDateTime(12),
                                ModifiedByName    = rd.IsDBNull(13) ? null : rd.GetString(13),
                                Active            = rd.GetBoolean(14),
                                Amount            = rd.IsDBNull(15) ? 0m   : rd.GetDecimal(15),
                                ConditionName     = rd.IsDBNull(16) ? null : rd.GetString(16),
                                ConditionId       = rd.IsDBNull(17) ? 1    : rd.GetInt32(17),
                                Remarks           = rd.IsDBNull(18) ? null : rd.GetString(18),
                                WarrantyYears     = rd.GetInt32(19),
                                WarrantyStartDate = rd.IsDBNull(20) ? (DateTime?)null  : rd.GetDateTime(20),
                                WarrantyEndDate   = rd.IsDBNull(21) ? (DateTime?)null  : rd.GetDateTime(21),
                                DatePurchased     = rd.IsDBNull(22) ? (DateTime?)null  : rd.GetDateTime(22),
                                LatestStatus      = rd.IsDBNull(23) ? null : rd.GetString(23),
                                LatestRemark      = rd.IsDBNull(24) ? null : rd.GetString(24),
                                RepairCount       = rd.IsDBNull(25) ? 0    : rd.GetInt32(25),
                                LastRepairAction  = rd.IsDBNull(26) ? null : rd.GetString(26),
                                VendorId          = rd.IsDBNull(27) ? (int?)null : rd.GetInt32(27),
                                VendorName        = rd.IsDBNull(28) ? null : rd.GetString(28),
                                IsTrackedAsset    = !rd.IsDBNull(29) && rd.GetBoolean(29),
                                AcquisitionType   = rd.IsDBNull(30) ? null : rd.GetString(30),
                                UsedDefaultCartridgeModel = !rd.IsDBNull(31) && rd.GetBoolean(31)
                            });
                        }
                    }
                }
                return items;
            });
        }
    }
}
