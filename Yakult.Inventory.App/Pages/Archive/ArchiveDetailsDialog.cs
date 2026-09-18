using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Archive
{
    public partial class ArchiveDetailsDialog : Form
    {
        private readonly string _connectionString;
        private readonly int _archiveId;
        private readonly string _entityType;
        private readonly int _entityId;

        private Panel headerPanel;
        private Label lblTitle;
        private Panel archiveInfoPanel;
        private Panel entityDetailsPanel;
        private Button btnClose;
        private Button btnRestore;

        public ArchiveDetailsDialog(int archiveId, string entityType, int entityId, string connectionString)
        {
            _archiveId = archiveId;
            _entityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            _entityId = entityId;
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            InitializeComponent();
            LoadArchiveDetails();
        }

        private void InitializeComponent()
        {
            // Configure form
            Text = $"{_entityType} Archive Details";
            Size = new Size(700, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScroll = true;
            BackColor = Color.White;

            // Header Panel
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0, 120, 215),
                Padding = new Padding(20, 10, 20, 10)
            };

            lblTitle = new Label
            {
                Text = $"Archive Record Details",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };

            headerPanel.Controls.Add(lblTitle);

            // Archive Info Panel
            archiveInfoPanel = new Panel
            {
                Location = new Point(20, 80),
                Width = 640,
                Height = 200,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(255, 250, 250)
            };

            // Entity Details Panel
            entityDetailsPanel = new Panel
            {
                Location = new Point(20, 300),
                Width = 640,
                Height = 220,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 250, 255),
                AutoScroll = true
            };

            // Close Button
            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(540, 535),
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(100, 100, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();

            btnRestore = new Button
            {
                Text = "Restore",
                Location = new Point(410, 535),
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += BtnRestore_Click;

            // Add controls to form
            Controls.AddRange(new Control[] { headerPanel, archiveInfoPanel, entityDetailsPanel, btnRestore, btnClose });
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                $"Restore this {_entityType} from archive?",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            RestoreEntityCascade(con, tx, _entityType, _entityId);
                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show("Restored successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring record: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreEntityCascade(SqlConnection con, SqlTransaction tx, string entityType, int entityId)
        {
            RestoreArchiveStatus(con, tx, entityType, entityId);

            switch (entityType)
            {
                case "Set":
                    RestoreSetCascade(con, tx, entityId);
                    break;
                case "Request":
                    RestoreRequestCascade(con, tx, entityId);
                    break;
                case "Inventory":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Inventory SET Active = 1 WHERE InvId = @Id",
                        ("@Id", entityId));
                    break;
                case "Item":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Item SET Active = 1 WHERE ItemId = @Id",
                        ("@Id", entityId));
                    LogItemRestored(con, tx, entityId);
                    break;
                case "Company":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Company SET Active = 1 WHERE ComId = @Id",
                        ("@Id", entityId));
                    break;
                case "Department":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Department SET Active = 1 WHERE DeptId = @Id",
                        ("@Id", entityId));
                    break;
                case "Branch":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Branch SET Active = 1 WHERE BranchId = @Id",
                        ("@Id", entityId));
                    break;
                case "Employee":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Employee SET Active = 1 WHERE EmpId = @Id",
                        ("@Id", entityId));
                    break;
                case "Vendor":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.Vendor SET IsActive = 1 WHERE VendorID = @Id",
                        ("@Id", entityId));
                    break;
                case "EmptyCartridge":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.EmptyCartridge SET Status = 'Pending', VendorBatchId = NULL WHERE EmptyCartridgeId = @Id",
                        ("@Id", entityId));
                    break;
                case "ItemCategory":
                    ExecuteNonQuery(con, tx,
                        "UPDATE dbo.ItemCategory SET Active = 1 WHERE CategoryId = @Id",
                        ("@Id", entityId));
                    break;
            }
        }

        private void LogItemRestored(SqlConnection con, SqlTransaction tx, int itemId)
        {
            try
            {
                string serial = null;
                using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    var result = cmd.ExecuteScalar();
                    serial = result == null || result == DBNull.Value ? null : Convert.ToString(result);
                }
                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                {
                    ItemId = itemId,
                    SerialNumber = serial,
                    Action = "Item Restored",
                    ActionTime = DateTime.Now,
                    Direction = "IN",
                    Status = "Completed",
                    ReferenceType = "Item",
                    ReferenceId = itemId,
                    Notes = "Item restored from archive.",
                    CreatedBy = AppSession.CurrentUserName ?? "System"
                });
            }
            catch { /* best-effort */ }
        }

        private void RestoreSetCascade(SqlConnection con, SqlTransaction tx, int setId)
        {
            ExecuteNonQuery(con, tx,
                "UPDATE dbo.[Set] SET Active = 1 WHERE SetId = @SetId",
                ("@SetId", setId));

            // Restore archived requests for this set
            ExecuteNonQuery(con, tx, @"
                UPDATE r
                SET r.Active = 1
                FROM dbo.Request r
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Request' AND a.EntityId = r.ReqId
                WHERE r.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId));

            ExecuteNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0,
                    a.RestoredAt = SYSDATETIME(),
                    a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Request r ON a.EntityType = 'Request' AND a.EntityId = r.ReqId
                WHERE r.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));

            // Restore archived inventory for this set
            ExecuteNonQuery(con, tx, @"
                UPDATE inv
                SET inv.Active = 1
                FROM dbo.Inventory inv
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId));

            ExecuteNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0,
                    a.RestoredAt = SYSDATETIME(),
                    a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Inventory inv ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.SetId = @SetId AND a.IsArchived = 1",
                ("@SetId", setId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));

            // Restore items archived explicitly "with Set <id>"
            ExecuteNonQuery(con, tx, @"
                UPDATE i
                SET i.Active = 1
                FROM dbo.Item i
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Item' AND a.EntityId = i.ItemId
                WHERE a.IsArchived = 1
                  AND (
                        a.ArchiveReason LIKE ('Archived with Set ' + CAST(@SetId AS NVARCHAR(20)) + '%')
                     OR a.ArchiveReason LIKE ('Archived with Invoice ' + CAST(@SetId AS NVARCHAR(20)) + '%')
                  )",
                ("@SetId", setId));

            ExecuteNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0,
                    a.RestoredAt = SYSDATETIME(),
                    a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                WHERE a.EntityType = 'Item'
                  AND a.IsArchived = 1
                  AND (
                        a.ArchiveReason LIKE ('Archived with Set ' + CAST(@SetId AS NVARCHAR(20)) + '%')
                     OR a.ArchiveReason LIKE ('Archived with Invoice ' + CAST(@SetId AS NVARCHAR(20)) + '%')
                  )",
                ("@SetId", setId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));
        }

        private void RestoreRequestCascade(SqlConnection con, SqlTransaction tx, int reqId)
        {
            ExecuteNonQuery(con, tx,
                "UPDATE dbo.Request SET Active = 1 WHERE ReqId = @ReqId",
                ("@ReqId", reqId));

            // Restore archived inventory for this request
            ExecuteNonQuery(con, tx, @"
                UPDATE inv
                SET inv.Active = 1
                FROM dbo.Inventory inv
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.ReqId = @ReqId AND a.IsArchived = 1",
                ("@ReqId", reqId));

            ExecuteNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0,
                    a.RestoredAt = SYSDATETIME(),
                    a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                INNER JOIN dbo.Inventory inv ON a.EntityType = 'Inventory' AND a.EntityId = inv.InvId
                WHERE inv.ReqId = @ReqId AND a.IsArchived = 1",
                ("@ReqId", reqId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));

            // Restore item archived explicitly "with Request <id>"
            ExecuteNonQuery(con, tx, @"
                UPDATE i
                SET i.Active = 1
                FROM dbo.Item i
                INNER JOIN dbo.ArchiveStatus a ON a.EntityType = 'Item' AND a.EntityId = i.ItemId
                WHERE a.IsArchived = 1
                  AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%')",
                ("@ReqId", reqId));

            ExecuteNonQuery(con, tx, @"
                UPDATE a
                SET a.IsArchived = 0,
                    a.RestoredAt = SYSDATETIME(),
                    a.RestoredBy = @RestoredBy
                FROM dbo.ArchiveStatus a
                WHERE a.EntityType = 'Item'
                  AND a.IsArchived = 1
                  AND a.ArchiveReason LIKE ('Archived with Request ' + CAST(@ReqId AS NVARCHAR(20)) + '%')",
                ("@ReqId", reqId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));
        }

        private void RestoreArchiveStatus(SqlConnection con, SqlTransaction tx, string entityType, int entityId)
        {
            ExecuteNonQuery(con, tx, @"
                UPDATE dbo.ArchiveStatus
                SET IsArchived = 0,
                    RestoredAt = SYSDATETIME(),
                    RestoredBy = @RestoredBy
                WHERE EntityType = @EntityType
                  AND EntityId = @EntityId
                  AND IsArchived = 1",
                ("@EntityType", entityType),
                ("@EntityId", entityId),
                ("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System"));
        }

        private void ExecuteNonQuery(SqlConnection con, SqlTransaction tx, string sql, params (string Name, object Value)[] parameters)
        {
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                foreach (var p in parameters)
                {
                    cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
                }

                cmd.ExecuteNonQuery();
            }
        }

        private void LoadArchiveDetails()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    // Load Archive Metadata
                    LoadArchiveMetadata(con);

                    // Load Entity Details
                    LoadEntityDetails(con);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading archive details: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadArchiveMetadata(SqlConnection con)
        {
            string sql = @"
                SELECT IsArchived, ArchivedAt, ArchivedBy, ArchiveReason, RestoredAt, RestoredBy
                FROM dbo.ArchiveStatus
                WHERE ArchiveId = @ArchiveId";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ArchiveId", _archiveId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        bool isArchived = reader["IsArchived"] != DBNull.Value && Convert.ToBoolean(reader["IsArchived"]);
                        if (btnRestore != null)
                            btnRestore.Enabled = isArchived;

                        int yPos = 10;

                        // Section Header
                        var lblSectionHeader = new Label
                        {
                            Text = "ARCHIVE INFORMATION",
                            Location = new Point(10, yPos),
                            Width = 600,
                            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                            ForeColor = Color.DarkRed
                        };
                        archiveInfoPanel.Controls.Add(lblSectionHeader);
                        yPos += 30;

                        // Archive ID
                        AddReadOnlyField(archiveInfoPanel, "Archive ID:", _archiveId.ToString(), 10, yPos, 180);
                        yPos += 30;

                        // Archived At
                        string archivedAt = reader["ArchivedAt"] != DBNull.Value
                            ? Convert.ToDateTime(reader["ArchivedAt"]).ToString("yyyy-MM-dd HH:mm:ss")
                            : "N/A";
                        AddReadOnlyField(archiveInfoPanel, "Archived At:", archivedAt, 10, yPos, 180);
                        yPos += 30;

                        // Archived By
                        string archivedBy = reader["ArchivedBy"]?.ToString() ?? "N/A";
                        AddReadOnlyField(archiveInfoPanel, "Archived By:", archivedBy, 10, yPos, 180);
                        yPos += 30;

                        // Archive Reason
                        string reason = reader["ArchiveReason"]?.ToString() ?? "N/A";
                        AddReadOnlyField(archiveInfoPanel, "Archive Reason:", reason, 10, yPos, 180, isMultiline: true);
                    }
                }
            }
        }

        private void LoadEntityDetails(SqlConnection con)
        {
            string sql = "";
            string sectionTitle = $"{_entityType.ToUpper()} DETAILS";

            switch (_entityType)
            {
                case "Department":
                    sql = "SELECT DeptId, Name FROM dbo.Department WHERE DeptId = @EntityId";
                    break;
                case "Branch":
                    sql = "SELECT BranchId, Name FROM dbo.Branch WHERE BranchId = @EntityId";
                    break;
                case "Employee":
                    sql = "SELECT EmpId, Name FROM dbo.Employee WHERE EmpId = @EntityId";
                    break;
                case "Company":
                    sql = "SELECT ComId, Name, Address FROM dbo.Company WHERE ComId = @EntityId";
                    break;
                case "Vendor":
                    sql = "SELECT VendorID, VendorName, Address, TIN FROM dbo.Vendor WHERE VendorID = @EntityId";
                    break;
                case "Set":
                    sql = @"
                        SELECT
                            s.SetCode                                              AS [Set Code],
                            s.SetType                                              AS [Type],
                            s.Status                                               AS [Status],
                            ISNULL(s.DocumentNumber,  '')                          AS [Document #],
                            ISNULL(s.ReferenceNumber, '')                          AS [Reference #],
                            ISNULL(CONVERT(VARCHAR(20), s.StartDate,    101), '')  AS [Start Date],
                            ISNULL(CONVERT(VARCHAR(20), s.EndDate,      101), '')  AS [End Date],
                            ISNULL(CONVERT(VARCHAR(20), s.DispatchDate, 101), '')  AS [Dispatch Date],
                            ISNULL(s.ComputerName, '')                             AS [Computer Name],
                            ISNULL(s.IPAddress,    '')                             AS [IP Address],
                            ISNULL(s.UpgradeReason,'')                             AS [Upgrade Reason],
                            ISNULL(s.Remarks,      '')                             AS [Remarks],
                            CONVERT(VARCHAR(20), s.CreatedAt, 120)                 AS [Created At],
                            ISNULL(u.Name, CAST(s.CreatedBy AS VARCHAR(20)))       AS [Created By],
                            ISNULL(co.Name, '')                                    AS [Company],
                            ISNULL(
                                (SELECT TOP 1 e.Name FROM dbo.Request r2
                                 INNER JOIN dbo.Employee e ON r2.EmpId = e.EmpId
                                 WHERE r2.SetId = s.SetId ORDER BY r2.ReqId), '') AS [Employee],
                            (SELECT COUNT(*) FROM dbo.Request r WHERE r.SetId = s.SetId) AS [Item Count]
                        FROM dbo.[Set] s
                        LEFT JOIN dbo.[User]   u  ON s.CreatedBy = u.UserId
                        LEFT JOIN dbo.Company  co ON s.ComId     = co.ComId
                        WHERE s.SetId = @EntityId";
                    break;
                case "Request":
                    sql = @"
                        SELECT
                            r.ReqId                                              AS [Request ID],
                            r.Status                                             AS [Status],
                            ISNULL(r.Description, '')                           AS [Description],
                            r.Quantity                                           AS [Quantity],
                            ISNULL(r.Remarks, '')                               AS [Remarks],
                            ISNULL(CONVERT(VARCHAR(20), r.DateCreated, 120), '') AS [Date Created],
                            CASE
                                WHEN crm.CartridgeModel IS NOT NULL THEN crm.CartridgeModel
                                ELSE ISNULL(i.Name, 'N/A')
                            END                                                  AS [Item Name],
                            ISNULL(i.Category, 'N/A')                           AS [Category],
                            COALESCE(crm.CartridgeModel, i.ModelNumber, 'N/A')  AS [Model Number],
                            ISNULL(i.SerialNumber, 'N/A')                       AS [Serial Number],
                            ISNULL(e.Name,          'N/A')                      AS [Employee],
                            ISNULL(e.EmployeeNumber,'N/A')                      AS [Employee Number]
                        FROM dbo.Request r
                        LEFT JOIN dbo.Item                  i   ON r.ItemId  = i.ItemId
                        LEFT JOIN dbo.Employee              e   ON r.EmpId   = e.EmpId
                        LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                        WHERE r.ReqId = @EntityId";
                    break;
                case "EmptyCartridge":
                    sql = @"
                        SELECT
                            ec.EmptyCartridgeId                                        AS [ID],
                            ISNULL(cm.ModelNumber, 'N/A')                             AS [Cartridge Model],
                            ec.Quantity                                                AS [Qty],
                            ec.Status                                                  AS [Status],
                            COALESCE(c.ConditionName, ec.ConditionStatus, 'N/A')      AS [Condition],
                            ISNULL(ec.RefillStatus, 'N/A')                            AS [Refill Status],
                            ISNULL(CONVERT(VARCHAR(20), ec.ReturnedAt, 120), '')       AS [Returned At],
                            ISNULL(ec.ReturnedBy, '')                                  AS [Returned By],
                            ISNULL(v.VendorName, 'N/A')                               AS [Vendor / Supplier],
                            ISNULL(CAST(ec.VendorBatchId AS VARCHAR(20)), 'N/A')      AS [Batch ID],
                            ISNULL(e.Name, 'N/A')                                     AS [Employee],
                            ISNULL(br.Name, 'N/A')                                    AS [Branch],
                            ISNULL(d.Name, 'N/A')                                     AS [Department],
                            ISNULL(CAST(ec.ReqId AS VARCHAR(20)), 'N/A')              AS [Request ID],
                            CASE
                                WHEN i.ItemId IS NOT NULL
                                THEN i.Name + ISNULL(' (' + i.ModelNumber + ')', '')
                                ELSE 'N/A'
                            END                                                        AS [Source Item],
                            ISNULL(CONVERT(VARCHAR(20), ec.DateModified, 120), '')    AS [Last Modified],
                            ISNULL(ec.Remarks, '')                                     AS [Remarks]
                        FROM dbo.EmptyCartridge ec
                        LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
                        LEFT JOIN dbo.Condition      c  ON ec.ConditionId      = c.ConditionId
                        LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
                        LEFT JOIN dbo.Employee       e  ON ec.EmpId            = e.EmpId
                        LEFT JOIN dbo.Branch         br ON ec.BranchId         = br.BranchId
                        LEFT JOIN dbo.Department     d  ON ec.DeptId           = d.DeptId
                        LEFT JOIN dbo.Item           i  ON ec.SourceItemId     = i.ItemId
                        WHERE ec.EmptyCartridgeId = @EntityId";
                    break;
                case "Inventory":
                    sql = "SELECT InvId, InvDate, Remarks FROM dbo.Inventory WHERE InvId = @EntityId";
                    break;
                default:
                    entityDetailsPanel.Controls.Add(new Label
                    {
                        Text = "Entity details not available for this type.",
                        Location = new Point(10, 10),
                        ForeColor = Color.Gray,
                        AutoSize = true
                    });
                    return;
            }

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EntityId", _entityId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int yPos = 10;

                        var lblSectionHeader = new Label
                        {
                            Text = sectionTitle,
                            Location = new Point(10, yPos),
                            Width = 600,
                            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                            ForeColor = Color.DarkBlue
                        };
                        entityDetailsPanel.Controls.Add(lblSectionHeader);
                        yPos += 30;

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string fieldName  = reader.GetName(i);
                            string fieldValue = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();

                            AddReadOnlyField(entityDetailsPanel, fieldName + ":", fieldValue, 10, yPos, 180);
                            yPos += 30;
                        }

                        // Expand panel scroll area to fit all fields
                        entityDetailsPanel.AutoScrollMinSize = new System.Drawing.Size(0, yPos + 10);
                    }
                    else
                    {
                        entityDetailsPanel.Controls.Add(new Label
                        {
                            Text = "Entity record not found. It may have been permanently deleted.",
                            Location = new Point(10, 40),
                            ForeColor = Color.Red,
                            AutoSize = true,
                            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
                        });
                    }
                }
            }

            // For Set: add a table showing all items/requests in the set
            if (_entityType == "Set")
                LoadSetItemsSection(con, _entityId);
        }

        private void LoadSetItemsSection(SqlConnection con, int setId)
        {
            int sectionTop = entityDetailsPanel.Bottom + 15;

            var lblSetItems = new Label
            {
                Text = "ITEMS IN THIS SET",
                Location = new Point(20, sectionTop),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 94, 32)
            };
            Controls.Add(lblSetItems);

            var dgvItems = new System.Windows.Forms.DataGridView
            {
                Location  = new Point(20, sectionTop + 25),
                Size      = new Size(640, 185),
                AutoGenerateColumns    = true,
                ReadOnly               = true,
                AllowUserToAddRows     = false,
                AllowUserToDeleteRows  = false,
                AllowUserToResizeRows  = false,
                RowHeadersVisible      = false,
                BackgroundColor        = Color.White,
                BorderStyle            = BorderStyle.None,
                SelectionMode          = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode    = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(27, 94, 32),
                    ForeColor = Color.White,
                    Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 8.5F)
                },
                RowTemplate = { Height = 26 }
            };

            const string sql = @"
                SELECT
                    r.ReqId                           AS [Req ID],
                    r.Status                          AS [Status],
                    ISNULL(r.Description, '')         AS [Description],
                    r.Quantity                        AS [Qty],
                    CASE
                        WHEN crm.CartridgeModel IS NOT NULL THEN crm.CartridgeModel
                        ELSE ISNULL(i.Name, 'N/A')
                    END                               AS [Item Name],
                    COALESCE(crm.CartridgeModel, i.ModelNumber, 'N/A') AS [Model #],
                    ISNULL(i.Category,     'N/A')     AS [Category],
                    ISNULL(i.SerialNumber, 'N/A')     AS [Serial #],
                    ISNULL(e.Name,         'N/A')     AS [Employee],
                    ISNULL(r.Remarks,      '')         AS [Remarks]
                FROM dbo.Request r
                LEFT JOIN dbo.Item                  i   ON r.ItemId  = i.ItemId
                LEFT JOIN dbo.Employee              e   ON r.EmpId   = e.EmpId
                LEFT JOIN dbo.CartridgeRequestModel crm ON crm.ReqId = r.ReqId
                WHERE r.SetId = @SetId
                ORDER BY r.ReqId";

            using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@SetId", setId);
                var dt = new System.Data.DataTable();
                using (var adapter = new System.Data.SqlClient.SqlDataAdapter(cmd))
                    adapter.Fill(dt);
                dgvItems.DataSource = dt;
            }

            Controls.Add(dgvItems);

            // Push buttons below the items section and resize the form
            int newBtnTop = dgvItems.Bottom + 20;
            btnRestore.Top = newBtnTop;
            btnClose.Top   = newBtnTop;
            ClientSize = new Size(ClientSize.Width, newBtnTop + btnClose.Height + 20);
        }

        private void AddReadOnlyField(Panel panel, string labelText, string value, int x, int y, int labelWidth, bool isMultiline = false)
        {
            var label = new Label
            {
                Text = labelText,
                Location = new Point(x, y + 3),
                Width = labelWidth,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var textBox = new TextBox
            {
                Text = value,
                Location = new Point(x + labelWidth + 10, y),
                Width = 420,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Multiline = isMultiline,
                Height = isMultiline ? 50 : 20,
                ScrollBars = isMultiline ? ScrollBars.Vertical : ScrollBars.None
            };

            panel.Controls.Add(label);
            panel.Controls.Add(textBox);
        }

        private string FormatFieldName(string fieldName)
        {
            // Add spaces before capital letters and handle ID
            var result = System.Text.RegularExpressions.Regex.Replace(fieldName, "(\\B[A-Z])", " $1");
            result = result.Replace("I d", "ID").Replace("i d", "ID");
            return result;
        }
    }
}
