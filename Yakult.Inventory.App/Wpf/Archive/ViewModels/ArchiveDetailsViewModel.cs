using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Archive.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Archive.ViewModels
{
    public class ArchiveDetailsViewModel : ViewModelBase
    {
        private readonly string _connectionString;

        public int    ArchiveId  { get; }
        public string EntityType { get; }
        public int    EntityId   { get; }

        // ── Archive metadata ──────────────────────────────────────────────────
        private bool   _isArchived;
        public bool    IsArchived    { get => _isArchived;   private set => SetField(ref _isArchived,   value); }

        private string _archivedAt    = "—";
        public  string ArchivedAt     { get => _archivedAt;   private set => SetField(ref _archivedAt,   value); }

        private string _archivedBy    = "—";
        public  string ArchivedBy     { get => _archivedBy;   private set => SetField(ref _archivedBy,   value); }

        private string _archiveReason = "—";
        public  string ArchiveReason  { get => _archiveReason; private set => SetField(ref _archiveReason, value); }

        private string _restoredAt    = "—";
        public  string RestoredAt     { get => _restoredAt;   private set => SetField(ref _restoredAt,   value); }

        private string _restoredBy    = "—";
        public  string RestoredBy     { get => _restoredBy;   private set => SetField(ref _restoredBy,   value); }

        public string DialogTitle => $"{EntityType} — Archive Details";
        public string EntityBadge => $"{EntityType} #{EntityId}";

        // ── Dynamic entity fields ─────────────────────────────────────────────
        public ObservableCollection<EntityFieldDto> EntityFields { get; }
            = new ObservableCollection<EntityFieldDto>();

        private string _entityNotFoundMessage;
        public  string EntityNotFoundMessage  { get => _entityNotFoundMessage; private set => SetField(ref _entityNotFoundMessage, value); }
        public  bool   EntityNotFound         => !string.IsNullOrEmpty(_entityNotFoundMessage);

        // ── Set items sub-section ─────────────────────────────────────────────
        public ObservableCollection<SetItemRowDto> SetItems { get; }
            = new ObservableCollection<SetItemRowDto>();

        private bool _showSetItems;
        public bool ShowSetItems { get => _showSetItems; private set => SetField(ref _showSetItems, value); }

        // ── Loading / status ──────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

        private bool _isRestoring;
        public bool IsRestoring { get => _isRestoring; private set => SetField(ref _isRestoring, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage   { get => _statusMessage; private set => SetField(ref _statusMessage, value); }

        public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RestoreCommand { get; }

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<bool> CloseRequested;

        public ArchiveDetailsViewModel(int archiveId, string entityType, int entityId, string connectionString)
        {
            ArchiveId         = archiveId;
            EntityType        = entityType ?? throw new ArgumentNullException(nameof(entityType));
            EntityId          = entityId;
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            RestoreCommand = new RelayCommand(
                () => _ = RestoreAsync(),
                () => IsArchived && !IsRestoring);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    await LoadArchiveMetadataAsync(con);
                    await LoadEntityDetailsAsync(con);
                    if (EntityType == "Set")
                        await LoadSetItemsAsync(con);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading details: " + ex.Message;
                OnPropertyChanged(nameof(HasStatusMessage));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadArchiveMetadataAsync(SqlConnection con)
        {
            const string sql = @"
                SELECT IsArchived, ArchivedAt, ArchivedBy, ArchiveReason, RestoredAt, RestoredBy
                FROM dbo.ArchiveStatus
                WHERE ArchiveId = @ArchiveId";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ArchiveId", ArchiveId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        IsArchived    = reader["IsArchived"] != DBNull.Value && Convert.ToBoolean(reader["IsArchived"]);
                        ArchivedAt    = reader["ArchivedAt"] != DBNull.Value ? Convert.ToDateTime(reader["ArchivedAt"]).ToString("yyyy-MM-dd HH:mm:ss") : "—";
                        ArchivedBy    = reader["ArchivedBy"]?.ToString()   ?? "—";
                        ArchiveReason = reader["ArchiveReason"]?.ToString() ?? "—";
                        RestoredAt    = reader["RestoredAt"] != DBNull.Value ? Convert.ToDateTime(reader["RestoredAt"]).ToString("yyyy-MM-dd HH:mm:ss") : "—";
                        RestoredBy    = reader["RestoredBy"]?.ToString()   ?? "—";
                    }
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task LoadEntityDetailsAsync(SqlConnection con)
        {
            EntityFields.Clear();
            EntityNotFoundMessage = null;

            string sql = BuildEntitySql();
            if (sql == null)
            {
                EntityNotFoundMessage = $"Entity details display is not yet implemented for type '{EntityType}'.";
                OnPropertyChanged(nameof(EntityNotFound));
                return;
            }

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EntityId", EntityId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string name  = reader.GetName(i);
                            string value = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();

                            bool multiline = name == "Description" || name == "Remarks" ||
                                             name == "Address"     || name == "Archive Reason" ||
                                             name == "Upgrade Reason";

                            EntityFields.Add(new EntityFieldDto
                            {
                                FieldName   = name,
                                FieldValue  = value,
                                IsMultiline = multiline
                            });
                        }
                    }
                    else
                    {
                        EntityNotFoundMessage = $"The {EntityType} record (ID {EntityId}) was not found. It may have been permanently deleted from the source table.";
                        OnPropertyChanged(nameof(EntityNotFound));
                    }
                }
            }
        }

        private string BuildEntitySql()
        {
            switch (EntityType)
            {
                case "Item":
                    return @"
                        SELECT
                            i.ItemId                                                   AS [Item ID],
                            ISNULL(i.Name, '')                                         AS [Name],
                            ISNULL(i.Description, '')                                  AS [Description],
                            ISNULL(i.Category, '')                                     AS [Category],
                            ISNULL(i.ModelNumber, '')                                  AS [Model Number],
                            ISNULL(i.SerialNumber, '')                                 AS [Serial Number],
                            ISNULL(i.LicenseNumber, '')                                AS [License Number],
                            CASE WHEN i.Amount IS NOT NULL THEN '₱ ' + FORMAT(i.Amount,'N2') ELSE 'N/A' END
                                                                                       AS [Amount],
                            ISNULL(c.ConditionName, 'N/A')                             AS [Condition],
                            ISNULL(CONVERT(VARCHAR(20), i.StartDate, 101), 'N/A')      AS [Start Date],
                            ISNULL(CONVERT(VARCHAR(20), i.EndDate,   101), 'N/A')      AS [End Date],
                            ISNULL(i.Remarks, '')                                      AS [Remarks]
                        FROM dbo.Item i
                        LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
                        WHERE i.ItemId = @EntityId";

                case "Set":
                    return @"
                        SELECT
                            s.SetCode                                                  AS [Set Code],
                            ISNULL(s.SetType, '')                                      AS [Type],
                            ISNULL(s.Status,  '')                                      AS [Status],
                            ISNULL(s.DocumentNumber,  '')                              AS [Document #],
                            ISNULL(s.ReferenceNumber, '')                              AS [Reference #],
                            ISNULL(CONVERT(VARCHAR(20), s.StartDate,    101), '')      AS [Start Date],
                            ISNULL(CONVERT(VARCHAR(20), s.EndDate,      101), '')      AS [End Date],
                            ISNULL(CONVERT(VARCHAR(20), s.DispatchDate, 101), '')      AS [Dispatch Date],
                            ISNULL(s.ComputerName, '')                                 AS [Computer Name],
                            ISNULL(s.IPAddress,    '')                                 AS [IP Address],
                            ISNULL(s.UpgradeReason,'')                                 AS [Upgrade Reason],
                            ISNULL(s.Remarks,      '')                                 AS [Remarks],
                            CONVERT(VARCHAR(20), s.CreatedAt, 120)                     AS [Created At],
                            ISNULL(u.Name, CAST(s.CreatedBy AS VARCHAR(20)))           AS [Created By],
                            ISNULL(co.Name, '')                                        AS [Company],
                            ISNULL(
                                (SELECT TOP 1 e.Name FROM dbo.Request r2
                                 INNER JOIN dbo.Employee e ON r2.EmpId = e.EmpId
                                 WHERE r2.SetId = s.SetId ORDER BY r2.ReqId), '')      AS [Employee],
                            (SELECT COUNT(*) FROM dbo.Request r WHERE r.SetId = s.SetId)
                                                                                       AS [Item Count]
                        FROM dbo.[Set] s
                        LEFT JOIN dbo.[User]  u  ON s.CreatedBy = u.UserId
                        LEFT JOIN dbo.Company co ON s.ComId     = co.ComId
                        WHERE s.SetId = @EntityId";

                case "Request":
                    return @"
                        SELECT
                            r.ReqId                                                    AS [Request ID],
                            ISNULL(r.Status, '')                                       AS [Status],
                            ISNULL(r.Description, '')                                  AS [Description],
                            r.Quantity                                                 AS [Quantity],
                            ISNULL(r.Remarks, '')                                      AS [Remarks],
                            ISNULL(CONVERT(VARCHAR(20), r.DateCreated, 120), '')       AS [Date Created],
                            CASE
                                WHEN crm.CartridgeModel IS NOT NULL THEN crm.CartridgeModel
                                ELSE ISNULL(i.Name, 'N/A')
                            END                                                        AS [Item Name],
                            ISNULL(i.Category, 'N/A')                                 AS [Category],
                            COALESCE(crm.CartridgeModel, i.ModelNumber, 'N/A')         AS [Model Number],
                            ISNULL(i.SerialNumber, 'N/A')                             AS [Serial Number],
                            ISNULL(e.Name,           'N/A')                           AS [Employee],
                            ISNULL(e.EmployeeNumber, 'N/A')                           AS [Employee Number]
                        FROM dbo.Request r
                        LEFT JOIN dbo.Item                  i   ON r.ItemId  = i.ItemId
                        LEFT JOIN dbo.Employee              e   ON r.EmpId   = e.EmpId
                        LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                        WHERE r.ReqId = @EntityId";

                case "Inventory":
                    return @"
                        SELECT
                            inv.InvId                                                  AS [Inventory ID],
                            ISNULL(CONVERT(VARCHAR(20), inv.InvDate, 101), 'N/A')     AS [Date],
                            ISNULL(inv.Remarks, '')                                    AS [Remarks]
                        FROM dbo.Inventory inv
                        WHERE inv.InvId = @EntityId";

                case "Department":
                    return @"
                        SELECT
                            d.DeptId    AS [Department ID],
                            d.Name      AS [Name]
                        FROM dbo.Department d
                        WHERE d.DeptId = @EntityId";

                case "Branch":
                    return @"
                        SELECT
                            br.BranchId AS [Branch ID],
                            br.Name     AS [Name]
                        FROM dbo.Branch br
                        WHERE br.BranchId = @EntityId";

                case "Employee":
                    return @"
                        SELECT
                            e.EmpId                              AS [Employee ID],
                            ISNULL(e.Name, '')                   AS [Name],
                            ISNULL(e.EmployeeNumber, '')          AS [Employee Number],
                            ISNULL(e.Position, '')               AS [Position],
                            ISNULL(br.Name, 'N/A')               AS [Branch],
                            ISNULL(d.Name,  'N/A')               AS [Department]
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Branch     br ON e.BranchId = br.BranchId
                        LEFT JOIN dbo.Department d  ON e.DeptId   = d.DeptId
                        WHERE e.EmpId = @EntityId";

                case "Company":
                    return @"
                        SELECT
                            co.ComId              AS [Company ID],
                            ISNULL(co.Name, '')   AS [Name],
                            ISNULL(co.Address, '') AS [Address]
                        FROM dbo.Company co
                        WHERE co.ComId = @EntityId";

                case "Vendor":
                    return @"
                        SELECT
                            v.VendorID               AS [Vendor ID],
                            ISNULL(v.VendorName, '') AS [Vendor Name],
                            ISNULL(v.Address, '')    AS [Address],
                            ISNULL(v.TIN, '')        AS [TIN]
                        FROM dbo.Vendor v
                        WHERE v.VendorID = @EntityId";

                case "EmptyCartridge":
                    return @"
                        SELECT
                            ec.EmptyCartridgeId                                              AS [ID],
                            ISNULL(cm.ModelNumber, 'N/A')                                   AS [Cartridge Model],
                            ec.Quantity                                                      AS [Qty],
                            ISNULL(ec.Status, '')                                           AS [Status],
                            COALESCE(c.ConditionName, ec.ConditionStatus, 'N/A')            AS [Condition],
                            ISNULL(ec.RefillStatus, 'N/A')                                  AS [Refill Status],
                            ISNULL(CONVERT(VARCHAR(20), ec.ReturnedAt, 120), '')            AS [Returned At],
                            ISNULL(ec.ReturnedBy, '')                                       AS [Returned By],
                            ISNULL(v.VendorName, 'N/A')                                    AS [Vendor / Supplier],
                            ISNULL(CAST(ec.VendorBatchId AS VARCHAR(20)), 'N/A')            AS [Batch ID],
                            ISNULL(e.Name, 'N/A')                                          AS [Employee],
                            ISNULL(br.Name, 'N/A')                                         AS [Branch],
                            ISNULL(d.Name,  'N/A')                                         AS [Department],
                            ISNULL(CAST(ec.ReqId AS VARCHAR(20)), 'N/A')                   AS [Request ID],
                            CASE WHEN i.ItemId IS NOT NULL THEN i.Name + ISNULL(' (' + i.ModelNumber + ')','') ELSE 'N/A' END
                                                                                            AS [Source Item],
                            ISNULL(CONVERT(VARCHAR(20), ec.DateModified, 120), '')         AS [Last Modified],
                            ISNULL(ec.Remarks, '')                                          AS [Remarks]
                        FROM dbo.EmptyCartridge ec
                        LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                        LEFT JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                        LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
                        LEFT JOIN dbo.Employee       e  ON ec.EmpId            = e.EmpId
                        LEFT JOIN dbo.Branch         br ON ec.BranchId         = br.BranchId
                        LEFT JOIN dbo.Department     d  ON ec.DeptId           = d.DeptId
                        LEFT JOIN dbo.Item           i  ON ec.SourceItemId     = i.ItemId
                        WHERE ec.EmptyCartridgeId = @EntityId";

                case "CartridgeModel":
                    return @"
                        SELECT
                            cm.CartridgeModelId           AS [Model ID],
                            ISNULL(cm.ModelNumber, '')    AS [Model Number]
                        FROM dbo.CartridgeModel cm
                        WHERE cm.CartridgeModelId = @EntityId";

                case "ItemCategory":
                    return @"
                        SELECT
                            ic.CategoryId     AS [Category ID],
                            ISNULL(ic.Name,'') AS [Name]
                        FROM dbo.ItemCategory ic
                        WHERE ic.CategoryId = @EntityId";

                case "Condition":
                    return @"
                        SELECT
                            c.ConditionId                    AS [Condition ID],
                            ISNULL(c.ConditionName, '')      AS [Condition Name]
                        FROM dbo.Condition c
                        WHERE c.ConditionId = @EntityId";

                case "Renewal":
                    return @"
                        SELECT
                            r.RenewalId                                                        AS [Renewal ID],
                            ISNULL(r.RenewalStatus, '')                                        AS [Status],
                            ISNULL(CONVERT(VARCHAR(20), r.NewStartDate, 101), 'N/A')           AS [New Start Date],
                            ISNULL(CONVERT(VARCHAR(20), r.NewEndDate,   101), 'N/A')           AS [New End Date],
                            ISNULL(CAST(r.RenewalYears AS VARCHAR(10)), 'N/A')                 AS [Renewal Years],
                            ISNULL(CAST(r.RenewalCount AS VARCHAR(10)), '0')                   AS [Renewal Count],
                            CASE WHEN r.RenewalAmount IS NOT NULL THEN '₱ ' + FORMAT(r.RenewalAmount,'N2') ELSE 'N/A' END
                                                                                               AS [Amount],
                            ISNULL(r.RenewalNotes, '')                                         AS [Notes],
                            ISNULL(r.PartNumber, '')                                           AS [Part Number]
                        FROM dbo.Renewals r
                        WHERE r.RenewalId = @EntityId";

                default:
                    return null;
            }
        }

        private async Task LoadSetItemsAsync(SqlConnection con)
        {
            SetItems.Clear();
            const string sql = @"
                SELECT
                    r.ReqId                            AS ReqId,
                    ISNULL(r.Status, '')               AS Status,
                    ISNULL(r.Description, '')          AS Description,
                    r.Quantity                         AS Qty,
                    CASE
                        WHEN crm.CartridgeModel IS NOT NULL THEN crm.CartridgeModel
                        ELSE ISNULL(i.Name, 'N/A')
                    END                                AS ItemName,
                    COALESCE(crm.CartridgeModel, i.ModelNumber, 'N/A') AS ModelNumber,
                    ISNULL(i.Category, 'N/A')          AS Category,
                    ISNULL(i.SerialNumber, 'N/A')      AS SerialNumber,
                    ISNULL(e.Name, 'N/A')              AS Employee,
                    ISNULL(r.Remarks, '')              AS Remarks
                FROM dbo.Request r
                LEFT JOIN dbo.Item                  i   ON r.ItemId  = i.ItemId
                LEFT JOIN dbo.Employee              e   ON r.EmpId   = e.EmpId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                WHERE r.SetId = @EntityId
                ORDER BY r.ReqId";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EntityId", EntityId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        SetItems.Add(new SetItemRowDto
                        {
                            ReqId        = reader.GetInt32(0),
                            Status       = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Description  = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Qty          = reader.GetInt32(3),
                            ItemName     = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            ModelNumber  = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Category     = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            SerialNumber = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            Employee     = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            Remarks      = reader.IsDBNull(9) ? "" : reader.GetString(9)
                        });
                    }
                }
            }

            ShowSetItems = SetItems.Count > 0;
        }

        private async Task RestoreAsync()
        {
            IsRestoring   = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            ArchiveRestoreService.RestoreEntityCascade(con, tx, EntityType, EntityId, AppSession.CurrentUserName ?? "System");
                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusMessage = "Restore failed: " + ex.Message;
                OnPropertyChanged(nameof(HasStatusMessage));
            }
            finally
            {
                IsRestoring = false;
            }
        }

    }
}
