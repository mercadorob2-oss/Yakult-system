using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Pages.Inventory;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Inventory.ViewModels
{
    /// <summary>Tab 4: Inventory — the only tab with edit/archive actions and permission
    /// gating (AppSession.IsReadOnly). Ported verbatim from the Tab-5-specific methods in the
    /// original ViewInventoryPage.cs.</summary>
    public sealed partial class InventoryPageViewModel
    {
        private const int PageSize = 10;

        private List<InventoryViewDto> _allInventory;
        private List<InventoryViewDto> _filteredInventory;
        private int _currentPage = 1;
        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;

        public ObservableCollection<InventoryViewDto> PagedInventory { get; } = new ObservableCollection<InventoryViewDto>();

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set { if (SetField(ref _showInactive, value)) LoadInventory(); }
        }

        public ObservableCollection<string> FilterByOptionsInventory { get; } = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };

        private string _selectedFilterByInventory = "Default";
        public string SelectedFilterByInventory
        {
            get => _selectedFilterByInventory;
            set
            {
                if (SetField(ref _selectedFilterByInventory, value))
                {
                    _sortColumnKey = null;
                    _sortDirection = null;
                    ApplyFilters();
                }
            }
        }

        public RelayCommand EditCommand => _editCommand ?? (_editCommand = new RelayCommand(Edit));
        public RelayCommand ArchiveCommand => _archiveCommand ?? (_archiveCommand = new RelayCommand(Archive));
        private RelayCommand _editCommand;
        private RelayCommand _archiveCommand;

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestErrorInventory;
        public event Action<InventoryViewDto> RequestEditInventory;
        public event Action<List<InventoryViewDto>> RequestArchiveConfirm;

        private void LoadInventory()
        {
            try
            {
                var inventory = new List<InventoryViewDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string activeFilter = ShowInactive ? "" : " AND it.Active = 1";

                    string query = $@"
                        SELECT
                            i.InvId,
                            i.Description,
                            i.EntryType,
                            i.Quantity,
                            i.DatePosted,
                            u.Name AS PostedByName,
                            i.ReqId,
                            it.Name AS ItemName,
                            it.Category,
                            ISNULL(it.ModelNumber, '') AS ModelNumber,
                            ISNULL(it.SerialNumber, '') AS SerialNumber,
                            (
                                SELECT ISNULL(SUM(
                                    CASE
                                        WHEN i2.EntryType = 'Positive' THEN i2.Quantity
                                        WHEN i2.EntryType = 'Negative' THEN -i2.Quantity
                                        ELSE 0
                                    END
                                ), 0)
                                FROM dbo.Inventory i2
                                INNER JOIN dbo.Item it2 ON i2.ItemId = it2.ItemId
                                LEFT JOIN dbo.ArchiveStatus arch2_inv ON arch2_inv.EntityType = 'Inventory' AND arch2_inv.EntityId = i2.InvId
                                LEFT JOIN dbo.ArchiveStatus arch2_itm ON arch2_itm.EntityType = 'Item' AND arch2_itm.EntityId = it2.ItemId
                                WHERE it2.Category = it.Category
                                  AND arch2_inv.EntityId IS NULL
                                  AND arch2_itm.EntityId IS NULL
                            ) AS CategoryTotalStock
                        FROM dbo.Inventory i
                        LEFT JOIN dbo.[User] u ON i.PostedBy = u.UserId
                        LEFT JOIN dbo.Item it ON i.ItemId = it.ItemId
                        LEFT JOIN dbo.ArchiveStatus arch_inv ON arch_inv.EntityType = 'Inventory' AND arch_inv.EntityId = i.InvId
                        LEFT JOIN dbo.ArchiveStatus arch_itm ON arch_itm.EntityType = 'Item' AND arch_itm.EntityId = it.ItemId
                        WHERE i.EntryType IN ('Negative', 'Positive', 'Fixed Assets'){activeFilter}
                          AND arch_inv.EntityId IS NULL
                          AND arch_itm.EntityId IS NULL
                        ORDER BY i.DatePosted DESC";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            inventory.Add(new InventoryViewDto
                            {
                                InvId = reader.GetInt32(0),
                                Description = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                EntryType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                DatePosted = reader.GetDateTime(4),
                                PostedByName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                RequestId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                ItemName = reader.IsDBNull(7) ? "N/A" : reader.GetString(7),
                                Category = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                                ModelNumber = reader.GetString(9),
                                SerialNumber = reader.GetString(10),
                                CategoryTotalStock = reader.GetInt32(11)
                            });
                        }
                    }
                }

                _allInventory = inventory;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                RequestErrorInventory?.Invoke("Error", $"Failed to load inventory: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
            if (_allInventory == null) return;

            var filtered = _allInventory.AsEnumerable();

            string searchText = SearchText?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(i =>
                    (i.ItemName != null && i.ItemName.ToLower().Contains(searchText)) ||
                    (i.ModelNumber != null && i.ModelNumber.ToLower().Contains(searchText)) ||
                    (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(searchText)) ||
                    (i.Category != null && i.Category.ToLower().Contains(searchText)) ||
                    (i.Description != null && i.Description.ToLower().Contains(searchText)) ||
                    (i.PostedByName != null && i.PostedByName.ToLower().Contains(searchText)) ||
                    (i.EntryType != null && i.EntryType.ToLower().Contains(searchText)) ||
                    (i.RequestId.HasValue && i.RequestId.Value.ToString().Contains(searchText))
                );
            }

            _filteredInventory = filtered.ToList();

            if (_sortColumnKey != null)
            {
                var propInfo = typeof(InventoryViewDto).GetProperty(_sortColumnKey);
                if (propInfo != null)
                {
                    _filteredInventory = _sortDirection == ListSortDirection.Ascending
                        ? _filteredInventory.OrderBy(x => propInfo.GetValue(x, null)).ToList()
                        : _filteredInventory.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
            }
            else
            {
                if (SelectedFilterByInventory == "Most Recently Added" || SelectedFilterByInventory == "Default")
                    _filteredInventory = _filteredInventory.OrderByDescending(x => x.DatePosted).ToList();
                else if (SelectedFilterByInventory == "Oldest Added")
                    _filteredInventory = _filteredInventory.OrderBy(x => x.DatePosted).ToList();
            }

            _currentPage = 1;
            UpdatePagination();
            if (ActiveTabIndex == 4) RaiseActivePagingChanged();
        }

        private void UpdatePagination()
        {
            if (_filteredInventory == null || _filteredInventory.Count == 0)
            {
                PagedInventory.Clear();
                return;
            }

            int totalPages = (int)Math.Ceiling((double)_filteredInventory.Count / PageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = _filteredInventory.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedInventory.Clear();
            foreach (var item in pagedData) PagedInventory.Add(item);
        }

        // ── Actions (Edit/Archive) — only reachable when ShowEditArchiveButtons is true ──

        private void Edit()
        {
            var checkedEntries = PagedInventory.Where(i => i.Selected).ToList();

            if (checkedEntries.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one entry to edit.");
                return;
            }
            if (checkedEntries.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one entry to edit.");
                return;
            }

            RequestEditInventory?.Invoke(checkedEntries[0]);
        }

        /// <summary>Called by the View after EditInventoryDialog returns OK.</summary>
        public void OnInventoryEdited()
        {
            LoadInventory();
            RequestInfo?.Invoke("Success", "Inventory entry updated successfully!");
        }

        private void Archive()
        {
            var checkedEntries = PagedInventory.Where(i => i.Selected).ToList();

            if (checkedEntries.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one entry to archive.");
                return;
            }

            RequestArchiveConfirm?.Invoke(checkedEntries);
        }

        /// <summary>Called by the View after the archive-reason dialog returns OK.</summary>
        public void ArchiveEntries(List<InventoryViewDto> entries, string reason)
        {
            foreach (var entry in entries)
                ArchiveInventoryEntry(entry.InvId, reason);

            LoadInventory();
        }

        private void ArchiveInventoryEntry(int invId, string reason)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            const string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Inventory', @InvId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@InvId", invId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            RequestInfo?.Invoke("Success", "Inventory entry archived successfully!\n\nYou can view archived entries in the Archive page.");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                RequestErrorInventory?.Invoke("Error", $"Database error while archiving inventory entry:\n\n{ex.Message}");
            }
            catch (Exception ex)
            {
                RequestErrorInventory?.Invoke("Error", $"Error archiving inventory entry:\n\n{ex.Message}");
            }
        }
    }
}
