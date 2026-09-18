using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Archive
{
    public partial class ItemArchiveDetailsDialog : Form
    {
        private readonly string _connectionString;
        private readonly int _itemId;
        private readonly int _archiveId;

        private Panel headerPanel;
        private Label lblTitle;
        private Panel archiveInfoPanel;
        private Panel itemDetailsPanel;
        private Button btnClose;
        private Button btnRestore;

        public ItemArchiveDetailsDialog(int itemId, int archiveId, string connectionString)
        {
            _itemId = itemId;
            _archiveId = archiveId;
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            InitializeComponent();
            InitializeCustomControls();
            LoadArchiveDetails();
        }

        private void InitializeCustomControls()
        {
            // Configure form
            Text = $"Item Archive Details";
            Size = new Size(700, 700);
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
                BackColor = Color.FromArgb(255, 250, 250),
                AutoScroll = true
            };

            // Item Details Panel
            itemDetailsPanel = new Panel
            {
                Location = new Point(20, 300),
                Width = 640,
                Height = 320,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 250, 255),
                AutoScroll = true
            };

            // Close Button
            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(540, 635),
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
                Location = new Point(410, 635),
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
            Controls.AddRange(new Control[] { headerPanel, archiveInfoPanel, itemDetailsPanel, btnRestore, btnClose });
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Restore this item from archive?",
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
                            RestoreArchiveStatus(con, tx, "Item", _itemId);

                            using (var cmd = new SqlCommand("UPDATE dbo.Item SET Active = 1 WHERE ItemId = @ItemId", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", _itemId);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand("SELECT SerialNumber FROM dbo.Item WHERE ItemId = @ItemId", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@ItemId", _itemId);
                                var result = cmd.ExecuteScalar();
                                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                                {
                                    ItemId = _itemId,
                                    SerialNumber = result == null || result == DBNull.Value ? null : Convert.ToString(result),
                                    Action = "Item Restored",
                                    ActionTime = DateTime.Now,
                                    Direction = "IN",
                                    Status = "Completed",
                                    ReferenceType = "Item",
                                    ReferenceId = _itemId,
                                    Notes = "Item restored from archive.",
                                    CreatedBy = AppSession.CurrentUserName ?? "System"
                                });
                            }

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show("Item restored successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restoring item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreArchiveStatus(SqlConnection con, SqlTransaction tx, string entityType, int entityId)
        {
            using (var cmd = new SqlCommand(@"
                UPDATE dbo.ArchiveStatus
                SET IsArchived = 0,
                    RestoredAt = SYSDATETIME(),
                    RestoredBy = @RestoredBy
                WHERE EntityType = @EntityType
                  AND EntityId = @EntityId
                  AND IsArchived = 1", con, tx))
            {
                cmd.Parameters.AddWithValue("@EntityType", entityType);
                cmd.Parameters.AddWithValue("@EntityId", entityId);
                cmd.Parameters.AddWithValue("@RestoredBy", Yakult.Inventory.App.Session.AppSession.CurrentUserName ?? "System");
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

                    // Load Item Details
                    LoadItemDetails(con);
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

        private void LoadItemDetails(SqlConnection con)
        {
            string sql = @"
                SELECT
                    i.*,
                    c.ConditionName
                FROM dbo.Item i
                LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
                WHERE i.ItemId = @ItemId";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ItemId", _itemId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int yPos = 10;

                        // Section Header
                        var lblSectionHeader = new Label
                        {
                            Text = "ITEM DETAILS",
                            Location = new Point(10, yPos),
                            Width = 600,
                            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                            ForeColor = Color.DarkBlue
                        };
                        itemDetailsPanel.Controls.Add(lblSectionHeader);
                        yPos += 30;

                        // Item ID
                        AddReadOnlyField(itemDetailsPanel, "Item ID:", _itemId.ToString(), 10, yPos, 180);
                        yPos += 30;

                        // Item Name
                        AddReadOnlyField(itemDetailsPanel, "Item Name:", reader["Name"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // Description
                        AddReadOnlyField(itemDetailsPanel, "Description:", reader["Description"]?.ToString() ?? "N/A", 10, yPos, 180, isMultiline: true);
                        yPos += 60;

                        // Category
                        AddReadOnlyField(itemDetailsPanel, "Category:", reader["Category"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // Model Number
                        AddReadOnlyField(itemDetailsPanel, "Model Number:", reader["ModelNumber"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // Serial Number
                        AddReadOnlyField(itemDetailsPanel, "Serial Number:", reader["SerialNumber"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // License Number
                        AddReadOnlyField(itemDetailsPanel, "License Number:", reader["LicenseNumber"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // Amount
                        string amount = reader["Amount"] != DBNull.Value
                            ? Convert.ToDecimal(reader["Amount"]).ToString("C")
                            : "N/A";
                        AddReadOnlyField(itemDetailsPanel, "Amount:", amount, 10, yPos, 180);
                        yPos += 30;

                        // Condition
                        AddReadOnlyField(itemDetailsPanel, "Condition:", reader["ConditionName"]?.ToString() ?? "N/A", 10, yPos, 180);
                        yPos += 30;

                        // Start Date
                        string startDate = reader["StartDate"] != DBNull.Value
                            ? Convert.ToDateTime(reader["StartDate"]).ToString("yyyy-MM-dd")
                            : "N/A";
                        AddReadOnlyField(itemDetailsPanel, "Start Date:", startDate, 10, yPos, 180);
                        yPos += 30;

                        // End Date
                        string endDate = reader["EndDate"] != DBNull.Value
                            ? Convert.ToDateTime(reader["EndDate"]).ToString("yyyy-MM-dd")
                            : "N/A";
                        AddReadOnlyField(itemDetailsPanel, "End Date:", endDate, 10, yPos, 180);
                        yPos += 30;

                        // Remarks
                        AddReadOnlyField(itemDetailsPanel, "Remarks:", reader["Remarks"]?.ToString() ?? "N/A", 10, yPos, 180, isMultiline: true);
                    }
                    else
                    {
                        itemDetailsPanel.Controls.Add(new Label
                        {
                            Text = "Item record not found. It may have been permanently deleted.",
                            Location = new Point(10, 40),
                            ForeColor = Color.Red,
                            AutoSize = true,
                            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
                        });
                    }
                }
            }
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

    }
}
